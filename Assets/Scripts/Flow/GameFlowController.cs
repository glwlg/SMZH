using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using XTD.Battle;
using XTD.Content;
using XTD.Roguelike;

namespace XTD.Flow
{
    public sealed class GameFlowController : MonoBehaviour
    {
        private const string MainMenuScene = "MainMenu";
        private const string BattleScene = "BattlePrototype";

        private readonly MapGenerationService mapGeneration = new();
        private readonly List<List<MapNodeRuntime>> mapRows = new();
        private bool hasPendingNode;
        private MapNodeRuntime pendingNode;
        private string pendingEncounterId = string.Empty;
        private bool pendingBattleSuppressRewards;
        private PermanentProgressData permanentProgress;

        public static GameFlowController Instance { get; private set; }
        public ContentCatalog Catalog { get; private set; }
        public RunState CurrentRun { get; private set; }
        public IReadOnlyList<List<MapNodeRuntime>> MapRows => mapRows;
        public PermanentProgressData PermanentProgress => permanentProgress ??= PermanentProgressStore.Load();
        public IReadOnlyList<string> PermanentArtifactIds => PermanentProgress.permanentArtifactIds;
        public bool HasActiveRun => CurrentRun != null && !CurrentRun.isComplete && !CurrentRun.isDefeated;
        public bool HasPendingNode => hasPendingNode;
        public MapNodeRuntime PendingNode => pendingNode;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            permanentProgress = PermanentProgressStore.Load();
        }

        private void Start()
        {
            if (SceneManager.GetActiveScene().name == "Boot")
            {
                LoadMainMenu();
            }
        }

        public static GameFlowController EnsureInstance()
        {
            if (Instance != null)
            {
                return Instance;
            }

            var go = new GameObject("游戏流程");
            return go.AddComponent<GameFlowController>();
        }

        public void ConfigureCatalog(ContentCatalog catalog)
        {
            Catalog = catalog ?? Catalog ?? DemoContentFactory.CreateCatalog();
            DemoContentFactory.EnsureCatalogComplete(Catalog);
        }

        public void StartNewRun(ContentCatalog catalog)
        {
            ConfigureCatalog(catalog);
            CurrentRun = DemoContentFactory.CreateStartingRun(Catalog);
            ApplyPermanentStartBonuses(CurrentRun);
            mapRows.Clear();
            mapRows.AddRange(mapGeneration.Generate(CurrentRun.seed));
            SetAvailableNodesForCurrentRow();
            ClearPendingNode();
            LoadMainMenu();
        }

        public void LoadMainMenu()
        {
            SceneManager.LoadScene(MainMenuScene);
        }

        public void ReturnToTitle()
        {
            CurrentRun = null;
            mapRows.Clear();
            ClearPendingNode();
            LoadMainMenu();
        }

        public IReadOnlyList<MapNodeRuntime> CurrentChoices()
        {
            if (CurrentRun == null)
            {
                return Array.Empty<MapNodeRuntime>();
            }

            EnsureMap();
            var index = ((CurrentRun.floor - 1) * 10) + (CurrentRun.row - 1);
            if (index < 0 || index >= mapRows.Count)
            {
                return Array.Empty<MapNodeRuntime>();
            }

            var row = mapRows[index];
            if (CurrentRun.availableNodeIndices.Count == 0)
            {
                return row;
            }

            return row.Where(node => CurrentRun.availableNodeIndices.Contains(node.NodeIndex)).ToList();
        }

        public void SelectNode(MapNodeRuntime node)
        {
            if (!HasActiveRun || node == null || !CurrentChoices().Any(choice => choice.Key == node.Key))
            {
                return;
            }

            pendingNode = node;
            hasPendingNode = true;
            pendingBattleSuppressRewards = false;
            pendingEncounterId = string.Empty;

            if (node.NodeType == MapNodeType.Mystery)
            {
                ResolveMysteryNode(node);
                return;
            }

            if (IsBattleNode(node.NodeType))
            {
                pendingEncounterId = ResolveEncounterId(node);
                SceneManager.LoadScene(BattleScene);
                return;
            }

            CurrentRun.lastMessage = $"进入{NodeTypeName(node.NodeType)}。";
        }

