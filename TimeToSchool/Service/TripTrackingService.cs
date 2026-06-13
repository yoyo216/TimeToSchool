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
        private const string ChannelId = "trip_channel_v2";
        private const int NotifId = 1;
        private const string ActionRepostNotification = "com.companyname.timetoschool.REPOST_NOTIFICATION";

        public static bool IsRunning { get; private set; }
        public static event Action TripAutoStopped;
        public static event Action TripStoppedFromNotification;

        public static void FireStoppedFromNotification() =>
            new Handler(Looper.MainLooper).Post(() => TripStoppedFromNotification?.Invoke());

        private LocationManager _locManager;
        private PowerManager.WakeLock _wakeLock;
        private ActiveBus _tripData;
        private double _firstStopLat;
        private double _firstStopLng;
        private bool _hasFirstStop;
        private double _schoolLat;
        private double _schoolLng;
        private bool _hasSchoolLocation;
        private bool _autoStopping;
        private DateTime _lastFirebaseWrite = DateTime.MinValue;

        public override IBinder OnBind(Intent intent) => null;

        public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
        {
            if (intent?.Action == ActionRepostNotification)
            {
                if (IsRunning && _tripData != null)
                    StartForeground(NotifId, BuildNotification());
                return StartCommandResult.Sticky;
            }

            IsRunning = true;
            var pm = (PowerManager)GetSystemService(PowerService);
            _wakeLock = pm.NewWakeLock(WakeLockFlags.Partial, ProManager.TAG + ":TripWakeLock");
            _wakeLock.Acquire();
            var tripJson = intent?.GetStringExtra("trip_json");
            if (tripJson == null) { StopSelf(); return StartCommandResult.NotSticky; }

            _tripData = JsonConvert.DeserializeObject<ActiveBus>(tripJson);
            _firstStopLat = intent.GetDoubleExtra("first_stop_lat", 0);
            _firstStopLng = intent.GetDoubleExtra("first_stop_lng", 0);
            _hasFirstStop = intent.GetBooleanExtra("has_first_stop", false);

            _ = GeocodeSchoolAsync(_tripData.SchoolName);

            CreateNotificationChannel();
            StartForeground(NotifId, BuildNotification());

            _locManager = (LocationManager)GetSystemService(LocationService);
            StartLocationUpdates();

            var lastKnown = _locManager.GetLastKnownLocation(LocationManager.NetworkProvider)
                         ?? _locManager.GetLastKnownLocation(LocationManager.GpsProvider);
            if (lastKnown != null)
                OnLocationChanged(lastKnown);

            return StartCommandResult.Sticky;
        }

        private Notification BuildNotification()
        {
            var pendingFlags = Build.VERSION.SdkInt >= BuildVersionCodes.M
                ? PendingIntentFlags.Immutable
                : PendingIntentFlags.UpdateCurrent;

            var tapIntent = new Intent(this, typeof(DriverActivity));
            tapIntent.AddFlags(ActivityFlags.SingleTop);
            var tapPendingIntent = PendingIntent.GetActivity(this, 0, tapIntent, pendingFlags);

            var stopIntent = new Intent(this, typeof(StopTripReceiver));
            stopIntent.SetAction(StopTripReceiver.Action);
            var stopPendingIntent = PendingIntent.GetBroadcast(this, 1, stopIntent, pendingFlags);

            var repostIntent = new Intent(this, typeof(TripTrackingService));
            repostIntent.SetAction(ActionRepostNotification);
            var repostPendingIntent = Build.VERSION.SdkInt >= BuildVersionCodes.O
                ? PendingIntent.GetForegroundService(this, 2, repostIntent, pendingFlags)
                : PendingIntent.GetService(this, 2, repostIntent, pendingFlags);

            return new NotificationCompat.Builder(this, ChannelId)
                .SetContentTitle("נסיעה פעילה")
                .SetContentText($"{_tripData.BusLine} — {_tripData.Town}")
                .SetSmallIcon(Resource.Mipmap.ic_launcher)
                .SetContentIntent(tapPendingIntent)
                .SetOngoing(true)
                .SetAutoCancel(false)
                .AddAction(Android.Resource.Drawable.IcMediaPause, "כבה נסיעה", stopPendingIntent)
                .SetDeleteIntent(repostPendingIntent)
                .Build();
        }

        public void OnLocationChanged(Location location)
        {
            if (_tripData == null || _autoStopping) return;
            if (location.HasAccuracy && location.Accuracy > 80f) return;

            _tripData.Latitude = location.Latitude;
            _tripData.Longitude = location.Longitude;
            if (!_tripData.IsVisible && _hasFirstStop)
                TryUnlockVisibility(location);
            if (_hasSchoolLocation)
                CheckSchoolArrival(location);

            if (!_autoStopping && (DateTime.UtcNow - _lastFirebaseWrite).TotalSeconds >= 10)
            {
                _lastFirebaseWrite = DateTime.UtcNow;
                _ = BusesRepository.UpdateBusLocation(_tripData);
            }
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
            _wakeLock?.Release();
            _wakeLock = null;
            if (_tripData != null)
            {
                _tripData.Status = "Inactive";
                _ = BusesRepository.UpdateBusLocation(_tripData);
            }
            StopForeground(StopForegroundFlags.Remove);
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
            var channel = new NotificationChannel(ChannelId, "נסיעה פעילה", NotificationImportance.Default);
            notifManager.CreateNotificationChannel(channel);
        }

        public void OnProviderDisabled(string provider) { }

        public void OnProviderEnabled(string provider) => TryRegisterProvider(provider);

        public void OnStatusChanged(string provider, Availability status, Bundle extras) { }

        private void StartLocationUpdates()
        {
            TryRegisterProvider(LocationManager.NetworkProvider);
            TryRegisterProvider(LocationManager.GpsProvider);
        }

        private void TryRegisterProvider(string provider)
        {
            try
            {
                if (_locManager.IsProviderEnabled(provider))
                    _locManager.RequestLocationUpdates(provider, 15000, 25, this);
            }
            catch (Exception ex)
            {
                Android.Util.Log.Warn(ProManager.TAG, $"[Location] {provider} unavailable: {ex.Message}");
            }
        }
    }
}
