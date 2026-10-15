using Reqnroll;
using Shouldly;
using TheBleedingDeacons.Intergroup.Monitor.Specs.Support;

namespace TheBleedingDeacons.Intergroup.Monitor.Specs.Steps;

/// <summary>
/// Pressing the card's one button, whatever it happens to say, and asking
/// Sentinel for the details behind an alert.
/// </summary>
[Binding]
public sealed class AcknowledgementSteps(World world)
{
	[When(@"^I press the button on alert (\d+)$")]
	public async Task PressTheButton(long id)
	{
		// Deliberately whatever is on screen rather than the object the
		// scenario built: the second press acts on a card the loop has
		// already settled, and the state it reads is what decides which of
		// the two things this button does.
		var alert = world.OnScreen(id) ?? world.Built[id];

		await world.Handset.AcknowledgeAsync(alert);
	}

	[When(@"^I acknowledge everything$")]
	public async Task AcknowledgeEverything() => await world.Handset.AcknowledgeAllAsync();

	[When(@"^I ask to see the contact for alert (\d+)$")]
	public async Task AskForContact(long id)
	{
		var alert = world.OnScreen(id) ?? world.Built[id];

		await world.Handset.ShowContactAsync(alert);
	}

	[Then(@"^alert (\d+)'s button says ""(.+)""$")]
	public void ButtonSays(long id, string label) =>
		world.OnScreen(id).ShouldNotBeNull().ActionLabel.ShouldBe(label);

	[Then(@"^alert (\d+) says ""(.+)""$")]
	public void CardSays(long id, string line) =>
		world.OnScreen(id).ShouldNotBeNull().AnsweredLine.ShouldBe(line);

	[Then(@"^alert (\d+) says nothing about having been answered$")]
	public void CardSaysNothing(long id) =>
		world.OnScreen(id).ShouldNotBeNull().AnsweredLine.ShouldBeEmpty();

	[Then(@"^Sentinel was told that alert (\d+) was acknowledged$")]
	public void SentinelWasTold(long id) => world.Sentinel.Acknowledged.ShouldContain(id);

	[Then(@"^Sentinel was told about alert (\d+) once$")]
	public void SentinelWasToldOnce(long id) =>
		world.Sentinel.Acknowledged.Count(a => a == id).ShouldBe(1);

	[Then(@"^Sentinel was told nothing$")]
	public void SentinelWasToldNothing() => world.Sentinel.Acknowledged.ShouldBeEmpty();

	[Then(@"^the contact for alert (\d+) is on screen$")]
	public void ContactShown(long id) =>
		world.OnScreen(id).ShouldNotBeNull().IsContactShown.ShouldBeTrue();

	[Then(@"^Sentinel was asked for alert (\d+)'s contact once$")]
	public void ContactAskedOnce(long id) =>
		world.Sentinel.ContactsRequested.Count(a => a == id).ShouldBe(1);

	[Then(@"^Sentinel was never asked for a contact$")]
	public void ContactNeverAsked() => world.Sentinel.ContactsRequested.ShouldBeEmpty();
}
