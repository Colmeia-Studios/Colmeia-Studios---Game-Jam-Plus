using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ColmeiaStudios.SoMaisUmPedido
{
    public enum AudioCue
    {
        ButtonClick, IngredientPlaced, EmptyMealboxPickup, DayEnd, Victory, Defeat,
        Smartphone, Notification, ChecklistComplete, BackpackClose, DeliveryPickup
    }

    /// <summary>Persistent mixer and audio playback owner. Scene buttons are subscribed once per scene.</summary>
    public sealed class AudioManager : MonoBehaviour
    {
        private const string MusicKey = "SoMaisUmPedido.MusicVolume";
        private const string SfxKey = "SoMaisUmPedido.SfxVolume";
        private const float MinimumDb = -80f;
        public static AudioManager Instance { get; private set; }

        [Header("Mixer")]
        [SerializeField] private AudioMixer audioMixer;
        [SerializeField] private string musicParameter = "MusicVolume";
        [SerializeField] private string sfxParameter = "SFXVolume";
        [Header("Sources and music")]
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioClip backgroundMusic;
        [Header("Effects")]
        [SerializeField] private AudioClip buttonClick;
        [SerializeField] private AudioClip ingredientPlaced;
        [SerializeField] private AudioClip emptyMealboxPickup;
        [SerializeField] private AudioClip dayEnd;
        [SerializeField] private AudioClip victory;
        [SerializeField] private AudioClip defeat;
        [SerializeField] private AudioClip smartphone;
        [SerializeField] private AudioClip notification;
        [SerializeField] private AudioClip checklistComplete;
        [SerializeField] private AudioClip backpackClose;
        [SerializeField] private AudioClip deliveryPickup;

        private readonly HashSet<Button> subscribedButtons = new HashSet<Button>();
        public float MusicVolume { get; private set; } = 0.8f;
        public float SfxVolume { get; private set; } = 0.8f;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadVolumes();
            if (audioMixer == null) Debug.LogWarning("AudioManager: assign an AudioMixer to control saved music/SFX volumes.", this);
            if (musicSource == null) Debug.LogWarning("AudioManager: assign the looping music AudioSource.", this);
            if (sfxSource == null) Debug.LogWarning("AudioManager: assign the one-shot SFX AudioSource.", this);
            if (musicSource != null && backgroundMusic != null)
            {
                musicSource.loop = true;
                if (musicSource.clip != backgroundMusic) musicSource.clip = backgroundMusic;
                if (!musicSource.isPlaying) musicSource.Play();
            }
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void Start() => SubscribeSceneButtons();

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            UnsubscribeButtons();
            if (Instance == this) Instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => SubscribeSceneButtons();

        private void SubscribeSceneButtons()
        {
            UnsubscribeButtons();
            Button[] buttons = Resources.FindObjectsOfTypeAll<Button>();
            Scene activeScene = SceneManager.GetActiveScene();
            foreach (Button button in buttons)
            {
                if (button == null || !button.gameObject.scene.IsValid() ||
                    (button.gameObject.scene != activeScene && button.gameObject.scene.name != "DontDestroyOnLoad"))
                    continue;
                RegisterButton(button);
            }
        }

        public void RegisterButton(Button button)
        {
            if (button == null || !subscribedButtons.Add(button)) return;
            button.onClick.AddListener(PlayButtonClick);
        }

        private void UnsubscribeButtons()
        {
            foreach (Button button in subscribedButtons)
                if (button != null) button.onClick.RemoveListener(PlayButtonClick);
            subscribedButtons.Clear();
        }

        public void SetMusicVolume(float value)
        {
            MusicVolume = SanitizeVolume(value);
            SetMixerVolume(musicParameter, MusicVolume);
            PlayerPrefs.SetFloat(MusicKey, MusicVolume);
            PlayerPrefs.Save();
        }

        public void SetSfxVolume(float value)
        {
            SfxVolume = SanitizeVolume(value);
            SetMixerVolume(sfxParameter, SfxVolume);
            PlayerPrefs.SetFloat(SfxKey, SfxVolume);
            PlayerPrefs.Save();
        }

        private void PlayButtonClick() => PlaySfx(AudioCue.ButtonClick);
        public void PlaySfx(AudioCue cue) => PlaySfx(GetClip(cue));
        public void PlaySfx(AudioClip clip)
        {
            if (clip != null && sfxSource != null) sfxSource.PlayOneShot(clip);
        }
        public void PlayClickSfx() => PlaySfx(AudioCue.ButtonClick);
        public void PlayVictory() => PlaySfx(AudioCue.Victory);
        public void PlayDefeat() => PlaySfx(AudioCue.Defeat);
        public void PlayBackpackClosed() => PlaySfx(AudioCue.BackpackClose);
        public void PlayMusic(AudioClip clip)
        {
            if (musicSource == null || clip == null || (musicSource.isPlaying && musicSource.clip == clip)) return;
            musicSource.clip = clip;
            musicSource.loop = true;
            musicSource.Play();
        }

        private void LoadVolumes()
        {
            MusicVolume = PlayerPrefs.HasKey(MusicKey) ? SanitizeVolume(PlayerPrefs.GetFloat(MusicKey)) : 0.8f;
            SfxVolume = PlayerPrefs.HasKey(SfxKey) ? SanitizeVolume(PlayerPrefs.GetFloat(SfxKey)) : 0.8f;
            SetMixerVolume(musicParameter, MusicVolume);
            SetMixerVolume(sfxParameter, SfxVolume);
        }

        private void SetMixerVolume(string parameter, float linear)
        {
            if (audioMixer == null || string.IsNullOrWhiteSpace(parameter)) return;
            float db = linear <= 0.0001f ? MinimumDb : Mathf.Max(MinimumDb, 20f * Mathf.Log10(linear));
            if (!audioMixer.SetFloat(parameter, db))
                Debug.LogWarning($"AudioMixer parameter '{parameter}' is missing or not exposed.", this);
        }

        private static float SanitizeVolume(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0.8f : Mathf.Clamp01(value);
        private AudioClip GetClip(AudioCue cue)
        {
            switch (cue)
            {
                case AudioCue.ButtonClick: return buttonClick;
                case AudioCue.IngredientPlaced: return ingredientPlaced;
                case AudioCue.EmptyMealboxPickup: return emptyMealboxPickup;
                case AudioCue.DayEnd: return dayEnd;
                case AudioCue.Victory: return victory;
                case AudioCue.Defeat: return defeat;
                case AudioCue.Smartphone: return smartphone;
                case AudioCue.Notification: return notification;
                case AudioCue.ChecklistComplete: return checklistComplete;
                case AudioCue.BackpackClose: return backpackClose;
                case AudioCue.DeliveryPickup: return deliveryPickup;
                default: return null;
            }
        }
    }
}
