using Android.App;
using Android.Content;
using Android.Gms.Extensions;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using Firebase.Firestore;
using Java.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TimeToSchool.BusinessLogic;
using TimeToSchool.Model;

namespace TimeToSchool.Service
{
    public class BusesRepository
    {
        public static IListenerRegistration BusRegistration;
        public static FirestoreEventListener BusEventListener;

        // Starts a real-time listener for the BusRoutes collection
        public static void FetchBusesListener()
        {
            BusEventListener = new FirestoreEventListener();
            BusRegistration = FirebaseFirestore.Instance
                .Collection("BusRoutes")
                .AddSnapshotListener(BusEventListener);
        }

        // Stops the bus listener to save resources
        public static void StopBusesListener()
        {
            BusRegistration?.Remove();
            BusRegistration = null;
            BusEventListener = null;
        }
        public static async Task AddBusRoute(BusRoute route)
        {
            try
            {
                FirebaseFirestore db = FirebaseFirestore.Instance;

                HashMap routeMap = new HashMap();
                routeMap.Put("School", route.School);
                routeMap.Put("Town", route.Town);
                routeMap.Put("BusLine", route.BusLine);

                // This creates the "BusRoutes" collection automatically
                await db.Collection("BusRoutes").Add(routeMap);
            }
            catch (Exception ex)
            {
                throw new Exception("Error saving bus route: " + ex.Message);
            }
        }
        public static async Task UpdateBusLocation(ActiveBus trip)
        {
            try
            {
                var db = FirebaseFirestore.Instance;
                await db.Collection("ActiveTrips")
                        .Document(trip.GetDocId())
                        .Set(trip.ToMap())
                        .AsAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Firestore Error: " + ex.Message);
            }
        }
        public static async Task<List<Model.BusRoute>> GetBusesCollection()
        {
            List<Model.BusRoute> routes = new List<Model.BusRoute>();

            try
            {
                var documents = await FirebaseFirestore.Instance.Collection("BusRoutes").Get();
                var FirestoreBusesCollection = (QuerySnapshot)documents;

                if (!FirestoreBusesCollection.IsEmpty)
                {
                    var BusesCollection = FirestoreBusesCollection.Documents;
                    foreach (DocumentSnapshot item in BusesCollection)
                    {
                        Model.BusRoute route = new Model.BusRoute()
                        {
                            Id = item.Id,
                            School = item.Get("School").ToString(),
                            Town = item.Get("Town").ToString(),
                            BusLine = item.Get("BusLine").ToString(),


                        };
                        routes.Add(route);
                    }
                    Log.Debug(ProManager.TAG, $"GetBusesCollection: loaded successfully! " +
                                              $"Count: {routes.Count}");
                }
                return routes;
            }
            catch (FirebaseFirestoreException ex)
            {
                Log.Debug(ProManager.TAG, $"GetBusessCollection failed: {ex.Message}");
                return routes; // Indicate failure
            }
            catch (System.Exception ex)
            {
                Log.Debug(ProManager.TAG, $"GetUsersCollection general error: {ex.Message}");
                return routes;
            }
        }
        // use to filiter buses
        public List<string> GetSchools(List<BusRoute> _allRoutes)
        {
            return _allRoutes.Select(r => r.School)
                             .Distinct()
                             .OrderBy(s => s)
                             .ToList();
        }

        public List<string> GetTownsForSchool(List<BusRoute> _allRoutes, string schoolName)
        {
            return _allRoutes.Where(r => r.School == schoolName)
                             .Select(r => r.Town)
                             .Distinct()
                             .OrderBy(t => t)
                             .ToList();
        }

        public List<string> GetBusesForRoute(List<BusRoute> _allRoutes, string school, string town)
        {
            var buses = _allRoutes.Where(r => r.School == school && r.Town == town)
                                  .Select(r => r.BusLine)
                                  .OrderBy(b => b)
                                  .ToList();

            if (buses.Count > 0)
            {
                buses.Insert(0, "Any Available Bus");
            }

            return buses;
        }
    }
}
