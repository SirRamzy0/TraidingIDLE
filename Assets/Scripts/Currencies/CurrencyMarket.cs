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

        public void SetActiveCurrency(CurrencyId id)
        {
            if (activeCurrency == id)
                return;

            activeCurrency = id;
            ActiveCurrencyChanged?.Invoke(activeCurrency);
        }

        public float GetPrice(CurrencyId id)
        {
            for (var i = 0; i < prices.Length; i++)
            {
                if (prices[i].id == id)
                    return prices[i].price;
            }

            throw new ArgumentOutOfRangeException(nameof(id), id, "Price not configured for currency.");
        }

        public void SetPrice(CurrencyId id, float price)
        {
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

            throw new ArgumentOutOfRangeException(nameof(id), id, "Price not configured for currency.");
        }
    }
}

