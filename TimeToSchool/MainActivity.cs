using Android.App;
using Android.Graphics;
using Android.OS;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.App;
using Google.Android.Material.TextField;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using TimeToSchool.Helpers;
using TimeToSchool.Model;
using TimeToSchool.Service;

namespace TimeToSchool
{
    [Activity(Label = "Time To School",
              MainLauncher = true,
              WindowSoftInputMode = SoftInput.AdjustResize | SoftInput.StateHidden)]
    public class MainActivity : AppCompatActivity
    {
        // UI Components
        private AutoCompleteTextView autoSchool;
        private AutoCompleteTextView autoTown;
        private AutoCompleteTextView autoBus;

        // Buttons
        private Button btnFindBus;
        private Button btnHeaderSignIn;

        // Logic & Data Dependencies
        private readonly SelectionValidator _validator = new SelectionValidator();
        private List<BusRoute> _allRoutes;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            SetContentView(Resource.Layout.activity_main);

            FireBaseHelper.Initialize();
            FillAllroutes();
        }

        private async void FillAllroutes()
        {
            try
            {
                _allRoutes = await BusesRepository.GetBusesCollection();


                InitViews();
                SetupAdapters();
                SetupDropdownBehavior();
                SetupEvents();

                // Initial UI State
                ValidateFields();


            }
            catch (Exception ex)
            {

                //handle excpetion
            }
        }

        private void InitViews()
        {
            autoSchool = FindViewById<AutoCompleteTextView>(Resource.Id.autoSchool);
            autoTown = FindViewById<AutoCompleteTextView>(Resource.Id.autoTown);
            autoBus = FindViewById<AutoCompleteTextView>(Resource.Id.autoBus);

            btnHeaderSignIn = FindViewById<Button>(Resource.Id.btnHeaderSignIn);
            btnFindBus = FindViewById<Button>(Resource.Id.btnFindBus);

            // Start with Town and Bus disabled
            SetFieldEnabled(autoTown, false);
            SetFieldEnabled(autoBus, false);
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

            var parent = view.Parent.Parent as TextInputLayout;
            if (parent != null)
            {
                parent.Enabled = isEnabled;
            }
        }

        private void SetupEvents()
        {
            // Selection Events
            autoSchool.ItemClick += OnSchoolSelected;
            autoTown.ItemClick += OnTownSelected;
            autoBus.ItemClick += (s, e) => HideKeyboard();

            // Validation Events (Triggers button color change)
            autoSchool.TextChanged += (s, e) => ValidateFields();
            autoTown.TextChanged += (s, e) => ValidateFields();
            autoBus.TextChanged += (s, e) => ValidateFields();

            // Action Events
            btnFindBus.Click += (s, e) => OnFindBusClicked();
            btnHeaderSignIn.Click += (s, e) => OnSginInClicked();


        }


        private void OnSchoolSelected(object sender, AdapterView.ItemClickEventArgs e)
        {
            string selectedSchool = autoSchool.Text;
            autoTown.Text = string.Empty;
            autoBus.Text = string.Empty;

            SetFieldEnabled(autoTown, true);
            SetFieldEnabled(autoBus, false);

            var filteredTowns = GetTownsForSchool(_allRoutes, selectedSchool);
            autoTown.Adapter = CreateAdapter(filteredTowns.ToArray());

            HideKeyboard();
        }

        private void OnTownSelected(object sender, AdapterView.ItemClickEventArgs e)
        {
            string selectedSchool = autoSchool.Text;
            string selectedTown = autoTown.Text;

            autoBus.Text = string.Empty;
            SetFieldEnabled(autoBus, true);

            var filteredBuses = GetBusesForRoute(_allRoutes, selectedSchool, selectedTown);
            autoBus.Adapter = CreateAdapter(filteredBuses.ToArray());

            HideKeyboard();
        }

        private void ValidateFields()
        {
            // Logic: Is the form ready to search?
            bool isReady = _validator.IsValid(autoSchool.Text, autoTown.Text, autoBus.Text);

            btnFindBus.Enabled = isReady;
            // High-contrast blue if ready, grey if not
            btnFindBus.SetBackgroundColor(isReady ? Color.ParseColor("#4A90E2") : Color.LightGray);
            btnFindBus.Alpha = isReady ? 1.0f : 0.6f;
        }

        private void OnFindBusClicked()
        {
            string school = autoSchool.Text;
            string town = autoTown.Text;
            string bus = string.IsNullOrWhiteSpace(autoBus.Text) ? "All Buses" : autoBus.Text;

            Toast.MakeText(this, $"Searching for {bus} from {town} to {school}...", ToastLength.Long).Show();
        }
        private void OnSginInClicked()
        {
            Toast.MakeText(this, "Navigating to Sign In...", ToastLength.Short).Show();
            StartActivity(typeof(SignInActivity));
        }

        // --- Helper Methods ---

        private ArrayAdapter<string> CreateAdapter(string[] data)
        {
            // Note: Ensure 'dropdown_item.xml' exists in Resources/layout
            return new ArrayAdapter<string>(this, Resource.Layout.dropdown_item, data);
        }

        private void ConfigureSearchableField(AutoCompleteTextView view)
        {
            view.Threshold = 1;
            view.Click += (s, e) => view.ShowDropDown();
            view.FocusChange += (s, e) => { if (e.HasFocus) view.ShowDropDown(); };
        }

        private void HideKeyboard()
        {
            UIHelper.HideKeyboard(this);
        }

        public override bool DispatchTouchEvent(MotionEvent ev)
        {
            UIHelper.HandleOutsideTouch(this, ev);
            return base.DispatchTouchEvent(ev);
        }
        public List<string> GetSchools(List<BusRoute> _allRoutes)
        {
            return _allRoutes.Select(r => r.School)
                             .Distinct()
                             .OrderBy(s => s)
                             .ToList();
        }

        public List<string> GetTownsForSchool(List<BusRoute> _allRoutes, string schoolName)
        {
            return _allRoutes.Where(r => r.School == schoolName)
                             .Select(r => r.Town)
                             .Distinct()
                             .OrderBy(t => t)
                             .ToList();
        }

        public List<string> GetBusesForRoute(List<BusRoute> _allRoutes, string school, string town)
        {
            var buses = _allRoutes.Where(r => r.School == school && r.Town == town)
                                  .Select(r => r.BusLine)
                                  .OrderBy(b => b)
                                  .ToList();

            if (buses.Count > 0)
            {
                buses.Insert(0, "Any Available Bus");
            }

            return buses;
        }
    }
}
