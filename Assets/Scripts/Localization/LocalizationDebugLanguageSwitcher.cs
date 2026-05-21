using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TraidingIDLE.Localization
{
    public sealed class LocalizationDebugLanguageSwitcher : MonoBehaviour
    {
        private const string RootName = "Localization Debug Language Switcher";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureCreated();
#endif
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureCreated();
        }

        private static void EnsureCreated()
        {
            if (FindObjectOfType<LocalizationDebugLanguageSwitcher>() != null)
                return;

            var canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
                return;

            var root = new GameObject(RootName, typeof(RectTransform), typeof(CanvasGroup), typeof(HorizontalLayoutGroup));
            var rect = root.GetComponent<RectTransform>();
            root.transform.SetParent(canvas.transform, false);
            root.transform.SetAsLastSibling();

            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-12f, -12f);
            rect.sizeDelta = new Vector2(136f, 42f);

            var canvasGroup = root.GetComponent<CanvasGroup>();
            canvasGroup.ignoreParentGroups = true;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            var layout = root.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var switcher = root.AddComponent<LocalizationDebugLanguageSwitcher>();
            switcher.CreateButton(root.transform, "RU", "ru");
            switcher.CreateButton(root.transform, "EN", "en");
        }

        private void CreateButton(Transform parent, string label, string languageCode)
        {
            var buttonObject = new GameObject($"Language {label}", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            var rect = buttonObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(64f, 42f);

            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.08f, 0.12f, 0.22f, 0.92f);

            var button = buttonObject.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = new Color(0.08f, 0.12f, 0.22f, 0.92f);
            colors.highlightedColor = new Color(0.16f, 0.28f, 0.46f, 1f);
            colors.pressedColor = new Color(0.05f, 0.35f, 0.2f, 1f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            button.onClick.AddListener(() => LocalizationManager.SetLanguage(languageCode));

            var textObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(buttonObject.transform, false);

            var textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 24f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
        }
    }
}
