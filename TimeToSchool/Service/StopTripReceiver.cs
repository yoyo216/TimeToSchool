using Android.App;
using Android.Content;

namespace TimeToSchool.Service
{
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
