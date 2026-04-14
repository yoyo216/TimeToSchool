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
using TimeToSchool.Model;

namespace TimeToSchool.Service
{
    public class FireBaseHelper
    {

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
        public static void Initialize()
        {
            // empty function to initailzie firebasehelper as singletone object- connection to firebase server
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
}