using System.Collections.Generic;
using System.Linq;
using XTD.Content;
using XTD.Roguelike;

namespace XTD.Flow
{
    public sealed class MvpValidationReport
    {
        public readonly List<string> issues = new();

        public bool Passed => issues.Count == 0;

        public override string ToString()
        {
            return Passed ? "MVP 校验通过" : string.Join("\n", issues);
        }
    }

    public static class MvpValidationService
    {
        public static MvpValidationReport Validate(ContentCatalog catalog, int seed = 12345)
        {
            GameContentFactory.EnsureCatalogComplete(catalog);

            var report = new MvpValidationReport();
            ValidateContent(catalog, report);
            ValidateMap(seed, report);
            ValidateRunCanReachFinalBoss(catalog, seed, report);
            return report;
        }

        private static void ValidateContent(ContentCatalog catalog, MvpValidationReport report)
        {
            if (catalog == null)
            {
                report.issues.Add("内容目录为空。");
                return;
            }

            var allCards = catalog.cards.Where(card => card != null).ToList();
            var countableCardFamilies = allCards
                .GroupBy(card => GameContentFactory.BaseCardId(card.id))
                .Count();
            if (countableCardFamilies < 25)
            {
                report.issues.Add($"卡牌内容族至少应为 25，当前为 {countableCardFamilies}。");
            }

            var playableCards = allCards.Where(card => card.type != CardType.Curse).ToList();
            if (playableCards.All(card => card.type != CardType.Structure || card.unitSpawns.All(spawn => spawn.unit == null || !spawn.unit.ProducesUnits)))
            {
                report.issues.Add("缺少能持续生产士兵的建筑牌。");
            }

            var productionCardCount = playableCards
                .Where(card => card.level == 1)
                .Count(card => card.type == CardType.Structure && card.unitSpawns.Any(spawn => spawn.unit != null && spawn.unit.ProducesUnits));
            var directSoldierCardCount = playableCards
                .Where(card => card.level == 1)
                .Count(card => card.type is CardType.Soldier or CardType.EliteSoldier or CardType.Hero);
            if (productionCardCount < 4)
            {
                report.issues.Add($"生产建筑牌至少需要 4 种，当前为 {productionCardCount}。");
            }

            if (directSoldierCardCount < 4)
            {
                report.issues.Add($"直接出兵牌至少需要 4 种，当前为 {directSoldierCardCount}。");
            }

            RequireCardType(playableCards, CardType.Soldier, "普通士兵牌", report);
            RequireCardType(playableCards, CardType.EliteSoldier, "精英士兵牌", report);
            RequireCardType(playableCards, CardType.Hero, "英雄士兵牌", report);
            RequireCardType(playableCards, CardType.Spell, "法术牌", report);
            RequireCardType(playableCards, CardType.Tactic, "战术牌", report);

            var levelGroups = playableCards
                .GroupBy(card => GameContentFactory.BaseCardId(card.id))
                .Where(group => group.Any(card => GameContentFactory.CardLevelFromId(card.id) > 1));
            foreach (var group in levelGroups)
            {
                var levels = group.Select(card => card.level).OrderBy(level => level).ToList();
                if (!levels.SequenceEqual(new[] { 1, 2, 3 }))
                {
                    report.issues.Add($"卡牌 {group.Key} 缺少 1-3 级完整链。");
                }
            }

            var artifacts = catalog.artifacts.Where(artifact => artifact != null).ToList();
            if (artifacts.Count < 15 || artifacts.Count > 25)
            {
                report.issues.Add($"神器数量应为 15-25，当前为 {artifacts.Count}。");
            }

            RequireEncounterCount(catalog, MapNodeType.NormalMonster, 1, "普通怪物池", report);
            RequireEncounterCount(catalog, MapNodeType.EliteMonster, 3, "精英怪", report);
            RequireEncounterCount(catalog, MapNodeType.SmallBoss, 2, "小首领", report);
            RequireEncounterCount(catalog, MapNodeType.FinalBoss, 1, "最终首领", report);

            var pressurePatterns = catalog.encounters
                .Where(encounter => encounter != null && encounter.nodeType is MapNodeType.EliteMonster or MapNodeType.SmallBoss)
                .Select(encounter => encounter.pressurePattern)
                .Where(pattern => pattern != EncounterPressurePattern.None)
                .Distinct()
                .Count();
            if (pressurePatterns < 3)
            {
                report.issues.Add($"精英和小首领至少需要 3 类压力机制，当前为 {pressurePatterns} 类。");
            }

            if (catalog.encounters.Any(encounter => encounter != null
                    && encounter.nodeType == MapNodeType.FinalBoss
                    && encounter.pressurePattern == EncounterPressurePattern.None))
            {
                report.issues.Add("最终首领必须配置独立压力机制。");
            }

            if (playableCards.Count == 0)
            {
                report.issues.Add("卡牌奖励池不能为空。");
            }

            if (artifacts.Count(artifact => artifact.id != "artifact_permanent_relic") < 3)
            {
                report.issues.Add("神器奖励池至少需要 3 个非永久神器。");
            }

            if (catalog.encounters.Any(encounter => encounter != null && encounter.nodeType == MapNodeType.NormalMonster && encounter.coreEnemy != null))
            {
                report.issues.Add("普通怪物节点应以摧毁敌方基地为目标，不应配置核心敌人。");
            }

            if (catalog.encounters.Any(encounter => encounter != null && encounter.nodeType == MapNodeType.NormalMonster && encounter.enemyBaseMaxHp <= 0f))
            {
                report.issues.Add("普通怪物节点必须有可摧毁的敌方基地血量。");
            }

            foreach (var nodeType in new[] { MapNodeType.EliteMonster, MapNodeType.SmallBoss, MapNodeType.FinalBoss })
            {
                if (catalog.encounters.Any(encounter => encounter != null && encounter.nodeType == nodeType && encounter.coreEnemy == null))
                {
                    report.issues.Add($"{GameFlowController.NodeTypeName(nodeType)}节点应配置核心敌人作为胜利目标。");
                }
            }

            if (catalog.encounters.Any(encounter => encounter != null && encounter.enemySpawns.Count == 0))
            {
                report.issues.Add("所有遭遇至少需要一组敌方派兵配置。");
            }

            foreach (var encounter in catalog.encounters.Where(encounter => encounter != null && encounter.enemySpawns.Count > 0))
            {
                if (encounter.enemySpawns.Any(spawn => spawn != null && spawn.interval < 0f))
                {
                    report.issues.Add($"{encounter.displayName} 的单条派兵间隔不能为负数。");
                }

                if (encounter.enemySpawns.All(spawn => spawn == null || spawn.interval <= 0f))
                {
                    report.issues.Add($"{encounter.displayName} 至少需要一组单条派兵间隔，避免所有敌潮共用同一节奏。");
                }
            }

            ValidateHeroClassStartingDecks(catalog, report);
        }

