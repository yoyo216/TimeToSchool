using Android.App;
using Android.Content;
using Android.Media;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TimeToSchool.Model;
using TimeToSchool.Service;


namespace TimeToSchool.Service
{
    public interface IDataRepository
    {
        List<string> GetSchools(List<BusRoute> _allRoutes);
        List<string> GetTownsForSchool(List<BusRoute> _allRoutes, string schoolName);
        List<string> GetBusesForRoute(List<BusRoute> _allRoutes, string schoolName, string townName);
    }

    public class LocalDataRepository : IDataRepository
    {


        public List<string> GetSchools(List<BusRoute> _allRoutes)
        {
            return _allRoutes.Select(r => r.School)
                             .Distinct()
                             .OrderBy(s => s)
                             .ToList();
        }

        public List<string> GetTownsForSchool(List<BusRoute> _allRoutes, string schoolName)
        {
            return _allRoutes.Where(r => r.School == schoolName)
                             .Select(r => r.Town)
                             .Distinct()
                             .OrderBy(t => t)
                             .ToList();
        }

        public List<string> GetBusesForRoute(List<BusRoute> _allRoutes, string school, string town)
        {
            var buses = _allRoutes.Where(r => r.School == school && r.Town == town)
                                  .Select(r => r.BusLine)
                                  .OrderBy(b => b)
                                  .ToList();

            if (buses.Count > 0)
            {
                buses.Insert(0, "Any Available Bus");
            }

            return buses;
        }
    }
}