        public EncounterDefinition PendingEncounterOrDefault(ContentCatalog fallbackCatalog)
        {
            ConfigureCatalog(fallbackCatalog);
            var encounter = !string.IsNullOrWhiteSpace(pendingEncounterId) ? Catalog.FindEncounter(pendingEncounterId) : null;
            if (encounter != null)
            {
                return encounter;
            }

            return Catalog.FirstEncounter(MapNodeType.NormalMonster);
        }

        public void CompleteBattle(BattleOutcome outcome, float remainingPlayerHp)
        {
            if (CurrentRun == null)
            {
                SceneManager.LoadScene(BattleScene);
                return;
            }

            if (outcome == BattleOutcome.Defeat)
            {
                CurrentRun.playerHp = 0f;
                CurrentRun.isDefeated = true;
                CurrentRun.lastMessage = "本次探索失败，已返回营地。";
                RecordRunFinished(false, CurrentRun.heroExperience);
                ClearPendingNode();
                LoadMainMenu();
                return;
            }

            CurrentRun.playerHp = Mathf.Max(1f, remainingPlayerHp);
            var encounter = PendingEncounterOrDefault(Catalog);
            var rewardText = pendingBattleSuppressRewards
                ? "凶阵被破，没有额外奖励。"
                : GrantBattleRewards(pendingNode, encounter);
            AdvanceAfterResolvedNode(pendingNode);
            ClearPendingNode();
            CurrentRun.lastMessage = rewardText;
            LoadMainMenu();
        }

        public bool BuyCard(CardDefinition card)
        {
            if (card == null || CurrentRun == null)
            {
                return false;
            }

            var price = CardBuyPrice(card);
            if (CurrentRun.gold < price)
            {
                CurrentRun.lastMessage = "金币不足。";
                return false;
            }

            CurrentRun.gold -= price;
            CurrentRun.deckCardIds.Add(card.id);
            CurrentRun.lastMessage = $"购买了 {card.displayName}，花费 {price} 金币。";
            return true;
        }

        public bool SellCard(string cardId)
        {
            if (string.IsNullOrWhiteSpace(cardId) || CurrentRun == null)
            {
                return false;
            }

            var card = Catalog.FindCard(cardId);
            if (card == null || !CurrentRun.deckCardIds.Remove(cardId))
            {
                return false;
            }

            var price = Mathf.Max(1, CardBuyPrice(card) / 2);
            CurrentRun.gold += price;
            CurrentRun.lastMessage = $"出售了 {card.displayName}，获得 {price} 金币。";
            return true;
        }

        public void LeaveShop()
        {
            ResolvePendingNonBattle("离开商店。");
        }

        public void TakeRestHeal()
        {
            if (CurrentRun == null)
            {
                return;
            }

            var random = RandomForCurrentNode(17);
            var percent = (float)(0.10 + random.NextDouble() * 0.20);
            if (HasArtifact("artifact_taiji_map"))
            {
                percent += 0.10f;
            }

            var maxHp = PlayerMaxHpForRun();
            var heal = Mathf.CeilToInt(maxHp * percent);
            CurrentRun.playerHp = Mathf.Min(maxHp, CurrentRun.playerHp + heal);
            ResolvePendingNonBattle($"休息恢复 {heal} 点生命。");
        }

        public bool UpgradeCardsAtRest(string cardId)
        {
            if (!TryUpgradeCardSet(cardId, out var upgradedName))
            {
                CurrentRun.lastMessage = "没有足够的三张同级卡牌可以合成。";
                return false;
            }

            if (HasArtifact("artifact_taiji_map"))
            {
                CurrentRun.gold += 20;
                upgradedName += "，太极图残卷额外带来 20 金币";
            }

            ResolvePendingNonBattle($"合成升级：{upgradedName}。");
            return true;
        }

