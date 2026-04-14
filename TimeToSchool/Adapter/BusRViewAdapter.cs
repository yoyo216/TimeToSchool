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
    public class BusRViewAdapter : RecyclerView.Adapter
    {
        private List<BusRoute> _buses;

        // Actions to communicate with the Fragment
        public Action<BusRoute> OnEditRequest;
        public Action<BusRoute> OnDeleteRequest;

        public BusRViewAdapter(List<BusRoute> buses)
        {
            _buses = buses;
        }

        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            View itemView = LayoutInflater.From(parent.Context).Inflate(Resource.Layout.bus_item_card, parent, false);

            // Pass the actions to the ViewHolder constructor
            return new BusViewHolder(itemView,
                (pos) => OnEditRequest?.Invoke(_buses[pos]),
                (pos) => OnDeleteRequest?.Invoke(_buses[pos]));
        }

        public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
        {
            var vh = holder as BusViewHolder;
            var bus = _buses[position];

            // Single Responsibility: Only binding data
            vh.TvBusLine.Text = $"קו {bus.BusLine}";
            vh.TvDetails.Text = $"{bus.School} | {bus.Town}";
        }

        public override int ItemCount => _buses.Count;

        // ViewHolder handles the UI clicks
        public class BusViewHolder : RecyclerView.ViewHolder
        {
            public TextView TvBusLine { get; }
            public TextView TvDetails { get; }
            public ImageButton BtnEdit { get; }
            public ImageButton BtnDelete { get; }

            public BusViewHolder(View itemView, Action<int> editHandler, Action<int> deleteHandler) : base(itemView)
            {
                TvBusLine = itemView.FindViewById<TextView>(Resource.Id.tvBusLineName);
                TvDetails = itemView.FindViewById<TextView>(Resource.Id.tvDetails);
                BtnEdit = itemView.FindViewById<ImageButton>(Resource.Id.btnEditBus);
                BtnDelete = itemView.FindViewById<ImageButton>(Resource.Id.btnDeleteBus);

                // Handling clicks in the constructor (SOLID: Single UI Responsibility)
                BtnEdit.Click += (s, e) => editHandler?.Invoke(AdapterPosition);
                BtnDelete.Click += (s, e) => deleteHandler?.Invoke(AdapterPosition);
            }
        }
    }
}