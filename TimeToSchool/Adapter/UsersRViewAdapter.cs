using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Views;
using AndroidX.RecyclerView.Widget;
using System;
using System.Collections.Generic;
using TimeToSchool.Model;

namespace TimeToSchool.Adapter
{
    public class UsersRViewAdapter : RecyclerView.Adapter
    {
        private readonly Context _context;
        private List<User> _users;

        private static readonly Color AdminAccent = Color.ParseColor("#1565C0");
        private static readonly Color UserAccent  = Color.ParseColor("#78909C");
        private static readonly Color AdminBadge  = Color.ParseColor("#1565C0");
        private static readonly Color UserBadge   = Color.ParseColor("#2E7D32");

        public event EventHandler<int> ItemEditClick;
        public event EventHandler<int> ItemDeleteClick;

        public UsersRViewAdapter(Context context, List<User> users)
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
            var view = LayoutInflater.From(_context).Inflate(Resource.Layout.usercard_item, parent, false);
            return new UserViewHolder(view,
                pos => ItemEditClick?.Invoke(this, pos),
                pos => ItemDeleteClick?.Invoke(this, pos));
        }

        public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
        {
            var vh = holder as UserViewHolder;
            if (vh == null) return;

            var user = _users[position];

            vh.firstName.Text = user.FirstName;
            vh.lastName.Text  = user.LastName;
            vh.tvEmail.Text   = user.UserEmail;
            vh.ivAvatar.SetImageResource(user.ImageId != 0 ? user.ImageId : Resource.Drawable.ic_icon_person);

            // Accent border strip
            vh.viewAccentBorder.SetBackgroundColor(user.IsAdmin ? AdminAccent : UserAccent);

            // Role badge with rounded corners
            var badge = new GradientDrawable();
            badge.SetCornerRadius(40f);
            badge.SetColor(user.IsAdmin ? AdminBadge : UserBadge);
            vh.tvRoleBadge.Background = badge;
            vh.tvRoleBadge.Text = user.IsAdmin ? "Admin" : "User";
        }
    }
}
