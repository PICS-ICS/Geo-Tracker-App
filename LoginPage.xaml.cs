using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using GeoTrackerApp3.Services;
using GeoTrackerApp3.Models;

#if ANDROID
using Android.Content;
using GeoTrackerApp3.Platforms.Android;
#endif
//using Android.Net;

namespace GeoTrackerApp3.Views
{
    public partial class LoginPage : ContentPage
    {
        public LoginPage()
        {
            InitializeComponent();

            // Optionally prefill from Preferences
            if (Preferences.ContainsKey("Username"))
                EmailEntry.Text = Preferences.Get("Username", string.Empty);
        }

        private async void OnRegisterClicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new RegisterPage());
        }

        private async void OnLoginClicked(object sender, EventArgs e)
        {
            var email = EmailEntry.Text?.Trim();
            var password = PasswordEntry.Text;

            if (string.IsNullOrEmpty(email))
            {
                await DisplayAlert("Error", "Please enter your email.", "OK");
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                await DisplayAlert("Error", "Please enter your password.", "OK");
                return;
            }

            try
            {
                SetBusy(true);

                var request = new LoginRequest
                {
                    UsernameOrEmail = email,
                    Password = password
                };

                var result = await ApiService.LoginAsync(request);

                if (result.IsSuccess)
                {
                    // Navigate to HomePage (replace stack so user can't go back to login)
                    Application.Current.MainPage = new NavigationPage(new HomePage());

#if ANDROID
                    var status = await Permissions.CheckStatusAsync<Permissions.LocationAlways>();
                    if (status != PermissionStatus.Granted)
                        status = await Permissions.RequestAsync<Permissions.LocationAlways>();

                    if (status != PermissionStatus.Granted)
                    {
                        await DisplayAlert("Permission required", "Location permission is required to track your location.", "OK");
                        return; // Don't start the service if permission is denied
                    }

                    // Now start the foreground service
                    var context = Android.App.Application.Context;
                    var intent = new Intent(context, typeof(LocationForegroundService));
                    context.StartForegroundService(intent);
#endif
                }
                else
                {
                    await DisplayAlert("Login failed", result.ErrorMessage ?? "Unknown error", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"An error occurred: {ex.Message}", "OK");
            }
            finally
            {
                SetBusy(false);
            }
        }

        void SetBusy(bool isBusy)
        {
            BusyIndicator.IsVisible = isBusy;
            BusyIndicator.IsRunning = isBusy;
            LoginButton.IsEnabled = !isBusy;
        }
    }
}
