using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace TraidingIDLE.Business
{
    public sealed class BusinessListRowUI : MonoBehaviour
    {
        [SerializeField, Min(0)] private int businessIndex;

        [Header("Selection")]
        [SerializeField] private Button selectButton;
        [SerializeField] private GameObject selectionHighlight;
        [SerializeField] private Image cardBackground;
        [SerializeField] private Color normalColor = new(0.10f, 0.12f, 0.18f, 0.95f);
        [SerializeField] private Color selectedColor = new(0.02f, 0.20f, 0.15f, 0.95f);
        [SerializeField] private Color notPurchasedColor = new(0.08f, 0.09f, 0.13f, 0.95f);

        [Header("Content")]
        [SerializeField] private Image artworkImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text categoryText;
        [SerializeField] private TMP_Text incomePerHourText;
        [SerializeField] private TMP_Text levelText;

        [Header("Action")]
        [SerializeField] private Button primaryActionButton;
        [SerializeField] private TMP_Text primaryActionVerbText;
        [SerializeField] private TMP_Text actionPriceText;
        [SerializeField] private Graphic primaryActionGraphic;
        [SerializeField] private Color actionEnabledColor = Color.white;
        [SerializeField] private Color actionDisabledColor = new(0.35f, 0.35f, 0.35f, 0.75f);

        public int BusinessIndex => businessIndex;

        public void AssignBusinessIndex(int index)
        {
            businessIndex = Mathf.Max(0, index);
        }

        internal void ResolveButtonReferences()
        {
            if (selectButton == null)
                selectButton = GetComponent<Button>();

            if (primaryActionButton == null)
            {
                foreach (var button in GetComponentsInChildren<Button>(true))
                {
                    if (button == null || ReferenceEquals(button, selectButton))
                        continue;

                    primaryActionButton = button;
                    break;
                }
            }

            if (primaryActionGraphic == null && primaryActionButton != null)
                primaryActionGraphic = primaryActionButton.targetGraphic;

            if (primaryActionGraphic != null && actionEnabledColor == Color.white)
                actionEnabledColor = primaryActionGraphic.color;

            if (artworkImage == null)
            {
                foreach (var image in GetComponentsInChildren<Image>(true))
                {
                    if (image == null
                        || ReferenceEquals(image, cardBackground)
                        || ReferenceEquals(image, primaryActionGraphic))
                        continue;

                    artworkImage = image;
                    break;
                }
            }
        }

        public void BindSelect(UnityAction handler)
        {
            if (selectButton == null)
                return;

            selectButton.onClick.RemoveAllListeners();
            if (handler != null)
                selectButton.onClick.AddListener(handler);
        }

        public void BindPrimaryAction(UnityAction handler)
        {
            if (primaryActionButton == null)
                return;

            primaryActionButton.onClick.RemoveAllListeners();
            if (handler != null)
                primaryActionButton.onClick.AddListener(handler);
        }

        public void RefreshAppearance(bool selected, bool owned)
        {
            if (cardBackground != null)
                cardBackground.color = selected ? selectedColor : owned ? normalColor : notPurchasedColor;

            if (selectionHighlight != null)
                selectionHighlight.SetActive(selected);
        }

        public void RefreshRow(
            Sprite artwork,
            string businessName,
            string category,
            string incomeLine,
            string levelLine,
            string actionPrice,
            string primaryActionVerbCaption,
            bool showPrimaryActionButton,
            bool primaryInteractableWhenShown)
        {
            if (artworkImage != null)
            {
                artworkImage.sprite = artwork;
                artworkImage.enabled = artwork != null;
            }

            if (nameText != null)
                nameText.text = businessName;
            if (categoryText != null)
                categoryText.text = category;
            if (incomePerHourText != null)
                incomePerHourText.text = incomeLine;
            if (levelText != null)
                levelText.text = levelLine;
            if (actionPriceText != null)
                actionPriceText.text = showPrimaryActionButton ? actionPrice : "";
            if (primaryActionVerbText != null)
                primaryActionVerbText.text = showPrimaryActionButton ? primaryActionVerbCaption : "";

            if (primaryActionButton != null)
            {
                primaryActionButton.gameObject.SetActive(showPrimaryActionButton);
                primaryActionButton.interactable = showPrimaryActionButton && primaryInteractableWhenShown;
            }

            if (primaryActionGraphic != null)
                primaryActionGraphic.color = showPrimaryActionButton && primaryInteractableWhenShown
                    ? actionEnabledColor
                    : actionDisabledColor;
        }
    }
}
