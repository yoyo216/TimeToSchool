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

namespace TimeToSchool.Model
{
    public class DriverCardState
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string FirebaseDocumentId { get; set; }
        public ActiveBus TripData { get; set; }
        public bool IsDriving { get; set; } = false;
    }
}
