using Android.Graphics;
using Android.Views;
using AndroidX.Core.Content;
using AndroidX.Core.Graphics.Drawable;
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

            vh.TvSchoolName.Text = !string.IsNullOrEmpty(state.TripData?.SchoolName)
                ? state.TripData.SchoolName
                : "לחץ על ההגדרות לבחירת מסלול";
            vh.TvCity.Text    = state.TripData?.Town ?? "";
            vh.TvBusLine.Text = !string.IsNullOrEmpty(state.TripData?.BusLine)
                ? $"קו {state.TripData.BusLine}"
                : "";

            if (state.IsDriving)
            {
                vh.ContentLayout.SetBackgroundResource(Resource.Drawable.bg_route_info_box);
                SetTextWhite(vh);
                SetIconTint(vh, Color.White);
                vh.ChipActive.Visibility  = ViewStates.Visible;
                vh.BtnSettings.Visibility = ViewStates.Gone;
                vh.ItemView.Clickable = true;
                vh.ItemView.Alpha = 1f;
            }
            else if (IsGlobalDriving)
            {
                vh.ContentLayout.Background = null;
                SetTextNormal(vh);
                SetIconTintNormal(vh);
                vh.ChipActive.Visibility  = ViewStates.Gone;
                vh.BtnSettings.Visibility = ViewStates.Visible;
                vh.BtnSettings.Enabled    = false;
                vh.ItemView.Clickable = false;
                vh.ItemView.Alpha = 0.5f;
            }
            else
            {
                vh.ContentLayout.Background = null;
                SetTextNormal(vh);
                SetIconTintNormal(vh);
                vh.ChipActive.Visibility  = ViewStates.Gone;
                vh.BtnSettings.Visibility = ViewStates.Visible;
                vh.BtnSettings.Enabled    = true;
                vh.ItemView.Clickable = true;
                vh.ItemView.Alpha = 1f;
            }
        }

        private static void SetTextWhite(CardViewHolder vh)
        {
            vh.TvSchoolName.SetTextColor(Color.White);
            vh.TvCity.SetTextColor(Color.White);
            vh.TvBusLine.SetTextColor(Color.White);
        }

        private static void SetTextNormal(CardViewHolder vh)
        {
            vh.TvSchoolName.SetTextColor(Color.ParseColor("#1E293B"));
            vh.TvCity.SetTextColor(Color.ParseColor("#64748B"));
            vh.TvBusLine.SetTextColor(Color.ParseColor("#94A3B8"));
        }

        private static void SetIconTint(CardViewHolder vh, Color color)
        {
            vh.IvSchool.SetColorFilter(color, Android.Graphics.PorterDuff.Mode.SrcIn);
            vh.IvCity.SetColorFilter(color, Android.Graphics.PorterDuff.Mode.SrcIn);
            vh.IvBus.SetColorFilter(color, Android.Graphics.PorterDuff.Mode.SrcIn);
        }

        private static void SetIconTintNormal(CardViewHolder vh)
        {
            vh.IvSchool.SetColorFilter(Color.ParseColor("#1E293B"), Android.Graphics.PorterDuff.Mode.SrcIn);
            vh.IvCity.SetColorFilter(Color.ParseColor("#64748B"), Android.Graphics.PorterDuff.Mode.SrcIn);
            vh.IvBus.SetColorFilter(Color.ParseColor("#94A3B8"), Android.Graphics.PorterDuff.Mode.SrcIn);
        }

        public class CardViewHolder : RecyclerView.ViewHolder
        {
            public DriverCardState CurrentState { get; set; }
            public LinearLayout ContentLayout { get; }
            public TextView     TvSchoolName  { get; }
            public TextView     TvCity        { get; }
            public TextView     TvBusLine     { get; }
            public ImageView    IvSchool      { get; }
            public ImageView    IvCity        { get; }
            public ImageView    IvBus         { get; }
            public TextView     ChipActive    { get; }
            public ImageButton  BtnSettings   { get; }

            public CardViewHolder(View view,
                Action<DriverCardState> onToggleTrip,
                Action<DriverCardState> onOpenSettings) : base(view)
            {
                ContentLayout = view.FindViewById<LinearLayout>(Resource.Id.cardContent);
                TvSchoolName  = view.FindViewById<TextView>(Resource.Id.tvSchoolName);
                TvCity        = view.FindViewById<TextView>(Resource.Id.tvCity);
                TvBusLine     = view.FindViewById<TextView>(Resource.Id.tvBusLine);
                IvSchool      = view.FindViewById<ImageView>(Resource.Id.ivSchool);
                IvCity        = view.FindViewById<ImageView>(Resource.Id.ivCity);
                IvBus         = view.FindViewById<ImageView>(Resource.Id.ivBus);
                ChipActive    = view.FindViewById<TextView>(Resource.Id.chipActive);
                BtnSettings   = view.FindViewById<ImageButton>(Resource.Id.btnSettings);

                var chipBg = ContextCompat.GetDrawable(view.Context, Resource.Drawable.bg_chip).Mutate();
                DrawableCompat.SetTint(chipBg, Color.ParseColor("#22C55E"));
                ChipActive.Background = chipBg;

                view.Click        += (s, e) => { if (CurrentState != null) onToggleTrip(CurrentState); };
                BtnSettings.Click += (s, e) => { if (CurrentState != null) onOpenSettings(CurrentState); };
            }
        }
    }
}
