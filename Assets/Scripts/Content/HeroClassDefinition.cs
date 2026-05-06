using System;
using System.Collections.Generic;
using UnityEngine;

namespace XTD.Content
{
    [Serializable]
    public sealed class HeroClassCardTypeModifier
    {
        public CardType type;
        public int costDelta;
        public float rewardWeight = 1f;
    }

    [Serializable]
    public sealed class HeroClassDefinition
    {
        public HeroClassType heroClass;
        public int displayOrder;
        public bool isUnlocked = true;
        public string displayName;
        public string shortStyle;
        [TextArea] public string description;
        public string spriteName;
        public Color panelColor = new(0.08f, 0.12f, 0.14f, 0.92f);

        public int startingGold = 25;
        public float startingHp = 100f;
        public int extraCommand;
        public int extraMaxMana;
        public float extraStartingMana;
        public int startingHandBonus;
        public int moraleThreshold = 5;
        public float spellDamageBonus;
        public float soldierAttackBonus;
        public float eliteAttackBonus;
        public float heroAttackBonus;
        public float structureProductionIntervalMultiplier = 1f;
        public float manaRegenMultiplier = 1f;
        public float effectRadiusMultiplier = 1f;

        public readonly List<string> startingDeckCardIds = new();
        public readonly List<string> neutralCardPoolBaseIds = new();
        public readonly List<string> classCardPoolBaseIds = new();
        public readonly List<HeroClassCardTypeModifier> cardTypeModifiers = new();

        public IReadOnlyList<string> FullCardPoolBaseIds()
        {
            var result = new List<string>();
            AddDistinct(result, neutralCardPoolBaseIds);
            AddDistinct(result, classCardPoolBaseIds);
            return result;
        }

        public int CostModifierFor(CardDefinition card)
        {
            if (card == null)
            {
                return 0;
            }

            var modifier = 0;
            foreach (var rule in cardTypeModifiers)
            {
                if (rule != null && rule.type == card.type)
                {
                    modifier += rule.costDelta;
                }
            }

            return modifier;
        }

        public float RewardWeightFor(CardDefinition card)
        {
            if (card == null)
            {
                return 1f;
            }

            var weight = 1f;
            foreach (var rule in cardTypeModifiers)
            {
                if (rule != null && rule.type == card.type)
                {
                    weight *= Mathf.Max(0.01f, rule.rewardWeight);
                }
            }

            if (classCardPoolBaseIds.Contains(GameContentFactory.BaseCardId(card.id)))
            {
                weight *= 1.08f;
            }

            return weight;
        }

        private static void AddDistinct(List<string> target, IEnumerable<string> source)
        {
            foreach (var id in source)
            {
                if (!string.IsNullOrWhiteSpace(id) && !target.Contains(id))
                {
                    target.Add(id);
                }
            }
        }
    }
}
