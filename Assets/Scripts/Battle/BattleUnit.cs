using System.Collections.Generic;
using UnityEngine;
using XTD.Content;
using XTD.Presentation;

namespace XTD.Battle
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class BattleUnit : MonoBehaviour
    {
        private const float VisualScaleMultiplier = 0.5f;

        private static readonly Dictionary<int, Sprite[]> spriteSheetCache = new();
        private static readonly int[] WalkCycleFrames = { 0, 2, 1, 2 };

        private readonly List<TimedUnitModifier> modifiers = new();
        private BattleController controller;
        private SpriteRenderer spriteRenderer;
        private Transform healthBarRoot;
        private SpriteRenderer healthBarBack;
        private SpriteRenderer healthBarFill;
        private SpriteRenderer shieldGlow;
        private SpriteRenderer attackGlow;
        private float attackCooldown;
        private float productionCooldown;
        private float shield;
        private float shieldGlowTimer;
        private float attackGlowTimer;
        private Vector3 baseScale;
        private Color baseColor;
        private float animationSeed;
        private float movementSignal;
        private float attackPulse;
        private float hitFlash;
        private int cachedSortingOrder = int.MinValue;
        private Sprite[] animationFrames;

        public UnitDefinition Definition { get; private set; }
        public Faction Faction { get; private set; }
        public float CurrentHp { get; private set; }
        public bool IsAlive => CurrentHp > 0f;
        public bool IsStructure => Definition != null && Definition.role == UnitRole.Structure;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        public void Initialize(BattleController owner, UnitDefinition definition, Faction faction, Vector3 position)
        {
            controller = owner;
            Definition = definition;
            Faction = faction;
            CurrentHp = EffectiveMaxHp();
            shield = 0f;
            attackCooldown = Random.Range(0f, definition.attackInterval * 0.4f);
            productionCooldown = definition.ProducesUnits ? EffectiveProductionInterval() : 0f;
            modifiers.Clear();
            transform.position = position;
            gameObject.name = $"{faction}_{definition.displayName}";
            gameObject.SetActive(true);
            transform.localRotation = Quaternion.identity;
            baseScale = ScaleFor(definition);
            transform.localScale = baseScale;
            animationSeed = Random.Range(0f, 100f);
            movementSignal = 0f;
            attackPulse = 0f;
            hitFlash = 0f;
            shieldGlowTimer = 0f;
            attackGlowTimer = 0f;
            cachedSortingOrder = int.MinValue;

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
                if (spriteRenderer == null)
                {
                    spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
                }
            }

            var baseSprite = definition.art != null ? definition.art : RuntimeSpriteFactory.UnitSprite;
            animationFrames = CreateAnimationFrames(definition, baseSprite);
            spriteRenderer.sprite = animationFrames is { Length: > 0 } ? animationFrames[0] : baseSprite;
            baseColor = definition.tint;
            spriteRenderer.color = baseColor;
            spriteRenderer.flipX = false;
            spriteRenderer.enabled = true;
            if (shieldGlow != null)
            {
                shieldGlow.gameObject.SetActive(false);
            }

            if (attackGlow != null)
            {
                attackGlow.gameObject.SetActive(false);
            }

            UpdateSortingOrder();
            EnsureHealthBar();
            UpdateHealthBar();
        }

        private void Update()
        {
            if (Definition == null || spriteRenderer == null || !gameObject.activeSelf)
            {
                return;
            }

            AnimatePresentation(Time.deltaTime);
        }

        public void Tick(float deltaTime)
        {
            if (!IsAlive)
            {
                return;
            }

            TickModifiers(deltaTime);
            if (HasModifier(EffectType.Stun))
            {
                return;
            }

            attackCooldown -= deltaTime;
            TickProduction(deltaTime);

            var target = controller.FindTargetFor(this);
            if (target != null && IsInRange(target.transform.position))
            {
                TryAttack(target);
                return;
            }

            if (controller.IsEnemyBaseInRange(this))
            {
                TryAttackBase();
                return;
            }

            MoveTowards(target != null ? target.transform.position : controller.GetAdvanceTargetFor(this), deltaTime);
        }

        public void TakeDamage(float damage)
        {
            if (!IsAlive)
            {
                return;
            }

            var remainingDamage = damage;
            if (shield > 0f)
            {
                var absorbed = Mathf.Min(shield, remainingDamage);
                shield -= absorbed;
                remainingDamage -= absorbed;
            }

            CurrentHp -= remainingDamage;
            hitFlash = 1f;
            controller.SpawnDamageNumber(transform.position, Mathf.Max(0f, damage));
            controller.SpawnHitEffect(transform.position, Faction);
            UpdateHealthBar();

            if (CurrentHp <= 0f)
            {
                Die();
            }
        }

        public void Heal(float amount)
        {
            if (!IsAlive)
            {
                return;
            }

            CurrentHp = Mathf.Min(EffectiveMaxHp(), CurrentHp + amount);
            UpdateHealthBar();
        }

        public void AddShield(float amount)
        {
            shield += Mathf.Max(0f, amount);
            shieldGlowTimer = Mathf.Max(shieldGlowTimer, 5.5f);
            UpdateStatusGlows();
        }

        public void AddModifier(EffectType type, float value, float duration)
        {
            modifiers.Add(new TimedUnitModifier(type, value, duration));
            if (type is EffectType.BuffAttack or EffectType.BuffAttackSpeed)
            {
                attackGlowTimer = Mathf.Max(attackGlowTimer, Mathf.Max(1.2f, duration));
                UpdateStatusGlows();
            }
        }

        public void KnockbackFrom(Vector3 sourcePosition, float distance)
        {
            if (!IsAlive || distance <= 0f || Definition.role == UnitRole.Boss || Definition.role == UnitRole.Structure)
            {
                return;
            }

            var direction = transform.position - sourcePosition;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Faction == Faction.Player ? Vector3.down : Vector3.up;
            }

            transform.position += direction.normalized * Mathf.Clamp(distance, 0.15f, 1.4f);
            movementSignal = 1f;
        }

        public float EffectiveAttack()
        {
            var multiplier = 1f;
            foreach (var modifier in modifiers)
            {
                if (modifier.type == EffectType.BuffAttack)
                {
                    multiplier += modifier.value;
                }
            }

            return Definition.attack * Mathf.Max(0.1f, multiplier) * controller.AttackMultiplierFor(Definition);
        }

        public float EffectiveAttackInterval()
        {
            var multiplier = 1f;
            foreach (var modifier in modifiers)
            {
                if (modifier.type == EffectType.BuffAttackSpeed)
                {
                    multiplier -= modifier.value;
                }
            }

            return Definition.attackInterval * Mathf.Clamp(multiplier, 0.25f, 3f);
        }

        private float EffectiveMaxHp()
        {
            return Definition.maxHp * controller.MaxHpMultiplierFor(Definition, Faction);
        }

        private float EffectiveRange()
        {
            return Definition.range * controller.RangeMultiplierFor(Definition, Faction);
        }

        private float EffectiveProductionInterval()
        {
            return Mathf.Max(0.35f, Definition.productionInterval * controller.ProductionIntervalMultiplierFor(Definition, Faction));
        }

        private void TickModifiers(float deltaTime)
        {
            for (var i = modifiers.Count - 1; i >= 0; i--)
            {
                if (modifiers[i].type is EffectType.Burn or EffectType.Poison)
                {
                    modifiers[i].tickTimer -= deltaTime;
                    if (modifiers[i].tickTimer <= 0f)
                    {
                        modifiers[i].tickTimer += 0.65f;
                        ApplyStatusDamage(modifiers[i].value * 0.65f, modifiers[i].type);
                        if (!IsAlive)
                        {
                            return;
                        }
                    }
                }

                modifiers[i].remaining -= deltaTime;
                if (modifiers[i].remaining <= 0f)
                {
                    modifiers.RemoveAt(i);
                }
            }
        }

        private void ApplyStatusDamage(float damage, EffectType source)
        {
            if (!IsAlive || damage <= 0f)
            {
                return;
            }

            CurrentHp -= damage;
            hitFlash = Mathf.Max(hitFlash, source == EffectType.Burn ? 0.75f : 0.45f);
            controller.SpawnDamageNumber(transform.position + new Vector3(0f, 0.18f, 0f), damage);
            UpdateHealthBar();
            if (CurrentHp <= 0f)
            {
                Die();
            }
        }

        private bool HasModifier(EffectType type)
        {
            foreach (var modifier in modifiers)
            {
                if (modifier.type == type)
                {
                    return true;
                }
            }

            return false;
        }

        private float EffectiveMoveSpeedMultiplier()
        {
            var multiplier = 1f;
            foreach (var modifier in modifiers)
            {
                if (modifier.type == EffectType.Slow)
                {
                    multiplier -= Mathf.Clamp01(modifier.value);
                }
            }

            return Mathf.Clamp(multiplier, 0.18f, 2f);
        }

        private bool IsInRange(Vector3 targetPosition)
        {
            return Vector2.Distance(transform.position, targetPosition) <= EffectiveRange();
        }

        private void TryAttack(BattleUnit target)
        {
            if (attackCooldown > 0f || Definition.attack <= 0f)
            {
                return;
            }

            attackCooldown = EffectiveAttackInterval();
            attackPulse = 1f;
            if (Definition.IsRanged)
            {
                controller.SpawnProjectile(transform.position, target.transform.position, Faction);
            }

            target.TakeDamage(EffectiveAttack());
        }

        private void TryAttackBase()
        {
            if (attackCooldown > 0f || Definition.attack <= 0f)
            {
                return;
            }

            attackCooldown = EffectiveAttackInterval();
            attackPulse = 1f;
            if (Faction == Faction.Player)
            {
                controller.DamageEnemyBase(EffectiveAttack());
            }
            else
            {
                controller.DamagePlayerBase(EffectiveAttack());
            }
        }

        private void MoveTowards(Vector3 targetPosition, float deltaTime)
        {
            if (Definition.moveSpeed <= 0f)
            {
                return;
            }

            var current = (Vector2)transform.position;
            var target = (Vector2)targetPosition;
            var direction = target - current;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            var step = Definition.moveSpeed * EffectiveMoveSpeedMultiplier() * controller.MoveSpeedMultiplierFor(Definition) * deltaTime;
            var next = Vector2.MoveTowards(current, target, step);
            if ((next - current).sqrMagnitude > 0.000001f)
            {
                movementSignal = 1f;
            }

            transform.position = next;
        }

        private void TickProduction(float deltaTime)
        {
            if (!Definition.ProducesUnits)
            {
                return;
            }

            productionCooldown -= deltaTime;
            if (productionCooldown > 0f)
            {
                return;
            }

            productionCooldown = EffectiveProductionInterval();
            var direction = Faction == Faction.Player ? 1f : -1f;
            for (var i = 0; i < Definition.productionCount; i++)
            {
                var position = transform.position + new Vector3(
                    Random.Range(-Definition.productionSpread, Definition.productionSpread),
                    direction * (0.45f + i * 0.18f),
                    0f);

                if (!controller.TrySpawnProducedUnit(Definition.producedUnit, Faction, position))
                {
                    break;
                }
            }
        }

        private void Die()
        {
            controller.SpawnDeathEffect(transform.position, Faction);
            controller.NotifyUnitDied(this);
            controller.ReleaseUnit(this);
        }

        private void EnsureHealthBar()
        {
            if (healthBarRoot != null && healthBarBack != null && healthBarFill != null)
            {
                return;
            }

            if (healthBarRoot == null)
            {
                var root = new GameObject("生命条").transform;
                root.SetParent(transform, false);
                root.localPosition = new Vector3(0f, 0.38f, -0.02f);
                healthBarRoot = root;
            }

            if (healthBarBack == null)
            {
                healthBarBack = CreateHealthBarPart("底色", new Color(0.05f, 0.06f, 0.08f, 0.88f), 39);
            }

            if (healthBarFill == null)
            {
                healthBarFill = CreateHealthBarPart("血量", Color.green, 40);
            }
        }

        private SpriteRenderer CreateHealthBarPart(string name, Color color, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(healthBarRoot, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = RuntimeSpriteFactory.UnitSprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private void UpdateHealthBar()
        {
            if (healthBarRoot == null || healthBarBack == null || healthBarFill == null || Definition == null)
            {
                return;
            }

            var percent = Mathf.Clamp01(CurrentHp / Mathf.Max(1f, EffectiveMaxHp()));
            healthBarBack.transform.localPosition = Vector3.zero;
            healthBarBack.transform.localScale = new Vector3(0.42f, 0.05f, 1f);
            healthBarFill.transform.localPosition = new Vector3(-0.21f + 0.21f * percent, 0f, -0.01f);
            healthBarFill.transform.localScale = new Vector3(0.42f * percent, 0.035f, 1f);
            healthBarFill.color = Color.Lerp(new Color(1f, 0.18f, 0.12f), new Color(0.28f, 1f, 0.36f), percent);
        }

        private void AnimatePresentation(float deltaTime)
        {
            movementSignal = Mathf.MoveTowards(movementSignal, 0f, deltaTime * 6f);
            attackPulse = Mathf.MoveTowards(attackPulse, 0f, deltaTime * 5.5f);
            hitFlash = Mathf.MoveTowards(hitFlash, 0f, deltaTime * 7f);
            shieldGlowTimer = Mathf.Max(0f, shieldGlowTimer - deltaTime);
            attackGlowTimer = Mathf.Max(0f, attackGlowTimer - deltaTime);

            var isStructure = Definition.role == UnitRole.Structure;
            var frequency = isStructure ? 2.1f : Mathf.Lerp(2.4f, 9.5f, movementSignal);
            var wave = Mathf.Sin(Time.time * frequency + animationSeed);
            var idleBreath = isStructure ? 0.022f : 0.028f;
            var hasFrameAnimation = animationFrames is { Length: > 0 };
            var walkSquash = movementSignal * (hasFrameAnimation ? 0.018f : 0.045f);
            var punch = attackPulse * (isStructure ? 0.035f : 0.12f);

            var xScale = baseScale.x * (1f + wave * idleBreath - wave * walkSquash + punch);
            var yScale = baseScale.y * (1f + wave * idleBreath + wave * walkSquash * 0.65f - punch * 0.35f);
            transform.localScale = new Vector3(xScale, yScale, baseScale.z);

            var tiltStrength = hasFrameAnimation ? 1.2f : 4.5f;
            var tilt = isStructure ? 0f : wave * movementSignal * tiltStrength;
            transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

            spriteRenderer.color = Color.Lerp(baseColor, Color.white, hitFlash);
            UpdateAnimatedSprite();
            UpdateSortingOrder();
            if (ShouldUpdateStatusGlows())
            {
                UpdateStatusGlows();
            }
        }

        private void UpdateSortingOrder()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            var targetSortingOrder = 16 + Mathf.RoundToInt((5.2f - transform.position.y) * 3f);
            if (cachedSortingOrder == targetSortingOrder)
            {
                return;
            }

            cachedSortingOrder = targetSortingOrder;
            spriteRenderer.sortingOrder = targetSortingOrder;
            if (healthBarBack != null)
            {
                healthBarBack.sortingOrder = spriteRenderer.sortingOrder + 20;
            }

            if (healthBarFill != null)
            {
                healthBarFill.sortingOrder = spriteRenderer.sortingOrder + 21;
            }

            if (shieldGlow != null)
            {
                shieldGlow.sortingOrder = spriteRenderer.sortingOrder - 2;
            }

            if (attackGlow != null)
            {
                attackGlow.sortingOrder = spriteRenderer.sortingOrder - 1;
            }
        }

        private void EnsureStatusGlows()
        {
            if (shieldGlow == null)
            {
                shieldGlow = CreateStatusGlow("护盾光晕");
            }

            if (attackGlow == null)
            {
                attackGlow = CreateStatusGlow("强化光晕");
            }
        }

        private SpriteRenderer CreateStatusGlow(string objectName)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = Vector3.one;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = RuntimeSpriteFactory.EffectSprite;
            renderer.color = new Color(1f, 1f, 1f, 0f);
            renderer.gameObject.SetActive(false);
            return renderer;
        }

        private bool ShouldUpdateStatusGlows()
        {
            return shield > 0.01f
                   || shieldGlowTimer > 0f
                   || attackGlowTimer > 0f
                   || (shieldGlow != null && shieldGlow.gameObject.activeSelf)
                   || (attackGlow != null && attackGlow.gameObject.activeSelf);
        }

        private void UpdateStatusGlows()
        {
            if (Definition == null)
            {
                return;
            }

            EnsureStatusGlows();
            var visualSizeMultiplier = VisualSizeMultiplierFor(Definition);
            var roleScale = Definition.role switch
            {
                UnitRole.Structure => 2.25f * VisualScaleMultiplier,
                UnitRole.Boss => 1.9f * VisualScaleMultiplier * visualSizeMultiplier,
                UnitRole.Hero => 1.55f * VisualScaleMultiplier * visualSizeMultiplier,
                UnitRole.Elite => 1.45f * VisualScaleMultiplier * visualSizeMultiplier,
                _ => 1.25f * VisualScaleMultiplier * visualSizeMultiplier
            };

            var pulse = 0.92f + Mathf.Sin(Time.time * 4.5f + animationSeed) * 0.08f;
            var shieldActive = shield > 0.01f || shieldGlowTimer > 0f;
            shieldGlow.gameObject.SetActive(shieldActive);
            if (shieldActive)
            {
                var alpha = shield > 0.01f ? 0.34f : 0.22f * Mathf.Clamp01(shieldGlowTimer / 5.5f);
                shieldGlow.color = new Color(1f, 0.78f, 0.20f, alpha);
                shieldGlow.transform.localScale = Vector3.one * roleScale * pulse;
            }

            var attackActive = attackGlowTimer > 0f;
            attackGlow.gameObject.SetActive(attackActive);
            if (attackActive)
            {
                var alpha = 0.30f * Mathf.Clamp01(attackGlowTimer / 1.2f);
                attackGlow.color = new Color(1f, 0.18f, 0.08f, alpha);
                attackGlow.transform.localScale = Vector3.one * roleScale * 1.16f * (1.02f - pulse * 0.08f);
            }
        }

        private void UpdateAnimatedSprite()
        {
            if (animationFrames == null || animationFrames.Length == 0 || spriteRenderer == null)
            {
                return;
            }

            var frameIndex = 0;
            if (hitFlash > 0.25f && animationFrames.Length > 5)
            {
                frameIndex = 5;
            }
            else if (attackPulse > 0.08f && animationFrames.Length > 4)
            {
                frameIndex = attackPulse > 0.45f ? 3 : 4;
            }
            else if (movementSignal > 0.15f && animationFrames.Length > 2)
            {
                var cycleIndex = Mathf.FloorToInt(Time.time * 8.5f + animationSeed) % WalkCycleFrames.Length;
                frameIndex = WalkCycleFrames[cycleIndex];
            }
            else if (animationFrames.Length > 1)
            {
                frameIndex = Mathf.FloorToInt(Time.time * 2.4f + animationSeed) % 2;
            }

            spriteRenderer.sprite = animationFrames[Mathf.Clamp(frameIndex, 0, animationFrames.Length - 1)];
        }

        private static Sprite[] CreateAnimationFrames(UnitDefinition definition, Sprite source)
        {
            if (!CanUseAnimationSheet(definition, source))
            {
                return null;
            }

            var key = source.GetInstanceID();
            if (spriteSheetCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            const int columns = 3;
            const int rows = 2;
            var texture = source.texture;
            if (texture.width < columns || texture.height < rows)
            {
                return null;
            }

            var cellWidth = texture.width / columns;
            var cellHeight = texture.height / rows;
            var frames = new Sprite[columns * rows];
            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    var frameIndex = row * columns + column;
                    var rect = new Rect(
                        column * cellWidth,
                        texture.height - (row + 1) * cellHeight,
                        cellWidth,
                        cellHeight);

                    frames[frameIndex] = Sprite.Create(
                        texture,
                        rect,
                        new Vector2(0.5f, 0.5f),
                        source.pixelsPerUnit,
                        0,
                        SpriteMeshType.FullRect);
                    frames[frameIndex].name = $"{source.name}_{frameIndex + 1}";
                }
            }

            spriteSheetCache[key] = frames;
            return frames;
        }

        private static bool CanUseAnimationSheet(UnitDefinition definition, Sprite source)
        {
            if (definition == null || source == null || source.texture == null || definition.role == UnitRole.Structure)
            {
                return false;
            }

            var texture = source.texture;
            if (texture.width < 384 || texture.height < 256)
            {
                return false;
            }

            var ratio = texture.width / (float)texture.height;
            return texture.width % 3 == 0
                   && texture.height % 2 == 0
                   && Mathf.Abs(ratio - 1.5f) <= 0.04f;
        }

        private static Vector3 ScaleFor(UnitDefinition definition)
        {
            var role = definition != null ? definition.role : UnitRole.Soldier;
            var visualSizeMultiplier = VisualSizeMultiplierFor(definition);
            return role switch
            {
                UnitRole.Elite => new Vector3(0.72f, 0.72f, 1f) * VisualScaleMultiplier * visualSizeMultiplier,
                UnitRole.Hero => new Vector3(0.9f, 0.9f, 1f) * VisualScaleMultiplier * visualSizeMultiplier,
                UnitRole.Structure => new Vector3(0.42f, 0.42f, 1f) * VisualScaleMultiplier,
                UnitRole.Monster => new Vector3(0.56f, 0.56f, 1f) * VisualScaleMultiplier * visualSizeMultiplier,
                UnitRole.Boss => new Vector3(1.12f, 1.12f, 1f) * VisualScaleMultiplier * visualSizeMultiplier,
                _ => new Vector3(0.50f, 0.50f, 1f) * VisualScaleMultiplier * visualSizeMultiplier
            };
        }

        private static float VisualSizeMultiplierFor(UnitDefinition definition)
        {
            if (definition == null)
            {
                return 1.5f;
            }

            if (definition.role == UnitRole.Structure)
            {
                return 1f;
            }

            if (definition.id == "boss_chaos_lord")
            {
                return 4f;
            }

            if (definition.id is "boss_black_wind" or "boss_bone_queen")
            {
                return 3f;
            }

            return definition.role switch
            {
                UnitRole.Boss => 2f,
                _ => 1.5f
            };
        }

    }
}
