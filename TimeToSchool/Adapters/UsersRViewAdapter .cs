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
using TimeToSchool.BusinessLogic;
using TimeToSchool.Model;
using TimeToSchool.Service;

namespace TimeToSchool.Adapters
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
                userViewHolder.FirstName.Text = users[position].FirstName;
                userViewHolder.LastName.Text = users[position].LastName;
                userViewHolder.IvAvatar.SetImageResource(users[position].ImageId);
            }
        }

        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            LinearLayout layout = (LinearLayout)LayoutInflater.From(context)
                .Inflate(Resource.Layout.user_item, parent, false);

            UserViewHolder viewHolder = new UserViewHolder(layout, OnClick);
            return viewHolder;
        }
    }
}