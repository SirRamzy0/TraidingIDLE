using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using TraidingIDLE.Business;
using TraidingIDLE.Currencies;
using TraidingIDLE.Player;

namespace TraidingIDLE.Currencies.Simulation
{
    public sealed class MarketSimulation : MonoBehaviour
    {
        [Serializable]
        private struct StartPrice
        {
            public CurrencyId id;
            [Min(0.000001f)] public float price;
        }

        [Serializable]
        private sealed class CoinRuntime
        {
            public CoinSimulationConfig config = null;
            public float tickTimer;
            public float rawPrice;
            public float visiblePrice;

            // Corridor
            public float corridorMin;
            public float corridorMax;
            public float corridorCenter;
            public float corridorWidthPercent;

            // State machine
            public MarketStateType currentState;
            public float stateTimeLeft;

            // Pattern
            public bool inPattern;
            public float patternCooldown;
            public MarketStateType patternType;
            public float patternTimeLeft;
            public float patternPhaseDuration;
            public int patternWaveIndex;
            public int patternWaveCount;
            public float patternLegStartPrice;
            public float patternLegTargetPrice;
            public bool patternShiftedCorridor;
            public float patternBasePrice;

            // Noise
            public float noiseValue;
            public float targetNoiseValue;

            // Time
            public float time;
            public int seed;

            // Debug
            public float previousRawPrice;
        }

        [Serializable]
        private sealed class BalanceAggregateStats
        {
            public CurrencyId id;
            public int runs;
            public double startPriceSum;
            public double finalPriceSum;
            public double minPriceSum;
            public double maxPriceSum;
            public double maxDrawdownSum;
            public double maxDrawdownWorst;
            public double bestSwingSum;
            public double bestSwingBest;
            public double averageAbsTickMoveSum;
            public int ticksSum;

            public void Add(BalanceRunStats stats)
            {
                id = stats.id;
                runs++;
                startPriceSum += stats.startPrice;
                finalPriceSum += stats.finalPrice;
                minPriceSum += stats.minPrice;
                maxPriceSum += stats.maxPrice;
                maxDrawdownSum += stats.maxDrawdown01;
                maxDrawdownWorst = Math.Max(maxDrawdownWorst, stats.maxDrawdown01);
                bestSwingSum += stats.bestSwing01;
                bestSwingBest = Math.Max(bestSwingBest, stats.bestSwing01);
                averageAbsTickMoveSum += stats.averageAbsTickMove01;
                ticksSum += stats.ticks;
            }
        }

        private struct BalanceRunStats
        {
            public CurrencyId id;
            public double startPrice;
            public double finalPrice;
            public double minPrice;
            public double maxPrice;
            public double maxDrawdown01;
            public double bestSwing01;
            public double averageAbsTickMove01;
            public int ticks;
        }

        // --- References ---
        [Header("Target market")]
        [SerializeField] private CurrencyMarket market = null;
        [SerializeField] private PriceHistoryStore priceHistoryStore = null;
        [SerializeField] private PlayerProfile playerProfile = null;
        [SerializeField] private BusinessController businessProgressLink = null;
        [SerializeField] private bool useCurrencyMarketPricesOnStart = false;
        [SerializeField, Min(0)] private int initialHistoryWarmupTicks = 50;

        [Header("Game start")]
        [SerializeField] private bool applyGameStartSettings = true;
        [SerializeField] private CurrencyId startActiveCurrency = CurrencyId.SHT;
        [SerializeField] private StartPrice[] startPrices =
        {
            new StartPrice() { id = CurrencyId.SHT, price = 5000f },
            new StartPrice() { id = CurrencyId.ETH, price = 100000f },
            new StartPrice() { id = CurrencyId.BTC, price = 2500000f },
        };

        [Header("Coins (3 configs)")]
        [SerializeField] private CoinSimulationConfig[] coins = new CoinSimulationConfig[0];

        [Header("Legacy (kept for inspector compatibility)")]
        [SerializeField] private bool useGlobalMarketGrowth = false;
        [SerializeField] private bool scaleMarketWithBusinessIncome = false;

        [Header("Debug")]
        [SerializeField] private bool writeToCurrencyMarket = true;
        [SerializeField] private bool logMarketStatesOnStart = true;
        [SerializeField] private bool logMarketStatesOnStateChange = true;
        [SerializeField] private bool logActiveCurrencyChopDebug = true;
        [SerializeField] private bool showDebugLogs = false;

        [Header("Debug state override")]
        [SerializeField] private bool debugOverrideState = false;
        [SerializeField] private bool debugOverrideActiveCurrencyOnly = true;
        [SerializeField] private CurrencyId debugOverrideCurrency = CurrencyId.SHT;
        [SerializeField] private MarketStateType debugOverrideStateType = MarketStateType.CalmCorridor;
        [Min(1f)]
        [SerializeField] private float debugStateDurationSeconds = 120f;

        // --- Runtime ---
        private readonly Dictionary<CurrencyId, CoinRuntime> _runtime = new();
        private bool _warmupInProgress;
        private bool _balanceSimulationInProgress;
        private double _lastRealtimeUtcSeconds;
        private bool _resumeCatchUpPending;

        // --- Lifecycle ---

