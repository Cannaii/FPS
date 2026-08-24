using System.Collections.Generic;
using UnityEngine;

namespace cowsins
{
    public class PlayerMultipliers : MonoBehaviour, IPlayerMultipliers
    {
        public class TemporaryModifierData
        {
            public ModifiableStat Stat;
            public StatModifier Modifier;
            public float ExpirationTime;
        }
        
        private List<TemporaryModifierData> activeTemporaryModifiers = new List<TemporaryModifierData>();
        public IReadOnlyList<TemporaryModifierData> ActiveTemporaryModifiers => activeTemporaryModifiers;
        
        private ModifiableStat damageMultiplier = new ModifiableStat(1);
        private ModifiableStat healMultiplier = new ModifiableStat(1);
        private ModifiableStat playerWeightMultiplier = new ModifiableStat(1);
        
        public ModifiableStat DamageMultiplier => damageMultiplier;
        public ModifiableStat HealMultiplier => healMultiplier;
        public ModifiableStat WeightMultiplier => playerWeightMultiplier;
        
        private void Awake()
        {
            damageMultiplier = new ModifiableStat(1);
            healMultiplier = new ModifiableStat(1);
            playerWeightMultiplier = new ModifiableStat(1);
        }

        public void AddTemporaryModifier(ModifiableStat stat, StatModifier modifier, float duration)
        {
            float expirationTime = Time.time + duration;
            
            // Check if this specific modifier (by Source) is already active
            foreach (TemporaryModifierData modifierData in activeTemporaryModifiers)
            {
                if (modifierData.Modifier.Source != modifier.Source) 
                    continue;
                // Refresh the duration
                modifierData.ExpirationTime = expirationTime;
                return;
            }

            // Not found, add as a new temporary modifier
            stat.AddModifier(modifier);
            activeTemporaryModifiers.Add(new TemporaryModifierData 
            { 
                Stat = stat, 
                Modifier = modifier, 
                ExpirationTime = expirationTime 
            });
        }

        public void RemoveModifierFromSource(object source)
        {
            // Iterate backwards to safely remove while iterating
            for (int i = activeTemporaryModifiers.Count - 1; i >= 0; i--)
            {
                if (activeTemporaryModifiers[i].Modifier.Source != source) 
                    continue;
                activeTemporaryModifiers[i].Stat.RemoveModifierFromSource(source);
                activeTemporaryModifiers.RemoveAt(i);
            }
        }
        
        private void Update()
        {
            if (activeTemporaryModifiers.Count == 0) return;

            // Iterate backwards to check expirations
            for (int i = activeTemporaryModifiers.Count - 1; i >= 0; i--)
            {
                if (Time.time < activeTemporaryModifiers[i].ExpirationTime) 
                    continue;
                activeTemporaryModifiers[i].Stat.RemoveModifierFromSource(activeTemporaryModifiers[i].Modifier.Source);
                activeTemporaryModifiers.RemoveAt(i);
            }
        }
    }
}
