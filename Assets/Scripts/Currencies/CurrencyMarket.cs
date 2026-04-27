using System;
using UnityEngine;

namespace TraidingIDLE.Currencies
{
    public sealed class CurrencyMarket : MonoBehaviour
    {
        [Serializable]
        private struct CurrencyPrice
        {
            public CurrencyId id;
            public float price;
        }

        [Header("Initial state")]
        [SerializeField] private CurrencyId activeCurrency = CurrencyId.SHT;

        [Header("Events")]
        [SerializeField] private bool notifyWhenPriceUnchanged = true;

        [Header("Prices")]
        [SerializeField] private CurrencyPrice[] prices =
        {
            new() { id = CurrencyId.SHT, price = 1f },
            new() { id = CurrencyId.ETH, price = 2500f },
            new() { id = CurrencyId.BTC, price = 65000f },
        };

        public CurrencyId ActiveCurrency => activeCurrency;

        public event Action<CurrencyId>? ActiveCurrencyChanged;
        public event Action<CurrencyId, float>? PriceChanged;

        private CurrencyId _lastActiveCurrency;

        private void Awake()
        {
            EnsureConfiguredPrices();
            _lastActiveCurrency = activeCurrency;
        }

        private void OnValidate()
        {
            EnsureConfiguredPrices();

            if (!Application.isPlaying)
            {
                _lastActiveCurrency = activeCurrency;
                return;
            }

            if (_lastActiveCurrency == activeCurrency)
                return;

            _lastActiveCurrency = activeCurrency;
            ActiveCurrencyChanged?.Invoke(activeCurrency);
            PriceChanged?.Invoke(activeCurrency, GetPrice(activeCurrency));
        }

        public void SetActiveCurrency(CurrencyId id)
        {
            if (activeCurrency == id)
                return;

            activeCurrency = id;
            _lastActiveCurrency = activeCurrency;
            ActiveCurrencyChanged?.Invoke(activeCurrency);
        }

        [ContextMenu("Set Active Currency/SHT")]
        public void SetActiveSht() => SetActiveCurrency(CurrencyId.SHT);

        [ContextMenu("Set Active Currency/ETH")]
        public void SetActiveEth() => SetActiveCurrency(CurrencyId.ETH);

        [ContextMenu("Set Active Currency/BTC")]
        public void SetActiveBtc() => SetActiveCurrency(CurrencyId.BTC);

        public float GetPrice(CurrencyId id)
        {
            EnsureConfiguredPrices();

            for (var i = 0; i < prices.Length; i++)
            {
                if (prices[i].id == id)
                    return prices[i].price;
            }

            throw new ArgumentOutOfRangeException(nameof(id), id, "Price not configured for currency.");
        }

        public void SetPrice(CurrencyId id, float price)
        {
            EnsureConfiguredPrices();

            if (price < 0f)
                price = 0f;

            for (var i = 0; i < prices.Length; i++)
            {
                if (prices[i].id != id)
                    continue;

                if (Mathf.Approximately(prices[i].price, price))
                {
                    if (notifyWhenPriceUnchanged)
                        PriceChanged?.Invoke(id, prices[i].price);
                    return;
                }

                prices[i].price = price;
                PriceChanged?.Invoke(id, price);
                return;
            }

            AddPrice(id, price);
            PriceChanged?.Invoke(id, price);
        }

        private void EnsureConfiguredPrices()
        {
            prices ??= Array.Empty<CurrencyPrice>();

            EnsurePrice(CurrencyId.SHT, 1f);
            EnsurePrice(CurrencyId.ETH, 2500f);
            EnsurePrice(CurrencyId.BTC, 65000f);
        }

        private void EnsurePrice(CurrencyId id, float defaultPrice)
        {
            for (var i = 0; i < prices.Length; i++)
            {
                if (prices[i].id == id)
                    return;
            }

            AddPrice(id, defaultPrice);
        }

        private void AddPrice(CurrencyId id, float price)
        {
            Array.Resize(ref prices, prices.Length + 1);
            prices[^1] = new CurrencyPrice
            {
                id = id,
                price = Mathf.Max(0f, price),
            };
        }
    }
}

