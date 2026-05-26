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

		// Check if the app was relaunched due to a significant location change
		if (launchOptions != null &&
			launchOptions.ContainsKey(UIApplication.LaunchOptionsLocationKey))
		{
			Debug.WriteLine("[iOS AppDelegate] App relaunched due to significant location change");
			_ = GeoTrackerApp3.Platforms.iOS.iOSLocationService.ResumeIfNeededAsync();
		}

		return result;
	}
}
