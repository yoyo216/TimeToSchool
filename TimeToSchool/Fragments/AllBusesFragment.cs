using Android.App;
using Android.Content;
using Android.Media; // Access to FireBaseHelper
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using AndroidX.RecyclerView.Widget;
using Firebase.Firestore; // For QuerySnapshot and DocumentSnapshot
using Firebase.Firestore.Auth;
using Google.Android.Material.FloatingActionButton;
using System;
using System.Collections.Generic;
using System.Linq;
using TimeToSchool.Adapter;
using TimeToSchool.Model;
using TimeToSchool.Service;
using static AndroidX.RecyclerView.Widget.RecyclerView;
using static TimeToSchool.Service.FireBaseHelper;

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
                dialog.Show(ParentFragmentManager, "CreateBusDialog");
            };
        }

        private void SetupRecyclerView()
        {
            _busList = new List<BusRoute>();
            _recyclerView.SetLayoutManager(new LinearLayoutManager(Context));

            // Initialize adapter
            _adapter = new BusRViewAdapter(_busList);

            // --- SOLID LINKING ---
            // We connect the Adapter's "Actions" to our Fragment's "Methods"
            _adapter.OnDeleteRequest = OnDeleteBusClick;
            _adapter.OnEditRequest = OnEditBusClick;

            _recyclerView.SetAdapter(_adapter);

            LoadBusData();
        }

        // Combined Method: Handles UI and triggers Data logic
        private void OnDeleteBusClick(BusRoute bus)
        {
            var builder = new AlertDialog.Builder(Activity);
            builder.SetTitle("מחיקת מסלול");
            builder.SetMessage($"האם אתה בטוח שברצונך למחוק את {bus.BusLine} של {bus.School}?");

            builder.SetPositiveButton("מחק", async (s, e) =>
            {
                bool success = await BusesRepository.DeleteBus(bus.Id);
                if (success)
                {
                    Toast.MakeText(Context, "המסלול נמחק בהצלחה", ToastLength.Short).Show();
                    // No need to manually refresh the list because LoadBusData() 
                    // has a listener that will trigger OnBusesChanged automatically!
                }
            });

            builder.SetNegativeButton("ביטול", (s, e) => { /* Do nothing */ });
            builder.Show();
        }

        private void OnEditBusClick(BusRoute bus)
        {
            // SOLID: Use the static factory method to create the fragment with its arguments
            var editDialog = CreateBusDialogFragment.NewInstance(bus);

            // Show the dialog
            editDialog.Show(ParentFragmentManager, "EditBusDialog");
        }

        private void LoadBusData()
        {
            BusesRepository.FetchBusesListener();
            BusesRepository.BusEventListener.getEvent += OnBusesChanged;
        }

        private void OnBusesChanged(object sender, FirestoreEventListener.TaskListenerEventArgs e)
        {
            var snapshot = e.Result as QuerySnapshot;
            if (snapshot != null)
            {
                _busList.Clear();
                foreach (DocumentSnapshot item in snapshot.Documents)
                {
                    _busList.Add(new BusRoute
                    {
                        Id = item.Id,
                        School = item.Get("School")?.ToString(),
                        Town = item.Get("Town")?.ToString(),
                        BusLine = item.Get("BusLine")?.ToString()
                    });
                }

                Activity?.RunOnUiThread(() => _adapter.NotifyDataSetChanged());
            }
        }

        public override void OnDestroyView()
        {
            if (BusesRepository.BusEventListener != null)
            {
                BusesRepository.BusEventListener.getEvent -= OnBusesChanged;
            }
            BusesRepository.StopBusesListener();
            base.OnDestroyView();
        }
    }
}