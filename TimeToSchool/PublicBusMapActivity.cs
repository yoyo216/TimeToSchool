using Android.App;
using Android.OS;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using AndroidX.AppCompat.App;
using TimeToSchool.Fragments;

namespace TimeToSchool
{
    [Activity(Label = "מפת האוטובוס שלי")]
    public class PublicBusMapActivity : AppCompatActivity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_public_bus_map);
            SupportActionBar?.SetDisplayHomeAsUpEnabled(true);

            if (savedInstanceState == null)
            {
                var frag = new PublicBusMapFragment();
                var args = new Bundle();
                args.PutString("school", Intent?.GetStringExtra("school") ?? string.Empty);
                args.PutString("town", Intent?.GetStringExtra("town") ?? string.Empty);
                args.PutString("bus_line", Intent?.GetStringExtra("bus_line") ?? string.Empty);
                frag.Arguments = args;

                SupportFragmentManager.BeginTransaction()
                    .Replace(Resource.Id.mapContainer, frag)
                    .Commit();
            }
        }

        public override bool DispatchTouchEvent(MotionEvent ev)
        {
            if (ev.Action == MotionEventActions.Down)
            {
                var focused = CurrentFocus;
                if (focused is EditText et)
                {
                    var rect = new Android.Graphics.Rect();
                    et.GetGlobalVisibleRect(rect);
                    if (!rect.Contains((int)ev.RawX, (int)ev.RawY))
                    {
                        et.ClearFocus();
                        var imm = (InputMethodManager)GetSystemService(InputMethodService);
                        imm.HideSoftInputFromWindow(et.WindowToken, HideSoftInputFlags.None);
                    }
                }
            }
            return base.DispatchTouchEvent(ev);
        }

        public override bool OnSupportNavigateUp()
        {
            Finish();
            return true;
        }
    }
}