        public void ResolveOpportunity()
        {
            if (CurrentRun == null)
            {
                return;
            }

            var random = RandomForCurrentNode(29);
            var roll = random.NextDouble();
            var message = string.Empty;
            if (roll < 0.34)
            {
                var gold = 35 + CurrentRun.floor * 8 + ArtifactBonusOpportunityGold();
                GrantGold(gold);
                message = $"机遇奖励：获得 {gold} 金币。";
            }
            else if (roll < 0.67)
            {
                var card = RandomRewardCard(random, true);
                CurrentRun.deckCardIds.Add(card.id);
                message = $"机遇奖励：获得卡牌 {card.displayName}。";
            }
            else if (TryUpgradeRandomCard(random, out var upgradedName))
            {
                message = $"机遇奖励：{upgradedName} 升级。";
            }
            else
            {
                var card = RandomRewardCard(random, true);
                CurrentRun.deckCardIds.Add(card.id);
                message = $"机遇奖励：获得卡牌 {card.displayName}。";
            }

            ResolvePendingNonBattle(message);
        }

        public void ChooseArtifact(ArtifactDefinition artifact)
        {
            if (artifact == null || CurrentRun == null)
            {
                return;
            }

            if (!CurrentRun.artifactIds.Contains(artifact.id))
            {
                CurrentRun.artifactIds.Add(artifact.id);
            }

            ResolvePendingNonBattle($"获得神器：{artifact.displayName}。");
        }

        public IReadOnlyList<CardDefinition> GenerateShopCards()
        {
            var random = RandomForCurrentNode(41);
            return RandomCards(random, 5, includeUpgraded: true);
        }

        public IReadOnlyList<ArtifactDefinition> GenerateArtifactChoices()
        {
            var random = RandomForCurrentNode(53);
            var candidates = Catalog.artifacts
                .Where(artifact => artifact != null && artifact.id != "artifact_permanent_relic" && !CurrentRun.artifactIds.Contains(artifact.id))
                .OrderBy(_ => random.Next())
                .ToList();
            var count = HasArtifact("artifact_artifact_eye") ? 4 : 3;
            return candidates.Take(count).ToList();
        }

        public IReadOnlyList<IGrouping<string, string>> UpgradableCardGroups()
        {
            if (CurrentRun == null)
            {
                return Array.Empty<IGrouping<string, string>>();
            }

            return CurrentRun.deckCardIds
                .Where(id => DemoContentFactory.CardLevelFromId(id) < 3)
                .GroupBy(id => id)
                .Where(group => group.Count() >= 3)
                .ToList();
        }

        public int CardBuyPrice(CardDefinition card)
        {
            var rarityAdd = card.rarity switch
            {
                CardRarity.Uncommon => 12,
                CardRarity.Rare => 28,
                CardRarity.Epic => 46,
                CardRarity.Legendary => 70,
                _ => 0
            };
            var price = 18 + card.cost * 8 + card.level * 6 + rarityAdd;
            if (HasArtifact("artifact_market_token"))
            {
                price = Mathf.CeilToInt(price * 0.8f);
            }

            return Mathf.Max(1, price);
        }

        public int PlayerExtraCommand()
        {
            var value = 0;
            if (HasArtifact("artifact_long_banner")) value += 5;
            if (HasArtifact("artifact_command_seal")) value += 3;
            if (HasArtifact("artifact_vajra")) value += 8;
            return value;
        }

        public int ExtraMaxMana()
        {
            return HasArtifact("artifact_heaven_seal") ? 2 : 0;
        }

        public float ExtraStartingMana()
        {
            var value = 0f;
            if (HasArtifact("artifact_heaven_seal")) value += 1f;
            if (HasArtifact("artifact_star_sand")) value += 1f;
            return value;
        }

        public int StartingHandBonus()
        {
            return HasArtifact("artifact_command_seal") ? 1 : 0;
        }

        public int MoraleThreshold()
        {
            return HasArtifact("artifact_war_drum") ? 4 : 5;
        }

        public float SpellDamageMultiplier()
        {
            return HasArtifact("artifact_fire_pearl") ? 1.25f : 1f;
        }

        public float UnitMoveSpeedMultiplier()
        {
            return HasArtifact("artifact_cloud_boots") ? 1.10f : 1f;
        }

