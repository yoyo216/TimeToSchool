using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.App;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TimeToSchool.BusinessLogic;
using TimeToSchool.Helpers;
using TimeToSchool.Service;

namespace TimeToSchool
{
    [Activity(Label = "Sign In", Name = "com.companyname.timetoschool.SignInActivity", MainLauncher = true)]
    public class SignInActivity : AppCompatActivity, View.IOnClickListener
    {
        private EditText etEmail, etPass;
        private Button btnSignIn;
        private TextView btnSighUp;
        private CheckBox cbRememberMe;
        private Dialog mProgressDialog;
        private PreferenceService _prefService; // Define service

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // 1. Initialize Preference Service
            _prefService = new PreferenceService(this);



            SetContentView(Resource.Layout.signin_layout);
            InitilizeViews();
            Log.Debug(ProManager.TAG, $"SignInActivity: OnCreate()");
        }

        private void InitilizeViews()
        {
            etEmail = FindViewById<EditText>(Resource.Id.et_email2);
            etPass = FindViewById<EditText>(Resource.Id.et_password2);
            btnSignIn = FindViewById<Button>(Resource.Id.btn_login2);
            btnSighUp = FindViewById<TextView>(Resource.Id.btn_sign_up);

            // FIX: Initialize the CheckBox!
            cbRememberMe = FindViewById<CheckBox>(Resource.Id.cbRememberMe);

            btnSignIn.SetOnClickListener(this);
            btnSighUp.SetOnClickListener(this);

            mProgressDialog = UIHelper.CreateProgressDialog(this);

            if (ProManager.DebugMode)
            {
                etEmail.Text = "yoav@gmail.com";
                etPass.Text = "123456";
            }
        }

        private async void SignInWithEmailAndPassword()
        {
            string userAuthID = await UsersRepository.SignInUserAsync(etEmail.Text, etPass.Text);
            if (userAuthID != null)
            {
                GetCurrentUserFromDB(userAuthID);
            }
            else
            {
                mProgressDialog.Dismiss();
                Toast.MakeText(this, "SignIn Process failed", ToastLength.Short).Show();
            }
        }

        private async void GetCurrentUserFromDB(string userAuthID)
        {
            var userfromDB = await UsersRepository.GetUserById(userAuthID);
            mProgressDialog.Dismiss();

            if (userfromDB != null)
            {
                ProManager.CurrentUser = userfromDB;
                string role = userfromDB.IsAdmin ? "Admin" : "Driver";

                // FIX: Handle the Remember Me saving here
                if (cbRememberMe.Checked)
                {
                    _prefService.SaveUserObject(userfromDB);
                }

                NavigateByRole(role);
            }
            else
            {
                Toast.MakeText(this, "Failed to get user profile", ToastLength.Short).Show();
            }
        }

        private void NavigateByRole(string role)
        {
            Intent intent;
            if (role == "Admin")
                intent = new Intent(this, typeof(AdminMainActivity));
            else
                intent = new Intent(this, typeof(DriverActivity));

            StartActivity(intent);
            Finish(); // Ensures user can't go back to Login
        }

        public void OnClick(View v)
        {
            if (v == btnSignIn)
            {
                if (Validate())
                {
                    mProgressDialog.Show();
                    SignInWithEmailAndPassword();
                }
            }
            else if (v == btnSighUp)
            {
                StartActivity(typeof(SignUpActivity));
            }
        }

        private bool Validate()
        {
            if (string.IsNullOrEmpty(etEmail.Text) || string.IsNullOrEmpty(etPass.Text))
            {
                Toast.MakeText(this, "Please enter email and password", ToastLength.Short).Show();
                return false;
            }
            return true;
        }

        public override bool DispatchTouchEvent(MotionEvent ev)
        {
            UIHelper.HandleOutsideTouch(this, ev);
            return base.DispatchTouchEvent(ev);
        }
    }
}