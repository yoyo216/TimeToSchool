using Android.App;
using Android.OS;
using AndroidX.AppCompat.App;
using TimeToSchool.Fragments;

namespace TimeToSchool
{
    [Activity(Label = "מפת אוטובוסים")]
    public class BusMapActivity : AppCompatActivity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_bus_map);
            SupportActionBar?.SetDisplayHomeAsUpEnabled(true);
            if (savedInstanceState == null)
                SupportFragmentManager.BeginTransaction()
                    .Replace(Resource.Id.mapContainer, new BusMapFragment())
                    .Commit();
        }

        public override bool OnSupportNavigateUp()
        {
            Finish();
            return true;
        }
    }
}