        private void Awake()
        {
            if (market == null)
                market = GetComponent<CurrencyMarket>();

            if (priceHistoryStore == null)
                priceHistoryStore = GetComponent<PriceHistoryStore>();
            if (priceHistoryStore == null)
                priceHistoryStore = FindFirstObjectByType<PriceHistoryStore>();
            if (playerProfile == null)
                playerProfile = FindAnyObjectByType<PlayerProfile>(FindObjectsInactive.Include);
            if (businessProgressLink == null)
                businessProgressLink = FindAnyObjectByType<BusinessController>(FindObjectsInactive.Include);
        }

        private void Start()
        {
            _runtime.Clear();

            var loadedFromSave = market != null && market.LoadedFromSave;

            if (applyGameStartSettings)
            {
                if (market != null)
                    market.SetActiveCurrency(startActiveCurrency);

                if (!loadedFromSave)
                    ApplyStartPriceOverrides();
            }

            for (var i = 0; i < coins.Length; i++)
            {
                var cfg = coins[i];
                if (cfg == null)
                    continue;

                cfg.EnsureDefaults();

                var startPrice = Mathf.Max(cfg.minPrice, cfg.initialPrice);
                if ((useCurrencyMarketPricesOnStart || loadedFromSave) && market != null)
                    startPrice = Mathf.Max(cfg.minPrice, market.GetPrice(cfg.id));

                var r = CreateRuntime(cfg, startPrice);
                _runtime[cfg.id] = r;

                if (writeToCurrencyMarket && market != null)
                    market.SetPrice(cfg.id, r.visiblePrice);
            }

            WarmupHistoryIfNeeded();

            ResetRealtimeClock();

            if (logMarketStatesOnStart)
                LogActiveMarketStates("Market simulation started");
        }

        private void Update()
        {
            var dt = GetRuntimeDeltaSeconds();
            if (dt <= 0f)
                return;

            foreach (var kv in _runtime)
                AdvanceRuntime(kv.Value, dt);
        }

        public void RefreshEconomyScaleNow()
        {
            // No-op in new architecture: economy scaling is handled via corridor shifts during patterns.
            // Kept for API compatibility with BusinessController.
        }

        /// <summary>
        /// Stub for backward compatibility with BusinessController business skills.
        /// Business skills temporarily force a pump-and-dump pattern on the target currency.
        /// </summary>
        public void EnqueueBusinessSkillOverrideNextState(
            CurrencyId currency,
            float growDurationSeconds,
            float dumpDurationSeconds,
            float growthPercent,
            float returnTolerancePercent)
        {
            if (!_runtime.TryGetValue(currency, out var r))
                return;

            // End any current pattern
            EndPattern(r);
            r.patternCooldown = 0f;

            // Clamp business skill parameters
            growDurationSeconds = Mathf.Max(1f, growDurationSeconds);
            dumpDurationSeconds = Mathf.Max(0.5f, dumpDurationSeconds);
            growthPercent = Mathf.Clamp(growthPercent, 0.05f, 3f);
            returnTolerancePercent = Mathf.Clamp(returnTolerancePercent, 0f, 0.3f);

            // Store business skill override data on runtime
            r.inPattern = true;
            r.patternType = MarketStateType.PumpAndCorrection;
            r.patternWaveIndex = 0;
            r.patternWaveCount = 2;
            r.patternShiftedCorridor = false;

            // Phase 1: pump (grow)
            r.patternPhaseDuration = growDurationSeconds;
            r.patternTimeLeft = r.patternPhaseDuration;
            r.patternBasePrice = r.rawPrice;
            r.patternLegStartPrice = r.rawPrice;
            r.patternLegTargetPrice = r.rawPrice * (1f + growthPercent);

            // Clamp target
            r.patternLegTargetPrice = Mathf.Clamp(r.patternLegTargetPrice, r.config.minPrice * 1.01f, r.config.maxPrice * 0.99f);

            // Phase 2: correction (dump) amplitude based on returnTolerancePercent
            var correctionDepth = growthPercent * returnTolerancePercent;
            var returnTarget = r.patternLegTargetPrice * (1f - correctionDepth);
            returnTarget = Mathf.Max(r.config.minPrice * 1.01f, returnTarget);

            // Total state duration covers both phases plus a small buffer
            r.stateTimeLeft = growDurationSeconds + dumpDurationSeconds + 2f;

            // Immediately tick the first leg to start movement
            r.patternWaveIndex = 0;

            if (showDebugLogs)
                Debug.Log($"[MarketSim] Business skill for {currency}: pump {growthPercent:P0} over {growDurationSeconds}s, then dump {returnTolerancePercent:P0} over {dumpDurationSeconds}s", this);
        }

        public void FlushPendingTicks()
        {
            foreach (var kv in _runtime)
                AdvanceRuntime(kv.Value, 0f);
        }

        // --- Runtime creation & tick loop ---

