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

            for (var floor = 1; floor <= floors; floor++)
            {
                for (var row = 1; row <= rowsPerFloor; row++)
                {
                    var choices = row == 1 || row == 5 || row == 10 ? 1 : random.Next(2, 4);
                    var rowNodes = new List<MapNodeRuntime>(choices);
                    var nonMonsterPlaced = false;
                    for (var i = 0; i < choices; i++)
                    {
                        var nodeType = PickNodeType(random, floor, row, nonMonsterPlaced);
                        if (nodeType != MapNodeType.NormalMonster)
                        {
                            nonMonsterPlaced = true;
                        }

                        rowNodes.Add(new MapNodeRuntime(floor, row, nodeType, string.Empty));
                    }

                    result.Add(rowNodes);
                }
            }

            return result;
        }

        private static MapNodeType PickNodeType(Random random, int floor, int row, bool nonMonsterAlreadyPlaced)
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

            if (row < 5 || nonMonsterAlreadyPlaced)
            {
                return MapNodeType.NormalMonster;
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
