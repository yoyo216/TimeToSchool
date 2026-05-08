using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using AndroidX.Fragment.App;
using AndroidX.RecyclerView.Widget;
using AndroidX.ViewPager2.Widget;
using Firebase.Firestore;
using Google.Android.Material.FloatingActionButton;
using Google.Android.Material.Tabs;
using System;
using System.Collections.Generic;
using TimeToSchool.Adapter;
using TimeToSchool.BusinessLogic;
using TimeToSchool.Model;
using TimeToSchool.Service;
namespace TimeToSchool.Fragments
{
    public class BusesManagementFragment : AndroidX.Fragment.App.Fragment
    {
        private ViewPager2 _viewPager;
        private TabLayout _tabLayout;
        private BusPagerAdapter _pagerAdapter;

        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            return inflater.Inflate(Resource.Layout.fragment_buses_management, container, false);
        }

        public override void OnViewCreated(View view, Bundle savedInstanceState)
        {
            base.OnViewCreated(view, savedInstanceState);
            InitViews(view);
            SetupViewPager();
        }

        // SOLID: Single Responsibility - Only handles View Finding
        private void InitViews(View view)
        {
            _viewPager = view.FindViewById<ViewPager2>(Resource.Id.busViewPager);
            _tabLayout = view.FindViewById<TabLayout>(Resource.Id.busTabLayout);
        }

        // SOLID: Single Responsibility - Only handles Pager Setup
        private void SetupViewPager()
        {
            _pagerAdapter = new BusPagerAdapter(this);
            _viewPager.Adapter = _pagerAdapter;

            // Using the TabMediator to connect them
            new TabLayoutMediator(_tabLayout, _viewPager, new BusTabConfiguration()).Attach();
        }
    }

    // SOLID: Single Responsibility - This class ONLY cares about Tab Titles
    public class BusTabConfiguration : Java.Lang.Object, TabLayoutMediator.ITabConfigurationStrategy
    {
        public void OnConfigureTab(TabLayout.Tab tab, int position)
        {
            switch (position)
            {
                case 0: tab.SetText("All Routes"); break;
                case 1: tab.SetText("Live Trips"); break;
                case 2: tab.SetText("Map"); break;
            }
        }
    }
}