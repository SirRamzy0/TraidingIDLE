using System;
using System.Globalization;
using TMPro;
using TraidingIDLE.Currencies;
using TraidingIDLE.Player;
using UnityEngine;
using UnityEngine.UI;

namespace TraidingIDLE.UI
{
    public sealed class TradingPanelUI : MonoBehaviour
    {
        private const int SliderSegments = 10;

        [Header("Refs")]
        [SerializeField] private PlayerProfile profile = null!;
        [SerializeField] private CurrencyMarket market = null!;

        [Header("Holdings label (active coin)")]
        [SerializeField] private TMP_Text holdingsText = null!;
        [SerializeField] private string holdingsFormat = "кол-во: {0}/{1}";

        [Header("Buy")]
        [SerializeField] private Button buyButton = null!;
        [SerializeField] private Slider buySlider = null!;
        [SerializeField] private Button buyMaxButton = null!;
        [SerializeField] private TMP_Text buyTotalText = null!;
        [SerializeField] private TMP_Text buyCountText = null!;
        [SerializeField] private string buyTotalFormat = "Р {0}";

        [Header("Sell")]
        [SerializeField] private Button sellButton = null!;
        [SerializeField] private Slider sellSlider = null!;
        [SerializeField] private Button sellMaxButton = null!;
        [SerializeField] private TMP_Text sellTotalText = null!;
        [SerializeField] private TMP_Text sellCountText = null!;
        [SerializeField] private string sellTotalFormat = "Р {0}";

        private CurrencyId _activeCurrency;
        private int _maxBuyCount;
        private int _maxSellCount;

        private void Awake()
        {
            if (profile == null)
                profile = FindFirstObjectByType<PlayerProfile>();

            if (market == null)
                market = FindFirstObjectByType<CurrencyMarket>();

            ConfigureSlider(buySlider);
            ConfigureSlider(sellSlider);
        }

        private void OnEnable()
        {
            if (profile != null)
            {
                profile.RublesChanged += OnRublesChanged;
                profile.HoldingsChanged += OnHoldingsChanged;
            }

            if (market != null)
            {
                market.ActiveCurrencyChanged += OnActiveCurrencyChanged;
                market.PriceChanged += OnPriceChanged;
                _activeCurrency = market.ActiveCurrency;
            }

            if (buyButton != null) buyButton.onClick.AddListener(OnBuyClicked);
            if (sellButton != null) sellButton.onClick.AddListener(OnSellClicked);
            if (buyMaxButton != null) buyMaxButton.onClick.AddListener(OnBuyMaxClicked);
            if (sellMaxButton != null) sellMaxButton.onClick.AddListener(OnSellMaxClicked);
            if (buySlider != null) buySlider.onValueChanged.AddListener(OnBuySliderChanged);
            if (sellSlider != null) sellSlider.onValueChanged.AddListener(OnSellSliderChanged);

            RefreshHoldings();
            RefreshTransaction();
        }

        private void Start()
        {
            RefreshTransaction();
        }

        private void OnDisable()
        {
            if (profile != null)
            {
                profile.RublesChanged -= OnRublesChanged;
                profile.HoldingsChanged -= OnHoldingsChanged;
            }

            if (market != null)
            {
                market.ActiveCurrencyChanged -= OnActiveCurrencyChanged;
                market.PriceChanged -= OnPriceChanged;
            }

            if (buyButton != null) buyButton.onClick.RemoveListener(OnBuyClicked);
            if (sellButton != null) sellButton.onClick.RemoveListener(OnSellClicked);
            if (buyMaxButton != null) buyMaxButton.onClick.RemoveListener(OnBuyMaxClicked);
            if (sellMaxButton != null) sellMaxButton.onClick.RemoveListener(OnSellMaxClicked);
            if (buySlider != null) buySlider.onValueChanged.RemoveListener(OnBuySliderChanged);
            if (sellSlider != null) sellSlider.onValueChanged.RemoveListener(OnSellSliderChanged);
        }

        private void OnActiveCurrencyChanged(CurrencyId id)
        {
            _activeCurrency = id;
            ResetSlider(buySlider);
            ResetSlider(sellSlider);
            RefreshHoldings();
            RefreshTransaction();
        }

        private void OnPriceChanged(CurrencyId id, float price)
        {
            if (id == _activeCurrency)
                RefreshTransaction();
        }

        private void OnRublesChanged(long _)
        {
            RefreshTransaction();
        }

        private void OnHoldingsChanged(CurrencyId id)
        {
            if (id == _activeCurrency)
            {
                RefreshHoldings();
                RefreshTransaction();
            }
        }

        private void OnBuyClicked()
        {
            var unitPrice = GetUnitPrice();
            var count = ComputeCountFromSlider(buySlider, _maxBuyCount);
            if (count <= 0 || profile == null)
                return;

            if (profile.TryBuy(_activeCurrency, count, unitPrice))
                ResetSlider(buySlider);
        }

        private void OnSellClicked()
        {
            var unitPrice = GetUnitPrice();
            var count = ComputeCountFromSlider(sellSlider, _maxSellCount);
            if (count <= 0 || profile == null)
                return;

            if (profile.TrySell(_activeCurrency, count, unitPrice))
                ResetSlider(sellSlider);
        }

