using Android.App;
using Android.Graphics;
using Android.OS;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.App;
using Google.Android.Material.TextField;
using System;
namespace TimeToSchool
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true)]
    public class MainActivity : AppCompatActivity
    {
        //Test with Kostya

        // UI Components
        private AutoCompleteTextView autoSchool;
        private AutoCompleteTextView autoTown;
        private AutoCompleteTextView autoBus;
        private Button btnSignIn;

        // Logic & Data Dependencies (SOLID: Separation of Concerns)
        private readonly IDataRepository _repository = new LocalDataRepository();
        private readonly SelectionValidator _validator = new SelectionValidator();

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            SetContentView(Resource.Layout.activity_main);

            // High-level organization
            InitViews();
            SetupAdapters();
            SetupDropdownBehavior();
            SetupEvents();

            // Initial UI State
            ValidateFields();
        }

        private void InitViews()
        {
            autoSchool = FindViewById<AutoCompleteTextView>(Resource.Id.autoSchool);
            autoTown = FindViewById<AutoCompleteTextView>(Resource.Id.autoTown);
            autoBus = FindViewById<AutoCompleteTextView>(Resource.Id.autoBus);
            btnSignIn = FindViewById<Button>(Resource.Id.btnSignIn);

            // Start with Town and Bus disabled
            SetFieldEnabled(autoTown, false);
            SetFieldEnabled(autoBus, false);
        }

        private void SetupAdapters()
        {
            // We only load Schools at the start. 
            // Towns and Buses are empty because we don't know the school yet!
            autoSchool.Adapter = CreateAdapter(_repository.GetSchools().ToArray());

            // Set empty adapters for the others so they don't crash
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
            // Dim the view to 50% opacity if disabled, 100% if enabled
            view.Alpha = isEnabled ? 1.0f : 0.5f;

            // Also disable the parent TextInputLayout to dim the outline/label
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

            // Validation Events
            autoSchool.TextChanged += (s, e) => ValidateFields();
            autoTown.TextChanged += (s, e) => ValidateFields();
            autoBus.TextChanged += (s, e) => ValidateFields();

            // Action Events
            btnSignIn.Click += (s, e) => OnSignInClicked();
        }
        private void OnSchoolSelected(object sender, AdapterView.ItemClickEventArgs e)
        {
            string selectedSchool = autoSchool.Text;

            // Reset and Update UI State
            autoTown.Text = string.Empty;
            autoBus.Text = string.Empty;
            SetFieldEnabled(autoTown, true);
            SetFieldEnabled(autoBus, false);

            // Update Data
            var filteredTowns = _repository.GetTownsForSchool(selectedSchool);
            autoTown.Adapter = CreateAdapter(filteredTowns.ToArray());

            HideKeyboard();
        }
        private void OnTownSelected(object sender, AdapterView.ItemClickEventArgs e)
        {
            string selectedSchool = autoSchool.Text;
            string selectedTown = autoTown.Text;

            // Reset and Update UI State
            autoBus.Text = string.Empty;
            SetFieldEnabled(autoBus, true);

            // Update Data
            var filteredBuses = _repository.GetBusesForRoute(selectedSchool, selectedTown);
            autoBus.Adapter = CreateAdapter(filteredBuses.ToArray());

            HideKeyboard();
        }
        private void ValidateFields()
        {
            // Delegates logic to the Validator class
            bool isReady = _validator.IsValid(autoSchool.Text, autoTown.Text, autoBus.Text);

            btnSignIn.Enabled = isReady;
            btnSignIn.SetBackgroundColor(isReady ? Color.ParseColor("#4A90E2") : Color.Gray);
        }

        private void OnSignInClicked()
        {
            string school = autoSchool.Text;
            string town = autoTown.Text;
            string bus = string.IsNullOrWhiteSpace(autoBus.Text) ? "All Buses" : autoBus.Text;

            Toast.MakeText(this, $"Finding {bus} from {town} to {school}...", ToastLength.Long).Show();
        }

        // --- Helper Methods (Keep the code DRY - Don't Repeat Yourself) ---

        private ArrayAdapter<string> CreateAdapter(string[] data)
        {
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
            var imm = (Android.Views.InputMethods.InputMethodManager)GetSystemService(InputMethodService);
            if (CurrentFocus != null)
            {
                imm.HideSoftInputFromWindow(CurrentFocus.WindowToken, 0);
                CurrentFocus.ClearFocus();
            }
        }

        public override bool DispatchTouchEvent(MotionEvent ev)
        {
            if (ev.Action == MotionEventActions.Down)
            {
                View v = CurrentFocus;
                if (v is EditText)
                {
                    Rect outRect = new Rect();
                    v.GetGlobalVisibleRect(outRect);
                    if (!outRect.Contains((int)ev.RawX, (int)ev.RawY)) HideKeyboard();
                }
            }
            return base.DispatchTouchEvent(ev);
        }
    }
}