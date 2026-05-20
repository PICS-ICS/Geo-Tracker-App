using CoreLocation;
using Foundation;
using UIKit;
using GeoTrackerApp3.Models;
using GeoTrackerApp3.Services;
using System.Diagnostics;

namespace GeoTrackerApp3.Platforms.iOS;

public class iOSLocationService
{
    private CLLocationManager? _locationManager;
    private string _token = string.Empty;
    private int _memberId;
    private int _companyId;
    private DateTime _lastSendTime = DateTime.MinValue;
    private static readonly TimeSpan MIN_SEND_INTERVAL = TimeSpan.FromSeconds(10);

    public void Start(string token, int memberId, int companyId)
    {
        _token = token;
        _memberId = memberId;
        _companyId = companyId;

        _locationManager = new CLLocationManager
        {
            DesiredAccuracy = CLLocation.AccuracyHundredMeters,
            AllowsBackgroundLocationUpdates = true,
            PausesLocationUpdatesAutomatically = false,
            DistanceFilter = 10 // meters
        };

        _locationManager.LocationsUpdated += OnLocationsUpdated;
        _locationManager.StartUpdatingLocation();

        Debug.WriteLine("[iOS Location] Service started");
    }

    public void Stop()
    {
        if (_locationManager != null)
        {
            _locationManager.LocationsUpdated -= OnLocationsUpdated;
            _locationManager.StopUpdatingLocation();
            _locationManager.Dispose();
            _locationManager = null;
        }

        Debug.WriteLine("[iOS Location] Service stopped");
    }

    private async void OnLocationsUpdated(object? sender, CLLocationsUpdatedEventArgs e)
    {
        try
        {
            // Throttle sends to avoid flooding the API
            if (DateTime.UtcNow - _lastSendTime < MIN_SEND_INTERVAL)
                return;

            var location = e.Locations[^1]; // Latest location
            if (location == null)
                return;

            _lastSendTime = DateTime.UtcNow;

            var data = new LocationData
            {
                memberID = _memberId,
                companyID = _companyId,
                lat = location.Coordinate.Latitude,
                lon = location.Coordinate.Longitude,
                pingTime = DateTime.UtcNow,
                ipAddress = "Unknown",
                deviceOS = "iOS",
                deviceModel = UIDevice.CurrentDevice.Model
            };

            var result = await ApiService.SendLocationAsync(data, _token);

            if (!result.IsSuccess)
                Debug.WriteLine($"[iOS Location] API error: {result.ErrorMessage}");
            else
                Debug.WriteLine($"[iOS Location] Sent: {data.lat},{data.lon}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[iOS Location] Error: {ex.Message}");
        }
    }
}
