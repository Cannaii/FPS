using System;
using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace cowsins
{
    public class PauseMenu : MonoBehaviour
    {
        [SerializeField] private PlayerDependencies playerDependencies;
        [SerializeField] private GameObject playerUI;
        [SerializeField] private Selectable firstSelectedItem;
        [SerializeField] private bool disablePlayerUIWhilePaused;
        [SerializeField] private CanvasGroup menu;
        [SerializeField] private float fadeSpeed;
        [SerializeField] private UnityEvent onPauseEvents, onUnpauseEvents;
        private Coroutine fadeCoroutine;

        private IPlayerControlProvider playerControlProvider; // IPlayerControlProvider is implemented in PlayerControl.cs
        private IPlayerStatsProvider playerStatsProvider; // IPlayerStatsProvider is implemented in PlayerStats.cs
        private InputManager inputManager;

        public static PauseMenu Instance { get; private set; }

        /// <summary>
        /// Returns the Pause State of the game
        /// </summary>
        public static bool isPaused { get; private set; }

        public event Action OnPause;
        public event Action OnUnPause;

        private void Start()
        {
            playerControlProvider = playerDependencies.PlayerControl;
            playerStatsProvider = playerDependencies.PlayerStats;
            inputManager = playerDependencies.InputManager;

            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Initially, the game is not paused
            isPaused = false;
            menu.gameObject.SetActive(false);
            menu.alpha = 0;

            if (EventSystem.current == null)
                CowsinsUtilities.LogError("No <b><color=cyan>EventSystem</color></b> object found in the scene. " +
                    "Please create a new Empty GameObject and assign the EventSystem component to it to fix this error.");
        }

        private void OnEnable()
        {
            if (inputManager == null && playerDependencies != null) inputManager = playerDependencies.InputManager;
            if (inputManager != null)
                inputManager.OnTogglePause += TogglePause;
        }

        private void OnDisable()
        {
            if (inputManager != null)
                inputManager.OnTogglePause -= TogglePause;
        }

        private IEnumerator HandlePause()
        {
            if (disablePlayerUIWhilePaused && !playerStatsProvider.IsDead)
                playerUI.SetActive(false);

            menu.gameObject.SetActive(true);
            while (menu.alpha < 1)
            {
                menu.alpha += Time.deltaTime * fadeSpeed;
                yield return null;
            }
            menu.alpha = 1;
        }

        private IEnumerator HandleUnpause()
        {
            playerUI.SetActive(true);
            while (menu.alpha > 0)
            {
                menu.alpha -= Time.deltaTime * fadeSpeed;
                yield return null;
            }
            menu.alpha = 0;
            menu.gameObject.SetActive(false);
        }

        public void TogglePause()
        {
            isPaused = !isPaused;

            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);

            if (isPaused)
            {
                playerControlProvider.LoseControl();
                fadeCoroutine = StartCoroutine(HandlePause());
                OnPause?.Invoke();
                onPauseEvents?.Invoke();
                if(Gamepad.current !=  null)
                    EventSystem.current?.SetSelectedGameObject(firstSelectedItem.gameObject);
            }
            else
                UnPause();
        }

        public void UnPause()
        {
            isPaused = false;
            playerControlProvider.CheckIfCanGrantControl();
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(HandleUnpause());
            OnUnPause?.Invoke();
            onUnpauseEvents?.Invoke();
        }

        public void QuitGame()
        {
            Application.Quit();
        }
    }
}
