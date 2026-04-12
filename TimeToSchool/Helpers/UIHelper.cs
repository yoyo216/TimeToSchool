using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Google.Android.Material.TextField;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TimeToSchool.Helpers
{
    public static class UIHelper
    {
        public static void SetFieldEnabled(AutoCompleteTextView view, bool isEnabled)
        {
            view.Enabled = isEnabled;
            view.Alpha = isEnabled ? 1.0f : 0.5f;
            if (view.Parent.Parent is TextInputLayout parent) parent.Enabled = isEnabled;
        }

        public static void ConfigureSearchableField(AutoCompleteTextView view)
        {
            view.Threshold = 1;
            view.Click += (s, e) => view.ShowDropDown();
            view.FocusChange += (s, e) => { if (e.HasFocus) view.ShowDropDown(); };
        }

        public static void HideKeyboard(Activity activity)
        {
            var imm = (Android.Views.InputMethods.InputMethodManager)activity.GetSystemService(Android.Content.Context.InputMethodService);
            if (activity.CurrentFocus != null)
            {
                imm.HideSoftInputFromWindow(activity.CurrentFocus.WindowToken, 0);
                activity.CurrentFocus.ClearFocus();
            }
        }
        public static Dialog CreateProgressDialog(Activity activity)
        {
            Dialog dialog = new Dialog(activity, Android.Resource.Style.ThemeNoTitleBar);
            View view = LayoutInflater.From(activity).Inflate(Resource.Layout.fb_progressbar, null);
            dialog.Window.SetBackgroundDrawableResource(Resource.Color.mtrl_btn_transparent_bg_color);
            dialog.SetContentView(view);
            dialog.SetCancelable(false);
            return dialog;
        }
        public static void HandleOutsideTouch(Activity activity, MotionEvent ev)
        {
            if (ev.Action == MotionEventActions.Down)
            {
                View v = activity.CurrentFocus;
                if (v is EditText)
                {
                    Rect outRect = new Rect();
                    v.GetGlobalVisibleRect(outRect);

                    // Check if the touch was outside the focused EditText
                    if (!outRect.Contains((int)ev.RawX, (int)ev.RawY))
                    {
                        HideKeyboard(activity);
                    }
                }
            }
        }
    }
}