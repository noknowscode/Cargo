using Cargo.Domain;
using NUnit.Framework;

namespace Cargo.Tests;

/// <summary>
/// The display culture is the machine's: numbers in sentences follow it. The data's own times ("Thu 02:30") are
/// read the same way in every culture.
/// </summary>
public class CultureTests
{
    private static int Index(string berth) => PortData.Berths.ToList().FindIndex(b => b.Number == berth);

    [Test]
    [SetCulture("fr-FR")]
    public void A_refusal_writes_the_draft_in_the_machines_culture() =>
        Assert.That(new BerthPlan().Refusal(PortData.Vessel("levant"), Index("02")),
            Is.EqualTo("Berth 02 is too shallow for Levant Express (14,2 m draft · 13 m depth)."));

    [TestCase("fr-FR")]
    [TestCase("ar-SA")]
    [TestCase("fa-IR")]
    public void The_datas_times_read_the_same_in_any_culture(string culture)
    {
        var previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo(culture);
            Assert.That(ShiftClock.ClockHours("Thu 02:30"), Is.EqualTo(26.5));
            Assert.That(PortData.Schedule["levant"], Is.EqualTo((26.5, 58d)));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }
}
