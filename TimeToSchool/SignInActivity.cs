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

            //Debug Mode
            //if (ProManager.DebugMode)
            //{
            //    etEmail.Text = "yoav@gmail.com";
            //    etPass.Text = "123456";
            //    ShowProgressBar(true);
            //    SignInWithEmailAndPassword();
            //}

        }
        private async void SignInWithEmailAndPassword()
        {
            string userAuthID = await FireBaseHelper.SignInUserAsync(etEmail.Text, etPass.Text);
            if (userAuthID != null) //Success
            {
                Log.Debug(ProManager.TAG, $"Firebase Auth SignIn success: {etEmail.Text} {etPass.Text}");
                //Toast.MakeText(this, "SignIn Success", ToastLength.Short).Show();
                GetCurrentUserFromDB(userAuthID);
            }
            else
            {
                ShowProgressBar(false);
                Log.Debug(ProManager.TAG, $"Firebase Auth SignIn Failed: {etEmail.Text} {etPass.Text}");
                Toast.MakeText(this, "SignIn Process failed", ToastLength.Short).Show();
            }
        }

        private async void GetCurrentUserFromDB(string userAuthID)
        {
            var userfromDB = await FireBaseHelper.GetUserById(userAuthID);

            if (userfromDB != null)
            {
                //Get current user from Firestore DB
                //Set Current User 
                ShowProgressBar(false);
                ProManager.CurrentUser = userfromDB;
                //StartActivity(typeof(MainPage));
            }
            else
            {
                ShowProgressBar(false);
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
                    ShowProgressBar(true);
                    SignInWithEmailAndPassword();
                }
            }
            else if (v == btnSighUp)
            {
                StartActivity(typeof(SignUpActivity));
            }
            else if (v.Id == Resource.Id.rootScrollView)
            {
                var inputMethodManager = (Android.Views.InputMethods.InputMethodManager)GetSystemService(InputMethodService);
                if (inputMethodManager != null && CurrentFocus != null)
                {
                    inputMethodManager.HideSoftInputFromWindow(CurrentFocus.WindowToken, 0);
                    CurrentFocus.ClearFocus();
                }
            }
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
        public override bool DispatchTouchEvent(MotionEvent ev)
        {// hiding keyboard when user clicks outside of EditText
            if (ev.Action == MotionEventActions.Down)
            {
                View v = CurrentFocus;
                if (v is EditText)
                {
                    Rect outRect = new Rect();
                    v.GetGlobalVisibleRect(outRect);
                    if (!outRect.Contains((int)ev.RawX, (int)ev.RawY))
                        HideKeyboard();
                }
            }
            return base.DispatchTouchEvent(ev);
        }

        private void HideKeyboard()
        {
            var imm = (Android.Views.InputMethods.InputMethodManager)GetSystemService(InputMethodService);
            if (CurrentFocus != null)
            {
                imm.HideSoftInputFromWindow(CurrentFocus.WindowToken, 0);
                CurrentFocus.ClearFocus();
            }
        }
        private bool Validate()
        {// Basic validation for email and password fields

            return true;
        }
    }
}