        private CoinRuntime CreateRuntime(CoinSimulationConfig cfg, float startPrice)
        {
            var r = new CoinRuntime
            {
                config = cfg,
                rawPrice = startPrice,
                visiblePrice = RoundPrice(cfg, startPrice),
                corridorCenter = startPrice,
                corridorWidthPercent = cfg.startCorridorWidthPercent,
                seed = UnityEngine.Random.Range(1, int.MaxValue),
                noiseValue = UnityEngine.Random.Range(-1f, 1f),
                targetNoiseValue = UnityEngine.Random.Range(-1f, 1f),
            };

            RebuildCorridor(r);
            PickNextState(r, initial: true);

            return r;
        }

        private void AdvanceRuntime(CoinRuntime r, float dt)
        {
            if (r == null)
                return;

            r.tickTimer += Mathf.Max(0f, dt);
            r.time += Mathf.Max(0f, dt);

            var interval = r.config.tickIntervalSeconds;
            while (r.tickTimer >= interval)
            {
                r.tickTimer -= interval;
                Tick(r, interval);
            }
        }

        private void Tick(CoinRuntime r, float dt)
        {
            r.previousRawPrice = r.rawPrice;

            // 1. Corridor Layer
            UpdateCorridor(r, dt);

            // 2. State / Pattern Layer
            if (r.inPattern)
            {
                UpdatePattern(r, dt);
            }
            else
            {
                UpdateRegularState(r, dt);
            }

            // 3. Noise Layer
            ApplyNoise(r, dt);

            // 4. Corridor Forces
            ApplyCorridorForces(r, dt);

            // Safety
            r.rawPrice = ClampPriceSafe(r);

            var rounded = RoundPrice(r.config, r.rawPrice);
            if (!Mathf.Approximately(rounded, r.visiblePrice))
            {
                r.visiblePrice = rounded;

                if (!_warmupInProgress)
                {
                    if (writeToCurrencyMarket && market != null)
                        market.SetPrice(r.config.id, r.visiblePrice);
                    if (priceHistoryStore != null)
                        priceHistoryStore.Push(r.config.id, r.visiblePrice);
                }
            }
        }

        // --- Corridor Layer ---

        private static void RebuildCorridor(CoinRuntime r)
        {
            var half = r.corridorCenter * r.corridorWidthPercent * 0.5f;
            r.corridorMin = Mathf.Max(r.config.minPrice, r.corridorCenter - half);
            r.corridorMax = r.corridorCenter + half;
        }

        private static void UpdateCorridor(CoinRuntime r, float dt)
        {
            // Slowly drift corridor center toward current price to keep price inside
            var driftStrength = 0.015f * dt / Mathf.Max(r.config.tickIntervalSeconds, 0.1f);
            var targetCenter = Mathf.Lerp(r.corridorCenter, r.rawPrice, driftStrength);
            r.corridorCenter = targetCenter;

            // Clamp width
            var currentWidth = (r.corridorMax - r.corridorMin) / Mathf.Max(r.corridorCenter, 0.0001f);
            var clampedWidth = Mathf.Clamp(currentWidth,
                r.config.minCorridorWidthPercent,
                r.config.maxCorridorWidthPercent);

            if (!Mathf.Approximately(currentWidth, clampedWidth))
            {
                var half = r.corridorCenter * clampedWidth * 0.5f;
                r.corridorMin = Mathf.Max(r.config.minPrice, r.corridorCenter - half);
                r.corridorMax = r.corridorCenter + half;
                r.corridorWidthPercent = clampedWidth;
            }
            else
            {
                RebuildCorridor(r);
            }
        }

        private static void ShiftCorridor(CoinRuntime r, float shiftPercent)
        {
            var target = r.corridorCenter * (1f + shiftPercent);
            r.corridorCenter = Mathf.Lerp(r.corridorCenter, target, 0.5f);
            RebuildCorridor(r);
        }

        // --- State Machine ---

        private void PickNextState(CoinRuntime r, bool initial = false)
        {
            var weights = r.config.stateWeights;
            if (weights == null || weights.Length == 0)
            {
                r.currentState = MarketStateType.CalmCorridor;
                r.stateTimeLeft = 10f;
                return;
            }

            var total = 0f;
            for (var i = 0; i < weights.Length; i++)
                if (weights[i] != null)
                    total += Mathf.Max(0f, weights[i].weight);

            var roll = UnityEngine.Random.Range(0f, total);
            var acc = 0f;
            var chosen = MarketStateType.CalmCorridor;

            for (var i = 0; i < weights.Length; i++)
            {
                if (weights[i] == null)
                    continue;
                if (IsLegacyState(weights[i].type))
                    continue;
                acc += Mathf.Max(0f, weights[i].weight);
                if (roll <= acc)
                {
                    chosen = weights[i].type;
                    break;
                }
            }

            r.currentState = chosen;
            var dur = UnityEngine.Random.Range(r.config.minStateDurationSeconds, r.config.maxStateDurationSeconds);
            r.stateTimeLeft = Mathf.Max(1f, dur);

            // Pattern cooldown ticks down
            if (r.patternCooldown > 0f)
                r.patternCooldown = Mathf.Max(0f, r.patternCooldown - r.stateTimeLeft);

            if (initial)
                r.inPattern = false;

            if (showDebugLogs && !_warmupInProgress)
                Debug.Log($"[MarketSim] {r.config.id}: entered state {chosen} for {r.stateTimeLeft:0.0}s");
        }

