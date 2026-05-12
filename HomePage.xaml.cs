using GeoTrackerApp3.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Storage;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace GeoTrackerApp3.Views
{
    public partial class HomePage : ContentPage
    {
        private string _token;
        private bool _isTracking = false;
        private CancellationTokenSource _cts;

        // Cache frequently accessed values to reduce SecureStorage calls
        private string _cachedMemberId;
        private string _cachedCompanyId;
        private const int LOCATION_UPDATE_INTERVAL_MS = 10000;

        public HomePage()
        {
            InitializeComponent();
            LoadCachedDataAsync();
        }

        private async void LoadCachedDataAsync()
        {
            try
            {
                _token = await SecureStorage.GetAsync("api_token") ?? string.Empty;
                _cachedMemberId = await SecureStorage.GetAsync("logged_in_memberID") ?? "0";
                _cachedCompanyId = await SecureStorage.GetAsync("logged_in_companyID") ?? "0";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Cache load error: {ex.Message}");
                _token = string.Empty;
                _cachedMemberId = "0";
                _cachedCompanyId = "0";
            }
        }

        private async void OnToggleTrackingClicked(object sender, EventArgs e)
        {
            if (!_isTracking)
            {
                // Open face verification
                System.Diagnostics.Debug.WriteLine("HomePage: Opening face verification");
                
                var webViewPage = new WebViewTestPage();
                await Navigation.PushModalAsync(webViewPage);
                
                // Wait for result
                var result = await webViewPage.GetAuthResultAsync();
                
                if (result == null)
                {
                    System.Diagnostics.Debug.WriteLine("HomePage: Cancelled");
                    return;
                }

                if (!result.Match)
                {
                    await DisplayAlert("Failed", result.Message + ":" + result.Match.ToString()+ ":" + result.MemberID.ToString() ?? "Verification failed", "OK");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"HomePage: Verified MemberID={result.MemberID}");
                
                // --- START TRACKING ---
                _isTracking = true;
                _cts = new CancellationTokenSource();

                ToggleTrackingButton.Text = "Stop Tracking";
                ToggleTrackingButton.BackgroundColor = Colors.Red;

                await TrackingStatusFrame.FadeTo(1, 300, Easing.Linear);

                _ = StartTrackingAsync(_cts.Token);
            }
            else
            {
                // --- STOP TRACKING ---
                _isTracking = false;
                _cts?.Cancel();
                _cts?.Dispose();
                _cts = null;

                ToggleTrackingButton.Text = "Start Tracking";
                ToggleTrackingButton.BackgroundColor = Color.FromArgb("#4CAF50");

                await TrackingStatusFrame.FadeTo(0, 300, Easing.Linear);
            }
        }


        private async Task StartTrackingAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await GetAndSendLocationAsync();

                try
                {
                    await Task.Delay(LOCATION_UPDATE_INTERVAL_MS, token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

       // private async void OnRefreshClicked(object sender, EventArgs e)
       // {
       //     await GetAndSendLocationAsync();
       // }

        private async Task GetAndSendLocationAsync()
        {
            try
            {
                var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
                if (status != PermissionStatus.Granted)
                    status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();

                if (status != PermissionStatus.Granted)
                    return;

                // Use Medium accuracy for better performance on low-end devices
                var location = await Geolocation.GetLocationAsync(
                    new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(5)));

                if (location != null)
                {
                    // Non-blocking UI update
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        LatitudeLabel.Text = $"Latitude: {location.Latitude}";
                        LongitudeLabel.Text = $"Longitude: {location.Longitude}";
                    });

                    // Use cached values instead of repeated SecureStorage calls
                    string token = _token;
                    if (string.IsNullOrEmpty(token))
                    {
                        token = await SecureStorage.GetAsync("api_token") ?? string.Empty;
                        _token = token;
                    }

                    if (string.IsNullOrEmpty(token))
                    {
                        await DisplayAlert("Error", "Token not found. Please log in again.", "OK");
                        return;
                    }

                    var data = new Models.LocationData
                    {
                        memberID = Convert.ToInt32(_cachedMemberId),
                        companyID = Convert.ToInt32(_cachedCompanyId),
                        lat = location.Latitude,
                        lon = location.Longitude,
                        pingTime = DateTime.UtcNow,
                        ipAddress = "Unknown", 
                        deviceOS = DeviceInfo.Platform.ToString(), 
                        deviceModel = DeviceInfo.Model
                    };

                    var result = await ApiService.SendLocationAsync(data, token);

                    if (!result.IsSuccess)
                    {
                        System.Diagnostics.Debug.WriteLine($"Location API error: {result.ErrorMessage}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Location error: {ex.Message}");
                // Don't show alert for every error to avoid interrupting user
            }
        }

        private void OnLogoutClicked(object sender, EventArgs e)
        {
            try
            {
                // Stop tracking and clean up resources
                if (_isTracking)
                {
                    _isTracking = false;
                    _cts?.Cancel();
                    _cts?.Dispose();
                    _cts = null;
                }

                SecureStorage.Remove("api_token");
            }
            catch { }

            Preferences.Remove("Username");

            Application.Current.MainPage = new NavigationPage(new LoginPage());
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            
            // Clean up tracking when page disappears
            if (_isTracking)
            {
                _cts?.Cancel();
            }
        }
    }
}
