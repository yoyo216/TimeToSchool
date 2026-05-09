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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        private bool _hasAutoFocused;
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

        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            string key = Resources.GetString(Resource.String.roads_api_key);
            _roadsApi = new RoadsApiService(key);
            _directionsApi = new DirectionsApiService(key);
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
                .Click += (s, e) => FocusAllMarkers();

            view.FindViewById<FloatingActionButton>(Resource.Id.fabBack)
                .Click += (s, e) => Activity?.Finish();
        }

        public void OnMapReady(GoogleMap googleMap)
        {
            _map = googleMap;
            _map.SetInfoWindowAdapter(new PublicBusInfoWindowAdapter(
                LayoutInflater.From(Context), _busData, _markers, _busTimestampMs));
            _map.SetOnMarkerClickListener(this);
            _map.CameraChange += (s, e) => UpdateBusIconForZoom(e.Position.Zoom);
            UpdateBusIconForZoom(_map.CameraPosition.Zoom);
            StartListening();
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
            string today = DateTime.Now.ToString("yyyy-MM-dd");
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
                    if (!_hasAutoFocused && _markers.Count > 0)
                    {
                        _hasAutoFocused = true;
                        FocusAllMarkers(animate: false);
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

        public bool OnMarkerClick(Marker marker) => false;

        private void FocusAllMarkers(bool animate = true)
        {
            if (_map == null || _markers.Count == 0) return;

            var targets = _markers.Values.Where(m => m.Visible).ToList();
            if (targets.Count == 0) targets = _markers.Values.ToList();

            CameraUpdate update;
            if (targets.Count == 1)
                update = CameraUpdateFactory.NewLatLngZoom(targets[0].Position, 13f);
            else
            {
                var builder = new LatLngBounds.Builder();
                foreach (var marker in targets)
                    builder.Include(marker.Position);
                update = CameraUpdateFactory.NewLatLngBounds(builder.Build(), 200);
            }

            if (animate)
                _map.AnimateCamera(update);
            else
                _map.MoveCamera(update);
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

        public override void OnDestroyView()
        {
            if (_listener != null)
                _listener.getEvent -= OnFirestoreUpdate;
            _reg?.Remove();
            _reg = null;
            _listener = null;
            base.OnDestroyView();
        }

        private async Task FetchBusRoutesAsync()
        {
            _busRoutes = await BusesRepository.GetBusesCollection();
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
            var matching = _busData.Values.Where(MatchesRoute).ToList();

            if (matching.Any(b => b.IsVisible && b.Status == "Active" && IsTimestampFresh(b.FirestoreDocId)))
            {
                ShowStatusText("בנסיעה");
                return;
            }

            var approaching = matching.Where(b => !b.IsVisible && b.Status == "Active" && IsTimestampFresh(b.FirestoreDocId)).ToList();
            if (approaching.Any())
            {
                var etaTasks = approaching.Select(ComputeEtaAsync).ToList();
                var etaResults = await Task.WhenAll(etaTasks);
                Activity?.RunOnUiThread(() => ShowEtaChips(etaResults));
                return;
            }

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

            foreach (var (minutes, busLine) in entries)
            {
                string label = minutes.HasValue ? $"{minutes} דק'" : "בדרך";
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
                chip.Click += (s, e) =>
                    Toast.MakeText(Context, $"קו {capturedLine}", ToastLength.Short).Show();
                _llEtaChips.AddView(chip);
            }

            _hsvEtaChips.Visibility = ViewStates.Visible;
            _panelContentHeight = -1;
        }

        private class PublicBusInfoWindowAdapter : Java.Lang.Object, GoogleMap.IInfoWindowAdapter
        {
            private readonly LayoutInflater _inflater;
            private readonly Dictionary<string, ActiveBus> _busData;
            private readonly Dictionary<string, Marker> _markers;
            private readonly Dictionary<string, long> _timestamps;

            public PublicBusInfoWindowAdapter(
                LayoutInflater inflater,
                Dictionary<string, ActiveBus> busData,
                Dictionary<string, Marker> markers,
                Dictionary<string, long> timestamps)
            {
                _inflater   = inflater;
                _busData    = busData;
                _markers    = markers;
                _timestamps = timestamps;
            }

            public View GetInfoWindow(Marker marker)
            {
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
