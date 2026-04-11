using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using AndroidX.RecyclerView.Widget;
using Google.Android.Material.FloatingActionButton;
using System;
using System.Collections.Generic;
using System.Linq;
using Firebase.Firestore; // For QuerySnapshot and DocumentSnapshot
using TimeToSchool.Adapter;
using TimeToSchool.Model;
using TimeToSchool.Service; // Access to FireBaseHelper

namespace TimeToSchool.Fragments
{
    public class AllBusesFragment : AndroidX.Fragment.App.Fragment
    {
        private RecyclerView _recyclerView;
        private BusRViewAdapter _adapter;
        private List<BusRoute> _busList;
        private FloatingActionButton _fabAdd;

        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            View view = inflater.Inflate(Resource.Layout.fragment_all_buses, container, false);

            InitViews(view);
            SetupRecyclerView();

            return view;
        }

        private void InitViews(View view)
        {
            _recyclerView = view.FindViewById<RecyclerView>(Resource.Id.rvAllBuses);
            _fabAdd = view.FindViewById<FloatingActionButton>(Resource.Id.fabAddBusRoute);

            _fabAdd.Click += (sender, e) =>
            {
                CreateBusDialogFragment dialog = new CreateBusDialogFragment();
                // Show the dialog over the current fragment
                dialog.Show(ParentFragmentManager, "CreateBusDialog");
            };
        }

        private void SetupRecyclerView()
        {
            _busList = new List<BusRoute>();

            _recyclerView.SetLayoutManager(new LinearLayoutManager(Context));
            _adapter = new BusRViewAdapter(_busList);
            _recyclerView.SetAdapter(_adapter);

            LoadBusData();
        }

        // Connects to the FireBaseHelper listener
        private void LoadBusData()
        {
            // Subscribe to the event in FireBaseHelper
            FireBaseHelper.FetchBusesListener();
            FireBaseHelper.BusEventListener.getEvent += OnBusesChanged;
        }

        // Logic for handling database updates
        private void OnBusesChanged(object sender, FirestoreEventListener.TaskListenerEventArgs e)
        {
            var snapshot = e.Result as QuerySnapshot;
            if (snapshot != null)
            {
                _busList.Clear();
                foreach (DocumentSnapshot item in snapshot.Documents)
                {
                    // Mapping Firestore document fields to the BusRoute class
                    _busList.Add(new BusRoute
                    {
                        Id = item.Id,
                        School = item.Get("School")?.ToString(),
                        Town = item.Get("Town")?.ToString(),
                        BusLine = item.Get("BusLine")?.ToString()
                    });
                }

                // Updating the list on the UI thread
                Activity?.RunOnUiThread(() => _adapter.NotifyDataSetChanged());
            }
        }

        // Lifecycle management to prevent memory leaks and unnecessary data usage
        public override void OnDestroyView()
        {
            if (FireBaseHelper.BusEventListener != null)
            {
                FireBaseHelper.BusEventListener.getEvent -= OnBusesChanged;
            }
            FireBaseHelper.StopBusesListener();
            base.OnDestroyView();
        }
    }
}