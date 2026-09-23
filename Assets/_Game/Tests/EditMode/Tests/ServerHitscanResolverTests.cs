using AFPS.NetCode.Weapons;
using AFPS.Simulation.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace AFPS.Tests.EditMode
{
    public sealed class ServerHitscanResolverTests
    {
        [Test]
        public void Resolve_SelectsNearestLivingCapsuleAndIgnoresShooter()
        {
            AuthoritativeShot shot = Shot(Vector3.zero, Vector3.forward, 100f);
            ServerCombatTarget[] targets =
            {
                new ServerCombatTarget(1, new Vector3(0f, 0f, 2f), 0.5f, 2f, 100f),
                new ServerCombatTarget(3, new Vector3(0f, 0f, 20f), 0.5f, 2f, 100f),
                new ServerCombatTarget(2, new Vector3(0f, 0f, 10f), 0.5f, 2f, 100f)
            };

            Assert.That(ServerHitscanResolver.TryResolve(shot, targets, out ServerCombatTarget hit, out Vector3 point), Is.True);
            Assert.That(hit.EntityId, Is.EqualTo(2));
            Assert.That(point.z, Is.EqualTo(10f).Within(0.01f));
        }

        [Test]
        public void Resolve_MissesDeadOffAxisAndOutOfRangeTargets()
        {
            AuthoritativeShot shot = Shot(Vector3.zero, Vector3.forward, 10f);
            ServerCombatTarget[] targets =
            {
                new ServerCombatTarget(2, new Vector3(0f, 0f, 5f), 0.5f, 2f, 0f),
                new ServerCombatTarget(3, new Vector3(2f, 0f, 5f), 0.5f, 2f, 100f),
                new ServerCombatTarget(4, new Vector3(0f, 0f, 20f), 0.5f, 2f, 100f)
            };

            Assert.That(ServerHitscanResolver.TryResolve(shot, targets, out _, out _), Is.False);
        }

        private static AuthoritativeShot Shot(Vector3 origin, Vector3 direction, float range)
        {
            return new AuthoritativeShot(1, 1, 1, 1, WeaponFireMode.Hitscan, origin, direction.normalized, 20f, range, 0f);
        }
    }
}
