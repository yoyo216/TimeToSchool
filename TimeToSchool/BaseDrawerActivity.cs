using Android.Content;
using Android.Content.Res;
using Android.OS;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.App;
using AndroidX.DrawerLayout.Widget;
using Firebase.Auth;
using Google.Android.Material.Navigation;
using TimeToSchool.BusinessLogic;
using TimeToSchool.Service;

namespace TimeToSchool
{
    public abstract class BaseDrawerActivity : AppCompatActivity
    {
        protected DrawerLayout _drawerLayout;
        private ActionBarDrawerToggle _drawerToggle;
        private NavigationView _navView;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.drawer_base);

            var toolbar = FindViewById<AndroidX.AppCompat.Widget.Toolbar>(Resource.Id.toolbar);
            SetSupportActionBar(toolbar);

            _drawerLayout = FindViewById<DrawerLayout>(Resource.Id.drawer_layout);
            _navView = FindViewById<NavigationView>(Resource.Id.nav_view);

            _drawerToggle = new ActionBarDrawerToggle(
                this, _drawerLayout, toolbar,
                Resource.String.open_drawer,
                Resource.String.close_drawer);
            _drawerLayout.AddDrawerListener(_drawerToggle);
            _drawerToggle.SyncState();

            _navView.NavigationItemSelected += OnNavItemSelected;
            RefreshDrawer();

            var contentFrame = FindViewById<FrameLayout>(Resource.Id.content_frame);
            LayoutInflater.Inflate(GetContentLayoutId(), contentFrame, true);

            OnCreateDrawerContent(savedInstanceState);
        }

        protected override void OnPostCreate(Bundle savedInstanceState)
        {
            base.OnPostCreate(savedInstanceState);
            _drawerToggle.SyncState();
        }

        public override void OnConfigurationChanged(Configuration newConfig)
        {
            base.OnConfigurationChanged(newConfig);
            _drawerToggle.OnConfigurationChanged(newConfig);
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            if (_drawerToggle.OnOptionsItemSelected(item))
                return true;
            return base.OnOptionsItemSelected(item);
        }

        protected abstract int GetContentLayoutId();
        protected abstract void OnCreateDrawerContent(Bundle savedInstanceState);

        protected void RefreshDrawer()
        {
            var header = _navView.GetHeaderView(0);
            var tvName = header.FindViewById<TextView>(Resource.Id.nav_header_name);
            var tvRole = header.FindViewById<TextView>(Resource.Id.nav_header_role);
            var menu = _navView.Menu;

            if (ProManager.CurrentUser != null)
            {
                tvName.Text = $"{ProManager.CurrentUser.FirstName} {ProManager.CurrentUser.LastName}";
                tvRole.Text = ProManager.CurrentUser.IsAdmin ? "Admin" : "Driver";
                menu.FindItem(Resource.Id.nav_login).SetVisible(false);
                menu.FindItem(Resource.Id.nav_logout).SetVisible(true);
            }
            else
            {
                tvName.Text = "Guest";
                tvRole.Text = string.Empty;
                menu.FindItem(Resource.Id.nav_login).SetVisible(true);
                menu.FindItem(Resource.Id.nav_logout).SetVisible(false);
            }
        }

        private void OnNavItemSelected(object sender, NavigationView.NavigationItemSelectedEventArgs e)
        {
            _drawerLayout.CloseDrawer(_navView);

            if (e.MenuItem.ItemId == Resource.Id.nav_login)
                StartActivity(typeof(SignInActivity));
            else if (e.MenuItem.ItemId == Resource.Id.nav_logout)
                Logout();
        }

        protected virtual void OnBeforeLogout() { }

        private void Logout()
        {
            OnBeforeLogout();
            new PreferenceService(this).ClearSession();
            FirebaseAuth.Instance.SignOut();
            ProManager.CurrentUser = null;

            var intent = new Intent(this, typeof(MainActivity));
            intent.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTask);
            StartActivity(intent);
        }

        public override void OnBackPressed()
        {
            if (_drawerLayout.IsDrawerOpen(_navView))
                _drawerLayout.CloseDrawer(_navView);
            else
                base.OnBackPressed();
        }
    }
}
