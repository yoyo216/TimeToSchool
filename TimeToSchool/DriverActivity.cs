using Android.App;
using Android.Content.PM;
using Android.Locations;
using Android.OS;
using Android.Views;
using Android.Widget;
using Google.Android.Material.TextField;
using System;
using System.Collections.Generic;
using System.Linq;
using TimeToSchool.BusinessLogic;
using TimeToSchool.Model;
using TimeToSchool.Service;

namespace TimeToSchool
{
    [Activity(Label = "Driver Console", MainLauncher = false)]  
    public class DriverActivity : BaseDrawerActivity, ILocationListener
    {
        private const int REQUEST_LOCATION_ID = 1001;

        private LinearLayout cardsContainer;
        private TextView globalStatusText;

        private LocationManager locManager;
        private PreferenceService _prefService;
        private List<DriverCardState> _driverCards = new List<DriverCardState>();
        private List<BusRoute> _allRoutes;
        private bool _isGlobalDriving = false;

        protected override int GetContentLayoutId() => Resource.Layout.driverpage_layout;

        protected override void OnCreateDrawerContent(Bundle savedInstanceState)
        {
            InitViews();
            SetupServices();
            CheckAndRequestLocationPermission();
            LoadInitialData();
        }

        public override bool OnCreateOptionsMenu(IMenu menu)
        {
            MenuInflater.Inflate(Resource.Menu.driver_menu, menu);
            return true;
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            if (item.ItemId == Resource.Id.action_add_route)
            {
                if (_isGlobalDriving)
                {
                    Android.Widget.Toast.MakeText(this, "לא ניתן להוסיף מסלול בזמן נסיעה", ToastLength.Short).Show();
                    return true;
                }
                _driverCards.Add(new DriverCardState { TripData = new ActiveBus() });
                SaveCardsIfRemembered();
                RenderCards();
                return true;
            }
            return base.OnOptionsItemSelected(item);
        }

        private void InitViews()
        {
            cardsContainer = FindViewById<LinearLayout>(Resource.Id.cardsContainer);
            globalStatusText = FindViewById<TextView>(Resource.Id.globalStatusText);
        }

        private void SetupServices()
        {
            locManager = (LocationManager)GetSystemService(LocationService);
            _prefService = new PreferenceService(this);
        }

        private bool IsRememberMeActive() => _prefService.GetSavedUser() != null;

        private void SaveCardsIfRemembered()
        {
            if (IsRememberMeActive())
                _prefService.SaveDriverCards(_driverCards);
        }

        protected override void OnPause()
        {
            base.OnPause();
            SaveCardsIfRemembered();
        }

        private async void LoadInitialData()
        {
            _allRoutes = await BusesRepository.GetBusesCollection();

            var savedCards = _prefService.GetDriverCards();
            if (savedCards != null && savedCards.Count > 0)
                _driverCards = savedCards;
            else if (_driverCards.Count == 0)
                _driverCards.Add(new DriverCardState { TripData = new ActiveBus() });

            RenderCards();
        }

        #region UI Rendering Engine

        private void RenderCards()
        {
            cardsContainer.RemoveAllViews();

            foreach (var state in _driverCards)
            {
                View cardView = LayoutInflater.From(this).Inflate(Resource.Layout.driver_route_item, null);

                var tvSchool = cardView.FindViewById<TextView>(Resource.Id.tvSchoolName);
                var tvStatus = cardView.FindViewById<TextView>(Resource.Id.tvStatusLabel);
                var btnAction = cardView.FindViewById<LinearLayout>(Resource.Id.btnTripAction);
                var btnSettings = cardView.FindViewById<ImageButton>(Resource.Id.btnSettings);

                tvSchool.Text = !string.IsNullOrEmpty(state.TripData.SchoolName)
                                ? $"{state.TripData.SchoolName} - קו {state.TripData.BusLine}"
                                : "לחץ על ההגדרות לבחירת מסלול";

                ApplyVisualState(state, btnAction, tvStatus, btnSettings);

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
                btnAction.SetBackgroundColor(Android.Graphics.Color.ParseColor("#4CAF50"));
                tvStatus.Text = "התחל נסיעה";
                btnAction.Enabled = true;
                btnAction.Alpha = 1.0f;
                btnSettings.Visibility = ViewStates.Visible;
            }
        }

        #endregion

        #region Business Logic

