using UnityEngine;
using UnityEngine.UI;

namespace TraidingIDLE.UI
{
    public sealed class TimedDialogToggle : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Button toggleButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private GameObject dialogRoot;

        [Header("Auto open")]
        [SerializeField] private bool autoOpenEnabled = true;
        [SerializeField, Min(0f)] private float autoOpenDelaySeconds = 180f;
        [SerializeField] private bool useUnscaledTime;
        [SerializeField] private bool manualOpenConsumesAutoOpen = true;

        private bool _autoOpenConsumed;
        private float _elapsedSeconds;

        private void Awake()
        {
            ResolveReferences();
            SetDialogOpen(false);
        }

        private void OnEnable()
        {
            ResolveReferences();

            if (toggleButton != null)
                toggleButton.onClick.AddListener(ToggleDialog);

            if (closeButton != null)
                closeButton.onClick.AddListener(CloseDialog);

            SetDialogOpen(false);
            if (!_autoOpenConsumed)
                _elapsedSeconds = 0f;
        }

        private void OnDisable()
        {
            if (toggleButton != null)
                toggleButton.onClick.RemoveListener(ToggleDialog);

            if (closeButton != null)
                closeButton.onClick.RemoveListener(CloseDialog);
        }

        private void Update()
        {
            if (!autoOpenEnabled || _autoOpenConsumed)
                return;

            _elapsedSeconds += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (_elapsedSeconds < autoOpenDelaySeconds)
                return;

            _autoOpenConsumed = true;
            SetDialogOpen(true);
        }

        public void ToggleDialog()
        {
            var shouldOpen = dialogRoot == null || !dialogRoot.activeSelf;
            SetDialogOpen(shouldOpen);

            if (shouldOpen && manualOpenConsumesAutoOpen)
                _autoOpenConsumed = true;
        }

        public void CloseDialog()
        {
            SetDialogOpen(false);
        }

        private void SetDialogOpen(bool open)
        {
            if (dialogRoot != null)
                dialogRoot.SetActive(open);
        }

        private void ResolveReferences()
        {
            if (toggleButton == null)
                toggleButton = GetComponent<Button>();
        }
    }
}
