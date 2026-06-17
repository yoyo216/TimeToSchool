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
    // Debug-only twin of TripTrackingService: same foreground-service/wake-lock/notification/
    // Firestore-write/visibility-guard mechanics, but location comes from a hardcoded waypoint
    // loop instead of GPS. Deliberately has no ties to DriverActivity's card list or to
    // TripTrackingService, so logging out, starting/stopping a real route, or destroying the
    // activity never stops it — only an explicit stop (button or notification action) does.
    [Service(Name = "com.companyname.timetoschool.SimulatedTripService", Exported = false)]
    public class SimulatedTripService : Android.App.Service
    {
        private const string ChannelId = "sim_trip_channel_v1";
        private const int NotifId = 1001;

        public static bool IsRunning { get; private set; }
        public static event Action SimulationStoppedFromNotification;

        public static void FireStoppedFromNotification() =>
            new Handler(Looper.MainLooper).Post(() => SimulationStoppedFromNotification?.Invoke());

        private static readonly (double Lat, double Lng)[] Waypoints =
        {
            (32.164200, 34.887500), // start — southern entry to Ramot HaShavim
            (32.164900, 34.888100),
            (32.165600, 34.888700),
            (32.166300, 34.889400),
            (32.167000, 34.890100),
            (32.167484, 34.890852), // user-specified target waypoint
            (32.167900, 34.891500),
            (32.168500, 34.892200),
            (32.169200, 34.892900),
            (32.169900, 34.893600),
            (32.170500, 34.894200),
            (32.171100, 34.894800), // end — northern exit of Ramot HaShavim
        };

        private PowerManager.WakeLock _wakeLock;
        private ActiveBus _tripData;
        private double _firstStopLat;
        private double _firstStopLng;
        private bool _hasFirstStop;
        private int _waypointIndex;
        private Handler _stepHandler;

        public override IBinder OnBind(Intent intent) => null;

        public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
        {
            // Already running — a second "start" tap or a stray relaunch shouldn't reset progress.
            if (IsRunning) return StartCommandResult.Sticky;

            var tripJson = intent?.GetStringExtra("trip_json");
            if (tripJson == null) { StopSelf(); return StartCommandResult.NotSticky; }

            IsRunning = true;
            _tripData = JsonConvert.DeserializeObject<ActiveBus>(tripJson);
            _firstStopLat = intent.GetDoubleExtra("first_stop_lat", 0);
            _firstStopLng = intent.GetDoubleExtra("first_stop_lng", 0);
            _hasFirstStop = intent.GetBooleanExtra("has_first_stop", false);
            _waypointIndex = 0;

            var pm = (PowerManager)GetSystemService(PowerService);
            _wakeLock = pm.NewWakeLock(WakeLockFlags.Partial, ProManager.TAG + ":SimWakeLock");
            _wakeLock.Acquire();

            CreateNotificationChannel();
            StartForeground(NotifId, BuildNotification());

            _stepHandler = new Handler(Looper.MainLooper);
            _stepHandler.Post(AdvanceStep);

            return StartCommandResult.Sticky;
        }

        // Self-rescheduling loop: each call writes one waypoint then queues the next call 8s
        // later via the same Handler, so it keeps ticking for as long as the service is alive —
        // there's no fixed end, the waypoint list just wraps around (% length) forever.
        // Mirrors TripTrackingService.OnLocationChanged's privacy-guard check, just driven by a
        // scripted waypoint instead of a real GPS fix.
        private void AdvanceStep()
        {
            if (!IsRunning) return;

            if (_waypointIndex >= Waypoints.Length)
            {
                Android.Util.Log.Debug(ProManager.TAG, "[SIM] waypoints exhausted — stopping simulation");
                StopSelf();
                return;
            }

            var wp = Waypoints[_waypointIndex];
            _tripData.Latitude = wp.Lat;
            _tripData.Longitude = wp.Lng;
            _waypointIndex++;

            // Only repost the notification when visibility actually flips — reposting on every
            // tick (regardless of content) re-triggers the channel's alert sound each time.
            if (!_tripData.IsVisible && _hasFirstStop)
            {
                float[] dist = new float[1];
                Location.DistanceBetween(wp.Lat, wp.Lng, _firstStopLat, _firstStopLng, dist);
                if (dist[0] <= 75f)
                {
                    _tripData.IsVisible = true;
                    RefreshNotification();
                }
            }

            _ = BusesRepository.UpdateBusLocation(_tripData);
            Android.Util.Log.Debug(ProManager.TAG, $"[SIM] step {_waypointIndex}/{Waypoints.Length}: {wp.Lat},{wp.Lng}");

            _stepHandler.PostDelayed(AdvanceStep, 8_000);
        }

        public override void OnDestroy()
        {
            IsRunning = false;
            _stepHandler?.RemoveCallbacksAndMessages(null);
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

        private Notification BuildNotification()
        {
            var pendingFlags = Build.VERSION.SdkInt >= BuildVersionCodes.M
                ? PendingIntentFlags.Immutable
                : PendingIntentFlags.UpdateCurrent;

            var tapIntent = new Intent(this, typeof(DriverActivity));
            tapIntent.AddFlags(ActivityFlags.SingleTop);
            var tapPendingIntent = PendingIntent.GetActivity(this, 10, tapIntent, pendingFlags);

            var stopIntent = new Intent(this, typeof(StopSimulationReceiver));
            stopIntent.SetAction(StopSimulationReceiver.Action);
            var stopPendingIntent = PendingIntent.GetBroadcast(this, 11, stopIntent, pendingFlags);

            return new NotificationCompat.Builder(this, ChannelId)
                .SetContentTitle($"סימולציה פעילה — קו {_tripData.BusLine}")
                .SetContentText($"{_tripData.Town} - {_tripData.SchoolName} · {_tripData.DriverName}")
                .SetSubText(_tripData.IsVisible ? "גלוי לציבור" : "מוסתר")
                .SetSmallIcon(Resource.Mipmap.ic_launcher)
                .SetContentIntent(tapPendingIntent)
                .SetOngoing(true)
                .SetAutoCancel(false)
                .AddAction(Android.Resource.Drawable.IcMediaPause, "עצור סימולציה", stopPendingIntent)
                .Build();
        }

        private void RefreshNotification()
        {
            var notifManager = (NotificationManager)GetSystemService(NotificationService);
            notifManager.Notify(NotifId, BuildNotification());
        }

        private void CreateNotificationChannel()
        {
            if (Build.VERSION.SdkInt < BuildVersionCodes.O) return;
            var notifManager = (NotificationManager)GetSystemService(NotificationService);
            if (notifManager.GetNotificationChannel(ChannelId) != null) return;
            var channel = new NotificationChannel(ChannelId, "סימולציית נסיעה", NotificationImportance.Default);
            notifManager.CreateNotificationChannel(channel);
        }
    }
}
