using Microsoft.Maui.Devices.Sensors;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace GeoTrackerApp3.Services
{
    public class LocationService
    {
        private readonly HttpClient _httpClient;

        public LocationService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task TrackLocationAsync()
        {
            try
            {
                // Request permission first
                var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
                if (status != PermissionStatus.Granted)
                    status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();

                if (status != PermissionStatus.Granted)
                    return;

                // Get current GPS coordinates
                var location = await Geolocation.GetLocationAsync(
                    new GeolocationRequest(GeolocationAccuracy.High, TimeSpan.FromSeconds(10)));

                if (location != null)
                {
                    var data = new
                    {
                        Latitude = location.Latitude,
                        Longitude = location.Longitude,
                        Timestamp = DateTime.UtcNow
                    };

                    // Send to your API endpoint
                    await _httpClient.PostAsJsonAsync("https://your-api-endpoint.com/location", data);
                }
            }
            catch (FeatureNotSupportedException)
            {
                // Location not supported on this device
            }
            catch (FeatureNotEnabledException)
            {
                // GPS disabled
            }
            catch (PermissionException)
            {
                // Permission denied
            }
            catch (Exception)
            {
                // Any other errors (network, etc.)
            }
        }
    }
}
