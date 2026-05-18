using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;
#if ANDROID
using Android.Webkit;
using AndroidX.Core.Content;
using static Android.Manifest;
#endif

namespace GeoTrackerApp3.Views
{
    public partial class WebViewTestPage : ContentPage
    {
        private TaskCompletionSource<FaceAuthResult> _authResultTask;
        private int _companyId;
        private int _expectedMemberId;
        private bool _isDisposed;

        // Cache user data to avoid repeated SecureStorage calls
        private static int? _cachedCompanyId;
        private static int? _cachedMemberId;
        private static DateTime _cacheTime = DateTime.MinValue;
        private static readonly TimeSpan CACHE_DURATION = TimeSpan.FromMinutes(10);

        public WebViewTestPage()
        {
            InitializeComponent();

            TestWebView.Navigating += OnWebViewNavigating;
            TestWebView.Navigated += OnWebViewNavigated;
            
            // Hook up HandlerChanged event BEFORE LoadUserData to set permissions before URL loads
            TestWebView.HandlerChanged += OnWebViewHandlerChanged;
            
            LoadUserData();
        }

        private void OnWebViewHandlerChanged(object sender, EventArgs e)
        {
#if ANDROID
            if (TestWebView.Handler?.PlatformView is Android.Webkit.WebView webView)
            {
                ConfigureAndroidWebView(webView);
            }
#endif
        }

#if ANDROID
private void ConfigureAndroidWebView(Android.Webkit.WebView webView)
{
    try
    {
        var settings = webView.Settings;
                
        // Enable JavaScript and related features
        settings.JavaScriptEnabled = true;
        settings.DomStorageEnabled = true;
        settings.DatabaseEnabled = true;
        settings.MediaPlaybackRequiresUserGesture = false;
                
        // Enable hardware acceleration for better performance
        webView.SetLayerType(Android.Views.LayerType.Hardware, null);
                
        // Additional settings for camera/media
        settings.JavaScriptCanOpenWindowsAutomatically = true;
        settings.SetGeolocationEnabled(true);
        settings.AllowFileAccess = true;
        settings.AllowContentAccess = true;

        // Enable mixed content (needed for some pages)
        settings.MixedContentMode = Android.Webkit.MixedContentHandling.AlwaysAllow;

        // Set custom WebViewClient to handle SSL errors for trusted domains
        webView.SetWebViewClient(new TrustedDomainWebViewClient(
            () => LoadingOverlay.IsVisible = false,
            (url) => HandleCustomUrl(url)
        ));
                
        // Set custom WebChromeClient to handle permissions
        webView.SetWebChromeClient(new CustomWebChromeClient());
                
        Debug.WriteLine("WebViewTestPage: Android WebView configured with camera permissions and SSL bypass");
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"WebViewTestPage: Error configuring Android WebView - {ex.Message}");
    }
}

private class TrustedDomainWebViewClient : WebViewClient
{
    private readonly Action _onPageLoaded;
    private readonly Action<string> _onCustomUrl;

    public TrustedDomainWebViewClient(Action onPageLoaded, Action<string> onCustomUrl)
    {
        _onPageLoaded = onPageLoaded;
        _onCustomUrl = onCustomUrl;
    }

    private static readonly HashSet<string> TrustedHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "picsconfig.ics.co.za",
        "picsapiconfig.ics.co.za",
        "pics.ics.co.za"
    };