        private static void ValidateHeroClassStartingDecks(ContentCatalog catalog, MvpValidationReport report)
        {
            foreach (var heroClass in GameContentFactory.AvailableHeroClasses())
            {
                var run = GameContentFactory.CreateStartingRun(catalog, heroClass);
                if (run.deckCardIds.Count < 8)
                {
                    report.issues.Add($"{GameFlowController.HeroClassName(heroClass)} 初始卡组过少，当前 {run.deckCardIds.Count} 张。");
                }

                var fullPool = GameContentFactory.HeroClassCardPoolBaseIds(heroClass);
                if (fullPool.Count == 0)
                {
                    report.issues.Add($"{GameFlowController.HeroClassName(heroClass)} 职业卡包为空。");
                }

                foreach (var baseId in fullPool)
                {
                    if (catalog.FindCard(baseId) == null)
                    {
                        report.issues.Add($"{GameFlowController.HeroClassName(heroClass)} 职业卡包包含不存在的卡牌：{baseId}。");
                    }
                }

                foreach (var cardId in run.deckCardIds)
                {
                    if (catalog.FindCard(cardId) == null)
                    {
                        report.issues.Add($"{GameFlowController.HeroClassName(heroClass)} 初始卡组包含不存在的卡牌：{cardId}。");
                    }

                    if (!fullPool.Contains(GameContentFactory.BaseCardId(cardId)))
                    {
                        report.issues.Add($"{GameFlowController.HeroClassName(heroClass)} 初始卡组卡牌未纳入职业卡包：{cardId}。");
                    }
                }

                var hasProduction = run.deckCardIds
                    .Select(id => catalog.FindCard(id))
                    .Where(card => card != null)
                    .Any(card => card.type == CardType.Structure && card.unitSpawns.Any(spawn => spawn.unit != null && spawn.unit.ProducesUnits));
                if (!hasProduction)
                {
                    report.issues.Add($"{GameFlowController.HeroClassName(heroClass)} 初始卡组缺少生产建筑。");
                }
            }
        }

