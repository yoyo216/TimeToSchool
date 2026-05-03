using Android.OS;
using Android.Views;
using Android.Widget;
using AndroidX.RecyclerView.Widget;
using Firebase.Firestore;
using Google.Android.Material.TextField;
using System.Collections.Generic;
using System.Linq;
using TimeToSchool.Adapter;
using TimeToSchool.Model;
using TimeToSchool.Service;
using static TimeToSchool.Service.FireBaseHelper;

namespace TimeToSchool.Fragments
{
    public class ActiveBusesFragment : AndroidX.Fragment.App.Fragment
    {
        private RecyclerView _recyclerView;
        private TextView _emptyView;
        private TextInputEditText _etSearch;
        private ActiveBusViewAdapter _adapter;
        private List<ActiveBus> _allBuses;
        private List<ActiveBus> _displayedBuses;

        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            View view = inflater.Inflate(Resource.Layout.fragment_active_buses, container, false);
            InitViews(view);
            SetupRecyclerView();
            LoadActiveBusData();
            return view;
        }

        private void InitViews(View view)
        {
            _recyclerView = view.FindViewById<RecyclerView>(Resource.Id.rvActiveBuses);
            _emptyView = view.FindViewById<TextView>(Resource.Id.tvActiveBusesEmpty);
            _etSearch = view.FindViewById<TextInputEditText>(Resource.Id.etActiveBusesSearch);
            _etSearch.TextChanged += (s, e) => ApplyFilter();
        }

        private void SetupRecyclerView()
        {
            _allBuses = new List<ActiveBus>();
            _displayedBuses = new List<ActiveBus>();
            _recyclerView.SetLayoutManager(new LinearLayoutManager(Context));
            _adapter = new ActiveBusViewAdapter(_displayedBuses);
            _recyclerView.SetAdapter(_adapter);
        }

        private void LoadActiveBusData()
        {
            BusesRepository.FetchActiveBusesListenerForToday();
            BusesRepository.ActiveTripsEventListener.getEvent += OnActiveBusesChanged;
        }

        private void OnActiveBusesChanged(object sender, FirestoreEventListener.TaskListenerEventArgs e)
        {
            var snapshot = e.Result as QuerySnapshot;
            if (snapshot == null) return;

            _allBuses.Clear();
            foreach (DocumentSnapshot item in snapshot.Documents)
            {
                _allBuses.Add(new ActiveBus
                {
                    SchoolName = item.Get("schoolName")?.ToString(),
                    Town = item.Get("town")?.ToString(),
                    BusLine = item.Get("busLine")?.ToString(),
                    DriverName = item.Get("driverName")?.ToString(),
                    DriverId = item.Get("driverId")?.ToString(),
                    Status = item.Get("status")?.ToString(),
                    Date = item.Get("date")?.ToString()
                });
            }

            Activity?.RunOnUiThread(ApplyFilter);
        }

        private void ApplyFilter()
        {
            if (_allBuses == null || _displayedBuses == null) return;

            string query = _etSearch?.Text?.Trim().ToLower() ?? string.Empty;

            IEnumerable<ActiveBus> filtered = _allBuses;
            if (!string.IsNullOrEmpty(query))
            {
                filtered = filtered.Where(b =>
                    (b.Town?.ToLower().Contains(query) == true) ||
                    (b.SchoolName?.ToLower().Contains(query) == true) ||
                    (b.DriverName?.ToLower().Contains(query) == true));
            }

            var sorted = filtered
                .OrderByDescending(b => b.Status == "Active")
                .ThenBy(b => b.BusLine)
                .ToList();

            _displayedBuses.Clear();
            _displayedBuses.AddRange(sorted);
            _adapter.NotifyDataSetChanged();
            _emptyView.Visibility = _displayedBuses.Count == 0 ? ViewStates.Visible : ViewStates.Gone;
        }

        public override void OnDestroyView()
        {
            if (BusesRepository.ActiveTripsEventListener != null)
            {
                BusesRepository.ActiveTripsEventListener.getEvent -= OnActiveBusesChanged;
            }
            BusesRepository.StopActiveBusesListener();
            base.OnDestroyView();
        }
    }
}
