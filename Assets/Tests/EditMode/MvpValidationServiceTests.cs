using NUnit.Framework;
using XTD.Content;
using XTD.Flow;

namespace XTD.Tests
{
    public sealed class MvpValidationServiceTests
    {
        [Test]
        public void Validate_GameContentPassesMvpScope()
        {
            var catalog = GameContentFactory.CreateCatalog();
            var report = MvpValidationService.Validate(catalog);

            Assert.That(report.Passed, Is.True, report.ToString());
        }

        [Test]
        public void Content_FinalBossHasDistinctPressureAndAuthoredSpawnIntervals()
        {
            var catalog = GameContentFactory.CreateCatalog();
            var finalBoss = catalog.FindEncounter("encounter_chaos_lord");

            Assert.That(finalBoss, Is.Not.Null);
            Assert.That(finalBoss.pressurePattern, Is.EqualTo(EncounterPressurePattern.ChaosRift));
            Assert.That(finalBoss.enemySpawns, Has.All.Matches<EnemySpawnEntry>(spawn => spawn.interval > 0f));
        }
    }
}
