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

namespace TimeToSchool.BusinessLogic
{
    public class ProManager
    {
#if DEBUG
        public static bool DebugMode = true;
#else
        public static bool DebugMode = false;
#endif
        public static readonly string TAG = "YoavApp";

        public static User CurrentUser { get; set; }
    }
}