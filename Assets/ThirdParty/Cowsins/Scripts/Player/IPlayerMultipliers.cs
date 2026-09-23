using System.Collections.Generic;

namespace cowsins
{
    public interface IPlayerMultipliers
    {
        IReadOnlyList<PlayerMultipliers.TemporaryModifierData> ActiveTemporaryModifiers { get; }
        
        ModifiableStat DamageMultiplier { get; }
        ModifiableStat HealMultiplier { get; }
        ModifiableStat WeightMultiplier { get; }

        void AddTemporaryModifier(ModifiableStat stat, StatModifier modifier, float duration);
        void RemoveModifierFromSource(object source);
    }
}