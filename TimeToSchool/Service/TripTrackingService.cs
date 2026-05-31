using Android.App;
using Android.Content;
using Android.Locations;
using Android.OS;
using AndroidX.Core.App;
using Newtonsoft.Json;
using System;
using TimeToSchool.BusinessLogic;
using TimeToSchool.Model;

namespace TimeToSchool.Service
{
    [Service(Name = "com.companyname.timetoschool.TripTrackingService", Exported = false)]
    public class TripTrackingService : Android.App.Service, ILocationListener
    {
        private const string ChannelId = "trip_channel";
        private const int NotifId = 1;

        public static bool IsRunning { get; private set; }
        public static event Action TripAutoStopped;

        private LocationManager _locManager;
        private ActiveBus _tripData;
        private double _firstStopLat;
        private double _firstStopLng;
        private bool _hasFirstStop;
        private double _schoolLat;
        private double _schoolLng;
        private bool _hasSchoolLocation;
        private bool _autoStopping;

        public override IBinder OnBind(Intent intent) => null;

        public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
        {
            IsRunning = true;
            var tripJson = intent?.GetStringExtra("trip_json");
            if (tripJson == null) { StopSelf(); return StartCommandResult.NotSticky; }

            _tripData = JsonConvert.DeserializeObject<ActiveBus>(tripJson);
            _firstStopLat = intent.GetDoubleExtra("first_stop_lat", 0);
            _firstStopLng = intent.GetDoubleExtra("first_stop_lng", 0);
            _hasFirstStop = intent.GetBooleanExtra("has_first_stop", false);

            _ = GeocodeSchoolAsync(_tripData.SchoolName);

            CreateNotificationChannel();

            var tapIntent = new Intent(this, typeof(DriverActivity));
            tapIntent.AddFlags(ActivityFlags.SingleTop);
            var pendingFlags = Build.VERSION.SdkInt >= BuildVersionCodes.M
                ? PendingIntentFlags.Immutable
                : PendingIntentFlags.UpdateCurrent;
            var pendingIntent = PendingIntent.GetActivity(this, 0, tapIntent, pendingFlags);

            var notification = new NotificationCompat.Builder(this, ChannelId)
                .SetContentTitle("נסיעה פעילה")
                .SetContentText($"{_tripData.BusLine} — {_tripData.Town}")
                .SetSmallIcon(Resource.Mipmap.ic_launcher)
                .SetContentIntent(pendingIntent)
                .SetOngoing(true)
                .Build();

            StartForeground(NotifId, notification);

            _locManager = (LocationManager)GetSystemService(LocationService);
            _locManager.RequestLocationUpdates(LocationManager.NetworkProvider, 15000, 2, this);

            var lastKnown = _locManager.GetLastKnownLocation(LocationManager.NetworkProvider)
                         ?? _locManager.GetLastKnownLocation(LocationManager.GpsProvider);
            if (lastKnown != null)
                OnLocationChanged(lastKnown);

            return StartCommandResult.Sticky;
        }

        public void OnLocationChanged(Location location)
        {
            if (_tripData == null || _autoStopping) return;
            _tripData.Latitude = location.Latitude;
            _tripData.Longitude = location.Longitude;
            if (!_tripData.IsVisible && _hasFirstStop)
                TryUnlockVisibility(location);
            if (_hasSchoolLocation)
                CheckSchoolArrival(location);
            if (!_autoStopping)
                _ = BusesRepository.UpdateBusLocation(_tripData);
        }

        public override void OnTaskRemoved(Intent rootIntent)
        {
            StopSelf();
            base.OnTaskRemoved(rootIntent);
        }

        public override void OnDestroy()
        {
            IsRunning = false;
            _locManager?.RemoveUpdates(this);
            if (_tripData != null)
            {
                _tripData.Status = "Inactive";
                _ = BusesRepository.UpdateBusLocation(_tripData);
            }
            StopForeground(true);
            base.OnDestroy();
        }

        private void TryUnlockVisibility(Location current)
        {
            float[] dist = new float[1];
            Location.DistanceBetween(current.Latitude, current.Longitude, _firstStopLat, _firstStopLng, dist);
            if (dist[0] <= 50f)
                _tripData.IsVisible = true;
        }

        private void CheckSchoolArrival(Location current)
        {
            float[] dist = new float[1];
            Location.DistanceBetween(current.Latitude, current.Longitude, _schoolLat, _schoolLng, dist);
            if (dist[0] <= 300f)
            {
                _autoStopping = true;
                new Handler(Looper.MainLooper).Post(() => TripAutoStopped?.Invoke());
                StopSelf();
            }
        }

        private async System.Threading.Tasks.Task GeocodeSchoolAsync(string schoolName)
        {
            if (!Geocoder.IsPresent || string.IsNullOrEmpty(schoolName)) return;
            try
            {
                var geocoder = new Geocoder(this, Java.Util.Locale.Default);
                var results = await System.Threading.Tasks.Task.Run(
                    () => geocoder.GetFromLocationName(schoolName, 1));
                if (results != null && results.Count > 0)
                {
                    _schoolLat = results[0].Latitude;
                    _schoolLng = results[0].Longitude;
                    _hasSchoolLocation = true;
                    Android.Util.Log.Debug(ProManager.TAG,
                        $"[AutoStop] Geocoded '{schoolName}' → {_schoolLat},{_schoolLng}");
                }
                else
                {
                    Android.Util.Log.Warn(ProManager.TAG,
                        $"[AutoStop] No geocode result for '{schoolName}'");
                }
            }
            catch (System.Exception ex)
            {
                Android.Util.Log.Warn(ProManager.TAG, $"[AutoStop] Geocoding failed: {ex.Message}");
            }
        }

        private void CreateNotificationChannel()
        {
            if (Build.VERSION.SdkInt < BuildVersionCodes.O) return;
            var notifManager = (NotificationManager)GetSystemService(NotificationService);
            if (notifManager.GetNotificationChannel(ChannelId) != null) return;
            var channel = new NotificationChannel(ChannelId, "נסיעה פעילה", NotificationImportance.Low);
            notifManager.CreateNotificationChannel(channel);
        }

        public void OnProviderDisabled(string provider) { }
        public void OnProviderEnabled(string provider) { }
        public void OnStatusChanged(string provider, Availability status, Bundle extras) { }
    }
}
