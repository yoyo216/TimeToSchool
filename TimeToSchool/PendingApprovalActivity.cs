using Android.App;
using Android.Content;
using Android.OS;
using Android.Widget;
using AndroidX.AppCompat.App;
using Firebase.Auth;
using TimeToSchool.BusinessLogic;
using TimeToSchool.Service;

namespace TimeToSchool
{
    [Activity(Label = "Pending Approval")]
    public class PendingApprovalActivity : AppCompatActivity
    {
        private Button _btnSignOut;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.pending_approval_layout);

            _btnSignOut = FindViewById<Button>(Resource.Id.btnSignOutPending);
            _btnSignOut.Click += (s, e) => SignOutAndReturn();
        }

        public override void OnBackPressed()
        {
            SignOutAndReturn();
        }

        private void SignOutAndReturn()
        {
            try { FirebaseAuth.Instance.SignOut(); } catch { }
            new PreferenceService(this).ClearSession();
            ProManager.CurrentUser = null;

            var intent = new Intent(this, typeof(SignInActivity));
            intent.AddFlags(ActivityFlags.ClearTop | ActivityFlags.NewTask);
            StartActivity(intent);
            Finish();
        }
    }
}
