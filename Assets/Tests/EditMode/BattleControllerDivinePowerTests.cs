using System.Reflection;
using NUnit.Framework;
using XTD.Battle;
using XTD.Content;

namespace XTD.Tests
{
    public sealed class BattleControllerDivinePowerTests
    {
        [Test]
        public void ResolveDivinePowerProfile_AssignsDedicatedProfileToEachHeroClass()
        {
            Assert.That(ResolveProfileName(HeroClassType.BorderCommander), Is.EqualTo("Commander"));
            Assert.That(ResolveProfileName(HeroClassType.SpiritSummoner), Is.EqualTo("Summoner"));
            Assert.That(ResolveProfileName(HeroClassType.ThunderMage), Is.EqualTo("ThunderMage"));
            Assert.That(ResolveProfileName(HeroClassType.TalismanSealer), Is.EqualTo("TalismanSealer"));
        }

        private static string ResolveProfileName(HeroClassType heroClass)
        {
            var method = typeof(BattleController).GetMethod("ResolveDivinePowerProfile", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            var result = method.Invoke(null, new object[] { heroClass });
            return result?.ToString();
        }
    }
}
