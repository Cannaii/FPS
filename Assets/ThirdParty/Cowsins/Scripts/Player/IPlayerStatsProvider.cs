using System;
using UnityEngine.Events;

namespace cowsins
{
    // Implemented by PlayerStats and required by PlayerDependencies
    public interface IPlayerStatsProvider
    {
        float Health { get; }
        float MaxHealth { get; }
        float Shield { get; }
        float MaxShield { get; }
        bool IsDead { get; }
        bool FreezePlayerOnDeath { get; }
        void Heal(float amount);
        bool IsFullyHealed();

        void AddOnDieListener(Action callback);
        void RemoveOnDieListener(Action callback);
    }

    public interface IPlayerStatsEventsProvider
    {
        PlayerStatsEvents Events { get; }
    }

    public class PlayerStatsEvents
    {
        public UnityEvent<IPlayerStatsProvider> OnInitializeHealth = new UnityEvent<IPlayerStatsProvider>();
        public UnityEvent<float, float, bool> OnHealthChanged = new UnityEvent<float, float, bool>();
    }
}