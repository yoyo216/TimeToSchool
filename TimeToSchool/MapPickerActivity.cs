using Android.App;
using Android.Content;
using Android.Gms.Maps;
using Android.Gms.Maps.Model;
using Android.Locations;
using Android.OS;
using Android.Widget;
using AndroidX.AppCompat.App;
using System;
using System.Threading.Tasks;
using TimeToSchool.BusinessLogic;

namespace TimeToSchool
{
    [Activity(Label = "בחר מיקום")]
    public class MapPickerActivity : AppCompatActivity, IOnMapReadyCallback, GoogleMap.IOnMapClickListener
    {
        private GoogleMap _map;
        private Marker _marker;
        private LatLng _selectedLatLng;
        private Button _btnConfirm;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_map_picker);

            _btnConfirm = FindViewById<Button>(Resource.Id.btnConfirmLocation);
            _btnConfirm.Enabled = false;
            _btnConfirm.Click += OnConfirmClicked;

            var mapFragment = (SupportMapFragment)SupportFragmentManager.FindFragmentById(Resource.Id.mapFragment);
            mapFragment.GetMapAsync(this);
        }

        public void OnMapReady(GoogleMap googleMap)
        {
            _map = googleMap;
            _map.SetOnMapClickListener(this);

            double existingLat = Intent.GetDoubleExtra("existingLat", double.MinValue);
            double existingLng = Intent.GetDoubleExtra("existingLng", double.MinValue);

            if (existingLat != double.MinValue && existingLng != double.MinValue)
            {
                var pos = new LatLng(existingLat, existingLng);
                PlaceMarker(pos);
                _map.MoveCamera(CameraUpdateFactory.NewLatLngZoom(pos, 15f));
            }
            else
            {
                GeocodeTownAsync(Intent.GetStringExtra("town") ?? "");
            }
        }

        private async void GeocodeTownAsync(string town)
        {
            try
            {
                var defaultPos = new LatLng(31.5, 34.8);
                if (string.IsNullOrWhiteSpace(town))
                {
                    _map.MoveCamera(CameraUpdateFactory.NewLatLngZoom(defaultPos, 8f));
                    return;
                }
                var geocoder = new Geocoder(this);
                var addresses = await Task.Run(() => geocoder.GetFromLocationName(town + ", Israel", 1));
                if (addresses != null && addresses.Count > 0)
                {
                    var pos = new LatLng(addresses[0].Latitude, addresses[0].Longitude);
                    _map.MoveCamera(CameraUpdateFactory.NewLatLngZoom(pos, 14f));
                }
                else
                {
                    _map.MoveCamera(CameraUpdateFactory.NewLatLngZoom(defaultPos, 8f));
                }
            }
            catch (Exception ex)
            {
                Android.Util.Log.Error(ProManager.TAG, "Geocode error: " + ex.Message);
            }
        }

        public void OnMapClick(LatLng point)
        {
            PlaceMarker(point);
        }

        private void PlaceMarker(LatLng position)
        {
            _marker?.Remove();
            _selectedLatLng = position;
            var markerOpts = new MarkerOptions();
            markerOpts.SetPosition(position);
            _marker = _map.AddMarker(markerOpts);
            _btnConfirm.Enabled = true;
        }

        private void OnConfirmClicked(object sender, EventArgs e)
        {
            if (_selectedLatLng == null) return;
            var result = new Intent();
            result.PutExtra("lat", _selectedLatLng.Latitude);
            result.PutExtra("lng", _selectedLatLng.Longitude);
            SetResult(Result.Ok, result);
            Finish();
        }
    }
}