        private static void ValidateMap(int seed, MvpValidationReport report)
        {
            var rows = new MapGenerationService().Generate(seed);
            if (rows.Count != 30)
            {
                report.issues.Add($"迷宫应为 3 层共 30 行，当前为 {rows.Count} 行。");
                return;
            }

            for (var i = 0; i < rows.Count; i++)
            {
                var nodes = rows[i];
                var row = (i % 10) + 1;
                var floor = (i / 10) + 1;
                var fixedRow = row is 1 or 5 or 10;
                if (fixedRow && nodes.Count != 1)
                {
                    report.issues.Add($"迷宫 {floor} · 房间 {row}/10 应为单个固定房间，当前为 {nodes.Count} 个。");
                }
                else if (!fixedRow && (nodes.Count < 2 || nodes.Count > 3))
                {
                    report.issues.Add($"迷宫 {floor} · 房间 {row}/10 节点数量应为 2-3，当前为 {nodes.Count}。");
                }

                if (row == 1 && nodes.Any(node => node.NodeType != MapNodeType.NormalMonster))
                {
                    report.issues.Add($"迷宫 {floor} 的第一个房间必须为普通怪物节点。");
                }

                if (row == 5 && nodes.Any(node => node.NodeType != MapNodeType.EliteMonster))
                {
                    report.issues.Add($"迷宫 {floor} · 房间 5/10 必须为精英怪物节点。");
                }

                if (row == 10)
                {
                    var expected = floor == 3 ? MapNodeType.FinalBoss : MapNodeType.SmallBoss;
                    if (nodes.Any(node => node.NodeType != expected))
                    {
                        report.issues.Add($"迷宫 {floor} · 房间 10/10 首领类型错误。");
                    }
                }

                if (row < 3 && nodes.Any(node => node.NodeType != MapNodeType.NormalMonster))
                {
                    report.issues.Add($"迷宫 {floor} · 房间 {row}/10 不应出现特殊节点。");
                }
            }

            var afterEliteTypes = rows
                .Where(row => row.Count > 0 && row[0].Row > 5 && row[0].Row < 10)
                .SelectMany(row => row)
                .Select(node => node.NodeType)
                .Distinct()
                .ToList();
            var hasEarlyFork = rows
                .Where(row => row.Count > 0 && row[0].Row is 3 or 4)
                .SelectMany(row => row)
                .Any(node => node.NodeType != MapNodeType.NormalMonster);
            if (!hasEarlyFork)
            {
                report.issues.Add("精英房间前需要至少 1 个特殊分叉，避免前半层只有普通战斗。");
            }

            foreach (var required in new[] { MapNodeType.Shop, MapNodeType.Rest, MapNodeType.Opportunity, MapNodeType.Artifact, MapNodeType.Mystery })
            {
                if (!afterEliteTypes.Contains(required))
                {
                    report.issues.Add($"精英房间之后缺少 {GameFlowController.NodeTypeName(required)}。");
                }
            }

            ValidateEveryRoomCanReachFloorEnd(rows, report);
            ValidateEveryRoomCanBeReachedFromFloorStart(rows, report);
        }

