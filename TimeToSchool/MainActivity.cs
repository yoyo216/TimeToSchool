using Android.App;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;
using Google.Android.Material.TextField;
using System;
using System.Collections.Generic;
using System.Linq;
using TimeToSchool.BusinessLogic;
using TimeToSchool.Helpers;
using TimeToSchool.Model;
using TimeToSchool.Service;


namespace TimeToSchool
{
    [Activity(Label = "Time To School",
              MainLauncher = true,
              WindowSoftInputMode = SoftInput.AdjustResize | SoftInput.StateHidden)]
    public class MainActivity : BaseDrawerActivity
    {
        private AutoCompleteTextView autoSchool;
        private AutoCompleteTextView autoTown;
        private AutoCompleteTextView autoBus;
        private Button btnFindBus;

        private readonly SelectionValidator _validator = new SelectionValidator();
        private List<BusRoute> _allRoutes;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            base.OnCreate(savedInstanceState);
        }

        protected override int GetContentLayoutId() => Resource.Layout.activity_main;

        // BaseDrawerActivity.OnCreate() inflates the nav-drawer chrome and this screen's layout,
        // then calls this hook — it's effectively MainActivity's "OnCreate" for screen-specific setup.
        protected override void OnCreateDrawerContent(Bundle savedInstanceState)
        {
            FireBaseHelper.Initialize();
            // If a saved session exists, CheckUserSession() already navigated to the driver/admin
            // screen — skip the Firestore read and dropdown setup below since this screen won't be seen.
            if (CheckUserSession()) return;
            FillAllroutes();
        }

