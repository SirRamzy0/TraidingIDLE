using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using TraidingIDLE.Currencies;
using TraidingIDLE.Currencies.Simulation;

namespace TraidingIDLE.UI.Charts
{
    public sealed class PriceChartBoard : MonoBehaviour
    {
        [Header("Data source")]
        [SerializeField] private CurrencyMarket market = null!;
        [SerializeField] private PriceHistoryStore? priceHistoryStore = null;
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

        [Tooltip("Keeps small percent moves from filling the whole chart. SHT gets a wider floor than ETH/BTC.")]
        [SerializeField] private bool useCurrencyRelativeViewportFloor = true;

        [Range(0f, 3f)]
        [SerializeField] private float shtMinViewportRangeFromPrice01 = 0.72f;
        [Range(0f, 3f)]
        [SerializeField] private float ethMinViewportRangeFromPrice01 = 0.24f;
        [Range(0f, 3f)]
        [SerializeField] private float btcMinViewportRangeFromPrice01 = 0.14f;

        [Header("Sideways viewport zoom")]
        [SerializeField] private bool enableSidewaysViewportZoom = true;
        [Tooltip("If real history range is below this share of the normal floor, use a tighter sideways floor.")]
        [Range(0.05f, 1f)]
        [SerializeField] private float sidewaysZoomRangeThreshold01 = 0.65f;
        [Range(0f, 3f)]
        [SerializeField] private float shtSidewaysMinViewportRangeFromPrice01 = 0.42f;
        [Range(0f, 3f)]
        [SerializeField] private float ethSidewaysMinViewportRangeFromPrice01 = 0.070f;
        [Range(0f, 3f)]
        [SerializeField] private float btcSidewaysMinViewportRangeFromPrice01 = 0.075f;

        [Min(0f)]
        [SerializeField] private float maxViewportRange = 0f; // 0 = unlimited

        [Range(0f, 1f)]
        [SerializeField] private float smoothViewport = 0.25f;

        [Tooltip("How quickly the chart zooms out when a new candle leaves the current viewport.")]
        [Range(0.01f, 1f)]
        [SerializeField] private float expandViewport = 0.35f;

        [Tooltip("Smooths price-relative minimum range, so a fast pump does not rescale the whole chart in one tick.")]
        [Range(0.01f, 1f)]
        [SerializeField] private float viewportFloorSmooth = 0.18f;

        [Header("Viewport stability")]
        [SerializeField] private bool stabilizeViewportUntilEdge = true;
        [Tooltip("Viewport will not rescale while current candles stay inside this inner edge zone.")]
        [Range(0.02f, 0.35f)]
        [SerializeField] private float viewportRelayoutEdgeZone01 = 0.16f;

        [Header("Render target")]
        [SerializeField] private UICandlestickChartGraphic candlestickChart = null!;

        [Header("Candles (random wick/body)")]
        [Range(0f, 0.25f)]
        [SerializeField] private float candleWickNoise01 = 0.10f;
        [Range(0f, 0.25f)]
        [SerializeField] private float candleBodyNoise01 = 0.07f;
        [Range(0f, 0.25f)]
        [SerializeField] private float candleVariety01 = 0.18f;
        [Tooltip("Minimum visual candle range relative to the current chart viewport, not to the price itself.")]
        [Range(0f, 0.10f)]
        [FormerlySerializedAs("minCandleRangeFromPrice01")]
        [SerializeField] private float minCandleRangeFromViewport01 = 0.028f;
        [Tooltip("Minimum visible wick length relative to the current chart viewport. Keeps small candles readable.")]
        [Range(0f, 0.03f)]
        [SerializeField] private float minCandleWickFromViewport01 = 0.012f;
        [Range(0f, 1f)]
        [SerializeField] private float minCandleBody01 = 0.24f;

        [Header("Duplicate candles (same rounded price)")]
        [SerializeField] private bool avoidIdenticalConsecutiveCandles = true;
        [Range(0.05f, 0.45f)]
        [SerializeField] private float duplicateBodyNudge01 = 0.26f;

