using UnityEngine;
using TraidingIDLE.Currencies;

namespace TraidingIDLE.UI.Charts
{
    public sealed class PriceChartBoard : MonoBehaviour
    {
        [Header("Data source")]
        [SerializeField] private CurrencyMarket market = null!;
        [SerializeField] private bool followActiveCurrency = true;
        [SerializeField] private CurrencyId currency = CurrencyId.SHT;

        [Header("History")]
        [SerializeField, Min(2)] private int historySize = 50; // candle count

        [Header("Initial fill (so chart isn't flat at start)")]
        [Range(0f, 0.25f)]
        [SerializeField] private float initialFillNoise01 = 0.03f;

        [Header("Viewport behavior")]
        [Range(0.01f, 0.45f)]
        [SerializeField] private float edgePadding01 = 0.10f;

        [Min(0.000001f)]
        [SerializeField] private float minViewportRange = 1f;

        [Min(0f)]
        [SerializeField] private float maxViewportRange = 0f; // 0 = unlimited

        [Range(0f, 1f)]
        [SerializeField] private float smoothViewport = 0.25f;

        [Header("Render target")]
        [SerializeField] private UICandlestickChartGraphic candlestickChart = null!;

        [Header("Candles (random wick/body)")]
        [Range(0f, 0.25f)]
        [SerializeField] private float candleWickNoise01 = 0.06f;
        [Range(0f, 0.25f)]
        [SerializeField] private float candleBodyNoise01 = 0.03f;
        [Range(0f, 0.25f)]
        [SerializeField] private float candleVariety01 = 0.10f;
        [Range(0f, 0.10f)]
        [SerializeField] private float minCandleRangeFromPrice01 = 0.02f;
        [Range(0f, 1f)]
        [SerializeField] private float minCandleBody01 = 0.18f;

        private readonly PriceHistoryBuffer _history = new();
        private readonly CandleHistoryBuffer _candles = new();
        private UICandlestickChartGraphic.Candle01[] _candles01 = new UICandlestickChartGraphic.Candle01[50];
        private float _viewMin;
        private float _viewMax;
        private float _currentPrice;
        private bool _hasViewport;
        private bool _receivedLivePrice;
        private int _seed;

        public event System.Action<float, float>? ViewportChanged;
        public event System.Action<float>? CurrentPriceChanged;

        public float ViewMin => _viewMin;
        public float ViewMax => _viewMax;
        public float CurrentPrice => _currentPrice;
        public bool HasViewport => _hasViewport;
        public CurrencyId Currency => currency;

        private void Awake()
        {
            if (market == null)
                market = FindFirstObjectByType<CurrencyMarket>();

            _history.SetCapacity(historySize + 1);
            _candles.SetCapacity(historySize);
            EnsureCandlesArray();
            _seed = UnityEngine.Random.Range(1, int.MaxValue);
        }

        private void OnEnable()
        {
            if (market != null)
            {
                market.PriceChanged += OnPriceChanged;
                market.ActiveCurrencyChanged += OnActiveCurrencyChanged;
            }

            if (followActiveCurrency && market != null)
                currency = market.ActiveCurrency;

            SeedWithCurrentPrice();
        }

        private void OnDisable()
        {
            if (market != null)
            {
                market.PriceChanged -= OnPriceChanged;
                market.ActiveCurrencyChanged -= OnActiveCurrencyChanged;
            }
        }

        private void OnValidate()
        {
            historySize = Mathf.Max(2, historySize);
            _history.SetCapacity(historySize + 1);
            _candles.SetCapacity(historySize);
            EnsureCandlesArray();
        }

        private void OnActiveCurrencyChanged(CurrencyId id)
        {
            if (!followActiveCurrency)
                return;

            currency = id;
            _history.Clear();
            _candles.Clear();
            _hasViewport = false;
            _receivedLivePrice = false;
            SeedWithCurrentPrice();
        }

