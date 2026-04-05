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

namespace TimeToSchool.Adapters
{
    public class UserViewHolder : RecyclerView.ViewHolder
    {
        public TextView FirstName, LastName;
        public ImageView IvAvatar;
        public UserViewHolder(View itemView, Action<int> listener) : base(itemView)
        {
            FirstName = itemView.FindViewById<TextView>(Resource.Id.tvFirstName);
            LastName = itemView.FindViewById<TextView>(Resource.Id.tvLastName);
            //IvAvatar = itemView.FindViewById<ImageView>(Resource.Id.ivAvatar);

            itemView.Click += (sender, e) => listener(base.LayoutPosition);
        }
    }
}