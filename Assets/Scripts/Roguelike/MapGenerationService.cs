using System;
using System.Collections.Generic;
using XTD.Content;

namespace XTD.Roguelike
{
    public sealed class MapGenerationService
    {
        private const int SpecialStartRow = 3;
        private const int EarlySpecialEndRow = 4;
        private const int SpecialEndRow = 9;

        public List<List<MapNodeRuntime>> Generate(int seed, int floors = 3, int rowsPerFloor = 10)
        {
            var random = new Random(seed);
            var result = new List<List<MapNodeRuntime>>();
            var nodeTypes = new List<List<MapNodeType>>();

            var firstFloorHadShop = false;
            for (var floor = 1; floor <= floors; floor++)
            {
                var forceShop = floor == 2 && !firstFloorHadShop;
                var floorRows = GenerateFloorRows(random, floor, rowsPerFloor, forceShop);
                if (floor == 1)
                {
                    firstFloorHadShop = ContainsNodeType(floorRows, MapNodeType.Shop);
                }

                nodeTypes.AddRange(floorRows);
            }

            for (var floor = 1; floor <= floors; floor++)
            {
                for (var row = 1; row <= rowsPerFloor; row++)
                {
                    var flatIndex = ((floor - 1) * rowsPerFloor) + (row - 1);
                    var rowTypes = nodeTypes[flatIndex];
                    var rowNodes = new List<MapNodeRuntime>(rowTypes.Count);
                    var nextLinks = row < rowsPerFloor
                        ? PickNextNodeIndices(random, rowTypes.Count, nodeTypes[flatIndex + 1].Count)
                        : null;
                    for (var i = 0; i < rowTypes.Count; i++)
                    {
                        IEnumerable<int> nextIndices = row < rowsPerFloor
                            ? nextLinks[i]
                            : Array.Empty<int>();
                        rowNodes.Add(new MapNodeRuntime(floor, row, i, rowTypes[i], string.Empty, nextIndices));
                    }

                    result.Add(rowNodes);
                }
            }

            return result;
        }

        private static List<List<MapNodeType>> GenerateFloorRows(Random random, int floor, int rowsPerFloor, bool forceShop)
        {
            var rows = new List<List<MapNodeType>>(rowsPerFloor);
            var specialByRow = PickFloorSpecialRows(random, floor, forceShop);
            var lastSpecialIndex = -1;

            for (var row = 1; row <= rowsPerFloor; row++)
            {
                var fixedType = FixedNodeTypeForRow(floor, row);
                var choices = fixedType.HasValue ? FixedNodeCountForRow(row) : random.Next(2, 4);
                var rowTypes = new List<MapNodeType>(choices);
                if (fixedType.HasValue || row < SpecialStartRow)
                {
                    var nodeType = fixedType ?? MapNodeType.NormalMonster;
                    FillRow(rowTypes, choices, nodeType);
                    rows.Add(rowTypes);
                    continue;
                }

                FillRow(rowTypes, choices, MapNodeType.NormalMonster);
                if (specialByRow.TryGetValue(row, out var specialType))
                {
                    var specialIndex = random.Next(choices);
                    if (choices > 1 && specialIndex == lastSpecialIndex)
                    {
                        specialIndex = (specialIndex + random.Next(1, choices)) % choices;
                    }

                    rowTypes[specialIndex] = specialType;
                    lastSpecialIndex = specialIndex;
                }

                rows.Add(rowTypes);
            }

            return rows;
        }

        private static MapNodeType? FixedNodeTypeForRow(int floor, int row)
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

            return null;
        }

        private static int FixedNodeCountForRow(int row)
        {
            return row is 1 or 5 or 10 ? 1 : 2;
        }

