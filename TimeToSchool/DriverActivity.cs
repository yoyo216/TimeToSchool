using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Locations;
using Android.OS;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using AndroidX.Core.Content;
using AndroidX.RecyclerView.Widget;
using Google.Android.Material.TextField;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TimeToSchool.Adapter;
using TimeToSchool.BusinessLogic;
using TimeToSchool.Model;
using TimeToSchool.Service;

namespace TimeToSchool
{
    [Activity(Label = "Driver Console", MainLauncher = false)]
    public class DriverActivity : BaseDrawerActivity, ILocationListener
    {
        private const int REQUEST_LOCATION_ID = 1001;

        private RecyclerView _recyclerView;
        private DriverCardAdapter _adapter;
        private ItemTouchHelper _touchHelper;
        private TextView globalStatusText;

        private LocationManager locManager;
        private PreferenceService _prefService;
        private List<DriverCardState> _driverCards = new List<DriverCardState>();
        private List<BusRoute> _allRoutes;
        private bool _isGlobalDriving = false;

        private Button _btnSimulate;
        private bool _simulating;
        private int _simIndex;
        private Handler _simHandler;
        private ActiveBus _simRoute;
        private double _simFirstStopLat;
        private double _simFirstStopLng;
        private bool _simHasFirstStop;
        private readonly (double Lat, double Lng)[] _simWaypoints =
        {
            (32.164200, 34.887500), // start — southern entry to Ramot HaShavim
            (32.164900, 34.888100),
            (32.165600, 34.888700),
            (32.166300, 34.889400),
            (32.167000, 34.890100),
            (32.167484, 34.890852), // user-specified target waypoint
            (32.167900, 34.891500),
            (32.168500, 34.892200),
            (32.169200, 34.892900),
            (32.169900, 34.893600),
            (32.170500, 34.894200),
            (32.171100, 34.894800), // end — northern exit of Ramot HaShavim
        };

        protected override int GetContentLayoutId() => Resource.Layout.driverpage_layout;

        protected override void OnCreateDrawerContent(Bundle savedInstanceState)
        {
            InitViews();
            SetupServices();
            SetupRecyclerView();
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
                RefreshCards();
                return true;
            }
            return base.OnOptionsItemSelected(item);
        }

        private void InitViews()
        {
            _recyclerView    = FindViewById<RecyclerView>(Resource.Id.cardsContainer);
            globalStatusText = FindViewById<TextView>(Resource.Id.globalStatusText);

            _btnSimulate = FindViewById<Button>(Resource.Id.btnSimulateBus);
            if (ProManager.DebugMode)
            {
                _btnSimulate.Visibility = ViewStates.Visible;
                _btnSimulate.Click += async (s, e) => { if (_simulating) StopSimulation(); else await StartSimulation(); };
            }
        }

        private void SetupServices()
        {
            locManager = (LocationManager)GetSystemService(LocationService);
            _prefService = new PreferenceService(this);
        }

        private void SetupRecyclerView()
        {
            _recyclerView.SetLayoutManager(new LinearLayoutManager(this));
            _adapter = new DriverCardAdapter(_driverCards, ToggleTrip, OpenRouteSelectionDialog);
            _recyclerView.SetAdapter(_adapter);

            var callback = new SwipeDeleteCallback(this, OnCardSwiped);
            _touchHelper = new ItemTouchHelper(callback);
            _touchHelper.AttachToRecyclerView(_recyclerView);
        }

