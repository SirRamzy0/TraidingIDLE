using TMPro;
using UnityEngine;

namespace TraidingIDLE.Localization
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class LocalizedText : MonoBehaviour
    {
        [SerializeField] private TMP_Text target;
        [SerializeField] private string key;
        [SerializeField, TextArea] private string fallbackText;
        [SerializeField] private bool useCurrentTextAsFallback = true;

        private void Reset()
        {
            target = GetComponent<TMP_Text>();
            if (target != null)
                fallbackText = target.text;
        }

        private void Awake()
        {
            if (target == null)
                target = GetComponent<TMP_Text>();

            if (useCurrentTextAsFallback && string.IsNullOrEmpty(fallbackText) && target != null)
                fallbackText = target.text;
        }

        private void OnEnable()
        {
            LocalizationManager.LanguageChanged += Apply;
            Apply();
        }

        private void OnDisable()
        {
            LocalizationManager.LanguageChanged -= Apply;
        }

        public void SetKey(string localizationKey, string fallback = null)
        {
            key = localizationKey;
            if (fallback != null)
                fallbackText = fallback;

            Apply();
        }

        public void Apply()
        {
            if (target == null)
                target = GetComponent<TMP_Text>();

            if (target == null || string.IsNullOrWhiteSpace(key))
                return;

            target.text = LocalizationManager.Tr(key, fallbackText);
        }
    }
}
