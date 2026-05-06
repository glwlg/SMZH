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
    public sealed class OpportunityEventPreview
    {
        public string title;
        public string story;
        public string reward;
        public string risk;
        public int templateIndex;
    }

    public sealed class GameFlowController : MonoBehaviour
    {
        private const string MainMenuScene = "MainMenu";
        private const string BattleScene = "BattlePrototype";
        private const int OpportunityTemplateCount = 6;
        private const int MysteryTemplateCount = 3;
        private const int ArtifactRefreshCost = 10;
        private const int ShopBaseRerollCost = 8;
        private const int ShopBaseRemoveCost = 58;
        private const int MinimumDeckSizeAfterRemove = 6;
        private const int MaxRunLogEntries = 42;
        private const int MaxPlaytestRecords = 10;

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
        public IReadOnlyList<string> RunEventLog => CurrentRun != null ? CurrentRun.eventLog : Array.Empty<string>();
        public IReadOnlyList<string> RunPlaytestRecords => CurrentRun != null ? CurrentRun.playtestRecords : Array.Empty<string>();
        public bool HasActiveRun => CurrentRun != null && !CurrentRun.isComplete && !CurrentRun.isDefeated;
        public bool HasPendingNode => hasPendingNode;
        public bool HasPendingCardReward => CurrentRun != null && CurrentRun.pendingCardRewardPickCount > 0 && CurrentRun.pendingCardRewardIds.Count > 0;
        public int PendingCardRewardPickCount => CurrentRun != null ? CurrentRun.pendingCardRewardPickCount : 0;
        public int PendingCardRewardSkipGold => CurrentRun != null ? CurrentRun.pendingCardRewardSkipGold : 0;
        public int ShopRerollCost => CurrentRun != null ? DiscountedShopUtilityCost(ShopBaseRerollCost + CurrentRun.shopOfferRerollCount * 5) : ShopBaseRerollCost;
        public int ShopRemoveCost => CurrentRun != null ? DiscountedShopUtilityCost(ShopBaseRemoveCost) : ShopBaseRemoveCost;
        public bool CanRemoveCardAtShop => CurrentRun != null && !CurrentRun.shopRemoveUsed && CurrentRun.deckCardIds.Count > MinimumDeckSizeAfterRemove;
        public int ArtifactRefreshesRemaining => CurrentRun != null ? CurrentRun.artifactRefreshesRemaining : 0;
        public int ArtifactRerollCost => ArtifactRefreshCost;
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
            Catalog = catalog ?? Catalog ?? GameContentFactory.CreateCatalog();
            GameContentFactory.EnsureCatalogComplete(Catalog);
        }

        public void StartNewRun(ContentCatalog catalog)
        {
            StartNewRun(catalog, GameContentFactory.DefaultHeroClass);
        }

        public void StartNewRun(ContentCatalog catalog, HeroClassType heroClass)
        {
            ConfigureCatalog(catalog);
            CurrentRun = GameContentFactory.CreateStartingRun(Catalog, heroClass);
            AssignFloorAffixes(CurrentRun);
            ApplyPermanentStartBonuses(CurrentRun);
            AppendRunLog(CurrentRun.lastMessage);
            mapRows.Clear();
            mapRows.AddRange(mapGeneration.Generate(CurrentRun.seed));
            SetAvailableNodesForCurrentRow();
            ClearPendingNode();
            LoadMainMenu();
        }

        public HeroClassType CurrentHeroClass => CurrentRun != null ? CurrentRun.heroClass : GameContentFactory.DefaultHeroClass;
        public HeroClassDefinition CurrentHeroClassDefinition => GameContentFactory.GetHeroClassDefinition(CurrentHeroClass);

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
                CurrentRun.lastMessage = "进入神秘房间，请选择探查方式。";
                AppendRunLog(CurrentRun.lastMessage);
                return;
            }

            if (IsBattleNode(node.NodeType))
            {
                pendingEncounterId = ResolveEncounterId(node);
                AppendRunLog($"进入{NodeTypeName(node.NodeType)}：迷宫 {node.Floor} · 房间 {node.Row}/10。");
                SceneManager.LoadScene(BattleScene);
                return;
            }

            if (node.NodeType == MapNodeType.Artifact)
            {
                CurrentRun.artifactRefreshesRemaining = 2;
                CurrentRun.artifactOfferRerollCount = 0;
            }
            else if (node.NodeType == MapNodeType.Shop)
            {
                ResetShopState();
                RefillShopOffers();
            }

            CurrentRun.lastMessage = $"进入{NodeTypeName(node.NodeType)}。";
            AppendRunLog(CurrentRun.lastMessage);
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

        public void CompleteBattle(BattleOutcome outcome, float remainingPlayerHp, string playtestSummary = null)
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
                AppendRunLog(CurrentRun.lastMessage);
                AppendPlaytestRecord(playtestSummary, pendingNode);
                RecordRunFinished(false, CurrentRun.heroExperience);
                ClearPendingNode();
                LoadMainMenu();
                return;
            }

            CurrentRun.playerHp = Mathf.Max(1f, remainingPlayerHp);
            var resolvedNode = pendingNode;
            var encounter = PendingEncounterOrDefault(Catalog);
            var rewardText = pendingBattleSuppressRewards
                ? "凶阵被破，没有额外奖励。"
                : GrantBattleRewards(resolvedNode, encounter);
            AppendPlaytestRecord(playtestSummary, resolvedNode);

            if (HasPendingCardReward)
            {
                CurrentRun.lastMessage = rewardText + " 请选择卡牌奖励。";
                AppendRunLog(rewardText, resolvedNode);
                LoadMainMenu();
                return;
            }

            AdvanceAfterResolvedNode(resolvedNode);
            ClearPendingNode();
            CurrentRun.lastMessage = rewardText;
            AppendRunLog(rewardText, resolvedNode);
            LoadMainMenu();
        }

        public bool BuyCard(CardDefinition card)
        {
            if (card == null || CurrentRun == null)
            {
                return false;
            }

            if (hasPendingNode && pendingNode != null && pendingNode.NodeType == MapNodeType.Shop)
            {
                if (!CurrentRun.shopOfferCardIds.Contains(card.id) || CurrentRun.shopBoughtCardIds.Contains(card.id))
                {
                    CurrentRun.lastMessage = "这张牌已经不在当前货架上。";
                    return false;
                }
            }

            var price = CardBuyPrice(card);
            if (CurrentRun.gold < price)
            {
                CurrentRun.lastMessage = "金币不足。";
                return false;
            }

            CurrentRun.gold -= price;
            CurrentRun.deckCardIds.Add(card.id);
            if (hasPendingNode && pendingNode != null && pendingNode.NodeType == MapNodeType.Shop)
            {
                CurrentRun.shopBoughtCardIds.Add(card.id);
            }

            CurrentRun.lastMessage = $"购买了 {card.displayName}，花费 {price} 金币。";
            AppendRunLog(CurrentRun.lastMessage);
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
            AppendRunLog(CurrentRun.lastMessage);
            return true;
        }

        public void LeaveShop()
        {
            ResetShopState();
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
            var options = GenerateOpportunityOptions();
            if (options.Count == 0)
            {
                return;
            }

            ChooseOpportunity(options[0].templateIndex);
        }

        public void ChooseOpportunity(int templateIndex)
        {
            if (CurrentRun == null)
            {
                return;
            }

            var random = RandomForCurrentNode(29);
            var message = ResolveOpportunityTemplate(templateIndex, random);

            if (HasPendingCardReward)
            {
                CurrentRun.lastMessage = message + " 请选择卡牌奖励。";
                return;
            }

            ResolvePendingNonBattle(message);
        }

        public IReadOnlyList<OpportunityEventPreview> GenerateOpportunityOptions()
        {
            if (CurrentRun == null)
            {
                return Array.Empty<OpportunityEventPreview>();
            }

            var random = RandomForCurrentNode(29);
            return RandomTemplateIndices(random, OpportunityTemplateCount, 3)
                .Select(OpportunityPreview)
                .ToList();
        }

        public OpportunityEventPreview GenerateOpportunityPreview()
        {
            if (CurrentRun == null)
            {
                return new OpportunityEventPreview
                {
                    title = "未知机遇",
                    story = "迷雾尚未散开。",
                    reward = "未知",
                    risk = "未知",
                    templateIndex = 0
                };
            }

            var random = RandomForCurrentNode(29);
            var templateIndex = random.Next(OpportunityTemplateCount);
            return OpportunityPreview(templateIndex);
        }

        public IReadOnlyList<OpportunityEventPreview> GenerateMysteryOptions()
        {
            if (CurrentRun == null)
            {
                return Array.Empty<OpportunityEventPreview>();
            }

            return Enumerable.Range(0, MysteryTemplateCount)
                .Select(MysteryPreview)
                .ToList();
        }

        public void ChooseMystery(int templateIndex)
        {
            if (CurrentRun == null || !hasPendingNode || pendingNode.NodeType != MapNodeType.Mystery)
            {
                return;
            }

            var random = RandomForCurrentNode(61 + templateIndex * 13);
            var message = ResolveMysteryTemplate(templateIndex, random);
            if (!string.IsNullOrWhiteSpace(pendingEncounterId))
            {
                return;
            }

            if (HasPendingCardReward)
            {
                CurrentRun.lastMessage = message + " 请先选择卡牌奖励。";
                return;
            }

            if (hasPendingNode)
            {
                ResolvePendingNonBattle(message);
            }
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

            CurrentRun.artifactRefreshesRemaining = 0;
            CurrentRun.artifactOfferRerollCount = 0;
            ResolvePendingNonBattle($"获得神器：{artifact.displayName}。");
        }

        public IReadOnlyList<CardDefinition> PendingCardRewardChoices()
        {
            if (!HasPendingCardReward)
            {
                return Array.Empty<CardDefinition>();
            }

            return CurrentRun.pendingCardRewardIds
                .Select(id => Catalog.FindCard(id))
                .Where(card => card != null)
                .ToList();
        }

        public void ChooseCardReward(CardDefinition card)
        {
            if (!HasPendingCardReward || card == null)
            {
                return;
            }

            CurrentRun.deckCardIds.Add(card.id);
            CurrentRun.pendingCardRewardIds.Remove(card.id);
            CurrentRun.pendingCardRewardPickCount--;

            if (CurrentRun.pendingCardRewardPickCount > 0 && CurrentRun.pendingCardRewardIds.Count > 0)
            {
                CurrentRun.lastMessage = $"获得 {card.displayName}。还可以选择 {CurrentRun.pendingCardRewardPickCount} 张奖励卡。";
                AppendRunLog($"获得卡牌：{card.displayName}。", pendingNode);
                return;
            }

            FinishPendingCardReward($"获得卡牌奖励：{card.displayName}。");
        }

        public void SkipCardReward()
        {
            if (!HasPendingCardReward)
            {
                return;
            }

            FinishPendingCardReward("放弃剩余卡牌奖励。");
        }

        public void SkipCardRewardForGold()
        {
            if (!HasPendingCardReward)
            {
                return;
            }

            var skipGold = CurrentRun.pendingCardRewardSkipGold;
            if (skipGold <= 0)
            {
                SkipCardReward();
                return;
            }

            GrantGold(skipGold);
            FinishPendingCardReward($"放弃剩余卡牌奖励，换得 {Mathf.CeilToInt(skipGold * GoldMultiplier())} 金币。");
        }

        public IReadOnlyList<CardDefinition> GenerateShopCards()
        {
            if (CurrentRun == null)
            {
                return Array.Empty<CardDefinition>();
            }

            if (CurrentRun.shopOfferCardIds.Count == 0)
            {
                RefillShopOffers();
            }

            return CurrentRun.shopOfferCardIds
                .Where(id => !CurrentRun.shopBoughtCardIds.Contains(id))
                .Select(id => Catalog.FindCard(id))
                .Where(card => card != null)
                .ToList();
        }

        public bool RerollShopCards()
        {
            if (CurrentRun == null)
            {
                return false;
            }

            var cost = ShopRerollCost;
            if (CurrentRun.gold < cost)
            {
                CurrentRun.lastMessage = "金币不足，无法刷新商店货架。";
                return false;
            }

            CurrentRun.gold -= cost;
            CurrentRun.shopOfferRerollCount++;
            CurrentRun.shopOfferCardIds.Clear();
            CurrentRun.shopBoughtCardIds.Clear();
            RefillShopOffers();
            CurrentRun.lastMessage = $"花费 {cost} 金币刷新商店货架。";
            AppendRunLog(CurrentRun.lastMessage);
            return true;
        }

        public bool RemoveCardAtShop(string cardId)
        {
            if (CurrentRun == null || string.IsNullOrWhiteSpace(cardId))
            {
                return false;
            }

            if (CurrentRun.shopRemoveUsed)
            {
                CurrentRun.lastMessage = "本次商店已经净化过一张牌。";
                return false;
            }

            if (CurrentRun.deckCardIds.Count <= MinimumDeckSizeAfterRemove)
            {
                CurrentRun.lastMessage = $"卡组至少保留 {MinimumDeckSizeAfterRemove} 张牌。";
                return false;
            }

            var card = Catalog.FindCard(cardId);
            if (card == null || !CurrentRun.deckCardIds.Contains(cardId))
            {
                CurrentRun.lastMessage = "没有找到要净化的卡牌。";
                return false;
            }

            var cost = ShopRemoveCost;
            if (CurrentRun.gold < cost)
            {
                CurrentRun.lastMessage = "金币不足，无法净化卡牌。";
                return false;
            }

            CurrentRun.gold -= cost;
            CurrentRun.deckCardIds.Remove(cardId);
            CurrentRun.shopRemoveUsed = true;
            CurrentRun.lastMessage = $"净化移除了 {card.displayName}，花费 {cost} 金币。";
            AppendRunLog(CurrentRun.lastMessage);
            return true;
        }

        public IReadOnlyList<ArtifactDefinition> GenerateArtifactChoices()
        {
            var random = RandomForCurrentNode(53 + (CurrentRun != null ? CurrentRun.artifactOfferRerollCount * 101 : 0));
            var candidates = Catalog.artifacts
                .Where(artifact => artifact != null && artifact.id != "artifact_permanent_relic" && !CurrentRun.artifactIds.Contains(artifact.id))
                .OrderBy(_ => random.Next())
                .ToList();
            var count = HasArtifact("artifact_artifact_eye") ? 4 : 3;
            return candidates.Take(count).ToList();
        }

        public bool RerollArtifactChoices()
        {
            if (CurrentRun == null)
            {
                return false;
            }

            if (CurrentRun.artifactRefreshesRemaining <= 0)
            {
                CurrentRun.lastMessage = "神器刷新次数已用完。";
                return false;
            }

            if (CurrentRun.gold < ArtifactRefreshCost)
            {
                CurrentRun.lastMessage = "金币不足，无法刷新神器。";
                return false;
            }

            CurrentRun.gold -= ArtifactRefreshCost;
            CurrentRun.artifactRefreshesRemaining--;
            CurrentRun.artifactOfferRerollCount++;
            CurrentRun.lastMessage = $"消耗 {ArtifactRefreshCost} 金币刷新神器。";
            AppendRunLog(CurrentRun.lastMessage);
            return true;
        }

        public IReadOnlyList<IGrouping<string, string>> UpgradableCardGroups()
        {
            if (CurrentRun == null)
            {
                return Array.Empty<IGrouping<string, string>>();
            }

            return CurrentRun.deckCardIds
                .Where(id => GameContentFactory.CardLevelFromId(id) < 3)
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

        private void RefillShopOffers()
        {
            if (CurrentRun == null)
            {
                return;
            }

            var random = RandomForCurrentNode(41 + CurrentRun.shopOfferRerollCount * 113);
            CurrentRun.shopOfferCardIds.Clear();
            CurrentRun.shopOfferCardIds.AddRange(RandomCards(random, 5, includeUpgraded: true)
                .Where(card => card != null)
                .Select(card => card.id));
        }

        private void ResetShopState()
        {
            if (CurrentRun == null)
            {
                return;
            }

            CurrentRun.shopOfferCardIds.Clear();
            CurrentRun.shopBoughtCardIds.Clear();
            CurrentRun.shopOfferRerollCount = 0;
            CurrentRun.shopRemoveUsed = false;
        }

        private int DiscountedShopUtilityCost(int baseCost)
        {
            var cost = Mathf.Max(1, baseCost);
            if (HasArtifact("artifact_market_token"))
            {
                cost = Mathf.CeilToInt(cost * 0.8f);
            }

            return Mathf.Max(1, cost);
        }

        public int PlayerExtraCommand()
        {
            var value = CurrentHeroClassDefinition.extraCommand;
            if (HasArtifact("artifact_long_banner")) value += 5;
            if (HasArtifact("artifact_command_seal")) value += 3;
            if (HasArtifact("artifact_vajra")) value += 8;
            return Mathf.Max(-8, value);
        }

        public int ExtraMaxMana()
        {
            var value = CurrentHeroClassDefinition.extraMaxMana;
            if (HasArtifact("artifact_heaven_seal")) value += 2;
            return value;
        }

        public float ExtraStartingMana()
        {
            var value = CurrentHeroClassDefinition.extraStartingMana;
            if (HasArtifact("artifact_heaven_seal")) value += 1f;
            if (HasArtifact("artifact_star_sand")) value += 1f;
            return value;
        }

        public int StartingHandBonus()
        {
            var value = CurrentHeroClassDefinition.startingHandBonus;
            if (HasArtifact("artifact_command_seal")) value += 1;
            return value;
        }

        public int MoraleThreshold()
        {
            var threshold = CurrentHeroClassDefinition.moraleThreshold;
            if (HasArtifact("artifact_war_drum")) threshold--;
            return Mathf.Max(3, threshold);
        }

        public float SpellDamageMultiplier()
        {
            var multiplier = 1f + CurrentHeroClassDefinition.spellDamageBonus;
            if (HasArtifact("artifact_fire_pearl")) multiplier += 0.25f;
            if (HasArtifact("artifact_thunder_fire_box")) multiplier += 0.15f;
            if (CurrentFloorAffix() == FloorAffixType.ThunderTribulation) multiplier += 0.08f;
            return multiplier;
        }

        public float UnitMoveSpeedMultiplier()
        {
            return HasArtifact("artifact_cloud_boots") ? 1.10f : 1f;
        }

        public float UnitAttackMultiplier(UnitDefinition unit)
        {
            var multiplier = 1f;
            var definition = CurrentHeroClassDefinition;
            if (unit != null && unit.role == UnitRole.Soldier)
            {
                multiplier += definition.soldierAttackBonus;
            }

            if (unit != null && unit.role == UnitRole.Elite)
            {
                multiplier += definition.eliteAttackBonus;
            }

            if (unit != null && unit.role == UnitRole.Hero)
            {
                multiplier += definition.heroAttackBonus;
            }

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

        public void DebugAddGold(int amount = 100)
        {
            if (CurrentRun == null)
            {
                return;
            }

            CurrentRun.gold += Mathf.Max(1, amount);
            CurrentRun.lastMessage = $"调试：金币 +{amount}。";
        }

        public void GainGoldDuringBattle(int amount)
        {
            if (CurrentRun == null)
            {
                return;
            }

            GrantGold(Mathf.Max(1, amount));
            CurrentRun.lastMessage = $"战斗中获得 {Mathf.CeilToInt(Mathf.Max(1, amount) * GoldMultiplier())} 金币。";
            AppendRunLog(CurrentRun.lastMessage);
        }

        public void DebugOpenCardReward()
        {
            if (CurrentRun == null)
            {
                return;
            }

            var random = RandomForCurrentNode(89);
            QueueCardReward(RandomCards(random, 3, includeUpgraded: true), 1);
            CurrentRun.lastMessage = "调试：打开三选一卡牌奖励。";
            LoadMainMenu();
        }

        public void DebugSkipPendingNode()
        {
            if (CurrentRun == null)
            {
                return;
            }

            if (hasPendingNode)
            {
                AdvanceAfterResolvedNode(pendingNode);
                ClearPendingNode();
                CurrentRun.lastMessage = "调试：已跳过当前房间。";
            }
            else
            {
                SetAvailableNodesForCurrentRow();
                CurrentRun.lastMessage = "调试：当前没有待处理房间。";
            }

            LoadMainMenu();
        }

        public void DebugJumpToBoss()
        {
            DebugJumpTo(10, "调试：已跳到本层首领前。");
        }

        public void DebugJumpToFinalBoss()
        {
            if (CurrentRun == null)
            {
                return;
            }

            EnsureMap();
            ClearPendingNode();
            ClearPendingCardReward();
            CurrentRun.floor = 3;
            CurrentRun.row = 10;
            SetAvailableNodesForCurrentRow();
            CurrentRun.lastMessage = "调试：已跳到最终首领前。";
        }

        public void DebugGrantRandomArtifact()
        {
            if (CurrentRun == null)
            {
                return;
            }

            var random = RandomForCurrentNode(191 + CurrentRun.artifactIds.Count * 17);
            if (TryGrantRandomArtifact(random, out var artifactName))
            {
                CurrentRun.lastMessage = $"调试：获得神器 {artifactName}。";
            }
            else
            {
                CurrentRun.lastMessage = "调试：没有可获得的新神器。";
            }
        }

        public void DebugHealToFull()
        {
            if (CurrentRun == null)
            {
                return;
            }

            CurrentRun.playerHp = PlayerMaxHpForRun();
            CurrentRun.lastMessage = "调试：生命已回满。";
        }

        public int PermanentHeroLevel()
        {
            return HeroLevelForExperience(PermanentProgress.totalHeroExperience);
        }

        public int CurrentRunPreviewHeroLevel()
        {
            var runExperience = CurrentRun != null ? CurrentRun.heroExperience : 0;
            return HeroLevelForExperience(PermanentProgress.totalHeroExperience + runExperience);
        }

        public int ExperienceForNextHeroLevel()
        {
            var level = CurrentRunPreviewHeroLevel();
            return ExperienceRequiredForLevel(level + 1);
        }

        public float PlayerMaxHpForRun()
        {
            var maxHp = CurrentHeroClassDefinition.startingHp;
            maxHp += Mathf.Max(0, PermanentHeroLevel() - 1) * 2f;
            if (PermanentProgress.permanentArtifactIds.Contains("artifact_permanent_relic") ||
                (CurrentRun != null && CurrentRun.permanentArtifactIds.Contains("artifact_permanent_relic")))
            {
                maxHp += 5f;
            }

            if (HasArtifact("artifact_black_tortoise")) maxHp += 20f;
            if (HasArtifact("artifact_vajra")) maxHp += 35f;
            return maxHp;
        }

        public float BattleStartHpBonus()
        {
            return HasArtifact("artifact_jade_bottle") ? 12f : 0f;
        }

        public FloorAffixType CurrentFloorAffix()
        {
            if (CurrentRun == null || CurrentRun.floorAffixes.Count == 0)
            {
                return FloorAffixType.None;
            }

            var index = Mathf.Clamp(CurrentRun.floor - 1, 0, CurrentRun.floorAffixes.Count - 1);
            return CurrentRun.floorAffixes[index];
        }

        public string CurrentFloorAffixName()
        {
            return FloorAffixName(CurrentFloorAffix());
        }

        public string CurrentFloorAffixDescription()
        {
            return FloorAffixDescription(CurrentFloorAffix());
        }

        public int CardCostModifier(CardDefinition card)
        {
            if (card == null || card.type == CardType.Curse)
            {
                return 0;
            }

            var modifier = CurrentHeroClassDefinition.CostModifierFor(card);

            if (HasArtifact("artifact_ten_thousand_banner"))
            {
                if (card.type == CardType.Structure) modifier -= 1;
                if (card.type == CardType.Spell) modifier += 1;
            }

            if (HasArtifact("artifact_thunder_fire_box"))
            {
                if (card.type == CardType.Spell) modifier -= 1;
                if (card.type == CardType.Structure) modifier += 1;
            }

            if (CurrentFloorAffix() == FloorAffixType.DemonFog && card.type == CardType.Spell)
            {
                modifier += 1;
            }

            return modifier;
        }

        public float EffectRadiusMultiplierForCard(CardDefinition card)
        {
            if (card == null || (card.type != CardType.Spell && card.type != CardType.Debuff))
            {
                return 1f;
            }

            return Mathf.Max(0.01f, CurrentHeroClassDefinition.effectRadiusMultiplier);
        }

        public float StructureProductionIntervalMultiplier()
        {
            var multiplier = CurrentHeroClassDefinition.structureProductionIntervalMultiplier;
            if (HasArtifact("artifact_ten_thousand_banner")) multiplier *= 0.65f;
            if (CurrentFloorAffix() == FloorAffixType.ImmortalArray) multiplier *= 0.88f;
            return multiplier;
        }

        public int MoraleFromStructureCard(CardDefinition card)
        {
            return card != null && card.type == CardType.Structure && HasArtifact("artifact_general_platform") ? 1 : 0;
        }

        public bool ConvertCurseToMorale()
        {
            return HasArtifact("artifact_curse_gourd");
        }

        public float UnitRangeMultiplier(UnitDefinition unit, Faction faction)
        {
            if (unit == null)
            {
                return 1f;
            }

            var multiplier = 1f;
            if (CurrentFloorAffix() == FloorAffixType.DemonFog && unit.IsRanged)
            {
                multiplier *= faction == Faction.Player ? 0.78f : 0.88f;
            }

            return multiplier;
        }

        public float UnitMaxHpMultiplier(UnitDefinition unit, Faction faction)
        {
            if (unit == null)
            {
                return 1f;
            }

            var multiplier = 1f;
            if (CurrentFloorAffix() == FloorAffixType.ImmortalArray && faction == Faction.Player && unit.role == UnitRole.Structure)
            {
                multiplier += 0.20f;
            }

            return multiplier;
        }

        public float EnemySpawnIntervalMultiplier()
        {
            return CurrentFloorAffix() == FloorAffixType.DemonTide ? 0.82f : 1f;
        }

        public float ManaRegenMultiplier()
        {
            var multiplier = CurrentHeroClassDefinition.manaRegenMultiplier;
            if (CurrentFloorAffix() == FloorAffixType.ImmortalArray) multiplier *= 0.92f;
            return multiplier;
        }

        public float FloorLightningDamage()
        {
            return CurrentFloorAffix() == FloorAffixType.ThunderTribulation ? 12f + CurrentRun.floor * 3f : 0f;
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
            var cards = RandomCards(random, 3, includeUpgraded: true, preferHighQuality: true);
            QueueCardReward(cards, 1);
            CurrentRun.lastMessage = $"神秘奖励：获得 {Mathf.CeilToInt(gold * GoldMultiplier())} 金币，并从 3 张高质量卡中选择 1 张。";
            LoadMainMenu();
        }

        private string GrantBattleRewards(MapNodeRuntime node, EncounterDefinition encounter)
        {
            var gold = encounter != null ? encounter.rewardGold : 20;
            GrantGold(gold);
            var messages = new List<string> { $"战斗胜利，获得 {Mathf.CeilToInt(gold * GoldMultiplier())} 金币" };

            var random = RandomForNode(node, 73);
            var cardChoiceCount = node.NodeType switch
            {
                MapNodeType.NormalMonster => 3,
                MapNodeType.EliteMonster => 3,
                MapNodeType.SmallBoss => 4,
                MapNodeType.FinalBoss => 5,
                MapNodeType.Mystery => 3,
                _ => 0
            };
            var pickCount = node.NodeType switch
            {
                MapNodeType.SmallBoss => 2,
                MapNodeType.FinalBoss => 3,
                MapNodeType.EliteMonster => 1,
                MapNodeType.NormalMonster => 1,
                MapNodeType.Mystery => 1,
                _ => 0
            };

            if (cardChoiceCount > 0)
            {
                var rewardTier = RewardTierForNode(node.NodeType);
                var includeUpgraded = node.NodeType != MapNodeType.NormalMonster;
                var cards = RandomCards(random, cardChoiceCount, includeUpgraded, rewardTier);
                var skipGold = node.NodeType == MapNodeType.NormalMonster ? 8 : 0;
                QueueCardReward(cards, pickCount, skipGold);
                var rewardHint = includeUpgraded ? "卡牌" : "等级 1 卡牌";
                messages.Add($"卡牌奖励：从 {cards.Count} 张{rewardHint}中选择 {pickCount} 张");
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

        private void QueueCardReward(IReadOnlyList<CardDefinition> cards, int pickCount, int skipGold = 0)
        {
            CurrentRun.pendingCardRewardIds.Clear();
            CurrentRun.pendingCardRewardIds.AddRange(cards.Where(card => card != null).Select(card => card.id));
            CurrentRun.pendingCardRewardPickCount = Mathf.Min(Mathf.Max(1, pickCount), CurrentRun.pendingCardRewardIds.Count);
            CurrentRun.pendingCardRewardSkipGold = Mathf.Max(0, skipGold);
        }

        private void FinishPendingCardReward(string message)
        {
            CurrentRun.pendingCardRewardIds.Clear();
            CurrentRun.pendingCardRewardPickCount = 0;
            CurrentRun.pendingCardRewardSkipGold = 0;
            var resolvedNode = pendingNode;

            if (hasPendingNode)
            {
                AdvanceAfterResolvedNode(resolvedNode);
                ClearPendingNode();
            }

            CurrentRun.lastMessage = message;
            AppendRunLog(message, resolvedNode);
        }

        private void GrantGold(int amount)
        {
            CurrentRun.gold += Mathf.CeilToInt(amount * GoldMultiplier());
        }

        private float GoldMultiplier()
        {
            var multiplier = 1f;
            if (HasArtifact("artifact_field_purse")) multiplier += 0.20f;
            if (CurrentFloorAffix() == FloorAffixType.DemonTide) multiplier += 0.15f;
            return multiplier;
        }

        private int ArtifactBonusOpportunityGold()
        {
            return HasArtifact("artifact_fox_coin") ? 15 : 0;
        }

        private string ResolveOpportunityTemplate(int templateIndex, System.Random random)
        {
            switch (templateIndex)
            {
                case 0:
                {
                    var gold = 38 + CurrentRun.floor * 10 + ArtifactBonusOpportunityGold();
                    GrantGold(gold);
                    return $"机遇：香火商队赠礼，获得 {Mathf.CeilToInt(gold * GoldMultiplier())} 金币。";
                }
                case 1:
                {
                    QueueCardReward(RandomCards(random, 3, includeUpgraded: false), 1);
                    return "机遇：天庭旧符苏醒，从 3 张等级 1 卡牌中选择 1 张。";
                }
                case 2:
                {
                    if (TryUpgradeRandomCard(random, out var upgradedName))
                    {
                        return $"机遇：炼器炉火正旺，{upgradedName} 升级。";
                    }

                    QueueCardReward(RandomCards(random, 3, includeUpgraded: true), 1);
                    return "机遇：炼器炉中无可合成卡，改为从 3 张卡中选择 1 张。";
                }
                case 3:
                {
                    var heal = Mathf.CeilToInt(PlayerMaxHpForRun() * 0.16f);
                    var gold = 18 + ArtifactBonusOpportunityGold();
                    CurrentRun.playerHp = Mathf.Min(PlayerMaxHpForRun(), CurrentRun.playerHp + heal);
                    GrantGold(gold);
                    return $"机遇：青莲泉眼，恢复 {heal} 生命并获得 {Mathf.CeilToInt(gold * GoldMultiplier())} 金币。";
                }
                case 4:
                {
                    var hpCost = Mathf.CeilToInt(PlayerMaxHpForRun() * 0.08f);
                    CurrentRun.playerHp = Mathf.Max(1f, CurrentRun.playerHp - hpCost);
                    QueueCardReward(RandomCards(random, 3, includeUpgraded: true), 1);
                    return $"机遇：妖市秘约，失去 {hpCost} 生命，从 3 张卡中选择 1 张。";
                }
                default:
                {
                    var gold = 24 + CurrentRun.floor * 6 + ArtifactBonusOpportunityGold();
                    GrantGold(gold);
                    if (TryUpgradeRandomCard(random, out var upgradedName))
                    {
                        return $"机遇：星斗残卷，获得 {Mathf.CeilToInt(gold * GoldMultiplier())} 金币，并使 {upgradedName} 升级。";
                    }

                    return $"机遇：星斗残卷，获得 {Mathf.CeilToInt(gold * GoldMultiplier())} 金币。";
                }
            }
        }

        private static OpportunityEventPreview OpportunityPreview(int templateIndex)
        {
            return templateIndex switch
            {
                0 => new OpportunityEventPreview
                {
                    title = "香火商队",
                    story = "一支供奉队伍从战场边缘经过，愿意资助边境军。",
                    reward = "获得一笔金币。",
                    risk = "无直接风险。",
                    templateIndex = templateIndex
                },
                1 => new OpportunityEventPreview
                {
                    title = "天庭旧符",
                    story = "破碎符箓在掌心发光，似乎还能唤来一张基础战力牌。",
                    reward = "获得 1 张等级 1 卡牌。",
                    risk = "奖励质量偏稳定，不保证稀有。",
                    templateIndex = templateIndex
                },
                2 => new OpportunityEventPreview
                {
                    title = "炼器炉火",
                    story = "无主丹炉仍有余温，可以尝试淬炼现有卡牌。",
                    reward = "随机升级 1 组可合成卡牌；没有可合成卡时改获卡牌。",
                    risk = "升级目标不可指定。",
                    templateIndex = templateIndex
                },
                3 => new OpportunityEventPreview
                {
                    title = "青莲泉眼",
                    story = "灵泉从裂缝中涌出，可以修整队伍并搜得少量供奉。",
                    reward = "恢复生命并获得少量金币。",
                    risk = "收益温和。",
                    templateIndex = templateIndex
                },
                4 => new OpportunityEventPreview
                {
                    title = "妖市秘约",
                    story = "妖市商人拿出一张强力牌，但索要一缕气血作抵押。",
                    reward = "获得 1 张可能带等级的卡牌。",
                    risk = "失去少量生命。",
                    templateIndex = templateIndex
                },
                _ => new OpportunityEventPreview
                {
                    title = "星斗残卷",
                    story = "残卷记录着星斗阵图，可以换成供奉，也可能点亮旧牌。",
                    reward = "获得金币，并尝试随机升级卡牌。",
                    risk = "没有可升级目标时只获得金币。",
                    templateIndex = templateIndex
                }
            };
        }

        private string ResolveMysteryTemplate(int templateIndex, System.Random random)
        {
            switch (templateIndex)
            {
                case 0:
                {
                    var gold = 48 + CurrentRun.floor * 12 + ArtifactBonusOpportunityGold();
                    GrantGold(gold);
                    QueueCardReward(RandomCards(random, 3, includeUpgraded: true, CardRewardTier.Elite), 1, 10);
                    return $"神秘：稳妥探查，获得 {Mathf.CeilToInt(gold * GoldMultiplier())} 金币，并从 3 张卡中选择 1 张。";
                }
                case 1:
                {
                    if (random.NextDouble() < 0.48)
                    {
                        pendingBattleSuppressRewards = true;
                        pendingEncounterId = "encounter_mystery_punishment";
                        CurrentRun.lastMessage = "神秘：误入凶阵，打赢也没有额外奖励。";
                        SceneManager.LoadScene(BattleScene);
                        return "神秘：误入凶阵。";
                    }

                    var gold = 86 + CurrentRun.floor * 20 + ArtifactBonusOpportunityGold();
                    GrantGold(gold);
                    QueueCardReward(RandomCards(random, 3, includeUpgraded: true, CardRewardTier.High), 1, 0);
                    return $"神秘：深入凶阵后夺得秘藏，获得 {Mathf.CeilToInt(gold * GoldMultiplier())} 金币，并从 3 张高质量卡中选择 1 张。";
                }
                default:
                {
                    var hpCost = Mathf.CeilToInt(PlayerMaxHpForRun() * 0.12f);
                    CurrentRun.playerHp = Mathf.Max(1f, CurrentRun.playerHp - hpCost);
                    AddRandomCurse(random, 1);
                    if (TryGrantRandomArtifact(random, out var artifactName))
                    {
                        return $"神秘：献祭换宝，失去 {hpCost} 生命并加入 1 张诅咒，获得神器：{artifactName}。";
                    }

                    QueueCardReward(RandomCards(random, 4, includeUpgraded: true, CardRewardTier.High), 1, 0);
                    return $"神秘：献祭换宝，失去 {hpCost} 生命并加入 1 张诅咒，从 4 张高质量卡中选择 1 张。";
                }
            }
        }

        private static OpportunityEventPreview MysteryPreview(int templateIndex)
        {
            return templateIndex switch
            {
                0 => new OpportunityEventPreview
                {
                    title = "稳妥探查",
                    story = "沿着阵眼边缘搜索，不惊动最深处的妖阵。",
                    reward = "金币，并从 3 张卡中选择 1 张。",
                    risk = "收益较稳，爆发不高。",
                    templateIndex = templateIndex
                },
                1 => new OpportunityEventPreview
                {
                    title = "深入凶阵",
                    story = "强行冲进妖阵深处，可能夺得秘藏，也可能被拖入惩罚战。",
                    reward = "高额金币和高质量卡牌。",
                    risk = "有概率触发强力怪物战，胜利也没有额外奖励。",
                    templateIndex = templateIndex
                },
                _ => new OpportunityEventPreview
                {
                    title = "献祭换宝",
                    story = "以气血和因果为价，换取更偏构筑方向的收获。",
                    reward = "优先获得一个神器，或改为高质量卡牌。",
                    risk = "失去生命，并加入 1 张诅咒牌。",
                    templateIndex = templateIndex
                }
            };
        }

        private static IReadOnlyList<int> RandomTemplateIndices(System.Random random, int maxExclusive, int count)
        {
            return Enumerable.Range(0, maxExclusive)
                .OrderBy(_ => random.Next())
                .Take(Mathf.Min(count, maxExclusive))
                .ToList();
        }

        private void AddRandomCurse(System.Random random, int count)
        {
            var curses = Catalog.cards
                .Where(card => card != null && card.type == CardType.Curse)
                .OrderBy(_ => random.Next())
                .Take(Mathf.Max(1, count))
                .ToList();

            foreach (var curse in curses)
            {
                CurrentRun.deckCardIds.Add(curse.id);
            }
        }

        private bool TryGrantRandomArtifact(System.Random random, out string artifactName)
        {
            artifactName = string.Empty;
            var artifact = Catalog.artifacts
                .Where(candidate => candidate != null && candidate.id != "artifact_permanent_relic" && !CurrentRun.artifactIds.Contains(candidate.id))
                .OrderByDescending(candidate => candidate.rarity)
                .ThenBy(_ => random.Next())
                .FirstOrDefault();
            if (artifact == null)
            {
                return false;
            }

            CurrentRun.artifactIds.Add(artifact.id);
            artifactName = artifact.displayName;
            return true;
        }

        private bool TryUpgradeRandomCard(System.Random random, out string upgradedName)
        {
            upgradedName = string.Empty;
            var group = CurrentRun.deckCardIds
                .Where(IsCardUpgradable)
                .GroupBy(id => id)
                .Where(candidate => candidate.Count() >= 3)
                .OrderBy(_ => random.Next())
                .FirstOrDefault();
            return group != null && TryUpgradeCardSet(group.Key, out upgradedName);
        }

        private bool TryUpgradeCardSet(string cardId, out string upgradedName)
        {
            upgradedName = string.Empty;
            if (CurrentRun == null || !IsCardUpgradable(cardId))
            {
                return false;
            }

            var upgradedId = GameContentFactory.UpgradeCardId(cardId);
            var upgradedCard = Catalog.FindCard(upgradedId);
            if (upgradedCard == null)
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

            CurrentRun.deckCardIds.Add(upgradedId);
            upgradedName = upgradedCard.displayName;
            return true;
        }

        private bool IsCardUpgradable(string cardId)
        {
            if (string.IsNullOrWhiteSpace(cardId) || GameContentFactory.CardLevelFromId(cardId) >= 3)
            {
                return false;
            }

            var card = Catalog.FindCard(cardId);
            if (card == null || card.type == CardType.Curse)
            {
                return false;
            }

            return Catalog.FindCard(GameContentFactory.UpgradeCardId(cardId)) != null;
        }

        private enum CardRewardTier
        {
            Basic,
            Elite,
            Boss,
            High
        }

        private IReadOnlyList<CardDefinition> RandomCards(System.Random random, int count, bool includeUpgraded, bool preferHighQuality = false)
        {
            return RandomCards(random, count, includeUpgraded, preferHighQuality ? CardRewardTier.High : CardRewardTier.Basic);
        }

        private IReadOnlyList<CardDefinition> RandomCards(System.Random random, int count, bool includeUpgraded, CardRewardTier rewardTier)
        {
            var classCardPool = GameContentFactory.HeroClassCardPoolBaseIds(CurrentHeroClass).ToHashSet(StringComparer.Ordinal);
            var candidates = Catalog.cards
                .Where(card =>
                    card != null &&
                    card.type != CardType.Curse &&
                    (includeUpgraded || card.level == 1) &&
                    classCardPool.Contains(GameContentFactory.BaseCardId(card.id)))
                .ToList();

            var result = new List<CardDefinition>();
            while (result.Count < count && candidates.Count > 0)
            {
                var totalWeight = candidates.Sum(card => CardRewardWeight(card, rewardTier) * HeroClassCardWeight(card));
                var roll = random.NextDouble() * Math.Max(0.001, totalWeight);
                var cursor = 0.0;
                CardDefinition picked = null;
                foreach (var card in candidates)
                {
                    cursor += CardRewardWeight(card, rewardTier) * HeroClassCardWeight(card);
                    if (roll <= cursor)
                    {
                        picked = card;
                        break;
                    }
                }

                picked ??= candidates[random.Next(candidates.Count)];
                result.Add(picked);
                candidates.Remove(picked);
            }

            return result.Count > 0
                ? result
                : Catalog.cards
                    .Where(card => card != null && card.type != CardType.Curse && classCardPool.Contains(GameContentFactory.BaseCardId(card.id)))
                    .Take(count)
                    .ToList();
        }

        private static CardRewardTier RewardTierForNode(MapNodeType nodeType)
        {
            return nodeType switch
            {
                MapNodeType.EliteMonster => CardRewardTier.Elite,
                MapNodeType.SmallBoss or MapNodeType.FinalBoss => CardRewardTier.Boss,
                MapNodeType.Mystery => CardRewardTier.High,
                _ => CardRewardTier.Basic
            };
        }

        private static double CardRewardWeight(CardDefinition card, CardRewardTier rewardTier)
        {
            var rarityWeight = rewardTier switch
            {
                CardRewardTier.Elite => card.rarity switch
                {
                    CardRarity.Common => 45,
                    CardRarity.Uncommon => 32,
                    CardRarity.Rare => 18,
                    CardRarity.Epic => 4,
                    CardRarity.Legendary => 1,
                    _ => 1
                },
                CardRewardTier.Boss => card.rarity switch
                {
                    CardRarity.Common => 12,
                    CardRarity.Uncommon => 32,
                    CardRarity.Rare => 35,
                    CardRarity.Epic => 16,
                    CardRarity.Legendary => 5,
                    _ => 1
                },
                CardRewardTier.High => card.rarity switch
                {
                    CardRarity.Common => 8,
                    CardRarity.Uncommon => 24,
                    CardRarity.Rare => 40,
                    CardRarity.Epic => 20,
                    CardRarity.Legendary => 8,
                    _ => 1
                },
                _ => card.rarity switch
                {
                    CardRarity.Common => 78,
                    CardRarity.Uncommon => 18,
                    CardRarity.Rare => 4,
                    CardRarity.Epic => 1,
                    CardRarity.Legendary => 0.2,
                    _ => 1
                }
            };

            var archetypeWeight = card.type switch
            {
                CardType.Structure => 1.25,
                CardType.Spell => 1.05,
                CardType.Tactic => 0.95,
                CardType.Hero => 0.55,
                _ => 1.0
            };

            return Math.Max(0.01, rarityWeight * archetypeWeight);
        }

        private double HeroClassCardWeight(CardDefinition card)
        {
            if (card == null)
            {
                return 1.0;
            }

            return CurrentHeroClassDefinition.RewardWeightFor(card);
        }

        private void ResolvePendingNonBattle(string message)
        {
            if (!hasPendingNode)
            {
                return;
            }

            var resolvedNode = pendingNode;
            AdvanceAfterResolvedNode(resolvedNode);
            ClearPendingNode();
            CurrentRun.lastMessage = message;
            AppendRunLog(message, resolvedNode);
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

        private void ClearPendingCardReward()
        {
            if (CurrentRun == null)
            {
                return;
            }

            CurrentRun.pendingCardRewardIds.Clear();
            CurrentRun.pendingCardRewardPickCount = 0;
            CurrentRun.pendingCardRewardSkipGold = 0;
        }

        private void AppendRunLog(string message)
        {
            AppendRunLog(message, CurrentRun != null ? CurrentRun.floor : 0, CurrentRun != null ? CurrentRun.row : 0);
        }

        private void AppendRunLog(string message, MapNodeRuntime resolvedNode)
        {
            if (resolvedNode == null)
            {
                AppendRunLog(message);
                return;
            }

            AppendRunLog(message, resolvedNode.Floor, resolvedNode.Row);
        }

        private void AppendRunLog(string message, int floor, int row)
        {
            if (CurrentRun == null || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            var logFloor = Mathf.Max(1, floor);
            var logRow = Mathf.Clamp(row, 1, 10);
            var stamp = $"迷宫 {logFloor} · 房间 {logRow}/10";
            CurrentRun.eventLog.Add($"{stamp}  {message}");
            while (CurrentRun.eventLog.Count > MaxRunLogEntries)
            {
                CurrentRun.eventLog.RemoveAt(0);
            }
        }

        private void AppendPlaytestRecord(string summary, MapNodeRuntime resolvedNode)
        {
            if (CurrentRun == null || string.IsNullOrWhiteSpace(summary))
            {
                return;
            }

            var floor = resolvedNode != null ? resolvedNode.Floor : CurrentRun.floor;
            var row = resolvedNode != null ? resolvedNode.Row : CurrentRun.row;
            var record = $"迷宫 {Mathf.Max(1, floor)} · 房间 {Mathf.Clamp(row, 1, 10)}/10  {summary}";
            CurrentRun.playtestRecords.Add(record);
            while (CurrentRun.playtestRecords.Count > MaxPlaytestRecords)
            {
                CurrentRun.playtestRecords.RemoveAt(0);
            }

            AppendRunLog($"试玩记录：{summary}", floor, row);
        }

        private void DebugJumpTo(int row, string message)
        {
            if (CurrentRun == null)
            {
                return;
            }

            EnsureMap();
            ClearPendingNode();
            ClearPendingCardReward();
            CurrentRun.row = Mathf.Clamp(row, 1, 10);
            SetAvailableNodesForCurrentRow();
            CurrentRun.lastMessage = message;
            AppendRunLog(message);
        }

        private static void AssignFloorAffixes(RunState run)
        {
            if (run == null)
            {
                return;
            }

            run.floorAffixes.Clear();
            var random = new System.Random(run.seed + 2027);
            var pool = new[]
            {
                FloorAffixType.DemonFog,
                FloorAffixType.ThunderTribulation,
                FloorAffixType.DemonTide,
                FloorAffixType.ImmortalArray
            };

            for (var i = 0; i < 3; i++)
            {
                run.floorAffixes.Add(pool[random.Next(pool.Length)]);
            }
        }

        private static string FloorAffixName(FloorAffixType affix)
        {
            return affix switch
            {
                FloorAffixType.DemonFog => "妖雾迷天",
                FloorAffixType.ThunderTribulation => "天雷劫池",
                FloorAffixType.DemonTide => "万妖潮",
                FloorAffixType.ImmortalArray => "仙阵余辉",
                _ => "无词缀"
            };
        }

        private static string FloorAffixDescription(FloorAffixType affix)
        {
            return affix switch
            {
                FloorAffixType.DemonFog => "远程射程下降，法术费用 +1。",
                FloorAffixType.ThunderTribulation => "战场周期性落雷，法术伤害小幅提高。",
                FloorAffixType.DemonTide => "敌方出兵更快，金币收益 +15%。",
                FloorAffixType.ImmortalArray => "我方建筑更硬且产兵略快，但费用恢复略慢。",
                _ => "本层没有额外规则。"
            };
        }

        private void ApplyPermanentStartBonuses(RunState run)
        {
            var profile = PermanentProgress;
            var messages = new List<string>();
            foreach (var artifactId in profile.permanentArtifactIds)
            {
                if (!run.permanentArtifactIds.Contains(artifactId))
                {
                    run.permanentArtifactIds.Add(artifactId);
                }
            }

            var heroLevel = HeroLevelForExperience(profile.totalHeroExperience);
            if (heroLevel > 1)
            {
                var bonusGold = (heroLevel - 1) * 4;
                run.gold += bonusGold;
                run.playerHp = Mathf.Min(PlayerMaxHpForRun(), run.playerHp + (heroLevel - 1) * 2f);
                messages.Add($"主角等级 {heroLevel} 生效：初始金币 +{bonusGold}，生命上限 +{(heroLevel - 1) * 2}");
            }

            if (profile.permanentArtifactIds.Contains("artifact_permanent_relic"))
            {
                run.gold += 20;
                run.playerHp = Mathf.Min(PlayerMaxHpForRun(), run.playerHp + 5f);
                messages.Add("永久神器通关遗珍生效：本次探索初始金币 +20，生命 +5");
            }

            if (messages.Count > 0)
            {
                run.lastMessage = string.Join("；", messages) + "。";
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

        private static int HeroLevelForExperience(int experience)
        {
            var level = 1;
            while (experience >= ExperienceRequiredForLevel(level + 1) && level < 99)
            {
                level++;
            }

            return level;
        }

        private static int ExperienceRequiredForLevel(int level)
        {
            if (level <= 1)
            {
                return 0;
            }

            return 40 + (level - 2) * 55 + Mathf.Max(0, level - 3) * 20;
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

        public static string HeroClassName(HeroClassType heroClass)
        {
            return GameContentFactory.GetHeroClassDefinition(heroClass).displayName;
        }

        public static string HeroClassShortStyle(HeroClassType heroClass)
        {
            return GameContentFactory.GetHeroClassDefinition(heroClass).shortStyle;
        }

        public static string HeroClassDescription(HeroClassType heroClass)
        {
            return GameContentFactory.GetHeroClassDefinition(heroClass).description;
        }
    }
}
