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

namespace TimeToSchool
{
    public interface IDataRepository
    {
        string[] GetSchools();
        string[] GetTowns();
        string[] GetBuses();
    }

    public class LocalDataRepository : IDataRepository
    {
        public string[] GetSchools() => new[] { "School A", "School B", "School C", "School D", "School E", "School F", "School G", "School H" };
        public string[] GetTowns() => new[] { "Town A", "Town B", "Town C", "Town D" };
        public string[] GetBuses() => new[] { "Bus 1", "Bus 2", "Bus 3", "Bus 4" };
    }
}