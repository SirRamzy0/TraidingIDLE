using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace TraidingIDLE.UI
{
    public sealed class MenuHelperTextByTab : MonoBehaviour
    {
        [Serializable]
        private sealed class HelperMessage
        {
            [Tooltip("Только для удобства в инспекторе. На логику не влияет.")]
            public string tabName = "";

            [TextArea(2, 5)]
            public string text = "";
        }

        [Header("Refs")]
        [SerializeField] private MenuSelectionHighlight menu;
        [SerializeField] private TMP_Text helperText;

        [Tooltip("Необязательно. Если указать CanvasGroup, текст будет плавно меняться.")]
        [SerializeField] private CanvasGroup helperCanvasGroup;

        [Header("Messages")]
        [SerializeField] private HelperMessage[] messages =
        {
            new HelperMessage
            {
                tabName = "Торговля",
                text = "Покупай валюту дешевле и продавай дороже, чтобы зарабатывать рубли."
            },
            new HelperMessage
            {
                tabName = "Майнинг",
                text = "Запускай майнинг и улучшай оборудование, чтобы получать больше ресурсов."
            },
            new HelperMessage
            {
                tabName = "Трейдинг",
                text = "Следи за движением рынка и используй возможности для быстрой прибыли."
            },
            new HelperMessage
            {
                tabName = "Бизнес",
                text = "Покупай и улучшай бизнесы, чтобы получать пассивный доход."
            },
        };

        [Header("Fallback")]
        [SerializeField, TextArea(2, 5)] private string fallbackText = "";
        [SerializeField] private bool clearTextIfMessageMissing = false;

        [Header("Animation")]
        [SerializeField] private bool animateTextChange = true;
        [SerializeField, Min(0.01f)] private float fadeOutSeconds = 0.08f;
        [SerializeField, Min(0.01f)] private float fadeInSeconds = 0.12f;
        [SerializeField] private bool useUnscaledTime = true;

        private Coroutine _changeRoutine;

        private void Awake()
        {
            if (menu == null)
                menu = FindAnyObjectByType<MenuSelectionHighlight>();

            if (helperText == null)
                helperText = GetComponent<TMP_Text>();

            if (helperCanvasGroup == null && helperText != null)
                helperCanvasGroup = helperText.GetComponentInParent<CanvasGroup>();
        }

        private void OnEnable()
        {
            if (menu != null)
                menu.SelectionChanged += OnMenuSelectionChanged;

            RefreshTextImmediate();
        }

        private void OnDisable()
        {
            if (menu != null)
                menu.SelectionChanged -= OnMenuSelectionChanged;

            if (_changeRoutine != null)
            {
                StopCoroutine(_changeRoutine);
                _changeRoutine = null;
            }
        }

        private void OnMenuSelectionChanged(int selectedIndex)
        {
            SetTextForIndex(selectedIndex);
        }

        private void RefreshTextImmediate()
        {
            if (menu == null)
                return;

            var text = GetTextForIndex(menu.CurrentIndex);

            if (helperText != null)
                helperText.text = text;

            if (helperCanvasGroup != null)
                helperCanvasGroup.alpha = 1f;
        }

        private void SetTextForIndex(int index)
        {
            var text = GetTextForIndex(index);

            if (helperText == null)
                return;

            if (!animateTextChange || helperCanvasGroup == null || !gameObject.activeInHierarchy)
            {
                helperText.text = text;
                return;
            }

            if (_changeRoutine != null)
                StopCoroutine(_changeRoutine);

            _changeRoutine = StartCoroutine(ChangeTextRoutine(text));
        }

        private IEnumerator ChangeTextRoutine(string nextText)
        {
            yield return FadeCanvasGroup(1f, 0f, fadeOutSeconds);

            if (helperText != null)
                helperText.text = nextText;

            yield return FadeCanvasGroup(0f, 1f, fadeInSeconds);

            _changeRoutine = null;
        }

        private IEnumerator FadeCanvasGroup(float from, float to, float duration)
        {
            if (helperCanvasGroup == null)
                yield break;

            duration = Mathf.Max(0.01f, duration);

            var time = 0f;
            while (time < duration)
            {
                time += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                var t = Mathf.Clamp01(time / duration);
                helperCanvasGroup.alpha = Mathf.Lerp(from, to, t);
                yield return null;
            }

            helperCanvasGroup.alpha = to;
        }

        private string GetTextForIndex(int index)
        {
            if (messages != null && index >= 0 && index < messages.Length && messages[index] != null)
            {
                var text = messages[index].text;
                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }

            return clearTextIfMessageMissing ? "" : fallbackText;
        }
    }
}