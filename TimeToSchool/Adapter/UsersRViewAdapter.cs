using Android.App;
using Android.Content;
using Android.Views;
using Android.Widget;
using AndroidX.RecyclerView.Widget;
using System;
using System.Collections.Generic;
using TimeToSchool.Model;

namespace TimeToSchool.Adapter
{
    public class UsersRViewAdapter : RecyclerView.Adapter
    {
        Context context;
        List<User> users;

        // C# Events to notify the Fragment of clicks
        public event EventHandler<int> ItemClick;
        public event EventHandler<int> AllowClick;
        public event EventHandler<int> DenyClick;

        public UsersRViewAdapter(Context context, List<User> users)
        {
            this.context = context;
            this.users = users;
        }

        public override int ItemCount => users.Count;

        public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
        {
            if (holder is UserViewHolder userViewHolder)
            {
                var user = users[position];
                userViewHolder.tvUserName.Text = user.FirstName;
                userViewHolder.tvStatus.Text = user.Status;
                userViewHolder.ivAvatar.SetImageResource(user.ImageId);

                // Set the Status text
                userViewHolder.tvStatus.Text = $"Status: {user.Status}";

                // Logic: Only show buttons if the user is currently Pending
                if (user.Status == UserStatus.Pending)
                {
                    userViewHolder.btnAllow.Visibility = ViewStates.Visible;
                    userViewHolder.btnDeny.Visibility = ViewStates.Visible;
                }
                else
                {
                    // Hide buttons if already Approved or Rejected
                    userViewHolder.btnAllow.Visibility = ViewStates.Gone;
                    userViewHolder.btnDeny.Visibility = ViewStates.Gone;
                }
            }
        }

        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            // FIX: Name the variable 'itemView' so the constructor can find it
            View itemView = LayoutInflater.From(parent.Context).Inflate(Resource.Layout.usercard_item, parent, false);

            var viewHolder = new UserViewHolder(
                itemView,
                (pos) => ItemClick?.Invoke(this, pos),   // Standard Card Click
                (pos) => AllowClick?.Invoke(this, pos),  // Allow Button Click
                (pos) => DenyClick?.Invoke(this, pos)   // Deny Button Click
            );

            return viewHolder;
        }
    }
}