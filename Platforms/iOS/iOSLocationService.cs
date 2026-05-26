using CoreLocation;
using Foundation;
using UIKit;
using GeoTrackerApp3.Models;
using GeoTrackerApp3.Services;
using System.Diagnostics;

namespace GeoTrackerApp3.Platforms.iOS;

public class iOSLocationService
{
    private static iOSLocationService? _instance;

    private CLLocationManager? _locationManager;
    private string _token = string.Empty;
    private int _memberId;
    private int _companyId;
    private DateTime _lastSendTime = DateTime.MinValue;
    private CLLocation? _lastKnownLocation;
    private NSTimer? _pingTimer;
    private static readonly TimeSpan MIN_SEND_INTERVAL = TimeSpan.FromSeconds(10);

    public void Start(string token, int memberId, int companyId)
    {
        _token = token;
        _memberId = memberId;
        _companyId = companyId;
        _instance = this;

        // Persist tracking state so we can resume after app relaunch
        Preferences.Set("ios_tracking_active", true);

        _locationManager = new CLLocationManager
        {
            DesiredAccuracy = CLLocation.AccuracyBest,
            AllowsBackgroundLocationUpdates = true,
            PausesLocationUpdatesAutomatically = false,
            DistanceFilter = CLLocationDistance.FilterNone
        };

        _locationManager.LocationsUpdated += OnLocationsUpdated;
        _locationManager.Failed += OnLocationManagerFailed;
        _locationManager.StartUpdatingLocation();

        // Also start significant location change monitoring
        // This allows iOS to relaunch the app after termination when the device moves ~500m
        _locationManager.StartMonitoringSignificantLocationChanges();

        // Use NSTimer (runs on main thread) to periodically send last known location
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _pingTimer = NSTimer.CreateRepeatingScheduledTimer(10.0, timer => OnPingTimerElapsed());
        });

        Debug.WriteLine("[iOS Location] Service started with significant location monitoring");
    }

    public void Stop()
    {
        Preferences.Set("ios_tracking_active", false);
        _instance = null;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            _pingTimer?.Invalidate();
            _pingTimer?.Dispose();
            _pingTimer = null;
        });

        if (_locationManager != null)
        {
            _locationManager.StopMonitoringSignificantLocationChanges();
            _locationManager.LocationsUpdated -= OnLocationsUpdated;
            _locationManager.Failed -= OnLocationManagerFailed;
            _locationManager.StopUpdatingLocation();
            _locationManager.Dispose();
            _locationManager = null;
        }

        _lastKnownLocation = null;
        Debug.WriteLine("[iOS Location] Service stopped");
    }

    /// <summary>
    /// Called from AppDelegate when the app is relaunched due to a significant location change.
    /// Resumes tracking using persisted credentials.
    /// </summary>
    public static async Task ResumeIfNeededAsync()
    {
        if (!Preferences.Get("ios_tracking_active", false))
            return;

        // Already running
        if (_instance != null)
            return;

        try
        {
            var token = await SecureStorage.GetAsync("api_token") ?? string.Empty;
            var memberIdStr = await SecureStorage.GetAsync("logged_in_memberID") ?? "0";
            var companyIdStr = await SecureStorage.GetAsync("logged_in_companyID") ?? "0";

            if (string.IsNullOrEmpty(token))
            {
                Debug.WriteLine("[iOS Location] Cannot resume - no token");
                return;
            }

            var service = new iOSLocationService();
            service.Start(token, Convert.ToInt32(memberIdStr), Convert.ToInt32(companyIdStr));

            Debug.WriteLine("[iOS Location] Resumed tracking after app relaunch");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[iOS Location] Resume error: {ex.Message}");
        }
    }

    private void OnPingTimerElapsed()
    {
        // If no location update has been sent recently, send the last known location
        if (_lastKnownLocation != null && DateTime.UtcNow - _lastSendTime >= MIN_SEND_INTERVAL)
        {
            _ = SendLocationToApiAsync(_lastKnownLocation);
        }
    }

    private void OnLocationManagerFailed(object? sender, NSErrorEventArgs e)
    {
        Debug.WriteLine($"[iOS Location] Manager failed: {e.Error?.LocalizedDescription}");
    }

    private async void OnLocationsUpdated(object? sender, CLLocationsUpdatedEventArgs e)
    {
        try
        {
            var location = e.Locations[^1]; // Latest location
            if (location == null)
                return;

            _lastKnownLocation = location;

            // Throttle sends to avoid flooding the API
            if (DateTime.UtcNow - _lastSendTime < MIN_SEND_INTERVAL)
                return;

            await SendLocationToApiAsync(location);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[iOS Location] Error: {ex.Message}");
        }
    }

    private async Task SendLocationToApiAsync(CLLocation location)
    {
        try
        {
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

            // Check if we're online
            if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
            {
                // Try to send, queue if it fails
                var result = await ApiService.SendLocationAsync(data, _token);

                if (!result.IsSuccess)
                {
                    Debug.WriteLine($"[iOS Location] API error, queuing: {result.ErrorMessage}");
                    await LocationQueueService.EnqueueAsync(data);
                }
                else
                {
                    Debug.WriteLine($"[iOS Location] Sent: {data.lat},{data.lon}");

                    // Also try to sync any previously queued offline points
                    _ = LocationQueueService.SyncAsync(_token);
                }
            }
            else
            {
                // Offline — queue for later sync
                Debug.WriteLine($"[iOS Location] Offline, queuing: {data.lat},{data.lon}");
                await LocationQueueService.EnqueueAsync(data);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[iOS Location] Send error: {ex.Message}");
        }
    }
}
