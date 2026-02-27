using Android.App;
using Android.OS;
using Android.Widget;
using AndroidX.AppCompat.App;
using Android.Graphics;
using Android.Views;
using System;

namespace TimeToSchool
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true)]
    public class MainActivity : AppCompatActivity
    {
        //Test with Kostya
        //Test with Kostya 2

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
        }

        private void SetupAdapters()
        {
            // Pulls data from the Repository class
            autoSchool.Adapter = CreateAdapter(_repository.GetSchools());
            autoTown.Adapter = CreateAdapter(_repository.GetTowns());
            autoBus.Adapter = CreateAdapter(_repository.GetBuses());
        }

        private void SetupDropdownBehavior()
        {
            ConfigureSearchableField(autoSchool);
            ConfigureSearchableField(autoTown);
            ConfigureSearchableField(autoBus);
        }

        private void SetupEvents()
        {
            // Text changes for validation
            autoSchool.TextChanged += (s, e) => ValidateFields();
            autoTown.TextChanged += (s, e) => ValidateFields();
            autoBus.TextChanged += (s, e) => ValidateFields();

            // Keyboard dismissal on selection
            autoSchool.ItemClick += (s, e) => HideKeyboard();
            autoTown.ItemClick += (s, e) => HideKeyboard();
            autoBus.ItemClick += (s, e) => HideKeyboard();

            btnSignIn.Click += (s, e) => OnSignInClicked();
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
            Toast.MakeText(this, $"Signing into {autoSchool.Text}...", ToastLength.Short).Show();
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