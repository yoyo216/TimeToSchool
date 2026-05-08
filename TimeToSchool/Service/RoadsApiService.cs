using Android.Gms.Maps.Model;
using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;

namespace TimeToSchool.Service
{
    public class RoadsApiService
    {
        private const string BaseUrl = "https://roads.googleapis.com/v1/snapToRoads";
        private readonly string _apiKey;
        private readonly HttpClient _http = new HttpClient();

        public RoadsApiService(string apiKey) { _apiKey = apiKey; }

        // Returns road-snapped LatLng, or null on any failure (caller falls back to raw GPS)
        public async Task<LatLng> SnapToRoad(double lat, double lng)
        {
            try
            {
                string url = $"{BaseUrl}?path={lat.ToString(CultureInfo.InvariantCulture)},{lng.ToString(CultureInfo.InvariantCulture)}&key={_apiKey}";
                string json = await _http.GetStringAsync(url);
                var root = JObject.Parse(json);
                var points = root["snappedPoints"] as JArray;
                if (points == null || points.Count == 0) return null;
                double sLat = (double)points[0]["location"]["latitude"];
                double sLng = (double)points[0]["location"]["longitude"];
                Android.Util.Log.Debug("RoadsApiService", $"Snapped ({lat:F5},{lng:F5}) → ({sLat:F5},{sLng:F5})");
                return new LatLng(sLat, sLng);
            }
            catch (Exception ex)
            {
                Android.Util.Log.Error("RoadsApiService", ex.Message);
                return null;
            }
        }
    }
}
