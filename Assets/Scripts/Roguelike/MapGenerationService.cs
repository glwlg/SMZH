using System;
using System.Collections.Generic;
using XTD.Content;

namespace XTD.Roguelike
{
    public sealed class MapGenerationService
    {
        public List<List<MapNodeRuntime>> Generate(int seed, int floors = 3, int rowsPerFloor = 10)
        {
            var random = new Random(seed);
            var result = new List<List<MapNodeRuntime>>();
            var nodeTypes = new List<List<MapNodeType>>();

            for (var floor = 1; floor <= floors; floor++)
            {
                for (var row = 1; row <= rowsPerFloor; row++)
                {
                    var choices = random.Next(2, 4);
                    var rowTypes = new List<MapNodeType>(choices);
                    for (var i = 0; i < choices; i++)
                    {
                        rowTypes.Add(PickNodeType(random, floor, row, i));
                    }

                    nodeTypes.Add(rowTypes);
                }
            }

            for (var floor = 1; floor <= floors; floor++)
            {
                for (var row = 1; row <= rowsPerFloor; row++)
                {
                    var flatIndex = ((floor - 1) * rowsPerFloor) + (row - 1);
                    var rowTypes = nodeTypes[flatIndex];
                    var rowNodes = new List<MapNodeRuntime>(rowTypes.Count);
                    for (var i = 0; i < rowTypes.Count; i++)
                    {
                        var nextIndices = row < rowsPerFloor
                            ? PickNextNodeIndices(i, rowTypes.Count, nodeTypes[flatIndex + 1].Count)
                            : Array.Empty<int>();
                        rowNodes.Add(new MapNodeRuntime(floor, row, i, rowTypes[i], string.Empty, nextIndices));
                    }

                    result.Add(rowNodes);
                }
            }

            return result;
        }

        private static IReadOnlyList<int> PickNextNodeIndices(int nodeIndex, int currentCount, int nextCount)
        {
            if (nextCount <= 0)
            {
                return Array.Empty<int>();
            }

            var projected = currentCount <= 1
                ? 0
                : (int)Math.Round(nodeIndex * (nextCount - 1) / (double)(currentCount - 1));
            projected = Math.Max(0, Math.Min(nextCount - 1, projected));

            var result = new List<int> { projected };
            if (nextCount > 2 && nodeIndex % 2 == 0 && projected + 1 < nextCount)
            {
                result.Add(projected + 1);
            }
            else if (nextCount > 2 && nodeIndex % 2 == 1 && projected - 1 >= 0)
            {
                result.Add(projected - 1);
            }

            return result;
        }

        private static MapNodeType PickNodeType(Random random, int floor, int row, int choiceIndex)
        {
            if (row == 1)
            {
                return MapNodeType.NormalMonster;
            }

            if (row == 5)
            {
                return MapNodeType.EliteMonster;
            }

            if (row == 10)
            {
                return floor == 3 ? MapNodeType.FinalBoss : MapNodeType.SmallBoss;
            }

            if (row < 5)
            {
                return MapNodeType.NormalMonster;
            }

            if (choiceIndex > 0)
            {
                return MapNodeType.NormalMonster;
            }

            if (row is >= 6 and <= 9)
            {
                var specialIndex = Math.Abs((floor - 1) * 4 + (row - 6)) % 5;
                return specialIndex switch
                {
                    0 => MapNodeType.Shop,
                    1 => MapNodeType.Rest,
                    2 => MapNodeType.Opportunity,
                    3 => MapNodeType.Artifact,
                    _ => MapNodeType.Mystery
                };
            }

            var roll = random.NextDouble();
            if (roll < 0.45) return MapNodeType.NormalMonster;
            if (roll < 0.58) return MapNodeType.Shop;
            if (roll < 0.70) return MapNodeType.Rest;
            if (roll < 0.84) return MapNodeType.Opportunity;
            if (roll < 0.94) return MapNodeType.Artifact;
            return MapNodeType.Mystery;
        }
    }
}
