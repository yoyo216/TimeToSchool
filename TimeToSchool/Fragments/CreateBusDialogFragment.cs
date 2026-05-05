using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TimeToSchool.Model;
using TimeToSchool.Service;
using TimeToSchool;
using TimeToSchool.BusinessLogic;

namespace TimeToSchool.Fragments
{
    public class CreateBusDialogFragment : AndroidX.Fragment.App.DialogFragment
    {
        private AutoCompleteTextView etSchool;
        private EditText etTown, etBusLine;
        private EditText etFirstStopLat, etFirstStopLng;
        private Button btnSave, btnCancel, btnPickLocation;
        private string _currentBusId = null;
        private const int MAP_PICKER_REQUEST = 101;

        public static CreateBusDialogFragment NewInstance(BusRoute bus = null)
        {
            var frag = new CreateBusDialogFragment();
            if (bus != null)
            {
                var args = new Bundle();
                args.PutString("busId", bus.Id);
                args.PutString("school", bus.School);
                args.PutString("town", bus.Town);
                args.PutString("busLine", bus.BusLine);
                if (bus.FirstStopLat.HasValue)
                {
                    args.PutDouble("firstStopLat", bus.FirstStopLat.Value);
                    args.PutDouble("firstStopLng", bus.FirstStopLng.Value);
                }
                frag.Arguments = args;
            }
            return frag;
        }

        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            Dialog.Window.RequestFeature(WindowFeatures.NoTitle);
            return inflater.Inflate(Resource.Layout.dialog_bus_form, container, false);
        }

        public override void OnViewCreated(View view, Bundle savedInstanceState)
        {
            base.OnViewCreated(view, savedInstanceState);

            // 1. Initialize Views
            etSchool = view.FindViewById<AutoCompleteTextView>(Resource.Id.etSchool);
            etTown = view.FindViewById<EditText>(Resource.Id.etTown);
            etBusLine = view.FindViewById<EditText>(Resource.Id.etBusLine);
            etFirstStopLat = view.FindViewById<EditText>(Resource.Id.etFirstStopLat);
            etFirstStopLng = view.FindViewById<EditText>(Resource.Id.etFirstStopLng);
            btnPickLocation = view.FindViewById<Button>(Resource.Id.btnPickLocation);
            btnSave = view.FindViewById<Button>(Resource.Id.btnSaveBus);
            btnCancel = view.FindViewById<Button>(Resource.Id.btnCancel);

            // 2. Load school list from asset and wire autocomplete
            try
            {
                using (var reader = new StreamReader(Activity.Assets.Open("schools.json")))
                {
                    var schools = JsonConvert.DeserializeObject<List<string>>(reader.ReadToEnd());
                    var adapter = new SubstringArrayAdapter(Activity, Resource.Layout.dropdown_item, schools.ToArray());
                    etSchool.Adapter = adapter;
                    etSchool.Threshold = 1;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ProManager.TAG, "Failed to load schools: " + ex.Message);
            }

            // 3. Setup Events
            btnCancel.Click += (s, e) => Dismiss();
            btnSave.Click += OnSaveClicked;
            btnPickLocation.Click += OnPickLocationClicked;

            // 4. Handle Pre-filling (Logic separated from lifecycle)
            PreFillBusData();
        }

