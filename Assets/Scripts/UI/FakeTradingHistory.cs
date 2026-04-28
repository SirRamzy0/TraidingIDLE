using System;
using System.Globalization;
using TMPro;
using TraidingIDLE.Currencies;
using UnityEngine;

namespace TraidingIDLE.UI
{
    public sealed class FakeTradingHistory : MonoBehaviour
    {
        [Serializable]
        private struct HistoryRow
        {
            public TMP_Text nameText;
            public TMP_Text coinAmountText;
            public TMP_Text totalText;
        }

        [Serializable]
        private struct CoinTradeSettings
        {
            public CurrencyId id;
            public string displayName;
            [Min(0.000001f)] public float minAmount;
            [Min(0.000001f)] public float maxAmount;
            [Range(0, 6)] public int amountDecimals;
            [Min(0f)] public float fallbackPrice;
        }

        private struct TradeEntry
        {
            public string TraderName;
            public string CurrencyDisplayName;
            public float Amount;
            public float Total;
            public bool IsBuy;
            public int AmountDecimals;
        }

        [Header("Market")]
        [SerializeField] private CurrencyMarket market = null!;
        [SerializeField] private bool useCurrentMarketPrices = true;

        [Header("Rows")]
        [SerializeField] private HistoryRow[] rows = new HistoryRow[3];
        [SerializeField] private bool fillRowsOnStart = true;
        [SerializeField] private bool autoRefresh = true;
        [SerializeField, Min(0.1f)] private float refreshIntervalMinSeconds = 1.5f;
        [SerializeField, Min(0.1f)] private float refreshIntervalMaxSeconds = 3.5f;

        [Header("Colors")]
        [SerializeField] private Color buyColor = new(0.18f, 1f, 0.68f);
        [SerializeField] private Color sellColor = new(1f, 0.25f, 0.38f);

        [Header("Names")]
        [SerializeField] private string[] firstNames =
        {
            "Ибрагим",
            "Семён",
            "Артур",
            "Дамир",
            "Марк",
            "Алиса",
            "София",
            "Виктория",
            "Никита",
            "Роман",
        };

        [SerializeField] private string[] lastNames =
        {
            "Кондибобрович",
            "Пампитов",
            "Дампов",
            "Свечкин",
            "Криптов",
            "Бычков",
            "Медведев",
            "Трейдинов",
            "Сатошин",
            "Биржев",
        };

        [Header("Coins")]
        [SerializeField] private CoinTradeSettings[] coins =
        {
            new()
            {
                id = CurrencyId.SHT,
                displayName = "SHT",
                minAmount = 25f,
                maxAmount = 2500f,
                amountDecimals = 0,
                fallbackPrice = 1f,
            },
            new()
            {
                id = CurrencyId.ETH,
                displayName = "ETH",
                minAmount = 1f,
                maxAmount = 8f,
                amountDecimals = 0,
                fallbackPrice = 2500f,
            },
            new()
            {
                id = CurrencyId.BTC,
                displayName = "BTC",
                minAmount = 1f,
                maxAmount = 3f,
                amountDecimals = 0,
                fallbackPrice = 65000f,
            },
        };

        private TradeEntry[] _entries = Array.Empty<TradeEntry>();
        private float _refreshTimer;

        private void OnValidate()
        {
            refreshIntervalMinSeconds = Mathf.Max(0.1f, refreshIntervalMinSeconds);
            refreshIntervalMaxSeconds = Mathf.Max(refreshIntervalMinSeconds, refreshIntervalMaxSeconds);

            if (rows == null)
                rows = Array.Empty<HistoryRow>();

            if (coins == null)
                coins = Array.Empty<CoinTradeSettings>();
        }

        private void Awake()
        {
            if (market == null)
                market = FindFirstObjectByType<CurrencyMarket>();

            EnsureEntriesSize();
        }

        private void OnEnable()
        {
            EnsureEntriesSize();

            if (fillRowsOnStart)
            {
                for (var i = _entries.Length - 1; i >= 0; i--)
                    PushNewTrade();
            }
            else
            {
                RenderRows();
            }

            ResetRefreshTimer();
        }

        private void Update()
        {
            if (!autoRefresh || rows == null || rows.Length == 0)
                return;

            _refreshTimer -= Time.deltaTime;
            if (_refreshTimer > 0f)
                return;

            PushNewTrade();
            ResetRefreshTimer();
        }

        [ContextMenu("Refresh All Rows")]
        private void RefreshAllRows()
        {
            EnsureEntriesSize();

            for (var i = _entries.Length - 1; i >= 0; i--)
                PushNewTrade();
        }

