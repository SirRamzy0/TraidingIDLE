using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using TraidingIDLE.Currencies;
using TraidingIDLE.Player;
using UnityEngine;
using UnityEngine.EventSystems;
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

        [Header("Active coin value + profit")]
        [SerializeField] private TMP_Text totalValueText = null!;
        [SerializeField] private TMP_Text profitPercentText = null!;
        [SerializeField] private string totalValueFormat = "Общая стоимость: {0}";
        [SerializeField] private string profitPercentFormat = "{0}%";
        [SerializeField] private Color profitPositiveColor = new(0.18f, 1f, 0.68f);
        [SerializeField] private Color profitNegativeColor = new(1f, 0.25f, 0.38f);
        [SerializeField] private Color profitNeutralColor = new(0.85f, 0.85f, 0.85f);

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

        [Header("Unavailable feedback")]
        [SerializeField] private Color unavailableFlashColor = new(1f, 0.18f, 0.25f, 1f);
        [SerializeField, Min(0.05f)] private float unavailableFlashDuration = 0.35f;
        [SerializeField, Min(1)] private int unavailableFlashPulses = 2;

        private CurrencyId _activeCurrency;
        private int _maxBuyCount;
        private int _maxSellCount;
        private readonly Dictionary<CurrencyId, float> _buySliderValues = new();
        private readonly Dictionary<CurrencyId, float> _sellSliderValues = new();
        private readonly Dictionary<Graphic, Coroutine> _feedbackCoroutines = new();
        private readonly Dictionary<Graphic, Color> _feedbackOriginalColors = new();

        private void Awake()
        {
            if (profile == null)
                profile = FindFirstObjectByType<PlayerProfile>();

            if (market == null)
                market = FindFirstObjectByType<CurrencyMarket>();

            ConfigureSlider(buySlider);
            ConfigureSlider(sellSlider);
            RegisterPointerDown(buySlider, OnBuySliderPointerDown);
            RegisterPointerDown(sellSlider, OnSellSliderPointerDown);
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

            RestoreSliderState(_activeCurrency);

            if (buyButton != null) buyButton.onClick.AddListener(OnBuyClicked);
            if (sellButton != null) sellButton.onClick.AddListener(OnSellClicked);
            if (buyMaxButton != null) buyMaxButton.onClick.AddListener(OnBuyMaxClicked);
            if (sellMaxButton != null) sellMaxButton.onClick.AddListener(OnSellMaxClicked);
            if (buySlider != null) buySlider.onValueChanged.AddListener(OnBuySliderChanged);
            if (sellSlider != null) sellSlider.onValueChanged.AddListener(OnSellSliderChanged);

            RefreshHoldings();
            RefreshValueAndProfit();
            RefreshTransaction();
        }

        private void Start()
        {
            RefreshTransaction();
        }

        private void OnDisable()
        {
            SaveActiveSliderState();

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
            SaveActiveSliderState();
            _activeCurrency = id;
            RestoreSliderState(id);
            RefreshHoldings();
            RefreshValueAndProfit();
            RefreshTransaction();
        }

        private void OnPriceChanged(CurrencyId id, float price)
        {
            if (id == _activeCurrency)
            {
                RefreshValueAndProfit();
                RefreshTransaction();
            }
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
                RefreshValueAndProfit();
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
            {
                ResetActiveBuySlider();
            }
            else
            {
                FlashUnavailable(buyButton);
                FlashUnavailable(buySlider);
            }
        }

        private void OnSellClicked()
        {
            var unitPrice = GetUnitPrice();
            var count = ComputeCountFromSlider(sellSlider, _maxSellCount);
            if (count <= 0 || profile == null)
                return;

            if (profile.TrySell(_activeCurrency, count, unitPrice))
            {
                ResetActiveSellSlider();
            }
            else
            {
                FlashUnavailable(sellButton);
                FlashUnavailable(sellSlider);
            }
        }

        private void OnBuyMaxClicked()
        {
            if (buySlider == null)
                return;

            if (_maxBuyCount <= 0)
            {
                FlashUnavailable(buyMaxButton);
                FlashUnavailable(buySlider);
                return;
            }

            buySlider.SetValueWithoutNotify(GetAllowedSliderMax(_maxBuyCount));
            SaveActiveSliderState();
            RefreshTransaction();
        }

        private void OnSellMaxClicked()
        {
            if (sellSlider == null)
                return;

            if (_maxSellCount <= 0)
            {
                FlashUnavailable(sellMaxButton);
                FlashUnavailable(sellSlider);
                return;
            }

            sellSlider.SetValueWithoutNotify(GetAllowedSliderMax(_maxSellCount));
            SaveActiveSliderState();
            RefreshTransaction();
        }

        private void OnBuySliderChanged(float value)
        {
            if (IsUnavailableSliderAttempt(value, _maxBuyCount))
                FlashUnavailable(buySlider);

            SaveActiveSliderState();
            RefreshTransaction();
        }

        private void OnSellSliderChanged(float value)
        {
            if (IsUnavailableSliderAttempt(value, _maxSellCount))
                FlashUnavailable(sellSlider);

            SaveActiveSliderState();
            RefreshTransaction();
        }

        private void OnBuySliderPointerDown()
        {
            if (_maxBuyCount <= 0)
                FlashUnavailable(buySlider);
        }

        private void OnSellSliderPointerDown()
        {
            if (_maxSellCount <= 0)
                FlashUnavailable(sellSlider);
        }

        private void RefreshTransaction()
        {
            var unitPrice = GetUnitPrice();
            _maxBuyCount = ComputeMaxBuyCount(unitPrice);
            _maxSellCount = ComputeMaxSellCount();

            var cap = profile == null ? 0 : profile.GetCap(_activeCurrency);
            UpdateRow(buySlider, buyMaxButton, buyButton, buyTotalText, buyCountText, buyTotalFormat, _maxBuyCount, cap, unitPrice);
            UpdateRow(sellSlider, sellMaxButton, sellButton, sellTotalText, sellCountText, sellTotalFormat, _maxSellCount, cap, unitPrice);
            SaveActiveSliderState();
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

        private void RefreshValueAndProfit()
        {
            if (profile == null || market == null)
                return;

            var amount = profile.GetAmount(_activeCurrency);
            var unitPrice = GetUnitPrice();
            var totalValue = unitPrice * Math.Max(0, amount);
            var invested = profile.GetInvestedRubles(_activeCurrency);

            if (totalValueText != null)
                totalValueText.text = string.Format(SafeFormat(totalValueFormat, "{0}"), FormatThousands(totalValue));

            if (profitPercentText != null)
            {
                if (invested <= 0)
                {
                    profitPercentText.text = string.Format(SafeFormat(profitPercentFormat, "{0}%"), "+0");
                    profitPercentText.color = profitNeutralColor;
                    return;
                }

                var diff = (double)(totalValue - invested);
                var pct = diff / invested * 100.0;
                var pctInt = (int)Math.Round(pct);
                var sign = pctInt >= 0 ? "+" : "";
                profitPercentText.text = string.Format(SafeFormat(profitPercentFormat, "{0}%"), $"{sign}{pctInt}");
                profitPercentText.color = pctInt > 0 ? profitPositiveColor : (pctInt < 0 ? profitNegativeColor : profitNeutralColor);
            }
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
                maxButton.interactable = true;

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

            var price = CurrencyMarket.SanitizePrice(market.GetPrice(_activeCurrency));
            return Math.Max(1L, (long)Math.Round(price));
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

        private void SaveActiveSliderState()
        {
            if (buySlider != null)
                _buySliderValues[_activeCurrency] = buySlider.value;
            if (sellSlider != null)
                _sellSliderValues[_activeCurrency] = sellSlider.value;
        }

        private void RestoreSliderState(CurrencyId id)
        {
            if (buySlider != null)
                buySlider.SetValueWithoutNotify(_buySliderValues.TryGetValue(id, out var buyValue) ? buyValue : 0f);
            if (sellSlider != null)
                sellSlider.SetValueWithoutNotify(_sellSliderValues.TryGetValue(id, out var sellValue) ? sellValue : 0f);
        }

        private void ResetActiveBuySlider()
        {
            ResetSlider(buySlider);
            _buySliderValues[_activeCurrency] = 0f;
            RefreshTransaction();
        }

        private void ResetActiveSellSlider()
        {
            ResetSlider(sellSlider);
            _sellSliderValues[_activeCurrency] = 0f;
            RefreshTransaction();
        }

        private static bool IsUnavailableSliderAttempt(float value, int maxCount)
        {
            return maxCount <= 0 || value > GetAllowedSliderMax(maxCount) + 0.001f;
        }

        private static void RegisterPointerDown(Component target, Action callback)
        {
            if (target == null || callback == null)
                return;

            var eventTrigger = target.GetComponent<EventTrigger>();
            if (eventTrigger == null)
                eventTrigger = target.gameObject.AddComponent<EventTrigger>();

            eventTrigger.triggers ??= new List<EventTrigger.Entry>();

            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            entry.callback.AddListener(_ => callback());
            eventTrigger.triggers.Add(entry);
        }

        private void FlashUnavailable(Button button)
        {
            if (button == null)
                return;

            FlashGraphic(button.targetGraphic);
        }

        private void FlashUnavailable(Slider slider)
        {
            if (slider == null)
                return;

            FlashGraphic(GetSliderHandleGraphic(slider));
        }

        private static Graphic GetSliderHandleGraphic(Slider slider)
        {
            if (slider == null)
                return null;

            if (slider.handleRect != null && slider.handleRect.TryGetComponent<Graphic>(out var handleGraphic))
                return handleGraphic;

            return slider.targetGraphic;
        }

        private void FlashGraphic(Graphic graphic)
        {
            if (graphic == null)
                return;

            if (!_feedbackOriginalColors.ContainsKey(graphic))
                _feedbackOriginalColors[graphic] = graphic.color;

            if (_feedbackCoroutines.TryGetValue(graphic, out var existing) && existing != null)
                StopCoroutine(existing);

            _feedbackCoroutines[graphic] = StartCoroutine(FlashGraphicRoutine(graphic));
        }

        private IEnumerator FlashGraphicRoutine(Graphic graphic)
        {
            var baseColor = _feedbackOriginalColors.TryGetValue(graphic, out var stored)
                ? stored
                : graphic.color;

            var duration = Mathf.Max(0.05f, unavailableFlashDuration);
            var pulses = Mathf.Max(1, unavailableFlashPulses);
            var elapsed = 0f;

            while (elapsed < duration && graphic != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var wave = Mathf.Sin(t * Mathf.PI * pulses);
                var strength = Mathf.Clamp01(wave);
                graphic.color = Color.Lerp(baseColor, unavailableFlashColor, strength);
                yield return null;
            }

            if (graphic != null)
            {
                graphic.color = baseColor;
                _feedbackCoroutines.Remove(graphic);
                _feedbackOriginalColors.Remove(graphic);
            }
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
