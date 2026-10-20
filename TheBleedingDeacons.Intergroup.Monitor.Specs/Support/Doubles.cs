using TheBleedingDeacons.Intergroup.Monitor.Models;
using TheBleedingDeacons.Intergroup.Monitor.Services.Interfaces;

namespace TheBleedingDeacons.Intergroup.Monitor.Specs.Support;

/// <summary>
/// The handset's surroundings, stood in for.
///
/// <para>Deliberately recording doubles rather than mocks with
/// expectations: a scenario says "the handset rings" and "Sentinel is told",
/// and both of those read better as a question asked of a list afterwards
/// than as an expectation set up before.</para>
///
/// <para>They are a second copy of Monitor.Tests' fakes, and that is
/// unavoidable rather than sloppy: those are <c>internal</c> to a test
/// project, and nothing can reference a test project. Keeping them lean
/// is the mitigation — this file covers what the feature files actually
/// ask for and nothing else.</para>
/// </summary>
public sealed class FakeConfigurationService : IConfigurationService
{
	public SentinelConfiguration Sentinel { get; set; } =
		new() { BaseUrl = "https://example.test/", PollSeconds = 20 };

	public string DeviceToken { get; set; } = "device-token";

	public string PayloadKey { get; set; } = string.Empty;

	public int ClearCount { get; private set; }

	public BetterStackConfiguration BetterStack { get; set; } = new();

	public string DeviceLabel { get; set; } = "Duty handset";

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

/// <summary>An alarm that records rather than making a noise.</summary>
public sealed class FakeAlarm : IAlertAlarm
{
	public List<MonitorAlert> Started { get; } = [];

	/// <summary>Whether each start was asked for silently, in order.</summary>
	public List<bool> StartedSilently { get; } = [];

	public int StopCount { get; private set; }

	public bool IsSounding { get; private set; }

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
/// A presenter that records instead of raising OS notifications, and can
/// be told to throw — the alert loop is required to ring anyway.
/// </summary>
public sealed class FakePresenter : IPlatformAlertPresenter
{
	public List<MonitorAlert> Presented { get; } = [];

	/// <summary>Whether each notification was posted silently, in order.</summary>
	public List<bool> PresentedSilently { get; } = [];

	public List<long> Dismissed { get; } = [];

	public bool ThrowOnPresent { get; set; }

	public Task<bool> RequestPermissionsAsync() => Task.FromResult(true);

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
public sealed class InlineDispatcher : IUiDispatcher
{
	public Task InvokeAsync(Action action)
	{
		ArgumentNullException.ThrowIfNull(action);

		action();

		return Task.CompletedTask;
	}
}

/// <summary>
/// A scripted Sentinel. Each route answers whatever the scenario set on it
/// and records what it was asked, so a feature can drive the handset
/// through outcomes a real server would take a lapsed certification or a
/// tunnel to produce.
/// </summary>
public sealed class FakeSentinelClient : ISentinelClient
{
	public SentinelResult<IReadOnlyList<MonitorAlert>> PendingAlerts { get; set; } =
		SentinelResult<IReadOnlyList<MonitorAlert>>.Ok([]);

	public SentinelResult<DeviceSession> Session { get; set; } =
		SentinelResult<DeviceSession>.Ok(new DeviceSession { Authorised = true });

	public SentinelResult<bool> Acknowledgement { get; set; } = SentinelResult<bool>.Ok(true);

	public SentinelResult<string> Contact { get; set; } = SentinelResult<string>.Ok("Caller — 07700 900000");

	public SentinelResult<bool> ReplyResult { get; set; } = SentinelResult<bool>.Ok(true);

	public SentinelResult<bool> ResendResult { get; set; } = SentinelResult<bool>.Ok(true);

	public SentinelResult<bool> SendResult { get; set; } = SentinelResult<bool>.Ok(true);

	public SentinelResult<MonitorMemberContact> MemberContact { get; set; } =
		SentinelResult<MonitorMemberContact>.Ok(new MonitorMemberContact());

	public List<long> Acknowledged { get; } = [];

	public List<long> ContactsRequested { get; } = [];

	public List<(long AlertId, string Body)> Replies { get; } = [];

	public List<long> Resent { get; } = [];

	public List<(string Subject, string Body, string Level, string Response, long MemberId, string Committee)> Sent { get; } = [];

	public int SessionChecks { get; private set; }

	public int Polls { get; private set; }

	public int UnreadableReports { get; private set; }

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

	public Task<SentinelResult<bool>> UpdatePushTokenAsync(
		string token, string pushProvider, string pushToken, string lockScreen, CancellationToken cancellationToken) =>
		Task.FromResult(SentinelResult<bool>.Ok(true));

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

	public Task<SentinelResult<MonitorMemberContact>> GetMemberContactAsync(
		string token, long memberId, CancellationToken cancellationToken) =>
		Task.FromResult(MemberContact);

	public Task<SentinelResult<bool>> ReportUnreadableAsync(string token, CancellationToken cancellationToken)
	{
		UnreadableReports++;

		return Task.FromResult(SentinelResult<bool>.Ok(true));
	}

	public Task<SentinelResult<IReadOnlyList<MonitorMember>>> GetMembersAsync(
		string token, string search, int page, CancellationToken cancellationToken) =>
		Task.FromResult(SentinelResult<IReadOnlyList<MonitorMember>>.Ok([]));

	public Task<SentinelResult<IReadOnlyList<MonitorCommittee>>> GetCommitteesAsync(
		string token, CancellationToken cancellationToken) =>
		Task.FromResult(SentinelResult<IReadOnlyList<MonitorCommittee>>.Ok([]));

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
/// <para>A store rather than a fake <c>IAlertHistory</c> on purpose: the
/// scenarios that use it are about what gets remembered, and a stubbed
/// history would let them pass while the real one wrote nothing. The
/// actual <c>AlertHistory</c> stays in the path, JSON round trip and
/// all — which is what lets a scenario restart the app.</para>
/// </summary>
public sealed class InMemoryAlertHistoryStore : IAlertHistoryStore
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
