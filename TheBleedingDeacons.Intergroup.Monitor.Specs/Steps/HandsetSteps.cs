using Reqnroll;
using Shouldly;
using TheBleedingDeacons.Intergroup.Monitor.Models;
using TheBleedingDeacons.Intergroup.Monitor.Specs.Support;

namespace TheBleedingDeacons.Intergroup.Monitor.Specs.Steps;

/// <summary>
/// The state a scenario puts the handset in before anything arrives, and
/// the questions it asks about the handset itself rather than about one
/// alert.
/// </summary>
[Binding]
public sealed class HandsetSteps(World world)
{
	[Given(@"^this handset is signed in to Sentinel$")]
	public void SignedIn() => world.Configuration.DeviceToken = "device-token";

	[Given(@"^this handset has no device token$")]
	public void NoToken() => world.Configuration.DeviceToken = string.Empty;

	[Given(@"^the handset is in a meeting$")]
	public void InAMeeting() => world.Configuration.Sentinel.InMeeting = true;

	[Given(@"^the handset is not in a meeting$")]
	public void NotInAMeeting() => world.Configuration.Sentinel.InMeeting = false;

	[Given(@"^polling is turned off$")]
	public void PollingOff() => world.Configuration.Sentinel.Poll = false;

	[Given(@"^the notification tray refuses everything$")]
	public void TrayRefuses() => world.Presenter.ThrowOnPresent = true;

	[Given(@"^Sentinel will refuse the acknowledgement$")]
	public void AcknowledgementRefused() =>
		world.Sentinel.Acknowledgement = SentinelResult<bool>.Fail(SentinelFailure.Server, "no");

	[Given(@"^Sentinel cannot be reached at all$")]
	public void SentinelIsUnreachable()
	{
		world.Sentinel.PendingAlerts =
			SentinelResult<IReadOnlyList<MonitorAlert>>.Fail(SentinelFailure.Network, "no route to host");
		world.Sentinel.Session =
			SentinelResult<DeviceSession>.Fail(SentinelFailure.Network, "no route to host");
	}

	// Also a When: a scenario can revoke the token part-way through, after
	// an alert is already on screen, which is the case the sign-out has to
	// clear up after.
	[Given(@"^Sentinel no longer knows this handset$")]
	[When(@"^Sentinel no longer knows this handset$")]
	public void SentinelHasForgottenIt()
	{
		world.Sentinel.Session =
			SentinelResult<DeviceSession>.Fail(SentinelFailure.Unauthenticated, "unknown device");
		world.Sentinel.PendingAlerts =
			SentinelResult<IReadOnlyList<MonitorAlert>>.Fail(SentinelFailure.Unauthenticated, "unknown device");
	}

	[Given(@"^this responder is no longer a certified telephone responder$")]
	public void NotEligible()
	{
		world.Sentinel.Session =
			SentinelResult<DeviceSession>.Fail(SentinelFailure.NotEligible, string.Empty);
		world.Sentinel.PendingAlerts =
			SentinelResult<IReadOnlyList<MonitorAlert>>.Fail(SentinelFailure.NotEligible, string.Empty);
	}

	[When(@"^the handset is silenced$")]
	public async Task Silence() => await world.Handset.SilenceAsync();

	[When(@"^the handset starts listening$")]
	public async Task Start() => await world.Handset.StartAsync();

	[When(@"^the handset stops listening$")]
	public async Task Stop() => await world.Handset.StopAsync();

	[Then(@"^the handset is still signed in$")]
	public void StillSignedIn()
	{
		world.SignedOutReason.ShouldBeNull();
		world.Configuration.DeviceToken.ShouldNotBeEmpty();
	}

	[Then(@"^the handset has signed itself out$")]
	public void SignedOut()
	{
		world.SignedOutReason.ShouldNotBeNull();
		world.Configuration.DeviceToken.ShouldBeEmpty();
	}

	[Then(@"^the responder is told something that mentions ""(.+)""$")]
	public void ToldWhy(string words) =>
		world.SignedOutReason.ShouldNotBeNull().ShouldContain(words);

	[Then(@"^Sentinel was asked whether this handset is still enrolled$")]
	public void SessionChecked() => world.Sentinel.SessionChecks.ShouldBeGreaterThan(0);

	[Then(@"^Sentinel was never asked for pending alerts$")]
	public void NeverPolled() => world.Sentinel.Polls.ShouldBe(0);

	[Then(@"^Sentinel was asked for pending alerts$")]
	public void Polled() => world.Sentinel.Polls.ShouldBeGreaterThan(0);
}
