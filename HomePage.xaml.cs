using System;
using System.Net.Http.Headers;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Storage;
using GeoTrackerApp3.Services;
using System.Threading;
using System.Threading.Tasks;

namespace GeoTrackerApp3.Views
{
    public partial class HomePage : ContentPage
    {
        private readonly HttpClient _httpClient = new HttpClient();
        private string _username;
        private string _token;

        private bool _isTracking = false;
        private CancellationTokenSource _cts;

        public HomePage()
        {
            InitializeComponent();

            _username = Preferences.Get("Username", "Unknown");
            UserLabel.Text = $"Hello, {_username}!";

            try
            {
                _token = SecureStorage.GetAsync("api_token").Result;
                if (!string.IsNullOrEmpty(_token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", _token);
                }
            }
            catch
            {
                _token = string.Empty;
            }
        }

        private async void OnToggleTrackingClicked(object sender, EventArgs e)
        {
            if (!_isTracking)
            {
                // --- START TRACKING ---
                _isTracking = true;
                _cts = new CancellationTokenSource();

                ToggleTrackingButton.Text = "Stop Tracking";
                ToggleTrackingButton.BackgroundColor = Colors.Red;
                TrackingStatusFrame.IsVisible = true;

                await DisplayAlert("Tracking", "Live location tracking started.", "OK");

                _ = StartTrackingAsync(_cts.Token);
            }
            else
            {
                // --- STOP TRACKING ---
                _isTracking = false;
                _cts?.Cancel();

                ToggleTrackingButton.Text = "Start Tracking";
                ToggleTrackingButton.BackgroundColor = Color.FromArgb("#4CAF50");
                TrackingStatusFrame.IsVisible = false;

                await DisplayAlert("Tracking", "Live location tracking stopped.", "OK");
            }
        }

        private async Task StartTrackingAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await GetAndSendLocationAsync();

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        private async void OnRefreshClicked(object sender, EventArgs e)
        {
            await GetAndSendLocationAsync();
        }

        private async Task GetAndSendLocationAsync()
        {
            try
            {
                var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
                if (status != PermissionStatus.Granted)
                    status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();

                if (status != PermissionStatus.Granted)
                    return;

                var location = await Geolocation.GetLocationAsync(
                    new GeolocationRequest(GeolocationAccuracy.High, TimeSpan.FromSeconds(10)));

                if (location != null)
                {
                    LatitudeLabel.Text = $"Latitude: {location.Latitude}";
                    LongitudeLabel.Text = $"Longitude: {location.Longitude}";

                    string token = await SecureStorage.GetAsync("api_token") ?? string.Empty;
                    string memberID = await SecureStorage.GetAsync("logged_in_memberID") ?? string.Empty;
                    string companyID = await SecureStorage.GetAsync("logged_in_companyID") ?? string.Empty;

                    if (string.IsNullOrEmpty(token))
                    {
                        await DisplayAlert("Error", "Token not found. Please log in again.", "OK");
                        return;
                    }

                    var data = new Models.LocationData
                    {
                        memberID = Convert.ToInt32(memberID),
                        companyID = Convert.ToInt32(companyID),
                        latitude = location.Latitude,
                        longatude = location.Longitude,
                        pingTime = DateTime.Now
                    };

                    var result = await ApiService.SendLocationAsync(data, token);

                    if (!result.IsSuccess)
                    {
                        System.Diagnostics.Debug.WriteLine($"Location API returned error: {result.ErrorMessage}");
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Could not get location: {ex.Message}", "OK");
            }
        }

        private void OnLogoutClicked(object sender, EventArgs e)
        {
            try
            {
                SecureStorage.Remove("api_token");
            }
            catch { }

            Preferences.Remove("Username");

            Application.Current.MainPage = new NavigationPage(new LoginPage());
        }
    }
}
