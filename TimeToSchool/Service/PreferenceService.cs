using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TimeToSchool.Model;

namespace TimeToSchool.Service
{
    public class PreferenceService
    {
        private const string PrefName = "TimeToSchoolPrefs"; //PREFS FILE NAME
        //private const string KeyIsLoggedIn = "isLoggedIn";
        private const string USEREMAILKEY= "userEmail";
        private const string USERROLEKEY = "userRole"; // Driver vs Admin

        private readonly ISharedPreferences _prefs;

        public PreferenceService(Context context)
        {
            _prefs = context.GetSharedPreferences(PrefName, FileCreationMode.Private);
        }

        public void SaveLoginSession(string email, string role)
        {
            _prefs.Edit().PutString(USEREMAILKEY, email).Apply();
            _prefs.Edit().PutString(USERROLEKEY, role).Apply();    
        }
       
        //public string GetSavedEmail() => _prefs.GetString(KeyUserEmail, "");
        //public string GetSavedRole() => _prefs.GetString(KeyUserRole, "");

        public void SaveUserObject(User user)
        {
            string userJson = JsonConvert.SerializeObject(user); // Convert User object to JSON string
            _prefs.Edit().PutString("user_json", userJson).Apply();
        }

        public User GetSavedUser()
        {
            string userJson = _prefs.GetString("user_json", null);
            if (userJson == null) return null;
            return JsonConvert.DeserializeObject<User>(userJson);
        }
        public void ClearSession()
        {
            _prefs.Edit().Clear().Apply();
        }
    }
}