        private bool TryStartPattern(CoinRuntime r)
        {
            if (r.patternCooldown > 0f)
                return false;

            if (UnityEngine.Random.value > r.config.chanceToStartPatternAfterState)
                return false;

            var weights = r.config.patternWeights;
            if (weights == null || weights.Length == 0)
                return false;

            var total = 0f;
            for (var i = 0; i < weights.Length; i++)
                if (weights[i] != null)
                    total += Mathf.Max(0f, weights[i].weight);

            if (total <= 0f)
                return false;

            var roll = UnityEngine.Random.Range(0f, total);
            var acc = 0f;
            var chosen = MarketStateType.BigSaw;

            for (var i = 0; i < weights.Length; i++)
            {
                if (weights[i] == null)
                    continue;
                acc += Mathf.Max(0f, weights[i].weight);
                if (roll <= acc)
                {
                    chosen = weights[i].type;
                    break;
                }
            }

            StartPattern(r, chosen);
            return true;
        }

        private static void StartPattern(CoinRuntime r, MarketStateType type)
        {
            r.inPattern = true;
            r.patternType = type;
            r.patternWaveIndex = 0;
            r.patternShiftedCorridor = false;
            r.patternBasePrice = r.rawPrice;

            var settings = GetPatternSettings(r.config, type);
            r.patternWaveCount = UnityEngine.Random.Range(settings.minWaves, settings.maxWaves + 1);
            r.patternPhaseDuration = UnityEngine.Random.Range(settings.minDurationSeconds, settings.maxDurationSeconds);
            r.patternTimeLeft = r.patternPhaseDuration;

            BuildPatternLeg(r);

            if (settings.chanceToShiftCorridor > 0f && UnityEngine.Random.value < settings.chanceToShiftCorridor)
            {
                var shift = UnityEngine.Random.Range(settings.minCorridorShiftPercent, settings.maxCorridorShiftPercent);
                if (type == MarketStateType.StaircaseShiftDown || type == MarketStateType.FalseBreakoutDown || type == MarketStateType.DumpAndRecovery)
                    shift = -shift;
                ShiftCorridor(r, shift);
                r.patternShiftedCorridor = true;
            }

            r.stateTimeLeft = r.patternPhaseDuration * r.patternWaveCount + 1f;
        }

        private void EndPattern(CoinRuntime r)
        {
            r.inPattern = false;
            r.patternCooldown = UnityEngine.Random.Range(r.config.minPatternCooldownSeconds, r.config.maxPatternCooldownSeconds);
            PickNextState(r);
        }

        // --- Regular States ---

        private void UpdateRegularState(CoinRuntime r, float dt)
        {
            r.stateTimeLeft -= dt;
            if (r.stateTimeLeft <= 0f)
            {
                if (!TryStartPattern(r))
                    PickNextState(r);
                return;
            }

            var settings = GetMovementSettings(r.config, r.currentState);
            var interval = Mathf.Max(0.1f, r.config.tickIntervalSeconds);

            // Base directional move
            var bias = settings.directionalBias;
            var move = r.rawPrice * bias * settings.maxTickChangePercent * dt / interval;

            // Random reversals
            if (UnityEngine.Random.value < settings.reverseMoveChance * dt / interval)
            {
                move *= -0.6f;
            }

            r.rawPrice += move;

            // Center / edge attraction
            var pos = Mathf.InverseLerp(r.corridorMin, r.corridorMax, r.rawPrice);
            var center = (r.corridorMin + r.corridorMax) * 0.5f;

            if (settings.centerAttraction > 0f)
            {
                var toCenter = center - r.rawPrice;
                r.rawPrice += toCenter * settings.centerAttraction * dt / interval;
            }

            if (settings.edgeAttraction > 0f)
            {
                var edgeDist = Mathf.Min(pos, 1f - pos) * 2f; // 1 at center
                if (edgeDist < 0.25f)
                {
                    var push = (center - r.rawPrice) * settings.edgeAttraction * (1f - edgeDist / 0.25f) * dt / interval;
                    r.rawPrice += push;
                }
            }
        }

        // --- Patterns ---

        private void UpdatePattern(CoinRuntime r, float dt)
        {
            r.patternTimeLeft -= dt;
            r.stateTimeLeft -= dt;

            if (r.patternTimeLeft <= 0f)
            {
                r.patternWaveIndex++;
                if (r.patternWaveIndex >= r.patternWaveCount)
                {
                    EndPattern(r);
                    return;
                }
                BuildPatternLeg(r);
                r.patternTimeLeft = r.patternPhaseDuration;
            }

            var t = 1f - Mathf.Clamp01(r.patternTimeLeft / Mathf.Max(0.001f, r.patternPhaseDuration));
            var curve = SmoothStep01(t);
            var targetPrice = Mathf.Lerp(r.patternLegStartPrice, r.patternLegTargetPrice, curve);

            var settings = GetPatternSettings(r.config, r.patternType);
            var maxDelta = r.rawPrice * settings.maxTickChangePercent * dt / Mathf.Max(r.config.tickIntervalSeconds, 0.1f);

            // Add some controlled noise during pattern
            var noise = r.noiseValue * settings.noiseAmountPercent * r.rawPrice * 0.1f * dt;
            targetPrice += noise;

            r.rawPrice = Mathf.MoveTowards(r.rawPrice, targetPrice, maxDelta);
        }

