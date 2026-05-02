using System;
using System.Collections.Generic;
using UnityEngine;

namespace XTD.Content
{
    [Serializable]
    public sealed class CardUnitSpawn
    {
        public UnitDefinition unit;
        public int count = 1;
        public float spacing = 0.35f;
        public float yJitter = 0.25f;
    }

    [CreateAssetMenu(menuName = "X-TD/Content/Card Definition", fileName = "CardDefinition")]
    public sealed class CardDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
        public CardType type = CardType.Soldier;
        public CardRarity rarity = CardRarity.Common;
        public int level = 1;
        public int cost = 1;
        public CardReleaseRule releaseRule = CardReleaseRule.PlayerSide;
        [TextArea] public string description;
        public Sprite art;
        public List<CardUnitSpawn> unitSpawns = new();
        public List<BattleEffectDefinition> effects = new();

        public bool CanReceiveMorale =>
            type == CardType.Soldier || type == CardType.EliteSoldier || type == CardType.Hero;

        public int CommandCost()
        {
            var total = 0;
            foreach (var spawn in unitSpawns)
            {
                if (spawn?.unit == null)
                {
                    continue;
                }

                if (spawn.unit.role != UnitRole.Structure)
                {
                    continue;
                }

                total += Mathf.Max(0, spawn.count);
            }

            return total;
        }
    }
}
