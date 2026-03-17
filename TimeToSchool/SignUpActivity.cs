using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TimeToSchool.BusinessLogic;
using TimeToSchool.Service;

namespace TimeToSchool
{
 
	[Activity(Label = "SignUpActivity")]
    public class SignUpActivity : Activity
    {
        EditText _firstName, _lastName, _userEmail, _userPassword, _userMobile;
        Button _btnSignUp;
        Dialog mProgressDialog;
        Model.User _user;


        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.signup_layout);

            InitializeViews();
        }
        private void InitializeViews()
        {
            _firstName = FindViewById<EditText>(Resource.Id.et_first_name);
            _lastName = FindViewById<EditText>(Resource.Id.et_last_name);
            _userEmail = FindViewById<EditText>(Resource.Id.et_email);
            _userPassword = FindViewById<EditText>(Resource.Id.et_password);
            _userMobile = FindViewById<EditText>(Resource.Id.et_mobile);
            _btnSignUp = FindViewById<Button>(Resource.Id.btn_register);

            _btnSignUp.Click += BtnSignUp_Click;


        }
        private void BtnSignUp_Click(object sender, EventArgs e)
        {
            _user = new Model.User()
            {
                FirstName = _firstName.Text,
                LastName = _lastName.Text,
                UserEmail = _userEmail.Text,
                UserPass = _userPassword.Text,
                UserMobile = _userMobile.Text
            };

            RegisterNewUser();
        }
        private async void RegisterNewUser()
        {
            ShowProgressBar(true);

            try
            {
                //Add user to firebase database
                _user.Id = await FireBaseHelper.InsertAsync(_user);
                ShowProgressBar(false);
                Toast.MakeText(this, $"SignUp succeeded!", ToastLength.Short).Show();

                //Set Current User
                ProManager.CurrentUser = _user;
                StartActivity(typeof(SignInActivity));
            }
            catch (Exception ex)
            {
                ShowProgressBar(false);
                Toast.MakeText(this, $"SignUp new user failed!", ToastLength.Short).Show();
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
    }
}