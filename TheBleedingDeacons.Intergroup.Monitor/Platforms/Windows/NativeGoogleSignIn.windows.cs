using TheBleedingDeacons.Intergroup.Monitor.Services.Interfaces;

namespace TheBleedingDeacons.Intergroup.Monitor.Services;

/// <summary>
/// Windows half of <see cref="NativeGoogleSignIn"/>: there is no Firebase
/// Auth SDK for this head, so Google goes through the browser as before.
/// </summary>
public sealed partial class NativeGoogleSignIn
{
	private static partial bool PlatformIsAvailable() => false;

	private static partial Task<NativeGoogleSignInResult> PlatformSignInAsync(CancellationToken cancellationToken) =>
		Task.FromResult(NativeGoogleSignInResult.Unavailable);
}