    public override void OnReceivedSslError(Android.Webkit.WebView view, SslErrorHandler handler, Android.Net.Http.SslError error)
    {
        var url = error.Url;
        Debug.WriteLine($"WebViewClient: SSL error for URL: {url}, Error: {error.PrimaryError}");

        try
        {
            if (!string.IsNullOrEmpty(url))
            {
                var host = new Uri(url).Host;
                if (TrustedHosts.Contains(host))
                {
                    Debug.WriteLine($"WebViewClient: Proceeding for trusted host: {host}");
                    handler.Proceed();
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WebViewClient: Error parsing URL - {ex.Message}");
        }

        Debug.WriteLine("WebViewClient: Cancelling SSL for untrusted host");
        handler.Cancel();
    }

    public override void OnPageFinished(Android.Webkit.WebView view, string url)
    {
        base.OnPageFinished(view, url);
        Debug.WriteLine($"WebViewClient: Page finished loading: {url}");

        MainThread.BeginInvokeOnMainThread(() =>
        {
            _onPageLoaded?.Invoke();
        });
    }

    public override bool ShouldOverrideUrlLoading(Android.Webkit.WebView view, IWebResourceRequest request)
    {
        var url = request.Url?.ToString();
        Debug.WriteLine($"WebViewClient: Loading URL: {url}");

        if (url != null && url.StartsWith("geotracker://"))
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _onCustomUrl?.Invoke(url);
            });
            return true;
        }
        return false;
    }
}

private class CustomWebChromeClient : WebChromeClient
        {
            public override void OnPermissionRequest(PermissionRequest request)
            {
                try
                {
                    var requestedResources = request.GetResources();
                    Debug.WriteLine($"WebChromeClient: Permission requested for: {string.Join(", ", requestedResources)}");
                    
                    // Must grant on UI thread
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        try
                        {
                            request.Grant(requestedResources);
                            Debug.WriteLine("WebChromeClient: Granted all requested permissions");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"WebChromeClient: Error granting permissions - {ex.Message}");
                        }
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"WebChromeClient: Error - {ex.Message}");
                    base.OnPermissionRequest(request);
                }
            }

            public override bool OnConsoleMessage(ConsoleMessage consoleMessage)
            {
                Debug.WriteLine($"WebView Console [{consoleMessage.InvokeMessageLevel()}]: {consoleMessage.Message()} -- From line {consoleMessage.LineNumber()} of {consoleMessage.SourceId()}");
                return true;
            }
        }
#endif

        private async void LoadUserData()
        {
            try
            {
#if ANDROID
                // Ensure camera permission is granted before loading the WebView URL
                var cameraStatus = await Permissions.CheckStatusAsync<Permissions.Camera>();
                if (cameraStatus != PermissionStatus.Granted)
                {
                    cameraStatus = await Permissions.RequestAsync<Permissions.Camera>();
                    Debug.WriteLine($"WebViewTestPage: Camera permission result = {cameraStatus}");
                }
#endif

                // Use cached values if available and not expired
                if (_cachedCompanyId.HasValue && _cachedMemberId.HasValue &&
                    DateTime.UtcNow - _cacheTime < CACHE_DURATION)
                {
                    _companyId = _cachedCompanyId.Value;
                    _expectedMemberId = _cachedMemberId.Value;
                }
                else
                {
                    string companyIdStr = await SecureStorage.GetAsync("logged_in_companyID") ?? "0";
                    string memberIdStr = await SecureStorage.GetAsync("logged_in_memberID") ?? "0";
                    
                    _companyId = int.TryParse(companyIdStr, out var cId) ? cId : 0;
                    _expectedMemberId = int.TryParse(memberIdStr, out var mId) ? mId : 0;

                    // Update cache
                    _cachedCompanyId = _companyId;
                    _cachedMemberId = _expectedMemberId;
                    _cacheTime = DateTime.UtcNow;
                }

                Debug.WriteLine($"WebViewTestPage: CompanyID={_companyId}, ExpectedMemberID={_expectedMemberId}");

                var url = $"https://picsconfig.ics.co.za/Home/UserRegistration?companyID={_companyId}&source=mobile&faDebug=1";
                
                TestWebView.Source = url;
                Debug.WriteLine($"WebViewTestPage: Loading {url}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WebViewTestPage: Error - {ex.Message}");
                TestWebView.Source = "https://picsconfig.ics.co.za/Home/UserRegistration?source=mobile&faDebug=1";
            }
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            Debug.WriteLine("WebViewTestPage: Waiting for face authentication...");
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            CleanupWebView();
        }

        private void CleanupWebView()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            try
            {
                // Unsubscribe from events to prevent memory leaks
                if (TestWebView != null)
                {
                    TestWebView.Navigating -= OnWebViewNavigating;
                    TestWebView.Navigated -= OnWebViewNavigated;
                    TestWebView.HandlerChanged -= OnWebViewHandlerChanged;
                    
                    // Clear WebView source and content to free memory
                    TestWebView.Source = null;
                    
#if ANDROID
                    // Android-specific cleanup
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        try
                        {
                            var handler = TestWebView.Handler;
                            if (handler?.PlatformView is Android.Webkit.WebView webView)
                            {
                                webView.StopLoading();
                                webView.LoadUrl("about:blank");
                                webView.ClearCache(true);
                                webView.ClearHistory();
                                webView.RemoveAllViews();
                                webView.Destroy();
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"WebView Android cleanup error: {ex.Message}");
                        }
                    });
