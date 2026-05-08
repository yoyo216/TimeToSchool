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

        public DirectionsApiService(string apiKey) { _apiKey = apiKey; }

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
                var routes = root["routes"] as JArray;
                if (routes == null || routes.Count == 0) return null;
                int seconds = (int)routes[0]["legs"][0]["duration"]["value"];
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
