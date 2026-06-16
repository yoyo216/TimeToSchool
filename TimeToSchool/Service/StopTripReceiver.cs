using Android.App;
using Android.Content;

namespace TimeToSchool.Service
{
    // Target of the "כבה נסיעה" action button on the trip notification (see
    // TripTrackingService.BuildNotification). Notification actions can't call a method directly —
    // tapping it fires this receiver, which tells DriverActivity (if open) to update its card UI,
    // then stops the service.
    [BroadcastReceiver(Exported = false)]
    [IntentFilter(new[] { StopTripReceiver.Action })]
    public class StopTripReceiver : BroadcastReceiver
    {
        public const string Action = "com.companyname.timetoschool.ACTION_STOP_TRIP";

        public override void OnReceive(Context context, Intent intent)
        {
            if (intent?.Action != Action) return;
            TripTrackingService.FireStoppedFromNotification();
            context.StopService(new Intent(context, typeof(TripTrackingService)));
        }
    }
}
