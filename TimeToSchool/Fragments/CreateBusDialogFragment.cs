using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TimeToSchool.Model;
using TimeToSchool.Service;

namespace TimeToSchool.Fragments
{
    public class CreateBusDialogFragment : AndroidX.Fragment.App.DialogFragment
    {
        EditText etSchool, etTown, etBusLine;
        Button btnSave, btnCancel;

        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            // Set the dialog to have no title bar for a cleaner look
            Dialog.Window.RequestFeature(WindowFeatures.NoTitle);

            View view = inflater.Inflate(Resource.Layout.createbusline_layout, container, false);

            etSchool = view.FindViewById<EditText>(Resource.Id.etSchool);
            etTown = view.FindViewById<EditText>(Resource.Id.etTown);
            etBusLine = view.FindViewById<EditText>(Resource.Id.etBusLine);
            btnSave = view.FindViewById<Button>(Resource.Id.btnSaveBus);
            btnCancel = view.FindViewById<Button>(Resource.Id.btnCancel);

            btnCancel.Click += (s, e) => Dismiss();
            btnSave.Click += OnSaveClicked;

            return view;
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            string school = etSchool.Text.Trim();
            string town = etTown.Text.Trim();
            string line = etBusLine.Text.Trim();

            if (string.IsNullOrEmpty(school) || string.IsNullOrEmpty(town) || string.IsNullOrEmpty(line))
            {
                Toast.MakeText(Activity, "Please fill all fields", ToastLength.Short).Show();
                return;
            }

            // Disable button so user doesn't click twice
            btnSave.Enabled = false;

            try
            {
                BusRoute newRoute = new BusRoute
                {
                    School = school,
                    Town = town,
                    BusLine = line
                };

                await FireBaseHelper.AddBusRoute(newRoute);

                Toast.MakeText(Activity, "Bus route added!", ToastLength.Short).Show();
                Dismiss(); // Close the dialog
            }
            catch (Exception ex)
            {
                Toast.MakeText(Activity, "Error: " + ex.Message, ToastLength.Long).Show();
                btnSave.Enabled = true;
            }
        }

        public override void OnStart()
        {
            base.OnStart();
            // Make the dialog take up most of the screen width
            Dialog.Window.SetLayout(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
        }
    }
}