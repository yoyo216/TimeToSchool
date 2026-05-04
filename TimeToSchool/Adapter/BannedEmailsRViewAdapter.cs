using Android.Content;
using Android.Views;
using Android.Widget;
using AndroidX.RecyclerView.Widget;
using System;
using System.Collections.Generic;
using TimeToSchool.Model;

namespace TimeToSchool.Adapter
{
    public class BannedEmailsRViewAdapter : RecyclerView.Adapter
    {
        private readonly Context _context;
        private List<BannedEmail> _items;

        public event EventHandler<int> ItemUnbanClick;

        public BannedEmailsRViewAdapter(Context context, List<BannedEmail> items)
        {
            _context = context;
            _items = items ?? new List<BannedEmail>();
        }

        public override int ItemCount => _items.Count;

        public void UpdateData(List<BannedEmail> newList)
        {
            _items = newList ?? new List<BannedEmail>();
            NotifyDataSetChanged();
        }

        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            var view = LayoutInflater.From(_context).Inflate(Resource.Layout.banned_email_item, parent, false);
            return new BannedEmailViewHolder(view, pos => ItemUnbanClick?.Invoke(this, pos));
        }

        public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
        {
            if (!(holder is BannedEmailViewHolder vh)) return;
            var entry = _items[position];
            vh.tvEmail.Text    = entry.Email;
            vh.tvBannedAt.Text = entry.BannedAt == default
                ? string.Empty
                : $"Banned {entry.BannedAt.ToLocalTime():yyyy-MM-dd HH:mm}";
        }

        private class BannedEmailViewHolder : RecyclerView.ViewHolder
        {
            public TextView tvEmail, tvBannedAt;
            public Button btnUnban;

            public BannedEmailViewHolder(View itemView, Action<int> onUnban) : base(itemView)
            {
                tvEmail    = itemView.FindViewById<TextView>(Resource.Id.tvBannedEmail);
                tvBannedAt = itemView.FindViewById<TextView>(Resource.Id.tvBannedAt);
                btnUnban   = itemView.FindViewById<Button>(Resource.Id.btnUnban);

                btnUnban.Click += (s, e) => onUnban(LayoutPosition);
            }
        }
    }
}
