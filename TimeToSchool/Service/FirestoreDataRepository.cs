using Android.Gms.Extensions;
using Android.Util; // For logging
using AndroidX.ConstraintLayout.Core.Motion.Utils;
using Firebase.Firestore;
using Org.Apache.Http.Impl.Conn;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static Android.Provider.Telephony.Mms;

namespace TimeToSchool
{
    public interface IDataRepository
    {
        Task<List<string>> GetSchoolsAsync();
        Task<List<string>> GetTownsForSchoolAsync(string schoolName);
        Task<List<string>> GetBusesForRouteAsync(string schoolName, string townName);
    }


    public class FirestoreDataRepository : IDataRepository
    {
        // Property to safely fetch the instance only when a call is actually made.
        // This prevents the "IllegalStateException" if the app is still warming up.
        private FirebaseFirestore DB
        {
            get
            {
                try
                {
                    return FirebaseFirestore.Instance;
                }
                catch (Exception ex)
                {
                    Log.Error("TimeToSchool", "Critical: Could not get Firestore Instance. " + ex.Message);
                    return null;
                }
            }
        }

        private const string CollectionName = "BusRoutes";

        public FirestoreDataRepository()
        {
            // Constructor is kept empty to prevent early-access crashes.
        }

        public async Task<List<string>> GetSchoolsAsync()
        {
            try
            {
                if (DB == null) return new List<string>();

                var snapshot = await DB.Collection(CollectionName).Get()
                    .AsAsync<QuerySnapshot>().ConfigureAwait(false);

                if (snapshot == null) return new List<string>();

                return snapshot.Documents
                    .Select(doc => doc.Get("School")?.ToString())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Distinct()
                    .OrderBy(s => s)
                    .ToList();
            }
            catch (Exception ex)
            {
                Log.Error("TimeToSchool", $"GetSchoolsAsync Error: {ex.Message}");
                return new List<string>();
            }
        }

        public async Task<List<string>> GetTownsForSchoolAsync(string schoolName)
        {
            try
            {
                if (DB == null || string.IsNullOrEmpty(schoolName)) return new List<string>();

                var query = DB.Collection(CollectionName).WhereEqualTo("School", schoolName);
                var snapshot = await query.Get()
                    .AsAsync<QuerySnapshot>().ConfigureAwait(false);

                return snapshot.Documents
                    .Select(doc => doc.Get("Town")?.ToString())
                    .Where(t => !string.IsNullOrEmpty(t))
                    .Distinct()
                    .OrderBy(t => t)
                    .ToList();
            }
            catch (Exception ex)
            {
                Log.Error("TimeToSchool", $"GetTowns Error: {ex.Message}");
                return new List<string>();
            }
        }

        public async Task<List<string>> GetBusesForRouteAsync(string school, string town)
        {
            try
            {
                if (DB == null || string.IsNullOrEmpty(school) || string.IsNullOrEmpty(town))
                    return new List<string>();

                var query = DB.Collection(CollectionName)
                               .WhereEqualTo("School", school)
                               .WhereEqualTo("Town", town);

                var snapshot = await query.Get()
                    .AsAsync<QuerySnapshot>().ConfigureAwait(false);

                var buses = snapshot.Documents
                    .Select(doc => doc.Get("BusLine")?.ToString())
                    .Where(b => !string.IsNullOrEmpty(b))
                    .OrderBy(b => b)
                    .ToList();

                // Add default option if results exist
                if (buses.Count > 0)
                {
                    buses.Insert(0, "Any Available Bus");
                }

                return buses;
            }
            catch (Exception ex)
            {
                Log.Error("TimeToSchool", $"GetBuses Error: {ex.Message}");
                return new List<string>();
            }
        }

    }
}