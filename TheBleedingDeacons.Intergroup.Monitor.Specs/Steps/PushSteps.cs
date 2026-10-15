using Reqnroll;
using Shouldly;
using TheBleedingDeacons.Intergroup.Monitor.Models;
using TheBleedingDeacons.Intergroup.Monitor.Specs.Support;

namespace TheBleedingDeacons.Intergroup.Monitor.Specs.Steps;

/// <summary>
/// The push as it comes off FCM: one sealed field, and what Monitor makes
/// of it.
/// </summary>
[Binding]
public sealed class PushSteps(World world)
{
	[Given(@"^this handset was given a payload key at enrolment$")]
	public void HasAKey() => world.Configuration.PayloadKey = SealedPush.NewKey();

	[Given(@"^Sentinel has sealed an alert to this handset$")]
	public void SealedToThisHandset() =>
		world.PushData = SealedPush.Seal(SealedPush.Payload(), world.Configuration.PayloadKey);

	[Given(@"^Sentinel has sealed an alert to somebody else's handset$")]
	public void SealedToAnotherHandset() =>
		world.PushData = SealedPush.Seal(SealedPush.Payload(), SealedPush.NewKey());

	[Given(@"^a push with no ciphertext$")]
	public void NoCiphertext() =>
		world.PushData = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["title"] = "Callback wanted",
		};

	[Given(@"^the sealed payload is tampered with$")]
	public void Tampered()
	{
		var sealedPayload = world.PushData.ShouldNotBeNull()["ciphertext"];
		var bytes = Convert.FromBase64String(sealedPayload);

		// One bit, in the body rather than the envelope. GCM authenticates,
		// so this must fail to open rather than decrypt to something
		// plausible.
		bytes[^1] ^= 0x01;

		world.PushData["ciphertext"] = Convert.ToBase64String(bytes);
	}

	[Given(@"^Sentinel has sealed an alert with no alert id$")]
	public void NoAlertId()
	{
		var payload = SealedPush.Payload();
		payload.Remove("alert_id");

		world.PushData = SealedPush.Seal(payload, world.Configuration.PayloadKey);
	}

	[Given(@"^Sentinel has sealed an alert with no response field$")]
	public void NoResponse()
	{
		var payload = SealedPush.Payload();
		payload.Remove("response");

		world.PushData = SealedPush.Seal(payload, world.Configuration.PayloadKey);
	}

	[Given(@"^Sentinel has sealed an alert carrying ""(.+)"" of ""(.+)""$")]
	public void SealedWithAnExtra(string key, string value)
	{
		var payload = SealedPush.Payload();
		payload[key] = value;

		world.PushData = SealedPush.Seal(payload, world.Configuration.PayloadKey);
	}

	[Given(@"^Sentinel has sealed an alert whose has_contact is ""(.*)""$")]
	public void SealedWithContactFlag(string flag)
	{
		var payload = SealedPush.Payload();
		payload["has_contact"] = flag;

		world.PushData = SealedPush.Seal(payload, world.Configuration.PayloadKey);
	}

	[When(@"^the push is opened$")]
	public void OpenIt()
	{
		world.Opened = MonitorAlert.FromPushData(
			world.PushData.ShouldNotBeNull(), world.Configuration.PayloadKey);

		// So the model steps — level, colour, lock screen — can be asked
		// about a pushed alert as readily as a built one.
		world.Subject = world.Opened;
		world.PushWasOpened = world.Opened is not null;
	}

	[When(@"^the handset reports that it cannot read its alerts$")]
	public async Task ReportUnreadable() => await world.Handset.ReportUnreadableAsync();

	[Then(@"^it opens into alert (\d+)$")]
	public void OpensIntoAlert(long id) => world.Opened.ShouldNotBeNull().Id.ShouldBe(id);

	[Then(@"^nothing was opened$")]
	public void NothingOpened() => world.Opened.ShouldBeNull();

	[Then(@"^the alert's subject is ""(.+)""$")]
	public void SubjectIs(string subject) => world.Opened.ShouldNotBeNull().Title.ShouldBe(subject);

	[Then(@"^the alert's reference is ""(.+)""$")]
	public void ReferenceIs(string reference) =>
		world.Opened.ShouldNotBeNull().Reference.ShouldBe(reference);

	[Then(@"^the alert's button says ""(.+)""$")]
	public void ButtonSays(string label) =>
		world.Opened.ShouldNotBeNull().ActionLabel.ShouldBe(label);

	[Then(@"^the alert's payload has ""(.+)"" of ""(.+)""$")]
	public void PayloadHas(string key, string value) =>
		world.Opened.ShouldNotBeNull().Payload[key].ShouldBe(value);

	[Then(@"^the alert's payload does not carry ""(.+)""$")]
	public void PayloadDoesNotCarry(string key) =>
		world.Opened.ShouldNotBeNull().Payload.ShouldNotContainKey(key);

	[Then(@"^the alert (offers|offers no) contact details$")]
	public void OffersContact(string offers) =>
		world.Opened.ShouldNotBeNull().HasContact
			.ShouldBe(string.Equals(offers, "offers", StringComparison.Ordinal));

	[Then(@"^Sentinel was told this handset cannot read its alerts once$")]
	public void ToldOnce() => world.Sentinel.UnreadableReports.ShouldBe(1);

	[Then(@"^Sentinel was never told this handset cannot read its alerts$")]
	public void NeverTold() => world.Sentinel.UnreadableReports.ShouldBe(0);
}
