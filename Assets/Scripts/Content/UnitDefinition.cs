using UnityEngine;

namespace XTD.Content
{
    [CreateAssetMenu(menuName = "X-TD/Content/Unit Definition", fileName = "UnitDefinition")]
    public sealed class UnitDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
        public Faction faction = Faction.Player;
        public UnitRole role = UnitRole.Soldier;
        public float maxHp = 20f;
        public float attack = 5f;
        public float attackInterval = 1f;
        public float range = 0.75f;
        public float moveSpeed = 1f;
        public int commandCost = 1;
        public float projectileSpeed = 8f;
        public bool blocksMovement = true;
        public Sprite art;
        public Color tint = Color.white;
        public GameObject prefab;
        public UnitDefinition producedUnit;
        public float productionInterval;
        public int productionCount;
        public float productionSpread = 0.4f;

        public bool IsRanged => range > 1.25f;
        public bool ProducesUnits => producedUnit != null && productionInterval > 0f && productionCount > 0;
    }
}
