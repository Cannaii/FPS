using UnityEngine;
using System.Collections;

namespace cowsins
{
    public class Lootbox : Interactable
    {
        [Title("LOOTBOX", upMargin: 8),SerializeField, Min(0)] private int price;

        [SerializeField] private GameObject[] loot;

        [SerializeField] private float delayToReceiveLoot;

        [SerializeField] private float minSpawnAngle, maxSpawnAngle, spawnDistance;

        [SerializeField] private bool directionDependsOnPlayerPosition;

        private Animation anim;

        private AudioSource audioSource;

        private string baseInteractText;
        private void Start()
        {
            anim = GetComponent<Animation>();
            audioSource = GetComponent<AudioSource>();
            baseInteractText = interactText;
            if (price != 0) interactText += $" [{price}]";
        }
        public override void Highlight()
        {
            if (price == 0) return;

            if (CoinManager.Instance.CheckIfEnoughCoins(price))
                interactText = baseInteractText + "<color=#" + ColorUtility.ToHtmlStringRGB(Color.green) + ">" + $" [{price}]" + "</color>";
            else interactText = "<color=#" + ColorUtility.ToHtmlStringRGB(Color.red) + ">" + $" [{price}]" + "</color>";
        }
        public override void Interact(Transform player)
        {
            if (price != 0 && CoinManager.Instance.useCoins && CoinManager.Instance.CheckIfEnoughCoins(price) || price == 0)
            {
                base.Interact(player);
                StartCoroutine(GetLoot(player));
            }
        }

        private IEnumerator GetLoot(Transform player)
        {
            if (price != 0)
            {
                CoinManager.Instance.RemoveCoins(price, false);
                UIEvents.onCoinsChange?.Invoke(CoinManager.Instance.coins, false);
            }
            yield return new WaitForSeconds(delayToReceiveLoot);
            GameObject lootObject = null;

            lootObject = loot[Random.Range(0, loot.Length)];

            SpawnSelectedLoot(lootObject, player);

            anim.Play();
            audioSource.Play();

            Destroy(GetComponent<Lootbox>());
            gameObject.layer = LayerMask.NameToLayer("Default");
            StopAllCoroutines();
        }

        private void SpawnSelectedLoot(GameObject loot, Transform player)
        {
            float spawnAngle = Random.Range(minSpawnAngle, maxSpawnAngle);
            Quaternion spawnRotation = Quaternion.Euler(0f, spawnAngle, 0f); // Rotate around the y-axis
            player = GameObject.FindGameObjectWithTag("Player").transform;
            Vector3 spawnDirection = directionDependsOnPlayerPosition ? (player.position - transform.position).normalized * spawnDistance : spawnRotation * -transform.right;

            Vector3 spawnPosition = transform.position + spawnDirection * spawnDistance;

            var instantiatedLoot = Instantiate(loot, spawnPosition, spawnRotation);
            if (instantiatedLoot.TryGetComponent<Identifiable>(out var identifiable))
            {
                identifiable.GenerateUniqueID();
#if SAVE_LOAD_ADD_ON
                identifiable.SaveInstance();
#endif
            }
        }

#if SAVE_LOAD_ADD_ON
        // Only one possible interaction state: Open
        public override void LoadedState()
        {
            anim.Play();
            Destroy(GetComponent<Lootbox>());
            // To avoid the InteractManager from detecting it as an Interactable
            gameObject.layer = LayerMask.NameToLayer("Default");
        }
#endif

        private void OnDrawGizmosSelected()
        {
            if (directionDependsOnPlayerPosition) return;

            Gizmos.color = Color.blue;

            Vector3 forward = -transform.right * spawnDistance;

            Quaternion minRotation = Quaternion.Euler(0f, minSpawnAngle, 0f);
            Vector3 minDirection = minRotation * forward;

            Quaternion maxRotation = Quaternion.Euler(0f, maxSpawnAngle, 0f);
            Vector3 maxDirection = maxRotation * forward;

            Gizmos.DrawLine(transform.position, transform.position + minDirection);
            Gizmos.DrawLine(transform.position, transform.position + maxDirection);
        }
    }
}