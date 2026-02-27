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
using TimeToSchool.Model;

namespace TimeToSchool
{
    public interface IDataRepository
    {
        List<string> GetSchools();
        List<string> GetTownsForSchool(string schoolName);
        List<string> GetBusesForRoute(string schoolName, string townName);
    }

    public class LocalDataRepository : IDataRepository
    {
        // This is your "Database"
        private readonly List<BusRoute> _allRoutes = new List<BusRoute>
        {
            // School A options
            new BusRoute { School = "School A", Town = "Town A", BusLine = "Bus 1" },
            new BusRoute { School = "School A", Town = "Town B", BusLine = "Bus 2" },
            
            // School B options
            new BusRoute { School = "School B", Town = "Town C", BusLine = "Bus 3" },
            new BusRoute { School = "School B", Town = "Town A", BusLine = "Bus 4" }
        };

        // 1. Get all unique schools
        public List<string> GetSchools()
        {
            return _allRoutes.Select(r => r.School)
                             .Distinct()
                             .OrderBy(s => s)
                             .ToList();
        }

        // 2. Get only towns that go to the selected school
        public List<string> GetTownsForSchool(string schoolName)
        {
            return _allRoutes.Where(r => r.School == schoolName)
                             .Select(r => r.Town)
                             .Distinct()
                             .OrderBy(t => t)
                             .ToList();
        }

        // 3. Get only buses for that specific School-Town pair
        public List<string> GetBusesForRoute(string school, string town)
        {
            var buses = _allRoutes.Where(r => r.School == school && r.Town == town)
                                  .Select(r => r.BusLine)
                                  .OrderBy(b => b)
                                  .ToList();

            // Add a default option at the start of the list
            if (buses.Count > 0)
            {
                buses.Insert(0, "Any Available Bus");
            }

            return buses;
        }
    }
}