using UnityEngine;

namespace cowsins
{
    public class WeaponContext
    {
        // Core References 
        public Transform Transform { get; set; }
        public Transform WeaponHolder { get; set; }
        public InputManager InputManager { get; set; }
        public PlayerDependencies Dependencies { get; set; }
        public Transform[] firePoint { get; set; }
        public MonoBehaviour CoroutineRunner { get; set; }
        public AudioSource AudioSource { get; set; }

        public WeaponControllerSettings Settings { get; set; }

        // Globals
        public bool CanShoot;
    }
}