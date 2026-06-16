using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Locations;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using AndroidX.Core.Content;
using AndroidX.Core.Graphics.Drawable;
using AndroidX.RecyclerView.Widget;
using Google.Android.Material.TextField;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TimeToSchool.Adapter;
using TimeToSchool.BusinessLogic;
using TimeToSchool.Helpers;
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
        private TextView _tvGreeting;
        private LinearLayout _chipStatus;
        private Android.Views.View _dotStatus;
        private TextView _tvStatus;
        private LinearLayout _chipVisibility;
        private Android.Views.View _dotVisibility;
        private TextView _tvVisibility;
        private ImageButton _btnToggleVisibility;
        private TextView _tvRouteInfo;

        private LocationManager locManager;
        private PreferenceService _prefService;
        private List<DriverCardState> _driverCards = new List<DriverCardState>();
        private List<BusRoute> _allRoutes;
        private bool _isGlobalDriving = false;

        private Button _btnSimulate;

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
            _tvGreeting      = FindViewById<TextView>(Resource.Id.tvGreeting);
            _chipStatus      = FindViewById<LinearLayout>(Resource.Id.chipStatus);
            _dotStatus       = FindViewById<Android.Views.View>(Resource.Id.dotStatus);
            _tvStatus        = FindViewById<TextView>(Resource.Id.tvStatus);
            _chipVisibility  = FindViewById<LinearLayout>(Resource.Id.chipVisibility);
            _dotVisibility   = FindViewById<Android.Views.View>(Resource.Id.dotVisibility);
            _tvVisibility    = FindViewById<TextView>(Resource.Id.tvVisibility);
            _btnToggleVisibility = FindViewById<ImageButton>(Resource.Id.btnToggleVisibility);
            _tvRouteInfo     = FindViewById<TextView>(Resource.Id.tvNoRoute);

            _btnToggleVisibility.Click += (s, e) => ToggleVisibility();

            UpdateInfoPanel();

            _btnSimulate = FindViewById<Button>(Resource.Id.btnSimulateBus);
            if (ProManager.DebugMode)
            {
                _btnSimulate.Visibility = ViewStates.Visible;
                UpdateSimulateButtonText();
                _btnSimulate.Click += async (s, e) =>
                {
                    if (SimulatedTripService.IsRunning)
                    {
                        StopService(new Intent(this, typeof(SimulatedTripService)));
                        UpdateSimulateButtonText();
                    }
                    else
                    {
                        await StartSimulation();
                    }
                };
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

        protected override void OnResume()
        {
            base.OnResume();
            TripTrackingService.TripAutoStopped += HandleAutoTripStop;
            TripTrackingService.TripStoppedFromNotification += HandleTripStoppedFromNotification;
            TripTrackingService.VisibilityChanged += HandleVisibilityChanged;
            SimulatedTripService.SimulationStoppedFromNotification += HandleSimulationStoppedFromNotification;
            UpdateSimulateButtonText();

            if (!TripTrackingService.IsRunning && _isGlobalDriving)
            {
                foreach (var card in _driverCards.Where(c => c.IsDriving))
                    card.IsDriving = false;
                _isGlobalDriving = false;
                SaveCardsIfRemembered();
                RefreshCards();
            }
        }

        protected override void OnPause()
        {
            TripTrackingService.TripAutoStopped -= HandleAutoTripStop;
            TripTrackingService.TripStoppedFromNotification -= HandleTripStoppedFromNotification;
            TripTrackingService.VisibilityChanged -= HandleVisibilityChanged;
            SimulatedTripService.SimulationStoppedFromNotification -= HandleSimulationStoppedFromNotification;
            base.OnPause();
            SaveCardsIfRemembered();
        }

        // Logging out only stops real driver trips — a running bus simulation is intentionally
        // left untouched so testers can log in as a different user while it keeps reporting.
        protected override void OnBeforeLogout() => StopAllActiveTrips();

        #region Bus Simulation (Debug)

        private async Task StartSimulation()
        {
            var route = await BusesRepository.GetBusRouteById("1Q5DZdPo7s1aqPHymZBo");
            if (route == null)
            {
                Android.Util.Log.Debug(ProManager.TAG, "[SIM] bus route not found");
                return;
            }

            bool hasFirstStop = route.FirstStopLat.HasValue && route.FirstStopLng.HasValue;

            var simRoute = new ActiveBus
            {
                SchoolName = route.School,
                Town       = route.Town,
                BusLine    = route.BusLine,
                DriverId   = ProManager.CurrentUser?.Id ?? "sim",
                DriverName = ProManager.CurrentUser?.FirstName ?? "Simulator",
                Status     = "Active",
                Date       = DateTime.Now.ToString("yyyy-MM-dd"),
                IsVisible  = !hasFirstStop,
            };

            var intent = new Intent(this, typeof(SimulatedTripService));
            intent.PutExtra("trip_json", JsonConvert.SerializeObject(simRoute));
            if (hasFirstStop)
            {
                intent.PutExtra("has_first_stop", true);
                intent.PutExtra("first_stop_lat", route.FirstStopLat.Value);
                intent.PutExtra("first_stop_lng", route.FirstStopLng.Value);
            }

            ContextCompat.StartForegroundService(this, intent);
            UpdateSimulateButtonText();
        }

        private void UpdateSimulateButtonText()
        {
            if (_btnSimulate == null) return;
            _btnSimulate.Text = SimulatedTripService.IsRunning ? "Stop Simulation" : "Simulate Bus (DEBUG)";
        }

        private void HandleSimulationStoppedFromNotification() => UpdateSimulateButtonText();

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
            UpdateInfoPanel();
        }

        private void UpdateGlobalStatus()
        {
            globalStatusText.Text = _isGlobalDriving ? "נסיעה פעילה" : "מוכן לנסיעה";
        }

        private void UpdateInfoPanel()
        {
            var name = ProManager.CurrentUser?.FirstName;
            _tvGreeting.Text = string.IsNullOrEmpty(name)
                ? GetTimeGreeting()
                : $"{GetTimeGreeting()}, {name}";

            if (_isGlobalDriving)
            {
                SetChip(_chipStatus, _dotStatus, _tvStatus, "#22C55E", "#16A34A", "#FFFFFF", "נוסע");

                var drivingCard = _driverCards?.FirstOrDefault(c => c.IsDriving);
                if (drivingCard != null)
                {
                    var t = drivingCard.TripData;
                    _tvRouteInfo.Text = $"{t.SchoolName} · {t.Town} · קו {t.BusLine}";
                    _tvRouteInfo.SetTextColor(Color.ParseColor("#1E293B"));

                    _btnToggleVisibility.Visibility = ViewStates.Visible;
                    if (t.IsVisible)
                    {
                        SetChip(_chipVisibility, _dotVisibility, _tvVisibility, "#A855F7", "#7C3AED", "#FFFFFF", "גלוי לציבור");
                        _btnToggleVisibility.SetImageResource(Resource.Drawable.ic_eye);
                        _btnToggleVisibility.SetColorFilter(Color.ParseColor("#FFFFFF"));
                    }
                    else
                    {
                        SetChip(_chipVisibility, _dotVisibility, _tvVisibility, "#334155", "#94A3B8", "#CBD5E1", "מוסתר");
                        _btnToggleVisibility.SetImageResource(Resource.Drawable.ic_eye_off);
                        _btnToggleVisibility.SetColorFilter(Color.ParseColor("#CBD5E1"));
                    }
                }
            }
            else
            {
                SetChip(_chipStatus,     _dotStatus,     _tvStatus,     "#334155", "#94A3B8", "#CBD5E1", "המתנה");
                SetChip(_chipVisibility, _dotVisibility, _tvVisibility, "#334155", "#94A3B8", "#CBD5E1", "מוסתר");
                _tvRouteInfo.Text = "לא נבחר מסלול";
                _tvRouteInfo.SetTextColor(Color.ParseColor("#94A3B8"));
                _btnToggleVisibility.Visibility = ViewStates.Gone;
            }
        }

        private void SetChip(LinearLayout chip, Android.Views.View dot, TextView label,
                              string chipHex, string dotHex, string textHex, string text)
        {
            var chipBg = ContextCompat.GetDrawable(this, Resource.Drawable.bg_chip).Mutate();
            DrawableCompat.SetTint(chipBg, Color.ParseColor(chipHex));
            chip.Background = chipBg;

            var dotBg = ContextCompat.GetDrawable(this, Resource.Drawable.shape_dot).Mutate();
            DrawableCompat.SetTint(dotBg, Color.ParseColor(dotHex));
            dot.Background = dotBg;

            label.Text = text;
            label.SetTextColor(Color.ParseColor(textHex));
        }

        private static string GetTimeGreeting()
        {
            int h = DateTime.Now.Hour;
            if (h >= 5 && h < 12)  return "בוקר טוב";
            if (h >= 12 && h < 17) return "צהריים טובים";
            if (h >= 17 && h < 21) return "ערב טוב";
            return "לילה טוב";
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
                RefreshCards();
                return;
            }

            if (string.IsNullOrEmpty(state.TripData.BusLine))
            {
                Android.Widget.Toast.MakeText(this, "אנא הגדר מסלול תחילה", ToastLength.Short).Show();
                return;
            }

            var route = _allRoutes?.FirstOrDefault(r =>
                r.School  == state.TripData.SchoolName &&
                r.Town    == state.TripData.Town &&
                r.BusLine == state.TripData.BusLine);
            bool willBeVisibleImmediately = !(route?.FirstStopLat.HasValue == true
                                            && route.FirstStopLng.HasValue == true);

            if (willBeVisibleImmediately && !state.SuppressVisibilityWarning)
            {
                var checkbox = new CheckBox(this) { Text = "אל תציג הודעה זו שוב למסלול זה" };
                int pad = (int)(16 * Resources.DisplayMetrics.Density);
                checkbox.SetPadding(pad, pad / 2, pad, pad / 2);

                new AlertDialog.Builder(this)
                    .SetTitle("נראות לציבור")
                    .SetMessage("למסלול זה לא הוגדרה תחנה ראשונה, כך שתהיה גלוי לציבור באופן מיידי לאחר האישור. להתחיל בנסיעה?")
                    .SetView(checkbox)
                    .SetPositiveButton("כן, התחל", (s, e) =>
                    {
                        if (checkbox.Checked)
                        {
                            state.SuppressVisibilityWarning = true;
                            SaveCardsIfRemembered();
                        }
                        StartTrip(state, route);
                    })
                    .SetNegativeButton("ביטול", (s, e) => { })
                    .Show();
                return;
            }

            StartTrip(state, route);
        }

        private void StartTrip(DriverCardState state, BusRoute route)
        {
            state.IsDriving = true;
            _isGlobalDriving = true;

            state.TripData.Status = "Active";
            state.TripData.DriverName = ProManager.CurrentUser.FirstName;
            state.TripData.DriverId = ProManager.CurrentUser.Id;
            state.TripData.Date = DateTime.Now.ToString("yyyy-MM-dd");
            state.TripData.IsVisible = !(route?.FirstStopLat.HasValue == true
                                       && route.FirstStopLng.HasValue == true);

            ContextCompat.StartForegroundService(this, BuildTripServiceIntent(state.TripData, route));
            RefreshCards();
        }

        private void ToggleVisibility()
        {
            var drivingCard = _driverCards.FirstOrDefault(c => c.IsDriving);
            if (drivingCard == null) return;

            bool newVisibility = !drivingCard.TripData.IsVisible;
            drivingCard.TripData.IsVisible = newVisibility;
            SaveCardsIfRemembered();
            UpdateInfoPanel();

            if (TripTrackingService.IsRunning)
            {
                var intent = new Intent(this, typeof(TripTrackingService));
                intent.SetAction(TripTrackingService.ActionToggleVisibility);
                intent.PutExtra("is_visible", newVisibility);
                StartService(intent);
            }
            else
            {
                _ = BusesRepository.UpdateBusLocation(drivingCard.TripData);
            }
        }

        private void HandleVisibilityChanged(bool isVisible)
        {
            var drivingCard = _driverCards.FirstOrDefault(c => c.IsDriving);
            if (drivingCard == null) return;
            drivingCard.TripData.IsVisible = isVisible;
            SaveCardsIfRemembered();
            UpdateInfoPanel();
        }

        private void HandleAutoTripStop()
        {
            foreach (var card in _driverCards.Where(c => c.IsDriving))
                card.IsDriving = false;
            _isGlobalDriving = false;
            SaveCardsIfRemembered();
            RefreshCards();
            Android.Widget.Toast.MakeText(this, "הנסיעה הסתיימה — הגעת ליעד", ToastLength.Long).Show();
        }

        private void HandleTripStoppedFromNotification()
        {
            foreach (var card in _driverCards.Where(c => c.IsDriving))
                card.IsDriving = false;
            _isGlobalDriving = false;
            SaveCardsIfRemembered();
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
            dialog.Window.SetSoftInputMode(SoftInput.StateHidden | SoftInput.AdjustResize);

            var autoSchool = dialog.FindViewById<AutoCompleteTextView>(Resource.Id.dialogAutoSchool);
            var autoTown   = dialog.FindViewById<AutoCompleteTextView>(Resource.Id.dialogAutoTown);
            var autoBus    = dialog.FindViewById<AutoCompleteTextView>(Resource.Id.dialogAutoBus);
            var btnSave    = dialog.FindViewById<Button>(Resource.Id.btnSaveRoute);

            UIHelper.ConfigureSearchableField(autoSchool);
            UIHelper.ConfigureSearchableField(autoTown);
            UIHelper.ConfigureSearchableField(autoBus);

            autoSchool.Adapter = CreateAdapter(BusesRepository.GetSchools(_allRoutes).ToArray());
            UIHelper.SetFieldEnabled(autoTown, false);
            UIHelper.SetFieldEnabled(autoBus, false);

            autoSchool.ItemClick += (s, e) =>
            {
                string selectedSchool = autoSchool.Text;
                autoTown.Text = string.Empty;
                autoBus.Text = string.Empty;
                UIHelper.SetFieldEnabled(autoTown, true);
                UIHelper.SetFieldEnabled(autoBus, false);
                autoTown.Adapter = CreateAdapter(BusesRepository.GetTownsForSchool(_allRoutes, selectedSchool).ToArray());
                ShowKeyboard(autoTown);
                autoTown.PostDelayed(() => {
                    if (autoTown.HasFocus && !autoTown.IsPopupShowing)
                        autoTown.ShowDropDown();
                }, 350);
            };

            autoTown.ItemClick += (s, e) =>
            {
                autoBus.Text = string.Empty;
                UIHelper.SetFieldEnabled(autoBus, true);
                autoBus.Adapter = CreateAdapter(BusesRepository.GetBusesForRoute(_allRoutes, autoSchool.Text, autoTown.Text, includeAnyOption: false).ToArray());
                ShowKeyboard(autoBus);
                autoBus.PostDelayed(() => {
                    if (autoBus.HasFocus && !autoBus.IsPopupShowing)
                        autoBus.ShowDropDown();
                }, 350);
            };

            autoBus.ItemClick += (s, e) => UIHelper.HideKeyboard(autoBus);

            btnSave.Click += (s, e) =>
            {
                UIHelper.HideKeyboard(btnSave);
                var validSchools = BusesRepository.GetSchools(_allRoutes);
                var validTowns   = BusesRepository.GetTownsForSchool(_allRoutes, autoSchool.Text);
                var validBuses   = BusesRepository.GetBusesForRoute(_allRoutes, autoSchool.Text, autoTown.Text, includeAnyOption: false);

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

                bool routeChanged = state.TripData.SchoolName != autoSchool.Text
                                 || state.TripData.Town != autoTown.Text
                                 || state.TripData.BusLine != autoBus.Text;

                state.TripData.SchoolName = autoSchool.Text;
                state.TripData.Town = autoTown.Text;
                state.TripData.BusLine = autoBus.Text;
                if (routeChanged)
                    state.SuppressVisibilityWarning = false;
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

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Permission[] grantResults)
        {
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            if (requestCode == REQUEST_LOCATION_ID && (grantResults.Length == 0 || grantResults[0] != Permission.Granted))
            {
                Android.Widget.Toast.MakeText(this,
                    "ללא הרשאת מיקום, מיקום האוטובוס לא ישותף עם הציבור בזמן הנסיעה",
                    ToastLength.Long).Show();
            }
        }

        private ArrayAdapter<string> CreateAdapter(string[] data) =>
            new SubstringArrayAdapter(this, Resource.Layout.dropdown_item, data);

        private void ShowKeyboard(View view)
        {
            view.RequestFocus();
            view.PostDelayed(() =>
            {
                var imm = (InputMethodManager)GetSystemService(Context.InputMethodService);
                imm.ShowSoftInput(view, ShowFlags.Implicit);
            }, 100);
        }

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
                _bgPaint.Color = Color.ParseColor("#EF4444");
                _bgPaint.AntiAlias = true;
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
                float radius = 12f * _ctx.Resources.DisplayMetrics.Density;

                // Draw rounded rect over the full card bounds — the translated card covers
                // the right portion, leaving a rounded red strip that matches the card shape.
                var bg = new Android.Graphics.RectF(iv.Left, iv.Top, iv.Right, iv.Bottom);
                c.DrawRoundRect(bg, radius, radius, _bgPaint);

                var icon = ContextCompat.GetDrawable(_ctx, Resource.Drawable.ic_trash_white);
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
}
