using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Views;
using Android.Widget;
using System;
using TimeToSchool.BusinessLogic;
using TimeToSchool.Helpers;
using TimeToSchool.Service;

namespace TimeToSchool
{
    [Activity(Label = "SignUpActivity")]
    public class SignUpActivity : Activity
    {
        EditText _firstName, _lastName, _userEmail, _userPassword, _userMobile;
        Button _btnSignUp;
        CheckBox _cbRememberMe;
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
            _cbRememberMe = FindViewById<CheckBox>(Resource.Id.cbRememberMeSignUp);
            _btnSignUp = FindViewById<Button>(Resource.Id.btn_register);



            _btnSignUp.Click += BtnSignUp_Click;
        }

        private void BtnSignUp_Click(object sender, EventArgs e)
        {
            // 2. Hide keyboard when button is clicked so it doesn't cover the progress dialog
            HideKeyboard();

            _user = new Model.User()
            {
                FirstName = _firstName.Text,
                LastName = _lastName.Text,
                UserEmail = _userEmail.Text,
                UserPass = _userPassword.Text,
                UserMobile = _userMobile.Text,
                Status = "pending"
            };

            RegisterNewUser();
        }


        private async void RegisterNewUser()
        {
            ShowProgressBar(true);
            try
            {
                if (await BannedEmailsRepository.IsEmailBanned(_user.UserEmail))
                {
                    ShowProgressBar(false);
                    Toast.MakeText(this, "כתובת מייל זו חסומה", ToastLength.Short).Show();
                    return;
                }

                _user.Id = await UsersRepository.InsertAsync(_user);
                ShowProgressBar(false);
                Toast.MakeText(this, $"SignUp succeeded!", ToastLength.Short).Show();

                ProManager.CurrentUser = _user;
                if (_cbRememberMe.Checked)
                    new PreferenceService(this).SaveUserObject(_user);
                RoleRouter.RouteFor(this, _user);
            }
            catch (Exception)
            {
                ShowProgressBar(false);
                Toast.MakeText(this, $"SignUp new user failed!", ToastLength.Short).Show();
            }
        }
        public override bool DispatchTouchEvent(MotionEvent ev)
        {
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
        private void ShowProgressBar(bool show)
        {
            if (show)
            {
                mProgressDialog = new Dialog(this, Android.Resource.Style.ThemeNoTitleBar);
                View view = LayoutInflater.From(this).Inflate(Resource.Layout.fb_progressbar, null);
                mProgressDialog.Window.SetBackgroundDrawable(new Android.Graphics.Drawables.ColorDrawable(Android.Graphics.Color.Transparent));
                mProgressDialog.SetContentView(view);
                mProgressDialog.SetCancelable(false);
                mProgressDialog.Show();
            }
            else if (mProgressDialog != null)
            {
                mProgressDialog.Dismiss();
            }
        }
    }
}