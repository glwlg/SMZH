using System;
using System.Collections.Generic;
using UnityEngine;

namespace XTD.Content
{
    [Serializable]
    public sealed class EnemySpawnEntry
    {
        public UnitDefinition unit;
        public int count = 1;
        public float interval = 1.2f;
    }

    [CreateAssetMenu(menuName = "神魔镇荒/Content/Encounter Definition", fileName = "EncounterDefinition")]
    public sealed class EncounterDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
        public MapNodeType nodeType = MapNodeType.NormalMonster;
        public float playerBaseMaxHp = 100f;
        public float enemyBaseMaxHp = 120f;
        public float enemySpawnInterval = 2.5f;
        public int rewardGold = 20;
        public List<EnemySpawnEntry> enemySpawns = new();
        public UnitDefinition coreEnemy;
        public EncounterPressurePattern pressurePattern = EncounterPressurePattern.None;
    }
}