        private static void ValidateEveryRoomCanReachFloorEnd(IReadOnlyList<List<MapNodeRuntime>> rows, MvpValidationReport report)
        {
            foreach (var floorRows in rows.GroupBy(row => row[0].Floor))
            {
                var orderedRows = floorRows.OrderBy(row => row[0].Row).ToList();
                foreach (var row in orderedRows)
                {
                    foreach (var node in row)
                    {
                        if (!CanReachFloorEnd(node, orderedRows))
                        {
                            report.issues.Add($"迷宫第 {node.Floor} 层房间 {node.Row}-{node.NodeIndex} 无法到达本层终点。");
                        }
                    }
                }
            }
        }

        private static bool CanReachFloorEnd(MapNodeRuntime start, IReadOnlyList<List<MapNodeRuntime>> floorRows)
        {
            var queue = new Queue<MapNodeRuntime>();
            var visited = new HashSet<string>();
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

        private static void ValidateEveryRoomCanBeReachedFromFloorStart(IReadOnlyList<List<MapNodeRuntime>> rows, MvpValidationReport report)
        {
            foreach (var floorRows in rows.GroupBy(row => row[0].Floor))
            {
                var orderedRows = floorRows.OrderBy(row => row[0].Row).ToList();
                var startRow = orderedRows.FirstOrDefault(row => row.Count > 0 && row[0].Row == 1);
                if (startRow == null)
                {
                    continue;
                }

                var queue = new Queue<MapNodeRuntime>();
                var visited = new HashSet<string>();
                foreach (var node in startRow)
                {
                    queue.Enqueue(node);
                    visited.Add(node.Key);
                }

                while (queue.Count > 0)
                {
                    var node = queue.Dequeue();
                    var nextRow = orderedRows.FirstOrDefault(row => row.Count > 0 && row[0].Row == node.Row + 1);
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

                foreach (var node in orderedRows.SelectMany(row => row))
                {
                    if (!visited.Contains(node.Key))
                    {
                        report.issues.Add($"迷宫第 {node.Floor} 层房间 {node.Row}-{node.NodeIndex} 没有入口路线。");
                    }
                }
            }
        }

        private static void ValidateRunCanReachFinalBoss(ContentCatalog catalog, int seed, MvpValidationReport report)
        {
            var run = GameContentFactory.CreateStartingRun(catalog);
            var rows = new MapGenerationService().Generate(seed);
            foreach (var nodes in rows)
            {
                var selected = nodes[0];
                run.floor = selected.Floor;
                run.row = selected.Row;

                if (selected.NodeType == MapNodeType.FinalBoss)
                {
                    run.isComplete = true;
                    run.heroExperience += 60;
                    run.permanentArtifactIds.Add("artifact_permanent_relic");
                    break;
                }

                if (selected.NodeType == MapNodeType.SmallBoss)
                {
                    run.heroExperience += 12;
                }
            }

            if (!run.isComplete)
            {
                report.issues.Add("自动路线无法抵达第三层最终首领。");
            }

            if (!run.permanentArtifactIds.Contains("artifact_permanent_relic"))
            {
                report.issues.Add("最终首领通关后没有永久神器。");
            }
        }

        private static void RequireCardType(IReadOnlyCollection<CardDefinition> cards, CardType type, string label, MvpValidationReport report)
        {
            if (cards.All(card => card.type != type))
            {
                report.issues.Add($"缺少{label}。");
            }
        }

        private static void RequireEncounterCount(ContentCatalog catalog, MapNodeType nodeType, int minCount, string label, MvpValidationReport report)
        {
            var count = catalog.encounters.Count(encounter => encounter != null && encounter.nodeType == nodeType);
            if (count < minCount)
            {
                report.issues.Add($"{label}数量不足，至少 {minCount} 个，当前 {count} 个。");
            }
        }
    }
}
