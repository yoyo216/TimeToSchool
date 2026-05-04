using Android.App;
using Android.Content;
using TimeToSchool.Model;

namespace TimeToSchool.Helpers
{
    public static class RoleRouter
    {
        public static void RouteFor(Activity from, User user, bool finishCaller = true)
        {
            Intent intent;
            if (user.IsAdmin)
                intent = new Intent(from, typeof(AdminMainActivity));
            else if (user.Status == "approved")
                intent = new Intent(from, typeof(DriverActivity));
            else
                intent = new Intent(from, typeof(PendingApprovalActivity));

            from.StartActivity(intent);
            if (finishCaller)
                from.Finish();
        }
    }
}
