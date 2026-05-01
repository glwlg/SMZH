using NUnit.Framework;
using XTD.Content;
using XTD.Roguelike;

namespace XTD.Tests
{
    public sealed class MapGenerationServiceTests
    {
        [Test]
        public void Generate_CreatesThreeFloorsWithFixedKeyRows()
        {
            var service = new MapGenerationService();
            var rows = service.Generate(123, 3, 10);

            Assert.That(rows.Count, Is.EqualTo(30));
            Assert.That(rows[0][0].NodeType, Is.EqualTo(MapNodeType.NormalMonster));
            Assert.That(rows[4][0].NodeType, Is.EqualTo(MapNodeType.EliteMonster));
            Assert.That(rows[9][0].NodeType, Is.EqualTo(MapNodeType.SmallBoss));
            Assert.That(rows[19][0].NodeType, Is.EqualTo(MapNodeType.SmallBoss));
            Assert.That(rows[29][0].NodeType, Is.EqualTo(MapNodeType.FinalBoss));
        }
    }
}