        [Header("Candle visuals (baked on candle creation, does not change market price)")]
        [SerializeField] private bool enableCandleVisualJitter = true;
        [Tooltip("How much the candle body height can vary inside the wick span.")]
        [Range(0f, 0.60f)]
        [SerializeField] private float bodyHeightJitter01 = 0.34f;
        [Tooltip("How much the total wick length can vary from candle to candle.")]
        [Range(0f, 0.80f)]
        [SerializeField] private float wickLengthJitter01 = 0.58f;
        [Tooltip("How strongly top vs bottom wick lengths can differ.")]
        [Range(0f, 1f)]
        [SerializeField] private float wickAsymmetry01 = 0.85f;
        [Tooltip("Adds extra randomness around symmetric wicks (near 0.5 balance).")]
        [Range(0f, 0.35f)]
        [SerializeField] private float wickBalanceRandomness = 0.20f;

        [Header("Long wick accents")]
        [Tooltip("Scales regular wick length. Long wick accents still use their own multiplier.")]
        [Range(0.25f, 1f)]
        [SerializeField] private float regularWickScale = 0.55f;
        [Range(0f, 0.20f)]
        [SerializeField] private float longUpperWickChance = 0.045f;
        [Range(0f, 0.20f)]
        [SerializeField] private float longLowerWickChance = 0.045f;
        [Range(0f, 0.10f)]
        [SerializeField] private float longBothWicksChance = 0.012f;
        [Range(1f, 8f)]
        [SerializeField] private float longWickMultiplierMin = 2.8f;
        [Range(1f, 8f)]
        [SerializeField] private float longWickMultiplierMax = 4.8f;

        [Header("Per-currency candle readability")]
        [Range(0.5f, 2.5f)]
        [SerializeField] private float shtCandleVisualRangeMultiplier = 1.45f;
        [Range(0.5f, 2.5f)]
        [SerializeField] private float ethCandleVisualRangeMultiplier = 1.55f;
        [Range(0.5f, 2.5f)]
        [SerializeField] private float btcCandleVisualRangeMultiplier = 1.35f;

        private readonly PriceHistoryBuffer _history = new();
        private readonly CandleHistoryBuffer _candles = new();
        private readonly Dictionary<CurrencyId, CandleSnapshot> _candleSnapshots = new();
        private UICandlestickChartGraphic.Candle01[] _candles01 = new UICandlestickChartGraphic.Candle01[50];
        private float _viewMin;
        private float _viewMax;
        private float _smoothedViewportRangeFloor;
        private float _currentPrice;
        private bool _hasViewport;
        private bool _receivedLivePrice;
        private int _candleSequenceIndex;
        private int _seed;

        private sealed class CandleSnapshot
        {
            public CandleHistoryBuffer.Candle[] candles = System.Array.Empty<CandleHistoryBuffer.Candle>();
            public int count;
            public float currentPrice;
            public bool receivedLivePrice;
            public int candleSequenceIndex;
        }

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

            if (priceHistoryStore == null)
                priceHistoryStore = FindFirstObjectByType<PriceHistoryStore>();

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

            if (priceHistoryStore != null)
                priceHistoryStore.HistoryReset += OnHistoryReset;

            if (followActiveCurrency && market != null)
                currency = market.ActiveCurrency;

            SeedFromStoreOrCurrentPrice();
        }

        private void OnDisable()
        {
            SaveCandleSnapshot(currency);

            if (market != null)
            {
                market.PriceChanged -= OnPriceChanged;
                market.ActiveCurrencyChanged -= OnActiveCurrencyChanged;
            }

            if (priceHistoryStore != null)
                priceHistoryStore.HistoryReset -= OnHistoryReset;
        }

        private void OnHistoryReset(CurrencyId id)
        {
            if (id != currency)
                return;

            _candleSnapshots.Remove(id);
            SeedFromStoreOrCurrentPrice();
        }

