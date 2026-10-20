using TheBleedingDeacons.Intergroup.Monitor.Services.Interfaces;

namespace TheBleedingDeacons.Intergroup.Monitor.Services;

/// <summary>
/// Google sign-in through the platform's own account picker, ending in a
/// Firebase ID token. See <see cref="INativeGoogleSignIn"/>.
///
/// <para>Shaped like <see cref="PushRegistrar"/>: the shared half says
/// what an unavailable head answers, and each platform file answers only
/// the question its platform can. Android and iOS do it; Windows and
/// macOS say no and the responder gets the browser, as before.</para>
/// </summary>
public sealed partial class NativeGoogleSignIn : INativeGoogleSignIn
{
	public bool IsAvailable => PlatformIsAvailable();

	public async Task<NativeGoogleSignInResult> SignInAsync(CancellationToken cancellationToken = default)
	{
		if (!IsAvailable)
		{
			return NativeGoogleSignInResult.Unavailable;
		}

		return await PlatformSignInAsync(cancellationToken).ConfigureAwait(false);
	}

	// A method rather than a partial property, as in Link's AppleSignIn:
	// the analysers do not follow a partial property across platform files.
	private static partial bool PlatformIsAvailable();

	private static partial Task<NativeGoogleSignInResult> PlatformSignInAsync(CancellationToken cancellationToken);
}