        public float UnitAttackMultiplier(UnitDefinition unit)
        {
            var multiplier = 1f;
            if (HasArtifact("artifact_dragon_bone") && unit != null && unit.role == UnitRole.Soldier)
            {
                multiplier += 0.12f;
            }

            if (HasArtifact("artifact_battle_scripture") && unit != null && (unit.role == UnitRole.Elite || unit.role == UnitRole.Hero))
            {
                multiplier += 0.20f;
            }

            return multiplier;
        }

        public float PlayerMaxHpForRun()
        {
            var maxHp = 100f;
            if (HasArtifact("artifact_black_tortoise")) maxHp += 20f;
            if (HasArtifact("artifact_vajra")) maxHp += 35f;
            return maxHp;
        }

        public float BattleStartHpBonus()
        {
            return HasArtifact("artifact_jade_bottle") ? 12f : 0f;
        }

        private bool HasArtifact(string id)
        {
            return CurrentRun != null && CurrentRun.artifactIds.Contains(id);
        }

        private void ResolveMysteryNode(MapNodeRuntime node)
        {
            var random = RandomForNode(node, 61);
            if (random.NextDouble() < 0.42)
            {
                pendingBattleSuppressRewards = true;
                pendingEncounterId = "encounter_mystery_punishment";
                CurrentRun.lastMessage = "神秘节点触发凶阵，打赢也没有奖励。";
                SceneManager.LoadScene(BattleScene);
                return;
            }

            var gold = 70 + CurrentRun.floor * 18 + ArtifactBonusOpportunityGold();
            GrantGold(gold);
            var card = RandomRewardCard(random, true);
            CurrentRun.deckCardIds.Add(card.id);
            AdvanceAfterResolvedNode(node);
            ClearPendingNode();
            CurrentRun.lastMessage = $"神秘奖励：获得 {gold} 金币和 {card.displayName}。";
            LoadMainMenu();
        }

        private string GrantBattleRewards(MapNodeRuntime node, EncounterDefinition encounter)
        {
            var gold = encounter != null ? encounter.rewardGold : 20;
            GrantGold(gold);
            var messages = new List<string> { $"战斗胜利，获得 {Mathf.CeilToInt(gold * GoldMultiplier())} 金币" };

            var random = RandomForNode(node, 73);
            var cardCount = node.NodeType switch
            {
                MapNodeType.EliteMonster => random.Next(1, 3),
                MapNodeType.SmallBoss => random.Next(2, 4),
                MapNodeType.FinalBoss => random.Next(3, 6),
                _ => 0
            };

            if (cardCount > 0)
            {
                var cards = RandomCards(random, cardCount, includeUpgraded: node.NodeType != MapNodeType.NormalMonster);
                foreach (var card in cards)
                {
                    CurrentRun.deckCardIds.Add(card.id);
                }

                messages.Add($"获得卡牌：{string.Join("、", cards.Select(card => card.displayName))}");
            }

            if (node.NodeType == MapNodeType.SmallBoss)
            {
                CurrentRun.heroExperience += 12;
                messages.Add("主角经验 +12");
            }

            if (node.NodeType == MapNodeType.FinalBoss)
            {
                CurrentRun.heroExperience += 60;
                if (!CurrentRun.permanentArtifactIds.Contains("artifact_permanent_relic"))
                {
                    CurrentRun.permanentArtifactIds.Add("artifact_permanent_relic");
                }

                messages.Add("主角经验 +60，获得永久神器：通关遗珍");
            }

            return string.Join("；", messages) + "。";
        }

        private void GrantGold(int amount)
        {
            CurrentRun.gold += Mathf.CeilToInt(amount * GoldMultiplier());
        }

        private float GoldMultiplier()
        {
            var multiplier = 1f;
            if (HasArtifact("artifact_field_purse")) multiplier += 0.20f;
            return multiplier;
        }

        private int ArtifactBonusOpportunityGold()
        {
            return HasArtifact("artifact_fox_coin") ? 15 : 0;
        }

