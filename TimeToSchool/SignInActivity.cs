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
    [Activity(Label = "Sign In", Name = "com.companyname.timetoschool.SignInActivity")]
    public class SignInActivity : AppCompatActivity, Android.Views.View.IOnClickListener
    {
        private EditText etEmail, etPass;
        private Button btnSignIn;
        private TextView btnSighUp;
        private Dialog mProgressDialog;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
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
            btnSignIn.SetOnClickListener(this);
            btnSighUp.SetOnClickListener(this);

            mProgressDialog = UIHelper.CreateProgressDialog(this);
            //Debug Mode
            if (ProManager.DebugMode)
            {
                etEmail.Text = "yoav@gmail.com";
                etPass.Text = "123456";
            }

        }
        private async void SignInWithEmailAndPassword()
        {
            string userAuthID = await UsersRepository.SignInUserAsync(etEmail.Text, etPass.Text);
            if (userAuthID != null) //Success
            {
                Log.Debug(ProManager.TAG, $"Firebase Auth SignIn success: {etEmail.Text} {etPass.Text}");
                //Toast.MakeText(this, "SignIn Success", ToastLength.Short).Show();
                GetCurrentUserFromDB(userAuthID);
            }
            else
            {
                mProgressDialog.Dismiss();
                Log.Debug(ProManager.TAG, $"Firebase Auth SignIn Failed: {etEmail.Text} {etPass.Text}");
                Toast.MakeText(this, "SignIn Process failed", ToastLength.Short).Show();
            }
        }

        private async void GetCurrentUserFromDB(string userAuthID)
        {
            var userfromDB = await UsersRepository.GetUserById(userAuthID);

            if (userfromDB != null)
            {
                //Get current user from Firestore DB
                //Set Current User
                mProgressDialog.Dismiss();
                ProManager.CurrentUser = userfromDB;
                if (ProManager.CurrentUser.IsAdmin)
                    StartActivity(typeof(AdminMainActivity));
                else
                    StartActivity(typeof(DriverActivity));
            }
            else
            {
                mProgressDialog.Dismiss();
                Log.Debug(ProManager.TAG, "SighIn: Failed get user from DB");
                Toast.MakeText(this, "SignIn Process failed", ToastLength.Short).Show();
            }

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
        
 
        public override bool DispatchTouchEvent(MotionEvent ev)
        {// hiding keyboard when user clicks outside of EditText
            UIHelper.HandleOutsideTouch(this, ev);
            return base.DispatchTouchEvent(ev);
        }

        private void HideKeyboard()
        {
            UIHelper.HideKeyboard(this);    
        }
        private bool Validate()
        {// Basic validation for email and password fields

            return true;
        }
    }
}