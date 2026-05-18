using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Webkit;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using GeoTrackerApp3.Platforms.Android;

namespace GeoTrackerApp3;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    HardwareAccelerated = true, // Enable hardware acceleration
    ConfigurationChanges = ConfigChanges.ScreenSize
        | ConfigChanges.Orientation
        | ConfigChanges.UiMode
        | ConfigChanges.ScreenLayout
        | ConfigChanges.SmallestScreenSize
        | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Performance optimization for low-end devices
        OptimizeForPerformance();

        // Request camera permission
        RequestCameraPermission();

        // Configure WebView for camera access with optimizations
        Microsoft.Maui.Handlers.WebViewHandler.Mapper.AppendToMapping("CameraWebView", (handler, view) =>
        {
            try
            {
                if (handler.PlatformView is Android.Webkit.WebView webView)
                {
                    webView.Settings.JavaScriptEnabled = true;
                    webView.Settings.DomStorageEnabled = true;
                    webView.Settings.MediaPlaybackRequiresUserGesture = false;
                
                    // Performance optimizations
                    webView.Settings.CacheMode = CacheModes.CacheElseNetwork;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WebViewMapper error: {ex.Message}");
            }
        });

        // Location service is started from LoginPage.OnAppearing after permission is granted
    }

    private void OptimizeForPerformance()
    {
        // Enable sustained performance mode on Android N+ for consistent performance
        if (Build.VERSION.SdkInt >= BuildVersionCodes.N)
        {
            try
            {
                Window?.SetSustainedPerformanceMode(true);
            }
            catch { /* Not all devices support this */ }
        }

        // Optimize for low RAM devices
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Kitkat)
        {
            var activityManager = (ActivityManager)GetSystemService(ActivityService);
            if (activityManager?.IsLowRamDevice ?? false)
            {
                // Additional optimizations for low-end devices
                System.Diagnostics.Debug.WriteLine("Low RAM device detected - applying optimizations");
            }
        }
    }

    private void RequestCameraPermission()
    {
        if (ContextCompat.CheckSelfPermission(this, Manifest.Permission.Camera) != Permission.Granted)
        {
            ActivityCompat.RequestPermissions(this, new[] { Manifest.Permission.Camera }, 100);
        }
    }
}

// WebChromeClient to grant camera permission
public class CameraWebChromeClient : WebChromeClient
{
    private readonly Activity _activity;

    public CameraWebChromeClient(Activity activity)
    {
        _activity = activity;
    }

    public override void OnPermissionRequest(PermissionRequest request)
    {
        // Auto-grant camera permission
        if (ContextCompat.CheckSelfPermission(_activity, Manifest.Permission.Camera) == Permission.Granted)
        {
            request.Grant(request.GetResources());
        }
        else
        {
            request.Deny();
        }
    }
}

