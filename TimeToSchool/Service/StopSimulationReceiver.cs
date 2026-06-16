using Android.App;
using Android.Content;

namespace TimeToSchool.Service
{
    // Same role as StopTripReceiver, but for the simulation's "עצור סימולציה" notification action —
    // kept separate so stopping a simulation can never be confused with stopping a real trip.
    [BroadcastReceiver(Exported = false)]
    [IntentFilter(new[] { StopSimulationReceiver.Action })]
    public class StopSimulationReceiver : BroadcastReceiver
    {
        public const string Action = "com.companyname.timetoschool.ACTION_STOP_SIMULATION";

        public override void OnReceive(Context context, Intent intent)
        {
            if (intent?.Action != Action) return;
            SimulatedTripService.FireStoppedFromNotification();
            context.StopService(new Intent(context, typeof(SimulatedTripService)));
        }
    }
}
