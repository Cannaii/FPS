using System;
using System.Collections.Generic;

namespace cowsins
{
    public class ModifiableStat
    {
        private float _baseValue;
        private float _lastCalculatedValue;
        private bool _isDirty = true;
        private readonly List<StatModifier> _statModifiers;
        
        public IReadOnlyList<StatModifier> StatModifiers => _statModifiers;
        
        public float BaseValue
        {
            get => _baseValue;
            set
            {
                _baseValue = value;
                _isDirty = true;
            }
        }

        public ModifiableStat(float baseValue)
        {
            _baseValue = baseValue;
            _statModifiers = new List<StatModifier>();
        }

        public float Value
        {
            get
            {
                if (!_isDirty) return _lastCalculatedValue;
                _lastCalculatedValue = CalculateFinalValue();
                _isDirty = false;
                return _lastCalculatedValue;
            }
        }

        public void AddModifier(StatModifier modifier)
        {
            _statModifiers.Add(modifier);
            _isDirty = true;
        }

        public bool RemoveModifierFromSource(object source)
        {
            bool removed = _statModifiers.RemoveAll(mod => mod.Source == source) > 0;
            if (removed) _isDirty = true;
            return removed;
        }

        private float CalculateFinalValue()
        {
            float finalValue = _baseValue;
            float sumPercentAdd = 0;

            foreach (StatModifier mod in _statModifiers)
            {
                switch (mod.Type)
                {
                    case StatModifierType.Additive:
                        finalValue += mod.Value;
                        break;
                    case StatModifierType.Multiplicative:
                        sumPercentAdd += mod.Value;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            // Multiplicative modifiers are stacked additively with each other
            return finalValue * (1 + sumPercentAdd);
        }
    }
}
