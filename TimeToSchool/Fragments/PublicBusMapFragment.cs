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

        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            string key = Resources.GetString(Resource.String.roads_api_key);
            _roadsApi = new RoadsApiService(key);
            return inflater.Inflate(Resource.Layout.fragment_public_bus_map, container, false);
        }

        public override void OnViewCreated(View view, Bundle savedInstanceState)
        {
            base.OnViewCreated(view, savedInstanceState);

            _school  = Arguments?.GetString("school")   ?? string.Empty;
            _town    = Arguments?.GetString("town")     ?? string.Empty;
            _busLine = Arguments?.GetString("bus_line") ?? string.Empty;

            var summary = string.IsNullOrEmpty(_busLine) || _busLine == "Any Available Bus"
                ? $"{_school} · {_town}"
                : $"{_school} · {_town} · {_busLine}";
            view.FindViewById<TextView>(Resource.Id.tvSelectionSummary).Text = summary;

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
            if (!Eq(bus.SchoolName, _school)) return false;
            if (!Eq(bus.Town, _town)) return false;
            if (!string.IsNullOrEmpty(_busLine) && _busLine != "Any Available Bus")
                if (!Eq(bus.BusLine, _busLine)) return false;
            return true;
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

        public override void OnDestroyView()
        {
            if (_listener != null)
                _listener.getEvent -= OnFirestoreUpdate;
            _reg?.Remove();
            _reg = null;
            _listener = null;
            base.OnDestroyView();
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
