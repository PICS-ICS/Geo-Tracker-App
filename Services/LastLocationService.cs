using System;
using System.Globalization;
using Microsoft.Maui.Storage;

namespace GeoTrackerApp3.Services
{
    /// <summary>
    /// Stores the last location ping that was successfully sent to the server so it can be
    /// displayed on the location details screen. Backed by <see cref="Preferences"/> so the
    /// value survives navigation and app restarts, and is shared across Android/iOS services.
    /// </summary>
    public static class LastLocationService
    {
        private const string LatKey = "last_sent_lat";
        private const string LonKey = "last_sent_lon";
        private const string TimeKey = "last_sent_time";

        /// <summary>
        /// Records a successfully sent location ping.
        /// </summary>
        public static void Record(double lat, double lon, DateTime timeUtc)
        {
            Preferences.Set(LatKey, lat);
            Preferences.Set(LonKey, lon);
            Preferences.Set(TimeKey, timeUtc.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Gets the last successfully sent location, if any.
        /// </summary>
        /// <returns><c>true</c> when a previous ping exists; otherwise <c>false</c>.</returns>
        public static bool TryGetLast(out double lat, out double lon, out DateTime timeUtc)
        {
            lat = Preferences.Get(LatKey, double.NaN);
            lon = Preferences.Get(LonKey, double.NaN);
            timeUtc = DateTime.MinValue;

            var timeStr = Preferences.Get(TimeKey, string.Empty);

            if (double.IsNaN(lat) || double.IsNaN(lon) || string.IsNullOrEmpty(timeStr))
                return false;

            return DateTime.TryParse(timeStr, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out timeUtc);
        }
    }
}
