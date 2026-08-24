using UnityEngine;
namespace cowsins
{
    public class TrainingTarget : EnemyHealth
    {
        [SerializeField] private float timeToRevive;

        private Animator animator;
        private CompassElement compassElement;

        public override void Start()
        {
            animator = GetComponent<Animator>();
            compassElement = transform.parent.GetComponent<CompassElement>();
            base.Start();
        }

        public override void Damage(float damage, bool isHeadshot)
        {
            if (isDead) return;
            animator.Play("Target_Hit");
            base.Damage(damage, isHeadshot);
        }
        public override void Die()
        {
            if (isDead) return;
            isDead = true;
            events.OnDeath?.Invoke();
            Invoke("Revive", timeToRevive);

            if (enemyStatusContainer != null) enemyStatusContainer.gameObject.SetActive(false);

            if (showKillFeed) UIEvents.onEnemyKilled.Invoke(_name);

            if (compassElement != null) compassElement.Remove();

            animator.Play("Target_Die");

            SoundManager.Instance.PlaySound(dieSFX, 0, 0, false);
        }
        private void Revive()
        {
            isDead = false;
            animator.Play("Target_Revive");
            health = maxHealth;
            shield = maxShield;

            if (enemyStatusContainer != null) enemyStatusContainer.gameObject.SetActive(true);

            if (compassElement != null) compassElement.Add();

            if (healthBar != null) healthBar.fillAmount = 1;
            if (shieldBar != null) shieldBar.fillAmount = 1;

#if SAVE_LOAD_ADD_ON
            StoreData();
#endif
        }

#if SAVE_LOAD_ADD_ON
        public override void LoadedState()
        {
            if (health <= 0)
                Revive();
                
            healthBar.fillAmount = health / maxHealth;
            shieldBar.fillAmount = shield / maxShield;
        }
#endif
    }
}