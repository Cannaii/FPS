using System.Collections.Generic;
using AFPS.NetCode.Weapons;
using AFPS.Simulation.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace AFPS.Tests.EditMode
{
    public sealed class ServerProjectileSimulationTests
    {
        [Test]
        public void Projectile_AdvancesByServerTicksAndHitsTarget()
        {
            AuthoritativeShot shot = new AuthoritativeShot(1, 5, 10, 2, WeaponFireMode.Projectile, new Vector3(0f, 1f, 0f), Vector3.forward, 30f, 20f, 10f);
            ServerProjectileSimulation simulation = new ServerProjectileSimulation();
            Assert.That(simulation.Spawn(shot), Is.True);
            ServerCombatTarget[] targets = { new ServerCombatTarget(2, new Vector3(0f, 0f, 5f), 0.5f, 2f, 100f) };
            List<AuthoritativeShotResult> impacts = new List<AuthoritativeShotResult>();

            for (int i = 0; i < 10 && impacts.Count == 0; i++)
            {
                simulation.Advance(0.1f, targets, impacts);
            }

            Assert.That(impacts.Count, Is.EqualTo(1));
            Assert.That(impacts[0].TargetEntityId, Is.EqualTo(2));
            Assert.That(simulation.Count, Is.Zero);
        }

        [Test]
        public void Projectile_ExpiresAtMaximumRange()
        {
            AuthoritativeShot shot = new AuthoritativeShot(1, 5, 10, 2, WeaponFireMode.Projectile, Vector3.zero, Vector3.forward, 30f, 2f, 10f);
            ServerProjectileSimulation simulation = new ServerProjectileSimulation();
            simulation.Spawn(shot);
            simulation.Advance(0.3f, new ServerCombatTarget[0], new List<AuthoritativeShotResult>());
            Assert.That(simulation.Count, Is.Zero);
        }
    }
}
