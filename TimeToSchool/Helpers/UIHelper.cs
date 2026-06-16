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
            if (view.Parent?.Parent is TextInputLayout parent) parent.Enabled = isEnabled;
        }

        public static void ConfigureSearchableField(AutoCompleteTextView view)
        {
            var keyboardReady = new bool[] { false };

            view.FocusChange += (s, e) => {
                if (e.HasFocus) {
                    keyboardReady[0] = false;
                    view.PostDelayed(() => {
                        if (view.HasFocus)
                            keyboardReady[0] = true;
                    }, 350);
                } else {
                    keyboardReady[0] = false;
                }
            };

            // No Click handler: ExposedDropdownMenu already toggles open/close on tap.
            // Adding our own Click handler would fight the toggle and prevent the user
            // from ever closing the dropdown by tapping the field again.

            view.AfterTextChanged += (s, e) => {
                if (string.IsNullOrEmpty(view.Text) && keyboardReady[0])
                    ShowAllItems(view);
            };
        }

        private static void ShowAllItems(AutoCompleteTextView view)
        {
            var filter = (view.Adapter as IFilterable)?.Filter;
            if (filter != null)
                filter.InvokeFilter((string)null, new FilterListener(() => view.Post(() => {
                    if (!view.IsPopupShowing)
                        view.ShowDropDown();
                })));
            else
                view.Post(() => {
                    if (!view.IsPopupShowing)
                        view.ShowDropDown();
                });
        }

        // Programmatically setting .Text (e.g. restoring a saved selection) runs it through the
        // adapter's filter just like typing would, narrowing the dropdown to that one match. Call
        // this afterward to reset the adapter back to its full list without touching the displayed text.
        public static void ResetDropdownFilter(AutoCompleteTextView view)
        {
            var filter = (view.Adapter as IFilterable)?.Filter;
            filter?.InvokeFilter((string)null, null);
        }

        private class FilterListener : Java.Lang.Object, Filter.IFilterListener
        {
            private readonly Action _onComplete;
            public FilterListener(Action onComplete) { _onComplete = onComplete; }
            public void OnFilterComplete(int count) => _onComplete();
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

        public static void HideKeyboard(View view)
        {
            var imm = (Android.Views.InputMethods.InputMethodManager)view.Context.GetSystemService(Android.Content.Context.InputMethodService);
            imm.HideSoftInputFromWindow(view.WindowToken, 0);
            view.ClearFocus();
        }
        public static Dialog CreateProgressDialog(Activity activity)
        {
            Dialog dialog = new Dialog(activity, Android.Resource.Style.ThemeNoTitleBar);
            View view = LayoutInflater.From(activity).Inflate(Resource.Layout.fb_progressbar, null);
            dialog.Window.SetBackgroundDrawable(new Android.Graphics.Drawables.ColorDrawable(Color.Transparent));
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