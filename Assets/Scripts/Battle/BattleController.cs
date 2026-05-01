using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using XTD.Cards;
using XTD.Content;
using XTD.Presentation;

namespace XTD.Battle
{
    public sealed class BattleController : MonoBehaviour
    {
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
        private float mana;

        public BattleOutcome Outcome { get; private set; } = BattleOutcome.Running;
        public float PlayerBaseHp { get; private set; }
        public float EnemyBaseHp { get; private set; }
        public float Mana => mana;
        public int MaxMana => maxMana;
        public int CurrentCommand => activeUnits.Where(unit => unit != null && unit.IsAlive && unit.Faction == Faction.Player).Sum(unit => unit.Definition.commandCost);
        public int MaxCommand => maxCommand;
        public int MoraleCharges => morale.Charges;
        public DeckRuntime Deck => deck;
        public float BattleMidY => (playerBaseY + enemyBaseY) * 0.5f;

        private void Awake()
        {
            catalog = ResolveContentCatalog();
            encounter = !string.IsNullOrWhiteSpace(encounterId) ? catalog.FindEncounter(encounterId) : null;
            encounter ??= catalog.FirstEncounter(MapNodeType.NormalMonster);

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
            mana = 4f;
            enemySpawnTimer = 0.5f;
            PlayerBaseHp = encounter != null ? encounter.playerBaseMaxHp : 100f;
            EnemyBaseHp = encounter != null ? encounter.enemyBaseMaxHp : 120f;
            EnsureBaseViews();
            RefreshBaseViews();

            var runState = DemoContentFactory.CreateStartingRun(catalog);
            var startingCards = runState.deckCardIds
                .Select(id => catalog.FindCard(id))
                .Where(card => card != null);
            deck = new DeckRuntime(startingCards, runState.seed);
            deck.DrawFullHand();
            ui.HideResult();
            ui.Refresh();

            if (encounter != null && encounter.coreEnemy != null)
            {
                SpawnUnit(encounter.coreEnemy, Faction.Enemy, RandomEnemySpawnPosition(0.65f), false);
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
                reason = "费用或统率不足";
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
            var targetBaseY = unit.Faction == Faction.Player ? enemyBaseY : playerBaseY;
            return Mathf.Abs(targetBaseY - unit.transform.position.y) <= Mathf.Max(0.25f, unit.Definition.range);
        }

        public Vector3 GetAdvanceTargetFor(BattleUnit unit)
        {
            var x = Mathf.Clamp(unit.transform.position.x, placementMinX, placementMaxX);
            var targetY = unit.Faction == Faction.Player ? enemyBaseY : playerBaseY;
            return new Vector3(x, targetY, 0f);
        }

        public void DamageEnemyBase(float damage)
        {
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
            playerBaseView?.UpdateHealth(PlayerBaseHp, encounter != null ? encounter.playerBaseMaxHp : 100f);
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
        }

        public void SpawnSpellImpact(Vector3 position)
        {
            var effect = effectPool.Get();
            effect.Initialize(position, Faction.Player, spellImpactSprite != null ? spellImpactSprite : hitEffectSprite, 0.55f, () => effectPool.Release(effect));
        }

        public bool TrySpawnProducedUnit(UnitDefinition unitDefinition, Faction faction, Vector3 position)
        {
            if (unitDefinition == null || Outcome != BattleOutcome.Running)
            {
                return false;
            }

            if (faction == Faction.Player && CurrentCommand + unitDefinition.commandCost > maxCommand)
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

            TickEnemyBase(deltaTime);

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

            enemySpawnTimer = Mathf.Max(0.2f, encounter.enemySpawnInterval);
            var entry = encounter.enemySpawns[Random.Range(0, encounter.enemySpawns.Count)];
            for (var i = 0; i < entry.count; i++)
            {
                SpawnUnit(entry.unit, Faction.Enemy, RandomEnemySpawnPosition(0.25f + i * 0.22f), false);
            }
        }

        private int CalculateCommandCost(CardDefinition card, bool strengthened)
        {
            var total = card.CommandCost();
            if (strengthened && card.unitSpawns.Count > 0 && card.unitSpawns[0].unit != null)
            {
                total += card.unitSpawns[0].unit.commandCost;
            }

            return total;
        }

        private void ResolveCard(CardDefinition card, bool strengthened, Vector3 targetPosition)
        {
            var spawnedSoldiers = 0;
            foreach (var spawn in card.unitSpawns)
            {
                if (spawn?.unit == null)
                {
                    continue;
                }

                var count = spawn.count;
                if (strengthened && spawnedSoldiers == 0)
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
                }
            }

            if (spawnedSoldiers > 0)
            {
                morale.RegisterSummonedSoldiers(spawnedSoldiers);
            }

            foreach (var effect in card.effects)
            {
                ResolveEffect(effect, strengthened, targetPosition, card.releaseRule == CardReleaseRule.Anywhere);
            }
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

            playerBaseView.Initialize(Faction.Player, playerBaseSprite, PlayerBaseViewPosition(), 0.85f, PlayerBaseHp);
            enemyBaseView.Initialize(Faction.Enemy, enemyBaseSprite, EnemyBaseViewPosition(), 0.92f, EnemyBaseHp);
        }

        private void RefreshBaseViews()
        {
            playerBaseView?.UpdateHealth(PlayerBaseHp, encounter != null ? encounter.playerBaseMaxHp : 100f);
            enemyBaseView?.UpdateHealth(EnemyBaseHp, encounter != null ? encounter.enemyBaseMaxHp : 120f);
        }

        private BattleBaseView CreateBaseView(string objectName)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(transform);
            return go.AddComponent<BattleBaseView>();
        }

        private Vector3 PlayerBaseViewPosition()
        {
            return new Vector3(placementMinX + 1.05f, playerBaseY - 0.12f, 0f);
        }

        private Vector3 EnemyBaseViewPosition()
        {
            return new Vector3(placementMaxX - 1.05f, enemyBaseY + 0.18f, 0f);
        }

        private BattleUnit SpawnUnit(UnitDefinition unitDefinition, Faction faction, Vector3 position, bool countCommand)
        {
            var unit = unitPool.Get();
            unit.Initialize(this, unitDefinition, faction, position);
            activeUnits.Add(unit);
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
            }
        }

        private bool IsEnemyBasePoint(Vector3 targetPosition, float radius)
        {
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

            if (EnemyBaseHp <= 0f)
            {
                Outcome = BattleOutcome.Victory;
                ui.ShowResult("胜利");
            }
            else if (encounter != null && encounter.coreEnemy != null && !HasLivingEnemyCore())
            {
                Outcome = BattleOutcome.Victory;
                ui.ShowResult("胜利");
            }
            else if (PlayerBaseHp <= 0f)
            {
                Outcome = BattleOutcome.Defeat;
                ui.ShowResult("失败");
            }
        }

        private bool HasLivingEnemyCore()
        {
            return activeUnits.Any(unit =>
                unit != null &&
                unit.IsAlive &&
                unit.Faction == Faction.Enemy &&
                unit.Definition == encounter.coreEnemy);
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

