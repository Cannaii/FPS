using UnityEngine;
namespace cowsins
{
    public class HurtTrigger : Trigger
    {
        [SerializeField] private float damage;
        [SerializeField] private float cooldown = 1f;

        private float timer;

        private void Awake() => timer = 0;

        public override void TriggerStay(Collider other)
        {
            if (timer <= 0)
            {
                if (other.TryGetComponent(out IDamageable damageable))
                    damageable.Damage(damage, false);

                timer = cooldown;
            }

            timer -= Time.deltaTime;
        }
    }
}