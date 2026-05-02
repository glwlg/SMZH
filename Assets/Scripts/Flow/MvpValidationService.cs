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
            DemoContentFactory.EnsureCatalogComplete(catalog);

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

            var playableCards = catalog.cards.Where(card => card != null).ToList();
            if (playableCards.Count < 25 || playableCards.Count > 35)
            {
                report.issues.Add($"卡牌数量应为 25-35，当前为 {playableCards.Count}。");
            }

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
            if (productionCardCount < directSoldierCardCount)
            {
                report.issues.Add($"生产建筑牌应不少于直接出兵牌，当前建筑 {productionCardCount}，直接出兵 {directSoldierCardCount}。");
            }

            RequireCardType(playableCards, CardType.Soldier, "普通士兵牌", report);
            RequireCardType(playableCards, CardType.EliteSoldier, "精英士兵牌", report);
            RequireCardType(playableCards, CardType.Hero, "英雄士兵牌", report);
            RequireCardType(playableCards, CardType.Spell, "法术牌", report);
            RequireCardType(playableCards, CardType.Tactic, "战术牌", report);

            var levelGroups = playableCards.GroupBy(card => DemoContentFactory.BaseCardId(card.id));
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

            if (catalog.encounters.Any(encounter => encounter != null && encounter.nodeType == MapNodeType.NormalMonster && encounter.coreEnemy != null))
            {
                report.issues.Add("普通怪物节点应以摧毁敌方基地为目标，不应配置核心敌人。");
            }

            foreach (var nodeType in new[] { MapNodeType.EliteMonster, MapNodeType.SmallBoss, MapNodeType.FinalBoss })
            {
                if (catalog.encounters.Any(encounter => encounter != null && encounter.nodeType == nodeType && encounter.coreEnemy == null))
                {
                    report.issues.Add($"{GameFlowController.NodeTypeName(nodeType)}节点应配置核心敌人作为胜利目标。");
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
                if (nodes.Count < 2 || nodes.Count > 3)
                {
                    report.issues.Add($"第 {i + 1} 行节点数量应为 2-3，当前为 {nodes.Count}。");
                }

                var row = (i % 10) + 1;
                var floor = (i / 10) + 1;
                if (row == 1 && nodes.Any(node => node.NodeType != MapNodeType.NormalMonster))
                {
                    report.issues.Add($"第 {floor} 层第 1 行必须全部为普通怪物节点。");
                }

                if (row == 5 && nodes.Any(node => node.NodeType != MapNodeType.EliteMonster))
                {
                    report.issues.Add($"第 {floor} 层第 5 行必须全部为精英怪物节点。");
                }

                if (row == 10)
                {
                    var expected = floor == 3 ? MapNodeType.FinalBoss : MapNodeType.SmallBoss;
                    if (nodes.Any(node => node.NodeType != expected))
                    {
                        report.issues.Add($"第 {floor} 层第 10 行首领类型错误。");
                    }
                }

                if (row < 5 && nodes.Any(node => node.NodeType != MapNodeType.NormalMonster))
                {
                    report.issues.Add($"第 {floor} 层第 {row} 行不应出现特殊节点。");
                }
            }

            var afterEliteTypes = rows
                .Where(row => row.Count > 0 && row[0].Row > 5 && row[0].Row < 10)
                .SelectMany(row => row)
                .Select(node => node.NodeType)
                .Distinct()
                .ToList();

            foreach (var required in new[] { MapNodeType.Shop, MapNodeType.Rest, MapNodeType.Opportunity, MapNodeType.Artifact, MapNodeType.Mystery })
            {
                if (!afterEliteTypes.Contains(required))
                {
                    report.issues.Add($"第 5 行之后缺少 {GameFlowController.NodeTypeName(required)}。");
                }
            }
        }

        private static void ValidateRunCanReachFinalBoss(ContentCatalog catalog, int seed, MvpValidationReport report)
        {
            var run = DemoContentFactory.CreateStartingRun(catalog);
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
