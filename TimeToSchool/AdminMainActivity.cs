using Android.App;
using Android.OS;
using Android.Views;
using AndroidX.Fragment.App;
using Google.Android.Material.Tabs;
using TimeToSchool.Fragments;
using TimeToSchool.Helpers;

namespace TimeToSchool
{
    [Activity(Label = "Admin Control Center")]
    public class AdminMainActivity : BaseDrawerActivity
    {
        private TabLayout _tabLayout;
        private readonly UsersManagementFragment _usersFragment = new UsersManagementFragment();
        private readonly BusesManagementFragment _busesFragment = new BusesManagementFragment();
        private readonly PendingRequestsFragment _pendingFragment = new PendingRequestsFragment();
        private readonly BannedUsersFragment _bannedFragment = new BannedUsersFragment();

        protected override int GetContentLayoutId() => Resource.Layout.adminpage_layout;

        protected override void OnCreateDrawerContent(Bundle savedInstanceState)
        {
            _tabLayout = FindViewById<TabLayout>(Resource.Id.adminTabLayout);
            _tabLayout.TabSelected += OnTabSelected;

            if (savedInstanceState == null)
            {
                SupportFragmentManager.BeginTransaction()
                    .Add(Resource.Id.adminFragmentContainer, _bannedFragment)
                    .Add(Resource.Id.adminFragmentContainer, _pendingFragment)
                    .Add(Resource.Id.adminFragmentContainer, _busesFragment)
                    .Add(Resource.Id.adminFragmentContainer, _usersFragment)
                    .Hide(_busesFragment)
                    .Hide(_pendingFragment)
                    .Hide(_bannedFragment)
                    .Commit();
            }
        }

        private void OnTabSelected(object sender, TabLayout.TabSelectedEventArgs e)
        {
            AndroidX.Fragment.App.Fragment show;
            switch (e.Tab.Position)
            {
                case 0: show = _usersFragment; break;
                case 1: show = _busesFragment; break;
                case 2: show = _pendingFragment; break;
                default: show = _bannedFragment; break;
            }

            var tx = SupportFragmentManager.BeginTransaction().Show(show);
            if (show != _usersFragment)   tx.Hide(_usersFragment);
            if (show != _busesFragment)   tx.Hide(_busesFragment);
            if (show != _pendingFragment) tx.Hide(_pendingFragment);
            if (show != _bannedFragment)  tx.Hide(_bannedFragment);
            tx.Commit();
        }

        public override bool DispatchTouchEvent(MotionEvent ev)
        {
            UIHelper.HandleOutsideTouch(this, ev);
            return base.DispatchTouchEvent(ev);
        }
    }
}
