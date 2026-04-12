using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Firebase.Firestore;
using Java.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TimeToSchool.Model
{
    public class ActiveBus
    {

        public string SchoolName { get; set; }
        public string Town { get; set; }
        public string BusLine { get; set; }
        public string DriverName { get; set; }
        public string DriverId { get; set; }
        public string Status { get; set; } // "Active" or "Inactive"
        public string Date { get; set; }   // "yyyy-MM-dd"
        public double Latitude { get; set; }
        public double Longitude { get; set; }

        // Helper to turn this object into a Firebase Map
        public HashMap ToMap()
        {
            HashMap map = new HashMap();
            map.Put("schoolName", SchoolName);
            map.Put("town", Town);
            map.Put("busLine", BusLine);
            map.Put("driverName", DriverName);
            map.Put("driverId", DriverId);
            map.Put("status", Status);
            map.Put("date", Date);
            map.Put("location", new GeoPoint(Latitude, Longitude));
            map.Put("lastUpdated", FieldValue.ServerTimestamp());
            return map;
        }

        // Helper to generate the unique ID
        public string GetDocId()
        {
            string rawId = $"{Date}_{SchoolName}_{Town}_{BusLine}_{DriverId.Substring(0, 6)}";
            return rawId.Replace(" ", "_").ToLower();
        }
    }
}