        private void OnBuyMaxClicked()
        {
            if (buySlider == null)
                return;

            buySlider.SetValueWithoutNotify(GetAllowedSliderMax(_maxBuyCount));
            RefreshTransaction();
        }

        private void OnSellMaxClicked()
        {
            if (sellSlider == null)
                return;

            sellSlider.SetValueWithoutNotify(GetAllowedSliderMax(_maxSellCount));
            RefreshTransaction();
        }

        private void OnBuySliderChanged(float _) => RefreshTransaction();
        private void OnSellSliderChanged(float _) => RefreshTransaction();

        private void RefreshTransaction()
        {
            var unitPrice = GetUnitPrice();
            _maxBuyCount = ComputeMaxBuyCount(unitPrice);
            _maxSellCount = ComputeMaxSellCount();

            var cap = profile == null ? 0 : profile.GetCap(_activeCurrency);
            UpdateRow(buySlider, buyMaxButton, buyButton, buyTotalText, buyCountText, buyTotalFormat, _maxBuyCount, cap, unitPrice);
            UpdateRow(sellSlider, sellMaxButton, sellButton, sellTotalText, sellCountText, sellTotalFormat, _maxSellCount, cap, unitPrice);
        }

        private void RefreshHoldings()
        {
            if (holdingsText == null || profile == null)
                return;

            var amount = profile.GetAmount(_activeCurrency);
            var cap = profile.GetCap(_activeCurrency);
            holdingsText.text = string.Format(
                SafeFormat(holdingsFormat, "{0}/{1}"),
                FormatThousands(amount),
                FormatThousands(cap));
        }

        private void UpdateRow(
            Slider slider,
            Button maxButton,
            Button actionButton,
            TMP_Text totalText,
            TMP_Text countText,
            string totalFormat,
            int maxCount,
            int cap,
            long unitPrice)
        {
            ClampSliderToAllowedRange(slider, maxCount);

            var count = ComputeCountFromSlider(slider, maxCount);
            var total = unitPrice * count;

            if (countText != null)
                countText.text = $"{FormatThousands(count)}/{FormatThousands(cap)}";

            if (totalText != null)
                totalText.text = string.Format(SafeFormat(totalFormat, "{0}"), FormatThousands(total));

            if (maxButton != null)
                maxButton.interactable = maxCount > 0;

            if (actionButton != null)
                actionButton.interactable = count > 0;
        }

        private static void ClampSliderToAllowedRange(Slider slider, int maxCount)
        {
            if (slider == null)
                return;

            slider.wholeNumbers = true;

            var allowedMax = GetAllowedSliderMax(maxCount);
            var clamped = Mathf.Clamp(slider.value, slider.minValue, allowedMax);
            if (!Mathf.Approximately(clamped, slider.value))
                slider.SetValueWithoutNotify(clamped);
        }

        private static int GetAllowedSliderMax(int maxCount)
        {
            if (maxCount <= 0)
                return 0;
            return maxCount < SliderSegments ? maxCount : SliderSegments;
        }

        private int ComputeMaxBuyCount(long unitPrice)
        {
            if (profile == null || unitPrice <= 0)
                return 0;

            var capRoom = profile.GetCap(_activeCurrency) - profile.GetAmount(_activeCurrency);
            if (capRoom <= 0)
                return 0;

            var affordable = profile.Rubles / unitPrice;
            if (affordable <= 0)
                return 0;

            return (int)Math.Min(capRoom, affordable);
        }

        private int ComputeMaxSellCount()
        {
            return profile == null ? 0 : profile.GetAmount(_activeCurrency);
        }

        private long GetUnitPrice()
        {
            if (market == null)
                return 0;

            var price = market.GetPrice(_activeCurrency);
            return price <= 0f ? 0 : (long)Math.Round(price);
        }

        private static int ComputeCountFromSlider(Slider slider, int maxCount)
        {
            if (slider == null || maxCount <= 0)
                return 0;

            if (maxCount < SliderSegments)
                return Mathf.Clamp(Mathf.RoundToInt(slider.value), 0, maxCount);

            var raw = Mathf.Clamp(slider.value, 0f, SliderSegments);
            var count = Mathf.RoundToInt(maxCount * (raw / SliderSegments));
            return Mathf.Clamp(count, 0, maxCount);
        }

        private static void ConfigureSlider(Slider slider)
        {
            if (slider == null)
                return;

            slider.minValue = 0;
            slider.maxValue = SliderSegments;
            slider.wholeNumbers = true;
            slider.SetValueWithoutNotify(0);
        }

        private static void ResetSlider(Slider slider)
        {
            if (slider != null)
                slider.SetValueWithoutNotify(0);
        }

        private static string FormatThousands(long value)
        {
            return value
                .ToString("N0", CultureInfo.InvariantCulture)
                .Replace(",", ".");
        }

        private static string SafeFormat(string format, string fallback)
        {
            return string.IsNullOrEmpty(format) ? fallback : format;
        }
    }
}
