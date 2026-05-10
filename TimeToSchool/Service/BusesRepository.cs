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
using static TimeToSchool.Service.FireBaseHelper;

namespace TimeToSchool.Service
{
    public class BusesRepository
    {
        public static IListenerRegistration BusRegistration;
        public static FirestoreEventListener BusEventListener;

        public static IListenerRegistration ActiveTripsRegistration;
        public static FirestoreEventListener ActiveTripsEventListener;

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

        // Starts a real-time listener for today's ActiveTrips (both Active and Inactive)
        public static void FetchActiveBusesListenerForToday()
        {
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            ActiveTripsEventListener = new FirestoreEventListener();
            ActiveTripsRegistration = FirebaseFirestore.Instance
                .Collection("ActiveTrips")
                .WhereEqualTo("date", today)
                .AddSnapshotListener(ActiveTripsEventListener);
        }

        public static void StopActiveBusesListener()
        {
            ActiveTripsRegistration?.Remove();
            ActiveTripsRegistration = null;
            ActiveTripsEventListener = null;
        }
        public static async Task<bool> UpdateBus(BusRoute bus)
        {
            try
            {
                var busData = new Dictionary<string, Java.Lang.Object>
        {
            { "BusLine", bus.BusLine },
            { "School", bus.School },
            { "Town", bus.Town }
        };
                if (bus.FirstStopLat.HasValue)
                {
                    busData.Add("FirstStopLat", new Java.Lang.Double(bus.FirstStopLat.Value));
                    busData.Add("FirstStopLng", new Java.Lang.Double(bus.FirstStopLng.Value));
                }

                // 2. Reference the specific document by its ID and call Update
                await FirebaseFirestore.Instance
                    .Collection("BusRoutes") // Ensure this matches your collection name exactly
                    .Document(bus.Id)
                    .Update(busData);

                return true;
            }
            catch (Exception ex)
            {
                // Log the error for debugging
                Log.Error(ProManager.TAG, $"Error updating bus {bus.Id}: {ex.Message}");
                return false;
            }
        }
        public static async Task<bool> AddBusRoute(BusRoute route)
        {
            try
            {
                FirebaseFirestore db = FirebaseFirestore.Instance;

                HashMap routeMap = new HashMap();
                routeMap.Put("School", route.School);
                routeMap.Put("Town", route.Town);
                routeMap.Put("BusLine", route.BusLine);
                if (route.FirstStopLat.HasValue)
                {
                    routeMap.Put("FirstStopLat", new Java.Lang.Double(route.FirstStopLat.Value));
                    routeMap.Put("FirstStopLng", new Java.Lang.Double(route.FirstStopLng.Value));
                }

                // This creates the "BusRoutes" collection automatically
                await db.Collection("BusRoutes").Add(routeMap);
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception("Error saving bus route: " + ex.Message);
                return false;
            }
        }
        public static async Task UpdateBusLocation(ActiveBus trip)
        {
            try
            {
                var db = FirebaseFirestore.Instance;
                await db.Collection("ActiveTrips")
                        .Document(trip.GetDocId())
                        .Set(trip.ToMap(), SetOptions.Merge())
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
                            FirstStopLat = TryGetDouble(item, "FirstStopLat"),
                            FirstStopLng = TryGetDouble(item, "FirstStopLng"),
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
        public static async Task<bool> DeleteBus(string busId)
        {
            try
            {
                // 1. Reference the specific bus by its unique ID
                // 2. Call DeleteAsync to remove it from the database
                await FirebaseFirestore.Instance
                    .Collection("BusRoutes")
                    .Document(busId)
                    .Delete();

                return true;
            }
            catch (Exception ex)
            {
                // Log the error so you can see it in the Output window 
                Android.Util.Log.Error(ProManager.TAG, "Error deleting bus: " + ex.Message);
                return false;
            }
        }
        public static async Task<BusRoute> GetBusRouteById(string id)
        {
            try
            {
                var snap = (DocumentSnapshot)await FirebaseFirestore.Instance
                    .Collection("BusRoutes").Document(id).Get();
                if (!snap.Exists()) return null;
                return new BusRoute
                {
                    Id           = snap.Id,
                    School       = snap.Get("School").ToString(),
                    Town         = snap.Get("Town").ToString(),
                    BusLine      = snap.Get("BusLine").ToString(),
                    FirstStopLat = TryGetDouble(snap, "FirstStopLat"),
                    FirstStopLng = TryGetDouble(snap, "FirstStopLng"),
                };
            }
            catch (Exception ex)
            {
                Log.Error(ProManager.TAG, $"GetBusRouteById failed: {ex.Message}");
                return null;
            }
        }

        private static double? TryGetDouble(DocumentSnapshot snapshot, string field)
        {
            var obj = snapshot.Get(field);
            if (obj == null) return null;
            if (double.TryParse(obj.ToString(), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double val))
                return val;
            return null;
        }

        public static List<string> GetSchools(List<BusRoute> routes) =>
            routes.Select(r => r.School).Distinct().OrderBy(s => s).ToList();

        public static List<string> GetTownsForSchool(List<BusRoute> routes, string schoolName) =>
            routes.Where(r => r.School == schoolName).Select(r => r.Town).Distinct().OrderBy(t => t).ToList();

        public static List<string> GetBusesForRoute(List<BusRoute> routes, string school, string town)
        {
            var buses = routes.Where(r => r.School == school && r.Town == town)
                              .Select(r => r.BusLine).OrderBy(b => b).ToList();
            if (buses.Count > 0)
                buses.Insert(0, "Any Available Bus");
            return buses;
        }
    }
}
