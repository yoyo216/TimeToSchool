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

        protected override int GetContentLayoutId() => Resource.Layout.adminpage_layout;

        protected override void OnCreateDrawerContent(Bundle savedInstanceState)
        {
            _tabLayout = FindViewById<TabLayout>(Resource.Id.adminTabLayout);
            _tabLayout.TabSelected += OnTabSelected;

            if (savedInstanceState == null)
            {
                SupportFragmentManager.BeginTransaction()
                    .Add(Resource.Id.adminFragmentContainer, _busesFragment)
                    .Add(Resource.Id.adminFragmentContainer, _usersFragment)
                    .Hide(_busesFragment)
                    .Commit();
            }
        }

        private void OnTabSelected(object sender, TabLayout.TabSelectedEventArgs e)
        {
            var show = e.Tab.Position == 0 ? (AndroidX.Fragment.App.Fragment)_usersFragment : _busesFragment;
            var hide = e.Tab.Position == 0 ? (AndroidX.Fragment.App.Fragment)_busesFragment : _usersFragment;

            SupportFragmentManager.BeginTransaction()
                .Show(show)
                .Hide(hide)
                .Commit();
        }

        public override bool DispatchTouchEvent(MotionEvent ev)
        {
            UIHelper.HandleOutsideTouch(this, ev);
            return base.DispatchTouchEvent(ev);
        }
    }
}
