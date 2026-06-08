using System;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using GeoTrackerApp3.Services;
using GeoTrackerApp3.Models;

namespace GeoTrackerApp3.Views
{
    public partial class LoginPage : ContentPage
    {
        private const string ForgotPasswordUrl = "https://pics.ics.co.za/Home/ForgetPassword";

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // Request location permission early so it's ready when tracking starts
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            }

            if (status != PermissionStatus.Granted)
            {
                await DisplayAlert("Permission required",
                    "Location permission is required to track your location.", "OK");
            }
        }

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
                    UserName = email,
                    password = password
                };

                var result = await ApiService.LoginAsync(request);

                if (result.IsSuccess)
                {
                    // Navigate to HomePage (replace stack so user can't go back to login)
                    Application.Current.MainPage = new NavigationPage(new HomePage());
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

        private async void OnForgotPasswordTapped(object sender, TappedEventArgs e)
        {
            try
            {
                await Browser.Default.OpenAsync(ForgotPasswordUrl, BrowserLaunchMode.SystemPreferred);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Unable to open link", ex.Message, "OK");
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
