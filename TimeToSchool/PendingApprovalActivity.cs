using Android.App;
using Android.Content;
using Android.OS;
using Android.Widget;
using AndroidX.AppCompat.App;
using Firebase.Auth;
using Firebase.Firestore;
using TimeToSchool.BusinessLogic;
using TimeToSchool.Service;
using static TimeToSchool.Service.FireBaseHelper;

namespace TimeToSchool
{
    [Activity(Label = "Pending Approval")]
    public class PendingApprovalActivity : AppCompatActivity
    {
        private Button _btnSignOut;
        private IListenerRegistration _statusRegistration;
        private FirestoreEventListener _statusListener;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.pending_approval_layout);

            _btnSignOut = FindViewById<Button>(Resource.Id.btnSignOutPending);
            _btnSignOut.Click += (s, e) => SignOutAndReturn();
        }

        protected override void OnResume()
        {
            base.OnResume();
            var userId = ProManager.CurrentUser?.Id;
            if (string.IsNullOrEmpty(userId)) return;

            _statusListener = new FirestoreEventListener();
            _statusListener.getEvent += OnStatusSnapshot;
            _statusRegistration = UsersRepository.ListenUserStatus(userId, _statusListener);
        }

        protected override void OnPause()
        {
            _statusRegistration?.Remove();
            _statusRegistration = null;
            if (_statusListener != null)
            {
                _statusListener.getEvent -= OnStatusSnapshot;
                _statusListener = null;
            }
            base.OnPause();
        }

        public override void OnBackPressed() => SignOutAndReturn();

        private void OnStatusSnapshot(object sender, FirestoreEventListener.TaskListenerEventArgs e)
        {
            if (e.Error != null || e.Result == null) return;
            var snap = e.Result as DocumentSnapshot;
            if (snap == null || !snap.Exists()) return;

            var status = snap.Get("Status")?.ToString();

            if (status == "approved")
            {
                if (ProManager.CurrentUser != null)
                    ProManager.CurrentUser.Status = "approved";

                var intent = new Intent(this, typeof(DriverActivity));
                intent.AddFlags(ActivityFlags.ClearTop | ActivityFlags.NewTask);
                StartActivity(intent);
                Finish();
            }
            else if (status == "rejected")
            {
                Toast.MakeText(this, "בקשתך נדחתה על ידי המנהל", ToastLength.Long).Show();
                SignOutAndReturn();
            }
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
