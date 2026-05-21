using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;

#if PLUGIN_YG_2
using YG;
#endif

namespace TraidingIDLE.Localization
{
    [DefaultExecutionOrder(-10000)]
    public sealed class LocalizationManager : MonoBehaviour
    {
        private const string ResourcePath = "Localization/localization";
        private const string DefaultLanguageCode = "ru";
        private const string LanguageOverrideKey = "TraidingIDLE.Localization.LanguageOverride";

        private static LocalizationManager _instance;

        private readonly Dictionary<string, LocalizationEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

        private string _currentLanguageCode = DefaultLanguageCode;
        private bool _databaseLoaded;

        public static event Action LanguageChanged;

        public static string CurrentLanguageCode => Instance._currentLanguageCode;
        public static GameLanguage CurrentLanguage => ToGameLanguage(CurrentLanguageCode);

        private static LocalizationManager Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;

                var go = new GameObject(nameof(LocalizationManager));
                if (Application.isPlaying)
                    DontDestroyOnLoad(go);

                _instance = go.AddComponent<LocalizationManager>();
                return _instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            _ = Instance;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            if (Application.isPlaying)
                DontDestroyOnLoad(gameObject);

            LoadDatabase();
            ApplyStartupLanguage(false);
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;

#if PLUGIN_YG_2
            YG2.onSwitchLang += OnYandexLanguageSwitched;
            YG2.onGetSDKData += OnYandexSdkReady;
#endif
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;

#if PLUGIN_YG_2
            YG2.onSwitchLang -= OnYandexLanguageSwitched;
            YG2.onGetSDKData -= OnYandexSdkReady;
#endif
        }

        private IEnumerator Start()
        {
            // Yandex SDK usually initializes before Unity starts on the portal, but this
            // keeps local/debug launches correct without waiting during normal gameplay.
            for (var i = 0; i < 30; i++)
            {
                ApplyStartupLanguage(true);
                yield return null;
            }
        }

        public static string Tr(string key, string fallback = "")
        {
            if (string.IsNullOrWhiteSpace(key))
                return fallback ?? string.Empty;

            return Instance.Translate(key, fallback);
        }

        public static string Format(string key, string fallback, params object[] args)
        {
            var format = Tr(key, fallback);

            try
            {
                return string.Format(CultureInfo.InvariantCulture, format, args);
            }
            catch (FormatException)
            {
                return string.Format(CultureInfo.InvariantCulture, fallback ?? string.Empty, args);
            }
        }

        public static void SetLanguage(string languageCode)
        {
            var normalized = NormalizeLanguageCode(languageCode);
            Instance.SaveLanguageOverride(normalized);
            Instance.SetLanguageInternal(normalized, true);

#if PLUGIN_YG_2
            try
            {
                YG2.SwitchLanguage(normalized);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"YG2 language switch failed: {exception.Message}");
            }
#endif
        }

        public static bool TryGetEntry(string key, out LocalizationEntry entry)
        {
            Instance.LoadDatabase();
            return Instance._entries.TryGetValue(key ?? string.Empty, out entry);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplyStartupLanguage(true);
        }

        private void OnYandexSdkReady()
        {
            ApplyStartupLanguage(true);
        }

        private void OnYandexLanguageSwitched(string languageCode)
        {
            SetLanguageInternal(GetStartupLanguage(languageCode), true);
        }

        private void ApplyStartupLanguage(bool notifyIfChanged)
        {
            SetLanguageInternal(GetStartupLanguage(DetectStartupLanguage()), notifyIfChanged);
        }

        private string GetStartupLanguage(string detectedLanguageCode)
        {
            return TryLoadLanguageOverride(out var savedLanguageCode)
                ? savedLanguageCode
                : NormalizeLanguageCode(detectedLanguageCode);
        }

        private void SetLanguageInternal(string languageCode, bool notifyIfChanged)
        {
            var normalized = NormalizeLanguageCode(languageCode);
            if (string.Equals(_currentLanguageCode, normalized, StringComparison.OrdinalIgnoreCase))
                return;

            _currentLanguageCode = normalized;

            if (notifyIfChanged)
                LanguageChanged?.Invoke();
        }

        private void SaveLanguageOverride(string languageCode)
        {
            PlayerPrefs.SetString(LanguageOverrideKey, NormalizeLanguageCode(languageCode));
            PlayerPrefs.Save();
        }

        private static bool TryLoadLanguageOverride(out string languageCode)
        {
            languageCode = string.Empty;
            if (!PlayerPrefs.HasKey(LanguageOverrideKey))
                return false;

            languageCode = NormalizeLanguageCode(PlayerPrefs.GetString(LanguageOverrideKey, DefaultLanguageCode));
            return true;
        }

        private string Translate(string key, string fallback)
        {
            LoadDatabase();

            if (!_entries.TryGetValue(key, out var entry))
                return fallback ?? key;

            var translated = entry.Get(_currentLanguageCode);
            if (!string.IsNullOrWhiteSpace(translated))
                return translated;

            translated = entry.Get(DefaultLanguageCode);
            return string.IsNullOrWhiteSpace(translated) ? fallback ?? key : translated;
        }

        private void LoadDatabase()
        {
            if (_databaseLoaded)
                return;

            _databaseLoaded = true;
            _entries.Clear();

            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null || string.IsNullOrWhiteSpace(asset.text))
            {
                Debug.LogWarning($"Localization database not found at Resources/{ResourcePath}.json");
                return;
            }

            LocalizationData data;
            try
            {
                data = JsonUtility.FromJson<LocalizationData>(asset.text);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Localization database parse failed: {exception.Message}");
                return;
            }

            if (data?.entries == null)
                return;

            for (var i = 0; i < data.entries.Length; i++)
            {
                var entry = data.entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.key))
                    continue;

                _entries[entry.key.Trim()] = entry;
            }
        }

        private static string DetectStartupLanguage()
        {
#if PLUGIN_YG_2
            try
            {
                if (!string.IsNullOrWhiteSpace(YG2.lang))
                    return NormalizeLanguageCode(YG2.lang);
            }
            catch
            {
                // YG2 can be unavailable during early editor domain reloads.
            }
#endif

            return DefaultLanguageCode;
        }

        private static string NormalizeLanguageCode(string languageCode)
        {
            if (string.IsNullOrWhiteSpace(languageCode))
                return DefaultLanguageCode;

            var code = languageCode.Trim().ToLowerInvariant();
            if (code.Length > 2)
                code = code.Substring(0, 2);

            if (code == "ru")
                return "ru";

            if (code == "en")
                return "en";

            if (code == "de")
                return "de";

            if (code == "es")
                return "es";

            return "en";
        }

        private static GameLanguage ToGameLanguage(string languageCode)
        {
            var code = NormalizeLanguageCode(languageCode);

            if (code == "en")
                return GameLanguage.En;

            if (code == "de")
                return GameLanguage.De;

            if (code == "es")
                return GameLanguage.Es;

            return GameLanguage.Ru;
        }

        [Serializable]
        private sealed class LocalizationData
        {
            public LocalizationEntry[] entries;
        }
    }
}
