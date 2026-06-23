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

    // Geofence burst mode state
    private bool _isBurstMode;
    private DateTime _burstModeStartTime;
    private NSTimer? _burstTimer;
    private static readonly TimeSpan BURST_DURATION = TimeSpan.FromMinutes(5);
    private static readonly double BURST_INTERVAL_SECONDS = 30.0;
    private static readonly double GEOFENCE_RADIUS_METERS = 500.0;
    private const int MAX_GEOFENCES = 20;

    // Foreground mode (app is open) - send every 5 minutes
    private NSTimer? _foregroundTimer;
    private static readonly double FOREGROUND_INTERVAL_SECONDS = 300.0; // 5 minutes

    private bool _isForegroundMode;

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
            PausesLocationUpdatesAutomatically = false
        };

        _locationManager.LocationsUpdated += OnLocationsUpdated;
        _locationManager.Failed += OnLocationManagerFailed;
        _locationManager.RegionEntered += OnRegionEntered;
        _locationManager.RegionLeft += OnRegionLeft;

        // Start location updates to get initial position
        _locationManager.StartUpdatingLocation();

        // Fetch geofences from the API and register them
        _ = LoadAndRegisterGeofencesAsync();

        // Start in foreground mode (app is open)
        StartForegroundMode();

        Debug.WriteLine("[iOS Location] Service started with geofence monitoring");
    }

    public void Stop()
    {
        Preferences.Set("ios_tracking_active", false);
        _instance = null;

        StopBurstMode();
        StopForegroundTimer();

        if (_locationManager != null)
        {
            // Remove all monitored regions
            foreach (var region in _locationManager.MonitoredRegions)
            {
                if (region is CLRegion clRegion)
                    _locationManager.StopMonitoring(clRegion);
            }

            _locationManager.RegionEntered -= OnRegionEntered;
            _locationManager.RegionLeft -= OnRegionLeft;
            _locationManager.LocationsUpdated -= OnLocationsUpdated;
            _locationManager.Failed -= OnLocationManagerFailed;
            _locationManager.StopUpdatingLocation();
            _locationManager.StopMonitoringSignificantLocationChanges();
            _locationManager.Dispose();
            _locationManager = null;
        }

        _lastKnownLocation = null;
        Debug.WriteLine("[iOS Location] Service stopped");
    }

    /// <summary>
    /// Called when the app enters the foreground - switches to 5-minute interval pings.
    /// </summary>
    public void StartForegroundMode()
    {
        _isForegroundMode = true;
        StopBurstMode();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            _foregroundTimer?.Invalidate();
            _foregroundTimer?.Dispose();
            _foregroundTimer = NSTimer.CreateRepeatingScheduledTimer(FOREGROUND_INTERVAL_SECONDS, timer => OnForegroundTimerElapsed());
        });

        // Stop continuous GPS updates while in foreground to save battery;
        // the timer will request location on demand.
        _locationManager?.StopUpdatingLocation();
        _locationManager?.StartMonitoringSignificantLocationChanges();

        Debug.WriteLine("[iOS Location] Foreground mode - pinging every 5 minutes");
    }

    /// <summary>
    /// Called when the app enters the background - relies on geofence triggers only.
    /// </summary>
    public void EnterBackgroundMode()
    {
        _isForegroundMode = false;
        StopForegroundTimer();

        // In background, we rely solely on geofence region monitoring.
        // If currently in burst mode, let it finish; otherwise stop continuous updates.
        if (!_isBurstMode)
        {
            _locationManager?.StopUpdatingLocation();
        }

        Debug.WriteLine("[iOS Location] Background mode - waiting for geofence triggers");
    }

    /// <summary>
    /// Called from AppDelegate when the app is relaunched due to a location event (geofence/significant change).
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
            // Launched in background, so go directly to background/geofence mode
            service.EnterBackgroundMode();
            // The geofence entry event that triggered the relaunch will fire burst mode

            Debug.WriteLine("[iOS Location] Resumed tracking after app relaunch");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[iOS Location] Resume error: {ex.Message}");
        }
    }

    /// <summary>
    /// Fetches geofence locations from the API and registers them with CLLocationManager.
    /// </summary>
    private async Task LoadAndRegisterGeofencesAsync()
    {
        try
        {
            double lat = _lastKnownLocation?.Coordinate.Latitude ?? 0;
            double lon = _lastKnownLocation?.Coordinate.Longitude ?? 0;
            var geofences = await ApiService.GetGeofencesAsync(_memberId, lat, lon, _token);

            if (geofences.Count == 0)
            {
                Debug.WriteLine("[iOS Geofence] No geofences returned from API, falling back to significant location monitoring");
                _locationManager?.StartMonitoringSignificantLocationChanges();
                return;
            }

            // Remove any previously monitored regions
            if (_locationManager != null)
            {
                foreach (var region in _locationManager.MonitoredRegions)
                {
                    if (region is CLRegion clRegion)
                        _locationManager.StopMonitoring(clRegion);
                }
            }

            // Register up to 20 geofence regions (iOS limit per app is 20)
            int count = 0;
            foreach (var fence in geofences.Take(MAX_GEOFENCES))
            {
                var center = new CLLocationCoordinate2D(fence.Lat, fence.Lon);
                var region = new CLCircularRegion(center, GEOFENCE_RADIUS_METERS, $"geofence_{count}")
                {
                    NotifyOnEntry = true,
                    NotifyOnExit = true
                };

                _locationManager?.StartMonitoring(region);
                count++;
                Debug.WriteLine($"[iOS Geofence] Registered region #{count}: ({fence.Lat:F6}, {fence.Lon:F6}) radius={GEOFENCE_RADIUS_METERS}m");
            }

            // Also keep significant location monitoring as a fallback
            _locationManager?.StartMonitoringSignificantLocationChanges();

            Debug.WriteLine($"[iOS Geofence] Total {count} geofences registered");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[iOS Geofence] Load error: {ex.Message}");
            // Fallback to significant location monitoring
            _locationManager?.StartMonitoringSignificantLocationChanges();
        }
    }

    /// <summary>
    /// Refreshes geofences from the API. Call periodically or after significant events.
    /// </summary>
    public async Task RefreshGeofencesAsync()
    {
        await LoadAndRegisterGeofencesAsync();
    }

    #region Region Monitoring Events

    private void OnRegionEntered(object? sender, CLRegionEventArgs e)
    {
        Debug.WriteLine($"[iOS Geofence] ENTERED region: {e.Region.Identifier}");
        StartBurstMode();
    }

    private void OnRegionLeft(object? sender, CLRegionEventArgs e)
    {
        Debug.WriteLine($"[iOS Geofence] LEFT region: {e.Region.Identifier}");
        StartBurstMode();
    }

    #endregion

    #region Burst Mode - sends location every 30s for 5 minutes

    private void StartBurstMode()
    {
        if (_isForegroundMode)
        {
            // In foreground mode we already send every 5 minutes; just send one immediate ping
            _ = SendCurrentLocationAsync();
            return;
        }

        _isBurstMode = true;
        _burstModeStartTime = DateTime.UtcNow;

        // Start GPS updates to get accurate location during burst
        _locationManager?.StartUpdatingLocation();

        // Send immediately
        _ = SendCurrentLocationAsync();

        // Set up repeating timer for 30-second pings
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _burstTimer?.Invalidate();
            _burstTimer?.Dispose();
            _burstTimer = NSTimer.CreateRepeatingScheduledTimer(BURST_INTERVAL_SECONDS, timer => OnBurstTimerElapsed());
        });

        Debug.WriteLine("[iOS Location] Burst mode STARTED - sending every 30s for 5 minutes");
    }

    private void StopBurstMode()
    {
        _isBurstMode = false;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            _burstTimer?.Invalidate();
            _burstTimer?.Dispose();
            _burstTimer = null;
        });

        // Stop continuous GPS to save battery; geofence monitoring continues passively
        if (!_isForegroundMode)
        {
            _locationManager?.StopUpdatingLocation();
        }

        Debug.WriteLine("[iOS Location] Burst mode STOPPED");
    }

    private void OnBurstTimerElapsed()
    {
        // Check if burst duration has expired
        if (DateTime.UtcNow - _burstModeStartTime >= BURST_DURATION)
        {
            StopBurstMode();
            return;
        }

        _ = SendCurrentLocationAsync();
    }

    #endregion

    #region Foreground Timer - sends every 5 minutes while app is open

    private void StopForegroundTimer()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _foregroundTimer?.Invalidate();
            _foregroundTimer?.Dispose();
            _foregroundTimer = null;
        });
    }

    private void OnForegroundTimerElapsed()
    {
        _ = SendCurrentLocationAsync();
    }

    #endregion

    #region Location Updates

    private void OnLocationManagerFailed(object? sender, NSErrorEventArgs e)
    {
        Debug.WriteLine($"[iOS Location] Manager failed: {e.Error?.LocalizedDescription}");
    }

    private void OnLocationsUpdated(object? sender, CLLocationsUpdatedEventArgs e)
    {
        try
        {
            var location = e.Locations[^1]; // Latest location
            if (location == null)
                return;

            _lastKnownLocation = location;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[iOS Location] LocationsUpdated error: {ex.Message}");
        }
    }

    private async Task SendCurrentLocationAsync()
    {
        try
        {
            CLLocation? location = _lastKnownLocation;

            // If no cached location, try to get one quickly
            if (location == null)
            {
                _locationManager?.StartUpdatingLocation();
                // Wait briefly for a location update
                await Task.Delay(2000);
                location = _lastKnownLocation;
            }

            if (location == null)
            {
                Debug.WriteLine("[iOS Location] No location available to send");
                return;
            }

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

            if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
            {
                var result = await ApiService.SendLocationAsync(data, _token);

                if (!result.IsSuccess)
                {
                    Debug.WriteLine($"[iOS Location] API error, queuing: {result.ErrorMessage}");
                    await LocationQueueService.EnqueueAsync(data);
                }
                else
                {
                    Debug.WriteLine($"[iOS Location] Sent: {data.lat:F6},{data.lon:F6}");
                    _ = LocationQueueService.SyncAsync(_token);
                }
            }
            else
            {
                Debug.WriteLine($"[iOS Location] Offline, queuing: {data.lat:F6},{data.lon:F6}");
                await LocationQueueService.EnqueueAsync(data);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[iOS Location] Send error: {ex.Message}");
        }
    }

    #endregion
}
