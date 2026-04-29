using System;
using UnityEngine;
using UnityEngine.UI;

namespace TraidingIDLE.UI
{
    /// <summary>
    /// One-button pause/resume toggle for UI buttons.
    /// Stops gameplay through Time.timeScale = 0 and restores it on the next click.
    /// </summary>
    public sealed class PauseToggle : MonoBehaviour
    {
        [Header("Button")]
        [SerializeField] private Button pauseButton = null!;
        [SerializeField] private bool wireButtonAutomatically = true;

        [Header("Pause")]
        [SerializeField] private bool pauseAudio = true;

        private static bool _isPaused;
        private static float _timeScaleBeforePause = 1f;

        public static bool IsPaused => _isPaused;
        public static event Action<bool>? PauseStateChanged;

        private void Awake()
        {
            if (pauseButton == null)
                pauseButton = GetComponent<Button>();

            if (wireButtonAutomatically && pauseButton != null)
                pauseButton.onClick.AddListener(TogglePause);
        }

        private void OnDestroy()
        {
            if (pauseButton != null)
                pauseButton.onClick.RemoveListener(TogglePause);
        }

        public void TogglePause()
        {
            if (_isPaused)
                Resume();
            else
                Pause();
        }

        public void Pause()
        {
            if (_isPaused)
                return;

            _timeScaleBeforePause = Time.timeScale;
            Time.timeScale = 0f;
            _isPaused = true;

            if (pauseAudio)
                AudioListener.pause = true;

            PauseStateChanged?.Invoke(true);
        }

        public void Resume()
        {
            if (!_isPaused)
                return;

            var restoredTimeScale = Mathf.Abs(_timeScaleBeforePause);
            if (restoredTimeScale <= 0.0001f || float.IsNaN(restoredTimeScale))
                restoredTimeScale = 1f;

            Time.timeScale = restoredTimeScale;
            _isPaused = false;

            if (pauseAudio)
                AudioListener.pause = false;

            PauseStateChanged?.Invoke(false);
        }

        [ContextMenu("Toggle Pause")]
        private void Debug_TogglePause()
        {
            TogglePause();
        }

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticsOnDomainReload()
        {
            _isPaused = false;
            _timeScaleBeforePause = 1f;
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }
#endif
    }
}
