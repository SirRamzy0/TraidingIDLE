using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TraidingIDLE.UI
{
    public sealed class MenuButtonHoverTextColor : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Target")]
        [SerializeField] private TMP_Text text = null!;
        [SerializeField] private Selectable selectable = null!;

        [Header("Selection (optional)")]
        [Tooltip("If assigned, hover color won't apply when this item is currently selected.")]
        [SerializeField] private MenuSelectionHighlight menu = null!;
        [SerializeField, Min(0)] private int menuIndex = 0;

        [Header("Colors")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color hoverColor = new(0.7f, 1f, 0.7f);

        private void Awake()
        {
            if (text == null)
                text = GetComponentInChildren<TMP_Text>(true);

            if (selectable == null)
                selectable = GetComponentInParent<Selectable>();

            if (menu == null)
                menu = GetComponentInParent<MenuSelectionHighlight>();
        }

        private void OnEnable()
        {
            Apply(normalColor);

            if (menu != null)
                menu.SelectionChanged += OnMenuSelectionChanged;
        }

        private void OnDisable()
        {
            if (menu != null)
                menu.SelectionChanged -= OnMenuSelectionChanged;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!IsInteractable())
                return;

            if (menu != null && menu.CurrentIndex == menuIndex)
                return;

            Apply(hoverColor);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Apply(normalColor);
        }

        private bool IsInteractable()
        {
            if (selectable != null && !selectable.IsInteractable())
                return false;

            // If any parent CanvasGroup disables interaction, treat as non-hoverable.
            var cg = GetComponentInParent<CanvasGroup>();
            if (cg != null && !cg.interactable)
                return false;

            return true;
        }

        private void OnMenuSelectionChanged(int selectedIndex)
        {
            // If we became selected, force back to normal color.
            if (selectedIndex == menuIndex)
                Apply(normalColor);
        }

        private void Apply(Color c)
        {
            if (text != null)
                text.color = c;
        }
    }
}

