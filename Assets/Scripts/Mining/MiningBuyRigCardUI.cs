using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TraidingIDLE.Mining
{
    public sealed class MiningBuyRigCardUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText = null!;
        [SerializeField] private TMP_Text priceText = null!;
        [SerializeField] private TMP_Text buttonText = null!;
        [SerializeField] private Button buyButton = null!;

        [SerializeField] private string titleFormat = "Купить\nновый риг";
        [SerializeField] private string priceFormat = "{0}";
        [SerializeField] private string buttonFormat = "Купить\n{0}";

        private int _rigIndex;
        private Action<int>? _buyClicked;

        private void Awake()
        {
            if (buyButton != null)
                buyButton.onClick.AddListener(OnBuyClicked);
        }

        private void OnDestroy()
        {
            if (buyButton != null)
                buyButton.onClick.RemoveListener(OnBuyClicked);
        }

        public void Configure(int rigIndex, long priceRubles, bool canAfford, Action<int> buyClicked)
        {
            _rigIndex = rigIndex;
            _buyClicked = buyClicked;

            var price = FormatRubles(priceRubles);
            if (titleText != null)
                titleText.text = titleFormat;
            if (priceText != null)
                priceText.text = string.Format(SafeFormat(priceFormat, "{0}"), price);
            if (buttonText != null)
                buttonText.text = string.Format(SafeFormat(buttonFormat, "Купить\n{0}"), price);
            if (buyButton != null)
                buyButton.interactable = canAfford;
        }

        private void OnBuyClicked()
        {
            _buyClicked?.Invoke(_rigIndex);
        }

        private static string SafeFormat(string format, string fallback)
        {
            return string.IsNullOrEmpty(format) ? fallback : format;
        }

        private static string FormatRubles(long value)
        {
            return Math.Max(0, value)
                .ToString("N0", CultureInfo.InvariantCulture)
                .Replace(",", ".");
        }
    }
}
