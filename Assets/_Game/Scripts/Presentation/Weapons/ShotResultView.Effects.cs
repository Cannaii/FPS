using System.Collections.Generic;
using AFPS.NetCode.Weapons;
using AFPS.Simulation.Weapons;
using UnityEngine;

namespace AFPS.Presentation.Weapons
{
    public sealed partial class ShotResultView
    {
        public const int MaximumAudioSources = 24;
        private readonly AudioSource[] audioSources = new AudioSource[MaximumAudioSources];
        private readonly HashSet<(uint Entity, uint Shot, byte Stage)> effects =
            new HashSet<(uint, uint, byte)>();
        private readonly Queue<(uint Entity, uint Shot, byte Stage)> effectOrder =
            new Queue<(uint, uint, byte)>();
        private int nextAudio;
        public ulong RemoteMuzzleCount { get; private set; }
        public ulong ExplosionEffectCount { get; private set; }

        private bool BeginEffect(in AuthoritativeShot shot, byte stage)
        {
            var key = (shot.EntityId, shot.ShotSequence, stage);
            if (!effects.Add(key))
            {
                return false;
            }
            effectOrder.Enqueue(key);
            if (effectOrder.Count > 256)
            {
                effects.Remove(effectOrder.Dequeue());
            }
            return true;
        }

        private void ConsumeEffects(in AuthoritativeShotResult result, uint localEntityId)
        {
            bool projectile = result.Shot.FireMode == WeaponFireMode.Projectile;
            bool impact = result.DidHit || result.HitEnvironment;
            bool firing = !projectile || !impact;
            if (firing && BeginEffect(result.Shot, 0) && result.Shot.EntityId != localEntityId)
            {
                RemoteMuzzleCount++;
                Vector3 origin = result.Shot.Origin;
                Vector3 direction = result.Shot.Direction;
                DrawLine(origin, origin + direction * 0.25f, new Color(1f, 0.65f, 0.08f), 0.045f);
                DrawLine(origin - Vector3.up * 0.08f, origin + Vector3.up * 0.08f, Color.yellow, 0.045f);
                PlayAudio(CombatPresentationAssets.FireClip(projectile), origin, true, 0.65f);
            }
            // 一次爆炸可有直接命中、自伤和多个溅射结果，只产生一次爆点和空间音效。
            if (projectile && impact && BeginEffect(result.Shot, 1))
            {
                ExplosionEffectCount++;
                Vector3 center = result.HitPoint;
                Color color = new Color(1f, 0.45f, 0.08f);
                for (int i = 0; i < 12; i++)
                {
                    float angle = i * Mathf.PI / 6f;
                    float next = (i + 1) * Mathf.PI / 6f;
                    DrawLine(center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * 0.4f,
                        center + new Vector3(Mathf.Cos(next), Mathf.Sin(next), 0f) * 0.4f, color, 0.22f);
                }
                PlayAudio(CombatPresentationAssets.Load<AudioClip>("Explosion"), center, true, 0.8f);
            }
            if (result.DidHit && localEntityId != 0 && result.Shot.EntityId == localEntityId
                && result.TargetEntityId != localEntityId && BeginEffect(result.Shot, 2))
            {
                PlayAudio(CombatPresentationAssets.Load<AudioClip>("Hit"), result.HitPoint, false, 0.45f);
            }
        }

        private void PlayAudio(AudioClip clip, Vector3 position, bool spatial, float volume)
        {
            if (clip == null || !Application.isPlaying)
            {
                return;
            }
            int index = nextAudio;
            nextAudio = (nextAudio + 1) % MaximumAudioSources;
            AudioSource source = audioSources[index];
            if (source == null)
            {
                var target = new GameObject("Combat Audio");
                target.transform.SetParent(transform, false);
                source = target.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.dopplerLevel = 0f;
                source.minDistance = 2f; // 米，近距离保持清晰；60 米外衰减至静音。
                source.maxDistance = 60f;
                source.rolloffMode = AudioRolloffMode.Linear;
                audioSources[index] = source;
            }
            source.Stop();
            source.transform.position = position;
            source.clip = clip;
            source.spatialBlend = spatial ? 1f : 0f;
            source.volume = volume;
            source.Play();
        }

        private void ClearEffects()
        {
            effects.Clear();
            effectOrder.Clear();
            RemoteMuzzleCount = 0;
            ExplosionEffectCount = 0;
            foreach (var source in audioSources)
            {
                if (source != null)
                {
                    source.Stop();
                }
            }
        }

        /// <summary>终局撤销在途表现，不清除已经确认的击杀/计数供结算 HUD 使用。</summary>
        public void CancelInFlight()
        {
            expiredProjectiles.Clear();
            foreach (var key in projectiles.Keys)
            {
                expiredProjectiles.Add(key);
            }
            foreach (var key in expiredProjectiles)
            {
                ReleaseProjectile(key);
                RememberCompletedProjectile(key);
            }
            foreach (var source in audioSources)
            {
                if (source != null)
                {
                    source.Stop();
                }
            }
        }
    }
}
