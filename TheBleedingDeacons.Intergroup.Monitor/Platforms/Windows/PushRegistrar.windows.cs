namespace TheBleedingDeacons.Intergroup.Monitor.Services;

/// <summary>
/// Windows half of <see cref="PushRegistrar"/>.
///
/// <para>There is no push here. FCM does not cover Windows, and Sentinel
/// speaks FCM. So this reports no transport and the handset collects its
/// own alerts by polling — which is why Monitor runs resident in the tray on
/// this head rather than being a foreground-only app.</para>
///
/// <para>If WNS is ever added on the Sentinel side, this is the file that
/// changes, and nothing above it needs to.</para>
/// </summary>
public sealed partial class PushRegistrar
{
	private partial string PlatformProvider() => string.Empty;

	private partial Task<string> PlatformGetTokenAsync() => Task.FromResult(string.Empty);
}
