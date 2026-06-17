using Android.Animation;
using Android.Gms.Maps;
using Android.Gms.Maps.Model;
using Android.Graphics;
using Android.OS;
using Android.Views;
using Android.Widget;
using AndroidX.Fragment.App;
using Firebase.Firestore;
using Google.Android.Material.FloatingActionButton;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using TimeToSchool.BusinessLogic;
using TimeToSchool.Model;
using TimeToSchool.Service;
using static TimeToSchool.Service.FireBaseHelper;

namespace TimeToSchool.Fragments
{
    public class PublicBusMapFragment : Fragment, IOnMapReadyCallback, GoogleMap.IOnMarkerClickListener
    {
        private GoogleMap _map;
        private RoadsApiService _roadsApi;
        private IListenerRegistration _reg;
        private FirestoreEventListener _listener;
        private BitmapDescriptor _busIcon;
        private readonly Dictionary<string, Marker> _markers = new Dictionary<string, Marker>();
        private readonly Dictionary<string, ActiveBus> _busData = new Dictionary<string, ActiveBus>();
        private readonly Dictionary<string, long> _busTimestampMs = new Dictionary<string, long>();
        private bool _hasStopAutoFocused;
        private bool _hasBusAutoFocused;
        private int _currentIconSizeDp = -1;
        private string _school;
        private string _town;
        private string _busLine;
        private View _panelContent;
        private bool _isPanelExpanded = true;
        private int _panelContentHeight = -1;
        private float _touchStartY;
        private DirectionsApiService _directionsApi;
        private List<BusRoute> _busRoutes;
        private TextView _tvStatusMessage;
        private HorizontalScrollView _hsvEtaChips;
        private LinearLayout _llEtaChips;
        private readonly Dictionary<string, Marker> _stopMarkers = new Dictionary<string, Marker>();
        private readonly Dictionary<string, BusRoute> _stopMarkerRouteMap = new Dictionary<string, BusRoute>();
        private readonly Dictionary<string, string> _stopEtaCache = new Dictionary<string, string>();
        private int _statusPanelVersion;
        private Marker _schoolMarker;
        private readonly List<Polygon> _townPolygons = new List<Polygon>();

        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            string roadsKey = Resources.GetString(Resource.String.roads_api_key);
            string mapsKey  = Resources.GetString(Resource.String.google_maps_key);
            _roadsApi      = new RoadsApiService(roadsKey);
            _directionsApi = new DirectionsApiService(mapsKey, Context);
            return inflater.Inflate(Resource.Layout.fragment_public_bus_map, container, false);
        }

        public override void OnViewCreated(View view, Bundle savedInstanceState)
        {
            base.OnViewCreated(view, savedInstanceState);

            _school  = Arguments?.GetString("school")   ?? string.Empty;
            _town    = Arguments?.GetString("town")     ?? string.Empty;
            _busLine = Arguments?.GetString("bus_line") ?? string.Empty;

            bool anyBus = string.IsNullOrEmpty(_busLine) || _busLine == "Any Available Bus";
            view.FindViewById<TextView>(Resource.Id.tvHeaderLabel).Text =
                $"{_school} - {_town} - {(anyBus ? "כל קו פנוי" : _busLine)}";
            _tvStatusMessage = view.FindViewById<TextView>(Resource.Id.tvStatusMessage);
            _hsvEtaChips     = view.FindViewById<HorizontalScrollView>(Resource.Id.hsvEtaChips);
            _llEtaChips      = view.FindViewById<LinearLayout>(Resource.Id.llEtaChips);
            _tvStatusMessage.Text = "מאתר אוטובוסים...";
            _ = FetchBusRoutesAsync();

            _panelContent = view.FindViewById(Resource.Id.llPanelContent);
            _panelContent.Post(() => _panelContentHeight = _panelContent.Height);

            view.FindViewById(Resource.Id.flHandleArea).Touch += OnHandleTouch;

            var mapFrag = (SupportMapFragment)ChildFragmentManager.FindFragmentById(Resource.Id.mapFragment);
            mapFrag.GetMapAsync(this);

            view.FindViewById<FloatingActionButton>(Resource.Id.fabFocusAll)
                .Click += (s, e) => _ = FocusByPriority();

            view.FindViewById<FloatingActionButton>(Resource.Id.fabBack)
                .Click += (s, e) => Activity?.Finish();
        }