        private static void BuildPatternLeg(CoinRuntime r)
        {
            r.patternLegStartPrice = r.rawPrice;

            var settings = GetPatternSettings(r.config, r.patternType);
            var range = r.corridorMax - r.corridorMin;
            var amplitude = range * UnityEngine.Random.Range(settings.minAmplitudePercentOfCorridor, settings.maxAmplitudePercentOfCorridor);

            var direction = GetPatternWaveDirection(r);
            r.patternLegTargetPrice = r.patternLegStartPrice + amplitude * direction;

            // Clamp to playable bounds
            r.patternLegTargetPrice = Mathf.Clamp(r.patternLegTargetPrice, r.config.minPrice * 1.01f, r.config.maxPrice * 0.99f);
        }

        private static float GetPatternWaveDirection(CoinRuntime r)
        {
            var pt = r.patternType;
            var wi = r.patternWaveIndex;

            if (pt == MarketStateType.BigSaw)
                return (wi % 2 == 0) ? 1f : -1f;
            if (pt == MarketStateType.StaircaseShiftUp)
                return (wi % 2 == 0) ? 1f : -0.4f;
            if (pt == MarketStateType.StaircaseShiftDown)
                return (wi % 2 == 0) ? -1f : 0.4f;
            if (pt == MarketStateType.FalseBreakoutUp)
                return wi == 0 ? 1f : -1f;
            if (pt == MarketStateType.FalseBreakoutDown)
                return wi == 0 ? -1f : 1f;
            if (pt == MarketStateType.PumpAndCorrection)
                return wi == 0 ? 1f : -0.5f;
            if (pt == MarketStateType.DumpAndRecovery)
                return wi == 0 ? -1f : 0.5f;
            if (pt == MarketStateType.CompressionBreakoutNew)
                return (wi < r.patternWaveCount - 1)
                    ? ((wi % 2 == 0) ? 1f : -1f) * 0.3f
                    : ((UnityEngine.Random.value < 0.5f) ? 1f : -1f);

            return (UnityEngine.Random.value < 0.5f) ? 1f : -1f;
        }

        // --- Noise Layer ---

        private void ApplyNoise(CoinRuntime r, float dt)
        {
            var interval = Mathf.Max(0.1f, r.config.tickIntervalSeconds);

            if (r.inPattern)
            {
                var s = GetPatternSettings(r.config, r.patternType);
                ApplyNoiseCore(r, dt, interval, s.noiseSmoothness, s.noiseAmountPercent);
            }
            else
            {
                var s = GetMovementSettings(r.config, r.currentState);
                ApplyNoiseCore(r, dt, interval, s.noiseSmoothness, s.noiseAmountPercent);
            }
        }

        private static void ApplyNoiseCore(CoinRuntime r, float dt, float interval, float smoothness, float noiseAmountPercent)
        {
            if (UnityEngine.Random.value < 0.08f * dt / interval)
                r.targetNoiseValue = UnityEngine.Random.Range(-1f, 1f);

            r.noiseValue = Mathf.Lerp(r.noiseValue, r.targetNoiseValue, smoothness * dt / interval);

            var noiseMove = r.rawPrice * r.noiseValue * noiseAmountPercent * dt / interval;
            r.rawPrice += noiseMove;
        }

        // --- Corridor Forces ---

        private void ApplyCorridorForces(CoinRuntime r, float dt)
        {
            var interval = Mathf.Max(0.1f, r.config.tickIntervalSeconds);

            if (!r.config.allowSoftBreakOutsideCorridor)
            {
                r.rawPrice = Mathf.Clamp(r.rawPrice, r.corridorMin, r.corridorMax);
                return;
            }

            // Soft return if outside
            if (r.rawPrice < r.corridorMin)
            {
                var depth = (r.corridorMin - r.rawPrice) / Mathf.Max(0.0001f, r.corridorMin);
                var strength = r.config.priceReturnToCorridorStrength * Mathf.Lerp(0.3f, 1f, Mathf.Clamp01(depth));
                r.rawPrice = Mathf.Lerp(r.rawPrice, r.corridorMin, strength * dt / interval);
            }
            else if (r.rawPrice > r.corridorMax)
            {
                var depth = (r.rawPrice - r.corridorMax) / Mathf.Max(0.0001f, r.corridorMax);
                var strength = r.config.priceReturnToCorridorStrength * Mathf.Lerp(0.3f, 1f, Mathf.Clamp01(depth));
                r.rawPrice = Mathf.Lerp(r.rawPrice, r.corridorMax, strength * dt / interval);
            }

            // Border bounce
            var range = r.corridorMax - r.corridorMin;
            if (range <= 0f)
                return;

            var pos = Mathf.InverseLerp(r.corridorMin, r.corridorMax, r.rawPrice);
            if (pos < 0.08f)
            {
                var push = r.config.borderBounceStrength * (1f - pos / 0.08f) * range * dt / interval;
                r.rawPrice += push;
            }
            else if (pos > 0.92f)
            {
                var push = r.config.borderBounceStrength * ((pos - 0.92f) / 0.08f) * range * dt / interval;
                r.rawPrice -= push;
            }
        }