        private void OnValidate()
        {
            historySize = Mathf.Max(2, historySize);
            _history.SetCapacity(historySize + 1);
            _candles.SetCapacity(historySize);
            EnsureCandlesArray();
            duplicateBodyNudge01 = Mathf.Clamp(duplicateBodyNudge01, 0.05f, 0.45f);
            bodyHeightJitter01 = Mathf.Clamp01(bodyHeightJitter01);
            wickLengthJitter01 = Mathf.Clamp01(wickLengthJitter01);
            wickAsymmetry01 = Mathf.Clamp01(wickAsymmetry01);
            wickBalanceRandomness = Mathf.Clamp01(wickBalanceRandomness);
            regularWickScale = Mathf.Clamp(regularWickScale, 0.25f, 1f);
            longUpperWickChance = Mathf.Clamp(longUpperWickChance, 0f, 0.20f);
            longLowerWickChance = Mathf.Clamp(longLowerWickChance, 0f, 0.20f);
            longBothWicksChance = Mathf.Clamp(longBothWicksChance, 0f, 0.10f);
            longWickMultiplierMin = Mathf.Clamp(longWickMultiplierMin, 1f, 8f);
            longWickMultiplierMax = Mathf.Clamp(longWickMultiplierMax, longWickMultiplierMin, 8f);
            shtMinViewportRangeFromPrice01 = Mathf.Max(0f, shtMinViewportRangeFromPrice01);
            ethMinViewportRangeFromPrice01 = Mathf.Max(0f, ethMinViewportRangeFromPrice01);
            btcMinViewportRangeFromPrice01 = Mathf.Max(0f, btcMinViewportRangeFromPrice01);
            sidewaysZoomRangeThreshold01 = Mathf.Clamp(sidewaysZoomRangeThreshold01, 0.05f, 1f);
            shtSidewaysMinViewportRangeFromPrice01 = Mathf.Max(0f, shtSidewaysMinViewportRangeFromPrice01);
            ethSidewaysMinViewportRangeFromPrice01 = Mathf.Max(0f, ethSidewaysMinViewportRangeFromPrice01);
            btcSidewaysMinViewportRangeFromPrice01 = Mathf.Max(0f, btcSidewaysMinViewportRangeFromPrice01);
            shtCandleVisualRangeMultiplier = Mathf.Max(0.5f, shtCandleVisualRangeMultiplier);
            ethCandleVisualRangeMultiplier = Mathf.Max(0.5f, ethCandleVisualRangeMultiplier);
            btcCandleVisualRangeMultiplier = Mathf.Max(0.5f, btcCandleVisualRangeMultiplier);
            expandViewport = Mathf.Clamp01(expandViewport);
            viewportFloorSmooth = Mathf.Clamp01(viewportFloorSmooth);
            viewportRelayoutEdgeZone01 = Mathf.Clamp(viewportRelayoutEdgeZone01, 0.02f, 0.35f);
        }

        private void OnActiveCurrencyChanged(CurrencyId id)
        {
            if (!followActiveCurrency)
                return;

            SaveCandleSnapshot(currency);
            currency = id;
            _history.Clear();
            _candles.Clear();
            _hasViewport = false;
            _receivedLivePrice = false;
            _candleSequenceIndex = 0;
            SeedFromStoreOrCurrentPrice();
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

            var prevClose = _candles.Count > 0 ? _candles[_candles.Count - 1].close : prev;
            _candles.Push(BuildCandle(prev, price, prevClose, _candleSequenceIndex++));

            UpdateViewport(price);
            Render();
            SaveCandleSnapshot(currency);
        }

        private void SeedWithCurrentPrice()
        {
            if (market == null)
                return;

            var p = market.GetPrice(currency);
            SeedAroundPrice(p);
        }

        private void SeedFromStoreOrCurrentPrice()
        {
            if (TryRestoreCandleSnapshot(currency))
                return;

            if (priceHistoryStore != null && priceHistoryStore.TryGet(currency, out var saved) && saved.Count >= 2)
            {
                SeedFromHistory(saved);
                return;
            }

            SeedWithCurrentPrice();
        }

        private void SeedFromHistory(IReadOnlyList<float> saved)
        {
            _history.Clear();
            _candles.Clear();
            _candleSequenceIndex = 0;
            if (_history.Capacity != historySize + 1)
                _history.SetCapacity(historySize + 1);
            if (_candles.Capacity != historySize)
                _candles.SetCapacity(historySize);

            // Take the last `historySize` prices.
            var start = System.Math.Max(0, saved.Count - historySize);
            var first = Mathf.Max(0.000001f, saved[start]);
            _history.Push(first);

            var prevClose = first;
            for (var i = start + 1; i < saved.Count; i++)
            {
                var open = prevClose;
                var close = Mathf.Max(0.000001f, saved[i]);
                _history.Push(close);

                var candle = BuildCandle(open, close, prevClose, _candleSequenceIndex++);
                _candles.Push(candle);
                prevClose = candle.close;
            }

            _currentPrice = prevClose;
            CurrentPriceChanged?.Invoke(_currentPrice);
            _receivedLivePrice = true;

            UpdateViewport(prevClose, force: true);
            Render();
            SaveCandleSnapshot(currency);
        }

