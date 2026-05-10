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

            UIHelper.SetFieldEnabled(autoTown, false);
            UIHelper.SetFieldEnabled(autoBus, false);
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
            UIHelper.SetFieldEnabled(autoTown, true);
            autoTown.Adapter = CreateAdapter(BusesRepository.GetTownsForSchool(_allRoutes, school).ToArray());

            if (!string.IsNullOrEmpty(town))
            {
                autoTown.Text = town;
                UIHelper.SetFieldEnabled(autoBus, true);
                autoBus.Adapter = CreateAdapter(BusesRepository.GetBusesForRoute(_allRoutes, school, town).ToArray());

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
    }
}