        private void OnCardSwiped(int position)
        {
            if (position < 0 || position >= _driverCards.Count) return;
            var state = _driverCards[position];
            if (state.IsDriving)
            {
                Android.Widget.Toast.MakeText(this, "לא ניתן למחוק קו פעיל", ToastLength.Short).Show();
                _adapter.NotifyItemChanged(position);
                return;
            }
            _driverCards.RemoveAt(position);
            _adapter.NotifyItemRemoved(position);
            SaveCardsIfRemembered();
            UpdateGlobalStatus();
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

        protected override void OnBeforeLogout() => StopAllActiveTrips();

        protected override void OnDestroy()
        {
            StopSimulation();
            base.OnDestroy();
        }

        #region Bus Simulation (Debug)

        private async Task StartSimulation()
        {
            var route = await BusesRepository.GetBusRouteById("1Q5DZdPo7s1aqPHymZBo");
            if (route == null)
            {
                Android.Util.Log.Debug(ProManager.TAG, "[SIM] bus route not found");
                return;
            }

            _simHasFirstStop = route.FirstStopLat.HasValue && route.FirstStopLng.HasValue;
            _simFirstStopLat = route.FirstStopLat ?? 0;
            _simFirstStopLng = route.FirstStopLng ?? 0;

            _simRoute = new ActiveBus
            {
                SchoolName = route.School,
                Town       = route.Town,
                BusLine    = route.BusLine,
                DriverId   = ProManager.CurrentUser?.Id ?? "sim",
                DriverName = ProManager.CurrentUser?.FirstName ?? "Simulator",
                Status     = "Active",
                Date       = DateTime.Now.ToString("yyyy-MM-dd"),
                IsVisible  = !_simHasFirstStop,
            };

            _simulating = true;
            _simIndex   = 0;
            _btnSimulate.Text = "Stop Simulation";
            _simHandler = new Handler(Looper.MainLooper);

            AdvanceSimStep();
        }

        private void AdvanceSimStep()
        {
            if (!_simulating) return;

            var wp = _simWaypoints[_simIndex % _simWaypoints.Length];
            _simRoute.Latitude  = wp.Lat;
            _simRoute.Longitude = wp.Lng;
            _simIndex++;

            if (!_simRoute.IsVisible && _simHasFirstStop)
            {
                float[] dist = new float[1];
                Android.Locations.Location.DistanceBetween(wp.Lat, wp.Lng, _simFirstStopLat, _simFirstStopLng, dist);
                if (dist[0] <= 50f)
                    _simRoute.IsVisible = true;
            }

            _ = BusesRepository.UpdateBusLocation(_simRoute);
            Android.Util.Log.Debug(ProManager.TAG, $"[SIM] step {_simIndex}: {wp.Lat},{wp.Lng}");

            _simHandler.PostDelayed(AdvanceSimStep, 8_000);
        }

        private void StopSimulation()
        {
            if (!_simulating) return;
            _simulating = false;
            _simHandler?.RemoveCallbacksAndMessages(null);
            _btnSimulate.Text = "Simulate Bus (DEBUG)";

            if (_simRoute != null)
            {
                _simRoute.Status = "Inactive";
                _ = BusesRepository.UpdateBusLocation(_simRoute);
            }
        }

        #endregion

        private async void LoadInitialData()
        {
            _allRoutes = await BusesRepository.GetBusesCollection();

            var savedCards = _prefService.GetDriverCards();
            if (savedCards != null && savedCards.Count > 0)
            {
                _driverCards.Clear();
                _driverCards.AddRange(savedCards);
            }
            else if (_driverCards.Count == 0)
                _driverCards.Add(new DriverCardState { TripData = new ActiveBus() });

            if (!TripTrackingService.IsRunning)
            {
                foreach (var card in _driverCards)
                    card.IsDriving = false;
                _isGlobalDriving = false;
            }
            else
            {
                _isGlobalDriving = _driverCards.Any(c => c.IsDriving);
            }

            RefreshCards();
        }

        #region UI Rendering

        private void RefreshCards()
        {
            _adapter.IsGlobalDriving = _isGlobalDriving;
            _adapter.NotifyDataSetChanged();
            UpdateGlobalStatus();
        }

        private void UpdateGlobalStatus()
        {
            globalStatusText.Text = _isGlobalDriving ? "נסיעה פעילה" : "מוכן לנסיעה";
        }

        #endregion

        #region Business Logic

        private async void ToggleTrip(DriverCardState state)
        {
            if (state.IsDriving)
            {
                state.IsDriving = false;
                _isGlobalDriving = false;
                StopService(new Intent(this, typeof(TripTrackingService)));

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

                var route = _allRoutes?.FirstOrDefault(r =>
                    r.School  == state.TripData.SchoolName &&
                    r.Town    == state.TripData.Town &&
                    r.BusLine == state.TripData.BusLine);
                state.TripData.IsVisible = !(route?.FirstStopLat.HasValue == true
                                           && route.FirstStopLng.HasValue == true);

                ContextCompat.StartForegroundService(this, BuildTripServiceIntent(state.TripData, route));
                await BusesRepository.UpdateBusLocation(state.TripData);
            }
            RefreshCards();
        }

        private void StopAllActiveTrips()
        {
            var activeDriving = _driverCards.Where(c => c.IsDriving).ToList();
            if (activeDriving.Count == 0) return;

            StopService(new Intent(this, typeof(TripTrackingService)));
            _isGlobalDriving = false;

            foreach (var card in activeDriving)
            {
                card.IsDriving = false;
                card.TripData.Status = "Inactive";
                _ = BusesRepository.UpdateBusLocation(card.TripData);
            }
            SaveCardsIfRemembered();
        }

        private Intent BuildTripServiceIntent(ActiveBus trip, BusRoute route)
        {
            var intent = new Intent(this, typeof(TripTrackingService));
            intent.PutExtra("trip_json", JsonConvert.SerializeObject(trip));
            if (route?.FirstStopLat.HasValue == true && route.FirstStopLng.HasValue == true)
            {
                intent.PutExtra("has_first_stop", true);
                intent.PutExtra("first_stop_lat", route.FirstStopLat.Value);
                intent.PutExtra("first_stop_lng", route.FirstStopLng.Value);
            }
            return intent;
        }

        private void OpenRouteSelectionDialog(DriverCardState state)
        {
            Dialog dialog = new Dialog(this);
            dialog.SetContentView(Resource.Layout.dialog_route_selector);
            dialog.Window.SetLayout(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);

            var autoSchool = dialog.FindViewById<AutoCompleteTextView>(Resource.Id.dialogAutoSchool);
            var autoTown   = dialog.FindViewById<AutoCompleteTextView>(Resource.Id.dialogAutoTown);
            var autoBus    = dialog.FindViewById<AutoCompleteTextView>(Resource.Id.dialogAutoBus);
            var btnSave    = dialog.FindViewById<Button>(Resource.Id.btnSaveRoute);

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
                ShowKeyboard(autoTown);
            };

            autoTown.ItemClick += (s, e) =>
            {
                autoBus.Text = string.Empty;
                SetDialogFieldEnabled(autoBus, true);
                autoBus.Adapter = CreateAdapter(GetBusesForRoute(_allRoutes, autoSchool.Text, autoTown.Text).ToArray());
                autoBus.ShowDropDown();
                ShowKeyboard(autoBus);
            };

            btnSave.Click += (s, e) =>
            {
                var validSchools = GetSchools(_allRoutes);
                var validTowns   = GetTownsForSchool(_allRoutes, autoSchool.Text);
                var validBuses   = GetBusesForRoute(_allRoutes, autoSchool.Text, autoTown.Text);

                if (!validSchools.Contains(autoSchool.Text))
                {
                    Android.Widget.Toast.MakeText(this, "יש לבחור בית ספר מהרשימה", ToastLength.Short).Show();
                    return;
                }
                if (!validTowns.Contains(autoTown.Text))
                {
                    Android.Widget.Toast.MakeText(this, "יש לבחור יישוב מהרשימה", ToastLength.Short).Show();
                    return;
                }
                if (!validBuses.Contains(autoBus.Text))
                {
                    Android.Widget.Toast.MakeText(this, "יש לבחור קו אוטובוס מהרשימה", ToastLength.Short).Show();
                    return;
                }

                state.TripData.SchoolName = autoSchool.Text;
                state.TripData.Town = autoTown.Text;
                state.TripData.BusLine = autoBus.Text;
                SaveCardsIfRemembered();
                RefreshCards();
                dialog.Dismiss();
            };

            dialog.Show();
        }

