using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using GeoTrackerApp3.Platforms.Android;

namespace GeoTrackerApp3;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
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

        // ✅ Only start the service once when the app launches (not on rotation/config change)
        if (savedInstanceState == null)
        {
            var intent = new Intent(this, typeof(LocationForegroundService));

            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                StartForegroundService(intent);
            else
                StartService(intent);
        }
    }
}