        private void OnPriceChanged(CurrencyId id, float price)
        {
            if (id != currency)
                return;

            _currentPrice = price;
            CurrentPriceChanged?.Invoke(_currentPrice);

            if (_history.Capacity != historySize + 1)
                _history.SetCapacity(historySize + 1);
            if (_candles.Capacity != historySize)
                _candles.SetCapacity(historySize);

            var prev = _history.Count > 0 ? _history[_history.Count - 1] : price;

            // First simulation tick often arrives after UI was seeded from default inspector prices.
            // Rebuild full chart around the first real price instead of drawing a giant jump candle.
            if (!_receivedLivePrice)
            {
                _receivedLivePrice = true;
                SeedAroundPrice(price);
                return;
            }

            _history.Push(price);

            _candles.Push(BuildCandle(prev, price, _candles.Count));

            UpdateViewport(price);
            Render();
        }

        private void SeedWithCurrentPrice()
        {
            if (market == null)
                return;

            var p = market.GetPrice(currency);
            SeedAroundPrice(p);
        }

        private void SeedAroundPrice(float price)
        {
            _currentPrice = price;
            CurrentPriceChanged?.Invoke(_currentPrice);

            _history.Clear();
            _candles.Clear();
            if (_history.Capacity != historySize + 1)
                _history.SetCapacity(historySize + 1);
            if (_candles.Capacity != historySize)
                _candles.SetCapacity(historySize);

            var baseP = Mathf.Max(0.000001f, price);
            var close = baseP * (1f + (Hash01(_seed, 0) * 2f - 1f) * initialFillNoise01);
            const int SeedSalt = unchecked((int)0xA1B2C3D4);
            var momentum = 0f;

            _history.Push(close);
            for (var i = 0; i < historySize; i++)
            {
                var open = close;

                // A small coherent random walk: candles look varied, but not like teleporting bars.
                var n = (Hash01(_seed ^ SeedSalt, i) * 2f - 1f) * initialFillNoise01;
                momentum = Mathf.Lerp(momentum, n, 0.35f);
                close = Mathf.Max(0f, open + baseP * momentum);
                close = Mathf.Lerp(close, baseP, 0.12f);

                if (i == historySize - 1)
                    close = baseP;

                _history.Push(close);
                _candles.Push(BuildCandle(open, close, i));
            }

            UpdateViewport(close, force: true);
            Render();
        }

        private void UpdateViewport(float latestPrice, bool force = false)
        {
            if (!_candles.TryGetMinMax(out var candlesMin, out var candlesMax))
                return;

            var candlesRange = Mathf.Max(minViewportRange, candlesMax - candlesMin);
            var pad = candlesRange * edgePadding01;
            var targetMin = Mathf.Max(0f, candlesMin - pad);
            var targetMax = candlesMax + pad;

            var targetRange = Mathf.Max(minViewportRange, targetMax - targetMin);
            if (maxViewportRange > 0f && targetRange > maxViewportRange)
            {
                // Max range is a zoom-out cap: center near the latest price, but still keep it visible.
                var center = Mathf.Clamp(latestPrice, targetMin + maxViewportRange * 0.5f, targetMax - maxViewportRange * 0.5f);
                targetMin = Mathf.Max(0f, center - maxViewportRange * 0.5f);
                targetMax = targetMin + maxViewportRange;
            }

            if (!_hasViewport || force)
            {
                _viewMin = targetMin;
                _viewMax = targetMax;
                _hasViewport = true;
                ViewportChanged?.Invoke(_viewMin, _viewMax);
                return;
            }

            // Expand instantly to avoid clipped candles; shrink/shift smoothly to avoid jumpy rescale.
            var needsExpansion = targetMin < _viewMin || targetMax > _viewMax;
            var t = needsExpansion ? 1f : smoothViewport;
            _viewMin = Mathf.Lerp(_viewMin, targetMin, t);
            _viewMax = Mathf.Lerp(_viewMax, targetMax, t);
            ViewportChanged?.Invoke(_viewMin, _viewMax);
        }

