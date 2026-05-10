using Android.App;
using Android.OS;
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

        protected override void OnCreateDrawerContent(Bundle savedInstanceState)
        {
            FireBaseHelper.Initialize();
            CheckUserSession();
            FillAllroutes();
        }

        private void CheckUserSession()
        {
            try
            {
                var prefService = new PreferenceService(this);
                var savedUser = prefService.GetSavedUser() as User;
                if (savedUser != null)
                {
                    ProManager.CurrentUser = savedUser;
                    RoleRouter.RouteFor(this, savedUser, finishCaller: false);
                }
            }
            catch (Exception)
            {
                Toast.MakeText(this, "Error checking user session. Please try again.", ToastLength.Long).Show();
            }
        }

        private async void FillAllroutes()
        {
            try
            {
                InitViews();
                _allRoutes = await BusesRepository.GetBusesCollection();
                SetupAdapters();
                SetupDropdownBehavior();
                SetupEvents();
                RestoreLastSearch();
                ValidateFields();
            }
            catch (Exception) { }
        }

        private void InitViews()
        {
            autoSchool = FindViewById<AutoCompleteTextView>(Resource.Id.autoSchool);
            autoTown = FindViewById<AutoCompleteTextView>(Resource.Id.autoTown);
            autoBus = FindViewById<AutoCompleteTextView>(Resource.Id.autoBus);
            btnFindBus = FindViewById<Button>(Resource.Id.btnFindBus);

            SetFieldEnabled(autoTown, false);
            SetFieldEnabled(autoBus, false);
            ValidateFields();

        }

        private void SetupAdapters()
        {
            autoSchool.Adapter = CreateAdapter(GetSchools(_allRoutes).ToArray());
            autoTown.Adapter = CreateAdapter(new string[] { });
            autoBus.Adapter = CreateAdapter(new string[] { });
        }

        private void SetupDropdownBehavior()
        {
            ConfigureSearchableField(autoSchool);
            ConfigureSearchableField(autoTown);
            ConfigureSearchableField(autoBus);
        }

        private void SetFieldEnabled(AutoCompleteTextView view, bool isEnabled)
        {
            view.Enabled = isEnabled;
            view.Alpha = isEnabled ? 1.0f : 0.5f;
            if (view.Parent?.Parent is TextInputLayout layout)
                layout.Enabled = isEnabled;
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

        private void OnSchoolSelected(object sender, AdapterView.ItemClickEventArgs e)
        {
            autoTown.Text = string.Empty;
            autoBus.Text = string.Empty;
            SetFieldEnabled(autoTown, true);
            SetFieldEnabled(autoBus, false);
            autoTown.Adapter = CreateAdapter(GetTownsForSchool(_allRoutes, autoSchool.Text).ToArray());
            UIHelper.HideKeyboard(this);
        }

        private void OnTownSelected(object sender, AdapterView.ItemClickEventArgs e)
        {
            autoBus.Text = string.Empty;
            SetFieldEnabled(autoBus, true);
            autoBus.Adapter = CreateAdapter(GetBusesForRoute(_allRoutes, autoSchool.Text, autoTown.Text).ToArray());
            UIHelper.HideKeyboard(this);
        }

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

        private void RestoreLastSearch()
        {
            var (school, town, busLine) = new PreferenceService(this).GetLastSearch();
            if (string.IsNullOrEmpty(school)) return;

            autoSchool.Text = school;
            SetFieldEnabled(autoTown, true);
            autoTown.Adapter = CreateAdapter(GetTownsForSchool(_allRoutes, school).ToArray());

            if (!string.IsNullOrEmpty(town))
            {
                autoTown.Text = town;
                SetFieldEnabled(autoBus, true);
                autoBus.Adapter = CreateAdapter(GetBusesForRoute(_allRoutes, school, town).ToArray());

                if (!string.IsNullOrEmpty(busLine))
                    autoBus.Text = busLine;
            }
        }

        public override bool DispatchTouchEvent(MotionEvent ev)
        {
            UIHelper.HandleOutsideTouch(this, ev);
            return base.DispatchTouchEvent(ev);
        }

        private ArrayAdapter<string> CreateAdapter(string[] data) =>
            new ArrayAdapter<string>(this, Resource.Layout.dropdown_item, data);

        private void ConfigureSearchableField(AutoCompleteTextView view)
        {
            view.Threshold = 1;
            view.Click += (s, e) => view.ShowDropDown();
            view.FocusChange += (s, e) => { if (e.HasFocus) view.ShowDropDown(); };
        }

        public List<string> GetSchools(List<BusRoute> routes) =>
            routes.Select(r => r.School).Distinct().OrderBy(s => s).ToList();

        public List<string> GetTownsForSchool(List<BusRoute> routes, string school) =>
            routes.Where(r => r.School == school).Select(r => r.Town).Distinct().OrderBy(t => t).ToList();

        public List<string> GetBusesForRoute(List<BusRoute> routes, string school, string town)
        {
            var buses = routes.Where(r => r.School == school && r.Town == town)
                              .Select(r => r.BusLine).OrderBy(b => b).ToList();
            if (buses.Count > 0)
                buses.Insert(0, "Any Available Bus");
            return buses;
        }
    }
}
