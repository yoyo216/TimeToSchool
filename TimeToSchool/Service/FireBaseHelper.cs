using Android.App;
using Android.Content.Res;
using Android.Gms.Extensions;
using Android.Runtime;
using Android.Util;
using Firebase;
using Firebase.Auth;
using Firebase.Firestore;
using Java.Util;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TimeToSchool.BusinessLogic;

namespace TimeToSchool.Service
{
    public class FireBaseHelper
    {
        public static IListenerRegistration Registration;
        public static FirestoreEventListener FirestoreEventListener;
        protected static FireBaseHelper me;
        private FirebaseApp app;
        static FireBaseHelper() { me = new FireBaseHelper(); }

        protected FireBaseHelper() { InitializeFirebase(); }

        //Initialize Firebase app
        private void InitializeFirebase()
        {
            try
            {
                //1.
                //Parse Firebase json file:
                //Install Newtonsoft.Json NuGet latest version
                //Rename json file google-services.json to googleservices.json 
                //Place json file google-services.json into Root/Assets
                //Set its Build Action in Property to "AndroidAsset"	

                string json;
                string projectId = "";
                string apiKey = "";
                string storageBucket = "";
                AssetManager assets = Application.Context.Assets;
                using (Stream stream = assets.Open("googleservices.json")) //Correct way to access raw resource
                {
                    // Reading from app data directory
                    using (StreamReader r = new StreamReader(stream))
                    {
                        json = r.ReadToEnd();

                        //using Newtonsoft.Json.Linq;
                        //JObject.Parse(json) parses the JSON string into a JObject, making it easy to navigate the JSON structure.
                        //JToken is used to access the individual elements within the JSON.
                        JObject jsonObj = JObject.Parse(json);
                        JToken projectInfo = jsonObj["project_info"];

                        if (projectInfo != null)
                        {
                            projectId = (string)projectInfo["project_id"];
                            storageBucket = (string)projectInfo["storage_bucket"];
                        }
                        else
                        {
                            Log.Error(ProManager.TAG, "project_info is null");
                            return; //Exit, as we cannot continue without project_info
                        }

                        JToken client = jsonObj["client"][0]; // Access the client array
                        apiKey = (string)client["api_key"][0]["current_key"];
                    }
                }

                //2. Initilize Firebase App
                app = FirebaseApp.InitializeApp(Application.Context); //using Firebase;
                if (app == null)
                {
                    var options = new FirebaseOptions.Builder()
                    .SetProjectId(projectId)
                    .SetApplicationId(projectId)
                    .SetApiKey(apiKey)
                    .SetDatabaseUrl(projectId + ".firebaseapp.com")
                    .SetStorageBucket(storageBucket)
                    .Build();

                    app = FirebaseApp.InitializeApp(Application.Context, options);
                }
            }
            catch (FileNotFoundException ex)
            {
                Android.Util.Log.Error(ProManager.TAG, $"File not found: {ex.Message}");
            }
            catch (System.Exception ex)
            {
                Android.Util.Log.Error(ProManager.TAG, $"Error parsing JSON: {ex.Message}");
            }
        }

