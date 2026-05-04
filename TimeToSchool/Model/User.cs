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
    public class User
    {
        public string Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string UserEmail { get; set; }
        public string UserPass { get; set; }
        public string UserMobile { get; set; }
        public int ImageId { get; set; }
        public bool IsAdmin { get; set; } = false;
        public string Status { get; set; } = "pending";
    }
}