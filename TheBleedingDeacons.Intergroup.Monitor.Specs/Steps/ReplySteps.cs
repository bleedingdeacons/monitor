using Reqnroll;
using Shouldly;
using TheBleedingDeacons.Intergroup.Monitor.Models;
using TheBleedingDeacons.Intergroup.Monitor.Specs.Support;

namespace TheBleedingDeacons.Intergroup.Monitor.Specs.Steps;

/// <summary>Replying to an alert, and handing one back to the rota.</summary>
[Binding]
public sealed class ReplySteps(World world)
{
	[Given(@"^Sentinel will refuse the reply$")]
	public void ReplyRefused() =>
		world.Sentinel.ReplyResult = SentinelResult<bool>.Fail(SentinelFailure.Server, "no");

	[Given(@"^Sentinel will refuse the pass-back$")]
	public void ResendRefused() =>
		world.Sentinel.ResendResult = SentinelResult<bool>.Fail(SentinelFailure.Server, "no");

	[Given(@"^a notice about somebody else's acknowledgement$")]
	public void ANotice() => world.Subject = world.Notice(9, "message-1");

	[Given(@"^an alert of kind ""(.+)""$")]
	public void AnAlertOfKind(string kind) =>
		world.Subject = world.Alert(1, kind: kind);

	[When(@"^I reply ""(.*)"" to alert (\d+)$")]
	public async Task Reply(string body, long id) =>
		world.LastCallSucceeded = await world.Handset.ReplyAsync(id, body);

	[When(@"^I pass alert (\d+) back to the rota$")]
	public async Task PassBack(long id)
	{
		var alert = world.OnScreen(id) ?? world.Built[id];

		world.LastCallSucceeded = await world.Handset.ResendAsync(alert);
	}

	[Then(@"^Sentinel was sent ""(.+)"" about alert (\d+)$")]
	public void ReplySent(string body, long id) =>
		world.Sentinel.Replies.ShouldContain((id, body));

	[Then(@"^Sentinel was sent no reply$")]
	public void NoReplySent() => world.Sentinel.Replies.ShouldBeEmpty();

	[Then(@"^the reply was (accepted|refused)$")]
	public void ReplyOutcome(string outcome) =>
		world.LastCallSucceeded.ShouldBe(string.Equals(outcome, "accepted", StringComparison.Ordinal));

	[Then(@"^Sentinel was asked to pass alert (\d+) back$")]
	public void PassBackSent(long id) => world.Sentinel.Resent.ShouldContain(id);
}
