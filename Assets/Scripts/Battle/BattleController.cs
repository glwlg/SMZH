using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using XTD.Cards;
using XTD.Content;
using XTD.Flow;
using XTD.Presentation;
using Random = UnityEngine.Random;

namespace XTD.Battle
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class BattleController : MonoBehaviour
    {
        private const string BattleMusicResourcePath = "Audio/BGM/hyoshi_action_track_2";
        private const string BattleMusicAssetPath = "Assets/Resources/Audio/BGM/hyoshi_action_track_2.ogg";
        private const string BattleMusicStreamingRelativePath = "Audio/BGM/hyoshi_action_track_2.ogg";
        private const string ProjectileResourcePath = "Art/AI/FX/projectile_spirit_arrow";
        private const string HitEffectResourcePath = "Art/AI/FX/fx_hit_jade_spark";
        private const string SpellImpactResourcePath = "Art/AI/FX/fx_samadhi_fire_impact";
        private const string CommanderDivineEffectResourcePath = "Art/AI/FX/fx_divine_commander_order";
        private const string SummonerDivineEffectResourcePath = "Art/AI/FX/fx_divine_summon_gate";
        private const string ThunderDivineEffectResourcePath = "Art/AI/FX/fx_divine_thunder_fire";
        private const float DivinePowerManaCost = 7f;
        private const float ThunderMageSpellRadiusMultiplier = 1.35f;
        private const int CloudBannerStructureCostReduction = 2;
        private const float StructurePlacementRadius = 0.34f;
        private const float StructurePlacementMinDistance = 0.64f;
        private const float StructurePlacementSpacing = 0.78f;
        private static readonly string[] HitSfxResourcePaths =
        {
            "Audio/SFX/attack_hit",
            "Audio/SFX/attack_hit_1",
            "Audio/SFX/hit01",
            "Audio/SFX/thud2",
            "Audio/SFX/clink1"
        };

        [Header("Content")]
        [SerializeField] private ContentCatalog defaultCatalog;
        [SerializeField] private string encounterId = "encounter_training_camp";

        [Header("Battle Rules")]
        [SerializeField] private int tickRate = 30;
        [SerializeField] private float laneX = 0f;
        [SerializeField] private float playerBaseY = -3.9f;
        [SerializeField] private float enemyBaseY = 3.45f;
        [SerializeField] private float manaRegenPerSecond = 0.75f;
        [SerializeField] private int maxMana = 10;
        [SerializeField] private int maxCommand = 30;
        [SerializeField] private float placementMinX = -7.6f;
        [SerializeField] private float placementMaxX = 7.6f;

        [Header("Presentation")]
        [SerializeField] private Sprite playerProjectileSprite;
        [SerializeField] private Sprite enemyProjectileSprite;
        [SerializeField] private Sprite hitEffectSprite;
        [SerializeField] private Sprite spellImpactSprite;
        [SerializeField] private Sprite commanderDivineEffectSprite;
        [SerializeField] private Sprite summonerDivineEffectSprite;
        [SerializeField] private Sprite thunderDivineEffectSprite;

        [Header("Audio")]
        [SerializeField] private AudioClip battleMusicClip;
        [SerializeField, Range(0f, 1f)] private float battleMusicVolume = 0.22f;
        [SerializeField] private AudioClip[] hitSfxClips;
        [SerializeField, Range(0f, 1f)] private float hitSfxVolume = 0.12f;

        private readonly List<BattleUnit> activeUnits = new();
        private readonly HashSet<string> defeatedHeroUnitIds = new();
        private readonly MoraleTracker morale = new();
        private ComponentPool<BattleUnit> unitPool;
        private ComponentPool<ProjectileView> projectilePool;
        private ComponentPool<DamageNumberView> damageNumberPool;
        private ComponentPool<SimpleEffectView> effectPool;
        private ContentCatalog catalog;
        private EncounterDefinition encounter;
        private DeckRuntime deck;
        private BattleUiController ui;
        private BattleBaseView playerBaseView;
        private BattleBaseView enemyBaseView;
        private float tickAccumulator;
        private float enemySpawnTimer;
        private float coreAreaSkillTimer;
        private float coreBuffSkillTimer;
        private float coreWarningTimer;
        private Vector3 pendingCoreBlastPosition;
        private float pendingCoreBlastRadius;
        private float pendingCoreBlastDamage;
        private bool corePhaseTwoTriggered;
        private bool corePhaseThreeTriggered;
        private float manaSuppressionTimer;
        private float floorAffixTimer;
        private float mana;
        private int baseMaxMana;
        private int baseMaxCommand;
        private float baseManaRegenPerSecond;
        private GameFlowController flow;
        private AudioSource audioSource;
        private AudioSource musicSource;
        private AudioClip playCardClip;
        private AudioClip summonClip;
        private AudioClip hitClip;
        private AudioClip victoryClip;
        private AudioClip defeatClip;
        private AudioClip fallbackBattleMusicClip;
        private Coroutine battleMusicLoadRoutine;
        private float hitSfxCooldown;
        private int temporaryStructureCostReduction;
        private int defeatedEnemyCount;

        public BattleOutcome Outcome { get; private set; } = BattleOutcome.Running;
        public float PlayerBaseHp { get; private set; }
        public float EnemyBaseHp { get; private set; }
        public float Mana => mana;
        public int MaxMana => maxMana;
        public float PlayerBaseLaneY => playerBaseY;
        public float EnemyBaseLaneY => enemyBaseY;
        public float LaneMinX => placementMinX;
        public float LaneMaxX => placementMaxX;
        public int CurrentCommand => activeUnits
            .Count(unit => unit != null && unit.IsAlive && unit.Faction == Faction.Player && unit.Definition != null && unit.Definition.role == UnitRole.Structure);
        public int MaxCommand => maxCommand;
        public int MoraleCharges => morale.Charges;
        public int MoralePendingSoldiers => morale.PendingSoldiers;
        public int MoraleSoldiersPerCharge => morale.SoldiersPerCharge;
        public bool NextCardWillUseMorale => morale.Charges > 0;
        public int PendingStructureCostReduction => temporaryStructureCostReduction;
        public bool HasPendingStructureDiscount => temporaryStructureCostReduction > 0;
        public DeckRuntime Deck => deck;
        public float BattleMidY => (playerBaseY + enemyBaseY) * 0.5f;
        public bool HasEnemyBase => encounter == null || encounter.coreEnemy == null;
        public string EnemyObjectiveLabel => HasEnemyBase ? "敌方基地" : "敌方核心";
        public float EnemyObjectiveHp => HasEnemyBase ? EnemyBaseHp : Mathf.Max(0f, EnemyCoreHp);
        public float EnemyObjectiveMaxHp => HasEnemyBase
            ? Mathf.Max(1f, encounter != null ? encounter.enemyBaseMaxHp : 120f)
            : Mathf.Max(1f, encounter?.coreEnemy != null ? encounter.coreEnemy.maxHp * MaxHpMultiplierFor(encounter.coreEnemy, Faction.Enemy) : 1f);
        public float EnemyCoreHp => EnemyCoreUnit()?.CurrentHp ?? 0f;
        public string EncounterDisplayName => encounter != null ? encounter.displayName : EnemyObjectiveLabel;
        public bool IsBossLikeEncounter => encounter != null && (encounter.coreEnemy != null || encounter.nodeType is MapNodeType.EliteMonster or MapNodeType.SmallBoss or MapNodeType.FinalBoss);
        public string BattleStageLabel => flow != null && flow.HasActiveRun
            ? $"迷宫 {flow.CurrentRun.floor}/3 · {GameFlowController.NodeTypeName(encounter != null ? encounter.nodeType : MapNodeType.NormalMonster)}"
            : "战斗原型";
        public string HeroClassLabel => flow != null && flow.HasActiveRun
            ? GameFlowController.HeroClassName(flow.CurrentHeroClass)
            : "战斗原型";
        public string HeroClassStyle => flow != null && flow.HasActiveRun
            ? GameFlowController.HeroClassShortStyle(flow.CurrentHeroClass)
            : "基础对抗";
        public int CurrentRow => flow != null && flow.HasActiveRun ? flow.CurrentRun.row : 1;
        public int EnemyUnitCount => activeUnits.Count(unit => unit != null && unit.IsAlive && unit.Faction == Faction.Enemy);
        public int PlayerUnitCount => activeUnits.Count(unit => unit != null && unit.IsAlive && unit.Faction == Faction.Player);
        public int DefeatedEnemyCount => defeatedEnemyCount;
        public float PlayerBaseMaxHp => CurrentPlayerBattleMaxHp();
        public float DivinePowerCost => DivinePowerManaCost;
        public bool CanReleaseDivinePower => Outcome == BattleOutcome.Running && mana >= DivinePowerManaCost;
        public float DivinePowerCharge => Mathf.Clamp01(mana / DivinePowerManaCost);
        public string DivinePowerName => CurrentHeroClassForBattle() switch
        {
            HeroClassType.SpiritSummoner => "万灵开阵",
            HeroClassType.ThunderMage => "九霄雷火",
            _ => "边境战令"
        };

        public IReadOnlyList<string> EnemySkillHints => BuildEnemySkillHints();

        private void Awake()
        {
            catalog = ResolveContentCatalog();
            DemoContentFactory.EnsureCatalogComplete(catalog);
            flow = GameFlowController.Instance;
            if (flow != null && flow.HasActiveRun && flow.HasPendingNode)
            {
                flow.ConfigureCatalog(catalog);
                encounter = flow.PendingEncounterOrDefault(catalog);
            }

            encounter ??= !string.IsNullOrWhiteSpace(encounterId) ? catalog.FindEncounter(encounterId) : null;
            encounter ??= catalog.FirstEncounter(MapNodeType.NormalMonster);
            baseMaxMana = maxMana;
            baseMaxCommand = maxCommand;
            baseManaRegenPerSecond = manaRegenPerSecond;
            EnsurePresentationSprites();

            unitPool = new ComponentPool<BattleUnit>(CreateUnitInstance);
            projectilePool = new ComponentPool<ProjectileView>(CreateProjectileInstance);
            damageNumberPool = new ComponentPool<DamageNumberView>(CreateDamageNumberInstance);
            effectPool = new ComponentPool<SimpleEffectView>(CreateEffectInstance);

            ui = FindAnyObjectByType<BattleUiController>();
            if (ui == null)
            {
                ui = BattleUiController.CreateDefault();
            }

            ui.Bind(this);
            ConfigureAudio();
        }

        private void Start()
        {
            StartPrototypeBattle();
        }

        private void Update()
        {
            if (Outcome != BattleOutcome.Running)
            {
                return;
            }

            tickAccumulator += Time.deltaTime;
            var fixedDelta = 1f / tickRate;
            while (tickAccumulator >= fixedDelta)
            {
                Tick(fixedDelta);
                tickAccumulator -= fixedDelta;
            }

            CleanupUnavailableHeroCards();
            ui.RequestRefresh();
        }

        public void StartPrototypeBattle()
        {
            Outcome = BattleOutcome.Running;
            for (var i = activeUnits.Count - 1; i >= 0; i--)
            {
                if (activeUnits[i] != null)
                {
                    unitPool.Release(activeUnits[i]);
                }
            }

            activeUnits.Clear();
            defeatedHeroUnitIds.Clear();
            morale.Reset();
            ApplyRunBattleModifiers();
            mana = Mathf.Min(maxMana, 4f + (flow != null && flow.HasActiveRun ? flow.ExtraStartingMana() : 0f));
            enemySpawnTimer = 0.5f;
            coreAreaSkillTimer = 3.2f;
            coreBuffSkillTimer = 5.5f;
            coreWarningTimer = 0f;
            corePhaseTwoTriggered = false;
            corePhaseThreeTriggered = false;
            manaSuppressionTimer = 0f;
            floorAffixTimer = 3.0f;
            hitSfxCooldown = 0f;
            temporaryStructureCostReduction = 0;
            defeatedEnemyCount = 0;
            StartBattleMusic();
            var playerMaxHp = CurrentPlayerBattleMaxHp();
            PlayerBaseHp = flow != null && flow.HasActiveRun
                ? Mathf.Clamp(flow.CurrentRun.playerHp + flow.BattleStartHpBonus(), 1f, playerMaxHp)
                : playerMaxHp;
            EnemyBaseHp = HasEnemyBase && encounter != null ? encounter.enemyBaseMaxHp : 0f;
            EnsureBaseViews();
            RefreshBaseViews();

            var runState = flow != null && flow.HasActiveRun
                ? flow.CurrentRun
                : DemoContentFactory.CreateStartingRun(catalog);
            var startingCards = runState.deckCardIds
                .Select(id => catalog.FindCard(id))
                .Where(card => card != null);
            deck = new DeckRuntime(startingCards, runState.seed);
            deck.MaxHandSize = 5 + (flow != null && flow.HasActiveRun ? flow.StartingHandBonus() : 0);
            deck.DrawFullHand();
            ui.HideResult();
            ui.Refresh();

            if (encounter != null && encounter.coreEnemy != null)
            {
                SpawnUnit(encounter.coreEnemy, Faction.Enemy, EnemyCorePosition(), false);
            }
        }

        public bool TryPlayCard(CardDefinition card)
        {
            return TryPlayCard(card, new Vector3(laneX, playerBaseY + 0.85f, 0f));
        }

        public bool TryPlayCard(CardDefinition card, Vector3 targetPosition)
        {
            if (Outcome != BattleOutcome.Running || card == null || deck == null || !deck.ContainsInHand(card))
            {
                return false;
            }

            if (!CanReleaseCardAt(card, targetPosition, out _))
            {
                return false;
            }

            var strengthened = card.CanReceiveMorale && morale.TryConsume();
            var commandCost = CalculateCommandCost(card, strengthened);
            if (CurrentCommand + commandCost > maxCommand)
            {
                if (strengthened)
                {
                    morale.RefundCharge();
                }

                return false;
            }

            var cardCost = EffectiveCardCost(card);
            var consumesCloudBannerDiscount = card.type == CardType.Structure && temporaryStructureCostReduction > 0;
            mana -= cardCost;
            deck.Play(card);
            if (consumesCloudBannerDiscount)
            {
                temporaryStructureCostReduction = 0;
            }

            ResolveCard(card, strengthened, targetPosition);
            PlayOneShot(ref playCardClip, 540f, 0.06f);
            if (strengthened)
            {
                SpawnMoraleEffect(targetPosition);
                ui.ShowNotice(MoraleNotice(card));
            }

            deck.RefillHandIfEmpty();
            ui.Refresh();
            return true;
        }

        public bool CanPlayCard(CardDefinition card)
        {
            if (Outcome != BattleOutcome.Running || card == null || deck == null || !deck.ContainsInHand(card))
            {
                return false;
            }

            if (mana < EffectiveCardCost(card))
            {
                return false;
            }

            if (IsHeroAlreadyPresent(card))
            {
                return false;
            }

            if (IsHeroDefeatedThisBattle(card))
            {
                return false;
            }

            var wouldUseMorale = card.CanReceiveMorale && morale.Charges > 0;
            return CurrentCommand + CalculateCommandCost(card, wouldUseMorale) <= maxCommand;
        }

        public int EffectiveCardCost(CardDefinition card)
        {
            if (card == null)
            {
                return 0;
            }

            var modifier = flow != null && flow.HasActiveRun ? flow.CardCostModifier(card) : 0;
            if (card.type == CardType.Structure && temporaryStructureCostReduction > 0)
            {
                modifier -= temporaryStructureCostReduction;
            }

            return Mathf.Max(0, card.cost + modifier);
        }

        public bool CanReleaseCardAt(CardDefinition card, Vector3 targetPosition, out string reason)
        {
            reason = string.Empty;
            if (IsHeroAlreadyPresent(card))
            {
                reason = "该英雄已经在场";
                return false;
            }

            if (IsHeroDefeatedThisBattle(card))
            {
                reason = "该英雄本场已阵亡";
                return false;
            }

            if (!CanPlayCard(card))
            {
                reason = "费用或建筑位不足";
                return false;
            }

            if (card.releaseRule == CardReleaseRule.PlayerSide && targetPosition.y > BattleMidY - 0.15f)
            {
                reason = "建筑和召唤不能越过中线";
                return false;
            }

            if (CardPlacesStructure(card))
            {
                if (targetPosition.y < playerBaseY + 0.10f)
                {
                    reason = "建筑不能放在手牌区或基地后方";
                    return false;
                }

                if (!CanPlaceStructures(card, targetPosition, out reason))
                {
                    return false;
                }
            }

            if (card.releaseRule != CardReleaseRule.None && (targetPosition.x < placementMinX || targetPosition.x > placementMaxX))
            {
                reason = "超出战场范围";
                return false;
            }

            return true;
        }

        public bool CardPlacesStructure(CardDefinition card)
        {
            if (card == null)
            {
                return false;
            }

            return card.unitSpawns.Any(spawn => spawn?.unit != null && spawn.unit.role == UnitRole.Structure && spawn.count > 0);
        }

        public float PreviewRadiusForCard(CardDefinition card)
        {
            if (card == null)
            {
                return 0f;
            }

            if (CardPlacesStructure(card))
            {
                return StructurePlacementRadius;
            }

            if (card.releaseRule != CardReleaseRule.Anywhere)
            {
                return 0f;
            }

            var radius = 0f;
            foreach (var effect in card.effects)
            {
                if (effect == null)
                {
                    continue;
                }

                if (effect.effectType is EffectType.Damage or EffectType.AreaDamage or EffectType.Slow or EffectType.Stun or EffectType.Burn or EffectType.Poison or EffectType.Knockback)
                {
                    radius = Mathf.Max(radius, EffectiveEffectRadiusForCard(card, effect, true));
                }
            }

            return radius > 0f ? radius : 0.95f;
        }

        public string CardBlockReason(CardDefinition card)
        {
            if (card == null)
            {
                return string.Empty;
            }

            if (IsHeroAlreadyPresent(card))
            {
                return "该英雄已经在场";
            }

            if (IsHeroDefeatedThisBattle(card))
            {
                return "该英雄本场已阵亡";
            }

            if (mana < EffectiveCardCost(card))
            {
                return "费用不足";
            }

            var wouldUseMorale = card.CanReceiveMorale && morale.Charges > 0;
            if (CurrentCommand + CalculateCommandCost(card, wouldUseMorale) > maxCommand)
            {
                return "建筑位不足";
            }

            return string.Empty;
        }

        private bool IsHeroAlreadyPresent(CardDefinition card)
        {
            if (card == null || card.type != CardType.Hero)
            {
                return false;
            }

            foreach (var spawn in card.unitSpawns)
            {
                if (spawn?.unit == null || spawn.unit.role != UnitRole.Hero)
                {
                    continue;
                }

                if (activeUnits.Any(unit =>
                    unit != null &&
                    unit.IsAlive &&
                    unit.Faction == Faction.Player &&
                    unit.Definition == spawn.unit))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsHeroDefeatedThisBattle(CardDefinition card)
        {
            if (card == null || card.type != CardType.Hero)
            {
                return false;
            }

            foreach (var spawn in card.unitSpawns)
            {
                if (spawn?.unit == null || spawn.unit.role != UnitRole.Hero)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(spawn.unit.id) && defeatedHeroUnitIds.Contains(spawn.unit.id))
                {
                    return true;
                }
            }

            return false;
        }

        public BattleUnit FindTargetFor(BattleUnit seeker)
        {
            var enemies = activeUnits
                .Where(unit => unit != null && unit.IsAlive && unit.Faction != seeker.Faction)
                .OrderBy(unit => Vector2.Distance(unit.transform.position, seeker.transform.position));
            return enemies.FirstOrDefault();
        }

        public bool IsEnemyBaseInRange(BattleUnit unit)
        {
            if (!CanUnitAttackBase(unit) || unit.Definition == null)
            {
                return false;
            }

            var targetBaseY = unit.Faction == Faction.Player ? enemyBaseY : playerBaseY;
            return Mathf.Abs(targetBaseY - unit.transform.position.y) <= Mathf.Max(0.25f, unit.Definition.range * RangeMultiplierFor(unit.Definition, unit.Faction));
        }

        public bool CanUnitAttackBase(BattleUnit unit)
        {
            if (unit == null)
            {
                return false;
            }

            if (unit.Faction == Faction.Player)
            {
                return HasEnemyBase;
            }

            return unit.Definition != null && unit.Definition.role != UnitRole.Boss;
        }

        public Vector3 GetAdvanceTargetFor(BattleUnit unit)
        {
            var x = Mathf.Clamp(unit.transform.position.x, placementMinX, placementMaxX);
            var targetY = unit.Faction == Faction.Player ? enemyBaseY : playerBaseY;
            return new Vector3(x, targetY, 0f);
        }

        public void DamageEnemyBase(float damage)
        {
            if (!HasEnemyBase)
            {
                return;
            }

            EnemyBaseHp -= damage;
            enemyBaseView?.Flash();
            enemyBaseView?.UpdateHealth(EnemyBaseHp, encounter != null ? encounter.enemyBaseMaxHp : 120f);
            SpawnDamageNumber(EnemyBaseViewPosition(), damage);
            CheckOutcome();
        }

        public void DamagePlayerBase(float damage)
        {
            PlayerBaseHp -= damage;
            playerBaseView?.Flash();
            playerBaseView?.UpdateHealth(PlayerBaseHp, CurrentPlayerBattleMaxHp());
            SpawnDamageNumber(PlayerBaseViewPosition(), damage);
            CheckOutcome();
        }

        public void ReleaseUnit(BattleUnit unit)
        {
            activeUnits.Remove(unit);
            unitPool.Release(unit);
        }

        public void NotifyUnitDied(BattleUnit unit)
        {
            if (unit != null && unit.Faction == Faction.Enemy)
            {
                defeatedEnemyCount++;
            }

            if (unit != null && unit.Faction == Faction.Player && unit.Definition != null && unit.Definition.role == UnitRole.Hero)
            {
                defeatedHeroUnitIds.Add(unit.Definition.id);
                ExhaustHeroCardsForBattle(unit.Definition.id);
                ui?.ShowNotice($"{unit.Definition.displayName} 已阵亡，本场不能再次召唤", 1.8f);
            }
        }

        public void SpawnProjectile(Vector3 start, Vector3 end, Faction faction)
        {
            var projectile = projectilePool.Get();
            var sprite = faction == Faction.Player ? playerProjectileSprite : enemyProjectileSprite;
            projectile.Initialize(start, end, faction, sprite, () => projectilePool.Release(projectile));
        }

        public void SpawnDamageNumber(Vector3 position, float value)
        {
            var number = damageNumberPool.Get();
            number.Initialize(position, Mathf.CeilToInt(value), () => damageNumberPool.Release(number));
        }

        public void SpawnHitEffect(Vector3 position, Faction faction)
        {
            var effect = effectPool.Get();
            effect.Initialize(position, faction, hitEffectSprite, 0.25f, () => effectPool.Release(effect));
            if (hitSfxCooldown <= 0f)
            {
                PlayHitSfx(faction);
                hitSfxCooldown = 0.08f;
            }
        }

        public void SpawnSpellImpact(Vector3 position)
        {
            var effect = effectPool.Get();
            effect.Initialize(position, Faction.Player, spellImpactSprite != null ? spellImpactSprite : hitEffectSprite, 0.55f, () => effectPool.Release(effect));
        }

        private void SpawnDivineEffect(Vector3 position, Sprite sprite, Color color, float startScale, float endScale, float duration)
        {
            var effect = effectPool.Get();
            effect.InitializeCustom(position, color, sprite != null ? sprite : spellImpactSprite, startScale, endScale, duration, 36, () => effectPool.Release(effect));
        }

        public void SpawnDeathEffect(Vector3 position, Faction faction)
        {
            var effect = effectPool.Get();
            effect.InitializeCustom(position, faction == Faction.Player ? new Color(0.58f, 0.88f, 1f, 0.9f) : new Color(1f, 0.36f, 0.25f, 0.9f), hitEffectSprite, 0.28f, 1.15f, 0.42f, 28, () => effectPool.Release(effect));
        }

        public void SpawnMoraleEffect(Vector3 position)
        {
            var effect = effectPool.Get();
            effect.InitializeCustom(position, new Color(1f, 0.86f, 0.22f, 0.92f), hitEffectSprite, 0.45f, 1.75f, 0.55f, 32, () => effectPool.Release(effect));
        }

        public bool TryReleaseDivinePower()
        {
            if (Outcome != BattleOutcome.Running)
            {
                return false;
            }

            if (mana < DivinePowerManaCost)
            {
                ui.ShowNotice($"神通需要 {DivinePowerManaCost:0} 点费用");
                return false;
            }

            mana -= DivinePowerManaCost;
            var heroClass = CurrentHeroClassForBattle();
            switch (heroClass)
            {
                case HeroClassType.SpiritSummoner:
                    ReleaseSummonerDivinePower();
                    break;
                case HeroClassType.ThunderMage:
                    ReleaseThunderMageDivinePower();
                    break;
                default:
                    ReleaseCommanderDivinePower();
                    break;
            }

            ui.Refresh();
            CheckOutcome();
            return true;
        }

        private void ReleaseCommanderDivinePower()
        {
            var damage = 24f + (flow != null && flow.HasActiveRun ? flow.CurrentRun.floor * 4f : 0f);
            var targets = activeUnits
                .Where(unit => unit != null && unit.IsAlive && unit.Faction == Faction.Enemy)
                .ToList();

            foreach (var target in targets)
            {
                SpawnWarningCircle(target.transform.position, 1.15f);
                target.TakeDamage(damage);
            }

            if (HasEnemyBase)
            {
                DamageEnemyBase(damage * 0.75f);
            }
            else
            {
                var core = EnemyCoreUnit();
                if (core != null && core.IsAlive && !targets.Contains(core))
                {
                    core.TakeDamage(damage);
                }
            }

            var allies = activeUnits
                .Where(unit => unit != null && unit.IsAlive && unit.Faction == Faction.Player)
                .Take(12)
                .ToList();
            foreach (var ally in allies)
            {
                ally.AddShield(10f);
                ally.AddModifier(EffectType.BuffAttack, 0.18f, 5f);
            }

            morale.AddCharges(1);
            SpawnDivineEffect(new Vector3(laneX, BattleMidY + 0.9f, 0f), commanderDivineEffectSprite, new Color(1f, 0.78f, 0.22f, 0.92f), 0.72f, 2.2f, 0.85f);
            SpawnMoraleEffect(new Vector3(laneX, playerBaseY + 1.15f, 0f));
            ui.ShowNotice($"边境战令：敌军受创 {damage:0}，前线加盾，士气 +1", 1.8f);
        }

        private void ReleaseSummonerDivinePower()
        {
            var militia = catalog.FindUnit("unit_militia");
            var archer = catalog.FindUnit("unit_archer");
            var spawned = 0;
            for (var i = 0; i < 6; i++)
            {
                if (militia == null)
                {
                    break;
                }

                var x = Mathf.Lerp(-2.6f, 2.6f, i / 5f);
                var y = playerBaseY + 0.85f + (i % 2) * 0.36f;
                var unit = SpawnUnit(militia, Faction.Player, new Vector3(x, y, 0f), true);
                if (unit == null)
                {
                    continue;
                }

                unit.AddShield(7f);
                spawned++;
            }

            for (var i = 0; i < 2; i++)
            {
                if (archer == null)
                {
                    break;
                }

                var x = i == 0 ? -1.25f : 1.25f;
                var unit = SpawnUnit(archer, Faction.Player, new Vector3(x, playerBaseY + 0.55f, 0f), true);
                if (unit == null)
                {
                    continue;
                }

                unit.AddModifier(EffectType.BuffAttackSpeed, 0.18f, 6f);
                spawned++;
            }

            if (spawned > 0)
            {
                morale.RegisterSummonedSoldiers(spawned);
            }

            SpawnDivineEffect(new Vector3(laneX, playerBaseY + 1.1f, 0f), summonerDivineEffectSprite, new Color(0.60f, 1f, 0.82f, 0.9f), 0.65f, 2.1f, 0.9f);
            ui.ShowNotice($"万灵开阵：立刻召来 {spawned} 名援军并推进士气", 1.8f);
        }

        private void ReleaseThunderMageDivinePower()
        {
            var damage = 38f + (flow != null && flow.HasActiveRun ? flow.CurrentRun.floor * 6f : 0f);
            var targets = activeUnits
                .Where(unit => unit != null && unit.IsAlive && unit.Faction == Faction.Enemy)
                .ToList();

            foreach (var target in targets)
            {
                SpawnWarningCircle(target.transform.position, 1.25f);
                target.TakeDamage(damage);
                if (target.IsAlive && target.Definition != null && target.Definition.role != UnitRole.Boss)
                {
                    target.AddModifier(EffectType.Stun, 1f, 0.55f);
                    target.AddModifier(EffectType.Slow, 0.45f, 3.2f);
                }
            }

            if (HasEnemyBase)
            {
                DamageEnemyBase(damage);
            }

            SpawnDivineEffect(new Vector3(laneX, BattleMidY + 0.95f, 0f), thunderDivineEffectSprite, new Color(0.65f, 0.88f, 1f, 0.92f), 0.78f, 2.35f, 0.85f);
            ui.ShowNotice($"九霄雷火：全场雷击 {damage:0}，非首领短暂定身", 1.8f);
        }

        public void SpawnWarningCircle(Vector3 position, float radius)
        {
            var effect = effectPool.Get();
            effect.InitializeCustom(position, new Color(1f, 0.12f, 0.08f, 0.42f), RuntimeSpriteFactory.EffectSprite, Mathf.Max(0.3f, radius * 0.55f), Mathf.Max(0.4f, radius * 0.72f), 0.72f, 18, () => effectPool.Release(effect));
        }

        public bool TrySpawnProducedUnit(UnitDefinition unitDefinition, Faction faction, Vector3 position)
        {
            if (unitDefinition == null || Outcome != BattleOutcome.Running)
            {
                return false;
            }

            var unit = SpawnUnit(unitDefinition, faction, position, true);
            if (unit == null)
            {
                return false;
            }

            if (faction == Faction.Player && (unitDefinition.role == UnitRole.Soldier || unitDefinition.role == UnitRole.Elite))
            {
                morale.RegisterSummonedSoldiers(1);
            }

            return true;
        }

        private void Tick(float deltaTime)
        {
            var manaRegenMultiplier = manaSuppressionTimer > 0f ? 0.55f : 1f;
            mana = Mathf.Min(maxMana, mana + manaRegenPerSecond * manaRegenMultiplier * deltaTime);
            manaSuppressionTimer = Mathf.Max(0f, manaSuppressionTimer - deltaTime);
            hitSfxCooldown = Mathf.Max(0f, hitSfxCooldown - deltaTime);
            TickFloorAffix(deltaTime);

            TickEnemyBase(deltaTime);
            TickEnemyCoreSkills(deltaTime);

            for (var i = activeUnits.Count - 1; i >= 0; i--)
            {
                if (i < activeUnits.Count && activeUnits[i] != null)
                {
                    activeUnits[i].Tick(deltaTime);
                }
            }

            CheckOutcome();
        }

        private void TickFloorAffix(float deltaTime)
        {
            if (flow == null || !flow.HasActiveRun)
            {
                return;
            }

            var damage = flow.FloorLightningDamage();
            if (damage <= 0f)
            {
                return;
            }

            floorAffixTimer -= deltaTime;
            if (floorAffixTimer > 0f)
            {
                return;
            }

            floorAffixTimer = 4.8f;
            var target = activeUnits
                .Where(unit => unit != null && unit.IsAlive)
                .OrderBy(_ => Random.value)
                .FirstOrDefault();
            if (target == null)
            {
                return;
            }

            SpawnWarningCircle(target.transform.position, 1.15f);
            SpawnSpellImpact(target.transform.position);
            target.TakeDamage(damage);
            ui.ShowNotice("天雷劫池：落雷击中战场单位");
        }

        private void TickEnemyBase(float deltaTime)
        {
            if (encounter == null || encounter.enemySpawns == null || encounter.enemySpawns.Count == 0)
            {
                return;
            }

            enemySpawnTimer -= deltaTime;
            if (enemySpawnTimer > 0f)
            {
                return;
            }

            var spawnMultiplier = CoreSpawnIntervalMultiplier() * (flow != null && flow.HasActiveRun ? flow.EnemySpawnIntervalMultiplier() : 1f);
            enemySpawnTimer = Mathf.Max(0.2f, encounter.enemySpawnInterval * spawnMultiplier);
            var validSpawns = encounter.enemySpawns
                .Where(spawn => spawn != null && spawn.unit != null && spawn.count > 0)
                .ToList();
            if (validSpawns.Count == 0)
            {
                Debug.LogWarning($"神魔镇荒：遭遇 {encounter.id} 没有可用的敌方刷怪配置。");
                return;
            }

            var entry = validSpawns[Random.Range(0, validSpawns.Count)];
            for (var i = 0; i < entry.count; i++)
            {
                SpawnUnit(entry.unit, Faction.Enemy, RandomEnemySpawnPosition(0.25f + i * 0.22f), false);
            }
        }

        private void TickEnemyCoreSkills(float deltaTime)
        {
            var core = EnemyCoreUnit();
            if (core == null)
            {
                coreWarningTimer = 0f;
                return;
            }

            if (coreWarningTimer > 0f)
            {
                coreWarningTimer -= deltaTime;
                if (coreWarningTimer <= 0f)
                {
                    ResolveCoreAreaBlast();
                }
            }

            var enrage = IsCoreEnraged(core);
            TickEnemyCorePhaseTriggers(core);
            coreAreaSkillTimer -= deltaTime;
            if (coreAreaSkillTimer <= 0f && coreWarningTimer <= 0f)
            {
                PrepareCoreAreaBlast(core, enrage);
                coreAreaSkillTimer = enrage ? 4.1f : 6.4f;
            }

            coreBuffSkillTimer -= deltaTime;
            if (coreBuffSkillTimer <= 0f)
            {
                BuffEnemyWave(enrage);
                coreBuffSkillTimer = enrage ? 5.2f : 8.0f;
            }
        }

        private void TickEnemyCorePhaseTriggers(BattleUnit core)
        {
            if (core == null || encounter == null || encounter.nodeType != MapNodeType.FinalBoss)
            {
                return;
            }

            var hpRatio = core.CurrentHp / Mathf.Max(1f, EnemyObjectiveMaxHp);
            if (!corePhaseTwoTriggered && hpRatio <= 0.70f)
            {
                corePhaseTwoTriggered = true;
                coreAreaSkillTimer = Mathf.Min(coreAreaSkillTimer, 1.25f);
                coreBuffSkillTimer = Mathf.Min(coreBuffSkillTimer, 1.8f);
                SpawnEmergencyEnemyWave(1.15f);
                SpawnMoraleEffect(core.transform.position);
                ui.ShowNotice("混沌魔君二阶段：妖潮加速，首领技能提前", 2.0f);
            }

            if (!corePhaseThreeTriggered && hpRatio <= 0.40f)
            {
                corePhaseThreeTriggered = true;
                mana = Mathf.Max(0f, mana - 2f);
                manaSuppressionTimer = 10f;
                coreAreaSkillTimer = Mathf.Min(coreAreaSkillTimer, 0.8f);
                SpawnEmergencyEnemyWave(0.75f);
                DamagePlayerStructures(16f);
                SpawnMoraleEffect(core.transform.position);
                ui.ShowNotice("混沌魔君三阶段：费用回流减弱，阵地遭到冲击", 2.2f);
            }
        }

        private void SpawnEmergencyEnemyWave(float yOffset)
        {
            if (encounter == null || encounter.enemySpawns == null || encounter.enemySpawns.Count == 0)
            {
                return;
            }

            var wave = encounter.enemySpawns
                .Where(entry => entry != null && entry.unit != null && entry.count > 0)
                .OrderByDescending(entry => entry.unit != null ? entry.unit.maxHp + entry.unit.attack * 3f : 0f)
                .Take(2)
                .ToList();
            foreach (var entry in wave)
            {
                var count = Mathf.Max(1, entry.count);
                for (var i = 0; i < count; i++)
                {
                    SpawnUnit(entry.unit, Faction.Enemy, RandomEnemySpawnPosition(yOffset + i * 0.20f), false);
                }
            }
        }

        private void DamagePlayerStructures(float damage)
        {
            var structures = activeUnits
                .Where(unit => unit != null && unit.IsAlive && unit.Faction == Faction.Player && unit.Definition != null && unit.Definition.role == UnitRole.Structure)
                .OrderByDescending(unit => unit.transform.position.y)
                .Take(4)
                .ToList();
            foreach (var structure in structures)
            {
                SpawnWarningCircle(structure.transform.position, 1.05f);
                structure.TakeDamage(damage);
            }
        }

        private void PrepareCoreAreaBlast(BattleUnit core, bool enrage)
        {
            var target = activeUnits
                .Where(unit => unit != null && unit.IsAlive && unit.Faction == Faction.Player && unit.Definition != null && unit.Definition.role != UnitRole.Structure)
                .OrderByDescending(unit => unit.transform.position.y)
                .FirstOrDefault();
            if (target == null)
            {
                return;
            }

            pendingCoreBlastPosition = target.transform.position;
            pendingCoreBlastRadius = enrage ? 2.15f : 1.65f;
            pendingCoreBlastDamage = Mathf.Max(8f, core.EffectiveAttack() * (enrage ? 1.15f : 0.85f));
            coreWarningTimer = enrage ? 0.48f : 0.72f;
            SpawnWarningCircle(pendingCoreBlastPosition, pendingCoreBlastRadius);
            ui.ShowNotice(enrage ? "敌方核心狂暴：范围技能即将落下" : "敌方核心正在蓄力");
        }

        private void ResolveCoreAreaBlast()
        {
            SpawnSpellImpact(pendingCoreBlastPosition);
            var targets = activeUnits
                .Where(unit => unit != null && unit.IsAlive && unit.Faction == Faction.Player && unit.Definition != null && unit.Definition.role != UnitRole.Structure)
                .Where(unit => Vector2.Distance(unit.transform.position, pendingCoreBlastPosition) <= pendingCoreBlastRadius)
                .ToList();

            foreach (var target in targets)
            {
                target.TakeDamage(pendingCoreBlastDamage);
            }
        }

        private void BuffEnemyWave(bool enrage)
        {
            var enemies = activeUnits
                .Where(unit => unit != null && unit.IsAlive && unit.Faction == Faction.Enemy && unit.Definition != null && unit.Definition.role != UnitRole.Boss)
                .OrderByDescending(unit => unit.transform.position.y)
                .Take(enrage ? 6 : 4)
                .ToList();
            if (enemies.Count == 0)
            {
                return;
            }

            foreach (var enemy in enemies)
            {
                enemy.AddModifier(EffectType.BuffAttack, enrage ? 0.28f : 0.16f, enrage ? 4.5f : 3.5f);
                SpawnMoraleEffect(enemy.transform.position);
            }

            ui.ShowNotice(enrage ? "敌方核心狂暴：妖兵攻击提升" : "敌方核心号令妖兵");
        }

        private bool IsCoreEnraged(BattleUnit core)
        {
            return core != null && core.Definition != null && core.Definition.maxHp > 0f && core.CurrentHp / core.Definition.maxHp <= 0.5f;
        }

        private float CoreSpawnIntervalMultiplier()
        {
            var core = EnemyCoreUnit();
            return core != null && IsCoreEnraged(core) ? 0.62f : 1f;
        }

        private int CalculateCommandCost(CardDefinition card, bool strengthened)
        {
            return card.CommandCost();
        }

        private void ResolveCard(CardDefinition card, bool strengthened, Vector3 targetPosition)
        {
            if (card.type == CardType.Curse)
            {
                ResolveCurseCard(card, targetPosition);
                return;
            }

            if (card.id == "card_cloud_banner")
            {
                temporaryStructureCostReduction = Mathf.Max(temporaryStructureCostReduction, CloudBannerStructureCostReduction);
                ui.ShowNotice($"流云军令：下一张建筑牌费用 -{CloudBannerStructureCostReduction}", 1.8f);
            }

            var spawnedSoldiers = 0;
            var structureIndex = 0;
            var totalStructures = StructureSpawnCount(card);
            var moraleUnits = new List<BattleUnit>();
            foreach (var spawn in card.unitSpawns)
            {
                if (spawn?.unit == null)
                {
                    continue;
                }

                var count = spawn.count;
                if (strengthened && card.type == CardType.Soldier && spawnedSoldiers == 0)
                {
                    count += 1;
                }

                var spawnCenter = ResolveSpawnCenter(card, targetPosition);
                var columns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(count)));
                for (var i = 0; i < count; i++)
                {
                    var column = i % columns;
                    var row = i / columns;
                    var position = spawn.unit.role == UnitRole.Structure
                        ? StructureSpawnPosition(spawnCenter, structureIndex++, totalStructures)
                        : new Vector3(
                            spawnCenter.x + (column - (columns - 1) * 0.5f) * spawn.spacing + Random.Range(-spawn.yJitter, spawn.yJitter),
                            spawnCenter.y - row * spawn.spacing * 0.65f,
                            0f);
                    var unit = SpawnUnit(spawn.unit, Faction.Player, position, true);
                    if (unit == null)
                    {
                        continue;
                    }

                    if (unit.Definition != null && (unit.Definition.role == UnitRole.Soldier || unit.Definition.role == UnitRole.Elite))
                    {
                        spawnedSoldiers++;
                    }

                    if (strengthened && unit.Definition != null && unit.Definition.role != UnitRole.Structure)
                    {
                        moraleUnits.Add(unit);
                    }
                }
            }

            ApplyMoraleUnitBonus(card, moraleUnits);
            var structureMorale = flow != null && flow.HasActiveRun ? flow.MoraleFromStructureCard(card) : 0;
            if (structureMorale > 0)
            {
                morale.AddCharges(structureMorale);
                SpawnMoraleEffect(targetPosition);
                ui.ShowNotice($"点将台激发：士气 +{structureMorale}");
            }

            if (spawnedSoldiers > 0)
            {
                morale.RegisterSummonedSoldiers(spawnedSoldiers);
            }

            foreach (var effect in card.effects)
            {
                ResolveEffect(card, effect, strengthened, targetPosition, card.releaseRule == CardReleaseRule.Anywhere);
            }

            if (card.type == CardType.Hero)
            {
                ExhaustHeroCardsForBattle(card);
            }
        }

        private void ResolveCurseCard(CardDefinition card, Vector3 targetPosition)
        {
            if (flow != null && flow.HasActiveRun && flow.ConvertCurseToMorale())
            {
                morale.AddCharges(1);
                SpawnMoraleEffect(targetPosition);
                ui.ShowNotice($"镇煞葫芦化解诅咒：士气 +1");
                return;
            }

            switch (card.id)
            {
                case "card_curse_karmic_fire":
                    DamagePlayerBase(8f);
                    SpawnSpellImpact(PlayerBaseViewPosition());
                    ui.ShowNotice("业火缠身：我方基地受损");
                    break;
                case "card_curse_demon_fog":
                    mana = Mathf.Max(0f, mana - 2f);
                    SpawnWarningCircle(targetPosition, 1.1f);
                    ui.ShowNotice("妖雾侵心：费用 -2");
                    break;
                default:
                    var enemy = catalog.FindUnit("enemy_grunt");
                    if (enemy != null)
                    {
                        SpawnUnit(enemy, Faction.Enemy, RandomEnemySpawnPosition(0.45f), false);
                    }

                    ui.ShowNotice("因果债契：额外妖兵来袭");
                    break;
            }
        }

        private void ApplyMoraleUnitBonus(CardDefinition card, IReadOnlyList<BattleUnit> units)
        {
            if (card == null || units.Count == 0)
            {
                return;
            }

            switch (card.type)
            {
                case CardType.EliteSoldier:
                    foreach (var unit in units)
                    {
                        unit.AddShield(Mathf.Max(12f, unit.Definition.maxHp * 0.35f));
                        SpawnMoraleEffect(unit.transform.position);
                    }
                    break;
                case CardType.Hero:
                    foreach (var unit in units)
                    {
                        unit.AddModifier(EffectType.BuffAttack, 0.65f, 6f);
                        SpawnMoraleEffect(unit.transform.position);
                    }
                    break;
            }
        }

        private static string MoraleNotice(CardDefinition card)
        {
            return card.type switch
            {
                CardType.Soldier => $"士气强化：{card.displayName} 额外召唤 1 个单位",
                CardType.EliteSoldier => $"士气强化：{card.displayName} 登场获得护盾",
                CardType.Hero => $"士气强化：{card.displayName} 登场短时增伤",
                CardType.Tactic => $"士气强化：{card.displayName} 效果提高",
                _ => $"士气强化：{card.displayName}"
            };
        }

        private void ExhaustHeroCardsForBattle(CardDefinition sourceCard)
        {
            if (sourceCard == null || sourceCard.type != CardType.Hero)
            {
                return;
            }

            foreach (var spawn in sourceCard.unitSpawns)
            {
                if (spawn?.unit == null || spawn.unit.role != UnitRole.Hero || string.IsNullOrWhiteSpace(spawn.unit.id))
                {
                    continue;
                }

                ExhaustHeroCardsForBattle(spawn.unit.id);
            }
        }

        private void ExhaustHeroCardsForBattle(string heroUnitId)
        {
            if (deck == null || string.IsNullOrWhiteSpace(heroUnitId))
            {
                return;
            }

            deck.ExhaustCards(card => HeroCardMatchesUnit(card, heroUnitId));
        }

        private static bool HeroCardMatchesUnit(CardDefinition card, string heroUnitId)
        {
            if (card == null || card.type != CardType.Hero || string.IsNullOrWhiteSpace(heroUnitId))
            {
                return false;
            }

            foreach (var spawn in card.unitSpawns)
            {
                if (spawn?.unit == null || spawn.unit.role != UnitRole.Hero)
                {
                    continue;
                }

                if (spawn.unit.id == heroUnitId)
                {
                    return true;
                }
            }

            return false;
        }

        private void CleanupUnavailableHeroCards()
        {
            if (deck == null)
            {
                return;
            }

            foreach (var heroUnitId in defeatedHeroUnitIds)
            {
                ExhaustHeroCardsForBattle(heroUnitId);
            }

            foreach (var activeHeroUnitId in activeUnits
                         .Where(unit => unit != null && unit.IsAlive && unit.Faction == Faction.Player && unit.Definition != null && unit.Definition.role == UnitRole.Hero)
                         .Select(unit => unit.Definition.id)
                         .Distinct())
            {
                ExhaustHeroCardsForBattle(activeHeroUnitId);
            }
        }

        private IReadOnlyList<string> BuildEnemySkillHints()
        {
            if (!IsBossLikeEncounter)
            {
                return new[] { "妖兵潮汐  持续", "据点守备  持续" };
            }

            if (encounter != null && encounter.nodeType == MapNodeType.FinalBoss)
            {
                return new[] { "混沌降临  12秒", "灭世雷劫  18秒", "魔君怒吼  23秒", "深渊漩涡  29秒" };
            }

            if (encounter != null && encounter.nodeType == MapNodeType.SmallBoss)
            {
                return new[] { "首领蓄力  10秒", "妖兵号令  16秒", "狂暴半血  被动" };
            }

            return new[] { "精英威压  9秒", "妖兵号令  15秒", "半血狂暴  被动" };
        }

        private Vector3 ResolveSpawnCenter(CardDefinition card, Vector3 targetPosition)
        {
            var x = Mathf.Clamp(targetPosition.x, placementMinX, placementMaxX);
            var y = card.releaseRule == CardReleaseRule.PlayerSide
                ? Mathf.Clamp(targetPosition.y, playerBaseY + 0.55f, BattleMidY - 0.2f)
                : targetPosition.y;
            return new Vector3(x, y, 0f);
        }

        private bool CanPlaceStructures(CardDefinition card, Vector3 targetPosition, out string reason)
        {
            reason = string.Empty;
            var planned = PlannedStructurePositions(card, targetPosition);
            for (var i = 0; i < planned.Count; i++)
            {
                var position = planned[i];
                if (position.x < placementMinX + StructurePlacementRadius || position.x > placementMaxX - StructurePlacementRadius)
                {
                    reason = "建筑超出战场范围";
                    return false;
                }

                if (position.y > BattleMidY - 0.2f)
                {
                    reason = "建筑不能越过中线";
                    return false;
                }

                if (position.y < playerBaseY + 0.35f)
                {
                    reason = "建筑不能放在手牌区或基地后方";
                    return false;
                }

                for (var j = i + 1; j < planned.Count; j++)
                {
                    if (Vector2.Distance(position, planned[j]) < StructurePlacementMinDistance)
                    {
                        reason = "建筑之间需要留出空位";
                        return false;
                    }
                }

                foreach (var unit in activeUnits)
                {
                    if (unit == null || !unit.IsAlive || unit.Faction != Faction.Player || unit.Definition == null || unit.Definition.role != UnitRole.Structure)
                    {
                        continue;
                    }

                    if (Vector2.Distance(position, unit.transform.position) < StructurePlacementMinDistance)
                    {
                        reason = "这里已有建筑";
                        return false;
                    }
                }
            }

            return true;
        }

        private List<Vector3> PlannedStructurePositions(CardDefinition card, Vector3 targetPosition)
        {
            var positions = new List<Vector3>();
            if (!CardPlacesStructure(card))
            {
                return positions;
            }

            var center = ResolveSpawnCenter(card, targetPosition);
            var total = StructureSpawnCount(card);
            var index = 0;
            foreach (var spawn in card.unitSpawns)
            {
                if (spawn?.unit == null || spawn.unit.role != UnitRole.Structure)
                {
                    continue;
                }

                for (var i = 0; i < spawn.count; i++)
                {
                    positions.Add(StructureSpawnPosition(center, index++, total));
                }
            }

            return positions;
        }

        private static int StructureSpawnCount(CardDefinition card)
        {
            if (card == null)
            {
                return 0;
            }

            var total = 0;
            foreach (var spawn in card.unitSpawns)
            {
                if (spawn?.unit != null && spawn.unit.role == UnitRole.Structure)
                {
                    total += Mathf.Max(0, spawn.count);
                }
            }

            return total;
        }

        private static Vector3 StructureSpawnPosition(Vector3 center, int index, int total)
        {
            if (total <= 1)
            {
                return center;
            }

            var columns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(total)));
            var column = index % columns;
            var row = index / columns;
            var x = center.x + (column - (columns - 1) * 0.5f) * StructurePlacementSpacing;
            var y = center.y - row * StructurePlacementSpacing * 0.78f;
            return new Vector3(x, y, 0f);
        }

        private Vector3 RandomEnemySpawnPosition(float yOffset)
        {
            var x = Random.Range(placementMinX + 0.4f, placementMaxX - 0.4f);
            return new Vector3(x, enemyBaseY - yOffset, 0f);
        }

        private ContentCatalog ResolveContentCatalog()
        {
            if (defaultCatalog != null)
            {
                return defaultCatalog;
            }

#if UNITY_EDITOR
            var assetCatalog = UnityEditor.AssetDatabase.LoadAssetAtPath<ContentCatalog>("Assets/_Project/Content/DemoContentCatalog.asset");
            if (assetCatalog != null)
            {
                return assetCatalog;
            }
#endif

            return DemoContentFactory.CreateCatalog();
        }

        private void EnsurePresentationSprites()
        {
            playerProjectileSprite ??= LoadResourceSprite(ProjectileResourcePath);
            enemyProjectileSprite ??= playerProjectileSprite;
            hitEffectSprite ??= LoadResourceSprite(HitEffectResourcePath);
            spellImpactSprite ??= LoadResourceSprite(SpellImpactResourcePath) ?? hitEffectSprite;
            commanderDivineEffectSprite ??= LoadResourceSprite(CommanderDivineEffectResourcePath) ?? spellImpactSprite;
            summonerDivineEffectSprite ??= LoadResourceSprite(SummonerDivineEffectResourcePath) ?? spellImpactSprite;
            thunderDivineEffectSprite ??= LoadResourceSprite(ThunderDivineEffectResourcePath) ?? spellImpactSprite;
        }

        private static Sprite LoadResourceSprite(string resourcePath)
        {
            var sprite = Resources.Load<Sprite>(resourcePath);
            if (sprite != null)
            {
                return sprite;
            }

            var texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null)
            {
                return null;
            }

            sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                256f);
            sprite.name = texture.name;
            return sprite;
        }

        private void EnsureBaseViews()
        {
            if (playerBaseView == null)
            {
                playerBaseView = CreateBaseView("Player Base View");
            }

            if (enemyBaseView == null)
            {
                enemyBaseView = CreateBaseView("Enemy Base View");
            }

            playerBaseView.Initialize(Faction.Player, null, PlayerBaseViewPosition(), 0.85f, CurrentPlayerBattleMaxHp(), false, false);
            enemyBaseView.gameObject.SetActive(HasEnemyBase);
            if (HasEnemyBase)
            {
                enemyBaseView.Initialize(Faction.Enemy, null, EnemyBaseViewPosition(), 0.92f, EnemyBaseHp, false, true);
            }
        }

        private void RefreshBaseViews()
        {
            playerBaseView?.UpdateHealth(PlayerBaseHp, CurrentPlayerBattleMaxHp());
            if (HasEnemyBase)
            {
                enemyBaseView?.UpdateHealth(EnemyBaseHp, encounter != null ? encounter.enemyBaseMaxHp : 120f);
            }
        }

        private BattleBaseView CreateBaseView(string objectName)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(transform);
            return go.AddComponent<BattleBaseView>();
        }

        private Vector3 PlayerBaseViewPosition()
        {
            return new Vector3(laneX, playerBaseY - 1.92f, 0f);
        }

        private Vector3 EnemyBaseViewPosition()
        {
            return new Vector3(laneX, enemyBaseY + 0.18f, 0f);
        }

        private Vector3 EnemyCorePosition()
        {
            return new Vector3(laneX, enemyBaseY + 0.12f, 0f);
        }

        private BattleUnit SpawnUnit(UnitDefinition unitDefinition, Faction faction, Vector3 position, bool countCommand)
        {
            if (unitDefinition == null)
            {
                Debug.LogWarning($"神魔镇荒：尝试生成空单位，阵营={faction}，位置={position}");
                return null;
            }

            unitPool ??= new ComponentPool<BattleUnit>(CreateUnitInstance);
            var unit = unitPool.Get();
            if (unit == null)
            {
                Debug.LogWarning($"神魔镇荒：单位对象池没有返回可用实例，单位={unitDefinition.id}");
                return null;
            }

            try
            {
                unit.Initialize(this, unitDefinition, faction, position);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"神魔镇荒：生成单位失败，已跳过。单位={unitDefinition.id}，阵营={faction}，位置={position}，错误={exception.Message}");
                Destroy(unit.gameObject);
                return null;
            }

            activeUnits.Add(unit);
            if (countCommand && faction == Faction.Player)
            {
                PlayOneShot(ref summonClip, 720f, 0.045f);
            }

            return unit;
        }

        private void ResolveEffect(CardDefinition sourceCard, BattleEffectDefinition effect, bool strengthened, Vector3 targetPosition, bool usePlacementTarget)
        {
            if (effect == null)
            {
                return;
            }

            var value = strengthened ? effect.value * 1.5f : effect.value;
            var duration = strengthened ? effect.duration * 1.25f : effect.duration;
            var radius = EffectiveEffectRadiusForCard(sourceCard, effect, usePlacementTarget);
            var usesPlacementTargets = usePlacementTarget && effect.effectType is EffectType.Damage or EffectType.AreaDamage or EffectType.Slow or EffectType.Stun or EffectType.Burn or EffectType.Poison or EffectType.Knockback;
            var isPlacedDamage = usesPlacementTargets && (effect.effectType == EffectType.Damage || effect.effectType == EffectType.AreaDamage);
            var targets = usesPlacementTargets
                ? SelectTargetsNearPosition(effect.targetRule, targetPosition, radius, effect.effectType)
                : SelectTargets(effect.targetRule, radius);

            switch (effect.effectType)
            {
                case EffectType.Damage:
                case EffectType.AreaDamage:
                    value *= flow != null && flow.HasActiveRun ? flow.SpellDamageMultiplier() : 1f;
                    if (isPlacedDamage)
                    {
                        SpawnSpellImpact(targetPosition);
                    }

                    foreach (var target in targets)
                    {
                        target.TakeDamage(value);
                    }

                    if (isPlacedDamage && targets.Count == 0 && IsEnemyBasePoint(targetPosition, radius))
                    {
                        DamageEnemyBase(value * 0.5f);
                    }
                    break;
                case EffectType.Heal:
                    foreach (var target in targets)
                    {
                        target.Heal(value);
                    }
                    break;
                case EffectType.Shield:
                    foreach (var target in targets)
                    {
                        target.AddShield(value);
                    }
                    break;
                case EffectType.BuffAttack:
                case EffectType.BuffAttackSpeed:
                case EffectType.Slow:
                case EffectType.Stun:
                case EffectType.Burn:
                case EffectType.Poison:
                    foreach (var target in targets)
                    {
                        target.AddModifier(effect.effectType, value, duration);
                        if (effect.effectType is EffectType.Slow or EffectType.Stun or EffectType.Burn or EffectType.Poison)
                        {
                            SpawnWarningCircle(target.transform.position, effect.effectType == EffectType.Stun ? 0.95f : 0.75f);
                        }
                    }
                    break;
                case EffectType.Knockback:
                    foreach (var target in targets)
                    {
                        target.KnockbackFrom(usePlacementTarget ? targetPosition : new Vector3(laneX, BattleMidY, 0f), value);
                        SpawnWarningCircle(target.transform.position, 0.85f);
                    }
                    break;
                case EffectType.DrawCard:
                    deck.Draw(Mathf.RoundToInt(value));
                    break;
                case EffectType.GainMana:
                    mana = Mathf.Min(maxMana, mana + value);
                    break;
                case EffectType.GainMorale:
                    var gainedMorale = Mathf.Max(1, Mathf.RoundToInt(value));
                    morale.AddCharges(gainedMorale);
                    ui.ShowNotice($"战鼓激发：士气 +{gainedMorale}，下一张出兵牌会强化");
                    break;
                case EffectType.GainGold:
                    if (flow != null && flow.HasActiveRun)
                    {
                        var gainedGold = Mathf.Max(1, Mathf.RoundToInt(value));
                        flow.GainGoldDuringBattle(gainedGold);
                        ui.ShowNotice($"聚宝生效：金币 +{gainedGold}");
                    }
                    else
                    {
                        ui.ShowNotice("聚宝效果需要从迷宫探索进入战斗");
                    }
                    break;
            }
        }

        private bool IsEnemyBasePoint(Vector3 targetPosition, float radius)
        {
            if (!HasEnemyBase)
            {
                return false;
            }

            var effectiveRadius = Mathf.Max(0.75f, radius);
            return Mathf.Abs(targetPosition.y - enemyBaseY) <= effectiveRadius &&
                targetPosition.x >= placementMinX - effectiveRadius &&
                targetPosition.x <= placementMaxX + effectiveRadius;
        }

        private List<BattleUnit> SelectTargetsNearPosition(TargetRule targetRule, Vector3 targetPosition, float radius, EffectType effectType)
        {
            var effectiveRadius = Mathf.Max(0.75f, radius);
            var candidates = activeUnits.Where(unit => unit != null && unit.IsAlive);

            candidates = targetRule switch
            {
                TargetRule.FriendlyFrontline or TargetRule.AllFriendlyUnits => candidates.Where(unit => unit.Faction == Faction.Player),
                _ => candidates.Where(unit => unit.Faction == Faction.Enemy)
            };

            var targets = candidates
                .Where(unit => Vector2.Distance(unit.transform.position, targetPosition) <= effectiveRadius)
                .OrderBy(unit => Vector2.Distance(unit.transform.position, targetPosition))
                .ToList();

            if (effectType == EffectType.Damage && targets.Count > 1)
            {
                return targets.Take(1).ToList();
            }

            return targets;
        }

        private float EffectiveEffectRadiusForCard(CardDefinition sourceCard, BattleEffectDefinition effect, bool usePlacementTarget)
        {
            if (effect == null)
            {
                return 0f;
            }

            var radius = effect.radius;
            if (!usePlacementTarget || sourceCard == null || CurrentHeroClassForBattle() != HeroClassType.ThunderMage)
            {
                return radius;
            }

            if (sourceCard.type is not (CardType.Spell or CardType.Debuff))
            {
                return radius;
            }

            return effect.effectType is EffectType.Damage or EffectType.AreaDamage or EffectType.Slow or EffectType.Stun or EffectType.Burn or EffectType.Poison or EffectType.Knockback
                ? radius * ThunderMageSpellRadiusMultiplier
                : radius;
        }

        private HeroClassType CurrentHeroClassForBattle()
        {
            return flow != null && flow.HasActiveRun ? flow.CurrentHeroClass : HeroClassType.BorderCommander;
        }

        private List<BattleUnit> SelectTargets(TargetRule targetRule, float radius)
        {
            var units = activeUnits.Where(unit => unit != null && unit.IsAlive);
            return targetRule switch
            {
                TargetRule.EnemyFrontline => units
                    .Where(unit => unit.Faction == Faction.Enemy)
                    .OrderBy(unit => unit.transform.position.y)
                    .Take(3)
                    .ToList(),
                TargetRule.EnemyBackline => units
                    .Where(unit => unit.Faction == Faction.Enemy)
                    .OrderByDescending(unit => unit.transform.position.y)
                    .Take(3)
                    .ToList(),
                TargetRule.AllEnemies => units.Where(unit => unit.Faction == Faction.Enemy).ToList(),
                TargetRule.FriendlyFrontline => units
                    .Where(unit => unit.Faction == Faction.Player)
                    .OrderByDescending(unit => unit.transform.position.y)
                    .Take(3)
                    .ToList(),
                TargetRule.AllFriendlyUnits => units.Where(unit => unit.Faction == Faction.Player).ToList(),
                _ => new List<BattleUnit>()
            };
        }

        private void CheckOutcome()
        {
            if (Outcome != BattleOutcome.Running)
            {
                return;
            }

            if (encounter != null && encounter.coreEnemy != null && !HasLivingEnemyCore())
            {
                Outcome = BattleOutcome.Victory;
                StopBattleMusic();
                ui.ShowResult("胜利");
                PlayOneShot(ref victoryClip, 880f, 0.11f);
            }
            else if ((encounter == null || encounter.coreEnemy == null) && EnemyBaseHp <= 0f)
            {
                Outcome = BattleOutcome.Victory;
                StopBattleMusic();
                ui.ShowResult("胜利");
                PlayOneShot(ref victoryClip, 880f, 0.11f);
            }
            else if (PlayerBaseHp <= 0f)
            {
                Outcome = BattleOutcome.Defeat;
                StopBattleMusic();
                ui.ShowResult("失败");
                PlayOneShot(ref defeatClip, 150f, 0.13f);
            }
        }

        public void ContinueAfterResult()
        {
            if (flow != null && flow.HasActiveRun)
            {
                flow.CompleteBattle(Outcome, PlayerBaseHp);
                return;
            }

            StartPrototypeBattle();
        }

        public void DebugWinNow()
        {
            if (Outcome != BattleOutcome.Running)
            {
                return;
            }

            Outcome = BattleOutcome.Victory;
            StopBattleMusic();
            ui.ShowResult("胜利");
            PlayOneShot(ref victoryClip, 880f, 0.11f);
        }

        public void DebugLoseNow()
        {
            if (Outcome != BattleOutcome.Running)
            {
                return;
            }

            PlayerBaseHp = 0f;
            Outcome = BattleOutcome.Defeat;
            StopBattleMusic();
            playerBaseView?.UpdateHealth(PlayerBaseHp, CurrentPlayerBattleMaxHp());
            ui.ShowResult("失败");
            PlayOneShot(ref defeatClip, 150f, 0.13f);
        }

        public void DebugAddMorale()
        {
            morale.AddCharges(1);
            ui.ShowNotice("调试：士气 +1，下一张出兵牌会强化");
            ui.Refresh();
        }

        public void DebugSpawnTalismanGuard()
        {
            if (Outcome != BattleOutcome.Running)
            {
                return;
            }

            var guard = catalog.FindUnit("unit_shield_guard");
            if (guard == null)
            {
                ui.ShowNotice("调试：没有找到金甲天将配置");
                return;
            }

            for (var i = 0; i < 2; i++)
            {
                var x = laneX + (i == 0 ? -0.42f : 0.42f);
                var y = playerBaseY + 1.28f + i * 0.14f;
                SpawnUnit(guard, Faction.Player, new Vector3(x, y, 0f), true);
            }

            morale.RegisterSummonedSoldiers(2);
            SpawnMoraleEffect(new Vector3(laneX, playerBaseY + 1.28f, 0f));
            ui.ShowNotice("调试：已召来金甲天将");
        }

        public void DebugAddGold()
        {
            if (flow != null && flow.HasActiveRun)
            {
                flow.DebugAddGold(100);
                ui.ShowNotice("调试：金币 +100");
                return;
            }

            ui.ShowNotice("调试加金币需要从迷宫探索进入战斗");
        }

        public void DebugOpenCardReward()
        {
            if (flow != null && flow.HasActiveRun)
            {
                flow.DebugOpenCardReward();
                return;
            }

            ui.ShowNotice("调试卡牌奖励需要从迷宫探索进入战斗");
        }

        public void DebugSkipNode()
        {
            if (flow != null && flow.HasActiveRun)
            {
                flow.DebugSkipPendingNode();
                return;
            }

            DebugWinNow();
        }

        public float AttackMultiplierFor(UnitDefinition unit)
        {
            return flow != null && flow.HasActiveRun ? flow.UnitAttackMultiplier(unit) : 1f;
        }

        public float MoveSpeedMultiplierFor(UnitDefinition unit)
        {
            return unit != null && unit.faction == Faction.Player && flow != null && flow.HasActiveRun
                ? flow.UnitMoveSpeedMultiplier()
                : 1f;
        }

        public float RangeMultiplierFor(UnitDefinition unit, Faction faction)
        {
            return flow != null && flow.HasActiveRun ? flow.UnitRangeMultiplier(unit, faction) : 1f;
        }

        public float MaxHpMultiplierFor(UnitDefinition unit, Faction faction)
        {
            return flow != null && flow.HasActiveRun ? flow.UnitMaxHpMultiplier(unit, faction) : 1f;
        }

        public float ProductionIntervalMultiplierFor(UnitDefinition unit, Faction faction)
        {
            return unit != null && faction == Faction.Player && unit.role == UnitRole.Structure && flow != null && flow.HasActiveRun
                ? flow.StructureProductionIntervalMultiplier()
                : 1f;
        }

        private void ApplyRunBattleModifiers()
        {
            maxMana = baseMaxMana;
            maxCommand = baseMaxCommand;
            manaRegenPerSecond = baseManaRegenPerSecond;
            morale.SoldiersPerCharge = 5;

            if (flow == null || !flow.HasActiveRun)
            {
                return;
            }

            maxMana += flow.ExtraMaxMana();
            maxCommand += flow.PlayerExtraCommand();
            manaRegenPerSecond *= flow.ManaRegenMultiplier();
            morale.SoldiersPerCharge = flow.MoraleThreshold();
        }

        private float CurrentPlayerBattleMaxHp()
        {
            var encounterMax = encounter != null ? encounter.playerBaseMaxHp : 100f;
            if (flow == null || !flow.HasActiveRun)
            {
                return encounterMax;
            }

            return encounterMax + Mathf.Max(0f, flow.PlayerMaxHpForRun() - 100f);
        }

        private bool HasLivingEnemyCore()
        {
            return activeUnits.Any(unit =>
                unit != null &&
                unit.IsAlive &&
                unit.Faction == Faction.Enemy &&
                unit.Definition == encounter.coreEnemy);
        }

        private BattleUnit EnemyCoreUnit()
        {
            return encounter != null && encounter.coreEnemy != null
                ? activeUnits.FirstOrDefault(unit =>
                    unit != null &&
                    unit.IsAlive &&
                    unit.Faction == Faction.Enemy &&
                    unit.Definition == encounter.coreEnemy)
                : null;
        }

        private void ConfigureAudio()
        {
            EnsureAudioListener();

            audioSource = gameObject.GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;
            audioSource.ignoreListenerPause = true;

            var musicObject = new GameObject("Battle Music");
            musicObject.transform.SetParent(transform, false);
            musicSource = musicObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.spatialBlend = 0f;
            musicSource.volume = battleMusicVolume;
            musicSource.ignoreListenerPause = true;

            battleMusicClip ??= LoadBattleMusicClip();
            if (hitSfxClips == null || hitSfxClips.Length == 0)
            {
                hitSfxClips = LoadHitSfxClips();
            }
        }

        private void EnsureAudioListener()
        {
            var existingListener = FindAnyObjectByType<AudioListener>();
            if (existingListener != null && existingListener.enabled && existingListener.gameObject.activeInHierarchy)
            {
                return;
            }

            var target = Camera.main != null ? Camera.main.gameObject : null;
            if (target == null)
            {
                var camera = FindAnyObjectByType<Camera>();
                target = camera != null ? camera.gameObject : gameObject;
            }

            var listener = target.GetComponent<AudioListener>();
            if (listener == null)
            {
                listener = target.AddComponent<AudioListener>();
            }

            listener.enabled = true;
        }

        private void StartBattleMusic()
        {
            if (musicSource == null)
            {
                return;
            }

            StopBattleMusic();

            battleMusicClip ??= LoadBattleMusicClip();
            if (TryPlayBattleMusicClip(battleMusicClip, false))
            {
                return;
            }

            if (battleMusicLoadRoutine != null)
            {
                StopCoroutine(battleMusicLoadRoutine);
            }

            battleMusicLoadRoutine = StartCoroutine(LoadBattleMusicFromStreamingAssets());
        }

        private void StopBattleMusic()
        {
            if (battleMusicLoadRoutine != null)
            {
                StopCoroutine(battleMusicLoadRoutine);
                battleMusicLoadRoutine = null;
            }

            if (musicSource != null && musicSource.isPlaying)
            {
                musicSource.Stop();
            }
        }

        private bool TryPlayBattleMusicClip(AudioClip clip, bool usingFallbackMusic)
        {
            if (clip == null || musicSource == null)
            {
                return false;
            }

            if (!EnsureAudioClipData(clip, "战斗 BGM"))
            {
                return false;
            }

            musicSource.clip = clip;
            musicSource.volume = usingFallbackMusic ? Mathf.Max(battleMusicVolume, 0.38f) : battleMusicVolume;
            musicSource.Play();
            return true;
        }

        private IEnumerator LoadBattleMusicFromStreamingAssets()
        {
            var musicUrl = BattleMusicStreamingUrl();
            if (!string.IsNullOrWhiteSpace(musicUrl))
            {
                var request = CreateStreamingAudioRequest(musicUrl);
                if (request != null)
                {
                    var asyncOp = SendStreamingAudioRequest(request);
                    if (asyncOp != null)
                    {
                        yield return asyncOp;

                        if (StreamingAudioRequestSucceeded(request))
                        {
                            var clip = ExtractAudioClipFromStreamingRequest(request);
                            if (clip != null)
                            {
                                clip.name = "hyoshi_action_track_2_streaming";
                                battleMusicClip = clip;
                                if (TryPlayBattleMusicClip(battleMusicClip, false))
                                {
                                    Debug.Log("神魔镇荒使用 StreamingAssets 加载战斗 BGM。");
                                    DisposeStreamingAudioRequest(request);
                                    battleMusicLoadRoutine = null;
                                    yield break;
                                }
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"神魔镇荒未能从 StreamingAssets 加载战斗 BGM：{StreamingAudioRequestError(request)}");
                        }
                    }

                    DisposeStreamingAudioRequest(request);
                }
            }

            fallbackBattleMusicClip ??= CreateFallbackMusicClip();
            if (TryPlayBattleMusicClip(fallbackBattleMusicClip, true))
            {
                Debug.LogWarning("神魔镇荒外部 BGM 未加载成功，使用程序生成的临时战斗 BGM。");
            }

            battleMusicLoadRoutine = null;
        }

        private static string BattleMusicStreamingUrl()
        {
            var basePath = Application.streamingAssetsPath;
            if (string.IsNullOrWhiteSpace(basePath))
            {
                return string.Empty;
            }

            if (basePath.Contains("://", StringComparison.Ordinal))
            {
                return $"{basePath.TrimEnd('/')}/{BattleMusicStreamingRelativePath}";
            }

            var filePath = Path.Combine(basePath, BattleMusicStreamingRelativePath);
            return File.Exists(filePath) ? new Uri(filePath).AbsoluteUri : string.Empty;
        }

        private static object CreateStreamingAudioRequest(string musicUrl)
        {
            var multimediaType = FindUnityType(
                "UnityEngine.Networking.UnityWebRequestMultimedia",
                "UnityEngine.UnityWebRequestAudioModule",
                "UnityEngine.UnityWebRequestModule");
            if (multimediaType == null)
            {
                return null;
            }

            var method = multimediaType
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(candidate =>
                {
                    if (candidate.Name != "GetAudioClip")
                    {
                        return false;
                    }

                    var parameters = candidate.GetParameters();
                    return parameters.Length == 2
                           && parameters[0].ParameterType == typeof(string)
                           && parameters[1].ParameterType == typeof(AudioType);
                });
            return method?.Invoke(null, new object[] { musicUrl, AudioType.OGGVORBIS });
        }

        private static AsyncOperation SendStreamingAudioRequest(object request)
        {
            return request?
                .GetType()
                .GetMethod("SendWebRequest", BindingFlags.Instance | BindingFlags.Public)?
                .Invoke(request, null) as AsyncOperation;
        }

        private static bool StreamingAudioRequestSucceeded(object request)
        {
            var result = request?
                .GetType()
                .GetProperty("result", BindingFlags.Instance | BindingFlags.Public)?
                .GetValue(request);
            return string.Equals(result?.ToString(), "Success", StringComparison.Ordinal);
        }

        private static string StreamingAudioRequestError(object request)
        {
            return request?
                       .GetType()
                       .GetProperty("error", BindingFlags.Instance | BindingFlags.Public)?
                       .GetValue(request) as string
                   ?? "未知错误";
        }

        private static AudioClip ExtractAudioClipFromStreamingRequest(object request)
        {
            if (request == null)
            {
                return null;
            }

            var downloadHandlerAudioClipType = FindUnityType(
                "UnityEngine.Networking.DownloadHandlerAudioClip",
                "UnityEngine.UnityWebRequestAudioModule",
                "UnityEngine.UnityWebRequestModule");
            if (downloadHandlerAudioClipType == null)
            {
                return null;
            }

            var contentMethod = downloadHandlerAudioClipType
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(candidate =>
                {
                    if (candidate.Name != "GetContent")
                    {
                        return false;
                    }

                    var parameters = candidate.GetParameters();
                    return parameters.Length == 1 && parameters[0].ParameterType.IsInstanceOfType(request);
                });
            if (contentMethod != null)
            {
                return contentMethod.Invoke(null, new[] { request }) as AudioClip;
            }

            var downloadHandler = request
                .GetType()
                .GetProperty("downloadHandler", BindingFlags.Instance | BindingFlags.Public)?
                .GetValue(request);
            return downloadHandler?
                .GetType()
                .GetProperty("audioClip", BindingFlags.Instance | BindingFlags.Public)?
                .GetValue(downloadHandler) as AudioClip;
        }

        private static void DisposeStreamingAudioRequest(object request)
        {
            if (request is IDisposable disposable)
            {
                disposable.Dispose();
                return;
            }

            request?
                .GetType()
                .GetMethod("Dispose", BindingFlags.Instance | BindingFlags.Public)?
                .Invoke(request, null);
        }

        private static Type FindUnityType(string typeName, params string[] assemblyNames)
        {
            foreach (var assemblyName in assemblyNames)
            {
                var qualifiedName = $"{typeName}, {assemblyName}";
                var type = Type.GetType(qualifiedName, false);
                if (type != null)
                {
                    return type;
                }

                try
                {
                    var assembly = Assembly.Load(assemblyName);
                    type = assembly.GetType(typeName, false);
                    if (type != null)
                    {
                        return type;
                    }
                }
                catch
                {
                    // Ignore and continue to fallback options.
                }
            }

            return AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(assembly => assembly.GetType(typeName, false))
                .FirstOrDefault(type => type != null);
        }

        private static AudioClip LoadBattleMusicClip()
        {
            var clip = Resources.Load<AudioClip>(BattleMusicResourcePath);
            if (clip != null)
            {
                return clip;
            }

            var clips = Resources.LoadAll<AudioClip>("Audio/BGM");
            clip = clips.FirstOrDefault(item => item != null && item.name == "hyoshi_action_track_2")
                ?? clips.FirstOrDefault(item => item != null);
            if (clip != null)
            {
                return clip;
            }

#if UNITY_EDITOR
            UnityEditor.AssetDatabase.ImportAsset(BattleMusicAssetPath, UnityEditor.ImportAssetOptions.ForceUpdate);
            clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(BattleMusicAssetPath);
            if (clip != null)
            {
                Debug.Log("神魔镇荒使用编辑器资源路径加载战斗 BGM。");
                return clip;
            }

            var guids = UnityEditor.AssetDatabase.FindAssets("hyoshi_action_track_2 t:AudioClip", new[] { "Assets" });
            foreach (var guid in guids)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null)
                {
                    Debug.Log($"神魔镇荒使用搜索到的音频资源加载战斗 BGM：{path}");
                    return clip;
                }
            }
