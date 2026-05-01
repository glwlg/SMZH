using XTD.Content;

namespace XTD.Battle
{
    public sealed class TimedUnitModifier
    {
        public EffectType type;
        public float value;
        public float remaining;

        public TimedUnitModifier(EffectType type, float value, float duration)
        {
            this.type = type;
            this.value = value;
            remaining = duration;
        }
    }
}
