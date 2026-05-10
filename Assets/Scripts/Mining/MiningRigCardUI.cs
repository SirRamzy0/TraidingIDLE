using System;
using System.Globalization;
using TMPro;
using TraidingIDLE.Currencies;
using UnityEngine;
using UnityEngine.UI;

namespace TraidingIDLE.Mining
{
    public sealed class MiningRigCardUI : MonoBehaviour
    {
        [Header("Image")]
        [SerializeField] private Image rigImage = null!;

        [Header("Texts")]
        [SerializeField] private TMP_Text rigNameText = null!;
        [SerializeField] private TMP_Text levelText = null!;
        [SerializeField] private TMP_Text incomePerHourText = null!;
        [SerializeField] private TMP_Text upgradeButtonText = null!;

        [Header("Controls")]
        [SerializeField] private Slider progressSlider = null!;
        [SerializeField] private Button upgradeButton = null!;

        [Header("Formats")]
        [SerializeField] private string rigNameFormat = "Риг #{0}";
        [SerializeField] private string levelFormat = "Уровень {0}";
        [SerializeField] private string incomeFormat = "{0} {1} в час";
        [SerializeField] private string upgradeFormat = "Улучшить\n{0}";
        [SerializeField] private string maxLabel = "MAX";

        private int _rigIndex;
        private Action<int>? _upgradeClicked;

        private void Awake()
        {
            if (upgradeButton != null)
                upgradeButton.onClick.AddListener(OnUpgradeClicked);
        }

        private void OnDestroy()
        {
            if (upgradeButton != null)
                upgradeButton.onClick.RemoveListener(OnUpgradeClicked);
        }

        public void Configure(
            int rigIndex,
            int level,
            int maxLevel,
            double incomePerHour,
            CurrencyId currency,
            Sprite rigSprite,
            long upgradeCostRubles,
            bool canAffordUpgrade,
            Action<int> upgradeClicked)
        {
            _rigIndex = rigIndex;
            _upgradeClicked = upgradeClicked;

            if (rigNameText != null)
                rigNameText.text = string.Format(SafeFormat(rigNameFormat, "Риг #{0}"), rigIndex + 1);

            if (levelText != null)
                levelText.text = string.Format(SafeFormat(levelFormat, "Уровень {0}"), Mathf.Max(1, level));

            if (rigImage != null && rigSprite != null)
                rigImage.sprite = rigSprite;

            if (incomePerHourText != null)
            {
                incomePerHourText.text = string.Format(
                    SafeFormat(incomeFormat, "{0} {1} в час"),
                    FormatAmount(incomePerHour, currency),
                    currency);
            }

            var isMax = level >= maxLevel;
            if (upgradeButtonText != null)
            {
                upgradeButtonText.text = isMax
                    ? maxLabel
                    : string.Format(SafeFormat(upgradeFormat, "Улучшить\n{0}"), FormatRubles(upgradeCostRubles));
            }

            if (upgradeButton != null)
                upgradeButton.interactable = !isMax && canAffordUpgrade;
        }

        public void SetProgress(float progress01)
        {
            if (progressSlider == null)
                return;

            progressSlider.minValue = 0f;
            progressSlider.maxValue = 1f;
            progressSlider.SetValueWithoutNotify(Mathf.Clamp01(progress01));
        }

        private void OnUpgradeClicked()
        {
            _upgradeClicked?.Invoke(_rigIndex);
        }

        private static string SafeFormat(string format, string fallback)
        {
            return string.IsNullOrEmpty(format) ? fallback : format;
        }

        private static string FormatAmount(double value, CurrencyId currency)
        {
            value = Math.Max(0d, value);
            return currency == CurrencyId.BTC
                ? value.ToString(value < 1d ? "0.##" : "N0", CultureInfo.InvariantCulture).Replace(",", ".")
                : value.ToString("N0", CultureInfo.InvariantCulture).Replace(",", ".");
        }

        private static string FormatRubles(long value)
        {
            return Math.Max(0, value)
                .ToString("N0", CultureInfo.InvariantCulture)
                .Replace(",", ".");
        }
    }
}
