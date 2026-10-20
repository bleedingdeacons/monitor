using TheBleedingDeacons.Intergroup.Monitor.Services.Interfaces;

namespace TheBleedingDeacons.Intergroup.Monitor.Services;

/// <summary>
/// macOS half of <see cref="NativeGoogleSignIn"/>. The bindings would
/// build here, but native sign-in is for the two handset heads, and the
/// desktop keeps the browser it has always used.
/// </summary>
public sealed partial class NativeGoogleSignIn
{
	private static partial bool PlatformIsAvailable() => false;

	private static partial Task<NativeGoogleSignInResult> PlatformSignInAsync(CancellationToken cancellationToken) =>
		Task.FromResult(NativeGoogleSignInResult.Unavailable);
}
