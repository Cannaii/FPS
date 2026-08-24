using UnityEngine;
using UnityEngine.SceneManagement;

namespace cowsins
{
    public class MainMenuManager : MonoBehaviour
    {
        [System.Serializable]
        public class MainMenuSection
        {
            public string sectionName;
            public CanvasGroup section;
        }
        public static MainMenuManager Instance { get; private set; }

        [SerializeField, Header("Sections")] private MainMenuSection[] mainMenuSections;

        private CanvasGroup objectToLerp;

        private AudioSource audioSource;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            mainMenuSections[0].section.gameObject.SetActive(true);
            mainMenuSections[0].section.alpha = 1;

            // We want to skip the first item
            for (int i = 1; i < mainMenuSections.Length; i++)
            {
                mainMenuSections[i].section.gameObject.SetActive(false);
                mainMenuSections[i].section.alpha = 0;
            }

            audioSource = GetComponent<AudioSource>();
        }

        private void Update()
        {
            if (!objectToLerp || objectToLerp?.alpha >= 1) return;
            objectToLerp.gameObject.SetActive(true);
            objectToLerp.alpha += Time.deltaTime * 3;
        }


        public void SetObjectToLerp(CanvasGroup To) => objectToLerp = To;

        public void ChangeScene(int scene) => SceneManager.LoadScene(scene);

        public void LoadScene(int sceneIndex)
        {
            SceneManager.LoadSceneAsync(sceneIndex);
        }
    }
}
