using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TraidingIDLE.Localization
{
    public sealed class KnownStaticTextLocalizer : MonoBehaviour
    {
        private const string RootName = "Known Static Text Localizer";

        private readonly Dictionary<TMP_Text, string> _originalTexts = new();
        private float _refreshTimer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<KnownStaticTextLocalizer>() != null)
                return;

            var root = new GameObject(RootName);
            if (Application.isPlaying)
                DontDestroyOnLoad(root);

            root.AddComponent<KnownStaticTextLocalizer>();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            LocalizationManager.LanguageChanged += Apply;
            Apply();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            LocalizationManager.LanguageChanged -= Apply;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _originalTexts.Clear();
            Apply();
        }

        private void LateUpdate()
        {
            _refreshTimer -= Time.unscaledDeltaTime;
            if (_refreshTimer > 0f)
                return;

            _refreshTimer = 0.5f;
            Apply();
        }

        private void Apply()
        {
            var texts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < texts.Length; i++)
            {
                var text = texts[i];
                if (text == null || !text.gameObject.scene.IsValid())
                    continue;

                if (!_originalTexts.TryGetValue(text, out var original))
                {
                    original = text.text;
                    _originalTexts[text] = original;
                }

                if (TryTranslateByKnownPath(text, out var pathTranslated))
                {
                    text.text = pathTranslated;
                    continue;
                }

                if (KnownLocalization.TryTranslateStaticText(original, out var translated))
                    text.text = translated;
            }
        }

        private static bool TryTranslateByKnownPath(TMP_Text text, out string translated)
        {
            translated = string.Empty;
            if (text == null)
                return false;

            var path = GetPath(text.transform);
            if (path.Contains("/Shop_dialog/", StringComparison.OrdinalIgnoreCase)
                && path.Contains("/Starter_pack/", StringComparison.OrdinalIgnoreCase)
                && path.Contains("/Description/", StringComparison.OrdinalIgnoreCase))
            {
                translated = LocalizationManager.Tr("shop.offer_gems_coins", "Кристаллы + монеты");
                return true;
            }

            return false;
        }

        private static string GetPath(Transform transform)
        {
            if (transform == null)
                return string.Empty;

            var parts = new List<string>();
            var current = transform;
            while (current != null)
            {
                parts.Add(current.name);
                current = current.parent;
            }

            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
