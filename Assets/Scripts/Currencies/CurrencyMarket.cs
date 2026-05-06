using System;
using TraidingIDLE.Saves;
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

        [Serializable]
        private sealed class SaveData
        {
            // Active currency is intentionally NOT persisted: each launch must start with SHT.
            public CurrencyPrice[] prices = Array.Empty<CurrencyPrice>();
        }

        private const string SaveKey = "save.market.v1";

        [Header("Initial state")]
        [SerializeField] private CurrencyId activeCurrency = CurrencyId.SHT;

        [Header("Events")]
        [SerializeField] private bool notifyWhenPriceUnchanged = true;

        [Header("Prices")]
        [SerializeField] private CurrencyPrice[] prices =
        {
            new() { id = CurrencyId.SHT, price = 5000f },
            new() { id = CurrencyId.ETH, price = 100000f },
            new() { id = CurrencyId.BTC, price = 2500000f },
        };

        public CurrencyId ActiveCurrency => activeCurrency;
        public bool LoadedFromSave { get; private set; }

        public event Action<CurrencyId>? ActiveCurrencyChanged;
        public event Action<CurrencyId, float>? PriceChanged;

        private CurrencyId _lastActiveCurrency;

        private float _saveCooldown;
        private bool _dirty;

        private void Awake()
        {
            EnsureConfiguredPrices();
            LoadFromStorage();
            _lastActiveCurrency = activeCurrency;
        }

        private void Update()
        {
            if (!_dirty)
                return;

            _saveCooldown -= Time.unscaledDeltaTime;
            if (_saveCooldown > 0f)
                return;

            FlushSave();
        }

        private void OnDisable()
        {
            if (_dirty)
                FlushSave();
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause && _dirty)
                FlushSave();
        }

        private void OnApplicationQuit()
        {
            if (_dirty)
                FlushSave();
        }

        private void MarkDirty()
        {
            _dirty = true;
            if (_saveCooldown <= 0f)
                _saveCooldown = 5f;
        }

        private void FlushSave()
        {
            _dirty = false;
            _saveCooldown = 0f;
            SaveToStorage();
        }

        private void SaveToStorage()
        {
            var data = new SaveData
            {
                prices = (CurrencyPrice[])prices.Clone(),
            };
            SaveStorage.SaveJson(SaveKey, data);
            SaveStorage.Flush();
        }

        private void LoadFromStorage()
        {
            if (!SaveStorage.TryLoadJson<SaveData>(SaveKey, out var data))
                return;

            LoadedFromSave = true;

            if (data.prices == null)
                return;

            for (var i = 0; i < data.prices.Length; i++)
            {
                var saved = data.prices[i];
                if (saved.price < 0f)
                    continue;

                for (var j = 0; j < prices.Length; j++)
                {
                    if (prices[j].id != saved.id)
                        continue;
                    prices[j].price = saved.price;
                    break;
                }
            }
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
            MarkDirty();
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
                MarkDirty();
                return;
            }

            AddPrice(id, price);
            PriceChanged?.Invoke(id, price);
            MarkDirty();
        }

        private void EnsureConfiguredPrices()
        {
            prices ??= Array.Empty<CurrencyPrice>();

            EnsurePrice(CurrencyId.SHT, 5000f);
            EnsurePrice(CurrencyId.ETH, 100000f);
            EnsurePrice(CurrencyId.BTC, 2500000f);
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
