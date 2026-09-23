using UnityEngine;

namespace AFPS.Presentation.Weapons
{
    /// <summary>纯表现弹丸；不参与碰撞、命中或伤害判定。</summary>
    public sealed class PredictedProjectileVisual : MonoBehaviour
    {
        private Vector3 velocity;
        private float remainingLifetime;

        public void Initialize(Vector3 direction, float speed, float lifetime)
        {
            velocity = direction.normalized * Mathf.Max(0f, speed);
            remainingLifetime = Mathf.Max(0.01f, lifetime);
        }

        private void Update()
        {
            transform.position += velocity * Time.deltaTime;
            remainingLifetime -= Time.deltaTime;
            if (remainingLifetime <= 0f)
            {
                Destroy(gameObject);
            }
        }
    }
}
