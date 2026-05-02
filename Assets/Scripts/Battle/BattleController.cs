using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using XTD.Cards;
using XTD.Content;
using XTD.Flow;
using XTD.Presentation;

namespace XTD.Battle
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class BattleController : MonoBehaviour
    {
        private const string BattleMusicResourcePath = "Audio/BGM/hyoshi_action_track_2";
        private const string BattleMusicAssetPath = "Assets/Resources/Audio/BGM/hyoshi_action_track_2.ogg";
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

        [Header("Audio")]
        [SerializeField] private AudioClip battleMusicClip;
        [SerializeField, Range(0f, 1f)] private float battleMusicVolume = 0.22f;
        [SerializeField] private AudioClip[] hitSfxClips;
        [SerializeField, Range(0f, 1f)] private float hitSfxVolume = 0.12f;

        private readonly List<BattleUnit> activeUnits = new();
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
        private float hitSfxCooldown;

        public BattleOutcome Outcome { get; private set; } = BattleOutcome.Running;
        public float PlayerBaseHp { get; private set; }
        public float EnemyBaseHp { get; private set; }
        public float Mana => mana;
        public int MaxMana => maxMana;
        public int CurrentCommand => activeUnits
            .Count(unit => unit != null && unit.IsAlive && unit.Faction == Faction.Player && unit.Definition.role == UnitRole.Structure);
        public int MaxCommand => maxCommand;
        public int MoraleCharges => morale.Charges;
        public int MoralePendingSoldiers => morale.PendingSoldiers;
        public int MoraleSoldiersPerCharge => morale.SoldiersPerCharge;
        public bool NextCardWillUseMorale => morale.Charges > 0;
        public DeckRuntime Deck => deck;
        public float BattleMidY => (playerBaseY + enemyBaseY) * 0.5f;
        public bool HasEnemyBase => encounter == null || encounter.coreEnemy == null;
        public string EnemyObjectiveLabel => HasEnemyBase ? "敌方基地" : "敌方核心";
        public float EnemyObjectiveHp => HasEnemyBase ? EnemyBaseHp : Mathf.Max(0f, EnemyCoreHp);
        public float EnemyCoreHp => EnemyCoreUnit()?.CurrentHp ?? 0f;

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

            unitPool = new ComponentPool<BattleUnit>(CreateUnitInstance);
            projectilePool = new ComponentPool<ProjectileView>(CreateProjectileInstance);
            damageNumberPool = new ComponentPool<DamageNumberView>(CreateDamageNumberInstance);
            effectPool = new ComponentPool<SimpleEffectView>(CreateEffectInstance);

            ui = FindFirstObjectByType<BattleUiController>();
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

            ui.Refresh();
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
            morale.Reset();
            ApplyRunBattleModifiers();
            mana = Mathf.Min(maxMana, 4f + (flow != null && flow.HasActiveRun ? flow.ExtraStartingMana() : 0f));
            enemySpawnTimer = 0.5f;
            coreAreaSkillTimer = 3.2f;
            coreBuffSkillTimer = 5.5f;
            coreWarningTimer = 0f;
            hitSfxCooldown = 0f;
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

            mana -= card.cost;
            deck.Play(card);
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

            if (mana < card.cost)
            {
                return false;
            }

            var wouldUseMorale = card.CanReceiveMorale && morale.Charges > 0;
            return CurrentCommand + CalculateCommandCost(card, wouldUseMorale) <= maxCommand;
        }

        public bool CanReleaseCardAt(CardDefinition card, Vector3 targetPosition, out string reason)
        {
            reason = string.Empty;
            if (!CanPlayCard(card))
            {
                reason = "费用或阵位不足";
                return false;
            }

            if (card.releaseRule == CardReleaseRule.PlayerSide && targetPosition.y > BattleMidY - 0.15f)
            {
                reason = "建筑和召唤不能越过中线";
                return false;
            }

            if (card.releaseRule != CardReleaseRule.None && (targetPosition.x < placementMinX || targetPosition.x > placementMaxX))
            {
                reason = "超出战场范围";
                return false;
            }

            return true;
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
            if (!CanUnitAttackBase(unit))
            {
                return false;
            }

            var targetBaseY = unit.Faction == Faction.Player ? enemyBaseY : playerBaseY;
            return Mathf.Abs(targetBaseY - unit.transform.position.y) <= Mathf.Max(0.25f, unit.Definition.range);
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

            return unit.Definition.role != UnitRole.Boss;
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

            SpawnUnit(unitDefinition, faction, position, true);
            if (faction == Faction.Player && (unitDefinition.role == UnitRole.Soldier || unitDefinition.role == UnitRole.Elite))
            {
                morale.RegisterSummonedSoldiers(1);
            }

            return true;
        }

        private void Tick(float deltaTime)
        {
            mana = Mathf.Min(maxMana, mana + manaRegenPerSecond * deltaTime);
            hitSfxCooldown = Mathf.Max(0f, hitSfxCooldown - deltaTime);

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

        private void TickEnemyBase(float deltaTime)
        {
            if (encounter == null || encounter.enemySpawns.Count == 0)
            {
                return;
            }

            enemySpawnTimer -= deltaTime;
            if (enemySpawnTimer > 0f)
            {
                return;
            }

            enemySpawnTimer = Mathf.Max(0.2f, encounter.enemySpawnInterval * CoreSpawnIntervalMultiplier());
            var entry = encounter.enemySpawns[Random.Range(0, encounter.enemySpawns.Count)];
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

        private void PrepareCoreAreaBlast(BattleUnit core, bool enrage)
        {
            var target = activeUnits
                .Where(unit => unit != null && unit.IsAlive && unit.Faction == Faction.Player && unit.Definition.role != UnitRole.Structure)
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
                .Where(unit => unit != null && unit.IsAlive && unit.Faction == Faction.Player && unit.Definition.role != UnitRole.Structure)
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
                .Where(unit => unit != null && unit.IsAlive && unit.Faction == Faction.Enemy && unit.Definition.role != UnitRole.Boss)
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
            return core != null && core.Definition.maxHp > 0f && core.CurrentHp / core.Definition.maxHp <= 0.5f;
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
            var spawnedSoldiers = 0;
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
                    var x = spawnCenter.x + (column - (columns - 1) * 0.5f) * spawn.spacing + Random.Range(-spawn.yJitter, spawn.yJitter);
                    var y = spawnCenter.y - row * spawn.spacing * 0.65f;
                    var unit = SpawnUnit(spawn.unit, Faction.Player, new Vector3(x, y, 0f), true);
                    if (unit.Definition.role == UnitRole.Soldier || unit.Definition.role == UnitRole.Elite)
                    {
                        spawnedSoldiers++;
                    }

                    if (strengthened && unit.Definition.role != UnitRole.Structure)
                    {
                        moraleUnits.Add(unit);
                    }
                }
            }

            ApplyMoraleUnitBonus(card, moraleUnits);

            if (spawnedSoldiers > 0)
            {
                morale.RegisterSummonedSoldiers(spawnedSoldiers);
            }

            foreach (var effect in card.effects)
            {
                ResolveEffect(effect, strengthened, targetPosition, card.releaseRule == CardReleaseRule.Anywhere);
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
                _ => $"士气强化：{card.displayName}"
            };
        }

        private Vector3 ResolveSpawnCenter(CardDefinition card, Vector3 targetPosition)
        {
            var x = Mathf.Clamp(targetPosition.x, placementMinX, placementMaxX);
            var y = card.releaseRule == CardReleaseRule.PlayerSide
                ? Mathf.Clamp(targetPosition.y, playerBaseY + 0.55f, BattleMidY - 0.2f)
                : targetPosition.y;
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

            var playerBaseSprite = catalog.FindUnit("unit_incense_barracks")?.art;
            var enemyBaseSprite = catalog.FindUnit("unit_roadblock")?.art ?? catalog.FindUnit("enemy_alpha")?.art;

            playerBaseView.Initialize(Faction.Player, playerBaseSprite, PlayerBaseViewPosition(), 0.85f, CurrentPlayerBattleMaxHp());
            enemyBaseView.gameObject.SetActive(HasEnemyBase);
            if (HasEnemyBase)
            {
                enemyBaseView.Initialize(Faction.Enemy, enemyBaseSprite, EnemyBaseViewPosition(), 0.92f, EnemyBaseHp);
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
            return new Vector3(laneX, playerBaseY - 0.12f, 0f);
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
            var unit = unitPool.Get();
            unit.Initialize(this, unitDefinition, faction, position);
            activeUnits.Add(unit);
            if (countCommand && faction == Faction.Player)
            {
                PlayOneShot(ref summonClip, 720f, 0.045f);
            }

            return unit;
        }

        private void ResolveEffect(BattleEffectDefinition effect, bool strengthened, Vector3 targetPosition, bool usePlacementTarget)
        {
            var value = strengthened ? effect.value * 1.5f : effect.value;
            var duration = strengthened ? effect.duration * 1.25f : effect.duration;
            var isPlacedDamage = usePlacementTarget && (effect.effectType == EffectType.Damage || effect.effectType == EffectType.AreaDamage);
            var targets = isPlacedDamage
                ? SelectTargetsNearPosition(effect.targetRule, targetPosition, effect.radius, effect.effectType)
                : SelectTargets(effect.targetRule, effect.radius);

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

                    if (isPlacedDamage && targets.Count == 0 && IsEnemyBasePoint(targetPosition, effect.radius))
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
                    foreach (var target in targets)
                    {
                        target.AddModifier(effect.effectType, value, duration);
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

            var usingFallbackMusic = false;
            battleMusicClip ??= LoadBattleMusicClip();
            if (battleMusicClip == null)
            {
                battleMusicClip = CreateFallbackMusicClip();
                usingFallbackMusic = true;
                Debug.Log("X-TD 外部 BGM 暂未加载，使用程序生成的临时战斗 BGM。");
            }

            if (!EnsureAudioClipData(battleMusicClip, "战斗 BGM"))
            {
                return;
            }

            musicSource.clip = battleMusicClip;
            musicSource.volume = usingFallbackMusic ? Mathf.Max(battleMusicVolume, 0.38f) : battleMusicVolume;
            if (!musicSource.isPlaying)
            {
                musicSource.Play();
            }
        }

        private void StopBattleMusic()
        {
            if (musicSource != null && musicSource.isPlaying)
            {
                musicSource.Stop();
            }
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
                Debug.Log("X-TD 使用编辑器资源路径加载战斗 BGM。");
                return clip;
            }

            var guids = UnityEditor.AssetDatabase.FindAssets("hyoshi_action_track_2 t:AudioClip", new[] { "Assets" });
            foreach (var guid in guids)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null)
                {
                    Debug.Log($"X-TD 使用搜索到的音频资源加载战斗 BGM：{path}");
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
                Debug.LogWarning($"X-TD 音频加载失败：{label}");
                return false;
            }

            if (clip.loadState == AudioDataLoadState.Unloaded && !clip.LoadAudioData())
            {
                Debug.LogWarning($"X-TD 音频数据未能载入：{label}");
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

