using Android.App;
using Android.Content;
using Android.Gms.Extensions;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using Firebase.Auth;
using Firebase.Firestore;
using Java.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TimeToSchool.BusinessLogic;
using static TimeToSchool.Service.FireBaseHelper;

namespace TimeToSchool.Service
{
    public class UsersRepository 
    {
        public static IListenerRegistration Registration;
        public static FirestoreEventListener FirestoreEventListener;


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
                userMap.Put("Status", user.Status ?? "pending");


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

                var snap = (DocumentSnapshot)userObject;
                newuser = new Model.User()
                {
                    Id = userId,
                    FirstName = snap.Get("FirstName").ToString(),
                    LastName = snap.Get("LastName").ToString(),
                    UserEmail = snap.Get("UserEmail").ToString(),
                    UserMobile = snap.Get("UserMobile").ToString(),
                    UserPass = snap.Get("UserPassword").ToString(),
                    IsAdmin = bool.Parse(snap.Get("IsAdmin").ToString()),
                    Status = snap.Get("Status")?.ToString() ?? "approved"
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
                            IsAdmin = bool.Parse(item.Get("IsAdmin").ToString()),
                            Status = item.Get("Status")?.ToString() ?? "approved"
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
                await userRef.Update("IsAdmin", user.IsAdmin);

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
        public static async Task BanUser(Model.User userToBan, string adminEmail)
        {
            try
            {
                string email = userToBan.UserEmail;

                // Delete the auth account + Firestore user doc using existing reauth flow.
                await Delete(userToBan);

                // Then record the email so they cannot register again.
                await BannedEmailsRepository.AddBannedEmail(new Model.BannedEmail
                {
                    Email = email,
                    BannedAt = DateTime.UtcNow,
                    BannedByEmail = adminEmail
                });

                Log.Debug(ProManager.TAG, $"BanUser: {email} banned by {adminEmail}");
            }
            catch (Exception ex)
            {
                Log.Error(ProManager.TAG, $"BanUser failed: {ex.Message}");
                throw new Exception("BanUser failed");
            }
        }
        public static async Task UpdateUserStatus(string userId, string status)
        {
            try
            {
                DocumentReference userRef = FirebaseFirestore.Instance
                                            .Collection("users").Document(userId);
                await userRef.Update("Status", status);
                Log.Debug(ProManager.TAG, $"UpdateUserStatus: {userId} -> {status}");
            }
            catch (Exception ex)
            {
                Log.Error(ProManager.TAG, $"UpdateUserStatus failed: {ex.Message}");
                throw new Exception("UpdateUserStatus failed");
            }
        }
        public static async Task<int> MarkAllUsersAsApproved()
        {
            int updated = 0;
            try
            {
                var documents = await FirebaseFirestore.Instance.Collection("users").Get();
                var snapshot = (QuerySnapshot)documents;
                if (snapshot == null || snapshot.IsEmpty) return 0;

                foreach (DocumentSnapshot item in snapshot.Documents)
                {
                    await item.Reference.Update("Status", "approved");
                    updated++;
                }
                Log.Debug(ProManager.TAG, $"MarkAllUsersAsApproved: updated {updated} user(s)");
                return updated;
            }
            catch (Exception ex)
            {
                Log.Error(ProManager.TAG, $"MarkAllUsersAsApproved failed: {ex.Message}");
                throw new Exception("MarkAllUsersAsApproved failed");
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
    }
}