        private void PreFillBusData()
        {
            string title = "הוספת מסלול חדש";
            string buttonText = "שמור מסלול";
            if (Arguments != null && Arguments.ContainsKey("busId"))
            {
                _currentBusId = Arguments.GetString("busId");
                etSchool.Text = Arguments.GetString("school");
                etTown.Text = Arguments.GetString("town");
                etBusLine.Text = Arguments.GetString("busLine");
                if (Arguments.ContainsKey("firstStopLat"))
                {
                    etFirstStopLat.Text = Arguments.GetDouble("firstStopLat").ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
                    etFirstStopLng.Text = Arguments.GetDouble("firstStopLng").ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
                }

                title = "עריכת מסלול קיים";
                buttonText = "עדכן שינויים";
            }
            var tvTitle = View.FindViewById<TextView>(Resource.Id.tvDialogTitle);
            if (tvTitle != null) tvTitle.Text = title;

            btnSave.Text = buttonText;  
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            string school = etSchool.Text.Trim();
            string town = etTown.Text.Trim();
            string line = etBusLine.Text.Trim();

            if (!ValidateInputs(school, town, line)) return;

            btnSave.Enabled = false;
            bool isEditing = !string.IsNullOrEmpty(_currentBusId);

            double? lat = double.TryParse(etFirstStopLat.Text?.Trim(),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double parsedLat) ? parsedLat : (double?)null;
            double? lng = double.TryParse(etFirstStopLng.Text?.Trim(),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double parsedLng) ? parsedLng : (double?)null;

            try
            {
                BusRoute busData = new BusRoute
                {
                    Id = _currentBusId,
                    School = school,
                    Town = town,
                    BusLine = line,
                    FirstStopLat = lat,
                    FirstStopLng = lng,
                };

                // SOLID: The repository handles deciding between Add or Update based on ID
                bool success = string.IsNullOrEmpty(_currentBusId)
                    ? await BusesRepository.AddBusRoute(busData)
                    : await BusesRepository.UpdateBus(busData);

                if (success)
                {
                    string message = isEditing ? "המסלול עודכן בהצלחה" : "המסלול נוסף בהצלחה";
                    Toast.MakeText(Activity, message, ToastLength.Short).Show();

                    Dismiss();
                }
            }
            catch (Exception ex)
            {
                Toast.MakeText(Activity, "שגיאה: " + ex.Message, ToastLength.Long).Show();
                btnSave.Enabled = true;
            }
        }

        private void OnPickLocationClicked(object sender, EventArgs e)
        {
            var intent = new Intent(Activity, typeof(MapPickerActivity));
            intent.PutExtra("town", etTown.Text.Trim());
            if (double.TryParse(etFirstStopLat.Text, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double existingLat) &&
                double.TryParse(etFirstStopLng.Text, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double existingLng))
            {
                intent.PutExtra("existingLat", existingLat);
                intent.PutExtra("existingLng", existingLng);
            }
            StartActivityForResult(intent, MAP_PICKER_REQUEST);
        }

        public override void OnActivityResult(int requestCode, int resultCode, Intent data)
        {
            base.OnActivityResult(requestCode, resultCode, data);
            if (requestCode == MAP_PICKER_REQUEST && resultCode == (int)Result.Ok && data != null)
            {
                etFirstStopLat.Text = data.GetDoubleExtra("lat", 0).ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
                etFirstStopLng.Text = data.GetDoubleExtra("lng", 0).ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        private bool ValidateInputs(string school, string town, string line)
        {
            if (string.IsNullOrEmpty(school) || string.IsNullOrEmpty(town) || string.IsNullOrEmpty(line))
            {
                Toast.MakeText(Activity, "אנא מלא את כל השדות", ToastLength.Short).Show();
                return false;
            }
            return true;
        }

        public override void OnStart()
        {
            base.OnStart();
            Dialog.Window.SetLayout(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
        }

        private class SubstringArrayAdapter : ArrayAdapter<string>
        {
            private readonly List<string> _original;
            private readonly SubstringFilter _filter;

            public SubstringArrayAdapter(Context context, int resource, string[] items)
                : base(context, resource, items.ToList())
            {
                _original = items.ToList();
                _filter = new SubstringFilter(this);
            }

            public override Filter Filter => _filter;

            private class SubstringFilter : Filter
            {
                private readonly SubstringArrayAdapter _adapter;
                public SubstringFilter(SubstringArrayAdapter adapter) => _adapter = adapter;

                protected override FilterResults PerformFiltering(Java.Lang.ICharSequence constraint)
                {
                    var query = constraint?.ToString().ToLower() ?? "";
                    var count = string.IsNullOrEmpty(query)
                        ? _adapter._original.Count
                        : _adapter._original.Count(s => s.ToLower().Contains(query));
                    return new FilterResults { Count = count };
                }

                protected override void PublishResults(Java.Lang.ICharSequence constraint, FilterResults results)
                {
                    var query = constraint?.ToString().ToLower() ?? "";
                    var toShow = string.IsNullOrEmpty(query)
                        ? _adapter._original
                        : _adapter._original.Where(s => s.ToLower().Contains(query)).ToList();
                    _adapter.SetNotifyOnChange(false);
                    _adapter.Clear();
                    foreach (var s in toShow)
                        _adapter.Add(s);
                    _adapter.NotifyDataSetChanged();
                }
            }
        }
    }
}