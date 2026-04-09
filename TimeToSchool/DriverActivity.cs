using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Locations;
using Android.OS;
using Android.Util;
using Android.Widget;
using System;
using System.Threading.Tasks;
using TimeToSchool.BusinessLogic;
using TimeToSchool.Model;
using TimeToSchool.Service;

namespace TimeToSchool
{
    [Activity(Label = "Driver Console", MainLauncher = false)]
    public class DriverActivity : Activity, ILocationListener
    {
        private const string TAG = "YOAV_APP_DEBUG";
        private const int REQUEST_LOCATION_ID = 1001;

        // UI Components
        private Button btnStart, btnStop;
        private TextView statusText;

        // Services & Data Model
        private LocationManager locManager;
        private ActiveBus currentTrip;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.driverpage_layout);

            // SOLID: Single Responsibility - Each method does one setup task
            InitViews();
            LoadTripData();
            SetupServices();

            // Check permissions immediately on startup to avoid late-game crashes
            CheckAndRequestLocationPermission();
        }

        #region Initialization (SOLID: Setup Responsibility)

        private void InitViews()
        {
            btnStart = FindViewById<Button>(Resource.Id.btnStartDrive);
            btnStop = FindViewById<Button>(Resource.Id.btnStopDrive);
            statusText = FindViewById<TextView>(Resource.Id.statusText);

            btnStart.Click += OnStartDriveClicked;
            btnStop.Click += OnStopDriveClicked;

            // Initial State
            btnStop.Enabled = false;
        }

        private void LoadTripData()
        {
            // Pulling data from the Intent (passed from Login/Selection screen)
            currentTrip = new ActiveBus()
            {
                SchoolName = Intent.GetStringExtra("School") ?? "Unknown School",
                Town = Intent.GetStringExtra("Town") ?? "Unknown Town",
                BusLine = Intent.GetStringExtra("Line") ?? "000",
                DriverName = ProManager.CurrentUser.FirstName,
                DriverId = ProManager.CurrentUser.Id,
                Date = DateTime.Now.ToString("yyyy-MM-dd"),
                Status = "Inactive"
            };
        }

        private void SetupServices()
        {
            locManager = (LocationManager)GetSystemService(LocationService);
        }

        #endregion

        #region Permissions (SOLID: Security Responsibility)

        private void CheckAndRequestLocationPermission()
        {
            if (CheckSelfPermission(Android.Manifest.Permission.AccessFineLocation) != Permission.Granted)
            {
                RequestPermissions(new string[] {
                    Android.Manifest.Permission.AccessFineLocation,
                    Android.Manifest.Permission.AccessCoarseLocation
                }, REQUEST_LOCATION_ID);
            }
        }

        // Handle the user's choice (Allow/Deny)
        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
        {
            if (requestCode == REQUEST_LOCATION_ID)
            {
                if (grantResults.Length > 0 && grantResults[0] == Permission.Granted)
                {
                    Toast.MakeText(this, "Permission Granted!", ToastLength.Short).Show();
                }
                else
                {
                    Toast.MakeText(this, "Location permission is required for this app to work.", ToastLength.Long).Show();
                }
            }
        }

        #endregion

        #region Event Handlers (SOLID: Business Logic)

        private async void OnStartDriveClicked(object sender, EventArgs e)
        {
            // Final safety check before starting GPS
            if (CheckSelfPermission(Android.Manifest.Permission.AccessFineLocation) != Permission.Granted)
            {
                CheckAndRequestLocationPermission();
                return;
            }

            try
            {
                UpdateUIState(true);
                currentTrip.Status = "Active";

                // Interval: 15 seconds (15000ms), Distance: 2 meters
                locManager.RequestLocationUpdates(LocationManager.NetworkProvider, 15000, 2, this);

                // Update Firebase immediately so students see the bus go "Online"
                await FireBaseHelper.UpdateBusLocation(currentTrip);
            }
            catch (Exception ex)
            {
                Log.Error(TAG, "GPS Start Error: " + ex.Message);
            }
        }

        private async void OnStopDriveClicked(object sender, EventArgs e)
        {
            UpdateUIState(false);
            locManager.RemoveUpdates(this);

            currentTrip.Status = "Inactive";

            // Mark as Inactive in Firebase so the icon disappears for students
            await FireBaseHelper.UpdateBusLocation(currentTrip);
        }

        #endregion

        #region UI & Location Logic

        private void UpdateUIState(bool isTracking)
        {
            btnStart.Enabled = !isTracking;
            btnStop.Enabled = isTracking;

            if (isTracking)
            {
                statusText.Text = "Status: LIVE TRACKING (15s)";
                statusText.SetTextColor(Android.Graphics.Color.Green);
            }
            else
            {
                statusText.Text = "Status: OFFLINE";
                statusText.SetTextColor(Android.Graphics.Color.Red);
            }
        }

        public void OnLocationChanged(Location location)
        {
            Log.Debug(TAG, $"GPS Update: {location.Latitude}, {location.Longitude}");

            // Update the Model
            currentTrip.Latitude = location.Latitude;
            currentTrip.Longitude = location.Longitude;

            // "Fire and Forget" update to Firebase
            _ = FireBaseHelper.UpdateBusLocation(currentTrip);
        }

        // Mandatory interface methods
        public void OnProviderDisabled(string provider) => Log.Warn(TAG, "GPS Provider Disabled");
        public void OnProviderEnabled(string provider) => Log.Info(TAG, "GPS Provider Enabled");
        public void OnStatusChanged(string provider, Availability status, Bundle extras) { }

        #endregion
    }
}