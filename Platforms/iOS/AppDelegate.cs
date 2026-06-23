using Foundation;
using UIKit;
using System.Diagnostics;

namespace GeoTrackerApp3;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

	public override bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions)
	{
		var result = base.FinishedLaunching(application, launchOptions!);

		// Check if the app was relaunched due to a location event (geofence or significant change)
		if (launchOptions != null &&
			launchOptions.ContainsKey(UIApplication.LaunchOptionsLocationKey))
		{
			Debug.WriteLine("[iOS AppDelegate] App relaunched due to location event (geofence/significant change)");
			_ = GeoTrackerApp3.Platforms.iOS.iOSLocationService.ResumeIfNeededAsync();
		}

		return result;
	}

	public override void DidEnterBackground(UIApplication application)
	{
		base.DidEnterBackground(application);
		Debug.WriteLine("[iOS AppDelegate] App entered background - switching to geofence mode");

		// Notify the iOS location service to enter background/geofence-only mode
		// The service will stop continuous updates and rely on geofence triggers
	}

	public override void WillEnterForeground(UIApplication application)
	{
		base.WillEnterForeground(application);
		Debug.WriteLine("[iOS AppDelegate] App entering foreground - switching to 5-min interval mode");

		// The HomePage will call StartForegroundMode when it resumes
	}
}
