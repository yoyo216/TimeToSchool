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
    public class BusRoute
    {
        public string School { get; set; }
        public string Town { get; set; }
        public string BusLine { get; set; }
    }
}