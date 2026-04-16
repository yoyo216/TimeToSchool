using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Locations;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;
using Google.Android.Material.TextField;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TimeToSchool.BusinessLogic;
using TimeToSchool.Model;
using TimeToSchool.Service;

namespace TimeToSchool
{
    [Activity(Label = "Driver Console", MainLauncher = false)]
    public class DriverActivity : Activity, ILocationListener
    {
        private const int REQUEST_LOCATION_ID = 1001;

        // UI Components
        private LinearLayout cardsContainer;
        private ImageButton btnAddRoute;
        private TextView globalStatusText;

        // Data & Services
        private LocationManager locManager;
        private List<DriverCardState> _driverCards = new List<DriverCardState>();
        private List<BusRoute> _allRoutes;
        private bool _isGlobalDriving = false;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.driverpage_layout);

            InitViews();
            SetupServices();
            CheckAndRequestLocationPermission();
            LoadInitialData();
        }

        private void InitViews()
        {
            cardsContainer = FindViewById<LinearLayout>(Resource.Id.cardsContainer);
            btnAddRoute = FindViewById<ImageButton>(Resource.Id.btnAddRoute);
            globalStatusText = FindViewById<TextView>(Resource.Id.globalStatusText);

            btnAddRoute.Click += (s, e) =>
            {
                if (_isGlobalDriving)
                {
                    Toast.MakeText(this, "לא ניתן להוסיף מסלול בזמן נסיעה", ToastLength.Short).Show();
                    return;
                }
                _driverCards.Add(new DriverCardState { TripData = new ActiveBus() });
                RenderCards();
            };
        }

        private void SetupServices()
        {
            locManager = (LocationManager)GetSystemService(LocationService);
        }

        private async void LoadInitialData()
        {
            // Fetch the static database of buses for the dialogs
            _allRoutes = await BusesRepository.GetBusesCollection();

            // Start with one empty card if none exist
            if (_driverCards.Count == 0)
                _driverCards.Add(new DriverCardState { TripData = new ActiveBus() });

            RenderCards();
        }

        #region UI Rendering Engine

        private void RenderCards()
        {
            cardsContainer.RemoveAllViews();

            foreach (var state in _driverCards)
            {
                // 1. Inflate the custom card item
                View cardView = LayoutInflater.From(this).Inflate(Resource.Layout.driver_route_item, null);

                // 2. Find Views
                var tvSchool = cardView.FindViewById<TextView>(Resource.Id.tvSchoolName);
                var tvStatus = cardView.FindViewById<TextView>(Resource.Id.tvStatusLabel);
                var btnAction = cardView.FindViewById<LinearLayout>(Resource.Id.btnTripAction);
                var btnSettings = cardView.FindViewById<ImageButton>(Resource.Id.btnSettings);

                // 3. Set Text (Using BusLine name as requested)
                tvSchool.Text = !string.IsNullOrEmpty(state.TripData.SchoolName)
                                ? $"{state.TripData.SchoolName} - קו {state.TripData.BusLine}"
                                : "לחץ על ההגדרות לבחירת מסלול";

                // 4. Apply Visual States (Red/Green/Grey)
                ApplyVisualState(state, btnAction, tvStatus, btnSettings);

                // 5. Events
                btnAction.Click += (s, e) => ToggleTrip(state);
                btnSettings.Click += (s, e) => OpenRouteSelectionDialog(state);

                cardsContainer.AddView(cardView);
            }

            globalStatusText.Text = _isGlobalDriving ? "נסיעה פעילה" : "מוכן לנסיעה";
        }

        private void ApplyVisualState(DriverCardState state, View btnAction, TextView tvStatus, View btnSettings)
        {
            if (state.IsDriving)
            {
                btnAction.SetBackgroundColor(Android.Graphics.Color.Red);
                tvStatus.Text = "סיום נסיעה";
                btnSettings.Visibility = ViewStates.Gone;
            }
            else if (_isGlobalDriving)
            {
                btnAction.SetBackgroundColor(Android.Graphics.Color.Gray);
                btnAction.Enabled = false;
                btnAction.Alpha = 0.5f;
                tvStatus.Text = "ממתין...";
                btnSettings.Enabled = false;
            }
            else
            {
                btnAction.SetBackgroundColor(Android.Graphics.Color.ParseColor("#4CAF50")); // Material Green
                tvStatus.Text = "התחל נסיעה";
                btnAction.Enabled = true;
                btnAction.Alpha = 1.0f;
                btnSettings.Visibility = ViewStates.Visible;
            }
        }

        #endregion

        #region Business Logic (Toggle & Dialog)

        private async void ToggleTrip(DriverCardState state)
        {
            if (state.IsDriving)
            {
                // --- STOP LOGIC ---
                state.IsDriving = false;
                _isGlobalDriving = false;
                locManager.RemoveUpdates(this);

                state.TripData.Status = "Inactive";
                await BusesRepository.UpdateBusLocation(state.TripData);
            }
            else
            {
                // --- START LOGIC ---
                if (string.IsNullOrEmpty(state.TripData.BusLine))
                {
                    Toast.MakeText(this, "אנא הגדר מסלול תחילה", ToastLength.Short).Show();
                    return;
                }

                state.IsDriving = true;
                _isGlobalDriving = true;

                state.TripData.Status = "Active";
                state.TripData.DriverName = ProManager.CurrentUser.FirstName;
                state.TripData.DriverId = ProManager.CurrentUser.Id;
                state.TripData.Date = DateTime.Now.ToString("yyyy-MM-dd");

                // Request Updates (15s, 2m as per original logic)
                locManager.RequestLocationUpdates(LocationManager.NetworkProvider, 15000, 2, this);

                // Create document and save ID
                await BusesRepository.UpdateBusLocation(state.TripData);
            }
            RenderCards();
        }
        private ArrayAdapter<string> CreateAdapter(string[] data)
        {
            // Note: Ensure 'dropdown_item.xml' exists in Resources/layout
            return new ArrayAdapter<string>(this, Resource.Layout.dropdown_item, data);
        }
        private void OpenRouteSelectionDialog(DriverCardState state)
        {
            Dialog dialog = new Dialog(this);
            dialog.SetContentView(Resource.Layout.dialog_route_selector);
            dialog.Window.SetLayout(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);

            // --- 1. Init Views (from dialog layout) ---
            var autoSchool = dialog.FindViewById<AutoCompleteTextView>(Resource.Id.dialogAutoSchool);
            var autoTown = dialog.FindViewById<AutoCompleteTextView>(Resource.Id.dialogAutoTown);
            var autoBus = dialog.FindViewById<AutoCompleteTextView>(Resource.Id.dialogAutoBus);
            var btnSave = dialog.FindViewById<Button>(Resource.Id.btnSaveRoute);

            // --- 2. Setup Initial Dropdown Behavior ---
            ConfigureSearchableField(autoSchool);
            ConfigureSearchableField(autoTown);
            ConfigureSearchableField(autoBus);

            // Initial state: Only School is ready
            autoSchool.Adapter = CreateAdapter(GetSchools(_allRoutes).ToArray());
            SetDialogFieldEnabled(autoTown, false);
            SetDialogFieldEnabled(autoBus, false);

            // --- 3. Selection Events (Cascading Logic from MainActivity) ---

            autoSchool.ItemClick += (s, e) =>
            {
                string selectedSchool = autoSchool.Text;
                autoTown.Text = string.Empty;
                autoBus.Text = string.Empty;

                SetDialogFieldEnabled(autoTown, true);
                SetDialogFieldEnabled(autoBus, false);

                var filteredTowns = GetTownsForSchool(_allRoutes, selectedSchool);
                autoTown.Adapter = CreateAdapter(filteredTowns.ToArray());
                autoTown.ShowDropDown();
            };

            autoTown.ItemClick += (s, e) =>
            {
                string selectedSchool = autoSchool.Text;
                string selectedTown = autoTown.Text;

                autoBus.Text = string.Empty;
                SetDialogFieldEnabled(autoBus, true);

                var filteredBuses = GetBusesForRoute(_allRoutes, selectedSchool, selectedTown);
                autoBus.Adapter = CreateAdapter(filteredBuses.ToArray());
                autoBus.ShowDropDown();
            };

            // --- 4. Save Logic ---
            btnSave.Click += (s, e) =>
            {
                if (string.IsNullOrEmpty(autoSchool.Text) || string.IsNullOrEmpty(autoBus.Text))
                {
                    Toast.MakeText(this, "אנא השלם את כל השדות", ToastLength.Short).Show();
                    return;
                }

                // Update the state object (ActiveBus model)
                state.TripData.SchoolName = autoSchool.Text;
                state.TripData.Town = autoTown.Text;
                state.TripData.BusLine = autoBus.Text;

                RenderCards(); // Refresh dashboard
                dialog.Dismiss();
            };

            dialog.Show();
        }
        private void ConfigureSearchableField(AutoCompleteTextView view)
        {
            view.Threshold = 1;
            view.Click += (s, e) => view.ShowDropDown();
            view.FocusChange += (s, e) => { if (e.HasFocus) view.ShowDropDown(); };
        }
        private void SetDialogFieldEnabled(AutoCompleteTextView view, bool isEnabled)
        {
            view.Enabled = isEnabled;
            view.Alpha = isEnabled ? 1.0f : 0.5f;

            var parent = view.Parent.Parent as TextInputLayout;
            if (parent != null)
            {
                parent.Enabled = isEnabled;
            }
        }
        #endregion

        #region Location Listener Implementation

        public void OnLocationChanged(Location location)
        {
            var activeCard = _driverCards.FirstOrDefault(c => c.IsDriving);
            if (activeCard != null)
            {
                activeCard.TripData.Latitude = location.Latitude;
                activeCard.TripData.Longitude = location.Longitude;
                _ = BusesRepository.UpdateBusLocation(activeCard.TripData);
            }
        }

        public void OnProviderDisabled(string provider) { }
        public void OnProviderEnabled(string provider) { }
        public void OnStatusChanged(string provider, Availability status, Bundle extras) { }

        #endregion

        private void CheckAndRequestLocationPermission()
        {
            if (CheckSelfPermission(Android.Manifest.Permission.AccessFineLocation) != Permission.Granted)
            {
                RequestPermissions(new string[] { Android.Manifest.Permission.AccessFineLocation }, REQUEST_LOCATION_ID);
            }
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


            return buses;
        }
    }

    public class DriverCardState
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string FirebaseDocumentId { get; set; } // To track the live trip doc
        public ActiveBus TripData { get; set; } // Your existing model
        public bool IsDriving { get; set; } = false;
    }
}