        public void OnMapReady(GoogleMap googleMap)
        {
            Android.Util.Log.Debug(ProManager.TAG, "[MAP] OnMapReady called");
            _map = googleMap;
            _map.SetMapStyle(new MapStyleOptions(@"[
                {""featureType"":""poi"",""stylers"":[{""visibility"":""off""}]},
                {""featureType"":""transit"",""stylers"":[{""visibility"":""off""}]}
            ]"));
            _map.SetInfoWindowAdapter(new PublicBusInfoWindowAdapter(
                LayoutInflater.From(Context), _busData, _markers, _busTimestampMs,
                _stopMarkerRouteMap, _stopEtaCache));
            _map.SetOnMarkerClickListener(this);
            _map.CameraChange += (s, e) => UpdateBusIconForZoom(e.Position.Zoom);
            UpdateBusIconForZoom(_map.CameraPosition.Zoom);
            StartListening();
            PlaceStopMarkersIfReady();
            _ = FetchAndDrawTownBoundaryAsync();
            _ = PlaceSchoolMarkerAsync();
        }

        private int DpToPx(int dp) =>
            (int)(dp * Resources.DisplayMetrics.Density + 0.5f);

        private static int GetIconSizeDp(float zoom)
        {
            float t = (Math.Max(5f, Math.Min(21f, zoom)) - 5f) / 16f;
            return (int)(8f + t * 36f + 0.5f);
        }

        private void UpdateBusIconForZoom(float zoom)
        {
            int newSize = GetIconSizeDp(zoom);
            if (newSize == _currentIconSizeDp) return;
            _currentIconSizeDp = newSize;
            _busIcon = CreateBusIcon(newSize);
            foreach (var m in _markers.Values)
                m.SetIcon(_busIcon);
        }

        private BitmapDescriptor CreateBusIcon(int sizeDp)
        {
            int w = DpToPx(sizeDp);
            int h = (int)(w * 1.2f);
            var output = Bitmap.CreateBitmap(w, h, Bitmap.Config.Argb8888);
            var canvas = new Canvas(output);

            float cx = w / 2f;
            float r  = w * 0.42f;
            float cy = r + 2f;

            var fillPaint = new Paint { AntiAlias = true };
            fillPaint.Color = Color.ParseColor("#1976D2");

            if (sizeDp >= 14)
            {
                var path = new Path();
                path.MoveTo(cx - r * 0.55f, cy + r * 0.55f);
                path.LineTo(cx + r * 0.55f, cy + r * 0.55f);
                path.LineTo(cx, h - 2f);
                path.Close();
                canvas.DrawPath(path, fillPaint);
            }

            canvas.DrawCircle(cx, cy, r, fillPaint);

            if (sizeDp >= 14)
            {
                var borderPaint = new Paint { AntiAlias = true };
                borderPaint.SetStyle(Paint.Style.Stroke);
                borderPaint.Color = Color.White;
                borderPaint.StrokeWidth = Math.Max(2f, w * 0.04f);
                canvas.DrawCircle(cx, cy, r - borderPaint.StrokeWidth / 2f, borderPaint);

                using (var busBmp = BitmapFactory.DecodeResource(Resources, Resource.Drawable.ic_icon_bus))
                {
                    int iconSize = (int)(r * 2 * 0.6f);
                    var scaled = Bitmap.CreateScaledBitmap(busBmp, iconSize, iconSize, true);
                    var tinted = TintBitmap(scaled, Color.White);
                    canvas.DrawBitmap(tinted, cx - iconSize / 2f, cy - iconSize / 2f, null);
                    scaled.Recycle();
                    tinted.Recycle();
                }
            }

            return BitmapDescriptorFactory.FromBitmap(output);
        }

        private static Bitmap TintBitmap(Bitmap src, Color color)
        {
            var result = Bitmap.CreateBitmap(src.Width, src.Height, Bitmap.Config.Argb8888);
            var canvas = new Canvas(result);
            var paint = new Paint();
            paint.SetColorFilter(new PorterDuffColorFilter(color, PorterDuff.Mode.SrcIn));
            canvas.DrawBitmap(src, 0, 0, paint);
            return result;
        }

        private void StartListening()
        {
            string today = DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            _listener = new FirestoreEventListener();
            _listener.getEvent += OnFirestoreUpdate;
            _reg = FirebaseFirestore.Instance
                .Collection("ActiveTrips")
                .WhereEqualTo("date", today)
                .AddSnapshotListener(_listener);
        }

        private async void OnFirestoreUpdate(object sender, FirestoreEventListener.TaskListenerEventArgs e)
        {
            var snapshot = e.Result as QuerySnapshot;
            if (snapshot == null) return;

            var activity = Activity;
            if (activity == null) return;

            var currentIds = new HashSet<string>();

            foreach (DocumentSnapshot item in snapshot.Documents)
            {
                var gp = item.GetGeoPoint("location");
                if (gp == null) continue;

                var ts = item.GetTimestamp("lastUpdated");
                long timestampMs = ts != null
                    ? ts.ToDate().Time
                    : Java.Lang.JavaSystem.CurrentTimeMillis();

                var bus = new ActiveBus
                {
                    FirestoreDocId = item.Id,
                    SchoolName     = item.Get("schoolName")?.ToString(),
                    Town           = item.Get("town")?.ToString(),
                    BusLine        = item.Get("busLine")?.ToString(),
                    DriverName     = item.Get("driverName")?.ToString(),
                    DriverId       = item.Get("driverId")?.ToString(),
                    Status         = item.Get("status")?.ToString(),
                    Date           = item.Get("date")?.ToString(),
                    Latitude       = gp.Latitude,
                    Longitude      = gp.Longitude,
                    IsVisible      = item.Get("isVisible")?.ToString() != "false",
                };

                _busData[bus.FirestoreDocId] = bus;
                _busTimestampMs[bus.FirestoreDocId] = timestampMs;
                currentIds.Add(bus.FirestoreDocId);

                var rawPos = new LatLng(bus.Latitude, bus.Longitude);
                activity.RunOnUiThread(() => UpdateOrAddMarker(bus, rawPos));

                var snapped = await _roadsApi.SnapToRoad(bus.Latitude, bus.Longitude);
                if (snapped != null
                    && (Math.Abs(snapped.Latitude  - rawPos.Latitude)  > 1e-7
                     || Math.Abs(snapped.Longitude - rawPos.Longitude) > 1e-7))
                {
                    if (Activity != null)
                        activity.RunOnUiThread(() => UpdateOrAddMarker(bus, snapped));
                }
            }

            if (Activity != null)
                activity.RunOnUiThread(() =>
                {
                    foreach (var staleId in _markers.Keys.Except(currentIds).ToList())
                    {
                        _markers[staleId].Remove();
                        _markers.Remove(staleId);
                        _busData.Remove(staleId);
                        _busTimestampMs.Remove(staleId);
                    }
                    if (!_hasBusAutoFocused)
                    {
                        _hasBusAutoFocused = true;
                        _ = FocusByPriority(animate: false);
                    }
                    _ = UpdateStatusPanelAsync();
                });
        }

        private void UpdateOrAddMarker(ActiveBus bus, LatLng pos)
        {
            bool visible = MatchesSelection(bus);

            if (_markers.TryGetValue(bus.FirestoreDocId, out var existing))
            {
                existing.Position = pos;
                existing.Visible = visible;
            }
            else
            {
                var marker = _map.AddMarker(
                    new MarkerOptions()
                        .SetPosition(pos)
                        .SetTitle(bus.BusLine)
                        .SetIcon(_busIcon));
                marker.Visible = visible;
                _markers[bus.FirestoreDocId] = marker;
            }
        }

        private bool MatchesSelection(ActiveBus bus)
        {
            if (!bus.IsVisible) return false;
            if (bus.Status == "Active" && !IsTimestampFresh(bus.FirestoreDocId)) return false;
            if (!Eq(bus.SchoolName, _school)) return false;
            if (!Eq(bus.Town, _town)) return false;
            if (!string.IsNullOrEmpty(_busLine) && _busLine != "Any Available Bus")
                if (!Eq(bus.BusLine, _busLine)) return false;
            return true;
        }

        private bool IsTimestampFresh(string docId)
        {
            if (!_busTimestampMs.TryGetValue(docId, out long tsMs) || tsMs == 0) return true;
            return Java.Lang.JavaSystem.CurrentTimeMillis() - tsMs < 3 * 60 * 1000;
        }

        private static bool Eq(string a, string b) =>
            string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

        public bool OnMarkerClick(Marker marker)
        {
            if (_stopMarkerRouteMap.TryGetValue(marker.Id, out var route))
            {
                _ = ShowStopEtaAsync(marker, route);
                return true;
            }
            return false;
        }

        private async Task FocusByPriority(bool animate = true)
        {
            if (_map == null) return;

            var buses = _markers.Values.Where(m => m.Visible).ToList();
            if (buses.Count > 0) { ApplyCameraToMarkers(buses, animate, singleZoom: 15f); return; }

            var stops = _stopMarkers.Values.ToList();
            if (stops.Count > 0) { ApplyCameraToMarkers(stops, animate, singleZoom: 15f, maxZoom: 15f); return; }

            var loc = await Task.Run(() => TryGeocode(_town))
                   ?? await Task.Run(() => TryGeocode(_school));
            if (loc == null) return;

            var capturedLoc = loc;
            Activity?.RunOnUiThread(() =>
            {
                var update = CameraUpdateFactory.NewLatLngZoom(capturedLoc, 14f);
                if (animate) _map.AnimateCamera(update); else _map.MoveCamera(update);
            });
        }

        private async Task PlaceSchoolMarkerAsync()
        {
            if (_map == null || string.IsNullOrEmpty(_school)) return;

            var loc = await Task.Run(() => TryGeocode(_school));
            if (loc == null) return;

            Activity?.RunOnUiThread(() =>
            {
                if (_schoolMarker != null) return;
                _schoolMarker = _map.AddMarker(
                    new MarkerOptions()
                        .SetPosition(loc)
                        .SetTitle(_school)
                        .SetIcon(BitmapDescriptorFactory.DefaultMarker(BitmapDescriptorFactory.HueOrange)));
            });
        }

        private void ApplyCameraToMarkers(List<Marker> targets, bool animate, float singleZoom = 13f, float maxZoom = 0f)
        {
            CameraUpdate update;
            if (targets.Count == 1)
            {
                update = CameraUpdateFactory.NewLatLngZoom(targets[0].Position, singleZoom);
            }
            else
            {
                var bounds = targets.Aggregate(new LatLngBounds.Builder(),
                    (b, m) => { b.Include(m.Position); return b; }).Build();

                if (maxZoom > 0f)
                {
                    // Move silently to derive the bounds zoom, then clamp it.
                    _map.MoveCamera(CameraUpdateFactory.NewLatLngBounds(bounds, 200));
                    float zoom = Math.Min(_map.CameraPosition.Zoom, maxZoom);
                    update = CameraUpdateFactory.NewLatLngZoom(_map.CameraPosition.Target, zoom);
                }
                else
                {
                    update = CameraUpdateFactory.NewLatLngBounds(bounds, 200);
                }
            }
            if (animate) _map.AnimateCamera(update); else _map.MoveCamera(update);
        }

        private LatLng TryGeocode(string query)
        {
            if (string.IsNullOrEmpty(query)) return null;
            try
            {
                var results = new Android.Locations.Geocoder(Context).GetFromLocationName(query, 1);
                if (results?.Count > 0)
                    return new LatLng(results[0].Latitude, results[0].Longitude);
            }
            catch { }
            return null;
        }

        private void OnHandleTouch(object sender, View.TouchEventArgs e)
        {
            switch (e.Event.Action)
            {
                case MotionEventActions.Down:
                    _touchStartY = e.Event.GetY();
                    e.Handled = true;
                    break;
                case MotionEventActions.Up:
                    float delta = e.Event.GetY() - _touchStartY;
                    if (Math.Abs(delta) < 10f)
                        TogglePanel();
                    else if (delta > 0 && !_isPanelExpanded)
                        ExpandPanel();
                    else if (delta < 0 && _isPanelExpanded)
                        CollapsePanel();
                    e.Handled = true;
                    break;
                default:
                    e.Handled = false;
                    break;
            }
        }

        private void TogglePanel()
        {
            if (_isPanelExpanded) CollapsePanel(); else ExpandPanel();
        }

        private void ExpandPanel()
        {
            if (_panelContentHeight < 0)
            {
                _panelContent.Visibility = ViewStates.Visible;
                _isPanelExpanded = true;
                return;
            }
            _panelContent.Visibility = ViewStates.Visible;
            _panelContent.LayoutParameters.Height = 0;
            var anim = ValueAnimator.OfInt(0, _panelContentHeight);
            anim.SetDuration(260);
            anim.Update += (s, e) =>
            {
                _panelContent.LayoutParameters.Height = ((Java.Lang.Integer)((ValueAnimator)s).AnimatedValue).IntValue();
                _panelContent.RequestLayout();
            };
            anim.AnimationEnd += (s, e) =>
                _panelContent.LayoutParameters.Height = ViewGroup.LayoutParams.WrapContent;
            anim.Start();
            _isPanelExpanded = true;
        }

        private void CollapsePanel()
        {
            if (_panelContentHeight < 0) _panelContentHeight = _panelContent.Height;
            var anim = ValueAnimator.OfInt(_panelContent.Height, 0);
            anim.SetDuration(260);
            anim.Update += (s, e) =>
            {
                _panelContent.LayoutParameters.Height = ((Java.Lang.Integer)((ValueAnimator)s).AnimatedValue).IntValue();
                _panelContent.RequestLayout();
            };
            anim.AnimationEnd += (s, e) =>
            {
                _panelContent.Visibility = ViewStates.Gone;
                _panelContent.LayoutParameters.Height = ViewGroup.LayoutParams.WrapContent;
            };
            anim.Start();
            _isPanelExpanded = false;
        }

        private async Task FetchAndDrawTownBoundaryAsync()
        {
            Android.Util.Log.Debug(ProManager.TAG, $"[TownBoundary] entered — _map={_map != null}, _town='{_town}'");
            if (_map == null || string.IsNullOrEmpty(_town)) return;

            List<IList<LatLng>> rings = null;

            // Try Nominatim first for the real municipal boundary.
            string q   = Uri.EscapeDataString(_town);
            string url = $"https://nominatim.openstreetmap.org/search?q={q}&format=json&polygon_geojson=1&limit=1&countrycodes=il";
            Android.Util.Log.Debug(ProManager.TAG, $"[TownBoundary] fetching: {url}");
            try
            {
                using var client = new HttpClient(new Xamarin.Android.Net.AndroidClientHandler());
                client.DefaultRequestHeaders.UserAgent.ParseAdd("TimeToSchool/1.0");
                string json = await client.GetStringAsync(url);
                Android.Util.Log.Debug(ProManager.TAG, $"[TownBoundary] got {json.Length} chars");
                rings = ParseTownRings(json);
                Android.Util.Log.Debug(ProManager.TAG, $"[TownBoundary] parsed {rings.Count} ring(s)");
            }
            catch (Exception ex)
            {
                Android.Util.Log.Error(ProManager.TAG, $"[TownBoundary] error: {ex.Message}");
            }

            // Fallback: geocode the town center and approximate with a circle polygon.
            if (rings == null || rings.Count == 0)
            {
                Android.Util.Log.Warn(ProManager.TAG, "[TownBoundary] falling back to circle approximation");
                var center = await Task.Run(() => TryGeocode(_town));
                if (center == null) return;
                rings = new List<IList<LatLng>> { CirclePolygon(center, 1500) };
            }

            Activity?.RunOnUiThread(() => DrawTownPolygons(rings));
        }

        private static IList<LatLng> CirclePolygon(LatLng center, double radiusMeters, int points = 64)
        {
            var ring = new List<LatLng>(points);
            double lat = center.Latitude  * Math.PI / 180;
            double lng = center.Longitude * Math.PI / 180;
            double d   = radiusMeters / 6_371_000.0;
            for (int i = 0; i < points; i++)
            {
                double bearing = 2 * Math.PI * i / points;
                double lat2    = Math.Asin(Math.Sin(lat) * Math.Cos(d) + Math.Cos(lat) * Math.Sin(d) * Math.Cos(bearing));
                double lng2    = lng + Math.Atan2(Math.Sin(bearing) * Math.Sin(d) * Math.Cos(lat), Math.Cos(d) - Math.Sin(lat) * Math.Sin(lat2));
                ring.Add(new LatLng(lat2 * 180 / Math.PI, lng2 * 180 / Math.PI));
            }
            return ring;
        }

        private static List<IList<LatLng>> ParseTownRings(string json)
        {
            var result  = new List<IList<LatLng>>();
            var arr     = JArray.Parse(json);
            if (arr.Count == 0) return result;

            var geojson = arr[0]["geojson"];
            if (geojson == null) return result;

            string type  = geojson["type"]?.ToString();
            var    coords = geojson["coordinates"] as JArray;
            if (coords == null) return result;

            if (type == "Polygon")
            {
                var ring = CoordRingToLatLng(coords[0] as JArray);
                if (ring.Count >= 3) result.Add(ring);
            }
            else if (type == "MultiPolygon")
            {
                foreach (var poly in coords)
                {
                    var ring = CoordRingToLatLng((poly as JArray)?[0] as JArray);
                    if (ring.Count >= 3) result.Add(ring);
                }
            }
            return result;
        }

        private static List<LatLng> CoordRingToLatLng(JArray ring)
        {
            var pts = new List<LatLng>();
            if (ring == null) return pts;
            foreach (var coord in ring)
                pts.Add(new LatLng((double)coord[1], (double)coord[0]));
            return pts;
        }

        private void DrawTownPolygons(List<IList<LatLng>> rings)
        {
            foreach (var p in _townPolygons) p.Remove();
            _townPolygons.Clear();

            if (rings.Count == 0) return;

            // Large rectangle covering the Middle East — avoids the ±180° antimeridian
            // rendering bug in Google Maps SDK while still covering all of Israel at any zoom.
            var world = new List<LatLng>
            {
                new LatLng(60,  10),
                new LatLng(60,  60),
                new LatLng(15,  60),
                new LatLng(15,  10),
            };

            int outsideFill = Color.Argb(25, 20, 20, 50);

            try
            {
                var opts = new PolygonOptions();
                foreach (var pt in world)
                    opts.Add(pt);
                foreach (var ring in rings)
                {
                    var hole = new Java.Util.ArrayList();
                    foreach (var pt in ring) hole.Add(pt);
                    opts.AddHole(hole);
                }
                opts.InvokeFillColor(outsideFill);
                opts.InvokeStrokeColor(Color.Argb(180, 110, 130, 230));
                opts.InvokeStrokeWidth(3f);
                opts.InvokeStrokePattern(new List<PatternItem> { new Dot(), new Gap(12f) });
                opts.Clickable(false);

                _townPolygons.Add(_map.AddPolygon(opts));
                Android.Util.Log.Debug(ProManager.TAG, "[TownBoundary] polygon added to map");
            }
            catch (Exception ex)
            {
                Android.Util.Log.Error(ProManager.TAG, $"[TownBoundary] DrawTownPolygons error: {ex.Message}");
            }
        }

        public override void OnDestroyView()
        {
            if (_listener != null)
                _listener.getEvent -= OnFirestoreUpdate;
            _reg?.Remove();
            _reg = null;
            _listener = null;
            foreach (var m in _stopMarkers.Values) m.Remove();
            _stopMarkers.Clear();
            _stopMarkerRouteMap.Clear();
            foreach (var p in _townPolygons) p.Remove();
            _townPolygons.Clear();
            _schoolMarker?.Remove();
            _schoolMarker = null;
            base.OnDestroyView();
        }

        private async Task FetchBusRoutesAsync()
        {
            _busRoutes = await BusesRepository.GetBusesCollection();
            Activity?.RunOnUiThread(PlaceStopMarkersIfReady);
        }

        private bool MatchesRoute(ActiveBus bus)
        {
            if (!Eq(bus.SchoolName, _school)) return false;
            if (!Eq(bus.Town, _town)) return false;
            if (!string.IsNullOrEmpty(_busLine) && _busLine != "Any Available Bus")
                if (!Eq(bus.BusLine, _busLine)) return false;
            return true;
        }

        private async Task UpdateStatusPanelAsync()
        {
            int version = ++_statusPanelVersion;

            var matching = _busData.Values.Where(MatchesRoute).ToList();

            if (matching.Any(b => b.IsVisible && b.Status == "Active" && IsTimestampFresh(b.FirestoreDocId)))
            {
                ShowStatusText("בנסיעה");
                return;
            }

            var approaching = matching.Where(b => !b.IsVisible && b.Status == "Active" && IsTimestampFresh(b.FirestoreDocId)).ToList();
            if (approaching.Any())
            {
                if (_busRoutes == null)
                    await FetchBusRoutesAsync();
                var etaTasks = approaching.Select(ComputeEtaAsync).ToList();
                var etaResults = await Task.WhenAll(etaTasks);
                if (version != _statusPanelVersion) return;
                Activity?.RunOnUiThread(() => ShowEtaChips(etaResults));
                return;
            }

            if (version != _statusPanelVersion) return;

            if (matching.Any())
            {
                var lines = string.Join(", ", matching.Select(b => $"קו {b.BusLine}").Distinct());
                ShowStatusText($"אוטובוסים שפעלו היום: {lines}");
                return;
            }

            ShowStatusText("אין אוטובוסים פעילים היום");
        }

        private async Task<(int? minutes, string busLine)> ComputeEtaAsync(ActiveBus bus)
        {
            var route = _busRoutes?.FirstOrDefault(r =>
                Eq(r.School, bus.SchoolName) &&
                Eq(r.Town, bus.Town) &&
                Eq(r.BusLine, bus.BusLine));

            if (route?.FirstStopLat == null || route.FirstStopLng == null)
                return (null, bus.BusLine);

            var minutes = await _directionsApi.GetEtaMinutes(
                bus.Latitude, bus.Longitude,
                route.FirstStopLat.Value, route.FirstStopLng.Value);

            return (minutes, bus.BusLine);
        }

        private void ShowStatusText(string text)
        {
            _hsvEtaChips.Visibility = ViewStates.Gone;
            _tvStatusMessage.Text = text;
            _tvStatusMessage.Visibility = ViewStates.Visible;
            _panelContentHeight = -1;
        }

        private void ShowEtaChips((int? minutes, string busLine)[] entries)
        {
            _tvStatusMessage.Visibility = ViewStates.Gone;
            _llEtaChips.RemoveAllViews();

            var prefix = new TextView(Context);
            prefix.Text = "בדרך: ";
            prefix.SetTextColor(Color.White);
            prefix.SetPadding(0, DpToPx(4), DpToPx(4), DpToPx(4));
            _llEtaChips.AddView(prefix);

            foreach (var (minutes, busLine) in entries)
            {
                string label = minutes.HasValue ? $"{minutes}" : "?";
                var chip = new TextView(Context);
                chip.Text = label;
                chip.SetTextColor(Color.White);
                chip.SetBackgroundResource(Resource.Drawable.bg_eta_chip);
                var lp = new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.WrapContent,
                    ViewGroup.LayoutParams.WrapContent);
                lp.SetMargins(0, 0, DpToPx(8), 0);
                chip.LayoutParameters = lp;
                chip.SetPadding(DpToPx(10), DpToPx(4), DpToPx(10), DpToPx(4));
                string capturedLine = busLine;
                chip.Click += (s, e) => ShowBusLineTooltip((View)s, capturedLine);
                _llEtaChips.AddView(chip);
            }

            _hsvEtaChips.Visibility = ViewStates.Visible;
            _panelContentHeight = -1;
        }