#endif

            return null;
        }

        private static AudioClip CreateFallbackMusicClip()
        {
            const int sampleRate = 44100;
            const float duration = 12f;
            var sampleCount = Mathf.RoundToInt(sampleRate * duration);
            var samples = new float[sampleCount];
            var scale = new[] { 220f, 261.63f, 293.66f, 329.63f, 392f, 440f, 523.25f, 587.33f };

            for (var i = 0; i < samples.Length; i++)
            {
                var t = i / (float)sampleRate;
                var beat = t * 2f;
                var step = Mathf.FloorToInt(beat * 2f) % 16;
                var note = scale[(step * 3 + (step >= 8 ? 2 : 0)) % scale.Length];
                var phraseLift = step >= 8 ? 1.125f : 1f;

                var melodyGate = SmoothPulse(beat * 2f, 0.18f);
                var melody = Mathf.Sin(2f * Mathf.PI * note * phraseLift * t) * 0.055f * melodyGate;
                melody += Mathf.Sin(2f * Mathf.PI * note * 2f * phraseLift * t) * 0.018f * melodyGate;

                var bassNote = step < 8 ? 110f : 130.81f;
                var bassGate = SmoothPulse(beat, 0.32f);
                var bass = Mathf.Sin(2f * Mathf.PI * bassNote * t) * 0.08f * bassGate;

                var drumPhase = beat - Mathf.Floor(beat);
                var drum = Mathf.Exp(-drumPhase * 18f) * Mathf.Sin(2f * Mathf.PI * 64f * t) * 0.09f;
                if (step % 4 == 2)
                {
                    drum += Mathf.Exp(-drumPhase * 30f) * Noise01(i) * 0.025f;
                }

                var pad = Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.025f;
                samples[i] = Mathf.Clamp((melody + bass + drum + pad) * 0.78f, -0.32f, 0.32f);
            }

            var clip = AudioClip.Create("XTD_Temporary_Battle_BGM", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static float SmoothPulse(float value, float width)
        {
            var phase = value - Mathf.Floor(value);
            if (phase > width)
            {
                return 0f;
            }

            var normalized = phase / Mathf.Max(0.001f, width);
            return Mathf.Sin(normalized * Mathf.PI);
        }

        private static float Noise01(int seed)
        {
            var value = Mathf.Sin(seed * 12.9898f) * 43758.5453f;
            return (value - Mathf.Floor(value)) * 2f - 1f;
        }

        private static AudioClip[] LoadHitSfxClips()
        {
            var clips = new List<AudioClip>();
            foreach (var path in HitSfxResourcePaths)
            {
                var clip = Resources.Load<AudioClip>(path);
                if (clip != null)
                {
                    clips.Add(clip);
                }
            }

            return clips.ToArray();
        }

        private void PlayHitSfx(Faction faction)
        {
            if (audioSource == null)
            {
                return;
            }

            if (hitSfxClips != null && hitSfxClips.Length > 0)
            {
                var clip = hitSfxClips[Random.Range(0, hitSfxClips.Length)];
                if (clip != null)
                {
                    EnsureAudioClipData(clip, $"打击音效 {clip.name}");
                    var volume = faction == Faction.Player ? hitSfxVolume * 0.9f : hitSfxVolume;
                    audioSource.PlayOneShot(clip, volume);
                    return;
                }
            }

            PlayOneShot(ref hitClip, faction == Faction.Player ? 240f : 310f, 0.035f);
        }

        private void PlayOneShot(ref AudioClip clip, float frequency, float volume)
        {
            if (audioSource == null)
            {
                return;
            }

            clip ??= CreateToneClip(frequency, 0.08f);
            EnsureAudioClipData(clip, clip.name);
            audioSource.PlayOneShot(clip, volume);
        }

        private static bool EnsureAudioClipData(AudioClip clip, string label)
        {
            if (clip == null)
            {
                return false;
            }

            if (clip.loadState == AudioDataLoadState.Failed)
            {
                Debug.LogWarning($"神魔镇荒音频加载失败：{label}");
                return false;
            }

            if (clip.loadState == AudioDataLoadState.Unloaded && !clip.LoadAudioData())
            {
                Debug.LogWarning($"神魔镇荒音频数据未能载入：{label}");
                return false;
            }

            return true;
        }

        private static AudioClip CreateToneClip(float frequency, float duration)
        {
            const int sampleRate = 22050;
            var sampleCount = Mathf.Max(1, Mathf.RoundToInt(sampleRate * duration));
            var samples = new float[sampleCount];
            for (var i = 0; i < samples.Length; i++)
            {
                var t = i / (float)sampleRate;
                var envelope = 1f - (i / (float)samples.Length);
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * 0.24f;
            }

            var clip = AudioClip.Create($"XTD_Tone_{frequency:0}", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private BattleUnit CreateUnitInstance()
        {
            var go = new GameObject("Pooled Battle Unit");
            go.transform.SetParent(transform);
            go.AddComponent<SpriteRenderer>();
            return go.AddComponent<BattleUnit>();
        }

        private ProjectileView CreateProjectileInstance()
        {
            var go = new GameObject("Pooled Projectile");
            go.transform.SetParent(transform);
            go.AddComponent<SpriteRenderer>();
            return go.AddComponent<ProjectileView>();
        }

        private DamageNumberView CreateDamageNumberInstance()
        {
            var go = new GameObject("Pooled Damage Number");
            go.transform.SetParent(transform);
            return go.AddComponent<DamageNumberView>();
        }

        private SimpleEffectView CreateEffectInstance()
        {
            var go = new GameObject("Pooled Hit Effect");
            go.transform.SetParent(transform);
            go.AddComponent<SpriteRenderer>();
            return go.AddComponent<SimpleEffectView>();
        }
    }
}

