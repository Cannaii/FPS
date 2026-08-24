using UnityEngine;

namespace cowsins
{
    public interface IBullet
    {
        float Speed { get; set; }
        float Damage { get; set; }
        Vector3 Destination { get; set; }
        bool Gravity { get; set; }
        Transform Player { get; set; }
        bool HurtsPlayer { get; set; }
        bool ExplosionOnHit { get; set; }
        GameObject ExplosionVFX { get; set; }
        float ExplosionRadius { get; set; }
        float ExplosionForce { get; set; }
        float CriticalMultiplier { get; set; }
        float Duration { get; set; }
    }
}