        /// <returns>true if a saved session was found and the user was routed away from this screen.</returns>
        private bool CheckUserSession()
        {
            try
            {
                var prefService = new PreferenceService(this);
                var savedUser = prefService.GetSavedUser() as User;
                if (savedUser != null)
                {
                    ProManager.CurrentUser = savedUser;
                    RoleRouter.RouteFor(this, savedUser, finishCaller: false);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(ProManager.TAG, $"CheckUserSession failed: {ex.Message}");
                Toast.MakeText(this, "Error checking user session. Please try again.", ToastLength.Long).Show();
                return false;
            }
        }

        // async void: Android event handlers can't be async Task, so exceptions here would otherwise
        // crash the app unhandled — hence the try/catch. The activity can be destroyed (e.g. user
        // navigates away) while the await is pending, so we re-check IsDestroyed/IsFinishing before
        // touching any views once the awaited call returns.
        private async void FillAllroutes()
        {
            try
            {
                InitViews();
                _allRoutes = await BusesRepository.GetBusesCollection();
                if (IsDestroyed || IsFinishing) return;

                SetupAdapters();
                SetupDropdownBehavior();
                SetupEvents();
                RestoreLastSearch();
                ValidateFields();
            }
            catch (Exception ex)
            {
                Log.Error(ProManager.TAG, $"FillAllroutes failed: {ex.Message}");
                Toast.MakeText(this, "Error loading bus routes. Please try again.", ToastLength.Long).Show();
            }
        }

        private void InitViews()
        {
            autoSchool = FindViewById<AutoCompleteTextView>(Resource.Id.autoSchool);
            autoTown = FindViewById<AutoCompleteTextView>(Resource.Id.autoTown);
            autoBus = FindViewById<AutoCompleteTextView>(Resource.Id.autoBus);
            btnFindBus = FindViewById<Button>(Resource.Id.btnFindBus);

            UIHelper.SetFieldEnabled(autoTown, false);
            UIHelper.SetFieldEnabled(autoBus, false);
            // Click handler isn't wired up until SetupEvents() runs post-await; this keeps the
            // button looking disabled during the load instead of an active-looking dead button.
            ValidateFields();
        }

        private void SetupAdapters()
        {
            autoSchool.Adapter = CreateAdapter(BusesRepository.GetSchools(_allRoutes).ToArray());
            autoTown.Adapter = CreateAdapter(new string[] { });
            autoBus.Adapter = CreateAdapter(new string[] { });
        }

        private void SetupDropdownBehavior()
        {
            UIHelper.ConfigureSearchableField(autoSchool);
            UIHelper.ConfigureSearchableField(autoTown);
            UIHelper.ConfigureSearchableField(autoBus);
        }

        private void SetupEvents()
        {
            autoSchool.ItemClick += OnSchoolSelected;
            autoTown.ItemClick += OnTownSelected;
            autoBus.ItemClick += (s, e) => UIHelper.HideKeyboard(this);

            autoSchool.TextChanged += (s, e) => ValidateFields();
            autoTown.TextChanged += (s, e) => ValidateFields();
            autoBus.TextChanged += (s, e) => ValidateFields();

            btnFindBus.Click += (s, e) => OnFindBusClicked();
        }

        // Cascading dropdown: picking a school invalidates any town/bus picked under a *previous*
        // school, so downstream fields are cleared and re-enabled/re-populated rather than left stale.
        private void OnSchoolSelected(object sender, AdapterView.ItemClickEventArgs e)
        {
            autoTown.Text = string.Empty;
            autoBus.Text = string.Empty;
            UIHelper.SetFieldEnabled(autoTown, true);
            UIHelper.SetFieldEnabled(autoBus, false);
            autoTown.Adapter = CreateAdapter(BusesRepository.GetTownsForSchool(_allRoutes, autoSchool.Text).ToArray());
            UIHelper.HideKeyboard(this);
        }

        private void OnTownSelected(object sender, AdapterView.ItemClickEventArgs e)
        {
            autoBus.Text = string.Empty;
            UIHelper.SetFieldEnabled(autoBus, true);
            autoBus.Adapter = CreateAdapter(BusesRepository.GetBusesForRoute(_allRoutes, autoSchool.Text, autoTown.Text).ToArray());
            UIHelper.HideKeyboard(this);
        }

        // Re-run on every keystroke and every programmatic Text change (see TextChanged wiring in
        // SetupEvents) so the Find-bus button's enabled state always matches SelectionValidator's rules.
        private void ValidateFields()
        {
            bool isReady = _validator.IsValid(autoSchool.Text, autoTown.Text, autoBus.Text);
            btnFindBus.Enabled = isReady;
            btnFindBus.SetBackgroundColor(isReady ? Android.Graphics.Color.ParseColor("#4A90E2") : Android.Graphics.Color.LightGray);
            btnFindBus.Alpha = isReady ? 1.0f : 0.6f;
        }

        private void OnFindBusClicked()
        {
            new PreferenceService(this).SaveLastSearch(autoSchool.Text, autoTown.Text, autoBus.Text);
            var intent = new Android.Content.Intent(this, typeof(PublicBusMapActivity));
            intent.PutExtra("school", autoSchool.Text);
            intent.PutExtra("town", autoTown.Text);
            intent.PutExtra("bus_line", autoBus.Text);
            StartActivity(intent);
        }

        // AutoCompleteTextView re-filters its adapter on every Text change, including programmatic
        // ones — so without ResetDropdownFilter() after each restored value, reopening the dropdown
        // would show only that one matching item instead of the full list (see UIHelper.ResetDropdownFilter).
        private void RestoreLastSearch()
        {
            var (school, town, busLine) = new PreferenceService(this).GetLastSearch();
            if (string.IsNullOrEmpty(school)) return;

            autoSchool.Text = school;
            UIHelper.ResetDropdownFilter(autoSchool);
            UIHelper.SetFieldEnabled(autoTown, true);
            autoTown.Adapter = CreateAdapter(BusesRepository.GetTownsForSchool(_allRoutes, school).ToArray());

            if (!string.IsNullOrEmpty(town))
            {
                autoTown.Text = town;
                UIHelper.ResetDropdownFilter(autoTown);
                UIHelper.SetFieldEnabled(autoBus, true);
                autoBus.Adapter = CreateAdapter(BusesRepository.GetBusesForRoute(_allRoutes, school, town).ToArray());

                if (!string.IsNullOrEmpty(busLine))
                {
                    autoBus.Text = busLine;
                    UIHelper.ResetDropdownFilter(autoBus);
                }
            }
        }

        // Every touch on the activity passes through here before normal view dispatch, which is the
        // only reliable place to detect "user tapped outside the focused field" and dismiss the keyboard.
        public override bool DispatchTouchEvent(MotionEvent ev)
        {
            UIHelper.HandleOutsideTouch(this, ev);
            return base.DispatchTouchEvent(ev);
        }

        private ArrayAdapter<string> CreateAdapter(string[] data) =>
            new ArrayAdapter<string>(this, Resource.Layout.dropdown_item, data);
    }
}
