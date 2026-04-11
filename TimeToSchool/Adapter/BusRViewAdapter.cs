using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using AndroidX.RecyclerView.Widget;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TimeToSchool.Model;

namespace TimeToSchool.Adapter
{
    // Manages the display of registered bus routes in a list
    public class BusRViewAdapter : RecyclerView.Adapter
    {
        private List<BusRoute> _buses;

        public BusRViewAdapter(List<BusRoute> buses)
        {
            _buses = buses;
        }

        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            // Inflates the card layout for an individual bus entry
            View itemView = LayoutInflater.From(parent.Context).Inflate(Resource.Layout.bus_item_card, parent, false);
            return new BusViewHolder(itemView);
        }

        public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
        {
            var vh = holder as BusViewHolder;
            var bus = _buses[position];

            // Mapping the BusRoute properties to the UI
            vh.TvBusLine.Text = $"Line {bus.BusLine}";
            vh.TvSchool.Text = bus.School; // Matches your class property
            vh.TvTown.Text = bus.Town;     // Matches your class property
        }

        public override int ItemCount => _buses.Count;

        public class BusViewHolder : RecyclerView.ViewHolder
        {
            public TextView TvBusLine { get; }
            public TextView TvSchool { get; }
            public TextView TvTown { get; }

            public BusViewHolder(View itemView) : base(itemView)
            {
                TvBusLine = itemView.FindViewById<TextView>(Resource.Id.tvBusLineName);
                TvSchool = itemView.FindViewById<TextView>(Resource.Id.tvSchoolName);
                TvTown = itemView.FindViewById<TextView>(Resource.Id.tvTownName);
            }
        }
    }
}