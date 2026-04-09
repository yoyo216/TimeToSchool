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
        public event EventHandler<int> ItemClick;

        public UsersRViewAdapter(Context context, List<User> users)
        {
            this.context = context;
            this.users = users;
        }

        public override int ItemCount => users.Count;

        void OnClick(int position)
        {
            if (ItemClick != null)
                ItemClick(this, position);
        }

        public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
        {
            if (holder is UserViewHolder userViewHolder)
            {
                userViewHolder.firstName.Text = users[position].FirstName;
                userViewHolder.lastName.Text = users[position].LastName;
                userViewHolder.ivAvatar.SetImageResource(users[position].ImageId);
            }
        }

        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            LinearLayout layout = (LinearLayout)LayoutInflater.From(context)
                .Inflate(Resource.Layout.usercard_item, parent, false);

            UserViewHolder viewHolder = new UserViewHolder(layout, OnClick);
            return viewHolder;
        }
    }
}