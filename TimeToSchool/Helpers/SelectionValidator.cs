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
    public class SelectionValidator
    {
        public bool IsValid(string school, string town, string bus)
        {
            return !string.IsNullOrWhiteSpace(school) &&
                   !string.IsNullOrWhiteSpace(town) &&
                   !string.IsNullOrWhiteSpace(bus);
        }
    }
}