        // --- Safety ---

        private static float ClampPriceSafe(CoinRuntime r)
        {
            var price = r.rawPrice;

            if (float.IsNaN(price) || float.IsInfinity(price))
                price = r.config.minPrice;

            price = Mathf.Clamp(price, r.config.minPrice, r.config.maxPrice);

            // Limit max change per tick
            var maxChange = r.previousRawPrice * r.config.maxNormalTickChangePercent;
            if (r.inPattern)
                maxChange = r.previousRawPrice * r.config.maxEventTickChangePercent;

            if (maxChange > 0f && r.previousRawPrice > 0f)
            {
                price = Mathf.Clamp(price, r.previousRawPrice - maxChange, r.previousRawPrice + maxChange);
            }

            return Mathf.Max(r.config.minPrice, price);
        }

        private static float RoundPrice(CoinSimulationConfig cfg, float price)
        {
            if (cfg == null || cfg.roundingRules == null || cfg.roundingRules.Length == 0)
                return Mathf.Round(price);

            var step = 1f;
            for (var i = 0; i < cfg.roundingRules.Length; i++)
            {
                if (price >= cfg.roundingRules[i].minPrice)
                    step = cfg.roundingRules[i].step;
            }

            return Mathf.Round(price / step) * step;
        }

        // --- Settings Helpers ---

        private static StateMovementSettings GetMovementSettings(CoinSimulationConfig cfg, MarketStateType state)
        {
            switch (state)
            {
                case MarketStateType.CalmCorridor: return cfg.calmCorridor;
                case MarketStateType.ActiveCorridor: return cfg.activeCorridor;
                case MarketStateType.ScalpingWindow: return cfg.scalpingWindow;
                case MarketStateType.PressureUp: return cfg.pressureUp;
                case MarketStateType.PressureDown: return cfg.pressureDown;
                case MarketStateType.SlowTrendUp: return cfg.slowTrendUp;
                case MarketStateType.SlowTrendDown: return cfg.slowTrendDown;
                case MarketStateType.VolatileChop: return cfg.volatileChop;
                case MarketStateType.Accumulation: return cfg.accumulation;
                case MarketStateType.Distribution: return cfg.distribution;
                default: return cfg.calmCorridor;
            }
        }

        private static PatternSettings GetPatternSettings(CoinSimulationConfig cfg, MarketStateType type)
        {
            switch (type)
            {
                case MarketStateType.BigSaw: return cfg.bigSaw;
                case MarketStateType.StaircaseShiftUp: return cfg.staircaseShiftUp;
                case MarketStateType.StaircaseShiftDown: return cfg.staircaseShiftDown;
                case MarketStateType.FalseBreakoutUp: return cfg.falseBreakoutUp;
                case MarketStateType.FalseBreakoutDown: return cfg.falseBreakoutDown;
                case MarketStateType.PumpAndCorrection: return cfg.pumpAndCorrection;
                case MarketStateType.DumpAndRecovery: return cfg.dumpAndRecovery;
                case MarketStateType.CompressionBreakoutNew: return cfg.compressionBreakoutNew;
                default: return cfg.bigSaw;
            }
        }

        private static bool IsLegacyState(MarketStateType type)
        {
            return (int)type < 100;
        }

        private static float SmoothStep01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        // --- Start Price ---

        private void ApplyStartPriceOverrides()
        {
            for (var i = 0; i < coins.Length; i++)
            {
                var cfg = coins[i];
                if (cfg == null)
                    continue;

                if (TryGetStartPrice(cfg.id, out var price))
                    cfg.initialPrice = price;
            }
        }

        private bool TryGetStartPrice(CurrencyId id, out float price)
        {
            for (var i = 0; i < startPrices.Length; i++)
            {
                if (startPrices[i].id != id)
                    continue;

                price = CurrencyMarket.SanitizePrice(startPrices[i].price);
                return true;
            }

            price = 0f;
            return false;
        }

        // --- History Warmup ---

        private void WarmupHistoryIfNeeded()
        {
            if (priceHistoryStore == null || initialHistoryWarmupTicks <= 0)
                return;

            _warmupInProgress = true;
            try
            {
                foreach (var kv in _runtime)
                {
                    var id = kv.Key;
                    var r = kv.Value;

                    if (priceHistoryStore.TryGet(id, out _))
                        continue;

                    var ticks = Mathf.Max(2, initialHistoryWarmupTicks);
                    var dt = Mathf.Max(0.05f, r.config.tickIntervalSeconds);
                    var samples = new List<float>(ticks);

                    samples.Add(r.visiblePrice);
                    for (var step = 1; step < ticks; step++)
                    {
                        r.time += dt;
                        Tick(r, dt);
                        samples.Add(r.visiblePrice);
                    }

                    priceHistoryStore.SetAll(id, samples);

                    if (writeToCurrencyMarket && market != null)
                        market.SetPrice(id, r.visiblePrice);
                }
            }
            finally
            {
                _warmupInProgress = false;
            }
        }

        // --- Realtime catch-up ---

        private void OnApplicationPause(bool pause)
        {
            if (pause)
                MarkResumeCatchUpPending();
        }

