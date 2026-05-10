using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using XTD.Content;
using XTD.Flow;
using XTD.Roguelike;

namespace XTD.Tests
{
    public sealed class GameFlowControllerFlowTests
    {
        private readonly List<GameObject> createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in createdObjects)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }

            createdObjects.Clear();
            SetStaticBackingField(typeof(GameFlowController), "<Instance>k__BackingField", null);
        }

        [Test]
        public void TakeRestHeal_LogsResolvedRoomInsteadOfNextRoom()
        {
            var catalog = GameContentFactory.CreateCatalog();
            var run = GameContentFactory.CreateStartingRun(catalog);
            run.seed = 12345;
            run.floor = 2;
            run.row = 6;
            run.playerHp = 40f;

            var flow = CreateFlow(catalog, run);
            SetPendingNode(flow, new MapNodeRuntime(2, 6, 1, MapNodeType.Rest, string.Empty, new[] { 0, 1 }));

            flow.TakeRestHeal();

            Assert.That(run.row, Is.EqualTo(7));
            Assert.That(run.eventLog, Is.Not.Empty);
            Assert.That(run.eventLog[^1], Does.StartWith("迷宫 2 · 房间 6/10"));
        }

        [Test]
        public void ChooseCardReward_UsesResolvedRoomForLogAndAdvancesRun()
        {
            var catalog = GameContentFactory.CreateCatalog();
            var run = GameContentFactory.CreateStartingRun(catalog);
            run.seed = 6789;
            run.floor = 1;
            run.row = 5;
            run.pendingCardRewardIds.Add("card_fireball");
            run.pendingCardRewardPickCount = 1;

            var flow = CreateFlow(catalog, run);
            SetPendingNode(flow, new MapNodeRuntime(1, 5, 0, MapNodeType.EliteMonster, string.Empty, new[] { 0 }));

            flow.ChooseCardReward(catalog.FindCard("card_fireball"));

            Assert.That(run.row, Is.EqualTo(6));
            Assert.That(run.pendingCardRewardPickCount, Is.EqualTo(0));
            Assert.That(run.pendingCardRewardIds, Is.Empty);
            Assert.That(run.deckCardIds, Does.Contain("card_fireball"));
            Assert.That(run.eventLog[^1], Does.StartWith("迷宫 1 · 房间 5/10"));
        }

        [Test]
        public void SkipCardRewardForGold_GrantsGoldAndLogsResolvedRoom()
        {
            var catalog = GameContentFactory.CreateCatalog();
            var run = GameContentFactory.CreateStartingRun(catalog);
            run.seed = 2222;
            run.floor = 1;
            run.row = 3;
            run.gold = 10;
            run.pendingCardRewardIds.Add("card_fireball");
            run.pendingCardRewardPickCount = 1;
            run.pendingCardRewardSkipGold = 8;

            var flow = CreateFlow(catalog, run);
            SetPendingNode(flow, new MapNodeRuntime(1, 3, 0, MapNodeType.NormalMonster, string.Empty, new[] { 1 }));

            flow.SkipCardRewardForGold();

            Assert.That(run.gold, Is.EqualTo(18));
            Assert.That(run.row, Is.EqualTo(4));
            Assert.That(run.eventLog[^1], Does.StartWith("迷宫 1 · 房间 3/10"));
            Assert.That(run.eventLog[^1], Does.Contain("换得 8 金币"));
        }

        [Test]
        public void PendingCardRewardResolvedSkipGold_AppliesCurrentRunMultipliers()
        {
            var catalog = GameContentFactory.CreateCatalog();
            var run = GameContentFactory.CreateStartingRun(catalog);
            run.seed = 3333;
            run.floor = 1;
            run.pendingCardRewardIds.Add("card_fireball");
            run.pendingCardRewardPickCount = 1;
            run.pendingCardRewardSkipGold = 8;
            run.artifactIds.Add("artifact_field_purse");
            run.floorAffixes.Add(FloorAffixType.DemonTide);

            var flow = CreateFlow(catalog, run);

            Assert.That(flow.PendingCardRewardResolvedSkipGold, Is.EqualTo(11));
        }

        [Test]
        public void BuildNodePreview_ReflectsCurrentGoldAndHealthAdvice()
        {
            var catalog = GameContentFactory.CreateCatalog();
            var run = GameContentFactory.CreateStartingRun(catalog);
            run.seed = 4444;
            run.gold = 100;

            var flow = CreateFlow(catalog, run);
            var shop = new MapNodeRuntime(1, 6, 0, MapNodeType.Shop, string.Empty, new[] { 0 });
            var shopPreview = flow.BuildNodePreview(shop, reachable: true);

            Assert.That(shopPreview, Does.Contain("商店 · 当前路线可达"));
            Assert.That(shopPreview, Does.Contain($"刷新 {flow.ShopRerollCost} 金币"));
            Assert.That(shopPreview, Does.Contain("金币充足"));

            run.playerHp = 20f;
            var rest = new MapNodeRuntime(1, 7, 0, MapNodeType.Rest, string.Empty, new[] { 0 });
            var restPreview = flow.BuildNodePreview(rest, reachable: true);

            Assert.That(restPreview, Does.Contain("低血量优先"));
            Assert.That(restPreview, Does.Contain("恢复约 10-30%"));
        }

        [Test]
        public void BuildNodePreview_BattleNodesUseEncounterRewardsAndPressure()
        {
            var catalog = GameContentFactory.CreateCatalog();
            var run = GameContentFactory.CreateStartingRun(catalog);
            run.seed = 5555;

            var flow = CreateFlow(catalog, run);
            var finalBoss = new MapNodeRuntime(3, 10, 0, MapNodeType.FinalBoss, string.Empty);
            var preview = flow.BuildNodePreview(finalBoss, reachable: false);

            Assert.That(preview, Does.Contain("最终首领 · 暂未连通"));
            Assert.That(preview, Does.Contain("约 220 金币"));
            Assert.That(preview, Does.Contain("经验 +60，永久神器"));
            Assert.That(preview, Does.Contain("13s 混沌裂隙"));
        }

        [Test]
        public void ShopActions_UpdateOfferStateAndDeck()
        {
            var catalog = GameContentFactory.CreateCatalog();
            var run = GameContentFactory.CreateStartingRun(catalog);
            run.seed = 9999;
            run.floor = 2;
            run.row = 8;
            run.gold = 200;
            run.deckCardIds.Add("card_fireball");
            run.deckCardIds.Add("card_fireball_lv2");
            run.deckCardIds.Add("card_rally");
            run.deckCardIds.Add("card_rally_lv2");

            var offerCard = catalog.FindCard("card_binding_talisman");
            run.shopOfferCardIds.Add(offerCard.id);

            var flow = CreateFlow(catalog, run);
            SetPendingNode(flow, new MapNodeRuntime(2, 8, 1, MapNodeType.Shop, string.Empty, new[] { 0 }));

            var bought = flow.BuyCard(offerCard);
            Assert.That(bought, Is.True);
            Assert.That(run.shopBoughtCardIds, Does.Contain(offerCard.id));
            Assert.That(run.deckCardIds, Does.Contain(offerCard.id));

            var removeTarget = "card_fireball";
            var removed = flow.RemoveCardAtShop(removeTarget);
            Assert.That(removed, Is.True);
            Assert.That(run.shopRemoveUsed, Is.True);
            Assert.That(run.deckCardIds, Does.Not.Contain(removeTarget));

            var rerolled = flow.RerollShopCards();
            Assert.That(rerolled, Is.True);
            Assert.That(run.shopOfferRerollCount, Is.EqualTo(1));
            Assert.That(run.shopOfferCardIds.Count, Is.EqualTo(5));
        }

        [Test]
        public void GrantBattleRewards_NormalMonsterOffersOnlyLevelOneCards()
        {
            var catalog = GameContentFactory.CreateCatalog();
            var run = GameContentFactory.CreateStartingRun(catalog);
            run.seed = 2468;

            var flow = CreateFlow(catalog, run);
            var node = new MapNodeRuntime(1, 2, 0, MapNodeType.NormalMonster, string.Empty, new[] { 0 });
            var encounter = catalog.FirstEncounter(MapNodeType.NormalMonster);

            InvokePrivate<string>(flow, "GrantBattleRewards", node, encounter);

            Assert.That(run.pendingCardRewardIds, Is.Not.Empty);
            Assert.That(run.pendingCardRewardIds
                .Select(GameContentFactory.CardLevelFromId), Has.All.EqualTo(1));
        }

        [Test]
        public void AppendPlaytestRecord_StoresRecordAndRunLog()
        {
            var catalog = GameContentFactory.CreateCatalog();
            var run = GameContentFactory.CreateStartingRun(catalog);
            run.seed = 1357;
            run.floor = 1;
            run.row = 2;

            var flow = CreateFlow(catalog, run);
            var node = new MapNodeRuntime(1, 2, 0, MapNodeType.NormalMonster, string.Empty, new[] { 0 });

            InvokePrivate<object>(flow, "AppendPlaytestRecord", "耗时 42s，出牌 7", node);

            Assert.That(run.playtestRecords, Has.Count.EqualTo(1));
            Assert.That(run.playtestRecords[0], Does.Contain("耗时 42s"));
            Assert.That(run.eventLog.Any(log => log.Contains("试玩记录")), Is.True);
        }

        [Test]
        public void HeroClassDefinitions_ExposeFullCardPoolsForEveryAvailableClass()
        {
            var catalog = GameContentFactory.CreateCatalog();
            var classes = GameContentFactory.AvailableHeroClasses();

            Assert.That(classes.Count, Is.GreaterThanOrEqualTo(4));
            Assert.That(classes, Does.Contain(HeroClassType.TalismanSealer));

            foreach (var heroClass in classes)
            {
                var pool = GameContentFactory.HeroClassCardPoolBaseIds(heroClass);
                var startingDeck = GameContentFactory.StartingDeckCardIds(heroClass);

                Assert.That(pool, Is.Not.Empty, $"{GameFlowController.HeroClassName(heroClass)} should have a full card pool.");
                Assert.That(startingDeck, Is.Not.Empty, $"{GameFlowController.HeroClassName(heroClass)} should have a starting deck.");
                Assert.That(pool, Is.Unique);

                foreach (var baseId in pool)
                {
                    Assert.That(catalog.FindCard(baseId), Is.Not.Null, $"{heroClass} card pool contains missing card {baseId}.");
                }

                foreach (var cardId in startingDeck)
                {
                    Assert.That(pool, Does.Contain(GameContentFactory.BaseCardId(cardId)), $"{heroClass} starting card {cardId} should belong to its full card pool.");
                }
            }

            Assert.That(
                GameContentFactory.HeroClassCardPoolBaseIds(HeroClassType.BorderCommander),
                Is.Not.EquivalentTo(GameContentFactory.HeroClassCardPoolBaseIds(HeroClassType.TalismanSealer)));
        }

        [Test]
        public void HeroClassDefinitions_KeepDistinctStartingDeckProfiles()
        {
            var catalog = GameContentFactory.CreateCatalog();

            Assert.That(StartingDeckTypeCount(catalog, HeroClassType.BorderCommander, CardType.Spell), Is.EqualTo(0));
            Assert.That(StartingDeckTypeCount(catalog, HeroClassType.BorderCommander, CardType.EliteSoldier), Is.GreaterThanOrEqualTo(2));

            Assert.That(StartingDeckTypeCount(catalog, HeroClassType.SpiritSummoner, CardType.Structure), Is.GreaterThanOrEqualTo(6));
            Assert.That(StartingDeckTypeCount(catalog, HeroClassType.SpiritSummoner, CardType.Spell), Is.EqualTo(0));

            Assert.That(StartingDeckTypeCount(catalog, HeroClassType.ThunderMage, CardType.Spell), Is.GreaterThanOrEqualTo(5));
            Assert.That(StartingDeckTypeCount(catalog, HeroClassType.ThunderMage, CardType.Structure), Is.EqualTo(1));

            Assert.That(StartingDeckTypeCount(catalog, HeroClassType.TalismanSealer, CardType.Debuff), Is.GreaterThanOrEqualTo(5));
            Assert.That(StartingDeckTypeCount(catalog, HeroClassType.TalismanSealer, CardType.Spell), Is.EqualTo(0));
        }

        [Test]
        public void HeroClassDefinitions_ReserveExclusiveAnchorCardsPerClass()
        {
            var classes = GameContentFactory.AvailableHeroClasses();

            foreach (var heroClass in classes)
            {
                var pool = GameContentFactory.HeroClassCardPoolBaseIds(heroClass);
                var otherPools = classes
                    .Where(candidate => candidate != heroClass)
                    .SelectMany(GameContentFactory.HeroClassCardPoolBaseIds)
                    .ToHashSet();

                var exclusiveCards = pool.Where(cardId => !otherPools.Contains(cardId)).ToList();
                Assert.That(exclusiveCards.Count, Is.GreaterThanOrEqualTo(2), $"{GameFlowController.HeroClassName(heroClass)} should keep at least two exclusive anchor cards.");
            }
        }

        private static int StartingDeckTypeCount(ContentCatalog catalog, HeroClassType heroClass, CardType cardType)
        {
            return GameContentFactory.StartingDeckCardIds(heroClass)
                .Select(catalog.FindCard)
                .Count(card => card != null && card.type == cardType);
        }

        private GameFlowController CreateFlow(ContentCatalog catalog, RunState run)
        {
            var go = new GameObject("GameFlowControllerFlowTests");
            createdObjects.Add(go);
            var flow = go.AddComponent<GameFlowController>();
            flow.ConfigureCatalog(catalog);
            SetBackingField(flow, "<Catalog>k__BackingField", catalog);
            SetBackingField(flow, "<CurrentRun>k__BackingField", run);
            return flow;
        }

        private static void SetPendingNode(GameFlowController flow, MapNodeRuntime node)
        {
            SetField(flow, "pendingNode", node);
            SetField(flow, "hasPendingNode", true);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            field?.SetValue(target, value);
        }

        private static void SetBackingField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            field?.SetValue(target, value);
        }

        private static T InvokePrivate<T>(object target, string methodName, params object[] args)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (T)method.Invoke(target, args);
        }

        private static void SetStaticBackingField(System.Type type, string fieldName, object value)
        {
            var field = type.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
            field?.SetValue(null, value);
        }
    }
}
