using System.Collections.Generic;
using UnityEngine;
using XTD.Content;
using XTD.Presentation;

namespace XTD.Battle
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class BattleUnit : MonoBehaviour
    {
        private readonly List<TimedUnitModifier> modifiers = new();
        private BattleController controller;
        private SpriteRenderer spriteRenderer;
        private Transform healthBarRoot;
        private SpriteRenderer healthBarBack;
        private SpriteRenderer healthBarFill;
        private float attackCooldown;
        private float productionCooldown;
        private float shield;
        private Vector3 baseScale;
        private Color baseColor;
        private float animationSeed;
        private float movementSignal;
        private float attackPulse;
        private float hitFlash;

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
            CurrentHp = definition.maxHp;
            shield = 0f;
            attackCooldown = Random.Range(0f, definition.attackInterval * 0.4f);
            productionCooldown = definition.ProducesUnits ? definition.productionInterval : 0f;
            modifiers.Clear();
            transform.position = position;
            gameObject.name = $"{faction}_{definition.displayName}";
            gameObject.SetActive(true);
            transform.localRotation = Quaternion.identity;
            baseScale = ScaleFor(definition.role);
            transform.localScale = baseScale;
            animationSeed = Random.Range(0f, 100f);
            movementSignal = 0f;
            attackPulse = 0f;
            hitFlash = 0f;

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            spriteRenderer.sprite = definition.art != null ? definition.art : RuntimeSpriteFactory.UnitSprite;
            baseColor = definition.tint;
            spriteRenderer.color = baseColor;
            spriteRenderer.flipX = false;
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

            CurrentHp = Mathf.Min(Definition.maxHp, CurrentHp + amount);
            UpdateHealthBar();
        }

        public void AddShield(float amount)
        {
            shield += Mathf.Max(0f, amount);
        }

        public void AddModifier(EffectType type, float value, float duration)
        {
            modifiers.Add(new TimedUnitModifier(type, value, duration));
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

            return Definition.attack * Mathf.Max(0.1f, multiplier);
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

        private void TickModifiers(float deltaTime)
        {
            for (var i = modifiers.Count - 1; i >= 0; i--)
            {
                modifiers[i].remaining -= deltaTime;
                if (modifiers[i].remaining <= 0f)
                {
                    modifiers.RemoveAt(i);
                }
            }
        }

        private bool IsInRange(Vector3 targetPosition)
        {
            return Vector2.Distance(transform.position, targetPosition) <= Definition.range;
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

            var step = Definition.moveSpeed * deltaTime;
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

            productionCooldown = Definition.productionInterval;
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
            controller.ReleaseUnit(this);
        }

        private void EnsureHealthBar()
        {
            if (healthBarRoot != null)
            {
                return;
            }

            var root = new GameObject("生命条").transform;
            root.SetParent(transform, false);
            root.localPosition = new Vector3(0f, 0.7f, -0.02f);
            healthBarRoot = root;

            healthBarBack = CreateHealthBarPart("底色", new Color(0.05f, 0.06f, 0.08f, 0.88f), 39);
            healthBarFill = CreateHealthBarPart("血量", Color.green, 40);
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
            if (healthBarRoot == null || Definition == null)
            {
                return;
            }

            var percent = Mathf.Clamp01(CurrentHp / Mathf.Max(1f, Definition.maxHp));
            healthBarBack.transform.localPosition = Vector3.zero;
            healthBarBack.transform.localScale = new Vector3(0.78f, 0.08f, 1f);
            healthBarFill.transform.localPosition = new Vector3(-0.39f + 0.39f * percent, 0f, -0.01f);
            healthBarFill.transform.localScale = new Vector3(0.78f * percent, 0.055f, 1f);
            healthBarFill.color = Color.Lerp(new Color(1f, 0.18f, 0.12f), new Color(0.28f, 1f, 0.36f), percent);
        }

        private void AnimatePresentation(float deltaTime)
        {
            movementSignal = Mathf.MoveTowards(movementSignal, 0f, deltaTime * 6f);
            attackPulse = Mathf.MoveTowards(attackPulse, 0f, deltaTime * 5.5f);
            hitFlash = Mathf.MoveTowards(hitFlash, 0f, deltaTime * 7f);

            var isStructure = Definition.role == UnitRole.Structure;
            var frequency = isStructure ? 2.1f : Mathf.Lerp(2.4f, 9.5f, movementSignal);
            var wave = Mathf.Sin(Time.time * frequency + animationSeed);
            var idleBreath = isStructure ? 0.022f : 0.028f;
            var walkSquash = movementSignal * 0.045f;
            var punch = attackPulse * (isStructure ? 0.035f : 0.12f);

            var xScale = baseScale.x * (1f + wave * idleBreath - wave * walkSquash + punch);
            var yScale = baseScale.y * (1f + wave * idleBreath + wave * walkSquash * 0.65f - punch * 0.35f);
            transform.localScale = new Vector3(xScale, yScale, baseScale.z);

            var tilt = isStructure ? 0f : wave * movementSignal * 4.5f;
            transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

            spriteRenderer.color = Color.Lerp(baseColor, Color.white, hitFlash);
            UpdateSortingOrder();
        }

        private void UpdateSortingOrder()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            spriteRenderer.sortingOrder = 16 + Mathf.RoundToInt((5.2f - transform.position.y) * 3f);
            if (healthBarBack != null)
            {
                healthBarBack.sortingOrder = spriteRenderer.sortingOrder + 20;
            }

            if (healthBarFill != null)
            {
                healthBarFill.sortingOrder = spriteRenderer.sortingOrder + 21;
            }
        }

        private static Vector3 ScaleFor(UnitRole role)
        {
            return role switch
            {
                UnitRole.Elite => new Vector3(0.72f, 0.72f, 1f),
                UnitRole.Hero => new Vector3(0.9f, 0.9f, 1f),
                UnitRole.Structure => new Vector3(0.86f, 0.86f, 1f),
                UnitRole.Monster => new Vector3(0.56f, 0.56f, 1f),
                UnitRole.Boss => new Vector3(1.12f, 1.12f, 1f),
                _ => new Vector3(0.50f, 0.50f, 1f)
            };
        }
    }
}
