using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using AndroidX.ViewPager2.Adapter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TimeToSchool.Fragments;

namespace TimeToSchool.Adapter
{
    public class BusPagerAdapter : FragmentStateAdapter
    {
        public BusPagerAdapter(AndroidX.Fragment.App.Fragment fragment) : base(fragment)
        {
        }

        // 2. ItemCount: We have 2 tabs (All Routes and Live Trips)
        public override int ItemCount => 2;

        // 3. CreateFragment: Returns the specific fragment for each tab position
        public override AndroidX.Fragment.App.Fragment CreateFragment(int position)
        {
            switch (position)
            {
                case 0:
                    return new AllBusesFragment();
                case 1:
                    return new ActiveBusesFragment();
                default:
                    return new AllBusesFragment();
            }
        }
    }
}
