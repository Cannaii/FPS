using UnityEngine;
using UnityEngine.Audio;
using System;
using System.Collections.Generic;


namespace cowsins.Settings
{
    public class GameSettingsManager : MonoBehaviour
    {
        public static GameSettingsManager Instance { get; private set; }

        [SerializeField] private AudioMixer masterMixer;

        private Dictionary<Type, SettingsCategory> categories = new Dictionary<Type, SettingsCategory>();

        public GraphicsSettings Graphics { get; private set; }
        public AudioSettings Audio { get; private set; }
        public ControlSettings Controls { get; private set; }
            
        public event Action OnSettingsLoaded;
        public event Action OnSettingsReset;
        public event Action OnSettingsApplied;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            InitializeCategories();
            LoadAllSettings();
        }

        private void Start()
        {
            ApplyAllSettings();
        }

        private void InitializeCategories()
        {
            Graphics = new GraphicsSettings();
            Audio = new AudioSettings(masterMixer);
            Controls = new ControlSettings();

            RegisterCategory(Graphics);
            RegisterCategory(Audio);
            RegisterCategory(Controls);
        }

        private void RegisterCategory(SettingsCategory category)
        {
            categories[category.GetType()] = category;
        }

        public T GetCategory<T>() where T : SettingsCategory
        {
            return categories.TryGetValue(typeof(T), out var category) ? category as T : null;
        }

        public void SaveAllSettings()
        {
            foreach (var category in categories.Values)
            {
                category.SaveAll();
            }
        }

        public void LoadAllSettings()
        {
            foreach (var category in categories.Values)
            {
                category.LoadAll();
                category.ApplyAll();
            }
            OnSettingsLoaded?.Invoke();
        }

        public void ResetAllSettings()
        {
            foreach (var category in categories.Values)
            {
                category.ResetAll();
                category.ApplyAll();
            }
            SaveAllSettings();
            OnSettingsReset?.Invoke();
        }

        public void ApplyAllSettings()
        {
            foreach (var category in categories.Values)
            {
                category.ApplyAll();
            }
            OnSettingsApplied?.Invoke();
        }

        public void InvokeSettingsApplied() => OnSettingsApplied?.Invoke();

        private void OnApplicationQuit()
        {
            SaveAllSettings();
        }
    }
}