        private bool TryUpgradeRandomCard(System.Random random, out string upgradedName)
        {
            upgradedName = string.Empty;
            var group = CurrentRun.deckCardIds
                .Where(id => DemoContentFactory.CardLevelFromId(id) < 3)
                .GroupBy(id => id)
                .Where(candidate => candidate.Count() >= 3)
                .OrderBy(_ => random.Next())
                .FirstOrDefault();
            return group != null && TryUpgradeCardSet(group.Key, out upgradedName);
        }

        private bool TryUpgradeCardSet(string cardId, out string upgradedName)
        {
            upgradedName = string.Empty;
            if (CurrentRun == null || string.IsNullOrWhiteSpace(cardId) || DemoContentFactory.CardLevelFromId(cardId) >= 3)
            {
                return false;
            }

            var removed = 0;
            for (var i = CurrentRun.deckCardIds.Count - 1; i >= 0 && removed < 3; i--)
            {
                if (CurrentRun.deckCardIds[i] != cardId)
                {
                    continue;
                }

                CurrentRun.deckCardIds.RemoveAt(i);
                removed++;
            }

            if (removed < 3)
            {
                for (var i = 0; i < removed; i++)
                {
                    CurrentRun.deckCardIds.Add(cardId);
                }

                return false;
            }

            var upgradedId = DemoContentFactory.UpgradeCardId(cardId);
            CurrentRun.deckCardIds.Add(upgradedId);
            upgradedName = Catalog.FindCard(upgradedId)?.displayName ?? upgradedId;
            return true;
        }

        private IReadOnlyList<CardDefinition> RandomCards(System.Random random, int count, bool includeUpgraded)
        {
            var cards = Catalog.cards
                .Where(card => card != null && (includeUpgraded || card.level == 1))
                .OrderBy(_ => random.Next())
                .Take(count)
                .ToList();
            return cards.Count > 0 ? cards : Catalog.cards.Take(count).ToList();
        }

        private CardDefinition RandomRewardCard(System.Random random, bool includeUpgraded)
        {
            return RandomCards(random, 1, includeUpgraded).First();
        }

        private void ResolvePendingNonBattle(string message)
        {
            if (!hasPendingNode)
            {
                return;
            }

            AdvanceAfterResolvedNode(pendingNode);
            ClearPendingNode();
            CurrentRun.lastMessage = message;
        }

        private void AdvanceAfterResolvedNode(MapNodeRuntime node)
        {
            if (CurrentRun == null)
            {
                return;
            }

            if (!CurrentRun.selectedNodeKeys.Contains(node.Key))
            {
                CurrentRun.selectedNodeKeys.Add(node.Key);
            }

            if (node.NodeType == MapNodeType.FinalBoss && node.Floor >= 3 && node.Row >= 10)
            {
                CurrentRun.isComplete = true;
                RecordRunFinished(true, CurrentRun.heroExperience);
                return;
            }

            CurrentRun.floor = node.Floor;
            CurrentRun.row = node.Row + 1;
            if (CurrentRun.row > 10)
            {
                CurrentRun.floor++;
                CurrentRun.row = 1;
                CurrentRun.availableNodeIndices.Clear();
            }
            else
            {
                CurrentRun.availableNodeIndices.Clear();
                CurrentRun.availableNodeIndices.AddRange(node.NextNodeIndices);
            }

            if (CurrentRun.floor > 3)
            {
                CurrentRun.isComplete = true;
                CurrentRun.availableNodeIndices.Clear();
            }

            if (!CurrentRun.isComplete && CurrentRun.availableNodeIndices.Count == 0)
            {
                SetAvailableNodesForCurrentRow();
            }
        }

        private void ClearPendingNode()
        {
            hasPendingNode = false;
            pendingEncounterId = string.Empty;
            pendingBattleSuppressRewards = false;
            pendingNode = null;
        }

        private void ApplyPermanentStartBonuses(RunState run)
        {
            var profile = PermanentProgress;
            foreach (var artifactId in profile.permanentArtifactIds)
            {
                if (!run.permanentArtifactIds.Contains(artifactId))
                {
                    run.permanentArtifactIds.Add(artifactId);
                }
            }

            if (profile.permanentArtifactIds.Contains("artifact_permanent_relic"))
            {
                run.gold += 20;
                run.playerHp = Mathf.Min(PlayerMaxHpForRun() + 5f, run.playerHp + 5f);
                run.lastMessage = "永久神器通关遗珍生效：本次探索初始金币 +20，生命 +5。";
            }
        }