        private static Dictionary<int, MapNodeType> PickFloorSpecialRows(Random random, int floor, bool forceShop)
        {
            var result = new Dictionary<int, MapNodeType>();
            var counts = new Dictionary<MapNodeType, int>();
            var maxSpecials = floor == 1 ? random.Next(4, 6) : random.Next(5, 7);

            var earlyType = random.NextDouble() < 0.65 ? MapNodeType.Opportunity : MapNodeType.Rest;
            AddSpecial(random, result, counts, earlyType, SpecialStartRow, EarlySpecialEndRow);
            if (CountOf(counts, MapNodeType.Opportunity) == 0)
            {
                AddSpecial(random, result, counts, MapNodeType.Opportunity, SpecialStartRow, EarlySpecialEndRow);
            }

            if (forceShop)
            {
                AddSpecial(random, result, counts, MapNodeType.Shop);
            }

            while (result.Count < maxSpecials)
            {
                var nextType = PickWeightedSpecialType(random, floor, counts);
                if (nextType == MapNodeType.NormalMonster || !AddSpecial(random, result, counts, nextType))
                {
                    break;
                }
            }

            return result;
        }

        private static MapNodeType PickWeightedSpecialType(Random random, int floor, Dictionary<MapNodeType, int> counts)
        {
            var totalWeight = 0;
            var choices = new[]
            {
                (type: MapNodeType.Opportunity, weight: CountOf(counts, MapNodeType.Opportunity) >= 2 ? 0 : floor == 1 ? 44 : 36),
                (type: MapNodeType.Shop, weight: CountOf(counts, MapNodeType.Shop) >= 1 ? 0 : floor == 1 ? 18 : 22),
                (type: MapNodeType.Rest, weight: CountOf(counts, MapNodeType.Rest) >= 1 ? 0 : floor == 1 ? 18 : 16),
                (type: MapNodeType.Artifact, weight: CountOf(counts, MapNodeType.Artifact) >= 1 ? 0 : floor == 1 ? 8 : 17),
                (type: MapNodeType.Mystery, weight: CountOf(counts, MapNodeType.Mystery) >= 1 ? 0 : floor == 1 ? 8 : 12)
            };

            foreach (var choice in choices)
            {
                totalWeight += choice.weight;
            }

            if (totalWeight <= 0)
            {
                return MapNodeType.NormalMonster;
            }

            var roll = random.Next(totalWeight);
            foreach (var choice in choices)
            {
                if (choice.weight <= 0)
                {
                    continue;
                }

                if (roll < choice.weight)
                {
                    return choice.type;
                }

                roll -= choice.weight;
            }

            return MapNodeType.Opportunity;
        }

        private static bool AddSpecial(Random random, Dictionary<int, MapNodeType> result, Dictionary<MapNodeType, int> counts, MapNodeType nodeType)
        {
            return AddSpecial(random, result, counts, nodeType, SpecialStartRow, SpecialEndRow);
        }

        private static bool AddSpecial(Random random, Dictionary<int, MapNodeType> result, Dictionary<MapNodeType, int> counts, MapNodeType nodeType, int startRow, int endRow)
        {
            var candidates = new List<int>();
            for (var row = startRow; row <= endRow; row++)
            {
                if (!result.ContainsKey(row) && IsSpecialAllowedOnRow(nodeType, row))
                {
                    candidates.Add(row);
                }
            }

            if (candidates.Count == 0)
            {
                return false;
            }

            var rowIndex = random.Next(candidates.Count);
            result[candidates[rowIndex]] = nodeType;
            counts[nodeType] = CountOf(counts, nodeType) + 1;
            return true;
        }

        private static bool IsSpecialAllowedOnRow(MapNodeType nodeType, int row)
        {
            return nodeType switch
            {
                MapNodeType.Shop => row >= 6,
                MapNodeType.Artifact => row >= 6,
                MapNodeType.Mystery => row >= 6,
                _ => row >= SpecialStartRow
            };
        }

        private static int CountOf(Dictionary<MapNodeType, int> counts, MapNodeType nodeType)
        {
            return counts.TryGetValue(nodeType, out var count) ? count : 0;
        }

        private static bool ContainsNodeType(List<List<MapNodeType>> rows, MapNodeType nodeType)
        {
            foreach (var row in rows)
            {
                if (row.Contains(nodeType))
                {
                    return true;
                }
            }

            return false;
        }

        private static void FillRow(List<MapNodeType> rowTypes, int choices, MapNodeType nodeType)
        {
            for (var i = 0; i < choices; i++)
            {
                rowTypes.Add(nodeType);
            }
        }

