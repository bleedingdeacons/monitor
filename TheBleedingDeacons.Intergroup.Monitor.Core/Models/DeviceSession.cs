using System.Text.Json.Serialization;

namespace TheBleedingDeacons.Intergroup.Monitor.Models;

/// <summary>
/// What Sentinel says about this handset when it is asked.
///
/// <para>Returned both by enrolment (with <see cref="Token"/> populated,
/// the only time the plaintext token is ever sent) and by the session
/// check at launch (without it).</para>
/// </summary>
public class DeviceSession
{
	/// <summary>
	/// The bearer token. Present only in an enrolment response; Sentinel
	/// cannot reissue it, so it goes straight to secure storage.
	/// </summary>
	[JsonPropertyName("token")]
	public string Token { get; set; } = string.Empty;

	/// <summary>
	/// The secret Sentinel encrypts alert payloads to. Like <see cref="Token"/>
	/// it appears only in an enrolment response and cannot be reissued, so
	/// it goes straight to secure storage and is dropped from memory.
	/// </summary>
	[JsonPropertyName("payload_key")]
	public string PayloadKey { get; set; } = string.Empty;

	[JsonPropertyName("device_id")]
	public long DeviceId { get; set; }

	/// <summary>
	/// The responder's anonymous name, for showing who this handset is
	/// signed in as. Deliberately the anonymous name and not an email —
	/// it is displayed on a screen that sits face-up on a table.
	/// </summary>
	[JsonPropertyName("responder")]
	public string Responder { get; set; } = string.Empty;

	[JsonPropertyName("platform")]
	public string Platform { get; set; } = string.Empty;

	[JsonPropertyName("push_provider")]
	public string PushProvider { get; set; } = string.Empty;

	[JsonPropertyName("label")]
	public string Label { get; set; } = string.Empty;

	[JsonPropertyName("authorised")]
	public bool Authorised { get; set; }

	/// <summary>
	/// Whether this handset can raise a message, reply to one, or pass a
	/// job back.
	///
	/// <para><b>Feature discovery, not a permission.</b> Every enrolled
	/// handset may send — Sentinel has no capability to consult, because
	/// there is no WordPress user behind a device token, and a handset
	/// exists only because the responder gate passed. What this reports
	/// is whether the <em>server</em> has those routes at all.</para>
	///
	/// <para>It defaults to false, which is the important half. Monitor
	/// updates itself and the site it talks to does not, so the ordinary
	/// case is a handset newer than its Sentinel — and a compose button that
	/// answers 404 is worse than no compose button. An older server says
	/// nothing here and gets the safe answer.</para>
	/// </summary>
	[JsonPropertyName("can_send")]
	public bool CanSend { get; set; }
}

/// <summary>
/// The outcome of an attempt to sign in or check the session.
///
/// <para>A result type rather than exceptions because every one of these
/// outcomes is ordinary: a responder mistypes a password, a certification
/// lapses, a handset is out of signal. Only the last of those is worth a
/// retry, and the caller needs to be able to tell them apart to say
/// anything useful.</para>
/// </summary>
public sealed class SentinelResult<T>
{
	private SentinelResult(bool success, T? value, SentinelFailure failure, string message)
	{
		Success = success;
		Value = value;
		Failure = failure;
		Message = message;
	}

	public bool Success { get; }

	public T? Value { get; }

	public SentinelFailure Failure { get; }

	/// <summary>Sentinel's own message, suitable for showing to a responder.</summary>
	public string Message { get; }

	public static SentinelResult<T> Ok(T value) =>
		new(true, value, SentinelFailure.None, string.Empty);

	public static SentinelResult<T> Fail(SentinelFailure failure, string message) =>
		new(false, default, failure, message);
}

public enum SentinelFailure
{
	None = 0,

	/// <summary>Could not reach the server at all. Worth retrying.</summary>
	Network,

	/// <summary>Sentinel is not configured, or its address is wrong.</summary>
	NotConfigured,

	/// <summary>
	/// The token is gone — revoked, expired, or never valid. The app must
	/// drop it and show sign-in.
	/// </summary>
	Unauthenticated,

	/// <summary>
	/// Proven identity, refused access: not a telephone responder, or not
	/// certified. Showing this plainly is what tells a responder to go and
	/// sort their certification out.
	/// </summary>
	NotEligible,

	/// <summary>Wrong email or password.</summary>
	InvalidCredentials,

	/// <summary>Too many attempts; wait and try again.</summary>
	RateLimited,

	/// <summary>Anything else the server said no to.</summary>
	Server,
}
