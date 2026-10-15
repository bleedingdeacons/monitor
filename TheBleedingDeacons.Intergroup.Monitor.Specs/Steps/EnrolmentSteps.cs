using Reqnroll;
using Shouldly;
using TheBleedingDeacons.Intergroup.Monitor.Models;
using TheBleedingDeacons.Intergroup.Monitor.Specs.Support;

namespace TheBleedingDeacons.Intergroup.Monitor.Specs.Steps;

/// <summary>
/// The removal notice — the one thing on the alert loop that is an
/// instruction to the app rather than something a responder is being
/// asked to act on.
/// </summary>
[Binding]
public sealed class EnrolmentSteps(World world)
{
	[When(@"^a removal notice arrives$")]
	public async Task RemovalNoticeArrives() =>
		await world.Handset.HandlePushAsync(Removal(expiresAt: 0));

	[When(@"^a removal notice that expired (\d+) minutes ago arrives$")]
	public async Task LateRemovalNoticeArrives(int minutes) =>
		await world.Handset.HandlePushAsync(
			Removal(DateTimeOffset.UtcNow.AddMinutes(-minutes).ToUnixTimeSeconds()));

	[Then(@"^Sentinel was never asked whether this handset is still enrolled$")]
	public void NeverChecked() => world.Sentinel.SessionChecks.ShouldBe(0);

	private MonitorAlert Removal(long expiresAt)
	{
		var notice = world.Alert(
			99,
			MonitorAlert.LevelRed,
			MonitorAlert.ResponseNone,
			MonitorAlert.KindDeviceRemoved,
			expiresAt);

		notice.Title = "This handset has been removed";

		return notice;
	}
}