#endif
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WebView cleanup error: {ex.Message}");
            }
        }

        private void OnWebViewNavigating(object sender, WebNavigatingEventArgs e)
        {
            Debug.WriteLine($"WebView navigating to: {e.Url}");
            
            if (e.Url.StartsWith("geotracker://"))
            {
                e.Cancel = true;
                HandleCustomUrl(e.Url);
            }
            else
            {
                LoadingOverlay.IsVisible = true;
            }
        }

        private async void HandleCustomUrl(string url)
        {
            try
            {
                Debug.WriteLine($"WebViewTestPage: Received {url}");

                var uri = new Uri(url);

                if (uri.Host == "face-result")
                {
                    // Uri.Query can be empty for custom schemes without a path separator.
                    // Fall back to parsing the query directly from the raw URL.
                    var queryString = uri.Query;
                    if (string.IsNullOrEmpty(queryString))
                    {
                        var qIndex = url.IndexOf('?');
                        if (qIndex >= 0)
                            queryString = url.Substring(qIndex);
                    }

                    var query = System.Web.HttpUtility.ParseQueryString(queryString);

                    var result = new FaceAuthResult
                    {
                        MemberID = int.TryParse(query["memberID"], out var mId) ? mId : 0,
                        MemberGUID = query["memberGUID"],
                        Match = bool.TryParse(query["match"], out var match) && match,
                        Confidence = double.TryParse(query["confidence"], out var conf) ? conf : 0,
                        Message = query["message"]
                    };

                    Debug.WriteLine($"Result: MemberID={result.MemberID}, Match={result.Match}");

                    if (result.MemberID != _expectedMemberId && result.MemberID != 0)
                    {
                        result.Match = false;
                    }

                    _authResultTask?.SetResult(result);
                    CleanupWebView(); // Clean up before closing
                    await Navigation.PopModalAsync();
                }
                else if (uri.Host == "face-cancelled")
                {
                    _authResultTask?.SetResult(null);
                    CleanupWebView(); // Clean up before closing
                    await Navigation.PopModalAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error: {ex.Message}");
                _authResultTask?.SetResult(null);
                CleanupWebView(); // Clean up before closing
                await Navigation.PopModalAsync();
            }
        }

        private void OnWebViewNavigated(object sender, WebNavigatedEventArgs e)
        {
            Debug.WriteLine($"Navigated: {e.Result}");
            
            LoadingOverlay.IsVisible = false;
        }

        public Task<FaceAuthResult> GetAuthResultAsync()
        {
            _authResultTask = new TaskCompletionSource<FaceAuthResult>();
            return _authResultTask.Task;
        }
    }

    public class FaceAuthResult
    {
        public int MemberID { get; set; }
        public string MemberGUID { get; set; }
        public bool Match { get; set; }
        public double Confidence { get; set; }
        public string Message { get; set; }
    }
}
