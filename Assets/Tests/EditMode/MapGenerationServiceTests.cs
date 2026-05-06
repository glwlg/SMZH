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
            for (var index = 0; index < rows.Count; index++)
            {
                var row = (index % 10) + 1;
                var expectedCount = row is 1 or 5 or 10 ? 1 : -1;
                if (expectedCount > 0)
                {
                    Assert.That(rows[index].Count, Is.EqualTo(expectedCount), $"row {row} should be fixed.");
                }
                else
                {
                    Assert.That(rows[index].Count, Is.InRange(2, 3), $"row {row} should have 2-3 rooms.");
                }
            }

            Assert.That(rows[0], Has.All.Matches<MapNodeRuntime>(node => node.NodeType == MapNodeType.NormalMonster));
            Assert.That(rows[4], Has.All.Matches<MapNodeRuntime>(node => node.NodeType == MapNodeType.EliteMonster));
            Assert.That(rows[9], Has.All.Matches<MapNodeRuntime>(node => node.NodeType == MapNodeType.SmallBoss));
            Assert.That(rows[19], Has.All.Matches<MapNodeRuntime>(node => node.NodeType == MapNodeType.SmallBoss));
            Assert.That(rows[29], Has.All.Matches<MapNodeRuntime>(node => node.NodeType == MapNodeType.FinalBoss));

            for (var index = 0; index < rows.Count; index++)
            {
                var row = (index % 10) + 1;
                if (row < 3)
                {
                    Assert.That(rows[index], Has.All.Matches<MapNodeRuntime>(node => node.NodeType == MapNodeType.NormalMonster));
                }
            }
        }

        [Test]
        public void Generate_AddsEarlyRouteForkBeforeEliteRows()
        {
            var service = new MapGenerationService();
            var rows = service.Generate(123, 3, 10);

            foreach (var floorRows in rows.GroupBy(row => row[0].Floor))
            {
                var earlyRows = floorRows
                    .Where(row => row[0].Row is 3 or 4)
                    .ToList();
                Assert.That(earlyRows.SelectMany(row => row).Any(node => node.NodeType != MapNodeType.NormalMonster), Is.True);
                Assert.That(earlyRows.SelectMany(row => row).Any(node => node.NodeType == MapNodeType.NormalMonster), Is.True);
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
        public void Generate_ConnectsEveryVisibleRoomFromPreviousRows()
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
                    Assert.That(node.NextNodeIndices.Count, Is.InRange(1, nextRow.Count));
                    Assert.That(node.NextNodeIndices, Has.All.InRange(0, nextRow.Count - 1));
                }

                foreach (var nextNode in nextRow)
                {
                    Assert.That(row.Any(node => node.NextNodeIndices.Contains(nextNode.NodeIndex)), Is.True, $"{nextNode.Key} should have an incoming route.");
                }
            }
        }

        [Test]
        public void Generate_EveryRoomCanReachFloorBoss()
        {
            var service = new MapGenerationService();
            var rows = service.Generate(123, 3, 10);

            foreach (var floorRows in rows.GroupBy(row => row[0].Floor))
            {
                var orderedRows = floorRows.OrderBy(row => row[0].Row).ToList();
                foreach (var row in orderedRows)
                {
                    foreach (var node in row)
                    {
                        Assert.That(CanReachFloorEnd(node, orderedRows), Is.True, $"{node.Key} should reach this floor boss.");
                    }
                }
            }
        }

        [Test]
        public void Generate_EveryRoomCanBeReachedFromFloorStart()
        {
            var service = new MapGenerationService();
            var rows = service.Generate(123, 3, 10);

            foreach (var floorRows in rows.GroupBy(row => row[0].Floor))
            {
                var orderedRows = floorRows.OrderBy(row => row[0].Row).ToList();
                foreach (var row in orderedRows)
                {
                    foreach (var node in row)
                    {
                        Assert.That(CanReachFromFloorStart(node, orderedRows), Is.True, $"{node.Key} should be reachable from this floor start.");
                    }
                }
            }
        }

        private static bool CanReachFloorEnd(MapNodeRuntime start, System.Collections.Generic.IReadOnlyList<System.Collections.Generic.List<MapNodeRuntime>> floorRows)
        {
            var queue = new System.Collections.Generic.Queue<MapNodeRuntime>();
            var visited = new System.Collections.Generic.HashSet<string>();
            queue.Enqueue(start);
            visited.Add(start.Key);

            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                if (node.Row == 10)
                {
                    return true;
                }

                var nextRow = floorRows.FirstOrDefault(row => row.Count > 0 && row[0].Row == node.Row + 1);
                if (nextRow == null)
                {
                    continue;
                }

                foreach (var nextIndex in node.NextNodeIndices)
                {
                    var next = nextRow.FirstOrDefault(candidate => candidate.NodeIndex == nextIndex);
                    if (next == null || visited.Contains(next.Key))
                    {
                        continue;
                    }

                    visited.Add(next.Key);
                    queue.Enqueue(next);
                }
            }

            return false;
        }

        private static bool CanReachFromFloorStart(MapNodeRuntime target, System.Collections.Generic.IReadOnlyList<System.Collections.Generic.List<MapNodeRuntime>> floorRows)
        {
            var startRow = floorRows.FirstOrDefault(row => row.Count > 0 && row[0].Row == 1);
            if (startRow == null)
            {
                return false;
            }

            var queue = new System.Collections.Generic.Queue<MapNodeRuntime>();
            var visited = new System.Collections.Generic.HashSet<string>();
            foreach (var node in startRow)
            {
                queue.Enqueue(node);
                visited.Add(node.Key);
            }

            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                if (node.Key == target.Key)
                {
                    return true;
                }

                var nextRow = floorRows.FirstOrDefault(row => row.Count > 0 && row[0].Row == node.Row + 1);
                if (nextRow == null)
                {
                    continue;
                }

                foreach (var nextIndex in node.NextNodeIndices)
                {
                    var next = nextRow.FirstOrDefault(candidate => candidate.NodeIndex == nextIndex);
                    if (next == null || visited.Contains(next.Key))
                    {
                        continue;
                    }

                    visited.Add(next.Key);
                    queue.Enqueue(next);
                }
            }

            return false;
        }
    }
}
