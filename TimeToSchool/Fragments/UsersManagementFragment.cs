using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using AndroidX.RecyclerView.Widget;
using Firebase.Firestore;
using System;
using System.Collections.Generic;
using TimeToSchool.Adapter;
using TimeToSchool.BusinessLogic;
using TimeToSchool.Model;
using TimeToSchool.Service;
using AndroidX.Fragment.App;

namespace TimeToSchool.Fragments
{
    public class UsersManagementFragment : AndroidX.Fragment.App.Fragment
    {
        RecyclerView usersRecyclerView;
        RecyclerView.LayoutManager layoutManager;
        UsersRViewAdapter userAdapter;

        TextView tvusername, tvisadmin, tvuserslist;
        Dialog mProgressDialog;
        List<Model.User> users;

        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            View view = inflater.Inflate(Resource.Layout.fragment_users_management, container, false);
            InitializeViews(view);
            return view;
        }

        private void InitializeViews(View view)
        {
            tvusername = view.FindViewById<TextView>(Resource.Id.tvUsername);
            tvisadmin = view.FindViewById<TextView>(Resource.Id.tvIsAdmin);
            tvuserslist = view.FindViewById<TextView>(Resource.Id.tvUserslist);

            layoutManager = new LinearLayoutManager(Activity);
            usersRecyclerView = view.FindViewById<RecyclerView>(Resource.Id.recyclerView);

            users = new List<Model.User>();
            userAdapter = new UsersRViewAdapter(Activity, users);
            usersRecyclerView.SetLayoutManager(layoutManager);

            // Subscribe to all adapter events


            usersRecyclerView.SetAdapter(userAdapter);
        }


        



        void OnItemClick(object sender, int position)
        {
            // Logic for viewing profile details
        }

        public override void OnResume()
        {
            base.OnResume();

            // Defensive check: If for some reason the app lost the user object, send them back to login
            if (ProManager.CurrentUser == null)
            {
                StartActivity(new Intent(Activity, typeof(SignInActivity)));
                return;
            }

            tvusername.Text = ProManager.CurrentUser.FirstName;

            if (ProManager.CurrentUser.IsAdmin)
                tvisadmin.Visibility = ViewStates.Visible;

            ShowProgressBar(true);
            FetchUsersFromDB();
        }


        public override void OnPause()
        {
            base.OnPause();
            UsersRepository.StopUsersListener();
        }

        private void FetchUsersFromDB()
        {
            UsersRepository.FetchUsersListener();
            UsersRepository.FirestoreEventListener.getEvent += (error, args) =>
            {
                Activity?.RunOnUiThread(() => {
                    ShowProgressBar(false);

                    if (users != null)
                        users.Clear();
                    else
                        users = new List<User>();

                    try
                    {
                        var snapshot = (QuerySnapshot)args.Result;
                        if (snapshot != null && !snapshot.IsEmpty)
                        {
                            var documents = snapshot.Documents;
                            foreach (DocumentSnapshot item in documents)
                            {
                                // Pulling the status and using null-coalescing for safety
                                User _user = new User()
                                {
                                    Id = item.Id,
                                    FirstName = item.Get("FirstName")?.ToString(),
                                    LastName = item.Get("LastName")?.ToString(),
                                    UserEmail = item.Get("UserEmail")?.ToString(),
                                    UserMobile = item.Get("UserMobile")?.ToString(),
                                    UserPass = item.Get("UserPassword")?.ToString(),
                                    IsAdmin = bool.Parse(item.Get("IsAdmin")?.ToString() ?? "false"),
                                    ImageId = Resource.Drawable.ic_icon_person
                                };
                                users.Add(_user);
                            }
                            userAdapter.NotifyDataSetChanged();
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ProManager.TAG, $"fetch user from db failed: {ex.Message}");
                    }
                });
            };
        }

        private void ShowProgressBar(bool show)
        {
            if (show)
            {
                mProgressDialog = new Dialog(Activity, Android.Resource.Style.ThemeNoTitleBar);
                View view = LayoutInflater.From(Activity).Inflate(Resource.Layout.fb_progressbar, null);
                mProgressDialog.Window.SetBackgroundDrawableResource(Resource.Color.mtrl_btn_transparent_bg_color);
                mProgressDialog.SetContentView(view);
                mProgressDialog.SetCancelable(false);
                mProgressDialog.Show();
            }
            else
            {
                mProgressDialog?.Dismiss();
            }
        }
    }
}