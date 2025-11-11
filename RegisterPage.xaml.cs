using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using GeoTrackerApp3.Services;
using GeoTrackerApp3.Models;

namespace GeoTrackerApp3.Views
{
    public partial class RegisterPage : ContentPage
    {
        public RegisterPage()
        {
            InitializeComponent();
        }

        private async void OnRegisterClicked(object sender, EventArgs e)
        {
            var firstName = FirstNameEntry.Text?.Trim();
            var lastName = LastNameEntry.Text?.Trim();
            var phone = PhoneEntry.Text?.Trim();
            var email = EmailEntry.Text?.Trim();
            var password = PasswordEntry.Text;
            var confirm = ConfirmPasswordEntry.Text;

            // ✅ Validation
            if (string.IsNullOrEmpty(firstName) ||
                string.IsNullOrEmpty(lastName) ||
                string.IsNullOrEmpty(phone) ||
                string.IsNullOrEmpty(email))
            {
                await DisplayAlert("Error", "Please fill in all fields.", "OK");
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                await DisplayAlert("Error", "Please enter a password.", "OK");
                return;
            }

            if (password != confirm)
            {
                await DisplayAlert("Error", "Passwords do not match.", "OK");
                return;
            }

            try
            {
                SetBusy(true);

                var request = new RegisterRequest
                {
                    Email = email,
                    Password = password,
                    FirstName = firstName,
                    LastName = lastName,
                    PhoneNumber = phone
                };

                var result = await ApiService.RegisterAsync(request);

                if (result.IsSuccess)
                {
                    try
                    {
                        await SecureStorage.SetAsync("api_token", result.Token);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"SecureStorage error: {ex.Message}");
                    }

                    Preferences.Set("Username", email);
                    await DisplayAlert("Success", "Account created and logged in.", "OK");

                    Application.Current.MainPage = new NavigationPage(new HomePage());
                }
                else
                {
                    await DisplayAlert("Registration failed", result.ErrorMessage ?? "Unknown error", "OK");
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

        private async void OnGoToLoginClicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new LoginPage());
        }

        void SetBusy(bool isBusy)
        {
            BusyIndicator.IsVisible = isBusy;
            BusyIndicator.IsRunning = isBusy;
            RegisterButton.IsEnabled = !isBusy;
        }
    }
}
