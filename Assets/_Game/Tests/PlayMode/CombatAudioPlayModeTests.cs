using System.Collections;
using System.Linq;
using AFPS.Math;
using AFPS.NetCode.Weapons;
using AFPS.Presentation.Characters;
using AFPS.Presentation.Weapons;
using AFPS.Simulation.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AFPS.Tests.PlayMode
{
    public sealed class CombatAudioPlayModeTests
    {
        private GameObject root;
        private GameObject listenerRoot;
        private AudioListener[] previousListeners;
        private bool[] previousListenerStates;
        private bool previousPause;
        private bool previousRunInBackground;
        private AudioClip sustainedClip;
        private ShotPresentationController view;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Assert.That(Application.isPlaying, Is.True);
            previousListeners = Object.FindObjectsOfType<AudioListener>();
            previousListenerStates = previousListeners.Select(listener => listener.enabled).ToArray();
            foreach (AudioListener listener in previousListeners)
            {
                listener.enabled = false;
            }
            previousPause = AudioListener.pause;
            previousRunInBackground = Application.runInBackground;
            AudioListener.pause = false;
            Application.runInBackground = true;
            listenerRoot = new GameObject("Audio verification listener");
            listenerRoot.AddComponent<AudioListener>();
            root = new GameObject("Combat audio verification");
            view = root.AddComponent<ShotPresentationController>();
            // 测试长片段只用于保持声源忙碌，不替换生产资产或依赖真实声卡的输出采样。
            sustainedClip = AudioClip.Create("Eight second audio fixture", 44100 * 8, 1, 44100, false);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(root);
            Object.Destroy(listenerRoot);
            Object.Destroy(sustainedClip);
            yield return null;
            if (previousListeners != null)
            {
                for (int i = 0; i < previousListeners.Length; i++)
                {
                    if (previousListeners[i] != null)
                    {
                        previousListeners[i].enabled = previousListenerStates[i];
                    }
                }
            }
            AudioListener.pause = previousPause;
            Application.runInBackground = previousRunInBackground;
        }

        private static AuthoritativeShot Shot(uint sequence, uint entity = 2, bool projectile = false)
        {
            var config = projectile
                ? WeaponCatalog.SecondaryFor(WeaponSimulationConfig.DefaultRifle)
                : WeaponSimulationConfig.DefaultRifle;
            return new AuthoritativeShot(entity, sequence, 100, config,
                new FixVec3(Fix.FromInt(3), Fix.One, Fix.FromInt(4)), FixVec3.Forward);
        }

        private void Fire(uint sequence)
        {
            view.Consume(new AuthoritativeShotResult(Shot(sequence), 29, false, 0,
                Fix.Zero, FixVec3.Forward * Fix.FromInt(10)), 1);
        }

        [UnityTest]
        public IEnumerator RemoteShot_UsesWorldSpatialSettingsAndDoesNotReplayDuplicate()
        {
            Fire(1);
            AudioSource source = root.GetComponentInChildren<AudioSource>();
            Assert.That(source, Is.Not.Null);
            Assert.That(source.isPlaying, Is.True);
            Assert.That(source.clip, Is.SameAs(CombatPresentationAssets.FireClip(false)));
            Assert.That(source.transform.position, Is.EqualTo(new Vector3(3f, 1f, 4f)));
            Assert.That(source.spatialBlend, Is.EqualTo(1f));
            Assert.That(source.volume, Is.EqualTo(0.65f));
            Assert.That(source.minDistance, Is.EqualTo(2f));
            Assert.That(source.maxDistance, Is.EqualTo(60f));
            Assert.That(source.rolloffMode, Is.EqualTo(AudioRolloffMode.Linear));
            Assert.That(source.dopplerLevel, Is.Zero);
            Assert.That(source.playOnAwake, Is.False);
            Fire(1);
            Assert.That(view.RemoteMuzzleCount, Is.EqualTo(1));
            Assert.That(root.GetComponentsInChildren<AudioSource>().Length, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Pool_ReusesIdleSourceBeforeAllocationAndBoundsConcurrentVoices()
        {
            Fire(1);
            AudioSource first = root.GetComponentInChildren<AudioSource>();
            first.clip = sustainedClip;
            first.Play();
            Fire(2);
            AudioSource second = root.GetComponentsInChildren<AudioSource>().Single(source => source != first);
            second.clip = sustainedClip;
            second.Play();
            first.Stop();
            Fire(3);
            Assert.That(root.GetComponentsInChildren<AudioSource>().Length, Is.EqualTo(2));
            Assert.That(first.isPlaying, Is.True);
            Assert.That(second.clip, Is.SameAs(sustainedClip));
            Assert.That(second.isPlaying, Is.True);
            for (uint sequence = 4; sequence <= 60; sequence++)
            {
                // 保持每个新占用声源忙碌，确保真正触发24声源耗尽后的覆盖分支。
                foreach (AudioSource source in root.GetComponentsInChildren<AudioSource>())
                {
                    source.clip = sustainedClip;
                    source.Play();
                }
                Fire(sequence);
            }
            Assert.That(root.GetComponentsInChildren<AudioSource>().Length,
                Is.EqualTo(ShotPresentationController.MaximumAudioSources));
            view.CancelInFlight();
            Assert.That(root.GetComponentsInChildren<AudioSource>().All(source => !source.isPlaying), Is.True);
            Assert.That(view.RemoteMuzzleCount, Is.EqualTo(60));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Disable_StopsChildrenAndRejectsExplicitConsumptionUntilEnabled()
        {
            Fire(1);
            AudioSource source = root.GetComponentInChildren<AudioSource>();
            source.clip = sustainedClip;
            source.Play();
            Assert.That(source.isPlaying, Is.True);
            view.enabled = false;
            Assert.That(source.isPlaying, Is.False);
            Assert.That(view.ConsumedResultCount, Is.Zero);
            Fire(2);
            Assert.That(source.isPlaying, Is.False);
            Assert.That(view.ConsumedResultCount, Is.Zero);
            view.enabled = true;
            Fire(3);
            Assert.That(source.isPlaying, Is.True);
            Assert.That(root.GetComponentsInChildren<AudioSource>().Length, Is.EqualTo(1));
            view.Clear();
            Assert.That(source.isPlaying, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator HitCue_IsTwoDimensionalAndSplashCreatesOnlyOneExplosionSound()
        {
            var shot = Shot(1, entity: 1, projectile: true);
            var point = FixVec3.Forward * Fix.FromInt(5);
            view.Consume(new AuthoritativeShotResult(shot, 4, true, 2, Fix.One, point), 1);
            view.Consume(new AuthoritativeShotResult(shot, 4, true, 3, Fix.One, point), 1);
            AudioSource[] sources = root.GetComponentsInChildren<AudioSource>();
            Assert.That(sources.Length, Is.EqualTo(2));
            AudioSource explosion = sources.Single(source => source.clip == CombatPresentationAssets.Load<AudioClip>("Explosion"));
            AudioSource hit = sources.Single(source => source.clip == CombatPresentationAssets.Load<AudioClip>("Hit"));
            Assert.That(explosion.spatialBlend, Is.EqualTo(1f));
            Assert.That(explosion.volume, Is.EqualTo(0.8f));
            Assert.That(explosion.transform.position, Is.EqualTo(new Vector3(0f, 0f, 5f)));
            Assert.That(hit.spatialBlend, Is.Zero);
            Assert.That(hit.volume, Is.EqualTo(0.45f));
            Assert.That(view.ExplosionEffectCount, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Footstep_UsesShorterRangeAndCannotRestartWhileComponentIsDisabled()
        {
            var footstep = root.AddComponent<FootstepAudioView>();
            footstep.Render(Vector3.zero, true, true);
            footstep.Render(Vector3.right * 2f, true, true);
            AudioSource source = root.GetComponentInChildren<AudioSource>();
            Assert.That(source, Is.Not.Null);
            Assert.That(source.isPlaying, Is.True);
            Assert.That(source.spatialBlend, Is.EqualTo(1f));
            Assert.That(source.minDistance, Is.EqualTo(2f));
            Assert.That(source.maxDistance, Is.EqualTo(24f));
            Assert.That(source.volume, Is.EqualTo(0.25f));
            Assert.That(source.dopplerLevel, Is.Zero);
            Assert.That(source.rolloffMode, Is.EqualTo(AudioRolloffMode.Linear));
            footstep.enabled = false;
            footstep.Render(Vector3.right * 4f, true, true);
            footstep.Render(Vector3.right * 6f, true, true);
            Assert.That(footstep.StepCount, Is.Zero);
            Assert.That(source.isPlaying, Is.False);
            footstep.enabled = true;
            footstep.Render(Vector3.zero, true, true);
            footstep.Render(Vector3.right * 2f, true, true);
            Assert.That(footstep.StepCount, Is.EqualTo(1));
            Assert.That(source.isPlaying, Is.True);
            yield return null;
        }
    }
}