        private void SeedAroundPrice(float price)
        {
            _currentPrice = price;
            CurrentPriceChanged?.Invoke(_currentPrice);

            _history.Clear();
            _candles.Clear();
            _candleSequenceIndex = 0;
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
                var prevClose = i > 0 ? _candles[i - 1].close : open;
                _candles.Push(BuildCandle(open, close, prevClose, _candleSequenceIndex++));
            }

            UpdateViewport(close, force: true);
            Render();
            SaveCandleSnapshot(currency);
        }

        private void SaveCandleSnapshot(CurrencyId id)
        {
            if (_candles.Count <= 0)
                return;

            if (!_candleSnapshots.TryGetValue(id, out var snapshot))
            {
                snapshot = new CandleSnapshot();
                _candleSnapshots[id] = snapshot;
            }

            if (snapshot.candles == null || snapshot.candles.Length != historySize)
                snapshot.candles = new CandleHistoryBuffer.Candle[historySize];

            snapshot.count = _candles.CopyTo(snapshot.candles);
            snapshot.currentPrice = _currentPrice;
            snapshot.receivedLivePrice = _receivedLivePrice;
            snapshot.candleSequenceIndex = _candleSequenceIndex;
        }

        private bool TryRestoreCandleSnapshot(CurrencyId id)
        {
            if (!_candleSnapshots.TryGetValue(id, out var snapshot) || snapshot.count <= 0 || snapshot.candles == null)
                return false;

            var count = System.Math.Min(snapshot.count, System.Math.Min(snapshot.candles.Length, historySize));
            if (count <= 0)
                return false;

            var savedStartIndex = -1;
            IReadOnlyList<float>? saved = null;
            if (priceHistoryStore != null && priceHistoryStore.TryGet(id, out saved) && saved.Count >= 2)
            {
                var snapshotLastClose = snapshot.candles[count - 1].close;
                if (!TryFindPriceIndexFromEnd(saved, snapshotLastClose, out savedStartIndex))
                    return false;
            }

            _history.Clear();
            _candles.Clear();
            _candleSequenceIndex = 0;
            if (_history.Capacity != historySize + 1)
                _history.SetCapacity(historySize + 1);
            if (_candles.Capacity != historySize)
                _candles.SetCapacity(historySize);

            _history.Push(Mathf.Max(0.000001f, snapshot.candles[0].open));
            for (var i = 0; i < count; i++)
            {
                var candle = snapshot.candles[i];
                _candles.Push(candle);
                _history.Push(Mathf.Max(0.000001f, candle.close));
            }

            _candleSequenceIndex = Mathf.Max(snapshot.candleSequenceIndex, _candles.Count);

            if (saved != null && savedStartIndex >= 0)
                AppendSavedPrices(saved, savedStartIndex + 1);

            _currentPrice = _history.Count > 0
                ? _history[_history.Count - 1]
                : snapshot.currentPrice > 0f
                    ? snapshot.currentPrice
                    : _candles[_candles.Count - 1].close;
            _receivedLivePrice = snapshot.receivedLivePrice;
            CurrentPriceChanged?.Invoke(_currentPrice);

            UpdateViewport(_currentPrice, force: true);
            Render();
            SaveCandleSnapshot(id);
            return true;
        }

        private void AppendSavedPrices(IReadOnlyList<float> saved, int startIndex)
        {
            for (var i = Mathf.Max(0, startIndex); i < saved.Count; i++)
            {
                var price = Mathf.Max(0.000001f, saved[i]);
                var prev = _history.Count > 0 ? _history[_history.Count - 1] : price;
                var prevClose = _candles.Count > 0 ? _candles[_candles.Count - 1].close : prev;

                _history.Push(price);
                _candles.Push(BuildCandle(prev, price, prevClose, _candleSequenceIndex++));
            }
        }

        private static bool TryFindPriceIndexFromEnd(IReadOnlyList<float> prices, float target, out int index)
        {
            for (var i = prices.Count - 1; i >= 0; i--)
            {
                if (ApproximatelySamePrice(prices[i], target))
                {
                    index = i;
                    return true;
                }
            }

            index = -1;
            return false;
        }

        private static bool ApproximatelySamePrice(float a, float b)
        {
            var tolerance = Mathf.Max(0.0001f, Mathf.Max(Mathf.Abs(a), Mathf.Abs(b)) * 0.00001f);
            return Mathf.Abs(a - b) <= tolerance;
        }

        private void UpdateViewport(float latestPrice, bool force = false)
        {
            if (!_history.TryGetMinMax(out var priceMin, out var priceMax))
                return;

            var observedRange = priceMax - priceMin;
            var viewportRangeFloor = GetSmoothedViewportRangeFloor(latestPrice, observedRange, force);
            var priceRange = Mathf.Max(viewportRangeFloor, priceMax - priceMin);
            var pad = priceRange * edgePadding01;
            var targetMin = Mathf.Max(0f, priceMin - pad);
            var targetMax = priceMax + pad;

            ExpandToMinimumRange(ref targetMin, ref targetMax, viewportRangeFloor, latestPrice);

            var targetRange = targetMax - targetMin;
            var maxRange = maxViewportRange > 0f ? Mathf.Max(maxViewportRange, viewportRangeFloor) : 0f;
            if (maxRange > 0f && targetRange > maxRange)
            {
                // Max range is a zoom-out cap: center near the latest price, but still keep it visible.
                var center = Mathf.Clamp(latestPrice, targetMin + maxRange * 0.5f, targetMax - maxRange * 0.5f);
                targetMin = Mathf.Max(0f, center - maxRange * 0.5f);
                targetMax = targetMin + maxRange;
            }

            if (!_hasViewport || force)
            {
                _viewMin = targetMin;
                _viewMax = targetMax;
                _hasViewport = true;
                ViewportChanged?.Invoke(_viewMin, _viewMax);
                return;
            }

            if (ShouldKeepViewportStable(latestPrice))
                return;

            // Expand faster than shrink/shift, but not instantly: instant expansion makes pumps look like
            // the chart scale jumps in a single frame.
            var needsExpansion = targetMin < _viewMin || targetMax > _viewMax;
            var t = needsExpansion ? expandViewport : smoothViewport;
            _viewMin = Mathf.Lerp(_viewMin, targetMin, t);
            _viewMax = Mathf.Lerp(_viewMax, targetMax, t);
            ViewportChanged?.Invoke(_viewMin, _viewMax);
        }

        private bool ShouldKeepViewportStable(float latestPrice)
        {
            if (!stabilizeViewportUntilEdge || !_hasViewport)
                return false;

            var range = _viewMax - _viewMin;
            if (range <= 0.000001f)
                return false;

            var edge = range * viewportRelayoutEdgeZone01;
            var safeMin = _viewMin + edge;
            var safeMax = _viewMax - edge;
            if (safeMax <= safeMin)
                return false;

            var min = latestPrice;
            var max = latestPrice;
            if (_candles.Count > 0)
            {
                var latestCandle = _candles[_candles.Count - 1];
                min = Mathf.Min(min, latestCandle.low);
                max = Mathf.Max(max, latestCandle.high);
            }
            else if (_history.TryGetMinMax(out var historyMin, out var historyMax))
            {
                min = Mathf.Min(min, historyMin);
                max = Mathf.Max(max, historyMax);
            }

            return min >= safeMin && max <= safeMax;
        }

        private float GetSmoothedViewportRangeFloor(float latestPrice, float observedRange, bool force)
        {
            var targetFloor = GetViewportRangeFloor(latestPrice, observedRange);
            if (!_hasViewport || force || _smoothedViewportRangeFloor <= 0f)
            {
                _smoothedViewportRangeFloor = targetFloor;
                return _smoothedViewportRangeFloor;
            }

            _smoothedViewportRangeFloor = Mathf.Lerp(_smoothedViewportRangeFloor, targetFloor, viewportFloorSmooth);
            return _smoothedViewportRangeFloor;
        }

        private float GetViewportRangeFloor(float latestPrice, float observedRange)
        {
            var minRange = Mathf.Max(0.000001f, minViewportRange);
            if (!useCurrencyRelativeViewportFloor)
                return minRange;

            var price = Mathf.Max(0.000001f, latestPrice);
            var normalFloor = Mathf.Max(minRange, price * GetMinViewportRangeFromPrice01(currency));
            if (!enableSidewaysViewportZoom)
                return normalFloor;

            var sidewaysFloor = Mathf.Max(minRange, price * GetSidewaysMinViewportRangeFromPrice01(currency));
            if (sidewaysFloor >= normalFloor)
                return normalFloor;

            var triggerRange = Mathf.Max(sidewaysFloor, normalFloor * sidewaysZoomRangeThreshold01);
            if (observedRange >= triggerRange)
                return normalFloor;

            var t = Mathf.InverseLerp(sidewaysFloor, triggerRange, Mathf.Max(0f, observedRange));
            return Mathf.Lerp(sidewaysFloor, normalFloor, Mathf.Clamp01(t));
        }

        private float GetMinViewportRangeFromPrice01(CurrencyId id)
        {
            return id switch
            {
                CurrencyId.SHT => shtMinViewportRangeFromPrice01,
                CurrencyId.ETH => ethMinViewportRangeFromPrice01,
                CurrencyId.BTC => btcMinViewportRangeFromPrice01,
                _ => ethMinViewportRangeFromPrice01,
            };
        }

        private float GetSidewaysMinViewportRangeFromPrice01(CurrencyId id)
        {
            return id switch
            {
                CurrencyId.SHT => shtSidewaysMinViewportRangeFromPrice01,
                CurrencyId.ETH => ethSidewaysMinViewportRangeFromPrice01,
                CurrencyId.BTC => btcSidewaysMinViewportRangeFromPrice01,
                _ => ethSidewaysMinViewportRangeFromPrice01,
            };
        }

        private static void ExpandToMinimumRange(ref float targetMin, ref float targetMax, float minRange, float preferredCenter)
        {
            minRange = Mathf.Max(0.000001f, minRange);
            if (targetMax - targetMin >= minRange)
                return;

            var center = Mathf.Clamp(preferredCenter, targetMin, targetMax);
            var half = minRange * 0.5f;
            targetMin = center - half;
            targetMax = center + half;

            if (targetMin < 0f)
            {
                targetMax -= targetMin;
                targetMin = 0f;
            }
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
                var open01 = Mathf.InverseLerp(_viewMin, _viewMax, c.open);
                var high01 = Mathf.InverseLerp(_viewMin, _viewMax, c.high);
                var low01 = Mathf.InverseLerp(_viewMin, _viewMax, c.low);
                var close01 = Mathf.InverseLerp(_viewMin, _viewMax, c.close);

                _candles01[i] = new UICandlestickChartGraphic.Candle01
                {
                    open01 = open01,
                    high01 = high01,
                    low01 = low01,
                    close01 = close01,
                    visible = IsCandleInsideViewport01(open01, high01, low01, close01),
                };
            }
        }

        private static bool IsCandleInsideViewport01(float open01, float high01, float low01, float close01)
        {
            return IsInside01(open01)
                && IsInside01(high01)
                && IsInside01(low01)
                && IsInside01(close01);
        }

        private static bool IsInside01(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f && value <= 1f;
        }

        private CandleHistoryBuffer.Candle BuildCandle(float open, float close, float prevClose, int index)
        {
            var absOpen = Mathf.Max(0.000001f, open);
            var absClose = Mathf.Max(0.000001f, close);
            var realClose = absClose;
            var bodyDirection = GetCandleBodyDirection(absOpen, absClose, prevClose, index);

            var r1 = Hash01(_seed, index);
            var r2 = Hash01(_seed ^ 0x6C8E9CF5, index);
            var r3 = Hash01(_seed ^ 0x3C6EF372, index);
            var r4 = Hash01(_seed ^ unchecked((int)0xB5297A4D), index);
            var r5 = Hash01(_seed ^ unchecked((int)0x68E31DA4), index);
            var r6 = Hash01(_seed ^ unchecked((int)0xC0FFEE11), index);

            var visualReferenceRange = GetCandleVisualReferenceRange(absOpen, absClose);
            var visualRangeMultiplier = GetCandleVisualRangeMultiplier(currency);
            var baseRange = Mathf.Abs(absClose - absOpen);
            var minRange = visualReferenceRange * minCandleRangeFromViewport01 * visualRangeMultiplier;
            var movement01 = Mathf.Clamp01(baseRange / Mathf.Max(0.000001f, visualReferenceRange * 0.08f));
            var smallMoveScale = Mathf.Lerp(0.35f, 1f, movement01);
            var variedMinRange = minRange
                * smallMoveScale
                * Mathf.Lerp(0.20f, 1.15f + candleVariety01 * 1.5f, r4);
            var range = Mathf.Max(baseRange, variedMinRange);

            // Variety: some candles get a long wick, some get a visible body.
            var bodyK = Mathf.Lerp(minCandleBody01 * 0.35f, Mathf.Min(0.95f, minCandleBody01 + candleBodyNoise01 + candleVariety01 * 3f), r2);

            var bodyAmp = range * bodyK;

            // Ensure body is visible sometimes (especially when rounded prices repeat).
            if (Mathf.Abs(absClose - absOpen) < bodyAmp)
            {
                var mid = (absOpen + absClose) * 0.5f;
                absOpen = Mathf.Max(0f, mid - bodyAmp * 0.5f * bodyDirection);
                absClose = Mathf.Max(0f, mid + bodyAmp * 0.5f * bodyDirection);
            }

            if (avoidIdenticalConsecutiveCandles)
                ApplyDuplicateCandleNudge(prevClose, bodyDirection, visualReferenceRange * visualRangeMultiplier, ref absOpen, ref absClose);

            if (enableCandleVisualJitter)
                ApplyCandleBodyJitterPrice(index, ref absOpen, ref absClose);

            AttachOpenToPreviousClose(prevClose, realClose, bodyDirection, index, ref absOpen, ref absClose);

            var high = Mathf.Max(absOpen, absClose, realClose);
            var low = Mathf.Min(absOpen, absClose, realClose);

            // Most candles keep short readable wicks; long wick profiles are occasional accents.
            var minWick = visualReferenceRange * minCandleWickFromViewport01 * visualRangeMultiplier * regularWickScale;
            var smallWick = Mathf.Max(
                minWick,
                Mathf.Abs(absClose - absOpen) * candleWickNoise01 * 0.35f);

            var commonWick = smallWick * Mathf.Lerp(0.80f, 1.20f, r3);
            var balance = Mathf.Lerp(0.90f, 1.10f, r4);
            var upWick = commonWick * balance;
            var downWick = commonWick / balance;
            var profile = r5;
            var longUpperThreshold = longUpperWickChance;
            var longLowerThreshold = longUpperThreshold + longLowerWickChance;
            var longBothThreshold = longLowerThreshold + longBothWicksChance;
            var longMultiplierA = Mathf.Lerp(longWickMultiplierMin, longWickMultiplierMax, r6);
            var longMultiplierB = Mathf.Lerp(longWickMultiplierMin, longWickMultiplierMax, r1);

            if (profile < longUpperThreshold)
            {
                // Long upper wick, lower stays modest.
                upWick = commonWick * longMultiplierA;
                downWick = commonWick * Mathf.Lerp(0.80f, 1.15f, r2);
            }
            else if (profile < longLowerThreshold)
            {
                // Long lower wick, upper stays modest.
                upWick = commonWick * Mathf.Lerp(0.80f, 1.15f, r1);
                downWick = commonWick * longMultiplierA;
            }
            else if (profile < longBothThreshold)
            {
                // Rare: both wicks are long.
                upWick = commonWick * longMultiplierA;
                downWick = commonWick * longMultiplierB;
            }

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

        private static void AttachOpenToPreviousClose(
            float prevClose,
            float realClose,
            int bodyDirection,
            int index,
            ref float open,
            ref float close)
        {
            if (index <= 0)
                return;

            var anchor = Mathf.Max(0.000001f, prevClose);
            var visualBody = Mathf.Abs(close - open);
            var realBody = Mathf.Abs(realClose - anchor);
            var body = Mathf.Max(0.000001f, visualBody, realBody);

            const float Epsilon = 0.0001f;
            var direction = bodyDirection;
            if (realClose > anchor + Epsilon)
                direction = 1;
            else if (realClose < anchor - Epsilon)
                direction = -1;

            open = anchor;
            close = direction >= 0
                ? anchor + body
                : Mathf.Max(0f, anchor - body);
        }

        private float GetCandleVisualReferenceRange(float open, float close)
        {
            if (_hasViewport && _viewMax > _viewMin)
                return Mathf.Max(minViewportRange, _viewMax - _viewMin);

            if (_history.TryGetMinMax(out var min, out var max) && max > min)
                return Mathf.Max(minViewportRange, max - min);

            var price = Mathf.Max(open, close, 0.000001f);
            return Mathf.Max(minViewportRange, price * Mathf.Max(0.005f, initialFillNoise01));
        }

        private float GetCandleVisualRangeMultiplier(CurrencyId id)
        {
            return id switch
            {
                CurrencyId.SHT => shtCandleVisualRangeMultiplier,
                CurrencyId.ETH => ethCandleVisualRangeMultiplier,
                CurrencyId.BTC => btcCandleVisualRangeMultiplier,
                _ => ethCandleVisualRangeMultiplier,
            };
        }

        private int GetCandleBodyDirection(float open, float close, float prevClose, int index)
        {
            const float Epsilon = 0.0001f;

            if (close > open + Epsilon)
                return 1;
            if (close < open - Epsilon)
                return -1;

            if (close > prevClose + Epsilon)
                return 1;
            if (close < prevClose - Epsilon)
                return -1;

            return Hash01(_seed ^ unchecked((int)0x68E31DA4), index) >= 0.5f ? 1 : -1;
        }

        private void ApplyCandleBodyJitterPrice(int index, ref float open, ref float close)
        {
            // Visual-only jitter baked into stored OHLC so the candle doesn't "morph" every redraw tick.
            var rA = Hash01(_seed ^ unchecked((int)0xD00DFACE), index);

            var bodyTop = Mathf.Max(open, close);
            var bodyBot = Mathf.Min(open, close);
            var bodyMid = (bodyTop + bodyBot) * 0.5f;
            var bodyHalf = Mathf.Max(0.000001f, (bodyTop - bodyBot) * 0.5f);

            var jitterHalf = bodyHalf * (1f + (rA * 2f - 1f) * bodyHeightJitter01);
            jitterHalf = Mathf.Max(0.000001f, jitterHalf);

            var newTop = bodyMid + jitterHalf;
            var newBot = bodyMid - jitterHalf;

            if (close >= open)
            {
                open = Mathf.Max(0f, newBot);
                close = Mathf.Max(0f, newTop);
            }
            else
            {
                open = Mathf.Max(0f, newTop);
                close = Mathf.Max(0f, newBot);
            }
        }

        private void ApplyDuplicateCandleNudge(
            float prevClose,
            int bodyDirection,
            float visualReferenceRange,
            ref float open,
            ref float close)
        {
            var step = GetRoundingStepFor(Mathf.Max(open, close));
            if (step <= 0f)
                return;

            var openR = RoundToStep(open, step);
            var closeR = RoundToStep(close, step);
            var prevR = RoundToStep(prevClose, step);

            if (Mathf.Abs(openR - closeR) > 0.0001f)
                return;

            if (Mathf.Abs(closeR - prevR) > 0.0001f)
                return;

            var mid = (open + close) * 0.5f;
            var nudge = Mathf.Max(step * 0.25f, visualReferenceRange * duplicateBodyNudge01 * 0.06f);
            var dir = bodyDirection >= 0 ? 1f : -1f;

            open = Mathf.Max(0f, open - nudge * dir);
            close = Mathf.Max(0f, close + nudge * dir);
        }

        private static float GetRoundingStepFor(float price)
        {
            // Default chart rounding: multiples of 5 (matches typical SHT setup).
            // If you later want per-currency rules here, we can wire CurrencyId through.
            return 5f;
        }

        private static float RoundToStep(float v, float step)
        {
            if (step <= 0f)
                return v;
            return Mathf.Round(v / step) * step;
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
