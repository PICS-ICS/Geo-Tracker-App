using Android.App;
using Android.Content;
using Android.OS;
using Android.Content.PM;
using AndroidX.Core.App;
using Microsoft.Maui.Devices.Sensors;
using GeoTrackerApp3.Models;
using GeoTrackerApp3.Services;

namespace GeoTrackerApp3.Platforms.Android
{
    [Service(ForegroundServiceType = ForegroundService.TypeLocation)]
    public class LocationForegroundService : Service
    {
        private Timer _timer;
        private string _token;
        private int _memberId;
        private int _companyId;

        private const int INTERVAL_MS = 10_000;

        public override IBinder OnBind(Intent intent) => null;

        public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
        {
            _token = intent?.GetStringExtra("token") ?? string.Empty;
            _memberId = intent?.GetIntExtra("memberId", 0) ?? 0;
            _companyId = intent?.GetIntExtra("companyId", 0) ?? 0;

            CreateNotification();
            _timer = new Timer(async _ => await SendLocationAsync(), null, 0, INTERVAL_MS);
            return StartCommandResult.Sticky;
        }

        private void CreateNotification()
        {
            var channelId = "location_channel";
            var channel = new NotificationChannel(channelId, "Location Tracking", NotificationImportance.Low);
            var manager = (NotificationManager)GetSystemService(NotificationService);
            manager.CreateNotificationChannel(channel);

            var notification = new NotificationCompat.Builder(this, channelId)
                .SetContentTitle("ICS Flow Active")
                .SetContentText("Tracking location in background")
                .SetOngoing(true)
                .Build();

            StartForeground(1, notification);
        }

        private async Task SendLocationAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(_token))
                    return;

                var location = await Geolocation.GetLocationAsync(
                    new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(5)));

                if (location == null)
                    return;

                var data = new LocationData
                {
                    memberID = _memberId,
                    companyID = _companyId,
                    lat = location.Latitude,
                    lon = location.Longitude,
                    pingTime = DateTime.UtcNow,
                    ipAddress = "Unknown",
                    deviceOS = DeviceInfo.Platform.ToString(),
                    deviceModel = DeviceInfo.Model
                };

                var result = await ApiService.SendLocationAsync(data, _token);

                if (!result.IsSuccess)
                    System.Diagnostics.Debug.WriteLine($"[BG Location] API error: {result.ErrorMessage}");
                else
                    System.Diagnostics.Debug.WriteLine($"[BG Location] Sent: {location.Latitude},{location.Longitude}");
            }
            catch (Exception ex)
            {
                ErrorDisplayService.ShowError("Background Location", ex);
            }
        }

        public override void OnDestroy()
        {
            _timer?.Dispose();
            base.OnDestroy();
        }
    }
}