        [ContextMenu("Push New Trade")]
        private void PushNewTradeContext()
        {
            EnsureEntriesSize();
            PushNewTrade();
        }

        private void PushNewTrade()
        {
            EnsureEntriesSize();

            for (var i = 0; i < _entries.Length - 1; i++)
                _entries[i] = _entries[i + 1];

            _entries[^1] = BuildRandomTrade();
            RenderRows();
        }

        private TradeEntry BuildRandomTrade()
        {
            var coin = PickRandomCoin();
            var amountDecimals = GetAmountDecimals(coin);
            var amount = PickAmount(coin);
            var price = GetPrice(coin);

            return new TradeEntry
            {
                TraderName = $"{PickRandom(firstNames, "Игрок")} {PickRandom(lastNames, "Трейдеров")}",
                CurrencyDisplayName = string.IsNullOrWhiteSpace(coin.displayName) ? coin.id.ToString() : coin.displayName,
                Amount = amount,
                Total = amount * price,
                IsBuy = UnityEngine.Random.value >= 0.5f,
                AmountDecimals = amountDecimals,
            };
        }

        private CoinTradeSettings PickRandomCoin()
        {
            if (coins == null || coins.Length == 0)
            {
                return new CoinTradeSettings
                {
                    id = CurrencyId.SHT,
                    displayName = CurrencyId.SHT.ToString(),
                    minAmount = 1f,
                    maxAmount = 100f,
                    amountDecimals = 0,
                    fallbackPrice = 1f,
                };
            }

            return coins[UnityEngine.Random.Range(0, coins.Length)];
        }

        private float PickAmount(CoinTradeSettings coin)
        {
            var min = Mathf.Min(coin.minAmount, coin.maxAmount);
            var max = Mathf.Max(coin.minAmount, coin.maxAmount);
            var amount = UnityEngine.Random.Range(min, max);
            var decimals = GetAmountDecimals(coin);

            if (decimals <= 0)
                return Mathf.Max(1f, Mathf.Round(amount));

            var multiplier = Mathf.Pow(10f, decimals);
            return Mathf.Round(amount * multiplier) / multiplier;
        }

        private float GetPrice(CoinTradeSettings coin)
        {
            if (useCurrentMarketPrices && market != null)
            {
                var marketPrice = market.GetPrice(coin.id);
                if (marketPrice > 0f)
                    return marketPrice;
            }

            return Mathf.Max(0f, coin.fallbackPrice);
        }

        private void RenderRows()
        {
            if (rows == null)
                return;

            for (var i = 0; i < rows.Length; i++)
            {
                if (i >= _entries.Length)
                    break;

                RenderRow(rows[i], _entries[i]);
            }
        }

        private void RenderRow(HistoryRow row, TradeEntry entry)
        {
            if (row.nameText != null)
                row.nameText.text = entry.TraderName;

            if (row.coinAmountText != null)
                row.coinAmountText.text = $"{FormatAmount(entry.Amount, entry.AmountDecimals)} {entry.CurrencyDisplayName}";

            if (row.totalText != null)
            {
                row.totalText.text = FormatMoney(entry.Total);
                row.totalText.color = entry.IsBuy ? buyColor : sellColor;
            }
        }

        private void ResetRefreshTimer()
        {
            var min = Mathf.Min(refreshIntervalMinSeconds, refreshIntervalMaxSeconds);
            var max = Mathf.Max(refreshIntervalMinSeconds, refreshIntervalMaxSeconds);
            _refreshTimer = UnityEngine.Random.Range(min, max);
        }

        private void EnsureEntriesSize()
        {
            var size = rows?.Length ?? 0;
            if (_entries.Length == size)
                return;

            Array.Resize(ref _entries, size);
        }

        private static string PickRandom(string[] values, string fallback)
        {
            if (values == null || values.Length == 0)
                return fallback;

            var value = values[UnityEngine.Random.Range(0, values.Length)];
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static string FormatAmount(float value, int decimals)
        {
            decimals = Mathf.Clamp(decimals, 0, 6);
            return decimals <= 0
                ? Math.Round(value).ToString("F0", CultureInfo.InvariantCulture)
                : FormatNumber(value, decimals);
        }

        private static string FormatMoney(float value)
        {
            return FormatNumber(Math.Round(value), 0);
        }

        private static string FormatNumber(double value, int decimals)
        {
            return value
                .ToString($"N{decimals}", CultureInfo.InvariantCulture)
                .Replace(",", ".");
        }

        private static int GetAmountDecimals(CoinTradeSettings coin)
        {
            return coin.id == CurrencyId.BTC ? 0 : Mathf.Clamp(coin.amountDecimals, 0, 6);
        }
    }
}
