using Android.App;
using Android.Content;
using Android.Gms.Extensions;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using AndroidX.RecyclerView.Widget;
using Firebase.Firestore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TimeToSchool.Adapters;
using TimeToSchool.BusinessLogic;
using TimeToSchool.Model;
using TimeToSchool.Service;

namespace TimeToSchool
{
    [Activity(Label = "User Management")]
    public class AdminUserManagementActivity : Activity
    {
        RecyclerView usersRecyclerView;
        RecyclerView.LayoutManager layoutManager;
        UsersRViewAdapter userAdapter;

        TextView tvusername, tvisadmin, tvuserslist;
        Dialog mProgressDialog;
        List<User> users;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.admin_usermanagement);

            InitializeViews();
        }

        private void InitializeViews()
        {
            tvusername = FindViewById<TextView>(Resource.Id.tvUsername);
            tvisadmin = FindViewById<TextView>(Resource.Id.tvIsAdmin);
            tvuserslist = FindViewById<TextView>(Resource.Id.tvUserslist);

            layoutManager = new LinearLayoutManager(this);
            usersRecyclerView = FindViewById<RecyclerView>(Resource.Id.recyclerView);

            users = new List<User>();
            userAdapter = new UsersRViewAdapter(this, users);
            usersRecyclerView.SetLayoutManager(layoutManager);
            userAdapter.ItemClick += OnItemClick;
            usersRecyclerView.SetAdapter(userAdapter);
        }

        void OnItemClick(object sender, int position)
        {
            //Intent intent = new Intent(this, typeof(AccountActivity));
            //intent.PutExtra("userID", users[position].Id);
            //StartActivity(intent);

            //Toast.MakeText(this, "This is item number " + itemIndewx, ToastLength.Short).Show();
        }

        protected override void OnResume()
        {
            base.OnResume();
            tvusername.Text = ProManager.CurrentUser.FirstName;

            if (ProManager.CurrentUser.IsAdmin)
                tvisadmin.Visibility = ViewStates.Visible;

            ShowProgressBar(true);
            //GetUsersFromDB();
            FetchUsersFromDB();
        }

        protected override void OnPause()
        {
            base.OnPause();
            FireBaseHelper.StopUsersListener();
        }

        private void FetchUsersFromDB()
        {
            FireBaseHelper.FetchUsersListener();
            FireBaseHelper.FirestoreEventListener.getEvent += (error, args) =>
            {
                ShowProgressBar(false);
                if (users != null)
                    users.Clear(); //Clear users list
                else
                    users = new List<User>();

                try
                {
                    var snapshot = (QuerySnapshot)args.Result;
                    if (!snapshot.IsEmpty)
                    {
                        var documents = snapshot.Documents;
                        foreach (DocumentSnapshot item in documents)
                        {
                            User _user = new User()
                            {
                                Id = item.Id,
                                FirstName = item.Get("FirstName").ToString(),
                                LastName = item.Get("LastName").ToString(),
                                UserEmail = item.Get("UserEmail").ToString(),
                                UserMobile = item.Get("UserMobile").ToString(),
                                UserPass = item.Get("UserPassword").ToString(),
                                IsAdmin = bool.Parse(item.Get("IsAdmin").ToString()),
                                ImageId = Resource.Drawable.ic_maleicon
                            };
                            users.Add(_user);
                        }

                        userAdapter.NotifyDataSetChanged();
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug(ProManager.TAG, ex.Message);
                }
            };
        }

        private void ShowProgressBar(bool show)
        {
            //android:background="@android:color/transparent"

            if (show)
            {
                mProgressDialog = new Dialog(this, Android.Resource.Style.ThemeNoTitleBar);
                View view = LayoutInflater.From(this).Inflate(Resource.Layout.fb_progressbar, null);
                //var mProgressMessage = (TextView)view.FindViewById(Resource.Id.;
                //mProgressMessage.Text = "Loading...";
                mProgressDialog.Window.SetBackgroundDrawableResource(Resource.Color.mtrl_btn_transparent_bg_color);
                mProgressDialog.SetContentView(view);
                mProgressDialog.SetCancelable(false);
                mProgressDialog.Show();
            }
            else
            {
                mProgressDialog.Dismiss();
            }
        }
    }
}