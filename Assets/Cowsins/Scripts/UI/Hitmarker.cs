using UnityEngine;
using UnityEngine.UI;

namespace cowsins
{
    public class Hitmarker : MonoBehaviour
    {

        [SerializeField] private AudioClip hitmarkerSoundEffect;

        [SerializeField] private Animator animator;

        [SerializeField] private Image image;

        public void Play(bool headshot)
        {
            SoundManager.Instance.PlaySound(hitmarkerSoundEffect, .08f, .15f, true);
            animator.SetTrigger("hit");

            image.color = headshot ? new Color(1, 0, 0) : new Color(1, 1, 1);
        }
    }
}