        private void OnApplicationFocus(bool focus)
        {
            if (!focus)
                MarkResumeCatchUpPending();
        }

        private float GetRuntimeDeltaSeconds()
        {
            if (Time.timeScale <= 0f)
            {
                if (!_resumeCatchUpPending)
                    ResetRealtimeClock();
                return 0f;
            }

            var now = GetUtcSecondsPrecise();
            if (_lastRealtimeUtcSeconds <= 0d)
            {
                _lastRealtimeUtcSeconds = now;
                _resumeCatchUpPending = false;
                return Time.deltaTime;
            }

            var elapsed = now - _lastRealtimeUtcSeconds;
            _lastRealtimeUtcSeconds = now;
            _resumeCatchUpPending = false;

            if (elapsed <= 0d)
                return Time.deltaTime;

            return Mathf.Min((float)elapsed, 900f); // max 15 min catch-up
        }

        private void MarkResumeCatchUpPending()
        {
            _resumeCatchUpPending = true;
            ResetRealtimeClock();
        }

        private void ResetRealtimeClock()
        {
            _lastRealtimeUtcSeconds = GetUtcSecondsPrecise();
        }

        private static double GetUtcSecondsPrecise()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000d;
        }

        // --- Debug & Logging ---

        private void LogActiveMarketStates(string reason)
        {
            if (_runtime.Count == 0)
            {
                Debug.Log($"[MarketSimulation] {reason}: no runtime data yet.", this);
                return;
            }

            var message = $"[MarketSimulation] {reason}\n";
            foreach (var kv in _runtime)
            {
                var r = kv.Value;
                var stateName = r.inPattern ? $"{r.patternType}(P)" : r.currentState.ToString();
                message += $"{r.config.id}: state={stateName}, timeLeft={r.stateTimeLeft:0.0}s, " +
                           $"price={r.visiblePrice:0.##}, corridor=[{r.corridorMin:0.##}..{r.corridorMax:0.##}]\n";
            }

            Debug.Log(message, this);
        }

        [ContextMenu("Debug/Log Active Market States")]
        private void LogActiveMarketStatesFromContextMenu()
        {
            LogActiveMarketStates("Manual market states log");
        }

        [ContextMenu("Debug/Reset all saves")]
        private void Debug_ResetAllSaves()
        {
            TraidingIDLE.Saves.SaveStorage.DeleteAll();
            TraidingIDLE.Saves.SaveStorage.Flush();
            Debug.Log($"[{nameof(MarketSimulation)}] All saves cleared. Restart Play Mode.", this);
        }

        [ContextMenu("Debug/Apply Selected State Now")]
        private void ApplyDebugSelectedStateNow()
        {
            if (!Application.isPlaying)
            {
                Debug.Log($"[{nameof(MarketSimulation)}] Enter Play Mode to apply a debug state.", this);
                return;
            }

            var applied = 0;
            foreach (var kv in _runtime)
            {
                var r = kv.Value;
                if (debugOverrideActiveCurrencyOnly && market != null && market.ActiveCurrency != r.config.id)
                    continue;
                if (!debugOverrideActiveCurrencyOnly && r.config.id != debugOverrideCurrency)
                    continue;

                r.inPattern = false;
                r.currentState = debugOverrideStateType;
                r.stateTimeLeft = debugStateDurationSeconds;
                applied++;
            }

            Debug.Log($"[{nameof(MarketSimulation)}] Applied debug state {debugOverrideStateType} to {applied} coin(s).", this);
        }

        // --- Balance Simulation ---

        [ContextMenu("Debug/Run Market Balance Simulation")]
        private void Debug_RunMarketBalanceSimulation()
        {
            Debug.Log(RunMarketBalanceSimulationReport(60f, 12), this);
        }

        public string RunMarketBalanceSimulationReport(float minutes, int runs)
        {
            minutes = Mathf.Max(1f, minutes);
            runs = Mathf.Max(1, runs);

            var randomState = UnityEngine.Random.state;
            var wasWarmup = _warmupInProgress;
            var wasBalance = _balanceSimulationInProgress;
            _warmupInProgress = true;
            _balanceSimulationInProgress = true;

            try
            {
                var aggregates = new Dictionary<CurrencyId, BalanceAggregateStats>();
                var simSeconds = minutes * 60f;

                for (var run = 0; run < runs; run++)
                {
                    for (var i = 0; i < coins.Length; i++)
                    {
                        var source = coins[i];
                        if (source == null)
                            continue;

                        var cfg = CloneConfig(source);
                        cfg.EnsureDefaults();

                        UnityEngine.Random.InitState(104729 + ((int)cfg.id + 1) * 1009 + run * 7919);
                        var stats = SimulateBalanceRun(cfg, simSeconds);

                        if (!aggregates.TryGetValue(stats.id, out var aggregate))
                        {
                            aggregate = new BalanceAggregateStats { id = stats.id };
                            aggregates.Add(stats.id, aggregate);
                        }

                        aggregate.Add(stats);
                    }
                }

                return BuildBalanceReport(minutes, runs, aggregates);
            }
            finally
            {
                UnityEngine.Random.state = randomState;
                _warmupInProgress = wasWarmup;
                _balanceSimulationInProgress = wasBalance;
            }
        }

