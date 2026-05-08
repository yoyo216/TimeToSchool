using Android.Gms.Maps;
using Android.Gms.Maps.Model;
using Android.OS;
using Android.Views;
using AndroidX.Fragment.App;
using Firebase.Firestore;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        private readonly Dictionary<string, Marker> _markers = new Dictionary<string, Marker>();
        private readonly Dictionary<string, ActiveBus> _busData = new Dictionary<string, ActiveBus>();
        private readonly ConcurrentDictionary<string, (double RawLat, double RawLng, LatLng Snapped)> _snapCache =
            new ConcurrentDictionary<string, (double, double, LatLng)>();

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
        }

        public void OnMapReady(GoogleMap googleMap)
        {
            _map = googleMap;
            _map.SetOnMarkerClickListener(this);
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
                        var date = new Java.Util.Date(ts.ToDate().Time);
                        var cal = Java.Util.Calendar.GetInstance(Java.Util.TimeZone.Default);
                        cal.Time = date;
                        int h = cal.Get(Java.Util.CalendarField.HourOfDay);
                        int m = cal.Get(Java.Util.CalendarField.Minute);
                        timeStr = $"{h:D2}:{m:D2}";
                    }
                    catch { }
                }

                var bus = new ActiveBus
                {
                    FirestoreDocId   = item.Id,
                    SchoolName       = item.Get("schoolName")?.ToString(),
                    Town             = item.Get("town")?.ToString(),
                    BusLine          = item.Get("busLine")?.ToString(),
                    DriverName       = item.Get("driverName")?.ToString(),
                    DriverId         = item.Get("driverId")?.ToString(),
                    Status           = item.Get("status")?.ToString(),
                    Date             = item.Get("date")?.ToString(),
                    Latitude         = gp.Latitude,
                    Longitude        = gp.Longitude,
                    IsVisible        = item.Get("isVisible")?.ToString() != "false",
                    LastUpdatedTime  = timeStr
                };

                _busData[bus.FirestoreDocId] = bus;
                currentIds.Add(bus.FirestoreDocId);

                var latLng = await ResolvePosition(bus);
                if (latLng == null) continue;

                Activity?.RunOnUiThread(() => UpdateOrAddMarker(bus, latLng));
            }

            Activity?.RunOnUiThread(() =>
            {
                foreach (var staleId in _markers.Keys.Except(currentIds).ToList())
                {
                    _markers[staleId].Remove();
                    _markers.Remove(staleId);
                    _busData.Remove(staleId);
                    _snapCache.TryRemove(staleId, out _);
                }
            });
        }

        private async Task<LatLng> ResolvePosition(ActiveBus bus)
        {
            if (_snapCache.TryGetValue(bus.FirestoreDocId, out var cached)
                && cached.RawLat == bus.Latitude
                && cached.RawLng == bus.Longitude)
            {
                return cached.Snapped;
            }

            var snapped = await _roadsApi.SnapToRoad(bus.Latitude, bus.Longitude);
            var result = snapped ?? new LatLng(bus.Latitude, bus.Longitude);
            _snapCache[bus.FirestoreDocId] = (bus.Latitude, bus.Longitude, result);
            return result;
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
                    new MarkerOptions().SetPosition(pos).SetTitle(bus.BusLine));
            }
        }

        public bool OnMarkerClick(Marker marker)
        {
            var entry = _markers.FirstOrDefault(kv => kv.Value.Id == marker.Id);
            if (entry.Key != null && _busData.TryGetValue(entry.Key, out var bus))
            {
                BusInfoBottomSheet.NewInstance(bus).Show(ChildFragmentManager, "bus_info");
            }
            return true;
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
    }
}
