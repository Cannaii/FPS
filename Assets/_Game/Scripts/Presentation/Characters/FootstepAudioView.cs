using AFPS.Presentation.Weapons;
using UnityEngine;

namespace AFPS.Presentation.Characters
{
    /// <summary>只消费渲染位移与权威落地状态；不使用墙钟定时循环，断流冻结时不会原地踏步。</summary>
    public sealed class FootstepAudioView : MonoBehaviour
    {
        private const float StepDistance = 1.8f; // 水平米数，每一步；无表面材质分型的基础脚步。
        private const float TeleportDistance = 4f; // 单渲染帧超过此距离只重置基准，不补播脚步。
        private Vector3 previousPosition;
        private bool hasPosition;
        private bool wasGrounded;
        private float distanceSinceStep;
        private AudioSource audioSource;
        private AudioSource jumpAudioSource;
        private float lowestAirVelocity;
        public ulong StepCount { get; private set; }
        public ulong JumpCount { get; private set; }

        public void Render(Vector3 position, bool grounded, bool alive, bool discontinuity = false,
            float verticalSpeed = 0f, bool jumpAudioAllowed = true)
        {
            if (!isActiveAndEnabled)
            {
                // 上层可在同一帧继续采样；停用后不能被显式Render重新播放脚步。
                return;
            }
            if (float.IsNaN(verticalSpeed) || float.IsInfinity(verticalSpeed))
            {
                // 非有限表现采样不能污染后续最低速度基准。
                verticalSpeed = 0f;
            }
            if (!alive || discontinuity || !hasPosition
                || Vector3.Distance(position, previousPosition) > TeleportDistance)
            {
                previousPosition = position;
                hasPosition = alive;
                wasGrounded = grounded;
                lowestAirVelocity = verticalSpeed;
                distanceSinceStep = 0f;
                if (audioSource != null)
                {
                    audioSource.Stop();
                }
                if (jumpAudioSource != null)
                {
                    jumpAudioSource.Stop();
                }
                return;
            }
            // 只读向上速度跃迁：累计增加3米/秒也可识别被插值摊开的空中跳冲量。
            // 走下边缘不会触发；本机排除攀爬/钩索，远端按现有快照速度过滤默认爬梯，首样本/传送不补播。
            bool upwardImpulse = verticalSpeed > 1f
                && (wasGrounded || verticalSpeed > lowestAirVelocity + 3f);
            if (!grounded && jumpAudioAllowed && upwardImpulse)
            {
                PlayJump(position);
                lowestAirVelocity = verticalSpeed;
            }
            else
            {
                lowestAirVelocity = grounded ? 0f : Mathf.Min(lowestAirVelocity, verticalSpeed);
            }
            Vector3 displacement = position - previousPosition;
            displacement.y = 0f;
            previousPosition = position;
            if (grounded && wasGrounded)
            {
                distanceSinceStep += displacement.magnitude;
                if (distanceSinceStep >= StepDistance)
                {
                    // 长帧最多播一次，不能在快速追帧或校正时集中补播一串声音。
                    distanceSinceStep %= StepDistance;
                    PlayStep(position);
                }
            }
            else
            {
                distanceSinceStep = 0f;
            }
            wasGrounded = grounded;
        }

        private void PlayStep(Vector3 position)
        {
            AudioClip clip = CombatPresentationAssets.Load<AudioClip>(StepCount % 2 == 0 ? "Footstep1" : "Footstep2");
            if (clip == null)
            {
                return;
            }
            StepCount++;
            if (!Application.isPlaying)
            {
                return;
            }
            if (audioSource == null)
            {
                var target = new GameObject("Positional Footsteps");
                target.transform.SetParent(transform, false);
                audioSource = target.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 1f;
                audioSource.dopplerLevel = 0f;
                audioSource.minDistance = 2f;
                audioSource.maxDistance = 24f; // 米，近处可辨位，远处优先保留枪声。
                audioSource.rolloffMode = AudioRolloffMode.Linear;
                audioSource.volume = 0.25f;
            }
            audioSource.transform.position = position;
            audioSource.clip = clip;
            audioSource.Play();
        }

        private void PlayJump(Vector3 position)
        {
            var clip = CombatPresentationAssets.Load<AudioClip>("Jump");
            if (clip == null)
            {
                return;
            }

            JumpCount++;
            if (!Application.isPlaying)
            {
                return;
            }

            if (jumpAudioSource == null)
            {
                var target = new GameObject("Positional Jump Audio");
                target.transform.SetParent(transform, false);
                jumpAudioSource = target.AddComponent<AudioSource>();
                jumpAudioSource.playOnAwake = false;
                jumpAudioSource.spatialBlend = 1f;
                jumpAudioSource.dopplerLevel = 0f;
                jumpAudioSource.rolloffMode = AudioRolloffMode.Linear;
                jumpAudioSource.minDistance = 2f; // 米，24米外静音；归一化增益0.35。
                jumpAudioSource.maxDistance = 24f;
                jumpAudioSource.volume = 0.35f;
            }

            jumpAudioSource.transform.position = position;
            jumpAudioSource.clip = clip;
            jumpAudioSource.Play();
        }

        public void Clear()
        {
            hasPosition = false;
            wasGrounded = false;
            distanceSinceStep = 0f;
            StepCount = 0;
            JumpCount = 0;
            if (audioSource != null)
            {
                audioSource.Stop();
            }
            if (jumpAudioSource != null)
            {
                jumpAudioSource.Stop();
            }
        }

        private void OnDisable()
        {
            Clear();
        }
    }
}
