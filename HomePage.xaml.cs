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

#if IOS
        private GeoTrackerApp3.Platforms.iOS.iOSLocationService _iOSLocationService;
#endif

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
                    await DisplayAlert("Failed", "Face Verification Failed!", "OK");
                    return;
                }

                if (!result.Match)
                {
                    await Task.Delay(700);
                    await DisplayAlert("Failed", "Face did not match logged in user!", "OK");

                    //await DisplayAlert("Failed", result.Message + ":" + result.Match.ToString()+ ":" + result.MemberID.ToString() ?? "Verification failed", "OK");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"HomePage: Verified MemberID={result.MemberID}");
                
                // --- START TRACKING ---
                _isTracking = true;
                _cts = new CancellationTokenSource();

                ToggleTrackingButton.Text = "Stop Tracking";
                ToggleTrackingButton.BackgroundColor = Color.FromArgb("#EF4444");

                await TrackingStatusFrame.FadeTo(1, 300, Easing.Linear);

                // Start foreground service for background tracking
                StartLocationService();

                // Also track in-app for UI updates
                _ = StartTrackingAsync(_cts.Token);
            }
            else
            {
                // --- STOP TRACKING ---
                _isTracking = false;
                _cts?.Cancel();
                _cts?.Dispose();
                _cts = null;

                // Stop the foreground service
                StopLocationService();

                ToggleTrackingButton.Text = "Start Tracking";
                ToggleTrackingButton.BackgroundColor = Color.FromArgb("#22C55E");

                await TrackingStatusFrame.FadeTo(0, 300, Easing.Linear);
            }
        }


        private async Task StartTrackingAsync(CancellationToken token)
        {
            // UI-only loop — the foreground service handles API calls
            while (!token.IsCancellationRequested)
            {
                await UpdateLocationUIAsync();
                await UpdateSyncStatusAsync();

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

        private async Task UpdateLocationUIAsync()
        {
            try
            {
                var location = await Geolocation.GetLocationAsync(
                    new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(5)));

                if (location != null)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        LatitudeLabel.Text = $"{location.Latitude:F6}";
                        LongitudeLabel.Text = $"{location.Longitude:F6}";
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UI location error: {ex.Message}");
            }
        }

        private void StartLocationService()
        {
#if ANDROID
            var context = Android.App.Application.Context;
            var intent = new Android.Content.Intent(context, typeof(Platforms.Android.LocationForegroundService));
            intent.PutExtra("token", _token);
            intent.PutExtra("memberId", Convert.ToInt32(_cachedMemberId));
            intent.PutExtra("companyId", Convert.ToInt32(_cachedCompanyId));
            context.StartForegroundService(intent);
#elif IOS
            _iOSLocationService = new GeoTrackerApp3.Platforms.iOS.iOSLocationService();
            _iOSLocationService.Start(_token, Convert.ToInt32(_cachedMemberId), Convert.ToInt32(_cachedCompanyId));
#endif
        }

        private void StopLocationService()
        {
#if ANDROID
            var context = Android.App.Application.Context;
            var intent = new Android.Content.Intent(context, typeof(Platforms.Android.LocationForegroundService));
            context.StopService(intent);
#elif IOS
            _iOSLocationService?.Stop();
            _iOSLocationService = null;
#endif
        }

        private async Task UpdateSyncStatusAsync()
        {
            try
            {
                var pendingCount = await LocationQueueService.GetPendingCountAsync();
                var isOnline = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    SyncStatusBorder.IsVisible = _isTracking;

                    if (!isOnline)
                    {
                        SyncStatusBorder.BackgroundColor = Color.FromArgb("#FFF3E0");
                        SyncStatusIcon.Text = "⚠️";
                        SyncStatusText.Text = "Offline";
                        SyncStatusText.TextColor = Color.FromArgb("#E65100");
                        SyncStatusDetail.Text = $"{pendingCount} pings queued • Will auto-sync";
                    }
                    else if (pendingCount > 0)
                    {
                        SyncStatusBorder.BackgroundColor = Color.FromArgb("#FFF3E0");
                        SyncStatusIcon.Text = "🔄";
                        SyncStatusText.Text = "Syncing";
                        SyncStatusText.TextColor = Color.FromArgb("#E65100");
                        SyncStatusDetail.Text = $"{pendingCount} pings pending";
                    }
                    else
                    {
                        SyncStatusBorder.BackgroundColor = Color.FromArgb("#E8F5E9");
                        SyncStatusIcon.Text = "✅";
                        SyncStatusText.Text = "Connected";
                        SyncStatusText.TextColor = Color.FromArgb("#2E7D32");
                        SyncStatusDetail.Text = "All pings sent";
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Sync status update error: {ex.Message}");
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
                    StopLocationService();
                }

                SecureStorage.Remove("api_token");
            }
            catch { }

            Preferences.Remove("Username");

            Application.Current.MainPage = new NavigationPage(new LoginPage());
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // Always refresh sync status when returning to the page
            await UpdateSyncStatusAsync();

            // Restart the UI update loop if tracking is active but loop was stopped
            if (_isTracking && (_cts == null || _cts.IsCancellationRequested))
            {
                _cts?.Dispose();
                _cts = new CancellationTokenSource();
                _ = StartTrackingAsync(_cts.Token);
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            
            // Only cancel in-app tracking loop — foreground service keeps running
            if (_isTracking)
            {
                _cts?.Cancel();
            }
        }
    }
}
