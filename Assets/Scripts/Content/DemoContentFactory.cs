using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace XTD.Content
{
    public static class DemoContentFactory
    {
        private const string ProjectAiArtPrefix = "Assets/_Project/Art/AI/";
        private const string ResourcesAssetPrefix = "Assets/Resources/";
        private const string RuntimeAiArtPrefix = "Art/AI/";

        private static readonly Dictionary<string, Sprite> LoadedSprites = new(StringComparer.OrdinalIgnoreCase);
        public static ContentCatalog CreateCatalog()
        {
            var catalog = ScriptableObject.CreateInstance<ContentCatalog>();
            catalog.name = "演示内容目录";
            EnsureCatalogComplete(catalog);
            return catalog;
        }

        public static void EnsureCatalogComplete(ContentCatalog catalog)
        {
            if (catalog == null)
            {
                return;
            }

            catalog.cards.RemoveAll(card => card == null || string.IsNullOrWhiteSpace(card.id));
            catalog.units.RemoveAll(unit => unit == null || string.IsNullOrWhiteSpace(unit.id));
            catalog.artifacts.RemoveAll(artifact => artifact == null || string.IsNullOrWhiteSpace(artifact.id));
            catalog.encounters.RemoveAll(encounter => encounter == null || string.IsNullOrWhiteSpace(encounter.id));

            var militia = Unit(catalog, "unit_militia", "香火民兵", Faction.Player, UnitRole.Soldier, 16, 4, 1.1f, 0.75f, 1.2f, 1, new Color(0.65f, 0.85f, 1f), "Assets/_Project/Art/AI/Battle/unit_militia_battle.png");
            var archer = Unit(catalog, "unit_archer", "灵弩射手", Faction.Player, UnitRole.Soldier, 12, 3, 1.2f, 2.15f, 0.85f, 1, new Color(0.75f, 1f, 0.6f), "Assets/_Project/Art/AI/Battle/unit_archer_battle.png");
            var shieldGuard = Unit(catalog, "unit_shield_guard", "金甲天将", Faction.Player, UnitRole.Elite, 58, 6, 1.2f, 0.85f, 0.72f, 4, new Color(0.55f, 0.65f, 1f), "Assets/_Project/Art/AI/Battle/unit_heaven_general_battle.png");
            var monkeyHero = Unit(catalog, "unit_monkey_vanguard", "齐天先锋", Faction.Player, UnitRole.Hero, 145, 15, 0.85f, 1.05f, 0.92f, 8, new Color(1f, 0.80f, 0.36f), "Assets/_Project/Art/AI/Battle/unit_monkey_vanguard_battle.png");
            var thunderGuard = Unit(catalog, "unit_thunder_guard", "雷鼓卫", Faction.Player, UnitRole.Soldier, 24, 5, 1.0f, 1.05f, 0.95f, 2, new Color(0.70f, 0.95f, 1f), "Assets/_Project/Art/AI/Battle/unit_thunder_guard_battle.png");
            monkeyHero.art ??= shieldGuard.art;
            thunderGuard.art ??= shieldGuard.art;

            var barracks = Unit(catalog, "unit_incense_barracks", "香火兵营", Faction.Player, UnitRole.Structure, 75, 0, 99f, 0f, 0f, 4, new Color(1f, 0.86f, 0.48f), "Assets/_Project/Art/AI/Battle/unit_incense_barracks_battle.png");
            barracks.producedUnit = militia;
            barracks.productionInterval = 2.6f;
            barracks.productionCount = 1;

            var archerAltar = Unit(catalog, "unit_spirit_arrow_altar", "灵弩坛", Faction.Player, UnitRole.Structure, 58, 0, 99f, 0f, 0f, 4, new Color(0.65f, 0.9f, 1f), "Assets/_Project/Art/AI/Battle/unit_spirit_arrow_altar_battle.png");
            archerAltar.producedUnit = archer;
            archerAltar.productionInterval = 3.2f;
            archerAltar.productionCount = 1;

            var roadblock = Unit(catalog, "unit_roadblock", "八卦石垒", Faction.Player, UnitRole.Structure, 90, 0, 99f, 0f, 0f, 3, new Color(0.75f, 0.55f, 0.35f), "Assets/_Project/Art/AI/Battle/unit_bagua_wall_battle.png");
            roadblock.blocksMovement = true;

            var thunderTower = Unit(catalog, "unit_thunder_drum_tower", "雷鼓台", Faction.Player, UnitRole.Structure, 70, 0, 99f, 0f, 0f, 5, new Color(0.66f, 0.86f, 1f), "Assets/_Project/Art/AI/Battle/unit_thunder_drum_tower_battle.png");
            thunderTower.producedUnit = thunderGuard;
            thunderTower.productionInterval = 4.2f;
            thunderTower.productionCount = 1;
            thunderTower.art ??= archerAltar.art;

            var grunt = Unit(catalog, "enemy_grunt", "山魈爪牙", Faction.Enemy, UnitRole.Monster, 16, 4, 1.2f, 0.75f, 0.95f, 0, new Color(1f, 0.55f, 0.45f), "Assets/_Project/Art/AI/Battle/enemy_grunt_battle.png");
            var brute = Unit(catalog, "enemy_brute", "蛮兽", Faction.Enemy, UnitRole.Monster, 42, 8, 1.5f, 0.8f, 0.55f, 0, new Color(1f, 0.35f, 0.25f), "Assets/_Project/Art/AI/Battle/enemy_brute_battle.png");
            var impArcher = Unit(catalog, "enemy_imp_archer", "骨弩小妖", Faction.Enemy, UnitRole.Monster, 18, 4, 1.25f, 2.05f, 0.72f, 0, new Color(1f, 0.68f, 0.45f), "Assets/_Project/Art/AI/Battle/enemy_imp_archer_battle.png");
            var venomShaman = Unit(catalog, "enemy_venom_shaman", "毒雾祭司", Faction.Enemy, UnitRole.Monster, 36, 7, 1.45f, 2.65f, 0.45f, 0, new Color(0.75f, 1f, 0.45f), "Assets/_Project/Art/AI/Battle/enemy_venom_shaman_battle.png");

            var wolfElite = Unit(catalog, "enemy_wolf_elite", "妖狼先锋", Faction.Enemy, UnitRole.Boss, 180, 11, 0.95f, 5.2f, 0f, 0, new Color(1f, 0.32f, 0.50f), "Assets/_Project/Art/AI/Battle/enemy_wolf_elite_battle.png");
            var boneElite = Unit(catalog, "enemy_bone_elite", "白骨督军", Faction.Enemy, UnitRole.Boss, 230, 13, 1.15f, 5.2f, 0f, 0, new Color(0.92f, 0.90f, 1f), "Assets/_Project/Art/AI/Battle/enemy_bone_elite_battle.png");
            var oxElite = Unit(catalog, "enemy_ox_elite", "牛魔校尉", Faction.Enemy, UnitRole.Boss, 270, 16, 1.35f, 5.2f, 0f, 0, new Color(1f, 0.50f, 0.28f), "Assets/_Project/Art/AI/Battle/enemy_ox_elite_battle.png");
            var blackWindBoss = Unit(catalog, "boss_black_wind", "黑风小圣", Faction.Enemy, UnitRole.Boss, 360, 18, 1.05f, 5.2f, 0f, 0, new Color(0.70f, 0.55f, 1f), "Assets/_Project/Art/AI/Battle/boss_black_wind_battle.png");
            var boneQueenBoss = Unit(catalog, "boss_bone_queen", "白骨军主", Faction.Enemy, UnitRole.Boss, 430, 21, 1.20f, 5.2f, 0f, 0, new Color(1f, 0.72f, 0.92f), "Assets/_Project/Art/AI/Battle/boss_bone_queen_battle.png");
            var finalBoss = Unit(catalog, "boss_chaos_lord", "混沌魔君", Faction.Enemy, UnitRole.Boss, 620, 26, 0.95f, 5.2f, 0f, 0, new Color(1f, 0.22f, 0.20f), "Assets/_Project/Art/AI/Battle/boss_chaos_lord_battle.png");
            impArcher.art ??= grunt.art;
            venomShaman.art ??= wolfElite.art;
            wolfElite.art ??= catalog.FindUnit("enemy_alpha")?.art ?? brute.art;
            boneElite.art ??= brute.art;
            oxElite.art ??= wolfElite.art;
            blackWindBoss.art ??= wolfElite.art;
            boneQueenBoss.art ??= wolfElite.art;
            finalBoss.art ??= wolfElite.art;

            UpsertLeveledCard(catalog, "card_incense_barracks", "香火兵营", CardType.Structure, 3, CardReleaseRule.PlayerSide, "放置后持续生产香火民兵。", "Assets/_Project/Art/AI/Cards/card_incense_barracks.png", level =>
            {
                var card = CreateCardPayload(level);
                card.spawns.Add(Spawn(barracks, 1));
                if (level >= 2) card.effects.Add(Effect(EffectType.Shield, TargetRule.AllFriendlyUnits, 8 + level * 4, 0, 99f));
                return card;
            });

            UpsertLeveledCard(catalog, "card_spirit_arrow_altar", "灵弩坛", CardType.Structure, 3, CardReleaseRule.PlayerSide, "放置后持续生产灵弩射手。", "Assets/_Project/Art/AI/Cards/card_spirit_arrow_altar.png", level =>
            {
                var card = CreateCardPayload(level);
                card.spawns.Add(Spawn(archerAltar, 1));
                if (level >= 3) card.effects.Add(Effect(EffectType.BuffAttackSpeed, TargetRule.AllFriendlyUnits, 0.12f, 5f, 99f));
                return card;
            });

            UpsertLeveledCard(catalog, "card_roadblock", "八卦石垒", CardType.Structure, 2, CardReleaseRule.PlayerSide, "放置一道临时防线，阻挡敌人推进。", "Assets/_Project/Art/AI/Cards/card_roadblock.png", level =>
            {
                var card = CreateCardPayload(level);
                card.spawns.Add(Spawn(roadblock, level));
                return card;
            });

            UpsertLeveledCard(catalog, "card_thunder_drum_tower", "雷鼓台", CardType.Structure, 4, CardReleaseRule.PlayerSide, "放置后持续生产近战雷鼓卫。", "Assets/_Project/Art/AI/Cards/card_thunder_drum_tower.png", level =>
            {
                var card = CreateCardPayload(level);
                card.spawns.Add(Spawn(thunderTower, 1));
                if (level >= 2) card.effects.Add(Effect(EffectType.GainMana, TargetRule.Self, level, 0, 0));
                return card;
            });

            UpsertLeveledCard(catalog, "card_heaven_soldier_talisman", "天兵符", CardType.Soldier, 2, CardReleaseRule.PlayerSide, "一次性召来一队弱小天兵。", "Assets/_Project/Art/AI/Cards/card_heaven_soldier_talisman.png", level =>
            {
                var card = CreateCardPayload(level);
                card.spawns.Add(Spawn(militia, 4 + level * 2));
                return card;
            });

            UpsertLeveledCard(catalog, "card_heaven_general_order", "天将令", CardType.EliteSoldier, 4, CardReleaseRule.PlayerSide, "一次性召来数名金甲天将。", "Assets/_Project/Art/AI/Cards/card_heaven_general_order.png", level =>
            {
                var card = CreateCardPayload(level);
                card.spawns.Add(Spawn(shieldGuard, level == 1 ? 2 : 3));
                if (level >= 3) card.effects.Add(Effect(EffectType.Shield, TargetRule.AllFriendlyUnits, 18, 0, 99f));
                return card;
            });

            UpsertLeveledCard(catalog, "card_monkey_hero", "齐天先锋", CardType.Hero, 6, CardReleaseRule.PlayerSide, "召来一名高影响力英雄士兵。", "Assets/_Project/Art/AI/Cards/card_monkey_hero.png", level =>
            {
                var card = CreateCardPayload(level);
                card.spawns.Add(Spawn(monkeyHero, 1));
                card.effects.Add(Effect(EffectType.BuffAttack, TargetRule.AllFriendlyUnits, 0.12f + level * 0.08f, 4f + level, 99f));
                return card;
            });

            UpsertLeveledCard(catalog, "card_fireball", "三昧真火", CardType.Spell, 3, CardReleaseRule.Anywhere, "在目标点燃起范围真火。", "Assets/_Project/Art/AI/Cards/card_fireball.png", level =>
            {
                var card = CreateCardPayload(level);
                card.effects.Add(Effect(EffectType.AreaDamage, TargetRule.AllEnemies, 18 + level * 12, 0, 1.25f + level * 0.25f));
                if (level >= 2) card.effects.Add(Effect(EffectType.Burn, TargetRule.AllEnemies, 4f + level * 1.5f, 3.2f, 1.25f + level * 0.25f));
                return card;
            });

            UpsertLeveledCard(catalog, "card_thunder_talisman", "掌心雷符", CardType.Spell, 2, CardReleaseRule.Anywhere, "对目标点附近敌人造成雷击伤害。", "Assets/_Project/Art/AI/Cards/card_thunder_talisman.png", level =>
            {
                var card = CreateCardPayload(level);
                card.effects.Add(Effect(EffectType.Damage, TargetRule.AllEnemies, 24 + level * 14, 0, 0.9f + level * 0.15f));
                if (level >= 3) card.effects.Add(Effect(EffectType.Knockback, TargetRule.AllEnemies, 0.75f, 0, 1.15f));
                return card;
            });

            UpsertLeveledCard(catalog, "card_rally", "战鼓法令", CardType.Tactic, 2, CardReleaseRule.None, "短时间提升己方单位攻击，并获得士气强化下一张出兵牌。", "Assets/_Project/Art/AI/Cards/card_rally.png", level =>
            {
                var card = CreateCardPayload(level);
                card.effects.Add(Effect(EffectType.BuffAttack, TargetRule.AllFriendlyUnits, 0.18f + level * 0.16f, 4f + level, 99f));
                card.effects.Add(Effect(EffectType.GainMorale, TargetRule.Self, level >= 3 ? 2 : 1, 0, 0));
                return card;
            });

            UpsertLeveledCard(catalog, "card_golden_barrier", "护阵金光", CardType.Tactic, 2, CardReleaseRule.None, "为己方单位附加护盾。", "Assets/_Project/Art/AI/Cards/card_golden_barrier.png", level =>
            {
                var card = CreateCardPayload(level);
                card.effects.Add(Effect(EffectType.Shield, TargetRule.AllFriendlyUnits, 12 + level * 12, 0, 99f));
                return card;
            });
            UpsertFixedCard(catalog, "card_binding_talisman", "缚妖索符", CardType.Debuff, CardRarity.Uncommon, 1, 2, CardReleaseRule.Anywhere, "在目标点减速敌人，并短暂定身。", "Assets/_Project/Art/AI/Cards/card_binding_talisman.png", () =>
            {
                var card = new CardPayload();
                card.effects.Add(Effect(EffectType.Slow, TargetRule.AllEnemies, 0.55f, 4.5f, 1.65f));
                card.effects.Add(Effect(EffectType.Stun, TargetRule.AllEnemies, 1f, 0.45f, 1.35f));
                return card;
            });
            UpsertFixedCard(catalog, "card_healing_rain", "甘霖咒", CardType.Tactic, CardRarity.Uncommon, 1, 2, CardReleaseRule.None, "治疗己方最靠前的数个单位。", "Assets/_Project/Art/AI/Cards/card_healing_rain.png", () =>
            {
                var card = new CardPayload();
                card.effects.Add(Effect(EffectType.Heal, TargetRule.FriendlyFrontline, 26, 0, 99f));
                card.effects.Add(Effect(EffectType.Shield, TargetRule.FriendlyFrontline, 8, 0, 99f));
                return card;
            });
            UpsertFixedCard(catalog, "card_cloud_banner", "流云军令", CardType.Economy, CardRarity.Rare, 1, 1, CardReleaseRule.None, "抽 1 张牌、回复费用，并令下一张建筑牌费用 -2。", "Assets/_Project/Art/AI/Cards/card_cloud_banner.png", () =>
            {
                var card = new CardPayload();
                card.effects.Add(Effect(EffectType.DrawCard, TargetRule.Self, 1, 0, 0));
                card.effects.Add(Effect(EffectType.GainMana, TargetRule.Self, 2, 0, 0));
                return card;
            });
            UpsertFixedCard(catalog, "card_break_formation", "破阵金令", CardType.Debuff, CardRarity.Rare, 1, 3, CardReleaseRule.None, "压制敌方前线攻击，为兵潮争取反推窗口。", "Assets/_Project/Art/AI/Cards/card_break_formation.png", () =>
            {
                var card = new CardPayload();
                card.effects.Add(Effect(EffectType.BuffAttack, TargetRule.EnemyFrontline, -0.35f, 5.5f, 99f));
                return card;
            });
            UpsertFixedCard(catalog, "card_poison_gourd", "瘴毒葫芦", CardType.Debuff, CardRarity.Rare, 1, 3, CardReleaseRule.Anywhere, "在目标点释放毒雾，持续腐蚀并减速敌人。", "Assets/_Project/Art/AI/Cards/card_poison_gourd.png", () =>
            {
                var card = new CardPayload();
                card.effects.Add(Effect(EffectType.Poison, TargetRule.AllEnemies, 5.5f, 5.0f, 1.85f));
                card.effects.Add(Effect(EffectType.Slow, TargetRule.AllEnemies, 0.35f, 5.0f, 1.85f));
                return card;
            });
            UpsertFixedCard(catalog, "card_mountain_seal", "推山印", CardType.Spell, CardRarity.Uncommon, 1, 2, CardReleaseRule.Anywhere, "击退目标点附近敌人，并造成少量伤害。", "Assets/_Project/Art/AI/Cards/card_mountain_seal.png", () =>
            {
                var card = new CardPayload();
                card.effects.Add(Effect(EffectType.Knockback, TargetRule.AllEnemies, 1.05f, 0, 1.55f));
                card.effects.Add(Effect(EffectType.AreaDamage, TargetRule.AllEnemies, 14, 0, 1.55f));
                return card;
            });
            UpsertFixedCard(catalog, "card_treasure_talisman", "聚宝符", CardType.Economy, CardRarity.Uncommon, 1, 1, CardReleaseRule.None, "立刻获得少量金币，并抽 1 张牌。", "Assets/_Project/Art/AI/Cards/card_treasure_talisman.png", () =>
            {
                var card = new CardPayload();
                card.effects.Add(Effect(EffectType.GainGold, TargetRule.Self, 12, 0, 0));
                card.effects.Add(Effect(EffectType.DrawCard, TargetRule.Self, 1, 0, 0));
                return card;
            });
            UpsertFixedCard(catalog, "card_star_river_decree", "星河诏令", CardType.Tactic, CardRarity.Epic, 3, 4, CardReleaseRule.None, "抽 2 张牌，并获得 1 点士气。", "Assets/_Project/Art/AI/Cards/card_star_river_decree.png", () =>
            {
                var card = new CardPayload();
                card.effects.Add(Effect(EffectType.DrawCard, TargetRule.Self, 2, 0, 0));
                card.effects.Add(Effect(EffectType.GainMorale, TargetRule.Self, 1, 0, 0));
                return card;
            });
            UpsertFixedCard(catalog, "card_pangu_spark", "盘古残火", CardType.Spell, CardRarity.Legendary, 3, 7, CardReleaseRule.Anywhere, "在目标点爆开大范围洪荒残火。", "Assets/_Project/Art/AI/Cards/card_pangu_spark.png", () =>
            {
                var card = new CardPayload();
                card.effects.Add(Effect(EffectType.AreaDamage, TargetRule.AllEnemies, 95, 0, 2.45f));
                return card;
            });
            UpsertFixedCard(catalog, "card_nezha_order", "哪吒点兵", CardType.EliteSoldier, CardRarity.Epic, 3, 5, CardReleaseRule.PlayerSide, "一次性召来 4 名雷鼓卫，并给前线套盾。", "Assets/_Project/Art/AI/Cards/card_nezha_order.png", () =>
            {
                var card = new CardPayload();
                card.spawns.Add(Spawn(thunderGuard, 4));
                card.effects.Add(Effect(EffectType.Shield, TargetRule.FriendlyFrontline, 24, 0, 99f));
                return card;
            });
            UpsertFixedCard(catalog, "card_heavenly_workshop", "天工营盘", CardType.Structure, CardRarity.Epic, 3, 5, CardReleaseRule.PlayerSide, "同时放置香火兵营和灵弩坛，快速形成生产线。", "Assets/_Project/Art/AI/Cards/card_heavenly_workshop.png", () =>
            {
                var card = new CardPayload();
                card.spawns.Add(Spawn(barracks, 1));
                card.spawns.Add(Spawn(archerAltar, 1));
                return card;
            });
            UpsertCurseCard(catalog, "card_curse_karmic_fire", "业火缠身", "拖进战场时会灼伤我方基地。", "Assets/_Project/Art/AI/Cards/card_curse_karmic_fire.png");
            UpsertCurseCard(catalog, "card_curse_demon_fog", "妖雾侵心", "拖进战场时会吞掉当前费用。", "Assets/_Project/Art/AI/Cards/card_curse_demon_fog.png");
            UpsertCurseCard(catalog, "card_curse_heavy_karma", "因果债契", "拖进战场时会引来一名额外妖兵。", "Assets/_Project/Art/AI/Cards/card_curse_heavy_karma.png");

            Artifact(catalog, "artifact_ten_thousand_banner", "万兵旗", ArtifactRarity.Epic, "建筑牌费用 -1，建筑产兵速度 +35%，但法术牌费用 +1。");
            Artifact(catalog, "artifact_thunder_fire_box", "雷火符匣", ArtifactRarity.Epic, "法术牌费用 -1，法术伤害额外 +15%，但建筑牌费用 +1。");
            Artifact(catalog, "artifact_general_platform", "点将台", ArtifactRarity.Rare, "每打出 1 张建筑牌，立刻获得 1 点士气。");
            Artifact(catalog, "artifact_curse_gourd", "镇煞葫芦", ArtifactRarity.Rare, "打出诅咒牌时不再受罚，改为获得 1 点士气。");

            CopyLeveledCardArt(catalog, "card_incense_barracks", "card_incense_barracks");
            CopyLeveledCardArt(catalog, "card_spirit_arrow_altar", "card_spirit_arrow_altar");
            CopyLeveledCardArt(catalog, "card_roadblock", "card_roadblock");
            CopyLeveledCardArt(catalog, "card_heaven_soldier_talisman", "card_heaven_soldier_talisman");
            CopyLeveledCardArt(catalog, "card_heaven_general_order", "card_heaven_general_order");
            CopyLeveledCardArt(catalog, "card_fireball", "card_fireball");
            CopyLeveledCardArt(catalog, "card_rally", "card_rally");
            CopyLeveledCardArt(catalog, "card_thunder_drum_tower", "card_thunder_drum_tower");
            CopyLeveledCardArt(catalog, "card_monkey_hero", "card_monkey_hero");
            CopyLeveledCardArt(catalog, "card_thunder_talisman", "card_thunder_talisman");
            CopyLeveledCardArt(catalog, "card_golden_barrier", "card_golden_barrier");
            CopyCardArt(catalog, "card_binding_talisman", "card_thunder_talisman");
            CopyCardArt(catalog, "card_healing_rain", "card_golden_barrier");
            CopyCardArt(catalog, "card_cloud_banner", "card_star_river_decree");
            CopyCardArt(catalog, "card_break_formation", "card_rally");
            CopyCardArt(catalog, "card_poison_gourd", "card_binding_talisman");
            CopyCardArt(catalog, "card_mountain_seal", "card_thunder_talisman");
            CopyCardArt(catalog, "card_treasure_talisman", "card_cloud_banner");

            Artifact(catalog, "artifact_long_banner", "长旗", ArtifactRarity.Common, "阵位上限 +5。");
            Artifact(catalog, "artifact_field_purse", "战地钱袋", ArtifactRarity.Common, "金币收益 +20%。");
            Artifact(catalog, "artifact_war_drum", "战鼓", ArtifactRarity.Rare, "每 4 个己方士兵触发 1 点士气。");
            Artifact(catalog, "artifact_heaven_seal", "天庭符印", ArtifactRarity.Rare, "费用上限 +2，开局费用 +1。");
            Artifact(catalog, "artifact_jade_bottle", "玉净瓶", ArtifactRarity.Rare, "每场战斗开始时主角生命 +12。");
            Artifact(catalog, "artifact_fire_pearl", "离火珠", ArtifactRarity.Rare, "法术伤害 +25%。");
            Artifact(catalog, "artifact_cloud_boots", "踏云履", ArtifactRarity.Common, "己方单位移动速度 +10%。");
            Artifact(catalog, "artifact_black_tortoise", "玄武甲", ArtifactRarity.Rare, "主角生命上限 +20。");
            Artifact(catalog, "artifact_market_token", "通宝令", ArtifactRarity.Common, "商店购买价格 -20%。");
            Artifact(catalog, "artifact_artifact_eye", "观星镜", ArtifactRarity.Epic, "神器层可选神器 +1。");
            Artifact(catalog, "artifact_dragon_bone", "龙骨", ArtifactRarity.Rare, "己方士兵攻击 +12%。");
            Artifact(catalog, "artifact_command_seal", "兵符", ArtifactRarity.Common, "阵位上限 +3，战斗开始抽牌 +1。");
            Artifact(catalog, "artifact_fox_coin", "狐仙钱", ArtifactRarity.Rare, "机遇和神秘奖励额外 +15 金币。");
            Artifact(catalog, "artifact_taiji_map", "太极图残卷", ArtifactRarity.Epic, "休息回血提升，三合一后额外获得 20 金币。");
            Artifact(catalog, "artifact_star_sand", "星砂", ArtifactRarity.Common, "战斗开始获得 1 点费用。");
            Artifact(catalog, "artifact_battle_scripture", "斗战经", ArtifactRarity.Epic, "英雄和精兵攻击 +20%。");
            Artifact(catalog, "artifact_vajra", "金刚杵", ArtifactRarity.Legendary, "主角生命上限 +35，阵位上限 +8。");
            Artifact(catalog, "artifact_permanent_relic", "通关遗珍", ArtifactRarity.Legendary, "最终首领掉落的永久神器占位。");

            Encounter(catalog, "encounter_training_camp", "妖物营地", MapNodeType.NormalMonster, 100, 140, 2.2f, 25, null,
                SpawnEnemy(grunt, 1), SpawnEnemy(brute, 1));
            Encounter(catalog, "encounter_bone_ford", "白骨渡口", MapNodeType.NormalMonster, 100, 160, 2.0f, 30, null,
                SpawnEnemy(grunt, 2), SpawnEnemy(impArcher, 1));
            Encounter(catalog, "encounter_poison_copse", "毒雾林", MapNodeType.NormalMonster, 100, 175, 2.15f, 34, null,
                SpawnEnemy(grunt, 1), SpawnEnemy(venomShaman, 1));

            Encounter(catalog, "encounter_elite_wolf", "妖狼先锋", MapNodeType.EliteMonster, 105, 220, 2.0f, 58, wolfElite,
                SpawnEnemy(grunt, 2), SpawnEnemy(impArcher, 1));
            Encounter(catalog, "encounter_elite_bone", "白骨督军", MapNodeType.EliteMonster, 105, 245, 1.95f, 66, boneElite,
                SpawnEnemy(grunt, 2), SpawnEnemy(brute, 1));
            Encounter(catalog, "encounter_elite_ox", "牛魔校尉", MapNodeType.EliteMonster, 105, 270, 1.85f, 75, oxElite,
                SpawnEnemy(brute, 1), SpawnEnemy(venomShaman, 1));

            Encounter(catalog, "encounter_black_wind", "黑风小圣", MapNodeType.SmallBoss, 110, 320, 1.75f, 110, blackWindBoss,
                SpawnEnemy(grunt, 2), SpawnEnemy(brute, 1), SpawnEnemy(impArcher, 1));
            Encounter(catalog, "encounter_bone_queen", "白骨军主", MapNodeType.SmallBoss, 110, 360, 1.65f, 125, boneQueenBoss,
                SpawnEnemy(grunt, 2), SpawnEnemy(venomShaman, 1), SpawnEnemy(brute, 1));

            Encounter(catalog, "encounter_chaos_lord", "混沌魔君", MapNodeType.FinalBoss, 120, 520, 1.45f, 220, finalBoss,
                SpawnEnemy(grunt, 3), SpawnEnemy(brute, 1), SpawnEnemy(impArcher, 2), SpawnEnemy(venomShaman, 1));
            Encounter(catalog, "encounter_mystery_punishment", "误入凶阵", MapNodeType.Mystery, 105, 300, 1.55f, 0, oxElite,
                SpawnEnemy(brute, 2), SpawnEnemy(venomShaman, 1));
        }

        public static RunState CreateStartingRun(ContentCatalog catalog)
        {
            return CreateStartingRun(catalog, HeroClassType.BorderCommander);
        }

        public static RunState CreateStartingRun(ContentCatalog catalog, HeroClassType heroClass)
        {
            EnsureCatalogComplete(catalog);

            var run = new RunState
            {
                heroClass = heroClass,
                gold = StartingGold(heroClass),
                playerHp = StartingHp(heroClass),
                seed = UnityEngine.Random.Range(1, int.MaxValue),
                lastMessage = $"{GameFlowControllerSafeHeroClassName(heroClass)}进入第一层。"
            };

            AddStartingDeck(run, heroClass);
            return run;
        }

        private static int StartingGold(HeroClassType heroClass)
        {
            return heroClass switch
            {
                HeroClassType.SpiritSummoner => 20,
                HeroClassType.ThunderMage => 32,
                _ => 25
            };
        }

        private static float StartingHp(HeroClassType heroClass)
        {
            return heroClass switch
            {
                HeroClassType.SpiritSummoner => 108f,
                HeroClassType.ThunderMage => 92f,
                _ => 100f
            };
        }

        private static void AddStartingDeck(RunState run, HeroClassType heroClass)
        {
            switch (heroClass)
            {
                case HeroClassType.SpiritSummoner:
                    Add(run, "card_incense_barracks", 3);
                    Add(run, "card_spirit_arrow_altar", 2);
                    Add(run, "card_thunder_drum_tower", 1);
                    Add(run, "card_roadblock", 1);
                    Add(run, "card_heaven_soldier_talisman", 1);
                    Add(run, "card_heaven_general_order", 1);
                    Add(run, "card_golden_barrier", 1);
                    Add(run, "card_rally", 1);
                    break;
                case HeroClassType.ThunderMage:
                    Add(run, "card_incense_barracks", 1);
                    Add(run, "card_spirit_arrow_altar", 1);
                    Add(run, "card_roadblock", 1);
                    Add(run, "card_fireball", 2);
                    Add(run, "card_thunder_talisman", 2);
                    Add(run, "card_binding_talisman", 1);
                    Add(run, "card_mountain_seal", 1);
                    Add(run, "card_cloud_banner", 1);
                    Add(run, "card_heaven_general_order", 1);
                    break;
                default:
                    Add(run, "card_incense_barracks", 2);
                    Add(run, "card_spirit_arrow_altar", 2);
                    Add(run, "card_roadblock", 1);
                    Add(run, "card_heaven_soldier_talisman", 2);
                    Add(run, "card_heaven_general_order", 1);
                    Add(run, "card_fireball", 1);
                    Add(run, "card_rally", 1);
                    break;
            }
        }

        private static string GameFlowControllerSafeHeroClassName(HeroClassType heroClass)
        {
            return heroClass switch
            {
                HeroClassType.SpiritSummoner => "万灵召使",
                HeroClassType.ThunderMage => "雷火方士",
                _ => "边境指挥官"
            };
        }

        private static UnitDefinition Unit(ContentCatalog catalog, string id, string displayName, Faction faction, UnitRole role, float hp, float attack, float interval, float range, float speed, int command, Color tint, string artPath)
        {
            var unit = catalog.FindUnit(id);
            if (unit == null)
            {
                unit = ScriptableObject.CreateInstance<UnitDefinition>();
                unit.name = id;
                unit.id = id;
                catalog.units.Add(unit);
            }

            unit.displayName = displayName;
            unit.faction = faction;
            unit.role = role;
            unit.maxHp = hp;
            unit.attack = attack;
            unit.attackInterval = interval;
            unit.range = range;
            unit.moveSpeed = speed;
            unit.commandCost = command;
            unit.art = LoadSprite(artPath) ?? unit.art;
            unit.tint = unit.art == null ? tint : Color.white;
            return unit;
        }

        private static void UpsertLeveledCard(ContentCatalog catalog, string baseId, string displayName, CardType type, int baseCost, CardReleaseRule releaseRule, string description, string artPath, Func<int, CardPayload> payloadFactory)
        {
            for (var level = 1; level <= 3; level++)
            {
                var id = LevelId(baseId, level);
                var card = catalog.FindCard(id);
                if (card == null)
                {
                    card = ScriptableObject.CreateInstance<CardDefinition>();
                    card.name = id;
                    card.id = id;
                    catalog.cards.Add(card);
                }

                var payload = payloadFactory(level);
                card.displayName = level == 1 ? displayName : $"{displayName}+{level - 1}";
                card.type = type;
                card.rarity = level switch
                {
                    2 => CardRarity.Uncommon,
                    3 => CardRarity.Rare,
                    _ => CardRarity.Common
                };
                card.level = level;
                card.cost = Mathf.Max(0, baseCost + payload.costOffset);
                card.releaseRule = releaseRule;
                card.description = description;
                card.art = LoadSprite(artPath) ?? card.art;
                card.unitSpawns.Clear();
                card.unitSpawns.AddRange(payload.spawns);
                card.effects.Clear();
                card.effects.AddRange(payload.effects);
            }
        }

        private static void UpsertFixedCard(ContentCatalog catalog, string id, string displayName, CardType type, CardRarity rarity, int level, int cost, CardReleaseRule releaseRule, string description, string artPath, Func<CardPayload> payloadFactory)
        {
            var card = catalog.FindCard(id);
            if (card == null)
            {
                card = ScriptableObject.CreateInstance<CardDefinition>();
                card.name = id;
                card.id = id;
                catalog.cards.Add(card);
            }

            var payload = payloadFactory();
            card.displayName = displayName;
            card.type = type;
            card.rarity = rarity;
            card.level = level;
            card.cost = Mathf.Max(0, cost + payload.costOffset);
            card.releaseRule = releaseRule;
            card.description = description;
            card.art = LoadSprite(artPath) ?? card.art;
            card.unitSpawns.Clear();
            card.unitSpawns.AddRange(payload.spawns);
            card.effects.Clear();
            card.effects.AddRange(payload.effects);
        }

        private static void UpsertCurseCard(ContentCatalog catalog, string id, string displayName, string description, string artPath)
        {
            UpsertFixedCard(catalog, id, displayName, CardType.Curse, CardRarity.Common, 1, 0, CardReleaseRule.None, description, artPath, () => new CardPayload());
        }

        public static string LevelId(string baseId, int level)
        {
            return level <= 1 ? baseId : $"{baseId}_lv{level}";
        }

        public static string BaseCardId(string id)
        {
            if (id == null)
            {
                return string.Empty;
            }

            var marker = id.LastIndexOf("_lv", StringComparison.Ordinal);
            return marker > 0 ? id.Substring(0, marker) : id;
        }

        public static string UpgradeCardId(string id)
        {
            var level = CardLevelFromId(id);
            return level >= 3 ? id : LevelId(BaseCardId(id), level + 1);
        }

        public static int CardLevelFromId(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return 1;
            }

            if (id.EndsWith("_lv2", StringComparison.Ordinal)) return 2;
            if (id.EndsWith("_lv3", StringComparison.Ordinal)) return 3;
            return 1;
        }

        private static CardPayload CreateCardPayload(int level)
        {
            return new CardPayload { costOffset = level == 3 ? 1 : 0 };
        }

        private static CardUnitSpawn Spawn(UnitDefinition unit, int count)
        {
            return new CardUnitSpawn { unit = unit, count = count };
        }

        private static BattleEffectDefinition Effect(EffectType type, TargetRule target, float value, float duration, float radius)
        {
            return new BattleEffectDefinition
            {
                effectType = type,
                targetRule = target,
                value = value,
                duration = duration,
                radius = radius
            };
        }

        private static EnemySpawnEntry SpawnEnemy(UnitDefinition unit, int count)
        {
            return new EnemySpawnEntry { unit = unit, count = count };
        }

        private static void Artifact(ContentCatalog catalog, string id, string displayName, ArtifactRarity rarity, string description)
        {
            var artifact = catalog.FindArtifact(id);
            if (artifact == null)
            {
                artifact = ScriptableObject.CreateInstance<ArtifactDefinition>();
                artifact.name = id;
                artifact.id = id;
                catalog.artifacts.Add(artifact);
            }

            artifact.displayName = displayName;
            artifact.rarity = rarity;
            artifact.description = description;
            artifact.trigger = ArtifactTrigger.Passive;
            artifact.icon = LoadSprite($"Assets/_Project/Art/AI/UI/Artifacts/{id}.png") ?? artifact.icon;
        }

        private static void Encounter(ContentCatalog catalog, string id, string displayName, MapNodeType nodeType, float playerHp, float enemyHp, float interval, int rewardGold, UnitDefinition coreEnemy, params EnemySpawnEntry[] spawns)
        {
            var encounter = catalog.FindEncounter(id);
            if (encounter == null)
            {
                encounter = ScriptableObject.CreateInstance<EncounterDefinition>();
                encounter.name = id;
                encounter.id = id;
                catalog.encounters.Add(encounter);
            }

            encounter.displayName = displayName;
            encounter.nodeType = nodeType;
            encounter.playerBaseMaxHp = playerHp;
            encounter.enemyBaseMaxHp = enemyHp;
            encounter.enemySpawnInterval = interval;
            encounter.rewardGold = rewardGold;
            encounter.coreEnemy = coreEnemy;
            encounter.enemySpawns ??= new List<EnemySpawnEntry>();
            encounter.enemySpawns.Clear();
            encounter.enemySpawns.AddRange(spawns.Where(spawn => spawn?.unit != null));
        }

        private static void Add(RunState run, string cardId, int count)
        {
            for (var i = 0; i < count; i++)
            {
                run.deckCardIds.Add(cardId);
            }
        }

        private static void CopyLeveledCardArt(ContentCatalog catalog, string targetBaseId, string sourceBaseId)
        {
            for (var level = 1; level <= 3; level++)
            {
                var target = catalog.FindCard(LevelId(targetBaseId, level));
                var source = catalog.FindCard(LevelId(sourceBaseId, level)) ?? catalog.FindCard(sourceBaseId);
                if (target != null && target.art == null && source != null)
                {
                    target.art = source.art;
                }
            }
        }

        private static void CopyCardArt(ContentCatalog catalog, string targetId, string sourceId)
        {
            var target = catalog.FindCard(targetId);
            var source = catalog.FindCard(sourceId);
            if (target != null && target.art == null && source != null)
            {
                target.art = source.art;
            }
        }

        private static Sprite LoadSprite(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            if (LoadedSprites.TryGetValue(path, out var cached))
            {
                return cached;
            }

            Sprite sprite = null;
#if UNITY_EDITOR
            sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
            {
                LoadedSprites[path] = sprite;
                return sprite;
            }
#endif

            var resourcePath = ToResourcePath(path);
            if (!string.IsNullOrWhiteSpace(resourcePath))
            {
                sprite = Resources.Load<Sprite>(resourcePath);
                if (sprite == null)
                {
                    var texture = Resources.Load<Texture2D>(resourcePath);
                    if (texture != null)
                    {
                        sprite = Sprite.Create(
                            texture,
                            new Rect(0f, 0f, texture.width, texture.height),
                            new Vector2(0.5f, 0.5f),
                            PixelsPerUnitForResource(resourcePath));
                        sprite.name = texture.name;
                    }
                }
            }

            if (sprite != null)
            {
                LoadedSprites[path] = sprite;
            }

            return sprite;
        }

        private static string ToResourcePath(string path)
        {
            var normalized = path.Replace("\\", "/");
            if (normalized.StartsWith(ProjectAiArtPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return RuntimeAiArtPrefix + RemoveExtension(normalized.Substring(ProjectAiArtPrefix.Length));
            }

            if (normalized.StartsWith(ResourcesAssetPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return RemoveExtension(normalized.Substring(ResourcesAssetPrefix.Length));
            }

            return null;
        }

        private static string RemoveExtension(string path)
        {
            var slash = path.LastIndexOf('/');
            var dot = path.LastIndexOf('.');
            return dot > slash ? path.Substring(0, dot) : path;
        }

        private static float PixelsPerUnitForResource(string resourcePath)
        {
            return resourcePath.Contains("/Backgrounds/", StringComparison.OrdinalIgnoreCase) ? 128f : 256f;
        }

        private sealed class CardPayload
        {
            public int costOffset;
            public readonly List<CardUnitSpawn> spawns = new();
            public readonly List<BattleEffectDefinition> effects = new();
        }
    }
}