        private void Render()
        {
            EnsureCandlesArray();

            var count = _candles.Count;
            if (count < 1 || !_hasViewport || _viewMax <= _viewMin + 0.000001f)
            {
                if (candlestickChart != null)
                    candlestickChart.SetCandles01(_candles01, 0);
                return;
            }

            BuildCandles01FromStored(count);
            if (candlestickChart != null)
                candlestickChart.SetCandles01(_candles01, count);
        }

        private void EnsureCandlesArray()
        {
            if (_candles01 == null || _candles01.Length != historySize)
                _candles01 = new UICandlestickChartGraphic.Candle01[historySize];
        }

        private void BuildCandles01FromStored(int candlesCount)
        {
            for (var i = 0; i < candlesCount; i++)
            {
                var c = _candles[i];

                _candles01[i] = new UICandlestickChartGraphic.Candle01
                {
                    open01 = Mathf.InverseLerp(_viewMin, _viewMax, c.open),
                    high01 = Mathf.InverseLerp(_viewMin, _viewMax, c.high),
                    low01 = Mathf.InverseLerp(_viewMin, _viewMax, c.low),
                    close01 = Mathf.InverseLerp(_viewMin, _viewMax, c.close),
                };
            }
        }

        private CandleHistoryBuffer.Candle BuildCandle(float open, float close, int index)
        {
            var absOpen = Mathf.Max(0.000001f, open);
            var absClose = Mathf.Max(0.000001f, close);

            var r1 = Hash01(_seed, index);
            var r2 = Hash01(_seed ^ 0x6C8E9CF5, index);
            var r3 = Hash01(_seed ^ 0x3C6EF372, index);
            var r4 = Hash01(_seed ^ unchecked((int)0xB5297A4D), index);
            var r5 = Hash01(_seed ^ unchecked((int)0x68E31DA4), index);

            var baseRange = Mathf.Abs(absClose - absOpen);
            var minRange = Mathf.Min(absOpen, absClose) * minCandleRangeFromPrice01;
            var variedMinRange = minRange * Mathf.Lerp(0.35f, 2.80f + candleVariety01 * 5f, r4);
            var range = Mathf.Max(baseRange, variedMinRange);

            // Variety: some candles get a long wick, some get a visible body.
            var wickK = Mathf.Lerp(0.05f, 1.75f + candleVariety01 * 6f, r1);
            var bodyK = Mathf.Lerp(minCandleBody01 * 0.35f, Mathf.Min(0.95f, minCandleBody01 + candleBodyNoise01 + candleVariety01 * 3f), r2);

            var wickAmp = range * candleWickNoise01 * wickK;
            var bodyAmp = range * bodyK;

            var high = Mathf.Max(absOpen, absClose);
            var low = Mathf.Min(absOpen, absClose);

            // Ensure body is visible sometimes (especially when rounded prices repeat).
            if (Mathf.Abs(absClose - absOpen) < bodyAmp)
            {
                var mid = (absOpen + absClose) * 0.5f;
                var bodyDirection = r5 >= 0.5f ? 1f : -1f;
                absOpen = Mathf.Max(0f, mid - bodyAmp * 0.5f * bodyDirection);
                absClose = Mathf.Max(0f, mid + bodyAmp * 0.5f * bodyDirection);
                high = Mathf.Max(absOpen, absClose);
                low = Mathf.Min(absOpen, absClose);
            }

            // Asymmetric wicks (more "real" look)
            var upWick = wickAmp * Mathf.Lerp(0.15f, 2.2f, r1);
            var downWick = wickAmp * Mathf.Lerp(0.15f, 2.2f, r2);

            high += upWick;
            low = Mathf.Max(0f, low - downWick);

            return new CandleHistoryBuffer.Candle
            {
                open = absOpen,
                close = absClose,
                high = high,
                low = low,
            };
        }

        private static float Hash01(int seed, int index)
        {
            unchecked
            {
                var h = (uint)seed;
                h ^= (uint)index + 0x9E3779B9u + (h << 6) + (h >> 2);
                h *= 0x85EBCA6Bu;
                h ^= h >> 13;
                h *= 0xC2B2AE35u;
                h ^= h >> 16;
                return (h & 0x00FFFFFFu) / 16777215f;
            }
        }

    }
}

