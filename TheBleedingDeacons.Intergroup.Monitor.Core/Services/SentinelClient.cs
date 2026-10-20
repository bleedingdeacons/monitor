using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;
using TheBleedingDeacons.Intergroup.Monitor.Models;
using TheBleedingDeacons.Intergroup.Monitor.Services.Interfaces;

namespace TheBleedingDeacons.Intergroup.Monitor.Services;

/// <summary>
/// Talks to Sentinel's REST API over the platform-native HttpClient.
///
/// <para>Every method returns a <see cref="SentinelResult{T}"/> rather than
/// throwing. Failure here is ordinary — a handset on a train loses signal
/// several times a journey — and the difference between "try again in
/// twenty seconds" and "your certification has lapsed" is exactly what
/// the caller needs to act on, which an exception type would flatten.</para>
///
/// <para>Sentinel's error bodies are WordPress's shape:
/// <c>{"code":"sentinel_not_eligible","message":"…","data":{"status":403}}</c>.
/// The code is what gets mapped, not the status, because several distinct
/// refusals share a status and the app treats them differently.</para>
/// </summary>
public sealed class SentinelClient : ISentinelClient
{
	/// <summary>
	/// The URI scheme Monitor registers on Android, iOS and macOS. Must match
	/// <c>DeviceRedirectValidator::APP_SCHEME</c> on the server and the
	/// platform manifests — it is a contract between the three, not a
	/// setting.
	/// </summary>
	public const string CallbackScheme = "monitor";

	/// <summary>
	/// The host half of the callback. Declared alongside the scheme so the
	/// Android intent filter can match <c>monitor://auth</c> exactly rather
	/// than every <c>monitor://</c> URL — a custom scheme can be claimed by
	/// another app, and there is no reason to hand it more than the one
	/// path this app actually answers on.
	/// </summary>
	public const string CallbackHost = "auth";

	public const string CallbackUri = "monitor://auth";

	private const string ApiRoot = "wp-json/sentinel/v1/";

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNameCaseInsensitive = true,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,

