using Android.Content;
using Android.Content.PM;
using Java.Security;
using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using TimeToSchool.BusinessLogic;

namespace TimeToSchool.Service
{
    public class DirectionsApiService
    {
        private const string BaseUrl = "https://maps.googleapis.com/maps/api/directions/json";
        private readonly string _apiKey;
        private readonly HttpClient _http = new HttpClient();

        public DirectionsApiService(string apiKey, Context context = null)
        {
            _apiKey = apiKey;
            if (context == null) return;
            try
            {
#pragma warning disable CS0618
                var info = context.PackageManager.GetPackageInfo(
                    context.PackageName, PackageInfoFlags.Signatures);
                var sig = info.Signatures[0];
#pragma warning restore CS0618
                var md = MessageDigest.GetInstance("SHA1");
                md.Update(sig.ToByteArray());
                string sha1 = BitConverter.ToString(md.Digest()).Replace("-", "");
                _http.DefaultRequestHeaders.Add("X-Android-Package", context.PackageName);
                _http.DefaultRequestHeaders.Add("X-Android-Cert", sha1);
            }
            catch (Exception ex)
            {
                Android.Util.Log.Warn(ProManager.TAG, $"DirectionsApi: could not set Android headers: {ex.Message}");
            }
        }

        public async Task<int?> GetEtaMinutes(double originLat, double originLng, double destLat, double destLng)
        {
            try
            {
                string url = $"{BaseUrl}" +
                    $"?origin={originLat.ToString(CultureInfo.InvariantCulture)},{originLng.ToString(CultureInfo.InvariantCulture)}" +
                    $"&destination={destLat.ToString(CultureInfo.InvariantCulture)},{destLng.ToString(CultureInfo.InvariantCulture)}" +
                    $"&mode=driving&key={_apiKey}";
                string json = await _http.GetStringAsync(url);
                var root = JObject.Parse(json);
                string status = root["status"]?.ToString();
                var routes = root["routes"] as JArray;
                if (routes == null || routes.Count == 0)
                {
                    Android.Util.Log.Warn(ProManager.TAG, $"DirectionsApi: status={status} — no routes returned");
                    return null;
                }
                int seconds = (int)routes[0]["legs"][0]["duration"]["value"];
                Android.Util.Log.Debug(ProManager.TAG, $"DirectionsApi: status={status} ETA={seconds}s");
                return (int)Math.Round(seconds / 60.0);
            }
            catch (Exception ex)
            {
                Android.Util.Log.Error(ProManager.TAG, $"DirectionsApi: {ex.Message}");
                return null;
            }
        }
    }
}
