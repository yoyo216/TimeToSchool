using Android.App;
using Android.OS;
using AndroidX.Fragment.App;
using Google.Android.Material.Tabs;
using TimeToSchool.Fragments;
using FragmentTransaction = AndroidX.Fragment.App.FragmentTransaction;

namespace TimeToSchool
{
    [Activity(Label = "Admin Control Center")]
    public class AdminMainActivity : BaseDrawerActivity
    {
        private TabLayout _tabLayout;

        protected override int GetContentLayoutId() => Resource.Layout.adminpage_layout;

        protected override void OnCreateDrawerContent(Bundle savedInstanceState)
        {
            _tabLayout = FindViewById<TabLayout>(Resource.Id.adminTabLayout);
            _tabLayout.TabSelected += OnTabSelected;

            if (savedInstanceState == null)
                ReplaceFragment(new UsersManagementFragment());
        }

        private void OnTabSelected(object sender, TabLayout.TabSelectedEventArgs e)
        {
            switch (e.Tab.Position)
            {
                case 0: ReplaceFragment(new UsersManagementFragment()); break;
                case 1: ReplaceFragment(new BusesManagementFragment()); break;
            }
        }

        private void ReplaceFragment(AndroidX.Fragment.App.Fragment fragment)
        {
            SupportFragmentManager.BeginTransaction()
                .Replace(Resource.Id.adminFragmentContainer, fragment)
                .SetTransition(FragmentTransaction.TransitFragmentFade)
                .Commit();
        }
    }
}
