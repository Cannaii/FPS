using AFPS.Simulation.Weapons;
using UnityEngine;

namespace AFPS.NetCode.Weapons
{
    /// <summary>由服务器接受射击请求后生成的唯一权威射击描述。</summary>
    public readonly struct AuthoritativeShot
    {
        public readonly uint EntityId;
        public readonly uint ShotSequence;
        public readonly uint ServerTick;
        public readonly uint RewindTick;
        public readonly ushort WeaponId;
        public readonly WeaponFireMode FireMode;
        public readonly Vector3 Origin;
        public readonly Vector3 Direction;
        public readonly float Damage;
        public readonly float Range;
        public readonly float ProjectileSpeed;

        public AuthoritativeShot(uint entityId, uint shotSequence, uint serverTick, in WeaponSimulationConfig config, Vector3 origin, Vector3 direction, uint rewindTick = 0)
        {
            EntityId = entityId;
            ShotSequence = shotSequence;
            ServerTick = serverTick;
            RewindTick = rewindTick == 0 ? serverTick : rewindTick;
            WeaponId = config.WeaponId;
            FireMode = config.FireMode;
            Origin = origin;
            Direction = direction;
            Damage = config.Damage;
            Range = config.Range;
            ProjectileSpeed = config.ProjectileSpeed;
        }

        public AuthoritativeShot(uint entityId, uint shotSequence, uint serverTick, ushort weaponId, WeaponFireMode fireMode, Vector3 origin, Vector3 direction, float damage, float range, float projectileSpeed, uint rewindTick = 0)
        {
            EntityId = entityId;
            ShotSequence = shotSequence;
            ServerTick = serverTick;
            RewindTick = rewindTick == 0 ? serverTick : rewindTick;
            WeaponId = weaponId;
            FireMode = fireMode;
            Origin = origin;
            Direction = direction;
            Damage = damage;
            Range = range;
            ProjectileSpeed = projectileSpeed;
        }
    }
}