        private void ShowBusLineTooltip(View anchor, string busLine)
        {
            var tv = new TextView(Context);
            tv.Text = $"קו {busLine}";
            tv.SetTextColor(Color.White);
            tv.SetBackgroundResource(Resource.Drawable.bg_eta_chip);
            tv.SetPadding(DpToPx(14), DpToPx(8), DpToPx(14), DpToPx(8));

            var popup = new Android.Widget.PopupWindow(
                tv,
                ViewGroup.LayoutParams.WrapContent,
                ViewGroup.LayoutParams.WrapContent,
                true);
            popup.ShowAsDropDown(anchor, 0, -DpToPx(80));
        }

        private void PlaceStopMarkersIfReady()
        {
            if (_map == null || _busRoutes == null) return;
            bool anyBus = string.IsNullOrEmpty(_busLine) || _busLine == "Any Available Bus";
            var icon = CreateStopIcon();

            foreach (var route in _busRoutes)
            {
                if (!Eq(route.School, _school)) continue;
                if (!Eq(route.Town, _town)) continue;
                if (!anyBus && !Eq(route.BusLine, _busLine)) continue;
                if (route.FirstStopLat == null || route.FirstStopLng == null) continue;
                if (_stopMarkers.ContainsKey(route.Id)) continue;

                var marker = _map.AddMarker(
                    new MarkerOptions()
                        .SetPosition(new LatLng(route.FirstStopLat.Value, route.FirstStopLng.Value))
                        .SetTitle(route.BusLine)
                        .SetIcon(icon)
                        .Anchor(0.5f, 1f));
                _stopMarkers[route.Id] = marker;
                _stopMarkerRouteMap[marker.Id] = route;
            }

            if (!_hasStopAutoFocused && !_hasBusAutoFocused)
            {
                _hasStopAutoFocused = true;
                _ = FocusByPriority(animate: false);
            }
        }

