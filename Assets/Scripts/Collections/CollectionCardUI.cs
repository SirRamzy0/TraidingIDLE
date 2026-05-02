using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TraidingIDLE.Collections
{
    public sealed class CollectionCardUI : MonoBehaviour
    {
        [Header("Content")]
        [SerializeField] private Image artworkImage;
        [SerializeField] private TMP_Text titleText;

        [Header("State")]
        [SerializeField] private GameObject dimRoot;
        [SerializeField] private GameObject boughtRoot;
        [SerializeField] private TMP_Text boughtText;
        [SerializeField] private Graphic cardGraphic;
        [SerializeField] private Color notBoughtColor = new(1f, 1f, 1f, 0.65f);
        [SerializeField] private Color boughtColor = Color.white;

        [Header("Action")]
        [SerializeField] private Button buyButton;
        [SerializeField] private TMP_Text buyButtonLabel;
        [SerializeField] private Graphic buyButtonGraphic;
        [SerializeField] private string buyButtonFormat = "Купить\n{0}";
        [SerializeField] private string boughtCaption = "Куплено";
        [SerializeField] private Color buyButtonEnabledColor = new(0.08f, 0.45f, 0.28f, 1f);
        [SerializeField] private Color buyButtonDisabledColor = new(0.35f, 0.35f, 0.35f, 0.75f);

        private Action _buyClicked;

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
            Sprite artwork,
            string title,
            string price,
            bool bought,
            bool canBuy,
            Action buyClicked)
        {
            AutoResolveReferences();

            if (artworkImage != null)
            {
                artworkImage.sprite = artwork;
                artworkImage.enabled = artwork != null;
            }

            if (titleText != null)
                titleText.text = title;

            if (dimRoot != null)
                dimRoot.SetActive(!bought);
            if (boughtRoot != null)
                boughtRoot.SetActive(bought);
            if (boughtText != null)
                boughtText.text = boughtCaption;
            if (cardGraphic != null)
                cardGraphic.color = bought ? boughtColor : notBoughtColor;

            _buyClicked = buyClicked;

            if (buyButton != null)
            {
                buyButton.gameObject.SetActive(!bought);
                buyButton.interactable = !bought && canBuy;
            }

            if (buyButtonLabel != null)
                buyButtonLabel.text = bought ? boughtCaption : FormatOne(buyButtonFormat, "Купить\n{0}", price);

            if (buyButtonGraphic != null)
                buyButtonGraphic.color = !bought && canBuy ? buyButtonEnabledColor : buyButtonDisabledColor;
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

        private static string FormatOne(string format, string fallback, object arg)
        {
            var safe = string.IsNullOrWhiteSpace(format) ? fallback : format;
            try
            {
                return string.Format(safe, arg);
            }
            catch (FormatException)
            {
                return string.Format(fallback, arg);
            }
        }
    }
}
