using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TraidingIDLE.UI
{
    [DisallowMultipleComponent]
    public sealed class TransparentActionButtonStyle : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private static readonly Color Transparent = new(1f, 1f, 1f, 0f);

        [SerializeField] private Button button;
        [SerializeField] private Graphic backgroundGraphic;
        [SerializeField] private TMP_Text[] labelTexts = { };
        [SerializeField, Range(0f, 1f)] private float hoverLighten = 0.16f;

        private bool _available;
        private bool _hovered;

        public static TransparentActionButtonStyle Attach(
            Button sourceButton,
            Graphic sourceBackground,
            params TMP_Text[] sourceLabels)
        {
            if (sourceButton == null)
                return null;

            var style = sourceButton.GetComponent<TransparentActionButtonStyle>();
            if (style == null)
                style = sourceButton.gameObject.AddComponent<TransparentActionButtonStyle>();

            style.SetReferences(sourceButton, sourceBackground, sourceLabels);
            return style;
        }

        public void SetState(bool available, Color availableColor, Color unavailableColor)
        {
            _useFixedColor = false;
            _available = available;
            Apply(availableColor, unavailableColor);
        }

        public void SetColor(Color color)
        {
            _useFixedColor = true;
            _fixedColor = color;
            Refresh();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            Refresh();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            Refresh();
        }

        private void SetReferences(Button sourceButton, Graphic sourceBackground, TMP_Text[] sourceLabels)
        {
            button = sourceButton;
            backgroundGraphic = sourceBackground != null ? sourceBackground : button.targetGraphic;
            labelTexts = HasAnyLabel(sourceLabels)
                ? sourceLabels
                : button.GetComponentsInChildren<TMP_Text>(true);

            if (button != null)
                button.transition = Selectable.Transition.None;

            ApplyTransparentBackground();
        }

        private void Refresh()
        {
            ApplyTransparentBackground();
            ApplyLabelColor();
        }

        private void Apply(Color availableColor, Color unavailableColor)
        {
            _availableColor = availableColor;
            _unavailableColor = unavailableColor;
            Refresh();
        }

        private Color _availableColor = new(0.25f, 0.95f, 0.52f, 1f);
        private Color _unavailableColor = new(0.55f, 0.57f, 0.62f, 1f);
        private Color _fixedColor = Color.white;
        private bool _useFixedColor;

        private void ApplyTransparentBackground()
        {
            if (backgroundGraphic == null)
                return;

            backgroundGraphic.color = Transparent;
            backgroundGraphic.raycastTarget = true;
        }

        private void ApplyLabelColor()
        {
            if (labelTexts == null)
                return;

            var baseColor = _useFixedColor ? _fixedColor : _available ? _availableColor : _unavailableColor;
            var targetColor = _hovered ? Lighten(baseColor, hoverLighten) : baseColor;

            for (var i = 0; i < labelTexts.Length; i++)
            {
                if (labelTexts[i] != null)
                    labelTexts[i].color = targetColor;
            }
        }

        private static Color Lighten(Color color, float amount)
        {
            var alpha = color.a;
            color = Color.Lerp(color, Color.white, amount);
            color.a = alpha;
            return color;
        }

        private static bool HasAnyLabel(TMP_Text[] labels)
        {
            if (labels == null)
                return false;

            for (var i = 0; i < labels.Length; i++)
            {
                if (labels[i] != null)
                    return true;
            }

            return false;
        }
    }
}
