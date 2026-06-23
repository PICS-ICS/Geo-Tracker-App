using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Input;
using GeoTrackerApp3.Models;
using GeoTrackerApp3.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Storage;

namespace GeoTrackerApp3.Views
{
    public partial class GeofenceInfoPage : ContentPage
    {
        // Each geofence zone is a 500 m radius circle (matches the tracking services).
        private const double GeofenceRadiusMeters = 500.0;

        private string _token = string.Empty;
        private int _memberId;

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<GeofenceItem> Geofences { get; } = new();

        public ICommand RefreshCommand { get; }

        public GeofenceInfoPage()
        {
            InitializeComponent();
            RefreshCommand = new Command(async () => await LoadAsync());
            BindingContext = this;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadCredentialsAsync();
            ShowLastSentLocation();
            await LoadAsync();
        }

        private async Task LoadCredentialsAsync()
        {
            try
            {
                _token = await SecureStorage.GetAsync("api_token") ?? string.Empty;
                var memberIdStr = await SecureStorage.GetAsync("logged_in_memberID") ?? "0";
                _memberId = int.TryParse(memberIdStr, out var id) ? id : 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GeofenceInfo] Credential load error: {ex.Message}");
            }
        }

        private async Task LoadAsync()
        {
            if (IsLoading) return;

            try
            {
                IsLoading = true;

                // 1. Get the current device location.
                Location? current = null;
                try
                {
                    current = await Geolocation.GetLocationAsync(
                        new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10)));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[GeofenceInfo] Location error: {ex.Message}");
                }

                if (current != null)
                {
                    CurrentLatLabel.Text = current.Latitude.ToString("F6", CultureInfo.InvariantCulture);
                    CurrentLonLabel.Text = current.Longitude.ToString("F6", CultureInfo.InvariantCulture);
                }
                else
                {
                    CurrentLatLabel.Text = "Unavailable";
                    CurrentLonLabel.Text = "Unavailable";
                }

                // Refresh the "last sent" panel in case a ping went out while open.
                ShowLastSentLocation();

                // 2. Fetch the geofences for this member from the API.
                double lat = current?.Latitude ?? 0;
                double lon = current?.Longitude ?? 0;
                var fences = await ApiService.GetGeofencesAsync(_memberId, lat, lon, _token);

                // 3. Build display items with distance + inside/outside status.
                Geofences.Clear();
                int index = 1;
                foreach (var fence in fences)
                {
                    double? distanceMeters = null;
                    if (current != null)
                    {
                        distanceMeters = Location.CalculateDistance(
                            current.Latitude, current.Longitude,
                            fence.Lat, fence.Lon,
                            DistanceUnits.Kilometers) * 1000.0;
                    }

                    Geofences.Add(BuildItem(index++, fence, distanceMeters));
                }

                GeofenceCountLabel.Text = Geofences.Count.ToString(CultureInfo.InvariantCulture);
                EmptyStateBorder.IsVisible = Geofences.Count == 0;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private static GeofenceItem BuildItem(int index, GeofenceLocation fence, double? distanceMeters)
        {
            bool isInside = distanceMeters.HasValue && distanceMeters.Value <= GeofenceRadiusMeters;

            return new GeofenceItem
            {
                Name = $"Zone {index}",
                Coordinates = $"{fence.Lat.ToString("F5", CultureInfo.InvariantCulture)}, {fence.Lon.ToString("F5", CultureInfo.InvariantCulture)}",
                DistanceText = FormatDistance(distanceMeters),
                StatusText = !distanceMeters.HasValue ? "Distance unknown"
                                                      : isInside ? "Inside zone" : "Outside zone",
                StatusIcon = !distanceMeters.HasValue ? "?" : isInside ? "?" : "??",
                StatusBackground = !distanceMeters.HasValue ? Color.FromArgb("#F1F5F9")
                                                            : isInside ? Color.FromArgb("#DCFCE7")
                                                                       : Color.FromArgb("#FEF3C7"),
                StatusTextColor = !distanceMeters.HasValue ? Color.FromArgb("#64748B")
                                                          : isInside ? Color.FromArgb("#16A34A")
                                                                     : Color.FromArgb("#B45309")
            };
        }

        private static string FormatDistance(double? meters)
        {
            if (!meters.HasValue)
                return "—";

            double m = meters.Value;
            if (m < 1000)
                return $"{m:F0} m";

            return $"{(m / 1000.0):F2} km";
        }

        private void ShowLastSentLocation()
        {
            if (LastLocationService.TryGetLast(out var lat, out var lon, out var timeUtc))
            {
                LastLatLabel.Text = lat.ToString("F6", CultureInfo.InvariantCulture);
                LastLonLabel.Text = lon.ToString("F6", CultureInfo.InvariantCulture);
                LastSentAgoLabel.Text = FormatTimeAgo(DateTime.UtcNow - timeUtc);
                LastSentTimeLabel.Text = $"Sent on {timeUtc.ToLocalTime():dd MMM yyyy 'at' HH:mm:ss}";
            }
            else
            {
                LastLatLabel.Text = "—";
                LastLonLabel.Text = "—";
                LastSentAgoLabel.Text = "No data";
                LastSentTimeLabel.Text = "No location has been sent yet.";
            }
        }

        private static string FormatTimeAgo(TimeSpan span)
        {
            if (span < TimeSpan.Zero)
                span = TimeSpan.Zero;

            if (span.TotalSeconds < 60)
                return "Just now";
            if (span.TotalMinutes < 60)
            {
                int mins = (int)span.TotalMinutes;
                return mins == 1 ? "1 minute ago" : $"{mins} minutes ago";
            }
            if (span.TotalHours < 24)
            {
                int hours = (int)span.TotalHours;
                return hours == 1 ? "1 hour ago" : $"{hours} hours ago";
            }

            int days = (int)span.TotalDays;
            return days == 1 ? "1 day ago" : $"{days} days ago";
        }

        private async void OnRefreshClicked(object sender, EventArgs e)
        {
            await LoadAsync();
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }
    }

    /// <summary>
    /// View-model item describing a single geofence zone for display.
    /// </summary>
    public class GeofenceItem
    {
        public string Name { get; set; } = string.Empty;
        public string Coordinates { get; set; } = string.Empty;
        public string DistanceText { get; set; } = string.Empty;
        public string StatusText { get; set; } = string.Empty;
        public string StatusIcon { get; set; } = string.Empty;
        public Color StatusBackground { get; set; } = Colors.Transparent;
        public Color StatusTextColor { get; set; } = Colors.Black;
    }
}