        private async void ToggleTrip(DriverCardState state)
        {
            if (state.IsDriving)
            {
                state.IsDriving = false;
                _isGlobalDriving = false;
                locManager.RemoveUpdates(this);

                state.TripData.Status = "Inactive";
                await BusesRepository.UpdateBusLocation(state.TripData);
            }
            else
            {
                if (string.IsNullOrEmpty(state.TripData.BusLine))
                {
                    Android.Widget.Toast.MakeText(this, "אנא הגדר מסלול תחילה", ToastLength.Short).Show();
                    return;
                }

                state.IsDriving = true;
                _isGlobalDriving = true;

                state.TripData.Status = "Active";
                state.TripData.DriverName = ProManager.CurrentUser.FirstName;
                state.TripData.DriverId = ProManager.CurrentUser.Id;
                state.TripData.Date = DateTime.Now.ToString("yyyy-MM-dd");

                locManager.RequestLocationUpdates(LocationManager.NetworkProvider, 15000, 2, this);
                await BusesRepository.UpdateBusLocation(state.TripData);
            }
            RenderCards();
        }

        private void OpenRouteSelectionDialog(DriverCardState state)
        {
            Dialog dialog = new Dialog(this);
            dialog.SetContentView(Resource.Layout.dialog_route_selector);
            dialog.Window.SetLayout(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);

            var autoSchool = dialog.FindViewById<AutoCompleteTextView>(Resource.Id.dialogAutoSchool);
            var autoTown = dialog.FindViewById<AutoCompleteTextView>(Resource.Id.dialogAutoTown);
            var autoBus = dialog.FindViewById<AutoCompleteTextView>(Resource.Id.dialogAutoBus);
            var btnSave = dialog.FindViewById<Button>(Resource.Id.btnSaveRoute);

            ConfigureSearchableField(autoSchool);
            ConfigureSearchableField(autoTown);
            ConfigureSearchableField(autoBus);

            autoSchool.Adapter = CreateAdapter(GetSchools(_allRoutes).ToArray());
            SetDialogFieldEnabled(autoTown, false);
            SetDialogFieldEnabled(autoBus, false);

            autoSchool.ItemClick += (s, e) =>
            {
                string selectedSchool = autoSchool.Text;
                autoTown.Text = string.Empty;
                autoBus.Text = string.Empty;
                SetDialogFieldEnabled(autoTown, true);
                SetDialogFieldEnabled(autoBus, false);
                autoTown.Adapter = CreateAdapter(GetTownsForSchool(_allRoutes, selectedSchool).ToArray());
                autoTown.ShowDropDown();
            };

            autoTown.ItemClick += (s, e) =>
            {
                autoBus.Text = string.Empty;
                SetDialogFieldEnabled(autoBus, true);
                autoBus.Adapter = CreateAdapter(GetBusesForRoute(_allRoutes, autoSchool.Text, autoTown.Text).ToArray());
                autoBus.ShowDropDown();
            };

            btnSave.Click += (s, e) =>
            {
                if (string.IsNullOrEmpty(autoSchool.Text) || string.IsNullOrEmpty(autoBus.Text))
                {
                    Android.Widget.Toast.MakeText(this, "אנא השלם את כל השדות", ToastLength.Short).Show();
                    return;
                }
                state.TripData.SchoolName = autoSchool.Text;
                state.TripData.Town = autoTown.Text;
                state.TripData.BusLine = autoBus.Text;
                SaveCardsIfRemembered();
                RenderCards();
                dialog.Dismiss();
            };

            dialog.Show();
        }

        #endregion

        #region Location Listener

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
        public void OnStatusChanged(string provider, Availability status, Android.OS.Bundle extras) { }

        #endregion

        private void CheckAndRequestLocationPermission()
        {
            if (CheckSelfPermission(Android.Manifest.Permission.AccessFineLocation) != Permission.Granted)
                RequestPermissions(new string[] { Android.Manifest.Permission.AccessFineLocation }, REQUEST_LOCATION_ID);
        }

        private ArrayAdapter<string> CreateAdapter(string[] data) =>
            new ArrayAdapter<string>(this, Resource.Layout.dropdown_item, data);

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
            if (view.Parent?.Parent is TextInputLayout layout)
                layout.Enabled = isEnabled;
        }

        public List<string> GetSchools(List<BusRoute> routes) =>
            routes.Select(r => r.School).Distinct().OrderBy(s => s).ToList();

        public List<string> GetTownsForSchool(List<BusRoute> routes, string school) =>
            routes.Where(r => r.School == school).Select(r => r.Town).Distinct().OrderBy(t => t).ToList();

        public List<string> GetBusesForRoute(List<BusRoute> routes, string school, string town) =>
            routes.Where(r => r.School == school && r.Town == town).Select(r => r.BusLine).OrderBy(b => b).ToList();
    }

    public class DriverCardState
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string FirebaseDocumentId { get; set; }
        public ActiveBus TripData { get; set; }
        public bool IsDriving { get; set; } = false;
    }
}
