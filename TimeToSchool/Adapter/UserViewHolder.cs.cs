using Android.Views;
using Android.Widget;
using AndroidX.RecyclerView.Widget;
using System;

namespace TimeToSchool.Adapter
{
    public class UserViewHolder : RecyclerView.ViewHolder
    {
        public TextView firstName, lastName, tvEmail, tvRoleBadge;
        public ImageView ivAvatar;
        public ImageButton btnEdit, btnDelete;
        public View viewAccentBorder;

        public UserViewHolder(View itemView, Action<int> onEdit, Action<int> onDelete) : base(itemView)
        {
            firstName       = itemView.FindViewById<TextView>(Resource.Id.tvFirstName);
            lastName        = itemView.FindViewById<TextView>(Resource.Id.tvLastName);
            tvEmail         = itemView.FindViewById<TextView>(Resource.Id.tvEmail);
            tvRoleBadge     = itemView.FindViewById<TextView>(Resource.Id.tvRoleBadge);
            ivAvatar        = itemView.FindViewById<ImageView>(Resource.Id.ivAvatar);
            btnEdit         = itemView.FindViewById<ImageButton>(Resource.Id.btnEdit);
            btnDelete       = itemView.FindViewById<ImageButton>(Resource.Id.btnDelete);
            viewAccentBorder = itemView.FindViewById<View>(Resource.Id.viewAccentBorder);

            btnEdit.Click   += (s, e) => onEdit(LayoutPosition);
            btnDelete.Click += (s, e) => onDelete(LayoutPosition);
        }
    }
}
