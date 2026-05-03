using Android.Graphics;
using Android.Views;
using Android.Widget;
using AndroidX.RecyclerView.Widget;
using System.Collections.Generic;
using TimeToSchool.Model;

namespace TimeToSchool.Adapter
{
    public class ActiveBusViewAdapter : RecyclerView.Adapter
    {
        private readonly List<ActiveBus> _buses;

        public ActiveBusViewAdapter(List<ActiveBus> buses)
        {
            _buses = buses;
        }

        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            View itemView = LayoutInflater.From(parent.Context)
                .Inflate(Resource.Layout.active_bus_item_card, parent, false);
            return new ActiveBusViewHolder(itemView);
        }

        public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
        {
            var vh = holder as ActiveBusViewHolder;
            var bus = _buses[position];

            vh.TvBusLine.Text = $"קו {bus.BusLine}";
            vh.TvDetails.Text = $"{bus.SchoolName} | {bus.Town}";
            vh.TvDriver.Text = $"נהג: {bus.DriverName}";

            bool isActive = bus.Status == "Active";
            vh.TvStatus.Text = isActive ? "פעיל" : "לא פעיל";
            vh.TvStatus.SetBackgroundColor(isActive
                ? Color.ParseColor("#4CAF50")
                : Color.ParseColor("#9E9E9E"));
        }

        public override int ItemCount => _buses.Count;

        public class ActiveBusViewHolder : RecyclerView.ViewHolder
        {
            public TextView TvBusLine { get; }
            public TextView TvDetails { get; }
            public TextView TvDriver { get; }
            public TextView TvStatus { get; }

            public ActiveBusViewHolder(View itemView) : base(itemView)
            {
                TvBusLine = itemView.FindViewById<TextView>(Resource.Id.tvBusLineName);
                TvDetails = itemView.FindViewById<TextView>(Resource.Id.tvDetails);
                TvDriver = itemView.FindViewById<TextView>(Resource.Id.tvDriver);
                TvStatus = itemView.FindViewById<TextView>(Resource.Id.tvStatusBadge);
            }
        }
    }
}
