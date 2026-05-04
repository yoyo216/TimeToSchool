using Android.Gms.Extensions;
using Android.Util;
using Firebase.Firestore;
using Java.Util;
using System;
using System.Threading.Tasks;
using TimeToSchool.BusinessLogic;
using TimeToSchool.Model;

namespace TimeToSchool.Service
{
    public static class BannedEmailsRepository
    {
        private const string Collection = "bannedEmails";

        public static string Normalize(string email) =>
            (email ?? string.Empty).Trim().ToLowerInvariant();

        public static async Task<bool> IsEmailBanned(string email)
        {
            try
            {
                var docRef = FirebaseFirestore.Instance
                    .Collection(Collection)
                    .Document(Normalize(email));
                var snap = (DocumentSnapshot)await docRef.Get();
                return snap != null && snap.Exists();
            }
            catch (Exception ex)
            {
                Log.Error(ProManager.TAG, $"IsEmailBanned failed: {ex.Message}");
                return false;
            }
        }

        public static async Task AddBannedEmail(BannedEmail entry)
        {
            try
            {
                var map = new HashMap();
                map.Put("Email", entry.Email);
                map.Put("BannedAt", entry.BannedAt.ToString("o"));
                map.Put("BannedByEmail", entry.BannedByEmail ?? string.Empty);

                var docRef = FirebaseFirestore.Instance
                    .Collection(Collection)
                    .Document(Normalize(entry.Email));
                await docRef.Set(map);
                Log.Debug(ProManager.TAG, $"AddBannedEmail: {entry.Email}");
            }
            catch (Exception ex)
            {
                Log.Error(ProManager.TAG, $"AddBannedEmail failed: {ex.Message}");
                throw new Exception("AddBannedEmail failed");
            }
        }

        public static async Task RemoveBannedEmail(string email)
        {
            try
            {
                await FirebaseFirestore.Instance
                    .Collection(Collection)
                    .Document(Normalize(email))
                    .Delete();
                Log.Debug(ProManager.TAG, $"RemoveBannedEmail: {email}");
            }
            catch (Exception ex)
            {
                Log.Error(ProManager.TAG, $"RemoveBannedEmail failed: {ex.Message}");
                throw new Exception("RemoveBannedEmail failed");
            }
        }
    }
}
