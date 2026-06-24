using System.Diagnostics;
using System.Linq;

namespace GeoTrackerApp3.Services
{
    /// <summary>
    /// Centralized helper for surfacing errors to the user on-screen.
    /// Safe to call from any thread and from background services (Android/iOS
    /// location services, static API helpers, etc.). When no UI is available
    /// (e.g. the app is suspended) it falls back to debug logging only.
    /// </summary>
    public static class ErrorDisplayService
    {
        // Prevents the same message from spamming the user repeatedly when it
        // originates from a timer/loop that fires every few seconds.
        private static readonly Dictionary<string, DateTime> _recentMessages = new();
        private static readonly object _sync = new();
        private static readonly TimeSpan ThrottleWindow = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Shows an exception to the user, including the inner exception detail.
        /// </summary>
        public static void ShowError(string source, Exception ex)
            => ShowError(source, ex?.Message ?? "Unknown error", ex);

        /// <summary>
        /// Shows an error message to the user as a popup alert and logs it.
        /// </summary>
        public static void ShowError(string source, string message, Exception? ex = null)
        {
            var detail = ex?.InnerException != null
                ? $"{message}\n\nDetail: {ex.InnerException.Message}"
                : message;

            // Always log so it is captured even when no UI can be shown.
            Debug.WriteLine($"[{source}] {detail}");

            // Throttle identical alerts so background timers don't spam the user.
            if (!ShouldShow(source, message))
                return;

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    var page = GetCurrentPage();
                    if (page != null)
                        await page.DisplayAlert($"Error: {source}", detail, "OK");
                }
                catch (Exception alertEx)
                {
                    Debug.WriteLine($"[ErrorDisplayService] Could not show alert: {alertEx.Message}");
                }
            });
        }

        private static bool ShouldShow(string source, string message)
        {
            var key = $"{source}|{message}";
            lock (_sync)
            {
                var now = DateTime.UtcNow;
                if (_recentMessages.TryGetValue(key, out var last) && now - last < ThrottleWindow)
                    return false;

                _recentMessages[key] = now;

                // Keep the dictionary from growing without bound.
                if (_recentMessages.Count > 50)
                {
                    var stale = _recentMessages
                        .Where(kvp => now - kvp.Value > ThrottleWindow)
                        .Select(kvp => kvp.Key)
                        .ToList();
                    foreach (var k in stale)
                        _recentMessages.Remove(k);
                }

                return true;
            }
        }

        private static Page? GetCurrentPage()
        {
            var app = Application.Current;
            if (app == null)
                return null;

            var window = app.Windows?.FirstOrDefault();
            var page = window?.Page;
            if (page == null)
                return null;

            // Prefer a visible modal page (e.g. the face-verification WebView).
            var modal = page.Navigation?.ModalStack?.LastOrDefault();
            if (modal != null)
                return modal;

            // Otherwise the top of the navigation stack.
            var pushed = page.Navigation?.NavigationStack?.LastOrDefault();
            if (pushed != null)
                return pushed;

            return page;
        }
    }
}
