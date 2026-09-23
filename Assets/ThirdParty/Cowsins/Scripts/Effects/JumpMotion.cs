using UnityEngine;
using System.Collections;

namespace cowsins
{
    public class JumpMotion : MonoBehaviour
    {
        [SerializeField] private PlayerDependencies playerDependencies;
        [SerializeField] private AnimationCurve jumpMotion, groundedMotion;
        [SerializeField] private float distance, rotationAmount;
        [SerializeField, Min(1)] private float evaluationSpeed;

        private Coroutine motionCoroutine;

        private void OnEnable()
        {
            playerDependencies.PlayerMovementEvents.Events.OnJump.AddListener(OnJump);
            playerDependencies.PlayerMovementEvents.Events.OnLand.AddListener(OnLand);
        }

        private void OnJump()
        {
            if (motionCoroutine != null) StopCoroutine(motionCoroutine);
            motionCoroutine = StartCoroutine(ApplyMotion(jumpMotion));
        }

        private void OnLand()
        {
            if (motionCoroutine != null) StopCoroutine(motionCoroutine);
            motionCoroutine = StartCoroutine(ApplyMotion(groundedMotion));
        }

        private IEnumerator ApplyMotion(AnimationCurve motionCurve)
        {
            float motion = 0;

            while (motion < 1f)
            {
                motion += Time.deltaTime * evaluationSpeed;
                float evaluatedMotion = motionCurve.Evaluate(motion);

                transform.localPosition = Vector3.Lerp(transform.localPosition, new Vector3(0, evaluatedMotion, 0) * distance, motion);
                transform.localRotation = Quaternion.Lerp(transform.localRotation, Quaternion.Euler(new Vector3(evaluatedMotion * rotationAmount, 0, 0)), motion);

                yield return null;
            }
        }
    }
}
