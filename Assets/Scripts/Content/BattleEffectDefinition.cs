using System;
using UnityEngine;

namespace XTD.Content
{
    [Serializable]
    public sealed class BattleEffectDefinition
    {
        public EffectType effectType = EffectType.None;
        public TargetRule targetRule = TargetRule.None;
        public float value;
        public float duration;
        public float radius = 1.5f;
        public string statusId;

        public BattleEffectDefinition Clone()
        {
            return new BattleEffectDefinition
            {
                effectType = effectType,
                targetRule = targetRule,
                value = value,
                duration = duration,
                radius = radius,
                statusId = statusId
            };
        }
    }
}