        private void RecordRunFinished(bool completed, int gainedExperience)
        {
            var profile = PermanentProgress;
            profile.totalRuns++;
            profile.totalHeroExperience += Mathf.Max(0, gainedExperience);
            if (completed)
            {
                profile.completedRuns++;
                UnlockPermanentArtifact("artifact_permanent_relic");
            }
            else
            {
                PermanentProgressStore.Save(profile);
            }
        }

        private void UnlockPermanentArtifact(string artifactId)
        {
            var profile = PermanentProgress;
            if (!profile.permanentArtifactIds.Contains(artifactId))
            {
                profile.permanentArtifactIds.Add(artifactId);
            }

            PermanentProgressStore.Save(profile);
        }

        private void EnsureMap()
        {
            if (CurrentRun == null || mapRows.Count > 0)
            {
                return;
            }

            mapRows.AddRange(mapGeneration.Generate(CurrentRun.seed));
            SetAvailableNodesForCurrentRow();
        }

        private string ResolveEncounterId(MapNodeRuntime node)
        {
            var nodeType = node.NodeType;
            var candidates = Catalog.encounters
                .Where(encounter => encounter != null && encounter.nodeType == nodeType)
                .ToList();
            if (candidates.Count == 0 && nodeType == MapNodeType.Mystery)
            {
                candidates = Catalog.encounters.Where(encounter => encounter != null && encounter.id == "encounter_mystery_punishment").ToList();
            }

            if (candidates.Count == 0)
            {
                candidates = Catalog.encounters.Where(encounter => encounter != null && encounter.nodeType == MapNodeType.NormalMonster).ToList();
            }

            if (candidates.Count == 0)
            {
                return string.Empty;
            }

            var index = Mathf.Abs(CurrentRun.seed + node.Floor * 31 + node.Row * 13 + (int)node.NodeType * 7) % candidates.Count;
            return candidates[index].id;
        }

        private System.Random RandomForCurrentNode(int salt)
        {
            return RandomForNode(hasPendingNode ? pendingNode : new MapNodeRuntime(CurrentRun.floor, CurrentRun.row, 0, MapNodeType.Opportunity, string.Empty), salt);
        }

        private System.Random RandomForNode(MapNodeRuntime node, int salt)
        {
            var seed = CurrentRun != null ? CurrentRun.seed : 12345;
            return new System.Random(seed + node.Floor * 1009 + node.Row * 97 + (int)node.NodeType * 17 + salt);
        }

        private static bool IsBattleNode(MapNodeType nodeType)
        {
            return nodeType is MapNodeType.NormalMonster or MapNodeType.EliteMonster or MapNodeType.SmallBoss or MapNodeType.FinalBoss;
        }

        private void SetAvailableNodesForCurrentRow()
        {
            if (CurrentRun == null)
            {
                return;
            }

            var rowIndex = ((CurrentRun.floor - 1) * 10) + (CurrentRun.row - 1);
            CurrentRun.availableNodeIndices.Clear();
            if (rowIndex < 0 || rowIndex >= mapRows.Count)
            {
                return;
            }

            CurrentRun.availableNodeIndices.AddRange(mapRows[rowIndex].Select(node => node.NodeIndex));
        }

        public static string NodeTypeName(MapNodeType type)
        {
            return type switch
            {
                MapNodeType.NormalMonster => "普通怪物",
                MapNodeType.EliteMonster => "精英怪物",
                MapNodeType.Shop => "商店",
                MapNodeType.Rest => "休息",
                MapNodeType.Opportunity => "机遇",
                MapNodeType.Mystery => "神秘",
                MapNodeType.Artifact => "神器层",
                MapNodeType.SmallBoss => "小首领",
                MapNodeType.FinalBoss => "最终首领",
                _ => "节点"
            };
        }
    }
}
