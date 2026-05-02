using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TraidingIDLE.UI
{
    public abstract class CategoryFilterButtonBase : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private string categoryKey = "";
        [SerializeField] private Button button;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Sprite normalSprite;
        [SerializeField] private Sprite selectedSprite;
        [SerializeField] private TMP_Text labelText;
        [SerializeField] private Color selectedColor = Color.white;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color hoverTint = new(1f, 1f, 1f, 0.9f);
        [SerializeField] private Color selectedTextColor = Color.white;
        [SerializeField] private Color normalTextColor = Color.white;

        private bool _selected;
        private bool _hovered;

        protected virtual bool IsAllFilter => false;

        public string CategoryKey
        {
            get
            {
                if (IsAllFilter)
                    return "";

                if (!string.IsNullOrWhiteSpace(categoryKey))
                    return categoryKey;

                AutoResolveReferences();
                return labelText != null ? labelText.text : "";
            }
        }

        public void Bind(Action<string> clicked)
        {
            AutoResolveReferences();
            if (button == null)
                return;

            button.onClick.RemoveAllListeners();
            if (clicked != null)
                button.onClick.AddListener(() => clicked.Invoke(CategoryKey));
        }

        public void SetSelected(bool selected)
        {
            AutoResolveReferences();
            _selected = selected;
            ApplyVisualState();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            ApplyVisualState();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            ApplyVisualState();
        }

        private void ApplyVisualState()
        {
            if (backgroundImage != null)
            {
                var targetSprite = _selected ? selectedSprite : normalSprite;
                if (targetSprite != null)
                    backgroundImage.sprite = targetSprite;

                var baseColor = _selected ? selectedColor : normalColor;
                backgroundImage.color = _hovered ? MultiplyColor(baseColor, hoverTint) : baseColor;
            }

            if (labelText != null)
                labelText.color = _selected ? selectedTextColor : normalTextColor;
        }

        private void AutoResolveReferences()
        {
            if (button == null)
                button = GetComponent<Button>();

            if (backgroundImage == null && button != null)
                backgroundImage = button.targetGraphic as Image;

            if (backgroundImage == null)
                backgroundImage = GetComponent<Image>();

            if (normalSprite == null && backgroundImage != null)
                normalSprite = backgroundImage.sprite;

            if (labelText == null)
                labelText = GetComponentInChildren<TMP_Text>(true);
        }

        private static Color MultiplyColor(Color a, Color b)
        {
            return new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a);
        }
    }
}
