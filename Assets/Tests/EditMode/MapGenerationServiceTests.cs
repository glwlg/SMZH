using System.Linq;
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
            foreach (var row in rows)
            {
                Assert.That(row.Count, Is.InRange(2, 3));
            }

            Assert.That(rows[0], Has.All.Matches<MapNodeRuntime>(node => node.NodeType == MapNodeType.NormalMonster));
            Assert.That(rows[4], Has.All.Matches<MapNodeRuntime>(node => node.NodeType == MapNodeType.EliteMonster));
            Assert.That(rows[9], Has.All.Matches<MapNodeRuntime>(node => node.NodeType == MapNodeType.SmallBoss));
            Assert.That(rows[19], Has.All.Matches<MapNodeRuntime>(node => node.NodeType == MapNodeType.SmallBoss));
            Assert.That(rows[29], Has.All.Matches<MapNodeRuntime>(node => node.NodeType == MapNodeType.FinalBoss));

            for (var index = 0; index < rows.Count; index++)
            {
                var row = (index % 10) + 1;
                if (row < 5)
                {
                    Assert.That(rows[index], Has.All.Matches<MapNodeRuntime>(node => node.NodeType == MapNodeType.NormalMonster));
                }
            }
        }

        [Test]
        public void Generate_AddsAllSpecialNodeTypesAfterEliteRows()
        {
            var service = new MapGenerationService();
            var rows = service.Generate(123, 3, 10);
            var afterEliteTypes = rows
                .Where(row => row.Count > 0 && row[0].Row > 5 && row[0].Row < 10)
                .SelectMany(row => row)
                .Select(node => node.NodeType)
                .Distinct()
                .ToList();

            Assert.That(afterEliteTypes, Does.Contain(MapNodeType.Shop));
            Assert.That(afterEliteTypes, Does.Contain(MapNodeType.Rest));
            Assert.That(afterEliteTypes, Does.Contain(MapNodeType.Opportunity));
            Assert.That(afterEliteTypes, Does.Contain(MapNodeType.Artifact));
            Assert.That(afterEliteTypes, Does.Contain(MapNodeType.Mystery));
        }

        [Test]
        public void Generate_ConnectsRoomsWithoutFanOutToEveryNextRoom()
        {
            var service = new MapGenerationService();
            var rows = service.Generate(123, 3, 10);

            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                var floorRow = (index % 10) + 1;
                if (floorRow == 10)
                {
                    Assert.That(row, Has.All.Matches<MapNodeRuntime>(node => node.NextNodeIndices.Count == 0));
                    continue;
                }

                var nextRow = rows[index + 1];
                foreach (var node in row)
                {
                    Assert.That(node.NextNodeIndices.Count, Is.InRange(1, 2));
                    Assert.That(node.NextNodeIndices, Has.All.InRange(0, nextRow.Count - 1));
                    Assert.That(node.NextNodeIndices.Count, Is.LessThan(nextRow.Count));
                }
            }
        }
    }
}