        #region Users
        public static async Task<string> SignInUserAsync(string uemail, string upass)
        {
            try
            {
                FirebaseAuth mAuth = FirebaseAuth.Instance;
                //using Android.Gms.Extensions;
                await mAuth.SignInWithEmailAndPassword(uemail, upass);
                Log.Debug(ProManager.TAG, $"MyApp: User Auth {uemail} SignIn success");
                return mAuth.CurrentUser.Uid; // Indicate success
            }
            catch (FirebaseAuthException ex)
            {
                Log.Error(ProManager.TAG, $"SignInUserAsync: User Auth SignIn failed: {ex.Message}");
                return null; // Indicate failure
            }
            catch (System.Exception ex)
            {
                Log.Error(ProManager.TAG, $"SignInUserAsync: User Auth SignIn failed, general error: {ex.Message}");
                return null; // Indicate failure
            }
        }
        public static async Task<string> InsertAsync(Model.User user)
        {
            try
            {
                //Add user account to Firebase Auth Module
                user.Id = await RegisterUserForAuth(user);
                await AddUserToFirestore(user);
                return user.Id;
            }
            catch (Exception ex)
            {
                Log.Error(ProManager.TAG, $"Insert user failed: {ex.Message}");
                throw new Exception("Insert user failed");
            }
        }
        public static async Task<string> RegisterUserForAuth(Model.User user)
        {
            //Add user account to Firebase Auth Module
            try
            {
                FirebaseAuth mAuth = FirebaseAuth.Instance;
                //using Android.Gms.Extensions;
                await mAuth.CreateUserWithEmailAndPasswordAsync(user.UserEmail, user.UserPass);
                Log.Debug(ProManager.TAG, $"RegisterUserForAuth: User Auth {user.UserEmail} SignIn success");

                return mAuth?.CurrentUser.Uid;
            }
            catch (FirebaseAuthException ex)
            {
                Log.Error(ProManager.TAG, $"RegisterUserForAuth: {ex.Message}");
                throw new Exception("RegisterUserForAuth Failed!");
            }
            catch (System.Exception ex)
            {
                Log.Error(ProManager.TAG, $"RegisterUserForAuth general error: {ex.Message}");
                throw new Exception("RegisterUserForAuth Failed!");
            }
        }
        public static async Task AddUserToFirestore(Model.User user)
        {
            try
            {
                //Insert user to FireStore database
                HashMap userMap = new HashMap(); //using Java.Util;
                userMap.Put("FirstName", user.FirstName);
                userMap.Put("IsAdmin", user.IsAdmin);
                userMap.Put("LastName", user.LastName);
                userMap.Put("UserEmail", user.UserEmail);
                userMap.Put("UserMobile", user.UserMobile);
                userMap.Put("UserPassword", user.UserPass);


                DocumentReference userReference = FirebaseFirestore.Instance
                                                                        .Collection("users")
                                                                        .Document(user.Id);
                await userReference.Set(userMap);
                Log.Debug(ProManager.TAG, $"Add User to Firestore complited");
            }
            catch (FirebaseFirestoreException ex)
            {
                Log.Error(ProManager.TAG, $"Add User to Firestore failed: {ex.Message}");
                throw new Exception("Add User to Firestore failed");
            }
            catch (System.Exception ex)
            {
                Log.Error(ProManager.TAG, $"Add User to Firestore failed: {ex.Message}");
                throw new Exception("Add User to Firestore failed");
            }
        }
        public static async Task<Model.User> GetUserById(string userId)
        {
            Model.User newuser = null;
            try
            {
                DocumentReference userRef = FirebaseFirestore.Instance
                .Collection("users")
                .Document(userId);

                var userObject = await userRef.Get();

                newuser = new Model.User()
                {
                    Id = userId,
                    FirstName = ((DocumentSnapshot)userObject).Get("FirstName").ToString(),
                    LastName = ((DocumentSnapshot)userObject).Get("LastName").ToString(),
                    UserEmail = ((DocumentSnapshot)userObject).Get("UserEmail").ToString(),
                    UserMobile = ((DocumentSnapshot)userObject).Get("UserMobile").ToString(),
                    UserPass = ((DocumentSnapshot)userObject).Get("UserPassword").ToString(),
                    IsAdmin = bool.Parse(((DocumentSnapshot)userObject).Get("IsAdmin").ToString())
                };
                Log.Debug(ProManager.TAG, $"GetUserById: Get User from Firestore DB success");
                return newuser;
            }
            catch (FirebaseFirestoreException ex)
            {
                Log.Debug(ProManager.TAG, $"GetUserByID: Get User from Firestore failed: {ex.Message}");
                return null; // Indicate failure
            }
            catch (System.Exception ex)
            {
                Log.Debug(ProManager.TAG, $"GetUserByID general error: {ex.Message}");
                return null;
            }
        }
        public static async Task<List<Model.User>> GetUsersCollection()
        {
            List<Model.User> users = new List<Model.User>();

            try
            {
                var documents = await FirebaseFirestore.Instance.Collection("users").Get();
                var FirestoreUsersCollection = (QuerySnapshot)documents;

                if (!FirestoreUsersCollection.IsEmpty)
                {
                    var usersCollection = FirestoreUsersCollection.Documents;
                    foreach (DocumentSnapshot item in usersCollection)
                    {
                        Model.User user = new Model.User()
                        {
                            Id = item.Id,
                            FirstName = item.Get("FirstName").ToString(),
                            LastName = item.Get("LastName").ToString(),
                            UserEmail = item.Get("UserEmail").ToString(),
                            UserMobile = item.Get("UserMobile").ToString(),
                            UserPass = item.Get("UserPassword").ToString(),
                            IsAdmin = bool.Parse(item.Get("IsAdmin").ToString())
                        };
                        users.Add(user);
                    }
                    Log.Debug(ProManager.TAG, $"GetUsersCollection: loaded successfully! " +
                                              $"Count: {users.Count}");
                }
                return users;
            }
            catch (FirebaseFirestoreException ex)
            {
                Log.Debug(ProManager.TAG, $"GetUsersCollection failed: {ex.Message}");
                return users; // Indicate failure
            }
            catch (System.Exception ex)
            {
                Log.Debug(ProManager.TAG, $"GetUsersCollection general error: {ex.Message}");
                return users;
            }
        }
        public static async Task UpdateUser(Model.User user)
        {
            try
            {
                DocumentReference userRef = FirebaseFirestore.Instance
                                            .Collection("users").Document(user.Id);

                await userRef.Update("FirstName", user.FirstName);
                await userRef.Update("LastName", user.LastName);
                await userRef.Update("UserMobile", user.UserMobile);

                Log.Debug(ProManager.TAG, $"FirebaseHelper: Update {user.UserEmail} success");
            }
            catch (System.Exception ex)
            {
                Log.Debug(ProManager.TAG, $"FirebaseHelper: Update {user.UserEmail} failed " + ex.Message);
                throw new Exception($"Update {user.UserEmail} failed");
            }
        }
        public static async Task Delete(Model.User userToDelete)
        {
            try
            {
                // Create the credential for the account being deleted
                AuthCredential credential = EmailAuthProvider.GetCredential(userToDelete.UserEmail, userToDelete.UserPass);

                // Reauthenticate the ACTIVE user session specifically
                await FirebaseAuth.Instance.SignInWithCredential(credential);

                // Now delete the Firestore data
                await FirebaseFirestore.Instance.Collection("users").Document(userToDelete.Id).Delete();

                // Now delete the Auth record
                await FirebaseAuth.Instance.CurrentUser.DeleteAsync();

                // Reauthenticate the ACTIVE user session to Current User CredentiaLS
                credential = EmailAuthProvider.GetCredential(ProManager.CurrentUser.UserEmail, ProManager.CurrentUser.UserPass);
                await FirebaseAuth.Instance.SignInWithCredential(credential);
            }
            catch (Exception ex)
            {
                Log.Debug(ProManager.TAG, $"Delete user failed! " + ex.Message);
                throw new Exception("Delete user failed!");
            }
        }
        public static void FetchUsersListener()
        {
            FirestoreEventListener = new FirestoreEventListener();
            Registration = FirebaseFirestore.Instance
                .Collection("users")
                .AddSnapshotListener(FirestoreEventListener);
        }
        public static void StopUsersListener()
        {
            Registration?.Remove();
            Registration = null;
            FirestoreEventListener = null;
        }
        #endregion

        #region bus location
        public static async Task UpdateBusLocation(string busId, double lat, double lng)
        {
            try
            {
                var docRef = FirebaseFirestore.Instance.Collection("BusLocations").Document(busId);

                // This is the object that Firestore recognizes as a 'geopoint'
                GeoPoint busLocation = new GeoPoint(lat, lng);

                var data = new JavaDictionary<string, object>
        {
            // Use the object here, not the individual doubles!
            { "location", busLocation },
            { "timestamp", FieldValue.ServerTimestamp() }
        };

                // In Xamarin, you often need .AsAsync() to properly await Java tasks
                await docRef.Set(data).AsAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Firestore Error: " + ex.Message);
            }
        }

        #endregion
    }
    public class FirestoreEventListener : Java.Lang.Object, Firebase.Firestore.IEventListener
    {
        public event EventHandler<TaskListenerEventArgs> getEvent;
        public class TaskListenerEventArgs : EventArgs
        {
            public Java.Lang.Object Result { get; set; }
        }
        public void OnEvent(Java.Lang.Object obj, FirebaseFirestoreException error)
        {
            getEvent?.Invoke(this, new TaskListenerEventArgs { Result = obj });
        }
    }
}