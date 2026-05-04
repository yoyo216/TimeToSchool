using Android.Content;
using Android.Views;
using Android.Widget;
using AndroidX.RecyclerView.Widget;
using System;
using System.Collections.Generic;
using TimeToSchool.Model;

namespace TimeToSchool.Adapter
{
    public class PendingRequestsRViewAdapter : RecyclerView.Adapter
    {
        private readonly Context _context;
        private List<User> _users;

        public event EventHandler<int> ItemApproveClick;
        public event EventHandler<int> ItemDeleteClick;
        public event EventHandler<int> ItemBanClick;

        public PendingRequestsRViewAdapter(Context context, List<User> users)
        {
            _context = context;
            _users = users ?? new List<User>();
        }

        public override int ItemCount => _users.Count;

        public void UpdateData(List<User> newList)
        {
            _users = newList ?? new List<User>();
            NotifyDataSetChanged();
        }

        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            var view = LayoutInflater.From(_context).Inflate(Resource.Layout.pending_request_item, parent, false);
            return new PendingRequestViewHolder(view,
                pos => ItemApproveClick?.Invoke(this, pos),
                pos => ItemDeleteClick?.Invoke(this, pos),
                pos => ItemBanClick?.Invoke(this, pos));
        }

        public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
        {
            if (!(holder is PendingRequestViewHolder vh)) return;
            var user = _users[position];
            vh.tvName.Text  = $"{user.FirstName} {user.LastName}";
            vh.tvEmail.Text = user.UserEmail;
        }

        private class PendingRequestViewHolder : RecyclerView.ViewHolder
        {
            public TextView tvName, tvEmail;
            public Button btnApprove, btnDelete, btnBan;

            public PendingRequestViewHolder(View itemView, Action<int> onApprove, Action<int> onDelete, Action<int> onBan) : base(itemView)
            {
                tvName     = itemView.FindViewById<TextView>(Resource.Id.tvPendingName);
                tvEmail    = itemView.FindViewById<TextView>(Resource.Id.tvPendingEmail);
                btnApprove = itemView.FindViewById<Button>(Resource.Id.btnApprove);
                btnDelete  = itemView.FindViewById<Button>(Resource.Id.btnDeleteReq);
                btnBan     = itemView.FindViewById<Button>(Resource.Id.btnBan);

                btnApprove.Click += (s, e) => onApprove(LayoutPosition);
                btnDelete.Click  += (s, e) => onDelete(LayoutPosition);
                btnBan.Click     += (s, e) => onBan(LayoutPosition);
            }
        }
    }
}
