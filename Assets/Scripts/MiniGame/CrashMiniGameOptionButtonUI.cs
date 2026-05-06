using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TraidingIDLE.MiniGame
{
    public sealed class CrashMiniGameOptionButtonUI : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text labelText;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Graphic backgroundGraphic;
        [SerializeField] private Sprite normalSprite;
        [SerializeField] private Sprite selectedSprite;
        [SerializeField] private Color enabledColor = Color.white;
        [SerializeField] private Color disabledTint = new(0.72f, 0.72f, 0.72f, 1f);
        [SerializeField] private Color blinkColor = new(0.9f, 0.18f, 0.18f, 1f);
        [SerializeField, Min(0.02f)] private float blinkHalfPeriodSeconds = 0.08f;
        [SerializeField, Min(1)] private int blinkCycles = 3;

        private Action _clicked;
        private bool _selected;
        private bool _interactable = true;
        private Coroutine _blinkRoutine;

        private void Awake()
        {
            ResolveReferences();
            if (button != null)
                button.onClick.AddListener(OnClicked);
        }

        private void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveListener(OnClicked);
        }

        public void Bind(Action clicked)
        {
            _clicked = clicked;
        }

        public void SetLabel(string label)
        {
            if (labelText != null)
                labelText.text = label;
        }

        public void SetState(bool selected, bool interactable)
        {
            _selected = selected;
            _interactable = interactable;

            if (button != null)
                button.interactable = interactable;

            ApplyVisualState();
        }

        public void BlinkUnavailable()
        {
            if (_blinkRoutine != null)
                StopCoroutine(_blinkRoutine);

            _blinkRoutine = StartCoroutine(BlinkRoutine());
        }

        private void OnClicked()
        {
            _clicked?.Invoke();
        }

        private IEnumerator BlinkRoutine()
        {
            for (var i = 0; i < blinkCycles; i++)
            {
                if (backgroundGraphic != null)
                    backgroundGraphic.color = blinkColor;

                yield return new WaitForSecondsRealtime(blinkHalfPeriodSeconds);
                ApplyVisualState();
                yield return new WaitForSecondsRealtime(blinkHalfPeriodSeconds);
            }

            _blinkRoutine = null;
        }

        private void ApplyVisualState()
        {
            if (backgroundImage != null)
            {
                var sprite = _selected ? selectedSprite : normalSprite;
                if (sprite != null)
                    backgroundImage.sprite = sprite;

                backgroundImage.color = _interactable ? enabledColor : disabledTint;
                return;
            }

            if (backgroundGraphic != null)
                backgroundGraphic.color = _interactable ? enabledColor : disabledTint;
        }

        private void ResolveReferences()
        {
            if (button == null)
                button = GetComponent<Button>();

            if (backgroundImage == null && button != null)
                backgroundImage = button.targetGraphic as Image;

            if (backgroundImage == null)
                backgroundImage = GetComponent<Image>();

            if (normalSprite == null && backgroundImage != null)
                normalSprite = backgroundImage.sprite;

            if (backgroundGraphic == null && button != null)
                backgroundGraphic = button.targetGraphic;

            if (backgroundGraphic == null && backgroundImage != null)
                backgroundGraphic = backgroundImage;

            if (labelText == null)
                labelText = GetComponentInChildren<TMP_Text>(true);
        }
    }
}
