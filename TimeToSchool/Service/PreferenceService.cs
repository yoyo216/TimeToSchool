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
using TimeToSchool;
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
        private const string DRIVER_CARDS_KEY = "driver_cards_json";

        public void SaveDriverCards(List<DriverCardState> cards)
        {
            var snapshot = cards.Select(c => new DriverCardState
            {
                Id = c.Id,
                FirebaseDocumentId = c.FirebaseDocumentId,
                TripData = c.TripData,
                IsDriving = false,
                SuppressVisibilityWarning = c.SuppressVisibilityWarning
            }).ToList();
            _prefs.Edit().PutString(DRIVER_CARDS_KEY, JsonConvert.SerializeObject(snapshot)).Apply();
        }

        public List<DriverCardState> GetDriverCards()
        {
            string json = _prefs.GetString(DRIVER_CARDS_KEY, null);
            if (json == null) return null;
            return JsonConvert.DeserializeObject<List<DriverCardState>>(json);
        }

        private const string LAST_SEARCH_SCHOOL = "last_search_school";
        private const string LAST_SEARCH_TOWN   = "last_search_town";
        private const string LAST_SEARCH_BUS    = "last_search_bus";

        public void SaveLastSearch(string school, string town, string busLine)
        {
            _prefs.Edit()
                .PutString(LAST_SEARCH_SCHOOL, school)
                .PutString(LAST_SEARCH_TOWN, town)
                .PutString(LAST_SEARCH_BUS, busLine)
                .Apply();
        }

        public (string school, string town, string busLine) GetLastSearch() =>
            (_prefs.GetString(LAST_SEARCH_SCHOOL, null),
             _prefs.GetString(LAST_SEARCH_TOWN,   null),
             _prefs.GetString(LAST_SEARCH_BUS,    null));

        public void ClearSession()
        {
            _prefs.Edit().Clear().Apply();
        }
    }
}