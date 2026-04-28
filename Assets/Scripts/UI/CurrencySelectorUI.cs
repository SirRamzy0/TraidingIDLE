using TMPro;
using TraidingIDLE.Currencies;
using UnityEngine;
using UnityEngine.UI;

namespace TraidingIDLE.UI
{
    public sealed class CurrencySelectorUI : MonoBehaviour
    {
        [Header("Market")]
        [SerializeField] private CurrencyMarket market = null!;

        [Header("Label")]
        [SerializeField] private TMP_Text activeCurrencyText = null!;

        [Header("Label names")]
        [SerializeField] private string shtDisplayName = "ScamHypeToken";
        [SerializeField] private string ethDisplayName = "EveryoneTradesHere";
        [SerializeField] private string btcDisplayName = "BratCoin";

        [Header("Buttons")]
        [SerializeField] private Button shtButton = null!;
        [SerializeField] private Button ethButton = null!;
        [SerializeField] private Button btcButton = null!;

        [Header("Button Images (for sprite swap)")]
        [SerializeField] private Image shtButtonImage = null!;
        [SerializeField] private Image ethButtonImage = null!;
        [SerializeField] private Image btcButtonImage = null!;

        [Header("Sprites")]
        [SerializeField] private Sprite inactiveSprite = null!;
        [SerializeField] private Sprite activeSprite = null!;

        private void Awake()
        {
            if (market == null)
                market = FindFirstObjectByType<CurrencyMarket>();
        }

        private void OnEnable()
        {
            if (shtButton != null) shtButton.onClick.AddListener(OnShtClicked);
            if (ethButton != null) ethButton.onClick.AddListener(OnEthClicked);
            if (btcButton != null) btcButton.onClick.AddListener(OnBtcClicked);

            if (market != null)
                market.ActiveCurrencyChanged += OnActiveCurrencyChanged;

            SyncFromMarket();
        }

        private void OnDisable()
        {
            if (shtButton != null) shtButton.onClick.RemoveListener(OnShtClicked);
            if (ethButton != null) ethButton.onClick.RemoveListener(OnEthClicked);
            if (btcButton != null) btcButton.onClick.RemoveListener(OnBtcClicked);

            if (market != null)
                market.ActiveCurrencyChanged -= OnActiveCurrencyChanged;
        }

        private void OnShtClicked()
        {
            if (market != null) market.SetActiveCurrency(CurrencyId.SHT);
            ApplySelection(CurrencyId.SHT);
        }

        private void OnEthClicked()
        {
            if (market != null) market.SetActiveCurrency(CurrencyId.ETH);
            ApplySelection(CurrencyId.ETH);
        }

        private void OnBtcClicked()
        {
            if (market != null) market.SetActiveCurrency(CurrencyId.BTC);
            ApplySelection(CurrencyId.BTC);
        }

        private void OnActiveCurrencyChanged(CurrencyId id)
        {
            ApplySelection(id);
        }

        private void SyncFromMarket()
        {
            if (market == null)
            {
                ApplySelection(CurrencyId.SHT);
                return;
            }

            ApplySelection(market.ActiveCurrency);
        }

        private void ApplySelection(CurrencyId active)
        {
            if (activeCurrencyText != null)
                activeCurrencyText.text = GetDisplayName(active);

            SetButtonState(CurrencyId.SHT, active == CurrencyId.SHT, shtButton, shtButtonImage);
            SetButtonState(CurrencyId.ETH, active == CurrencyId.ETH, ethButton, ethButtonImage);
            SetButtonState(CurrencyId.BTC, active == CurrencyId.BTC, btcButton, btcButtonImage);
        }

        private string GetDisplayName(CurrencyId id)
        {
            return id switch
            {
                CurrencyId.SHT => string.IsNullOrWhiteSpace(shtDisplayName) ? "ScamHypeToken" : shtDisplayName,
                CurrencyId.ETH => string.IsNullOrWhiteSpace(ethDisplayName) ? "EveryoneTradesHere" : ethDisplayName,
                CurrencyId.BTC => string.IsNullOrWhiteSpace(btcDisplayName) ? "BratCoin" : btcDisplayName,
                _ => id.ToString(),
            };
        }

        private void SetButtonState(CurrencyId id, bool isActive, Button button, Image img)
        {
            if (button != null)
                button.interactable = !isActive;

            if (img != null)
                img.sprite = isActive ? activeSprite : inactiveSprite;
        }
    }
}