        #endregion

        #region Location Listener

        public void OnLocationChanged(Location location) { }

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
            new SubstringArrayAdapter(this, Resource.Layout.dropdown_item, data);

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

        private void ShowKeyboard(View view)
        {
            view.RequestFocus();
            view.PostDelayed(() =>
            {
                var imm = (InputMethodManager)GetSystemService(Context.InputMethodService);
                imm.ShowSoftInput(view, ShowFlags.Implicit);
            }, 100);
        }

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

        private class SwipeDeleteCallback : ItemTouchHelper.SimpleCallback
        {
            private readonly Context _ctx;
            private readonly Action<int> _onDelete;
            private readonly Paint _bgPaint = new Paint();

            public SwipeDeleteCallback(Context ctx, Action<int> onDelete)
                : base(0, ItemTouchHelper.Right)
            {
                _ctx = ctx;
                _onDelete = onDelete;
                _bgPaint.Color = Color.ParseColor("#F44336");
            }

            public override bool OnMove(RecyclerView rv, RecyclerView.ViewHolder vh,
                RecyclerView.ViewHolder target) => false;

            public override void OnSwiped(RecyclerView.ViewHolder viewHolder, int direction)
                => _onDelete(viewHolder.LayoutPosition);

            public override void OnChildDraw(Canvas c, RecyclerView recyclerView,
                RecyclerView.ViewHolder viewHolder, float dX, float dY,
                int actionState, bool isCurrentlyActive)
            {
                var iv = viewHolder.ItemView;

                c.DrawRect(iv.Left, iv.Top, iv.Left + dX, iv.Bottom, _bgPaint);

                var icon = ContextCompat.GetDrawable(_ctx, Android.Resource.Drawable.IcMenuDelete);
                if (icon != null)
                {
                    int margin   = (iv.Height - icon.IntrinsicHeight) / 2;
                    int iconTop  = iv.Top + margin;
                    int iconLeft = iv.Left + margin;
                    icon.SetBounds(iconLeft, iconTop,
                                   iconLeft + icon.IntrinsicWidth,
                                   iconTop  + icon.IntrinsicHeight);
                    icon.Draw(c);
                }

                base.OnChildDraw(c, recyclerView, viewHolder, dX, dY, actionState, isCurrentlyActive);
            }
        }
    }

    public class DriverCardState
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string FirebaseDocumentId { get; set; }
        public ActiveBus TripData { get; set; }
        public bool IsDriving { get; set; } = false;
    }
}
