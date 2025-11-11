using System;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Storage;
using GeoTrackerApp3.Services;
using System.ComponentModel.Design;

namespace GeoTrackerApp3.Views
{
    public partial class HomePage : ContentPage
    {
        private readonly HttpClient _httpClient = new HttpClient();
        private string _username;
        private string _token;

        public HomePage()
        {
            InitializeComponent();

            // Load username and token
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

            // Start tracking
            StartTracking();
        }

        private void StartTracking()
        {
            _ = GetAndSendLocationAsync();

            // Repeat every 5 minutes
            Device.StartTimer(TimeSpan.FromSeconds(10), () =>
            {
                _ = GetAndSendLocationAsync();
                return true;
            });
        }

        private async void OnRefreshClicked(object sender, EventArgs e)
        {
            await GetAndSendLocationAsync();
        }

        private async Task GetAndSendLocationAsync()
        {
            try
            {
                // Check location permission
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

                    // Get the token from SecureStorage
                    string token = string.Empty;
                    string memberID = string.Empty;
                    string companyID = string.Empty;
                    try
                    {
                        token = await SecureStorage.GetAsync("api_token") ?? string.Empty;
                        memberID = await SecureStorage.GetAsync("logged_in_memberID") ?? string.Empty;
                        companyID = await SecureStorage.GetAsync("logged_in_companyID") ?? string.Empty;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"SecureStorage error: {ex.Message}");
                    }

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

            // Go back to login page (reset stack)
            Application.Current.MainPage = new NavigationPage(new LoginPage());
        }
    }
}
