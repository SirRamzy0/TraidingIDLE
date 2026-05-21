using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using TraidingIDLE.Currencies;
using TraidingIDLE.Integrations;
using TraidingIDLE.Localization;
using TraidingIDLE.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TraidingIDLE.UI
{
    public sealed class TradingPanelUI : MonoBehaviour
    {
        private const int SliderSegments = 10;

        [Serializable]
        private sealed class CapUpgradeRule
        {
            public CurrencyId id;
            [Min(1)] public int baseCap = 500;
            [Min(1)] public int capIncrease = 500;
            [Min(0)] public long baseGemCost = 10;
            [Min(1f)] public float costGrowth = 1.35f;
        }

        [Header("Refs")]
        [SerializeField] private PlayerProfile profile;
        [SerializeField] private CurrencyMarket market;

        [Header("Holdings label (active coin)")]
        [SerializeField] private TMP_Text holdingsText;
        [SerializeField] private string holdingsFormat = "кол-во: {0}/{1}";

        [Header("Active coin value + profit")]
        [SerializeField] private TMP_Text totalValueText;
        [SerializeField] private TMP_Text profitPercentText;
        [SerializeField] private string totalValueFormat = "Общая стоимость: {0}";
        [SerializeField] private string profitPercentFormat = "{0}%";
        [SerializeField] private Color profitPositiveColor = new(0.18f, 1f, 0.68f);
        [SerializeField] private Color profitNegativeColor = new(1f, 0.25f, 0.38f);
        [SerializeField] private Color profitNeutralColor = new(0.85f, 0.85f, 0.85f);

        [Header("Buy")]
        [SerializeField] private Button buyButton;
        [SerializeField] private Slider buySlider;
        [SerializeField] private Button buyMaxButton;
        [SerializeField] private TMP_Text buyTotalText;
        [SerializeField] private TMP_Text buyCountText;
        [SerializeField] private string buyTotalFormat = "Р {0}";

        [Header("Sell")]
        [SerializeField] private Button sellButton;
        [SerializeField] private Slider sellSlider;
        [SerializeField] private Button sellMaxButton;
        [SerializeField] private TMP_Text sellTotalText;
        [SerializeField] private TMP_Text sellCountText;
        [SerializeField] private string sellTotalFormat = "Р {0}";

        [Header("Unavailable feedback")]
        [SerializeField] private Color unavailableFlashColor = new(1f, 0.18f, 0.25f, 1f);
        [SerializeField, Min(0.05f)] private float unavailableFlashDuration = 0.35f;
        [SerializeField, Min(1)] private int unavailableFlashPulses = 2;

        [Header("Cap Upgrade")]
        [SerializeField] private Button capUpgradeOpenButton;
        [SerializeField] private RectTransform capUpgradeDialogRoot;
        [SerializeField] private Button capUpgradeCloseButton;
        [SerializeField] private Button capUpgradeOutsideCloseButton;
        [SerializeField] private Button capUpgradeBuyButton;
        [SerializeField] private TMP_Text capUpgradeDescriptionText;
        [SerializeField] private TMP_Text capUpgradeCurrentLimitText;
        [SerializeField] private TMP_Text capUpgradeBuyPriceText;

        [SerializeField] private string capUpgradeDescriptionFormat = "Увеличить с {0} до {1}";
        [SerializeField] private string capUpgradeCurrentLimitFormat = "Текущий лимит {0} - {1} монет";
        [SerializeField] private string capUpgradeBuyPriceFormat = "{0}";

        [SerializeField, Min(1)] private int capUpgradeCostStep = 5;
        [SerializeField] private CapUpgradeRule[] capUpgradeRules =
        {
            new CapUpgradeRule { id = CurrencyId.SHT, baseCap = 500, capIncrease = 500, baseGemCost = 10, costGrowth = 1.35f },
            new CapUpgradeRule { id = CurrencyId.ETH, baseCap = 150, capIncrease = 150, baseGemCost = 15, costGrowth = 1.35f },
            new CapUpgradeRule { id = CurrencyId.BTC, baseCap = 50, capIncrease = 50, baseGemCost = 20, costGrowth = 1.35f },
        };

        [Header("SHT First Upgrade Ad")]
        [SerializeField] private bool shtFirstCapUpgradeByAd = true;
        [SerializeField] private string shtFirstCapUpgradeRewardedId = "sht_first_cap_upgrade";
        [SerializeField] private string capUpgradeAdPriceText = "Реклама";

        [Header("Cap Upgrade Position")]
        [SerializeField] private bool capUpgradeAutoPosition = true;
        [SerializeField] private RectTransform capUpgradeAnchor;
        [SerializeField] private Vector2 capUpgradeDialogOffset = new(0f, -12f);

        private CurrencyId _activeCurrency;
        private int _maxBuyCount;
        private int _maxSellCount;
        private bool _capUpgradeDialogOpen;
        private Coroutine _capUpgradePositionRoutine;

        private readonly Dictionary<CurrencyId, float> _buySliderValues = new();
        private readonly Dictionary<CurrencyId, float> _sellSliderValues = new();
        private readonly Dictionary<Graphic, Coroutine> _feedbackCoroutines = new();
        private readonly Dictionary<Graphic, Color> _feedbackOriginalColors = new();

        public event Action BuyButtonClicked;
        public Button BuyButton => buyButton;

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
            LocalizationManager.LanguageChanged += OnLanguageChanged;

            if (profile != null)
            {
                profile.RublesChanged += OnRublesChanged;
                profile.GemsChanged += OnGemsChanged;
                profile.HoldingsChanged += OnHoldingsChanged;
            }

            if (market != null)
            {
                market.ActiveCurrencyChanged += OnActiveCurrencyChanged;
                market.PriceChanged += OnPriceChanged;
                _activeCurrency = market.ActiveCurrency;
            }

            RestoreSliderState(_activeCurrency);

            if (buyButton != null)
                buyButton.onClick.AddListener(OnBuyClicked);

            if (sellButton != null)
                sellButton.onClick.AddListener(OnSellClicked);

            if (buyMaxButton != null)
                buyMaxButton.onClick.AddListener(OnBuyMaxClicked);

            if (sellMaxButton != null)
                sellMaxButton.onClick.AddListener(OnSellMaxClicked);

            if (buySlider != null)
                buySlider.onValueChanged.AddListener(OnBuySliderChanged);

            if (sellSlider != null)
                sellSlider.onValueChanged.AddListener(OnSellSliderChanged);

            if (capUpgradeOpenButton != null)
                capUpgradeOpenButton.onClick.AddListener(OnCapUpgradeOpenClicked);

            if (capUpgradeCloseButton != null)
                capUpgradeCloseButton.onClick.AddListener(CloseCapUpgradeDialog);

            if (capUpgradeOutsideCloseButton != null)
                capUpgradeOutsideCloseButton.onClick.AddListener(CloseCapUpgradeDialog);

            if (capUpgradeBuyButton != null)
                capUpgradeBuyButton.onClick.AddListener(OnCapUpgradeBuyClicked);

            SetCapUpgradeDialogOpen(false);

            RefreshHoldings();
            RefreshValueAndProfit();
            RefreshTransaction();
            RefreshCapUpgradeUi();
        }

        private void Start()
        {
            RefreshTransaction();
            RefreshCapUpgradeUi();
        }

        private void OnDisable()
        {
            SaveActiveSliderState();

            if (_capUpgradePositionRoutine != null)
            {
                StopCoroutine(_capUpgradePositionRoutine);
                _capUpgradePositionRoutine = null;
            }

            if (profile != null)
            {
                profile.RublesChanged -= OnRublesChanged;
                profile.GemsChanged -= OnGemsChanged;
                profile.HoldingsChanged -= OnHoldingsChanged;
            }

            if (market != null)
            {
                market.ActiveCurrencyChanged -= OnActiveCurrencyChanged;
                market.PriceChanged -= OnPriceChanged;
            }

            if (buyButton != null)
                buyButton.onClick.RemoveListener(OnBuyClicked);

            if (sellButton != null)
                sellButton.onClick.RemoveListener(OnSellClicked);

            if (buyMaxButton != null)
                buyMaxButton.onClick.RemoveListener(OnBuyMaxClicked);

            if (sellMaxButton != null)
                sellMaxButton.onClick.RemoveListener(OnSellMaxClicked);

            if (buySlider != null)
                buySlider.onValueChanged.RemoveListener(OnBuySliderChanged);

            if (sellSlider != null)
                sellSlider.onValueChanged.RemoveListener(OnSellSliderChanged);

            if (capUpgradeOpenButton != null)
                capUpgradeOpenButton.onClick.RemoveListener(OnCapUpgradeOpenClicked);

            if (capUpgradeCloseButton != null)
                capUpgradeCloseButton.onClick.RemoveListener(CloseCapUpgradeDialog);

            if (capUpgradeOutsideCloseButton != null)
                capUpgradeOutsideCloseButton.onClick.RemoveListener(CloseCapUpgradeDialog);

            if (capUpgradeBuyButton != null)
                capUpgradeBuyButton.onClick.RemoveListener(OnCapUpgradeBuyClicked);

            LocalizationManager.LanguageChanged -= OnLanguageChanged;
        }

        private void OnLanguageChanged()
        {
            RefreshHoldings();
            RefreshValueAndProfit();
            RefreshTransaction();
            RefreshCapUpgradeUi();
        }

        private void OnActiveCurrencyChanged(CurrencyId id)
        {
            SaveActiveSliderState();

            _activeCurrency = id;
            RestoreSliderState(id);

            CloseCapUpgradeDialog();

            RefreshHoldings();
            RefreshValueAndProfit();
            RefreshTransaction();
            RefreshCapUpgradeUi();
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

        private void OnGemsChanged(long _)
        {
            RefreshCapUpgradeUi();
        }

        private void OnHoldingsChanged(CurrencyId id)
        {
            if (id == _activeCurrency)
            {
                RefreshHoldings();
                RefreshValueAndProfit();
                RefreshTransaction();
                RefreshCapUpgradeUi();
            }
        }

        private void OnBuyClicked()
        {
            BuyButtonClicked?.Invoke();

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

            UpdateRow(
                buySlider,
                buyMaxButton,
                buyButton,
                buyTotalText,
                buyCountText,
                buyTotalFormat,
                _maxBuyCount,
                cap,
                unitPrice);

            UpdateRow(
                sellSlider,
                sellMaxButton,
                sellButton,
                sellTotalText,
                sellCountText,
                sellTotalFormat,
                _maxSellCount,
                cap,
                unitPrice);

            SaveActiveSliderState();
        }

        private void RefreshHoldings()
        {
            if (holdingsText == null || profile == null)
                return;

            var amount = profile.GetAmount(_activeCurrency);
            var cap = profile.GetCap(_activeCurrency);

            holdingsText.text = string.Format(
                SafeFormat(LocalizationManager.Tr("trading.holdings_format", holdingsFormat), "{0}/{1}"),
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
            {
                totalValueText.text = string.Format(
                    SafeFormat(LocalizationManager.Tr("trading.total_value_format", totalValueFormat), "{0}"),
                    FormatThousands(totalValue));
            }

            if (profitPercentText != null)
            {
                if (invested <= 0)
                {
                    profitPercentText.text = string.Format(
                        SafeFormat(profitPercentFormat, "{0}%"),
                        "+0");

                    profitPercentText.color = profitNeutralColor;
                    return;
                }

                var diff = (double)(totalValue - invested);
                var pct = diff / invested * 100.0;
                var pctInt = (int)Math.Round(pct);
                var sign = pctInt >= 0 ? "+" : "";

                profitPercentText.text = string.Format(
                    SafeFormat(profitPercentFormat, "{0}%"),
                    $"{sign}{pctInt}");

                profitPercentText.color = pctInt > 0
                    ? profitPositiveColor
                    : pctInt < 0
                        ? profitNegativeColor
                        : profitNeutralColor;
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
            {
                totalText.text = string.Format(
                    SafeFormat(
                        LocalizationManager.Tr(totalFormat == buyTotalFormat ? "trading.buy_total_format" : "trading.sell_total_format", totalFormat),
                        "{0}"),
                    FormatThousands(total));
            }

            if (maxButton != null)
                maxButton.interactable = true;

            if (actionButton != null)
                actionButton.interactable = count > 0;
        }

        private void OnCapUpgradeOpenClicked()
        {
            SetCapUpgradeDialogOpen(true);
        }

        private void CloseCapUpgradeDialog()
        {
            SetCapUpgradeDialogOpen(false);
        }

        private void SetCapUpgradeDialogOpen(bool open)
        {
            _capUpgradeDialogOpen = open;

            if (_capUpgradePositionRoutine != null)
            {
                StopCoroutine(_capUpgradePositionRoutine);
                _capUpgradePositionRoutine = null;
            }

            if (!open)
            {
                if (capUpgradeOutsideCloseButton != null)
                    capUpgradeOutsideCloseButton.gameObject.SetActive(false);

                if (capUpgradeDialogRoot != null)
                    capUpgradeDialogRoot.gameObject.SetActive(false);

                return;
            }

            RefreshCapUpgradeUi();

            Canvas.ForceUpdateCanvases();

            if (capUpgradeDialogRoot != null)
            {
                // Важно: диалог ещё выключен, поэтому игрок не видит старую позицию.
                capUpgradeDialogRoot.gameObject.SetActive(false);

                PositionCapUpgradeDialog();

                // Включаем уже после позиционирования.
                capUpgradeDialogRoot.gameObject.SetActive(true);
            }

            if (capUpgradeOutsideCloseButton != null)
                capUpgradeOutsideCloseButton.gameObject.SetActive(true);
        }

        private void RefreshCapUpgradeUi()
        {
            if (capUpgradeOpenButton != null)
            {
                capUpgradeOpenButton.interactable = profile != null;
                var openButtonText = capUpgradeOpenButton.GetComponentInChildren<TMP_Text>(true);
                if (openButtonText != null)
                    openButtonText.text = LocalizationManager.Tr("common.increase", "Увеличить");
            }

            if (profile == null)
            {
                if (capUpgradeBuyButton != null)
                    capUpgradeBuyButton.interactable = false;

                if (capUpgradeDescriptionText != null)
                    capUpgradeDescriptionText.text = "";

                if (capUpgradeCurrentLimitText != null)
                    capUpgradeCurrentLimitText.text = "";

                if (capUpgradeBuyPriceText != null)
                    capUpgradeBuyPriceText.text = "";

                return;
            }

            var currentCap = profile.GetCap(_activeCurrency);
            var increase = GetCapUpgradeIncrease(_activeCurrency);
            var nextCap = currentCap > int.MaxValue - increase
                ? int.MaxValue
                : currentCap + increase;

            var cost = GetCapUpgradeGemCost(_activeCurrency, currentCap);
            var useRewardedAd = IsFirstShtCapUpgradeAvailableByAd();

            if (capUpgradeDescriptionText != null)
            {
                capUpgradeDescriptionText.text = string.Format(
                    SafeFormat(LocalizationManager.Tr("trading.cap_upgrade_description_format", capUpgradeDescriptionFormat), "Увеличить с {0} до {1}"),
                    FormatThousands(currentCap),
                    FormatThousands(nextCap));
            }

            if (capUpgradeCurrentLimitText != null)
            {
                capUpgradeCurrentLimitText.text = string.Format(
                    SafeFormat(LocalizationManager.Tr("trading.cap_upgrade_current_limit_format", capUpgradeCurrentLimitFormat), "Текущий лимит {0} - {1} монет"),
                    _activeCurrency.ToString(),
                    FormatThousands(currentCap));
            }

            if (capUpgradeBuyPriceText != null)
            {
                capUpgradeBuyPriceText.text = useRewardedAd
                    ? LocalizationManager.Tr("common.ad", capUpgradeAdPriceText)
                    : string.Format(
                        SafeFormat(capUpgradeBuyPriceFormat, "{0}"),
                        FormatThousands(cost));
            }

            if (capUpgradeBuyButton != null)
            {
                capUpgradeBuyButton.interactable =
                    nextCap > currentCap &&
                    (useRewardedAd || profile.Gems >= cost);
            }
        }

        private void OnCapUpgradeBuyClicked()
        {
            if (profile == null)
                return;

            if (IsFirstShtCapUpgradeAvailableByAd())
            {
                YandexRewardedAds.Show(
                    string.IsNullOrWhiteSpace(shtFirstCapUpgradeRewardedId)
                        ? "sht_first_cap_upgrade"
                        : shtFirstCapUpgradeRewardedId,
                    OnShtFirstCapUpgradeAdRewarded);

                return;
            }

            var currentCap = profile.GetCap(_activeCurrency);
            var increase = GetCapUpgradeIncrease(_activeCurrency);

            if (currentCap >= int.MaxValue || increase <= 0)
                return;

            var nextCap = currentCap > int.MaxValue - increase
                ? int.MaxValue
                : currentCap + increase;

            var cost = GetCapUpgradeGemCost(_activeCurrency, currentCap);

            if (!profile.TrySpendGems(cost))
            {
                FlashUnavailable(capUpgradeBuyButton);
                return;
            }

            profile.SetCap(_activeCurrency, nextCap);

            RefreshHoldings();
            RefreshTransaction();
            RefreshCapUpgradeUi();
        }

        private bool IsFirstShtCapUpgradeAvailableByAd()
        {
            if (!shtFirstCapUpgradeByAd || profile == null || _activeCurrency != CurrencyId.SHT)
                return false;

            var rule = GetCapUpgradeRule(CurrencyId.SHT);
            var baseCap = Mathf.Max(1, rule?.baseCap ?? 500);

            return profile.GetCap(CurrencyId.SHT) <= baseCap;
        }

        private void OnShtFirstCapUpgradeAdRewarded()
        {
            if (profile == null)
                return;

            var rule = GetCapUpgradeRule(CurrencyId.SHT);
            var baseCap = Mathf.Max(1, rule?.baseCap ?? 500);
            var currentCap = profile.GetCap(CurrencyId.SHT);

            if (currentCap > baseCap)
            {
                RefreshCapUpgradeUi();
                return;
            }

            var increase = GetCapUpgradeIncrease(CurrencyId.SHT);
            if (increase <= 0 || currentCap >= int.MaxValue)
                return;

            var nextCap = currentCap > int.MaxValue - increase
                ? int.MaxValue
                : currentCap + increase;

            profile.SetCap(CurrencyId.SHT, nextCap);

            if (_activeCurrency == CurrencyId.SHT)
            {
                RefreshHoldings();
                RefreshTransaction();
            }

            RefreshCapUpgradeUi();
        }

        private int GetCapUpgradeIncrease(CurrencyId id)
        {
            var rule = GetCapUpgradeRule(id);
            return Mathf.Max(1, rule?.capIncrease ?? 500);
        }

        private long GetCapUpgradeGemCost(CurrencyId id, int currentCap)
        {
            var rule = GetCapUpgradeRule(id);

            var baseCap = Mathf.Max(1, rule?.baseCap ?? currentCap);
            var increase = Mathf.Max(1, rule?.capIncrease ?? 500);
            var baseCost = Math.Max(0, rule?.baseGemCost ?? 10);
            var growth = Mathf.Max(1f, rule?.costGrowth ?? 1.35f);

            var upgradeCount = Mathf.Max(
                0,
                Mathf.FloorToInt((currentCap - baseCap) / (float)increase));

            var rawCost = baseCost * Math.Pow(growth, upgradeCount);

            return RoundUpToStep(rawCost, capUpgradeCostStep);
        }

        private CapUpgradeRule GetCapUpgradeRule(CurrencyId id)
        {
            if (capUpgradeRules != null)
            {
                for (var i = 0; i < capUpgradeRules.Length; i++)
                {
                    var rule = capUpgradeRules[i];
                    if (rule != null && rule.id == id)
                        return rule;
                }
            }

            return id switch
            {
                CurrencyId.SHT => new CapUpgradeRule
                {
                    id = CurrencyId.SHT,
                    baseCap = 500,
                    capIncrease = 500,
                    baseGemCost = 10,
                    costGrowth = 1.35f,
                },
                CurrencyId.ETH => new CapUpgradeRule
                {
                    id = CurrencyId.ETH,
                    baseCap = 150,
                    capIncrease = 150,
                    baseGemCost = 15,
                    costGrowth = 1.35f,
                },
                CurrencyId.BTC => new CapUpgradeRule
                {
                    id = CurrencyId.BTC,
                    baseCap = 50,
                    capIncrease = 50,
                    baseGemCost = 20,
                    costGrowth = 1.35f,
                },
                _ => new CapUpgradeRule
                {
                    id = id,
                    baseCap = 500,
                    capIncrease = 500,
                    baseGemCost = 10,
                    costGrowth = 1.35f,
                },
            };
        }

        private static long RoundUpToStep(double value, int step)
        {
            step = Mathf.Max(1, step);

            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0d)
                return step;

            if (value >= long.MaxValue)
                return long.MaxValue;

            return (long)(Math.Ceiling(value / step) * step);
        }

        private void PositionCapUpgradeDialog()
{
    if (!capUpgradeAutoPosition || capUpgradeDialogRoot == null)
        return;

    var anchor = capUpgradeAnchor;
    if (anchor == null && capUpgradeOpenButton != null)
        anchor = capUpgradeOpenButton.transform as RectTransform;

    var parent = capUpgradeDialogRoot.parent as RectTransform;
    if (anchor == null || parent == null)
        return;

    // Защита: нельзя позиционировать диалог относительно самого себя
    // или объекта внутри самого диалога, иначе он будет уезжать при каждом открытии.
    if (anchor == capUpgradeDialogRoot || anchor.IsChildOf(capUpgradeDialogRoot))
    {
        if (capUpgradeOpenButton == null)
            return;

        anchor = capUpgradeOpenButton.transform as RectTransform;

        if (anchor == null || anchor == capUpgradeDialogRoot || anchor.IsChildOf(capUpgradeDialogRoot))
            return;
    }

    // Важно: сбрасываем позицию перед новым расчетом,
    // чтобы offset не накапливался от прошлого открытия.
    capUpgradeDialogRoot.anchoredPosition = Vector2.zero;

    var anchorCanvas = anchor.GetComponentInParent<Canvas>();
    var parentCanvas = parent.GetComponentInParent<Canvas>();

    var anchorCamera =
        anchorCanvas != null && anchorCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? anchorCanvas.worldCamera
            : null;

    var parentCamera =
        parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? parentCanvas.worldCamera
            : null;

    var corners = new Vector3[4];
    anchor.GetWorldCorners(corners);

    var bottomCenterWorld = (corners[0] + corners[3]) * 0.5f;

    var screenPoint = RectTransformUtility.WorldToScreenPoint(
        anchorCamera,
        bottomCenterWorld);

    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parent,
            screenPoint,
            parentCamera,
            out var localPoint))
    {
        capUpgradeDialogRoot.anchoredPosition = localPoint + capUpgradeDialogOffset;
    }
}

        private static void ClampSliderToAllowedRange(Slider slider, int maxCount)
        {
            if (slider == null)
                return;

            slider.wholeNumbers = true;
            slider.minValue = 1;

            var allowedMax = GetAllowedSliderMax(maxCount);
            slider.maxValue = allowedMax;

            var clamped = Mathf.Clamp(slider.value, 1f, allowedMax);

            if (!Mathf.Approximately(clamped, slider.value))
                slider.SetValueWithoutNotify(clamped);
        }

        private static int GetAllowedSliderMax(int maxCount)
        {
            if (maxCount <= 0)
                return 1;

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
                return 1;

            if (maxCount < SliderSegments)
                return Mathf.Clamp(Mathf.RoundToInt(slider.value), 1, maxCount);

            var raw = Mathf.Clamp(slider.value, 1f, SliderSegments);
            var normalized = (raw - 1f) / (SliderSegments - 1f);
            var count = 1 + Mathf.RoundToInt((maxCount - 1) * normalized);

            return Mathf.Clamp(count, 1, maxCount);
        }

        private static void ConfigureSlider(Slider slider)
        {
            if (slider == null)
                return;

            slider.minValue = 1;
            slider.maxValue = SliderSegments;
            slider.wholeNumbers = true;
            slider.SetValueWithoutNotify(1);
        }

        private static void ResetSlider(Slider slider)
        {
            if (slider != null)
                slider.SetValueWithoutNotify(1);
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
            {
                buySlider.SetValueWithoutNotify(
                    _buySliderValues.TryGetValue(id, out var buyValue) ? buyValue : 1f);
            }

            if (sellSlider != null)
            {
                sellSlider.SetValueWithoutNotify(
                    _sellSliderValues.TryGetValue(id, out var sellValue) ? sellValue : 1f);
            }
        }

        private void ResetActiveBuySlider()
        {
            ResetSlider(buySlider);
            _buySliderValues[_activeCurrency] = 1f;
            RefreshTransaction();
        }

        private void ResetActiveSellSlider()
        {
            ResetSlider(sellSlider);
            _sellSliderValues[_activeCurrency] = 1f;
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

            var entry = new EventTrigger.Entry
            {
                eventID = EventTriggerType.PointerDown,
            };

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

            if (slider.handleRect != null &&
                slider.handleRect.TryGetComponent<Graphic>(out var handleGraphic))
            {
                return handleGraphic;
            }

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