		// PHP does not distinguish an integer from its decimal spelling, and
		// WordPress hands plenty of both back from post meta — `"id":"42"`
		// and `"id":42` are the same value to the server. Refusing the
		// quoted one would lose the whole response over a difference that
		// carries no meaning.
		NumberHandling = JsonNumberHandling.AllowReadingFromString,
	};

	private readonly HttpClient _httpClient;
	private readonly IConfigurationService _configuration;

	public SentinelClient(HttpClient httpClient, IConfigurationService configuration)
	{
		_httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
		_configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
	}

	public (Uri Start, Uri Callback) BuildSignInUrls(string provider)
	{
		var callback = new Uri(CallbackUri);
		var start = new Uri(
			Endpoint("auth/device/start"),
			// The redirect is sent so the server can validate it against its
			// allow-list and stash it with the OAuth state. It is never echoed
			// back from the callback, which is what stops it being a redirect
			// an attacker can influence.
			$"?provider={Uri.EscapeDataString(provider)}&redirect_uri={Uri.EscapeDataString(CallbackUri)}");

		return (start, callback);
	}

	public Task<SentinelResult<DeviceSession>> ExchangeCodeAsync(
		string code, string label, string platform, string pushProvider, string pushToken, CancellationToken cancellationToken)
	{
		return PostAsync<DeviceSession>(
			"auth/device/exchange",
			new Dictionary<string, string>(StringComparer.Ordinal)
			{
				["code"] = code,
				["label"] = label,
				["platform"] = platform,
				["push_provider"] = pushProvider,
				["push_token"] = pushToken,
			},
			token: null,
			cancellationToken);
	}

	public Task<SentinelResult<DeviceSession>> SignInWithFirebaseAsync(
		string idToken, string label, string platform, string pushProvider, string pushToken, CancellationToken cancellationToken)
	{
		return PostAsync<DeviceSession>(
			"auth/device/firebase",
			new Dictionary<string, string>(StringComparer.Ordinal)
			{
				["id_token"] = idToken,
				["label"] = label,
				["platform"] = platform,
				["push_provider"] = pushProvider,
				["push_token"] = pushToken,
			},
			token: null,
			cancellationToken);
	}

	public Task<SentinelResult<DeviceSession>> SignInWithPasswordAsync(
		string email, string password, string label, string platform, string pushProvider, string pushToken, CancellationToken cancellationToken)
	{
		return PostAsync<DeviceSession>(
			"auth/device/password",
			new Dictionary<string, string>(StringComparer.Ordinal)
			{
				["email"] = email,
				["password"] = password,
				["label"] = label,
				["platform"] = platform,
				["push_provider"] = pushProvider,
				["push_token"] = pushToken,
			},
			token: null,
			cancellationToken);
	}

	public async Task<SentinelResult<DeviceSession>> GetSessionAsync(string token, CancellationToken cancellationToken)
	{
		return await SendAsync<DeviceSession>(
			HttpMethod.Get, "auth/device/session", body: null, token, cancellationToken).ConfigureAwait(false);
	}

	public async Task<SentinelResult<bool>> UpdatePushTokenAsync(
		string token,
		string pushProvider,
		string pushToken,
		string lockScreen,
		CancellationToken cancellationToken)
	{
		var body = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["push_provider"] = pushProvider,
			["push_token"] = pushToken,
		};

		// Omitted rather than sent empty when this handset cannot tell.
		// The server reads an absent field as "no news" and keeps what it
		// already holds; an empty one would have to be given the same
		// meaning again at the far end to be no worse.
		if (lockScreen.Length > 0)
		{
			body["lock_screen"] = lockScreen;
		}

		var result = await PostAsync<JsonElement>(
			"auth/device/push", body, token, cancellationToken).ConfigureAwait(false);

		return Collapse(result);
	}

	public async Task<SentinelResult<bool>> SignOutAsync(string token, CancellationToken cancellationToken)
	{
		var result = await PostAsync<JsonElement>(
			"auth/device/signout",
			new Dictionary<string, string>(StringComparer.Ordinal),
			token,
			cancellationToken).ConfigureAwait(false);

		return Collapse(result);
	}

	public async Task<SentinelResult<IReadOnlyList<MonitorAlert>>> GetPendingAlertsAsync(
		string token, CancellationToken cancellationToken)
	{
		var result = await SendAsync<PendingAlertsResponse>(
			HttpMethod.Get, "alerts", body: null, token, cancellationToken).ConfigureAwait(false);

		if (!result.Success || result.Value is null)
		{
			return SentinelResult<IReadOnlyList<MonitorAlert>>.Fail(result.Failure, result.Message);
		}

		return SentinelResult<IReadOnlyList<MonitorAlert>>.Ok(result.Value.Alerts);
	}

	public async Task<SentinelResult<bool>> AcknowledgeAsync(string token, long alertId, CancellationToken cancellationToken)
	{
		var result = await PostAsync<JsonElement>(
			$"alerts/{alertId}/ack",
			new Dictionary<string, string>(StringComparer.Ordinal),
			token,
			cancellationToken).ConfigureAwait(false);

		return Collapse(result);
	}

	public async Task<SentinelResult<bool>> ReportUnreadableAsync(string token, CancellationToken cancellationToken)
	{
		var result = await PostAsync<JsonElement>(
			"alerts/unreadable",
			new Dictionary<string, string>(StringComparer.Ordinal),
			token,
			cancellationToken).ConfigureAwait(false);

		return Collapse(result);
	}

	public async Task<SentinelResult<IReadOnlyList<MonitorMember>>> GetMembersAsync(
		string token, string search, int page, CancellationToken cancellationToken)
	{
		var query = $"members?page={Math.Max(1, page)}";
		if (!string.IsNullOrWhiteSpace(search))
		{
			query += $"&search={Uri.EscapeDataString(search)}";
		}

		var result = await SendAsync<MembersResponse>(
			HttpMethod.Get, query, body: null, token, cancellationToken).ConfigureAwait(false);

		return result.Success && result.Value is not null
			? SentinelResult<IReadOnlyList<MonitorMember>>.Ok(result.Value.Members)
			: SentinelResult<IReadOnlyList<MonitorMember>>.Fail(result.Failure, result.Message);
	}

	public async Task<SentinelResult<MonitorMemberContact>> GetMemberContactAsync(
		string token, long memberId, CancellationToken cancellationToken)
	{
		var result = await SendAsync<MonitorMemberContact>(
			HttpMethod.Get,
			$"members/{memberId}/contact",
			body: null,
			token,
			cancellationToken).ConfigureAwait(false);

		return result.Success && result.Value is not null
			? SentinelResult<MonitorMemberContact>.Ok(result.Value)
			: SentinelResult<MonitorMemberContact>.Fail(result.Failure, result.Message);
	}

	public async Task<SentinelResult<IReadOnlyList<MonitorCommittee>>> GetCommitteesAsync(
		string token, CancellationToken cancellationToken)
	{
		var result = await SendAsync<CommitteesResponse>(
			HttpMethod.Get, "committees", body: null, token, cancellationToken).ConfigureAwait(false);

		return result.Success && result.Value is not null
			? SentinelResult<IReadOnlyList<MonitorCommittee>>.Ok(result.Value.Committees)
			: SentinelResult<IReadOnlyList<MonitorCommittee>>.Fail(result.Failure, result.Message);
	}

	public async Task<SentinelResult<bool>> SendAlertAsync(
		string token,
		string subject,
		string body,
		string level,
		string response,
		long memberId,
		string committeeSlug,
		CancellationToken cancellationToken)
	{
		var form = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["subject"] = subject,
			["body"] = body,
			["level"] = level,
			["response"] = response,
		};

		// Exactly one recipient field is sent. Sending both empty would be
		// refused as "choose one" rather than treated as a broadcast, and
		// sending both filled is refused too — see ISentinelClient.
		if (memberId > 0)
		{
			form["member_id"] = memberId.ToString(System.Globalization.CultureInfo.InvariantCulture);
		}
		else if (committeeSlug.Length > 0)
		{
			form["committee"] = committeeSlug;
		}

		var result = await PostAsync<JsonElement>("alerts", form, token, cancellationToken)
			.ConfigureAwait(false);

		return Collapse(result);
	}

	public async Task<SentinelResult<bool>> ReplyAsync(
		string token, long alertId, string body, CancellationToken cancellationToken)
	{
		var result = await PostAsync<JsonElement>(
			$"alerts/{alertId}/reply",
			new Dictionary<string, string>(StringComparer.Ordinal) { ["body"] = body },
			token,
			cancellationToken).ConfigureAwait(false);

		return Collapse(result);
	}

	public async Task<SentinelResult<bool>> ResendAsync(
		string token, long alertId, CancellationToken cancellationToken)
	{
		var result = await PostAsync<JsonElement>(
			$"alerts/{alertId}/resend",
			new Dictionary<string, string>(StringComparer.Ordinal),
			token,
			cancellationToken).ConfigureAwait(false);

		return Collapse(result);
	}

	public async Task<SentinelResult<string>> GetContactAsync(
		string token, long alertId, CancellationToken cancellationToken)
	{
		var result = await SendAsync<ContactResponse>(
			HttpMethod.Get, $"alerts/{alertId}/contact", body: null, token, cancellationToken)
			.ConfigureAwait(false);

		return result.Success && result.Value is not null
			? SentinelResult<string>.Ok(result.Value.Contact)
			: SentinelResult<string>.Fail(result.Failure, result.Message);
	}

	private Task<SentinelResult<T>> PostAsync<T>(
		string path,
		Dictionary<string, string> body,
		string? token,
		CancellationToken cancellationToken)
	{
		return SendAsync<T>(HttpMethod.Post, path, body, token, cancellationToken);
	}

	private async Task<SentinelResult<T>> SendAsync<T>(
		HttpMethod method,
		string path,
		Dictionary<string, string>? body,
		string? token,
		CancellationToken cancellationToken)
	{
		Uri endpoint;
		try
		{
			endpoint = Endpoint(path);
		}
		catch (InvalidOperationException ex)
		{
			return SentinelResult<T>.Fail(SentinelFailure.NotConfigured, ex.Message);
		}

		using var request = new HttpRequestMessage(method, endpoint);

		if (body is not null)
		{
			// Form encoding rather than JSON: WordPress's REST layer reads
			// form fields into request parameters natively, which is what the
			// controllers' registered `args` validation runs against.
			request.Content = new FormUrlEncodedContent(body);
		}

		if (!string.IsNullOrEmpty(token))
		{
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
		}

		try
		{
			using var response = await _httpClient
				.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken)
				.ConfigureAwait(false);

			if (response.IsSuccessStatusCode)
			{
				var value = await response.Content
					.ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
					.ConfigureAwait(false);

				return value is null
					? SentinelResult<T>.Fail(SentinelFailure.Server, "Sentinel returned an empty response.")
					: SentinelResult<T>.Ok(value);
			}

			return await FailureFromAsync<T>(response, cancellationToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			// A deliberate cancellation — the app is closing, or a poll was
			// superseded. Not a failure worth reporting or logging.
			throw;
		}
		catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
		{
			// Out of signal, DNS failure, timeout. Expected often enough that
			// it is logged at debug: a handset on a train would otherwise fill
			// the log with noise on every poll.
			Log.Debug(ex, "Sentinel request to {Path} could not be completed", path);
			return SentinelResult<T>.Fail(SentinelFailure.Network, "Could not reach the server.");
		}
		catch (JsonException ex)
		{
			// A 200 whose body is not what we expected. Usually a hosting
			// interstitial — a WAF challenge page or a maintenance notice —
			// which is worth seeing in the log because it looks like success.
			Log.Warning(ex, "Sentinel response from {Path} could not be parsed", path);
			return SentinelResult<T>.Fail(SentinelFailure.Server, "The server sent something unexpected.");
		}
	}

	/// <summary>
	/// Turn a non-2xx response into a typed failure, mapping Sentinel's own
	/// error code where there is one.
	/// </summary>
	private static async Task<SentinelResult<T>> FailureFromAsync<T>(
		HttpResponseMessage response, CancellationToken cancellationToken)
	{
		var code = string.Empty;
		var message = string.Empty;

		try
		{
			var error = await response.Content
				.ReadFromJsonAsync<WordPressError>(JsonOptions, cancellationToken)
				.ConfigureAwait(false);

			if (error is not null)
			{
				code = error.Code ?? string.Empty;
				message = error.Message ?? string.Empty;
			}
		}
		catch (Exception ex) when (ex is JsonException or NotSupportedException)
		{
			// Non-JSON error body — an edge WAF or a proxy, not Sentinel. The
			// status code below is all we have to go on.
		}

		var failure = code switch
		{
			"sentinel_not_eligible" => SentinelFailure.NotEligible,
			"sentinel_invalid_credentials" => SentinelFailure.InvalidCredentials,
			"sentinel_device_not_authenticated" => SentinelFailure.Unauthenticated,
			"sentinel_rate_limited" => SentinelFailure.RateLimited,
			// A route this Sentinel does not have, or has but has not been
			// set up for. Matched on WordPress's own code rather than on 404,
			// so an edge proxy's 404 page still reads as a server fault.
			"sentinel_firebase_unavailable" or "rest_no_route" => SentinelFailure.Unavailable,
			_ => response.StatusCode switch
			{
				HttpStatusCode.Unauthorized => SentinelFailure.Unauthenticated,
				HttpStatusCode.Forbidden => SentinelFailure.NotEligible,
				HttpStatusCode.TooManyRequests => SentinelFailure.RateLimited,
				_ => SentinelFailure.Server,
			},
		};

		if (string.IsNullOrWhiteSpace(message))
		{
			message = $"The server refused the request ({(int)response.StatusCode}).";
		}

		return SentinelResult<T>.Fail(failure, message);
	}

	/// <summary>
	/// Reduce a response whose body we do not care about to a plain
	/// success flag, preserving the failure detail.
	/// </summary>
	private static SentinelResult<bool> Collapse<T>(SentinelResult<T> result) =>
		result.Success
			? SentinelResult<bool>.Ok(true)
			: SentinelResult<bool>.Fail(result.Failure, result.Message);

	private Uri Endpoint(string path)
	{
		var configuration = _configuration.GetSentinelConfiguration();
		if (!configuration.IsValid())
		{
			throw new InvalidOperationException(
				"The Sentinel server address has not been set. Add it in Settings.");
		}

		return new Uri(new Uri(configuration.BaseUrl), ApiRoot + path);
	}

	private sealed class PendingAlertsResponse
	{
		[JsonPropertyName("alerts")]
		public List<MonitorAlert> Alerts { get; set; } = [];

		[JsonPropertyName("now")]
		public long Now { get; set; }
	}

	private sealed class ContactResponse
	{
		[JsonPropertyName("alert_id")]
		public long AlertId { get; set; }

		[JsonPropertyName("contact")]
		public string Contact { get; set; } = string.Empty;
	}

	private sealed class MembersResponse
	{
		[JsonPropertyName("members")]
		public List<MonitorMember> Members { get; set; } = [];

		[JsonPropertyName("page")]
		public int Page { get; set; }

		[JsonPropertyName("total")]
		public int Total { get; set; }
	}

	private sealed class CommitteesResponse
	{
		[JsonPropertyName("committees")]
		public List<MonitorCommittee> Committees { get; set; } = [];
	}

	private sealed class WordPressError
	{
		[JsonPropertyName("code")]
		public string? Code { get; set; }

		[JsonPropertyName("message")]
		public string? Message { get; set; }
	}
}
