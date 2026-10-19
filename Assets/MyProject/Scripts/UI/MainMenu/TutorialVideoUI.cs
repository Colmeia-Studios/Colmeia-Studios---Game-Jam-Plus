using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace ColmeiaStudios.SoMaisUmPedido
{
    /// <summary>
    /// Main-menu tutorial panel. Plays a platform-specific VideoClip and can optionally
    /// continue into character selection when opened from PLAY.
    /// </summary>
    public sealed class TutorialVideoUI : MonoBehaviour
    {
        private const string HasSeenTutorialKey = "HasSeenTutorial";

        [Header("Panel")]
        [SerializeField] private GameObject tutorialPanel;
        [SerializeField] private Button skipButton;
        [SerializeField] private TMP_Text statusText;

        [Header("Playback")]
        [SerializeField] private VideoPlayer videoPlayer;
        [Tooltip("MP4 clip used on Windows Player/Editor.")]
        [SerializeField] private VideoClip windowsTutorialClip;
        [Tooltip("WebM clip used on Linux Player/Editor.")]
        [SerializeField] private VideoClip linuxTutorialClip;

        private Action onClosedContinueToCharacterSelection;
        private bool isOpen;
        private bool isClosing;
        private bool eventsBound;

        public bool IsOpen => isOpen;

        public static bool HasSeenTutorial => PlayerPrefs.GetInt(HasSeenTutorialKey, 0) == 1;

        public static void MarkTutorialSeen()
        {
            if (HasSeenTutorial) return;
            PlayerPrefs.SetInt(HasSeenTutorialKey, 1);
            PlayerPrefs.Save();
        }

        private void Awake()
        {
            if (skipButton != null)
                skipButton.onClick.AddListener(Skip);

            if (videoPlayer != null)
            {
                videoPlayer.playOnAwake = false;
                videoPlayer.isLooping = false;
                BindVideoEvents();
            }

            if (tutorialPanel != null)
                tutorialPanel.SetActive(false);

            ClearStatus();
        }

        private void OnDestroy()
        {
            if (skipButton != null)
                skipButton.onClick.RemoveListener(Skip);
            UnbindVideoEvents();
            StopPlayback();
        }

        private void OnDisable()
        {
            if (isOpen)
                CloseInternal(invokeContinue: false);
        }

        /// <summary>HOW TO PLAY: show tutorial and return to the menu when done.</summary>
        public void OpenForManualReview() => Open(null);

        /// <summary>First PLAY: show tutorial, then invoke <paramref name="onComplete"/>.</summary>
        public void OpenBeforeNewGame(Action onComplete) => Open(onComplete);

        public void Skip() => CloseInternal(invokeContinue: true);

        private void Open(Action continueCallback)
        {
            if (isOpen || isClosing) return;

            onClosedContinueToCharacterSelection = continueCallback;
            // Opening once counts as seen, even if the player skips immediately.
            MarkTutorialSeen();

            if (tutorialPanel == null || videoPlayer == null)
            {
                Debug.LogError("TutorialVideoUI: assign Tutorial Panel and Video Player in the Inspector.", this);
                FinishWithoutPlayback();
                return;
            }

            if (!TryResolvePlatformClip(out VideoClip clip, out string error))
            {
                SetStatus(error);
                Debug.LogError(error, this);
                FinishWithoutPlayback();
                return;
            }

            isOpen = true;
            isClosing = false;
            ClearStatus();
            tutorialPanel.SetActive(true);

            StopPlayback();
            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = false;
            videoPlayer.clip = clip;
            BindVideoEvents();

            try
            {
                videoPlayer.Prepare();
            }
            catch (Exception exception)
            {
                SetStatus("Tutorial video failed to prepare.");
                Debug.LogError($"TutorialVideoUI: prepare failed. {exception.Message}", this);
                FinishWithoutPlayback();
            }
        }

        private void FinishWithoutPlayback() => CloseInternal(invokeContinue: true);

        private void CloseInternal(bool invokeContinue)
        {
            if (isClosing) return;
            isClosing = true;

            StopPlayback();

            if (tutorialPanel != null)
                tutorialPanel.SetActive(false);

            Action continueCallback = onClosedContinueToCharacterSelection;
            onClosedContinueToCharacterSelection = null;
            isOpen = false;
            isClosing = false;

            if (invokeContinue)
                continueCallback?.Invoke();
        }

        private void StopPlayback()
        {
            if (videoPlayer == null) return;
            if (videoPlayer.isPlaying || videoPlayer.isPaused)
                videoPlayer.Stop();
        }

        private void BindVideoEvents()
        {
            if (videoPlayer == null || eventsBound) return;
            videoPlayer.prepareCompleted += OnPrepareCompleted;
            videoPlayer.loopPointReached += OnLoopPointReached;
            videoPlayer.errorReceived += OnErrorReceived;
            eventsBound = true;
        }

        private void UnbindVideoEvents()
        {
            if (videoPlayer == null || !eventsBound) return;
            videoPlayer.prepareCompleted -= OnPrepareCompleted;
            videoPlayer.loopPointReached -= OnLoopPointReached;
            videoPlayer.errorReceived -= OnErrorReceived;
            eventsBound = false;
        }

        private void OnPrepareCompleted(VideoPlayer source)
        {
            if (!isOpen || isClosing || source != videoPlayer) return;
            source.Play();
        }

        private void OnLoopPointReached(VideoPlayer source)
        {
            if (!isOpen || isClosing || source != videoPlayer) return;
            CloseInternal(invokeContinue: true);
        }

        private void OnErrorReceived(VideoPlayer source, string message)
        {
            if (source != videoPlayer) return;
            SetStatus("Tutorial video failed to play.");
            Debug.LogError($"TutorialVideoUI: playback error. {message}", this);
            CloseInternal(invokeContinue: true);
        }

        private bool TryResolvePlatformClip(out VideoClip clip, out string error)
        {
            clip = null;
            error = null;

            RuntimePlatform platform = Application.platform;
            bool isLinux = platform == RuntimePlatform.LinuxPlayer ||
                           platform == RuntimePlatform.LinuxEditor;
            bool isWindows = platform == RuntimePlatform.WindowsPlayer ||
                             platform == RuntimePlatform.WindowsEditor;

            if (isLinux)
            {
                clip = linuxTutorialClip;
                if (clip == null)
                {
                    error = "Linux tutorial VideoClip (.webm) is not assigned.";
                    return false;
                }

                return true;
            }

            if (isWindows)
            {
                clip = windowsTutorialClip;
                if (clip == null)
                {
                    error = "Windows tutorial VideoClip (.mp4) is not assigned.";
                    return false;
                }

                return true;
            }

            error = $"Tutorial video is not supported on this platform ({platform}).";
            return false;
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
                statusText.text = message ?? string.Empty;
        }

        private void ClearStatus()
        {
            if (statusText != null)
                statusText.text = string.Empty;
        }
    }
}
