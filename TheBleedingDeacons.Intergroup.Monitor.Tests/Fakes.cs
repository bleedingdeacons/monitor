using System.Net;
using TheBleedingDeacons.Intergroup.Monitor.Models;
using TheBleedingDeacons.Intergroup.Monitor.Services.Interfaces;

namespace TheBleedingDeacons.Intergroup.Monitor.Tests;

/// <summary>
/// A stub <see cref="HttpMessageHandler"/> that answers from a
/// caller-supplied responder, so a real <c>SentinelClient</c> can be driven
/// without a live server. Records what it was asked for, because half of
/// what these tests check is the request rather than the response.
/// </summary>
internal sealed class StubHttpMessageHandler(
	Func<HttpRequestMessage, (HttpStatusCode Status, string Body)> responder)
	: HttpMessageHandler
{
	/// <summary>Every request that reached the handler, in order.</summary>
	public List<HttpRequestMessage> Requests { get; } = [];

	/// <summary>The form bodies of those requests, in the same order.</summary>
	public List<string> Bodies { get; } = [];

	protected override async Task<HttpResponseMessage> SendAsync(
		HttpRequestMessage request, CancellationToken cancellationToken)
	{
		Requests.Add(request);
		Bodies.Add(request.Content is null
			? string.Empty
			: await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));

		var (status, body) = responder(request);

		return new HttpResponseMessage(status)
		{
			Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
			RequestMessage = request,
		};
	}

	/// <summary>A handler that gives the same answer to everything.</summary>
	public static StubHttpMessageHandler Always(HttpStatusCode status, string body) =>
		new(_ => (status, body));
}

/// <summary>
/// A configuration service backed by fields rather than Preferences and
/// SecureStorage, which are MAUI and therefore out of reach here.
/// </summary>
internal sealed class FakeConfigurationService : IConfigurationService
{
	public SentinelConfiguration Sentinel { get; set; } = new() { BaseUrl = "https://example.test/", PollSeconds = 20 };

	public string DeviceToken { get; set; } = string.Empty;

	public string PayloadKey { get; set; } = string.Empty;

	public int ClearCount { get; private set; }

	public BetterStackConfiguration BetterStack { get; set; } = new();

	public string DeviceLabel { get; set; } = "Test handset";

	public bool AppLockEnabled { get; set; }

	public SentinelConfiguration GetSentinelConfiguration() => Sentinel;

	public Task SaveSentinelConfigurationAsync(SentinelConfiguration configuration)
	{
		Sentinel = configuration.Normalised();
		return Task.CompletedTask;
	}

	public Task<string> GetDeviceTokenAsync() => Task.FromResult(DeviceToken);

	public Task SaveDeviceTokenAsync(string token)
	{
		DeviceToken = token;
		return Task.CompletedTask;
	}

	public Task ClearDeviceTokenAsync()
	{
		ClearCount++;
		DeviceToken = string.Empty;
		return Task.CompletedTask;
	}

	public Task<string> GetPayloadKeyAsync() => Task.FromResult(PayloadKey);

	public Task SavePayloadKeyAsync(string key)
	{
		PayloadKey = key;
		return Task.CompletedTask;
	}

	public Task ClearPayloadKeyAsync()
	{
		PayloadKey = string.Empty;
		return Task.CompletedTask;
	}

	public BetterStackConfiguration GetBetterStackConfiguration() => BetterStack;
}

/// <summary>
/// An alarm that records what it was told to do instead of making a noise.
/// </summary>
internal sealed class FakeAlarm : IAlertAlarm
{
	public List<MonitorAlert> Started { get; } = [];

	public int StopCount { get; private set; }

	public bool IsSounding { get; private set; }

	/// <summary>Whether each start was asked for silently, in order.</summary>
	public List<bool> StartedSilently { get; } = [];

	public Task StartAsync(MonitorAlert alert, bool silent = false)
	{
		Started.Add(alert);
		StartedSilently.Add(silent);
		IsSounding = true;
		return Task.CompletedTask;
	}

