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

namespace TimeToSchool.Adapter
{
    public class UserViewHolder : RecyclerView.ViewHolder
    {
        public TextView firstName, lastName;
        public ImageView ivAvatar;
        public UserViewHolder(View itemView, Action<int> listener) : base(itemView)
        {
            firstName = itemView.FindViewById<TextView>(Resource.Id.tvFirstName);
            lastName = itemView.FindViewById<TextView>(Resource.Id.tvLastName);
            ivAvatar = itemView.FindViewById<ImageView>(Resource.Id.ivAvatar);

            itemView.Click += (sender, e) => listener(base.LayoutPosition);
        }
    }
}