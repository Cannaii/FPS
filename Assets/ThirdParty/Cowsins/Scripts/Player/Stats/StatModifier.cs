namespace cowsins
{
    public class StatModifier
    {
        public readonly float Value;
        public readonly StatModifierType Type;
        public readonly object Source; // Used to identify and remove specific buffs

        public StatModifier(float value, StatModifierType type, object source = null)
        {
            Value = value;
            Type = type;
            Source = source;
        }
    }
}