	public Task StopAsync()
	{
		StopCount++;
		IsSounding = false;
		return Task.CompletedTask;
	}
}

/// <summary>
/// A presenter that records rather than raising OS notifications, and can
/// be told to throw — the alert loop is required to survive that.
/// </summary>
internal sealed class FakePresenter : IPlatformAlertPresenter
{
	public List<MonitorAlert> Presented { get; } = [];

	public List<long> Dismissed { get; } = [];

	public bool ThrowOnPresent { get; set; }

	public Task<bool> RequestPermissionsAsync() => Task.FromResult(true);

	/// <summary>Whether each notification was posted silently, in order.</summary>
	public List<bool> PresentedSilently { get; } = [];

	public Task PresentAsync(MonitorAlert alert, bool silent = false)
	{
		PresentedSilently.Add(silent);

		if (ThrowOnPresent)
		{
			throw new InvalidOperationException("no notification permission");
		}

		Presented.Add(alert);
		return Task.CompletedTask;
	}

	public Task DismissAsync(long alertId)
	{
		Dismissed.Add(alertId);
		return Task.CompletedTask;
	}
}

/// <summary>
/// Runs the work inline. There is no UI thread in a test host, and the
/// contract only requires the action to have completed by the time the
/// task does.
/// </summary>
internal sealed class InlineDispatcher : IUiDispatcher
{
	public int Invocations { get; private set; }

	public Task InvokeAsync(Action action)
	{
		Invocations++;
		action();
		return Task.CompletedTask;
	}
}

/// <summary>
/// A scripted <see cref="ISentinelClient"/>. Each endpoint hands back
/// whatever the test set on it and records the calls, so the alert loop
/// can be driven through outcomes — a lapsed certification, a 401, a
/// network drop — that a stub HTTP handler would only reach indirectly.
/// </summary>
internal sealed class FakeSentinelClient : ISentinelClient
{
	public SentinelResult<IReadOnlyList<MonitorAlert>> PendingAlerts { get; set; } =
		SentinelResult<IReadOnlyList<MonitorAlert>>.Ok([]);

	public SentinelResult<DeviceSession> Session { get; set; } =
		SentinelResult<DeviceSession>.Ok(new DeviceSession());

	public SentinelResult<bool> Acknowledgement { get; set; } = SentinelResult<bool>.Ok(true);

	public SentinelResult<string> Contact { get; set; } = SentinelResult<string>.Ok("07700 900000");

	public List<long> Acknowledged { get; } = [];

	public List<long> ContactsRequested { get; } = [];

	public int SessionChecks { get; private set; }

	public int Polls { get; private set; }

	public (Uri Start, Uri Callback) BuildSignInUrls(string provider) =>
		(new Uri("https://example.test/start"), new Uri("monitor://auth"));

	public Task<SentinelResult<DeviceSession>> ExchangeCodeAsync(
		string code, string label, string platform, string pushProvider, string pushToken, CancellationToken cancellationToken) =>
		Task.FromResult(Session);

	public Task<SentinelResult<DeviceSession>> SignInWithFirebaseAsync(
		string idToken, string label, string platform, string pushProvider, string pushToken, CancellationToken cancellationToken) =>
		Task.FromResult(Session);

	public Task<SentinelResult<DeviceSession>> SignInWithPasswordAsync(
		string email, string password, string label, string platform, string pushProvider, string pushToken, CancellationToken cancellationToken) =>
		Task.FromResult(Session);

	public Task<SentinelResult<DeviceSession>> GetSessionAsync(string token, CancellationToken cancellationToken)
	{
		SessionChecks++;
		return Task.FromResult(Session);
	}

	/// <summary>The lock-screen state of the last push registration.</summary>
	public string LastLockScreen { get; private set; } = "not called";

	public Task<SentinelResult<bool>> UpdatePushTokenAsync(
		string token,
		string pushProvider,
		string pushToken,
		string lockScreen,
		CancellationToken cancellationToken)
	{
		LastLockScreen = lockScreen;

		return Task.FromResult(SentinelResult<bool>.Ok(true));
	}

