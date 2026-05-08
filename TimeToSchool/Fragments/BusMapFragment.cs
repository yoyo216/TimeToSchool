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
    public class BusMapFragment : Fragment, IOnMapReadyCallback, GoogleMap.IOnMarkerClickListener
    {
        private GoogleMap _map;
        private RoadsApiService _roadsApi;
        private IListenerRegistration _reg;
        private FirestoreEventListener _listener;
        private BitmapDescriptor _busIcon;
        private readonly Dictionary<string, Marker> _markers = new Dictionary<string, Marker>();
        private readonly Dictionary<string, ActiveBus> _busData = new Dictionary<string, ActiveBus>();

        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            string key = Resources.GetString(Resource.String.roads_api_key);
            _roadsApi = new RoadsApiService(key);
            return inflater.Inflate(Resource.Layout.fragment_bus_map, container, false);
        }

        public override void OnViewCreated(View view, Bundle savedInstanceState)
        {
            base.OnViewCreated(view, savedInstanceState);
            var mapFrag = (SupportMapFragment)ChildFragmentManager.FindFragmentById(Resource.Id.mapFragment);
            mapFrag.GetMapAsync(this);
            view.FindViewById<FloatingActionButton>(Resource.Id.fabFocusAll)
                .Click += (s, e) => FocusAllMarkers();
        }

        public void OnMapReady(GoogleMap googleMap)
        {
            _map = googleMap;
            _map.SetInfoWindowAdapter(new BusInfoWindowAdapter(
                LayoutInflater.From(Context), _busData, _markers));
            _map.SetOnMarkerClickListener(this);

            using (var bmp = BitmapFactory.DecodeResource(Resources, Resource.Drawable.ic_icon_bus))
            {
                var scaled = Bitmap.CreateScaledBitmap(bmp, 96, 96, true);
                _busIcon = BitmapDescriptorFactory.FromBitmap(scaled);
            }

            StartListening();
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
                string timeStr = "--:--";
                if (ts != null)
                {
                    try
                    {
                        var cal = Java.Util.Calendar.GetInstance(Java.Util.TimeZone.Default);
                        cal.Time = new Java.Util.Date(ts.ToDate().Time);
                        timeStr = $"{cal.Get(Java.Util.CalendarField.HourOfDay):D2}:{cal.Get(Java.Util.CalendarField.Minute):D2}";
                    }
                    catch { }
                }

                var bus = new ActiveBus
                {
                    FirestoreDocId  = item.Id,
                    SchoolName      = item.Get("schoolName")?.ToString(),
                    Town            = item.Get("town")?.ToString(),
                    BusLine         = item.Get("busLine")?.ToString(),
                    DriverName      = item.Get("driverName")?.ToString(),
                    DriverId        = item.Get("driverId")?.ToString(),
                    Status          = item.Get("status")?.ToString(),
                    Date            = item.Get("date")?.ToString(),
                    Latitude        = gp.Latitude,
                    Longitude       = gp.Longitude,
                    IsVisible       = item.Get("isVisible")?.ToString() != "false",
                    LastUpdatedTime = timeStr
                };

                _busData[bus.FirestoreDocId] = bus;
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
                    }
                });
        }

        private void UpdateOrAddMarker(ActiveBus bus, LatLng pos)
        {
            if (_markers.TryGetValue(bus.FirestoreDocId, out var existing))
            {
                existing.Position = pos;
            }
            else
            {
                _markers[bus.FirestoreDocId] = _map.AddMarker(
                    new MarkerOptions()
                        .SetPosition(pos)
                        .SetTitle(bus.BusLine)
                        .SetIcon(_busIcon));
            }
        }

        public bool OnMarkerClick(Marker marker) => false;

        private void FocusAllMarkers()
        {
            if (_map == null || _markers.Count == 0) return;

            if (_markers.Count == 1)
            {
                _map.AnimateCamera(CameraUpdateFactory.NewLatLngZoom(
                    _markers.Values.First().Position, 15f));
                return;
            }

            var builder = new LatLngBounds.Builder();
            foreach (var marker in _markers.Values)
                builder.Include(marker.Position);

            _map.AnimateCamera(CameraUpdateFactory.NewLatLngBounds(builder.Build(), 150));
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

        private class BusInfoWindowAdapter : Java.Lang.Object, GoogleMap.IInfoWindowAdapter
        {
            private readonly LayoutInflater _inflater;
            private readonly Dictionary<string, ActiveBus> _busData;
            private readonly Dictionary<string, Marker> _markers;

            public BusInfoWindowAdapter(LayoutInflater inflater,
                Dictionary<string, ActiveBus> busData,
                Dictionary<string, Marker> markers)
            {
                _inflater = inflater;
                _busData  = busData;
                _markers  = markers;
            }

            public View GetInfoWindow(Marker marker)
            {
                var entry = _markers.FirstOrDefault(kv => kv.Value.Id == marker.Id);
                if (entry.Key == null || !_busData.TryGetValue(entry.Key, out var bus))
                    return null;

                var view = _inflater.Inflate(Resource.Layout.fragment_bus_info_sheet, null, false);
                view.FindViewById<TextView>(Resource.Id.tv_bus_line).Text     = bus.BusLine;
                view.FindViewById<TextView>(Resource.Id.tv_driver_name).Text  = bus.DriverName;
                view.FindViewById<TextView>(Resource.Id.tv_school).Text       = bus.SchoolName;
                view.FindViewById<TextView>(Resource.Id.tv_town).Text         = bus.Town;
                view.FindViewById<TextView>(Resource.Id.tv_status).Text       =
                    bus.Status == "Active" ? "פעיל" : "לא פעיל";
                view.FindViewById<TextView>(Resource.Id.tv_visible).Text      =
                    bus.IsVisible ? "גלוי לציבור" : "מוסתר מהציבור";
                view.FindViewById<TextView>(Resource.Id.tv_last_updated).Text =
                    $"עודכן: {bus.LastUpdatedTime}";
                return view;
            }

            public View GetInfoContents(Marker marker) => null;
        }
    }
}
