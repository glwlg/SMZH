using NUnit.Framework;
using XTD.Battle;

namespace XTD.Tests
{
    public sealed class MoraleTrackerTests
    {
        [Test]
        public void RegisterSummonedSoldiers_GrantsChargeEveryFiveSoldiers()
        {
            var morale = new MoraleTracker();
            morale.RegisterSummonedSoldiers(4);
            Assert.That(morale.Charges, Is.EqualTo(0));

            morale.RegisterSummonedSoldiers(1);
            Assert.That(morale.Charges, Is.EqualTo(1));

            Assert.That(morale.TryConsume(), Is.True);
            Assert.That(morale.Charges, Is.EqualTo(0));

            morale.RefundCharge();
            Assert.That(morale.Charges, Is.EqualTo(1));
        }
    }
}
