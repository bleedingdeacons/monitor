using Foundation;

namespace TheBleedingDeacons.Intergroup.Monitor;

[Register("AppDelegate")]
public class AppDelegate : MonitorAppDelegate
{
	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
