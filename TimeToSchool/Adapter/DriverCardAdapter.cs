using Android.Views;
using Android.Widget;
using AndroidX.RecyclerView.Widget;
using System;
using System.Collections.Generic;
using TimeToSchool.Model;

namespace TimeToSchool.Adapter
{
    public class DriverCardAdapter : RecyclerView.Adapter
    {
        private readonly List<DriverCardState> _cards;
        private readonly Action<DriverCardState> _onToggleTrip;
        private readonly Action<DriverCardState> _onOpenSettings;
        public bool IsGlobalDriving { get; set; }

        public DriverCardAdapter(List<DriverCardState> cards,
            Action<DriverCardState> onToggleTrip,
            Action<DriverCardState> onOpenSettings)
        {
            _cards = cards;
            _onToggleTrip = onToggleTrip;
            _onOpenSettings = onOpenSettings;
        }

        public override int ItemCount => _cards.Count;

        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            var view = LayoutInflater.From(parent.Context)
                           .Inflate(Resource.Layout.driver_route_item, parent, false);
            return new CardViewHolder(view, _onToggleTrip, _onOpenSettings);
        }

        public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
        {
            var vh = (CardViewHolder)holder;
            var state = _cards[position];

            vh.CurrentState = state;

            vh.TvSchoolName.Text = !string.IsNullOrEmpty(state.TripData.SchoolName)
                ? $"{state.TripData.SchoolName} - {state.TripData.Town} - קו {state.TripData.BusLine}"
                : "לחץ על ההגדרות לבחירת מסלול";

            if (state.IsDriving)
            {
                vh.BtnAction.SetBackgroundColor(Android.Graphics.Color.Red);
                vh.TvStatus.Text = "סיום נסיעה";
                vh.BtnSettings.Visibility = ViewStates.Gone;
                vh.BtnAction.Enabled = true;
                vh.BtnAction.Alpha = 1.0f;
            }
            else if (IsGlobalDriving)
            {
                vh.BtnAction.SetBackgroundColor(Android.Graphics.Color.Gray);
                vh.BtnAction.Enabled = false;
                vh.BtnAction.Alpha = 0.5f;
                vh.TvStatus.Text = "ממתין...";
                vh.BtnSettings.Enabled = false;
                vh.BtnSettings.Visibility = ViewStates.Visible;
            }
            else
            {
                vh.BtnAction.SetBackgroundColor(Android.Graphics.Color.ParseColor("#4CAF50"));
                vh.TvStatus.Text = "התחל נסיעה";
                vh.BtnAction.Enabled = true;
                vh.BtnAction.Alpha = 1.0f;
                vh.BtnSettings.Visibility = ViewStates.Visible;
                vh.BtnSettings.Enabled = true;
            }
        }

        public class CardViewHolder : RecyclerView.ViewHolder
        {
            public DriverCardState CurrentState { get; set; }
            public TextView TvSchoolName { get; }
            public TextView TvStatus { get; }
            public LinearLayout BtnAction { get; }
            public ImageButton BtnSettings { get; }

            public CardViewHolder(View view,
                Action<DriverCardState> onToggleTrip,
                Action<DriverCardState> onOpenSettings) : base(view)
            {
                TvSchoolName = view.FindViewById<TextView>(Resource.Id.tvSchoolName);
                TvStatus     = view.FindViewById<TextView>(Resource.Id.tvStatusLabel);
                BtnAction    = view.FindViewById<LinearLayout>(Resource.Id.btnTripAction);
                BtnSettings  = view.FindViewById<ImageButton>(Resource.Id.btnSettings);

                BtnAction.Click   += (s, e) => { if (CurrentState != null) onToggleTrip(CurrentState); };
                BtnSettings.Click += (s, e) => { if (CurrentState != null) onOpenSettings(CurrentState); };
            }
        }
    }
}
