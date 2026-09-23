/// <summary>
/// This script belongs to cowsins™ as a part of the cowsins´ FPS Engine. All rights reserved. 
/// </summary>
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace cowsins
{
    public class PowerUp : Trigger
    {
        [SerializeField] private bool reappears;

        [SerializeField] protected float reappearTime;

        [SerializeField] private Image Timer;

        [HideInInspector] public bool used;

        protected float timer = 0;

        private Coroutine timerCoroutine;

        private void Start()
        {
            Timer?.gameObject.SetActive(false);
        }

        public override void TriggerStay(Collider other)
        {
            if (used) return;
            
            Interact(other.GetComponent<PlayerDependencies>());

#if SAVE_LOAD_ADD_ON
            SaveTrigger();
#endif

            if (!reappears)
            {
                Destroy(this.gameObject);
            }
            else
            {
                used = true;
                Timer?.gameObject.SetActive(true);
                if (timerCoroutine != null) StopCoroutine(timerCoroutine); // Stop any existing coroutine
                timerCoroutine = StartCoroutine(StartTimerCoroutine());
            } 
        }

        private IEnumerator StartTimerCoroutine()
        {
            float timer = reappearTime;

            while (timer > 0)
            {
                timer -= Time.deltaTime;
                if (Timer != null)
                    Timer.fillAmount = (reappearTime - timer) / reappearTime;

                yield return null;
            }

            used = false;
            Timer?.gameObject.SetActive(false);
        }

        public virtual void Interact(PlayerDependencies player)
        {
            // Override this
        }

#if SAVE_LOAD_ADD_ON
        // If this power up was triggered and is not supposed to reappear, destroy it.
        public override void LoadedState()
        {
            if (triggered && !reappears) Destroy(this.gameObject);
        }
#endif
    }
}