	public Task<SentinelResult<bool>> SignOutAsync(string token, CancellationToken cancellationToken) =>
		Task.FromResult(SentinelResult<bool>.Ok(true));

	public Task<SentinelResult<IReadOnlyList<MonitorAlert>>> GetPendingAlertsAsync(
		string token, CancellationToken cancellationToken)
	{
		Polls++;
		return Task.FromResult(PendingAlerts);
	}

	public Task<SentinelResult<bool>> AcknowledgeAsync(string token, long alertId, CancellationToken cancellationToken)
	{
		Acknowledged.Add(alertId);
		return Task.FromResult(Acknowledgement);
	}

	public Task<SentinelResult<string>> GetContactAsync(string token, long alertId, CancellationToken cancellationToken)
	{
		ContactsRequested.Add(alertId);
		return Task.FromResult(Contact);
	}

	/// <summary>How many times the handset said it cannot read its alerts.</summary>
	public int UnreadableReports { get; private set; }

	public Task<SentinelResult<bool>> ReportUnreadableAsync(string token, CancellationToken cancellationToken)
	{
		UnreadableReports++;
		return Task.FromResult(SentinelResult<bool>.Ok(true));
	}

	public SentinelResult<IReadOnlyList<MonitorMember>> Members { get; set; } =
		SentinelResult<IReadOnlyList<MonitorMember>>.Ok([]);

	public SentinelResult<IReadOnlyList<MonitorCommittee>> Committees { get; set; } =
		SentinelResult<IReadOnlyList<MonitorCommittee>>.Ok([]);

	public SentinelResult<bool> ReplyResult { get; set; } = SentinelResult<bool>.Ok(true);

	public SentinelResult<bool> ResendResult { get; set; } = SentinelResult<bool>.Ok(true);

	public SentinelResult<bool> SendResult { get; set; } = SentinelResult<bool>.Ok(true);

	/// <summary>Alert id and body of every reply sent, in order.</summary>
	public List<(long AlertId, string Body)> Replies { get; } = [];

	/// <summary>The alerts passed back to the rota, in order.</summary>
	public List<long> Resent { get; } = [];

	/// <summary>The last search term the member directory was asked for.</summary>
	public string LastSearch { get; private set; } = string.Empty;

	public Task<SentinelResult<IReadOnlyList<MonitorMember>>> GetMembersAsync(
		string token, string search, int page, CancellationToken cancellationToken)
	{
		LastSearch = search;
		return Task.FromResult(Members);
	}

	public Task<SentinelResult<IReadOnlyList<MonitorCommittee>>> GetCommitteesAsync(
		string token, CancellationToken cancellationToken) =>
		Task.FromResult(Committees);

	/// <summary>
	/// What a contact lookup answers. Both numbers, so the default
	/// exercises the case where a preference actually has two things to
	/// choose between.
	/// </summary>
	public SentinelResult<MonitorMemberContact> MemberContact { get; set; } =
		SentinelResult<MonitorMemberContact>.Ok(new MonitorMemberContact
		{
			Id = 1,
			MobileNumber = "07700 900123",
			LandlineNumber = "0117 496 0123",
			PreferredContact = "Mobile",
		});

	/// <summary>
	/// Every member whose numbers were asked for, in order — so a test
	/// can say that the picker asked once, or did not ask at all.
	/// </summary>
	public List<long> MemberContactsRequested { get; } = [];

	public Task<SentinelResult<MonitorMemberContact>> GetMemberContactAsync(
		string token, long memberId, CancellationToken cancellationToken)
	{
		MemberContactsRequested.Add(memberId);
		return Task.FromResult(MemberContact);
	}

	public Task<SentinelResult<bool>> SendAlertAsync(
		string token,
		string subject,
		string body,
		string level,
		string response,
		long memberId,
		string committeeSlug,
		CancellationToken cancellationToken)
	{
		Sent.Add((subject, body, level, response, memberId, committeeSlug));
		return Task.FromResult(SendResult);
	}

	/// <summary>Everything raised from this handset, in order.</summary>
	public List<(string Subject, string Body, string Level, string Response, long MemberId, string Committee)> Sent { get; } = [];