        private static List<int>[] PickNextNodeIndices(Random random, int currentCount, int nextCount)
        {
            var links = new List<int>[currentCount];
            for (var i = 0; i < currentCount; i++)
            {
                links[i] = new List<int>();
            }

            if (nextCount <= 0)
            {
                return links;
            }

            for (var nodeIndex = 0; nodeIndex < currentCount; nodeIndex++)
            {
                var projected = ProjectIndex(nodeIndex, currentCount, nextCount);
                var main = JitterIndex(random, projected, nextCount);
                AddUnique(links[nodeIndex], main);

                if (nextCount > 1 && random.NextDouble() < 0.42)
                {
                    var side = random.Next(2) == 0 ? -1 : 1;
                    var extra = main + side;
                    if (extra < 0 || extra >= nextCount)
                    {
                        extra = main - side;
                    }

                    if (extra >= 0 && extra < nextCount)
                    {
                        AddUnique(links[nodeIndex], extra);
                    }
                }
            }

            EnsureEveryNextNodeHasIncoming(random, links, nextCount);

            for (var i = 0; i < links.Length; i++)
            {
                links[i].Sort();
            }

            return links;
        }

        private static void EnsureEveryNextNodeHasIncoming(Random random, List<int>[] links, int nextCount)
        {
            if (links.Length == 0 || nextCount <= 0)
            {
                return;
            }

            var incomingCounts = CountIncoming(links, nextCount);
            for (var target = 0; target < nextCount; target++)
            {
                if (incomingCounts[target] > 0)
                {
                    continue;
                }

                var sourceIndex = PickSourceForMissingIncoming(random, links, incomingCounts, target, nextCount);
                if (sourceIndex < 0)
                {
                    continue;
                }

                var source = links[sourceIndex];
                var replacementIndex = FindReplaceableDuplicateLink(source, incomingCounts);
                if (replacementIndex >= 0)
                {
                    incomingCounts[source[replacementIndex]]--;
                    source[replacementIndex] = target;
                    incomingCounts[target]++;
                    continue;
                }

                AddUnique(source, target);
                incomingCounts[target]++;
            }
        }

        private static int[] CountIncoming(List<int>[] links, int nextCount)
        {
            var incomingCounts = new int[nextCount];
            foreach (var link in links)
            {
                foreach (var target in link)
                {
                    if (target >= 0 && target < nextCount)
                    {
                        incomingCounts[target]++;
                    }
                }
            }

            return incomingCounts;
        }

        private static int PickSourceForMissingIncoming(Random random, List<int>[] links, int[] incomingCounts, int target, int nextCount)
        {
            var maxLinks = links.Length == 1 ? nextCount : Math.Min(2, nextCount);
            var bestIndex = -1;
            var bestScore = int.MaxValue;
            for (var i = 0; i < links.Length; i++)
            {
                if (links[i].Contains(target))
                {
                    return i;
                }

                if (links[i].Count >= maxLinks && FindReplaceableDuplicateLink(links[i], incomingCounts) < 0)
                {
                    continue;
                }

                var projected = ProjectIndex(i, links.Length, nextCount);
                var score = Math.Abs(projected - target) * 10 + links[i].Count + random.Next(0, 2);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            return bestIndex >= 0 ? bestIndex : random.Next(links.Length);
        }

        private static int FindReplaceableDuplicateLink(List<int> link, int[] incomingCounts)
        {
            for (var i = link.Count - 1; i >= 0; i--)
            {
                var target = link[i];
                if (target >= 0 && target < incomingCounts.Length && incomingCounts[target] > 1)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int ProjectIndex(int index, int sourceCount, int targetCount)
        {
            if (targetCount <= 1 || sourceCount <= 1)
            {
                return 0;
            }

            var projected = (int)Math.Round(index * (targetCount - 1) / (double)(sourceCount - 1));
            return Math.Max(0, Math.Min(targetCount - 1, projected));
        }

        private static int JitterIndex(Random random, int projected, int count)
        {
            if (count <= 1)
            {
                return 0;
            }

            var roll = random.NextDouble();
            var offset = roll < 0.25 ? -1 : roll > 0.75 ? 1 : 0;
            var result = projected + offset;
            if (result < 0 || result >= count)
            {
                return projected;
            }

            return result;
        }

        private static void AddUnique(List<int> values, int value)
        {
            if (!values.Contains(value))
            {
                values.Add(value);
            }
        }
    }
}
