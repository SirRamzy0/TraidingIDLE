using System;
using TMPro;
using TraidingIDLE.Localization;
using TraidingIDLE.UI;
using UnityEngine;
using UnityEngine.UI;

namespace TraidingIDLE.Collections
{
    public sealed class CollectionFinalItemUI : MonoBehaviour
    {
        [Header("Content")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image artworkImage;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bonusText;

        [Header("Locked")]
        [SerializeField] private GameObject lockedRoot;
        [SerializeField] private TMP_Text lockedText;
        [SerializeField] private string lockedMessage = "Собери всю коллекцию чтобы разблокировать";

        [Header("Bought")]
        [SerializeField] private GameObject boughtRoot;
        [SerializeField] private TMP_Text boughtText;
        [SerializeField] private string boughtCaption = "Куплено";

        [Header("Image tint")]
        [SerializeField] private Color boughtBackgroundColor = Color.white;
        [SerializeField] private Color notBoughtBackgroundColor = new(0.45f, 0.45f, 0.5f, 1f);
        [SerializeField] private Color boughtArtworkColor = Color.white;
        [SerializeField] private Color notBoughtArtworkColor = new(0.55f, 0.55f, 0.6f, 1f);

        [Header("Action")]
        [SerializeField] private Button buyButton;
        [SerializeField] private TMP_Text buyButtonLabel;
        [SerializeField] private Graphic buyButtonGraphic;
        [SerializeField] private string buyButtonFormat = "Купить\n{0}";
        [SerializeField] private Color buyButtonEnabledColor = new(0.25f, 0.95f, 0.52f, 1f);
        [SerializeField] private Color buyButtonDisabledColor = new(0.55f, 0.57f, 0.62f, 1f);
        [SerializeField] private Color buyButtonBoughtColor = new(0.68f, 0.83f, 0.79f, 1f);

        private Action _buyClicked;
        private TransparentActionButtonStyle _buyButtonStyle;

        private void Awake()
        {
            AutoResolveReferences();
            if (buyButton != null)
                buyButton.onClick.AddListener(OnBuyClicked);
        }

        private void OnDestroy()
        {
            if (buyButton != null)
                buyButton.onClick.RemoveListener(OnBuyClicked);
        }

        public void Configure(
            Sprite background,
            Sprite artwork,
            string title,
            string bonus,
            string price,
            bool unlocked,
            bool bought,
            bool canBuy,
            Action buyClicked)
        {
            AutoResolveReferences();

            if (backgroundImage != null)
            {
                backgroundImage.sprite = background;
                backgroundImage.enabled = background != null;
                backgroundImage.color = bought ? boughtBackgroundColor : notBoughtBackgroundColor;
            }

            if (artworkImage != null)
            {
                artworkImage.sprite = artwork;
                artworkImage.enabled = artwork != null;
                artworkImage.color = bought ? boughtArtworkColor : notBoughtArtworkColor;
            }

            if (titleText != null)
                titleText.text = title;
            if (bonusText != null)
                bonusText.text = bonus;

            if (lockedRoot != null)
                lockedRoot.SetActive(!unlocked);
            if (lockedText != null)
                lockedText.text = LocalizationManager.Tr("collections.final_locked", lockedMessage);

            if (boughtRoot != null)
                boughtRoot.SetActive(false);
            if (boughtText != null)
                boughtText.text = LocalizationManager.Tr("common.purchased", boughtCaption);

            _buyClicked = buyClicked;

            if (buyButton != null)
            {
                buyButton.gameObject.SetActive(unlocked);
                buyButton.interactable = unlocked && !bought && canBuy;
            }

            if (buyButtonLabel != null)
                buyButtonLabel.text = bought
                    ? LocalizationManager.Tr("common.purchased", boughtCaption)
                    : GameTextFormatter.Format(
                        LocalizationManager.Tr("common.buy_cost_format", buyButtonFormat),
                        "Купить\n{0}",
                        price);

            ApplyBuyButtonStyle(bought, unlocked && !bought && canBuy);
        }

        private void OnBuyClicked()
        {
            _buyClicked?.Invoke();
        }

        private void AutoResolveReferences()
        {
            if (buyButton == null)
                buyButton = GetComponentInChildren<Button>(true);

            if (buyButtonGraphic == null && buyButton != null)
                buyButtonGraphic = buyButton.targetGraphic;

            if (buyButtonLabel == null && buyButton != null)
                buyButtonLabel = buyButton.GetComponentInChildren<TMP_Text>(true);
        }

        private void ApplyBuyButtonStyle(bool bought, bool canUse)
        {
            if (buyButton == null)
                return;

            _buyButtonStyle = TransparentActionButtonStyle.Attach(buyButton, buyButtonGraphic, buyButtonLabel);
            if (_buyButtonStyle == null)
                return;

            if (bought)
            {
                _buyButtonStyle.SetColor(buyButtonBoughtColor);
            }
            else
            {
                _buyButtonStyle.SetState(canUse, buyButtonEnabledColor, buyButtonDisabledColor);
            }
        }

    }
}