	public Task<SentinelResult<bool>> ReplyAsync(
		string token, long alertId, string body, CancellationToken cancellationToken)
	{
		Replies.Add((alertId, body));
		return Task.FromResult(ReplyResult);
	}

	public Task<SentinelResult<bool>> ResendAsync(string token, long alertId, CancellationToken cancellationToken)
	{
		Resent.Add(alertId);
		return Task.FromResult(ResendResult);
	}
}

/// <summary>
/// Holds the history document in memory instead of on disk.
///
/// <para>Deliberately a store rather than a fake <c>IAlertHistory</c>:
/// the tests that use it are about what the alert loop records, and a
/// stubbed history would let them pass while the real one wrote nothing.
/// This keeps the actual <c>AlertHistory</c> in the path, including its
/// JSON round trip.</para>
/// </summary>
internal sealed class InMemoryAlertHistoryStore : IAlertHistoryStore
{
	public string Contents { get; set; } = string.Empty;

	public int Writes { get; private set; }

	public Task<string> ReadAsync() => Task.FromResult(Contents);

	public Task WriteAsync(string contents)
	{
		Contents = contents;
		Writes++;

		return Task.CompletedTask;
	}
}

/// <summary>A store that cannot be read or written. See AlertHistory.</summary>
internal sealed class BrokenAlertHistoryStore : IAlertHistoryStore
{
	public Task<string> ReadAsync() => throw new IOException("no");

	public Task WriteAsync(string contents) => throw new IOException("no");
}

/// <summary>Shorthand for building alerts in tests.</summary>
internal static class Alerts
{
	/// <summary>
	/// An alert of the kind a responder has to deal with.
	///
	/// <para><b>Red by default, and that is not a shortcut.</b> Most of
	/// this suite is about a handset ringing, being silenced and being
	/// answered, and only red does any of that — yellow is a notification
	/// that can be missed. A fixture that defaulted to yellow would leave
	/// every one of those tests asserting against an alert that was never
	/// going to alarm.</para>
	/// </summary>
	public static MonitorAlert New(
		long id = 1,
		string kind = "shift_uncovered",
		long expiresAt = 0,
		string messageUuid = "",
		string level = MonitorAlert.LevelRed,
		string response = MonitorAlert.ResponseFirst) =>
		new()
		{
			Id = id,
			Kind = kind,
			Source = "trusted",
			Priority = level == MonitorAlert.LevelRed ? "urgent" : "normal",
			Level = level,
			Response = response,
			Title = "Shift uncovered",
			Body = "Nobody is on the helpline.",
			Reference = $"SHIFT-{id}",
			CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
			ExpiresAt = expiresAt,
			MessageUuid = messageUuid,
		};

	/// <summary>
	/// The notice Sentinel sends to everybody else when one handset
	/// acknowledges: the same shape as any other alert, carrying the
	/// message it reports on and who answered as payload properties.
	///
	/// <para><b>Blue and informational, because that is how Sentinel raises
	/// it</b> — see <c>AcknowledgementNotifier</c>. Nothing here
	/// recognises the kind as special any more: it is quiet because it is
	/// blue and it offers Close because nobody has to take it on, exactly
	/// as any other alert with those two fields would.</para>
	/// </summary>
	public static MonitorAlert Notice(
		long id,
		string aboutMessageUuid,
		string by = "Jo B",
		long expiresAt = 0)
	{
		var notice = New(
			id,
			MonitorAlert.KindMessageAcknowledged,
			expiresAt,
			$"notice-{id}",
			MonitorAlert.LevelBlue,
			MonitorAlert.ResponseNone);
		notice.Title = $"{by} acknowledged";
		notice.Body = "Shift uncovered";

		if (aboutMessageUuid.Length > 0)
		{
			notice.Payload[MonitorAlert.PayloadAckMessageUuid] = aboutMessageUuid;
		}

		if (by.Length > 0)
		{
			notice.Payload[MonitorAlert.PayloadAckResponder] = by;
		}

		return notice;
	}
}
