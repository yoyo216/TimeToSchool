using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Locations;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Widget;
using System;
using TimeToSchool.BusinessLogic;
using TimeToSchool.Service;

namespace TimeToSchool
{
    [Activity(Label = "Driver Console")]
    public class DriverActivity : Activity, ILocationListener
    {
        private Button btnStartDrive;
        private Button btnStopDrive;
        private TextView statusText;

        private LocationManager locManager;
        private string currentBusLine = "Line_102"; // You can set this via Intent later

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.driverpage_layout);
            // Initialize Helper and Location Manager
            locManager = (LocationManager)GetSystemService(LocationService);

            InitilizeViews();
            RequestLocationPermission();
        }

        private void InitilizeViews()
        {
            btnStartDrive = FindViewById<Button>(Resource.Id.btnStartDrive);
            btnStopDrive = FindViewById<Button>(Resource.Id.btnStopDrive);
            statusText = FindViewById<TextView>(Resource.Id.statusText);

            btnStartDrive.Click += (s, e) => OnStartDriveClicked();
            btnStopDrive.Click += (s, e) => OnStopDriveClicked();

            btnStopDrive.Enabled = false;
        }

        private void OnStartDriveClicked()
        {
            try
            {
                // Request updates: (15 seconds) or 20 meters of movement
                locManager.RequestLocationUpdates(LocationManager.GpsProvider, 15000, 20, this);

                btnStartDrive.Enabled = false;
                btnStopDrive.Enabled = true;
                statusText.Text = "STATUS: TRACKING ACTIVE";
                statusText.SetTextColor(Color.Green);

                Toast.MakeText(this, "GPS Tracking Started", ToastLength.Short).Show();
            }
            catch (Exception ex)
            {
                Log.Debug(ProManager.TAG, "error:" + ex.Message);
                Toast.MakeText(this, "Error: " + ex.Message, ToastLength.Long).Show();
            }
        }

        private void OnStopDriveClicked()
        {
            locManager.RemoveUpdates(this);

            btnStartDrive.Enabled = true;
            btnStopDrive.Enabled = false;
            statusText.Text = "STATUS: IDLE";
            statusText.SetTextColor(Color.Red);

            Toast.MakeText(this, "GPS Tracking Stopped", ToastLength.Short).Show();
        }

        // --- ILocationListener implementation ---

        public void OnLocationChanged(Location location)
        {
            // Whenever the driver moves, call your FirebaseHelper
            FireBaseHelper.UpdateBusLocation(currentBusLine, location.Latitude, location.Longitude);
        }

        public void OnProviderDisabled(string provider) { }
        public void OnProviderEnabled(string provider) { }
        public void OnStatusChanged(string provider, Availability status, Bundle extras) { }
        private void RequestLocationPermission()
        {
            // Check if we already have it
            if (CheckSelfPermission(Android.Manifest.Permission.AccessFineLocation) != Android.Content.PM.Permission.Granted)
            {
                // This triggers the Android "Allow YoavApp to access this device's location?" pop-up
                RequestPermissions(new string[] {
            Android.Manifest.Permission.AccessFineLocation,
            Android.Manifest.Permission.AccessCoarseLocation
        }, 1000);
            }
        }
    }
}