        private BalanceRunStats SimulateBalanceRun(CoinSimulationConfig cfg, float simSeconds)
        {
            var startPrice = Mathf.Max(cfg.minPrice, cfg.initialPrice);
            var r = CreateRuntime(cfg, startPrice);
            var stats = new BalanceRunStats
            {
                id = cfg.id,
                startPrice = startPrice,
                finalPrice = startPrice,
                minPrice = startPrice,
                maxPrice = startPrice,
            };

            var elapsed = 0f;
            var previousPrice = (double)startPrice;
            var peak = previousPrice;
            var trough = previousPrice;
            var absMoveSum = 0d;

            while (elapsed < simSeconds)
            {
                var dt = Mathf.Min(r.config.tickIntervalSeconds, simSeconds - elapsed);
                if (dt <= 0f)
                    break;

                r.time += dt;
                Tick(r, dt);
                elapsed += dt;

                var price = (double)r.visiblePrice;
                stats.finalPrice = price;
                stats.minPrice = Math.Min(stats.minPrice, price);
                stats.maxPrice = Math.Max(stats.maxPrice, price);

                if (previousPrice > 0d)
                    absMoveSum += Math.Abs(price - previousPrice) / previousPrice;

                peak = Math.Max(peak, price);
                if (peak > 0d)
                    stats.maxDrawdown01 = Math.Max(stats.maxDrawdown01, (peak - price) / peak);

                trough = Math.Min(trough, price);
                if (trough > 0d)
                    stats.bestSwing01 = Math.Max(stats.bestSwing01, (price - trough) / trough);

                stats.ticks++;
                previousPrice = price;
            }

            stats.averageAbsTickMove01 = stats.ticks > 0 ? absMoveSum / stats.ticks : 0d;
            return stats;
        }

        private static CoinSimulationConfig CloneConfig(CoinSimulationConfig source)
        {
            var json = JsonUtility.ToJson(source);
            return JsonUtility.FromJson<CoinSimulationConfig>(json);
        }

        private static string BuildBalanceReport(
            float minutes,
            int runs,
            Dictionary<CurrencyId, BalanceAggregateStats> aggregates)
        {
            var sb = new StringBuilder(2048);
            sb.AppendLine($"[Market Balance Simulation] {minutes:0.#}m, {runs} runs");

            AppendBalanceAggregate(sb, aggregates, CurrencyId.SHT);
            AppendBalanceAggregate(sb, aggregates, CurrencyId.ETH);
            AppendBalanceAggregate(sb, aggregates, CurrencyId.BTC);

            return sb.ToString();
        }

        private static void AppendBalanceAggregate(
            StringBuilder sb,
            Dictionary<CurrencyId, BalanceAggregateStats> aggregates,
            CurrencyId id)
        {
            if (!aggregates.TryGetValue(id, out var stats) || stats.runs <= 0)
                return;

            var runs = stats.runs;
            var start = stats.startPriceSum / runs;
            var final = stats.finalPriceSum / runs;
            var min = stats.minPriceSum / runs;
            var max = stats.maxPriceSum / runs;

            sb.AppendLine();
            sb.AppendLine($"{id}:");
            sb.AppendLine($"  start avg: {FormatMoney(start)}");
            sb.AppendLine($"  final avg: {FormatMoney(final)} (x{final / start:0.00})");
            sb.AppendLine($"  min avg: {FormatMoney(min)}, max avg: {FormatMoney(max)}");
            sb.AppendLine($"  best swing avg/best: {stats.bestSwingSum / runs * 100:0.#}% / {stats.bestSwingBest * 100:0.#}%");
            sb.AppendLine($"  drawdown avg/worst: {stats.maxDrawdownSum / runs * 100:0.#}% / {stats.maxDrawdownWorst * 100:0.#}%");
            sb.AppendLine($"  avg tick move: {stats.averageAbsTickMoveSum / runs * 100:0.##}%");
            sb.AppendLine($"  ticks: {stats.ticksSum / runs}");
        }

        private static string FormatMoney(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return "0";
            return Math.Round(value).ToString("#,0").Replace(",", ".");
        }

        // --- Business Skill Overrides ---

        public void EnqueueBusinessSkillOverrideNextState(
            CurrencyId currency,
            Vector2 growDurationSeconds,
            Vector2 dumpDurationSeconds,
            Vector2 growthPercent,
            float returnTolerancePercent)
        {
            float growDuration = RandomRangeSafe(growDurationSeconds, 1f);
            float dumpDuration = RandomRangeSafe(dumpDurationSeconds, 0.25f);
            float growth = RandomRangeSafe(growthPercent, 0.01f);

            EnqueueBusinessSkillOverrideNextState(
                currency,
                growDuration,
                dumpDuration,
                growth,
                returnTolerancePercent);
        }

        private static float RandomRangeSafe(Vector2 range, float min)
        {
            float x = Mathf.Max(min, range.x);
            float y = Mathf.Max(min, range.y);
            float from = Mathf.Min(x, y);
            float to = Mathf.Max(x, y);

            return Mathf.Approximately(from, to)
                ? from
                : UnityEngine.Random.Range(from, to);
        }
    }
}
