using Android.App;
using Android.OS;
using AndroidX.Fragment.App;
using Google.Android.Material.Tabs;
using TimeToSchool.Fragments;
// Alias to resolve the ambiguous reference error we saw earlier
using FragmentTransaction = AndroidX.Fragment.App.FragmentTransaction;

namespace TimeToSchool
{
    [Activity(Label = "Admin Control Center")]
    public class AdminMainActivity : FragmentActivity
    {
        // Fields - Private to this class
        private TabLayout _tabLayout;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.adminpage_layout);

            InitializeViews();
            SetupEventListeners();
            LoadDefaultFragment(savedInstanceState);
        }

        private void InitializeViews()
        {
            _tabLayout = FindViewById<TabLayout>(Resource.Id.adminTabLayout);
        }

        private void SetupEventListeners()
        {
            // Separating the listener logic from OnCreate
            _tabLayout.TabSelected += OnTabSelected;
        }

        private void OnTabSelected(object sender, TabLayout.TabSelectedEventArgs e)
        {
            switch (e.Tab.Position)
            {
                case 0:
                    ReplaceFragment(new UsersManagementFragment());
                    break;
                case 1:
                    // Added the missing case for Buses
                    ReplaceFragment(new BusesManagementFragment());
                    break;
            }
        }

        private void LoadDefaultFragment(Bundle savedInstanceState)
        {
            // Only load if the app isn't being recreated (e.g., on rotation)
            if (savedInstanceState == null)
            {
                ReplaceFragment(new UsersManagementFragment());
            }
        }

        // Dedicated method for Fragment Transactions
        private void ReplaceFragment(AndroidX.Fragment.App.Fragment fragment)
        {
            SupportFragmentManager.BeginTransaction()
                .Replace(Resource.Id.adminFragmentContainer, fragment)
                .SetTransition(FragmentTransaction.TransitFragmentFade)
                .Commit();
        }
    }
}