        private BitmapDescriptor CreateStopIcon()
        {
            int sizeDp = 22;
            int w = DpToPx(sizeDp);
            int h = (int)(w * 1.4f);
            var bmp = Bitmap.CreateBitmap(w, h, Bitmap.Config.Argb8888);
            var canvas = new Canvas(bmp);

            float cx = w / 2f;
            float r  = w * 0.42f;
            float cy = r + 2f;

            var fill = new Paint { AntiAlias = true };
            fill.Color = Color.ParseColor("#388E3C");

            var tip = new Path();
            tip.MoveTo(cx - r * 0.55f, cy + r * 0.55f);
            tip.LineTo(cx + r * 0.55f, cy + r * 0.55f);
            tip.LineTo(cx, h - 2f);
            tip.Close();
            canvas.DrawPath(tip, fill);
            canvas.DrawCircle(cx, cy, r, fill);

            var border = new Paint { AntiAlias = true };
            border.SetStyle(Paint.Style.Stroke);
            border.Color = Color.White;
            border.StrokeWidth = Math.Max(2f, w * 0.08f);
            canvas.DrawCircle(cx, cy, r - border.StrokeWidth / 2f, border);

            return BitmapDescriptorFactory.FromBitmap(bmp);
        }

        private async Task ShowStopEtaAsync(Marker anchor, BusRoute route)
        {
            _stopEtaCache[anchor.Id] = "מחשב זמן הגעה...";
            anchor.ShowInfoWindow();

            var buses = _busData.Values
                .Where(b => Eq(b.SchoolName, route.School) && Eq(b.Town, route.Town) &&
                            Eq(b.BusLine, route.BusLine) && b.Status == "Active" &&
                            IsTimestampFresh(b.FirestoreDocId))
                .ToList();

            if (!buses.Any())
            {
                _stopEtaCache[anchor.Id] = "אין אוטובוס פעיל כרגע";
                anchor.ShowInfoWindow();
                return;
            }

            var etas = await Task.WhenAll(buses.Select(ComputeEtaAsync));

            Activity?.RunOnUiThread(() =>
            {
                var nums = new List<string>();
                for (int i = 0; i < etas.Length; i++)
                {
                    var (minutes, _) = etas[i];
                    if (!buses[i].IsVisible && minutes.HasValue)
                        nums.Add(minutes.Value.ToString());
                }
                _stopEtaCache[anchor.Id] = nums.Count > 0
                    ? string.Join(", ", nums) + " דקות"
                    : "בדרך";
                anchor.ShowInfoWindow();
            });
        }

