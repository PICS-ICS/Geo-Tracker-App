using Android.App;
using Android.Content;
using Android.OS;
using Android.Content.PM; // ✅ Needed for ForegroundServiceType
using AndroidX.Core.App;
using Microsoft.Maui.Devices.Sensors;
using System.Net.Http.Json;

namespace GeoTrackerApp3.Platforms.Android
{
    [Service(ForegroundServiceType = ForegroundService.TypeLocation)]
    public class LocationForegroundService : Service
    {
        private Timer _timer;
        private readonly HttpClient _httpClient = new();

        public override IBinder OnBind(Intent intent) => null;

        public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
        {
            CreateNotification();
            _timer = new Timer(async _ => await SendLocationAsync(), null, 0, 5 * 60 * 1000);
            return StartCommandResult.Sticky;
        }

        private void CreateNotification()
        {
            var channelId = "location_channel";
            var channel = new NotificationChannel(channelId, "Location Tracking", NotificationImportance.Default);
            var manager = (NotificationManager)GetSystemService(NotificationService);
            manager.CreateNotificationChannel(channel);

            var notification = new NotificationCompat.Builder(this, channelId)
                .SetContentTitle("GeoTracker Active")
                .SetContentText("Tracking location in background")
                .SetSmallIcon(Resource.Mipmap.appicon)
                .Build();

            StartForeground(1, notification);
        }

        private async Task SendLocationAsync()
        {
            try
            {
                var status = await Permissions.CheckStatusAsync<Permissions.LocationAlways>();
                if (status != PermissionStatus.Granted)
                    status = await Permissions.RequestAsync<Permissions.LocationAlways>();

                if (status != PermissionStatus.Granted)
                    return;

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

                    await _httpClient.PostAsJsonAsync("https://your-api-endpoint.com/location", data);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Background Location Error] {ex.Message}");
            }
        }

        public override void OnDestroy()
        {
            _timer?.Dispose();
            base.OnDestroy();
        }
    }
}
