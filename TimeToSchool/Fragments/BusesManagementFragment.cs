using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using AndroidX.RecyclerView.Widget;
using Firebase.Firestore;
using System;
using System.Collections.Generic;
using TimeToSchool.Adapter;
using TimeToSchool.BusinessLogic;
using TimeToSchool.Model;
using TimeToSchool.Service;
using AndroidX.Fragment.App;
using Google.Android.Material.FloatingActionButton;
namespace TimeToSchool.Fragments
{
    public class BusesManagementFragment : AndroidX.Fragment.App.Fragment
    {
        FloatingActionButton fabAddBus;

        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            // Inflate the layout for this fragment
            View view = inflater.Inflate(Resource.Layout.fragment_buses_management, container, false);

            // Initialize the FAB
            fabAddBus = view.FindViewById<FloatingActionButton>(Resource.Id.fabAddBus);

            // Click event to open your DialogFragment
            fabAddBus.Click += (s, e) =>
            {
                CreateBusDialogFragment dialog = new CreateBusDialogFragment();
                // Show the dialog over the current fragment
                dialog.Show(ParentFragmentManager, "CreateBusDialog");
            };

            return view;
        }
    }

}