        private class PublicBusInfoWindowAdapter : Java.Lang.Object, GoogleMap.IInfoWindowAdapter
        {
            private readonly LayoutInflater _inflater;
            private readonly Dictionary<string, ActiveBus> _busData;
            private readonly Dictionary<string, Marker> _markers;
            private readonly Dictionary<string, long> _timestamps;
            private readonly Dictionary<string, BusRoute> _stopRouteMap;
            private readonly Dictionary<string, string> _stopEtaCache;

            public PublicBusInfoWindowAdapter(
                LayoutInflater inflater,
                Dictionary<string, ActiveBus> busData,
                Dictionary<string, Marker> markers,
                Dictionary<string, long> timestamps,
                Dictionary<string, BusRoute> stopRouteMap,
                Dictionary<string, string> stopEtaCache)
            {
                _inflater     = inflater;
                _busData      = busData;
                _markers      = markers;
                _timestamps   = timestamps;
                _stopRouteMap = stopRouteMap;
                _stopEtaCache = stopEtaCache;
            }

            public View GetInfoWindow(Marker marker)
            {
                if (_stopRouteMap.TryGetValue(marker.Id, out var route))
                {
                    float density = _inflater.Context.Resources.DisplayMetrics.Density;
                    int dp(int v) => (int)(v * density + 0.5f);

                    var ll = new LinearLayout(_inflater.Context) { Orientation = Orientation.Vertical };
                    ll.SetPadding(dp(16), dp(12), dp(16), dp(12));

                    var tvLine = new TextView(_inflater.Context) { Text = $"קו {route.BusLine}" };
                    tvLine.SetTypeface(null, Android.Graphics.TypefaceStyle.Bold);
                    tvLine.TextSize = 15f;
                    ll.AddView(tvLine);

                    string etaText = _stopEtaCache.TryGetValue(marker.Id, out var t) ? t : "...";
                    var tvEta = new TextView(_inflater.Context) { Text = etaText };
                    tvEta.TextSize = 13f;
                    ll.AddView(tvEta);

                    return ll;
                }

                var entry = _markers.FirstOrDefault(kv => kv.Value.Id == marker.Id);
                if (entry.Key == null || !_busData.TryGetValue(entry.Key, out var bus))
                    return null;

                var view = _inflater.Inflate(Resource.Layout.fragment_public_bus_info, null, false);
                view.FindViewById<TextView>(Resource.Id.tv_bus_line).Text = bus.BusLine;
                view.FindViewById<TextView>(Resource.Id.tv_school).Text   = bus.SchoolName;
                view.FindViewById<TextView>(Resource.Id.tv_town).Text     = bus.Town;

                var tvStatus = view.FindViewById<TextView>(Resource.Id.tv_status);
                bool isActive = bus.Status == "Active";
                tvStatus.Text = isActive ? "פעיל" : "לא פעיל";
                tvStatus.SetTextColor(isActive ? Color.ParseColor("#2E7D32") : Color.ParseColor("#C62828"));

                long nowMs = Java.Lang.JavaSystem.CurrentTimeMillis();
                long ageSeconds = _timestamps.TryGetValue(entry.Key, out long tsMs)
                    ? (nowMs - tsMs) / 1000
                    : 0;
                view.FindViewById<TextView>(Resource.Id.tv_seconds_ago).Text =
                    $"עודכן לפני {ageSeconds} שניות";

                return view;
            }

            public View GetInfoContents(Marker marker) => null;
        }
    }
}
