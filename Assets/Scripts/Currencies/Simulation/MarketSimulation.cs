using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TraidingIDLE.Currencies;

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
        private struct PlannedState
        {
            public MarketStateType type;
            public float durationSeconds;
        }

        private enum SpikeDumpPhase
        {
            Grow,
            Dump,
            Shake,
        }

        private enum DipPumpPhase
        {
            Dip,
            Pump,
            Shake,
        }

        [Serializable]
        private sealed class CoinRuntime
        {
            public CoinSimulationConfig config = null!;
            public float tickTimer;

            public float rawPrice;
            public float visiblePrice;

            public float corridorLow;
            public float corridorHigh;
            public float corridorAnchor;
            public float stateCorridorWidth;
            public float stateCorridorPullStrength = 0.35f;

            public bool crashArmed;
            public float crashThresholdX;

            public MarketStateType currentState;
            public float stateTimeLeft;
            public readonly Queue<PlannedState> plan = new();

            public float chopTargetPrice;
            public float chopPhaseDuration;
            public float chopPhaseTimeLeft;
            public int chopDirection = 1;
            public bool chopFakeoutUsed;
            public float chopFakeoutStartTime;
            public float chopFakeoutTimeLeft;
            public float chopRecoveryTimeLeft;

            public float calmTargetPrice;
            public float calmLegDuration;
            public float calmLegTimeLeft;
            public bool calmLegInitialized;

            public float longUptrendStartPrice;
            public float longUptrendTargetPrice;
            public float longUptrendTargetAnchor;
            public float longUptrendDuration;
            public float longUptrendTimeLeft;
            public bool longUptrendInitialized;
            public float longUptrendPullbackTimeLeft;
            public float longUptrendPullbackCooldown;
            public float longUptrendPullbackPower;
            public float longUptrendRecoveryTimeLeft;

            public float longDowntrendStartPrice;
            public float longDowntrendTargetPrice;
            public float longDowntrendTargetAnchor;
            public float longDowntrendDuration;
            public float longDowntrendTimeLeft;
            public bool longDowntrendInitialized;
            public float longDowntrendRallyTimeLeft;
            public float longDowntrendRallyCooldown;
            public float longDowntrendRallyPower;
            public float longDowntrendRecoveryTimeLeft;

            public SpikeDumpPhase spikeDumpPhase;
            public float spikeDumpPhaseStartPrice;
            public float spikeDumpTargetPrice;
            public float spikeDumpPhaseDuration;
            public float spikeDumpPhaseTimeLeft;
            public float spikeDumpGrowPercent;
            public int spikeDumpShakeMovesLeft;
            public bool spikeDumpInitialized;

            public DipPumpPhase dipPumpPhase;
            public float dipPumpPhaseStartPrice;
            public float dipPumpTargetPrice;
            public float dipPumpPhaseDuration;
            public float dipPumpPhaseTimeLeft;
            public float dipPumpDipPercent;
            public int dipPumpShakeMovesLeft;
            public bool dipPumpInitialized;

            public float deadFlatRocketBasePrice;
            public float deadFlatRocketPumpStartPrice;
            public float deadFlatRocketTargetPrice;
            public float deadFlatRocketFlatTimeLeft;
            public float deadFlatRocketPumpDuration;
            public float deadFlatRocketPumpTimeLeft;
            public bool deadFlatRocketPumpStarted;
            public bool deadFlatRocketInitialized;

            public float deadFlatDumpBasePrice;
            public float deadFlatDumpStartPrice;
            public float deadFlatDumpTargetPrice;
            public float deadFlatDumpFlatTimeLeft;
            public float deadFlatDumpDuration;
            public float deadFlatDumpTimeLeft;
            public bool deadFlatDumpStarted;
            public bool deadFlatDumpInitialized;

            public int seed;
            public float time;
        }

        [Header("Target market")]
        [SerializeField] private CurrencyMarket market = null!;
        [SerializeField] private PriceHistoryStore? priceHistoryStore = null;
        [SerializeField] private bool useCurrencyMarketPricesOnStart = false;
        [Tooltip("On first launch (no saved history), warm up each coin by running this many ticks so the chart isn't empty.")]
        [SerializeField, Min(0)] private int initialHistoryWarmupTicks = 50;

        [Header("Game start")]
        [SerializeField] private bool applyGameStartSettings = true;
        [SerializeField] private CurrencyId startActiveCurrency = CurrencyId.SHT;
        [SerializeField] private StartPrice[] startPrices =
        {
            new() { id = CurrencyId.SHT, price = 50f },
            new() { id = CurrencyId.ETH, price = 2500f },
            new() { id = CurrencyId.BTC, price = 65000f },
        };

        [Header("Coins (3 configs)")]
        [SerializeField] private CoinSimulationConfig[] coins =
        {
            new()
            {
                id = CurrencyId.SHT,
                tickIntervalSeconds = 0.6f,
                normalTickSpeedMultiplier = 1f,
                pumpTickSpeedMultiplier = 1.35f,
                crashTickSpeedMultiplier = 1.6f,
                initialPrice = 50f,
                corridorWidthAtLowPrice = 2.8f,
                corridorWidthAtHighPrice = 1.5f,
                highPriceReference = 3000f,
                chopPhaseDurationMinSeconds = 10f,
                chopPhaseDurationMaxSeconds = 35f,
                normalMaxPriceChangePerTick01 = 0.12f,
                pumpMaxPriceChangePerTick01 = 0.35f,
                crashMaxPriceChangePerTick01 = 0.45f,
                roundingRules = new[] { new PriceRoundingRule { minPrice = 0f, step = 5f } },
                crashFirstTriggerPrice = 500f,
            },
            new()
            {
                id = CurrencyId.ETH,
                tickIntervalSeconds = 1.0f,
                normalTickSpeedMultiplier = 1f,
                pumpTickSpeedMultiplier = 1.25f,
                crashTickSpeedMultiplier = 1.45f,
                initialPrice = 2500f,
                corridorWidthAtLowPrice = 1.8f,
                corridorWidthAtHighPrice = 1.25f,
                highPriceReference = 50000f,
                chopPhaseDurationMinSeconds = 10f,
                chopPhaseDurationMaxSeconds = 35f,
                normalMaxPriceChangePerTick01 = 0.07f,
                pumpMaxPriceChangePerTick01 = 0.25f,
                crashMaxPriceChangePerTick01 = 0.35f,
                roundingRules = new[]
                {
                    new PriceRoundingRule { minPrice = 0f, step = 1f },
                    new PriceRoundingRule { minPrice = 1000f, step = 5f },
                    new PriceRoundingRule { minPrice = 5000f, step = 10f },
                    new PriceRoundingRule { minPrice = 20000f, step = 50f },
                },
                crashFirstTriggerPrice = 8000f,
            },
            new()
            {
                id = CurrencyId.BTC,
                tickIntervalSeconds = 1.4f,
                normalTickSpeedMultiplier = 1f,
                pumpTickSpeedMultiplier = 1.15f,
                crashTickSpeedMultiplier = 1.3f,
                initialPrice = 65000f,
                corridorWidthAtLowPrice = 1.45f,
                corridorWidthAtHighPrice = 1.18f,
                highPriceReference = 800000f,
                chopPhaseDurationMinSeconds = 10f,
                chopPhaseDurationMaxSeconds = 35f,
                normalMaxPriceChangePerTick01 = 0.04f,
                pumpMaxPriceChangePerTick01 = 0.12f,
                crashMaxPriceChangePerTick01 = 0.18f,
                roundingRules = new[]
                {
                    new PriceRoundingRule { minPrice = 0f, step = 10f },
                    new PriceRoundingRule { minPrice = 10000f, step = 50f },
                    new PriceRoundingRule { minPrice = 50000f, step = 100f },
                    new PriceRoundingRule { minPrice = 200000f, step = 500f },
                    new PriceRoundingRule { minPrice = 500000f, step = 1000f },
                },
                crashFirstTriggerPrice = 120000f,
            },
        };

        [Header("Corridor change magnitudes")]
        [Range(0f, 1f)]
        [SerializeField] private float corridorChangeChanceOnStateChange = 0.35f;

        [Min(0f)]
        [SerializeField] private float minorChangeMinPercent = 0.10f;
        [Min(0f)]
        [SerializeField] private float minorChangeMaxPercent = 0.50f;

        [Min(0f)]
        [SerializeField] private float majorChangeMinPercent = 0.50f;
        [Min(0f)]
        [SerializeField] private float majorChangeMaxPercent = 0.90f;

        [Min(1f)]
        [SerializeField] private float hugeChangeMinMultiplier = 1.0f;
        [Min(1f)]
        [SerializeField] private float hugeChangeMaxMultiplier = 3.0f;

        [Header("Debug")]
        [SerializeField] private bool writeToCurrencyMarket = true;
        [SerializeField] private bool logMarketStatesOnStart = true;
        [SerializeField] private bool logMarketStatesOnStateChange = true;
        [SerializeField] private bool logActiveCurrencyChopDebug = true;

        [Header("Debug state override")]
        [SerializeField] private bool debugOverrideState = false;
        [SerializeField] private bool debugOverrideActiveCurrencyOnly = true;
        [SerializeField] private CurrencyId debugOverrideCurrency = CurrencyId.SHT;
        [SerializeField] private MarketStateType debugOverrideStateType = MarketStateType.Calm;
        [Min(1f)]
        [SerializeField] private float debugStateDurationSeconds = 120f;

        private readonly Dictionary<CurrencyId, CoinRuntime> _runtime = new();

        private bool _warmupInProgress;

        private void Awake()
        {
            if (market == null)
                market = GetComponent<CurrencyMarket>();

            if (priceHistoryStore == null)
                priceHistoryStore = GetComponent<PriceHistoryStore>();
            if (priceHistoryStore == null)
                priceHistoryStore = FindFirstObjectByType<PriceHistoryStore>();
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

                NormalizeConfig(cfg);

                var startPrice = cfg.initialPrice;
                if ((useCurrencyMarketPricesOnStart || loadedFromSave) && market != null)
                    startPrice = market.GetPrice(cfg.id);

                var r = new CoinRuntime
                {
                    config = cfg,
                    rawPrice = startPrice,
                    visiblePrice = RoundPrice(cfg, startPrice),
                    corridorAnchor = startPrice,
                    seed = UnityEngine.Random.Range(1, int.MaxValue),
                };

                RebuildCorridor(r, r.rawPrice, force: true);
                BuildInitialPlan(r);
                ApplyNextPlannedState(r);

                _runtime[cfg.id] = r;

                if (writeToCurrencyMarket && market != null)
                    market.SetPrice(cfg.id, r.visiblePrice);
            }

            WarmupHistoryIfNeeded();

            if (logMarketStatesOnStart)
                LogActiveMarketStates("Market simulation started");
        }

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
                    {
                        // Push current price as the latest sample only if we don't already have one.
                        continue;
                    }

                    var ticks = Mathf.Max(2, initialHistoryWarmupTicks);
                    var dt = Mathf.Max(0.05f, GetEffectiveTickInterval(r));
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

        private void ApplyGameStartSettings()
        {
            if (market != null)
                market.SetActiveCurrency(startActiveCurrency);

            ApplyStartPriceOverrides();
        }

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

                price = Mathf.Max(0.000001f, startPrices[i].price);
                return true;
            }

            price = 0f;
            return false;
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            foreach (var kv in _runtime)
            {
                var r = kv.Value;
                r.tickTimer += dt;
                r.time += dt;

                var interval = GetEffectiveTickInterval(r);
                while (r.tickTimer >= interval)
                {
                    r.tickTimer -= interval;
                    Tick(r, interval);
                    interval = GetEffectiveTickInterval(r);
                }
            }
        }

        private void Tick(CoinRuntime r, float dt)
        {
            if (IsDebugStateOverrideTarget(r))
            {
                if (r.currentState != debugOverrideStateType)
                    EnterState(r, debugOverrideStateType, debugStateDurationSeconds, changeCorridor: false);

                r.stateTimeLeft = Mathf.Max(r.stateTimeLeft, dt + 0.01f);
            }
            else
            {
                r.stateTimeLeft -= dt;
                if (r.stateTimeLeft <= 0f)
                {
                    ApplyNextPlannedState(r);
                    EnsurePlannedStates(r);

                    if (logMarketStatesOnStateChange && !_warmupInProgress)
                        LogActiveMarketStates($"{r.config.id} changed market state");
                }
            }

            ArmCrashIfNeeded(r);
            ForceCrashIfThresholdReached(r);

            var nextRaw = SimulateNextRawPrice(r, dt);
            nextRaw = Mathf.Max(0f, nextRaw);
            nextRaw = LimitPriceChangePerTick(r, nextRaw);

            r.rawPrice = nextRaw;
            r.visiblePrice = RoundPrice(r.config, r.rawPrice);

            if (_warmupInProgress)
                return;

            if (writeToCurrencyMarket && market != null)
                market.SetPrice(r.config.id, r.visiblePrice);

            if (priceHistoryStore != null)
                priceHistoryStore.Push(r.config.id, r.visiblePrice);
        }

        private float SimulateNextRawPrice(CoinRuntime r, float dt)
        {
            var cfg = r.config;

            if (r.corridorHigh <= r.corridorLow + 0.000001f)
                RebuildCorridor(r, r.rawPrice, force: true);

            var range = r.corridorHigh - r.corridorLow;
            var pos = Mathf.InverseLerp(r.corridorLow, r.corridorHigh, r.rawPrice);
            var corridorCenter = (r.corridorLow + r.corridorHigh) * 0.5f;

            // Noise: smooth + per-coin seed
            var noiseA = Mathf.PerlinNoise((r.seed * 0.001f) + r.time * 0.35f, 0.1f) * 2f - 1f;
            var noiseB = Mathf.PerlinNoise((r.seed * 0.002f) + r.time * 0.75f, 0.7f) * 2f - 1f;
            var noise = (noiseA * 0.65f + noiseB * 0.35f);

            float desiredPos;
            float impulse;

            switch (r.currentState)
            {
                case MarketStateType.ChopInCorridor:
                    return SimulateChopInCorridor(r, dt, noise, range);
                case MarketStateType.Calm:
                    return SimulateCalm(r, dt, noise, range);
                case MarketStateType.LongUptrend:
                    return SimulateLongUptrend(r, dt, noise, range);
                case MarketStateType.LongDowntrend:
                    return SimulateLongDowntrend(r, dt, noise, range);
                case MarketStateType.SlowUpThenDump:
                    return SimulateSpikeThenDump(r, dt, noise, range);
                case MarketStateType.SlowUpThenPumpAndDump:
                    return SimulateDipThenPump(r, dt, noise, range);
                case MarketStateType.DeadFlatThenRocketPump:
                    return SimulateDeadFlatThenRocketPump(r, dt, noise, range);
                case MarketStateType.DeadFlatThenRocketDump:
                    return SimulateDeadFlatThenRocketDump(r, dt, noise, range);
                case MarketStateType.Flat:
                    desiredPos = Mathf.Clamp01(0.5f + noise * 0.05f);
                    impulse = -0.05f;
                    break;
                case MarketStateType.MarketCrash:
                    desiredPos = 0.08f;
                    impulse = -2.0f;
                    break;
                default:
                    desiredPos = 0.5f;
                    impulse = 0f;
                    break;
            }

            desiredPos = Mathf.Clamp01(desiredPos);

            // Mean reversion to desiredPos inside the corridor (soft, not a hard clamp).
            var target = Mathf.Lerp(r.corridorLow, r.corridorHigh, desiredPos);
            var toTarget = target - r.rawPrice;

            // Extra pull to corridor center when outside (soft corridor walls).
            var outsidePull = 0f;
            if (r.rawPrice > r.corridorHigh) outsidePull = (r.corridorHigh - r.rawPrice);
            else if (r.rawPrice < r.corridorLow) outsidePull = (r.corridorLow - r.rawPrice);

            var drift = (toTarget / Mathf.Max(1f, range)) * cfg.meanReversionStrength;
            drift += impulse * cfg.trendStrength;

            var noiseMove = noise * cfg.noiseStrength;

            // Convert normalized movement into price delta scaled by corridor range.
            var delta = (drift + noiseMove) * range * dt;
            delta += outsidePull * r.stateCorridorPullStrength; // pull back if we leaked out

            var next = r.rawPrice + delta;

            // Corridor can adapt slowly to follow price (anchor creeps), especially in trends.
            var anchorLerp = r.currentState == MarketStateType.Flat ? 0.01f : 0.03f;
            r.corridorAnchor = Mathf.Lerp(r.corridorAnchor, corridorCenter, anchorLerp);

            // Rebuild corridor gently each tick (keeps volatility shrinking as price rises).
            RebuildCorridor(r, next, force: false);

            // During crash: pull corridor down faster.
            if (r.currentState == MarketStateType.MarketCrash)
            {
                var recoverTarget = cfg.initialPrice * cfg.crashRecoverToInitialMultiplier;
                r.corridorAnchor = Mathf.Lerp(r.corridorAnchor, recoverTarget, 0.08f);
                RebuildCorridor(r, next, force: false);
            }

            return next;
        }

        private float SimulateChopInCorridor(CoinRuntime r, float dt, float noise, float range)
        {
            if (r.chopPhaseTimeLeft <= 0f || r.chopTargetPrice <= 0f)
            {
                var pos = Mathf.InverseLerp(r.corridorLow, r.corridorHigh, r.rawPrice);
                var firstDirection = pos <= 0.5f ? 1 : -1;
                StartNewChopPhase(r, firstDirection);
            }

            var elapsed = r.chopPhaseDuration - r.chopPhaseTimeLeft;
            if (!r.chopFakeoutUsed && r.chopPhaseDuration > 10f && elapsed >= r.chopFakeoutStartTime)
            {
                r.chopFakeoutUsed = true;
                r.chopFakeoutTimeLeft = UnityEngine.Random.Range(
                    r.config.chopFakeoutDurationMinSeconds,
                    r.config.chopFakeoutDurationMaxSeconds);
                r.chopRecoveryTimeLeft = 0f;

                if (ShouldLogActiveCurrency(r))
                {
                    Debug.Log(
                        $"[ChopInCorridor] {r.config.id}: fakeout started. " +
                        $"Main direction={FormatChopDirection(r.chopDirection)}, " +
                        $"fakeout duration={r.chopFakeoutTimeLeft:0.0}s, recovery={r.config.chopRecoveryDurationSeconds:0.0}s.",
                        this);
                }
            }

            var effectiveDirection = r.chopDirection;
            var fakeoutActive = r.chopFakeoutTimeLeft > 0f;
            if (fakeoutActive)
            {
                effectiveDirection *= -1;
                r.chopFakeoutTimeLeft -= dt;
                if (r.chopFakeoutTimeLeft <= 0f)
                    r.chopRecoveryTimeLeft = r.config.chopRecoveryDurationSeconds;
            }

            var boost = 1f;
            if (!fakeoutActive && r.chopRecoveryTimeLeft > 0f)
            {
                boost = r.config.chopRecoveryBoost;
                r.chopRecoveryTimeLeft -= dt;
            }

            var remaining = Mathf.Max(dt, r.chopPhaseTimeLeft);
            var guidedDelta = (r.chopTargetPrice - r.rawPrice) / remaining * dt * 0.45f;

            // Random step: biased toward the target, but still often moves against it.
            var targetDirection = r.chopTargetPrice >= r.rawPrice ? 1 : -1;
            var chanceTowardTarget = fakeoutActive ? 0.18f : 0.68f;
            var randomDirection = UnityEngine.Random.value <= chanceTowardTarget ? targetDirection : -targetDirection;
            var randomMagnitude = UnityEngine.Random.Range(0.45f, 1.35f);
            var noisyDelta = randomDirection * range * r.config.noiseStrength * 0.30f * dt * randomMagnitude;

            // Smooth noise prevents the random walk from looking like pure coin flips.
            noisyDelta += noise * range * r.config.noiseStrength * 0.12f * dt;

            if (fakeoutActive)
            {
                // Fakeout: several ticks against the target, then the boost above pulls it back.
                guidedDelta = 0f;
                noisyDelta = effectiveDirection * range * r.config.trendStrength * r.config.chopFakeoutStrength * dt;
                noisyDelta += noise * range * r.config.noiseStrength * 0.08f * dt;
            }

            var outsidePull = 0f;
            if (r.rawPrice > r.corridorHigh) outsidePull = (r.corridorHigh - r.rawPrice);
            else if (r.rawPrice < r.corridorLow) outsidePull = (r.corridorLow - r.rawPrice);

            var next = r.rawPrice + (guidedDelta + noisyDelta) * boost + outsidePull * r.stateCorridorPullStrength;

            var corridorCenter = (r.corridorLow + r.corridorHigh) * 0.5f;
            r.corridorAnchor = Mathf.Lerp(r.corridorAnchor, corridorCenter, 0.025f);
            RebuildCorridor(r, next, force: false);

            r.chopPhaseTimeLeft -= dt;
            if (r.chopPhaseTimeLeft <= 0f)
                StartNewChopPhase(r, -r.chopDirection);

            return next;
        }

        private float SimulateCalm(CoinRuntime r, float dt, float noise, float range)
        {
            if (r.calmLegTimeLeft <= 0f || r.calmTargetPrice <= 0f)
                StartNewCalmLeg(r);

            var remaining = Mathf.Max(0.0001f, r.calmLegTimeLeft);
            var cfg = r.config;
            var guidedDelta = (r.calmTargetPrice - r.rawPrice) / remaining * dt * cfg.calmApproachStrength;

            var noisyDelta = noise * range * cfg.calmNoiseStrength * dt;
            var microWiggle = Mathf.PerlinNoise((r.seed * 0.0037f) + r.time * 1.1f, 0.3f) * 2f - 1f;
            noisyDelta += microWiggle * range * cfg.calmNoiseStrength * 0.35f * dt;

            var outsidePull = 0f;
            if (r.rawPrice > r.corridorHigh) outsidePull = (r.corridorHigh - r.rawPrice);
            else if (r.rawPrice < r.corridorLow) outsidePull = (r.corridorLow - r.rawPrice);

            var next = r.rawPrice + guidedDelta + noisyDelta + outsidePull * r.stateCorridorPullStrength;

            var corridorCenter = (r.corridorLow + r.corridorHigh) * 0.5f;
            r.corridorAnchor = Mathf.Lerp(r.corridorAnchor, corridorCenter, 0.02f);
            RebuildCorridor(r, next, force: false);

            r.calmLegTimeLeft -= dt;
            if (r.calmLegTimeLeft <= 0f)
                StartNewCalmLeg(r);

            return next;
        }

        private void StartNewCalmLeg(CoinRuntime r)
        {
            var cfg = r.config;
            var range = Mathf.Max(0.000001f, r.corridorHigh - r.corridorLow);
            var center = (r.corridorLow + r.corridorHigh) * 0.5f;
            var half = range * 0.5f;

            var fromPrice = r.calmLegInitialized ? r.calmTargetPrice : r.rawPrice;

            var offMin = Mathf.Min(cfg.calmOffsetFromHalfMin01, cfg.calmOffsetFromHalfMax01);
            var offMax = Mathf.Max(cfg.calmOffsetFromHalfMin01, cfg.calmOffsetFromHalfMax01);
            offMin = Mathf.Clamp(offMin, 0f, 0.49f);
            offMax = Mathf.Clamp(offMax, offMin, 0.49f);

            var dir = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            var offsetT = UnityEngine.Random.Range(offMin, offMax);
            var newTarget = center + dir * offsetT * half;
            newTarget = Mathf.Clamp(newTarget, r.corridorLow, r.corridorHigh);

            var relDist = Mathf.Abs(newTarget - fromPrice) / range;
            var durMin = Mathf.Min(cfg.calmLegDurationMinSeconds, cfg.calmLegDurationMaxSeconds);
            var durMax = Mathf.Max(cfg.calmLegDurationMinSeconds, cfg.calmLegDurationMaxSeconds);
            var legDur = UnityEngine.Random.Range(durMin, durMax);

            if (relDist <= cfg.calmSmallMoveRelativeThreshold)
                legDur = Mathf.Min(legDur, cfg.calmShortLegMaxSeconds);

            legDur = Mathf.Max(0.5f, legDur);

            r.calmTargetPrice = newTarget;
            r.calmLegDuration = legDur;
            r.calmLegTimeLeft = legDur;
            r.calmLegInitialized = true;
        }

        private static void ResetCalm(CoinRuntime r)
        {
            r.calmTargetPrice = 0f;
            r.calmLegDuration = 0f;
            r.calmLegTimeLeft = 0f;
            r.calmLegInitialized = false;
        }

        private float SimulateLongUptrend(CoinRuntime r, float dt, float noise, float range)
        {
            if (!r.longUptrendInitialized)
                StartLongUptrend(r);

            var cfg = r.config;
            var elapsed = Mathf.Clamp(r.longUptrendDuration - r.longUptrendTimeLeft, 0f, r.longUptrendDuration);
            var progress = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, r.longUptrendDuration));
            var nextProgress = Mathf.Clamp01((elapsed + dt) / Mathf.Max(0.0001f, r.longUptrendDuration));
            var targetProgress = TrendProgress01(nextProgress);
            var scheduledPrice = Mathf.Lerp(r.longUptrendStartPrice, r.longUptrendTargetPrice, targetProgress);

            // Follow the scheduled curve instead of the final target, so the state spends all its duration growing.
            var maxCatchupDelta = Mathf.Abs(r.longUptrendTargetPrice - r.longUptrendStartPrice)
                / Mathf.Max(1f, r.longUptrendDuration)
                * dt
                * Mathf.Lerp(1.25f, 2.0f, progress);
            if (r.longUptrendRecoveryTimeLeft > 0f)
                maxCatchupDelta *= cfg.longUptrendRecoveryCatchupMultiplier;

            var guidedDelta = Mathf.Clamp(
                scheduledPrice - r.rawPrice,
                -maxCatchupDelta * 0.45f,
                maxCatchupDelta);

            var smoothNoise = noise * range * cfg.longUptrendNoiseStrength * dt;
            var microNoise = (Mathf.PerlinNoise((r.seed * 0.0043f) + r.time * 1.35f, 0.55f) * 2f - 1f)
                * range
                * cfg.longUptrendNoiseStrength
                * 0.35f
                * dt;
            var maxNoise = Mathf.Max(0.000001f, maxCatchupDelta) * 0.35f;
            smoothNoise = Mathf.Clamp(smoothNoise, -maxNoise, maxNoise * 0.45f);
            microNoise = Mathf.Clamp(microNoise, -maxNoise * 0.5f, maxNoise * 0.25f);
            var wobble = BuildLongUptrendWobble(r, dt, maxCatchupDelta);

            UpdateLongUptrendPullbackPhase(r, dt, progress);

            var pullback = 0f;
            if (r.longUptrendPullbackTimeLeft > 0f)
            {
                // Short micro-dips keep the uptrend alive without turning it into a visible downtrend.
                guidedDelta = Mathf.Min(guidedDelta, maxCatchupDelta * 0.25f);
                pullback = -Mathf.Min(
                    range * cfg.longUptrendPullbackStrength * r.longUptrendPullbackPower * dt,
                    maxCatchupDelta * 0.45f);
            }

            var outsidePull = 0f;
            if (r.rawPrice > r.corridorHigh) outsidePull = (r.corridorHigh - r.rawPrice);
            else if (r.rawPrice < r.corridorLow) outsidePull = (r.corridorLow - r.rawPrice);

            var next = r.rawPrice + guidedDelta + smoothNoise + microNoise + wobble + pullback + outsidePull * r.stateCorridorPullStrength;
            var allowedLead = Mathf.Max(GetRoundingStep(cfg, r.rawPrice), maxCatchupDelta * 1.25f);
            next = Mathf.Min(next, scheduledPrice + allowedLead);

            // The uptrend owns its target anchor for the whole state. This prevents exponential corridor creep.
            r.corridorAnchor = Mathf.Lerp(r.corridorAnchor, r.longUptrendTargetAnchor, 0.035f);
            RebuildCorridor(r, next, force: false);

            r.longUptrendTimeLeft -= dt;
            if (r.longUptrendRecoveryTimeLeft > 0f)
                r.longUptrendRecoveryTimeLeft -= dt;

            if (next >= r.longUptrendTargetPrice * 0.995f)
                r.stateTimeLeft = 0f;

            return next;
        }

        private static float BuildLongUptrendWobble(CoinRuntime r, float dt, float maxCatchupDelta)
        {
            var cfg = r.config;
            if (cfg.longUptrendWobbleStrength <= 0f)
                return 0f;

            var step = GetRoundingStep(cfg, r.rawPrice);
            var baseMove = Mathf.Max(step, maxCatchupDelta * 0.45f) * cfg.longUptrendWobbleStrength;

            if (UnityEngine.Random.value < cfg.longUptrendDownWobbleChance)
                return -baseMove * UnityEngine.Random.Range(0.35f, cfg.longUptrendDownWobbleMultiplier);

            return baseMove * UnityEngine.Random.Range(0.10f, cfg.longUptrendUpWobbleMultiplier);
        }

        private void UpdateLongUptrendPullbackPhase(CoinRuntime r, float dt, float progress)
        {
            var cfg = r.config;

            if (r.longUptrendPullbackCooldown > 0f)
                r.longUptrendPullbackCooldown -= dt;

            if (r.longUptrendPullbackTimeLeft > 0f)
            {
                r.longUptrendPullbackTimeLeft -= dt;
                if (r.longUptrendPullbackTimeLeft <= 0f)
                {
                    r.longUptrendRecoveryTimeLeft = cfg.longUptrendRecoveryDurationSeconds;
                    r.longUptrendPullbackCooldown = Mathf.Min(cfg.longUptrendPullbackCooldownSeconds, 3.5f);
                }
                return;
            }

            // Keep the very beginning and the final push mostly clean.
            if (progress < 0.12f || progress > 0.82f)
                return;

            if (r.longUptrendPullbackCooldown > 0f)
                return;

            if (UnityEngine.Random.value >= cfg.longUptrendPullbackChancePerTick)
                return;

            var minDuration = Mathf.Min(cfg.longUptrendPullbackDurationMinSeconds, cfg.longUptrendPullbackDurationMaxSeconds);
            var maxDuration = Mathf.Max(cfg.longUptrendPullbackDurationMinSeconds, cfg.longUptrendPullbackDurationMaxSeconds);
            maxDuration = Mathf.Min(maxDuration, 1.8f);
            minDuration = Mathf.Min(minDuration, maxDuration);
            r.longUptrendPullbackTimeLeft = UnityEngine.Random.Range(minDuration, maxDuration);
            r.longUptrendPullbackPower = UnityEngine.Random.Range(0.75f, 1.35f);
            r.longUptrendRecoveryTimeLeft = 0f;
        }

        private static float SmoothStep01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private static float TrendProgress01(float t)
        {
            t = Mathf.Clamp01(t);
            return Mathf.Pow(t, 1.12f);
        }

        private void StartLongUptrend(CoinRuntime r)
        {
            var cfg = r.config;
            var minMultiplier = Mathf.Min(cfg.longUptrendTargetMultiplierMin, cfg.longUptrendTargetMultiplierMax);
            var maxMultiplier = Mathf.Max(cfg.longUptrendTargetMultiplierMin, cfg.longUptrendTargetMultiplierMax);
            var rolledMultiplier = UnityEngine.Random.Range(minMultiplier, maxMultiplier);

            // Cheap coins get the full dopamine x2/x4 feel. Expensive coins get a compressed boost
            // so one uptrend doesn't permanently break the economy near highPriceReference.
            var pricePressure = Mathf.Clamp01(r.rawPrice / Mathf.Max(0.000001f, cfg.highPriceReference * cfg.longUptrendSoftCapAtHighReference01));
            var boostKeep = Mathf.Lerp(1f, cfg.longUptrendMinBoostAfterSoftCap01, pricePressure);
            var effectiveMultiplier = 1f + (rolledMultiplier - 1f) * boostKeep;
            effectiveMultiplier = Mathf.Min(effectiveMultiplier, cfg.longUptrendMaxEffectiveMultiplier);

            r.longUptrendStartPrice = r.rawPrice;
            r.longUptrendTargetAnchor = Mathf.Max(0.000001f, r.corridorAnchor * effectiveMultiplier);

            var previousAnchor = r.corridorAnchor;
            r.corridorAnchor = r.longUptrendTargetAnchor;
            RebuildCorridor(r, r.rawPrice, force: false);
            r.longUptrendTargetPrice = Mathf.Lerp(
                r.corridorLow,
                r.corridorHigh,
                cfg.longUptrendTargetCorridorPos);
            r.corridorAnchor = previousAnchor;

            var maxTarget = r.rawPrice * cfg.longUptrendMaxEffectiveMultiplier;
            r.longUptrendTargetPrice = Mathf.Min(r.longUptrendTargetPrice, maxTarget);

            r.longUptrendDuration = Mathf.Max(1f, r.stateTimeLeft);
            r.longUptrendTimeLeft = r.longUptrendDuration;
            r.longUptrendInitialized = true;

            if (ShouldLogActiveCurrency(r))
            {
                Debug.Log(
                    $"[LongUptrend] {r.config.id}: started. " +
                    $"rolledX={rolledMultiplier:0.00}, effectiveX={effectiveMultiplier:0.00}, " +
                    $"target={r.longUptrendTargetPrice:0.##}, duration={r.longUptrendDuration:0.0}s.",
                    this);
            }
        }

        private static void ResetLongUptrend(CoinRuntime r)
        {
            r.longUptrendStartPrice = 0f;
            r.longUptrendTargetPrice = 0f;
            r.longUptrendTargetAnchor = 0f;
            r.longUptrendDuration = 0f;
            r.longUptrendTimeLeft = 0f;
            r.longUptrendInitialized = false;
            r.longUptrendPullbackTimeLeft = 0f;
            r.longUptrendPullbackCooldown = 0f;
            r.longUptrendPullbackPower = 0f;
            r.longUptrendRecoveryTimeLeft = 0f;
        }

        private float SimulateLongDowntrend(CoinRuntime r, float dt, float noise, float range)
        {
            if (!r.longDowntrendInitialized)
                StartLongDowntrend(r);

            var cfg = r.config;
            var elapsed = Mathf.Clamp(r.longDowntrendDuration - r.longDowntrendTimeLeft, 0f, r.longDowntrendDuration);
            var progress = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, r.longDowntrendDuration));
            var nextProgress = Mathf.Clamp01((elapsed + dt) / Mathf.Max(0.0001f, r.longDowntrendDuration));
            var targetProgress = SmoothStep01(nextProgress);
            var scheduledPrice = Mathf.Lerp(r.longDowntrendStartPrice, r.longDowntrendTargetPrice, targetProgress);

            var maxCatchupDelta = Mathf.Abs(r.longDowntrendStartPrice - r.longDowntrendTargetPrice)
                / Mathf.Max(1f, r.longDowntrendDuration)
                * dt
                * Mathf.Lerp(1.25f, 2.0f, progress);
            if (r.longDowntrendRecoveryTimeLeft > 0f)
                maxCatchupDelta *= cfg.longDowntrendRecoveryCatchupMultiplier;

            var guidedDelta = Mathf.Clamp(
                scheduledPrice - r.rawPrice,
                -maxCatchupDelta,
                maxCatchupDelta * 0.45f);

            var smoothNoise = noise * range * cfg.longDowntrendNoiseStrength * dt;
            var microNoise = (Mathf.PerlinNoise((r.seed * 0.0051f) + r.time * 1.35f, 0.82f) * 2f - 1f)
                * range
                * cfg.longDowntrendNoiseStrength
                * 0.35f
                * dt;
            var maxNoise = Mathf.Max(0.000001f, maxCatchupDelta) * 0.35f;
            smoothNoise = Mathf.Clamp(smoothNoise, -maxNoise * 0.45f, maxNoise);
            microNoise = Mathf.Clamp(microNoise, -maxNoise * 0.25f, maxNoise * 0.5f);

            UpdateLongDowntrendRallyPhase(r, dt, progress);

            var rally = 0f;
            if (r.longDowntrendRallyTimeLeft > 0f)
            {
                // Suppress downward guidance during the fake reversal so several green candles can happen in a row.
                guidedDelta = Mathf.Max(guidedDelta, -maxCatchupDelta * 0.12f);
                rally = Mathf.Min(
                    range * cfg.longDowntrendRallyStrength * r.longDowntrendRallyPower * dt,
                    maxCatchupDelta * 1.35f);
            }

            var outsidePull = 0f;
            if (r.rawPrice > r.corridorHigh) outsidePull = (r.corridorHigh - r.rawPrice);
            else if (r.rawPrice < r.corridorLow) outsidePull = (r.corridorLow - r.rawPrice);

            var next = r.rawPrice + guidedDelta + smoothNoise + microNoise + rally + outsidePull * r.stateCorridorPullStrength;

            r.corridorAnchor = Mathf.Lerp(r.corridorAnchor, r.longDowntrendTargetAnchor, 0.035f);
            RebuildCorridor(r, next, force: false);

            r.longDowntrendTimeLeft -= dt;
            if (r.longDowntrendRecoveryTimeLeft > 0f)
                r.longDowntrendRecoveryTimeLeft -= dt;

            if (next <= r.longDowntrendTargetPrice * 1.005f)
                r.stateTimeLeft = 0f;

            return next;
        }

        private void UpdateLongDowntrendRallyPhase(CoinRuntime r, float dt, float progress)
        {
            var cfg = r.config;

            if (r.longDowntrendRallyCooldown > 0f)
                r.longDowntrendRallyCooldown -= dt;

            if (r.longDowntrendRallyTimeLeft > 0f)
            {
                r.longDowntrendRallyTimeLeft -= dt;
                if (r.longDowntrendRallyTimeLeft <= 0f)
                {
                    r.longDowntrendRecoveryTimeLeft = cfg.longDowntrendRecoveryDurationSeconds;
                    r.longDowntrendRallyCooldown = cfg.longDowntrendRallyCooldownSeconds;
                }
                return;
            }

            if (progress < 0.12f || progress > 0.82f)
                return;

            if (r.longDowntrendRallyCooldown > 0f)
                return;

            if (UnityEngine.Random.value >= cfg.longDowntrendRallyChancePerTick)
                return;

            var minDuration = Mathf.Min(cfg.longDowntrendRallyDurationMinSeconds, cfg.longDowntrendRallyDurationMaxSeconds);
            var maxDuration = Mathf.Max(cfg.longDowntrendRallyDurationMinSeconds, cfg.longDowntrendRallyDurationMaxSeconds);
            r.longDowntrendRallyTimeLeft = UnityEngine.Random.Range(minDuration, maxDuration);
            r.longDowntrendRallyPower = UnityEngine.Random.Range(0.75f, 1.35f);
            r.longDowntrendRecoveryTimeLeft = 0f;
        }

        private void StartLongDowntrend(CoinRuntime r)
        {
            var cfg = r.config;
            var minDivider = Mathf.Min(cfg.longDowntrendTargetDividerMin, cfg.longDowntrendTargetDividerMax);
            var maxDivider = Mathf.Max(cfg.longDowntrendTargetDividerMin, cfg.longDowntrendTargetDividerMax);
            var divider = UnityEngine.Random.Range(minDivider, maxDivider);

            r.longDowntrendStartPrice = r.rawPrice;
            r.longDowntrendTargetAnchor = Mathf.Max(
                cfg.initialPrice * cfg.minCorridorLowFromInitial,
                r.corridorAnchor / divider);

            var previousAnchor = r.corridorAnchor;
            r.corridorAnchor = r.longDowntrendTargetAnchor;
            RebuildCorridor(r, r.rawPrice, force: false);
            r.longDowntrendTargetPrice = Mathf.Lerp(
                r.corridorLow,
                r.corridorHigh,
                cfg.longDowntrendTargetCorridorPos);
            r.corridorAnchor = previousAnchor;

            var minTarget = cfg.initialPrice * cfg.minCorridorLowFromInitial;
            r.longDowntrendTargetPrice = Mathf.Max(r.longDowntrendTargetPrice, minTarget);

            r.longDowntrendDuration = Mathf.Max(1f, r.stateTimeLeft);
            r.longDowntrendTimeLeft = r.longDowntrendDuration;
            r.longDowntrendInitialized = true;

            if (ShouldLogActiveCurrency(r))
            {
                Debug.Log(
                    $"[LongDowntrend] {r.config.id}: started. " +
                    $"divider={divider:0.00}, target={r.longDowntrendTargetPrice:0.##}, duration={r.longDowntrendDuration:0.0}s.",
                    this);
            }
        }

        private static void ResetLongDowntrend(CoinRuntime r)
        {
            r.longDowntrendStartPrice = 0f;
            r.longDowntrendTargetPrice = 0f;
            r.longDowntrendTargetAnchor = 0f;
            r.longDowntrendDuration = 0f;
            r.longDowntrendTimeLeft = 0f;
            r.longDowntrendInitialized = false;
            r.longDowntrendRallyTimeLeft = 0f;
            r.longDowntrendRallyCooldown = 0f;
            r.longDowntrendRallyPower = 0f;
            r.longDowntrendRecoveryTimeLeft = 0f;
        }

        private float SimulateSpikeThenDump(CoinRuntime r, float dt, float noise, float range)
        {
            if (!r.spikeDumpInitialized || r.spikeDumpPhaseTimeLeft <= 0f)
                StartNextSpikeDumpPhase(r);

            var cfg = r.config;
            var elapsed = Mathf.Clamp(r.spikeDumpPhaseDuration - r.spikeDumpPhaseTimeLeft, 0f, r.spikeDumpPhaseDuration);
            var progress = Mathf.Clamp01((elapsed + dt) / Mathf.Max(0.0001f, r.spikeDumpPhaseDuration));
            var curve = r.spikeDumpPhase == SpikeDumpPhase.Dump
                ? 1f - (1f - progress) * (1f - progress) // fast at the beginning, still reaches target by phase end
                : SmoothStep01(progress);
            var scheduledPrice = Mathf.Lerp(r.spikeDumpPhaseStartPrice, r.spikeDumpTargetPrice, curve);

            var phaseDistance = Mathf.Abs(r.spikeDumpTargetPrice - r.spikeDumpPhaseStartPrice);
            var maxCatchupDelta = phaseDistance
                / Mathf.Max(0.5f, r.spikeDumpPhaseDuration)
                * dt
                * (r.spikeDumpPhase == SpikeDumpPhase.Dump ? 3.0f : 1.8f);

            var guidedDelta = Mathf.Clamp(
                scheduledPrice - r.rawPrice,
                -maxCatchupDelta,
                maxCatchupDelta);

            var noiseScale = r.spikeDumpPhase == SpikeDumpPhase.Dump ? 0.25f : 1f;
            var noiseDelta = noise * range * cfg.spikeDumpNoiseStrength * noiseScale * dt;
            noiseDelta = Mathf.Clamp(noiseDelta, -maxCatchupDelta * 0.35f, maxCatchupDelta * 0.35f);

            var outsidePull = 0f;
            if (r.rawPrice > r.corridorHigh) outsidePull = (r.corridorHigh - r.rawPrice);
            else if (r.rawPrice < r.corridorLow) outsidePull = (r.corridorLow - r.rawPrice);

            var next = r.rawPrice + guidedDelta + noiseDelta + outsidePull * r.stateCorridorPullStrength;

            r.corridorAnchor = Mathf.Lerp(r.corridorAnchor, Mathf.Max(0.000001f, next), 0.025f);
            RebuildCorridor(r, next, force: false);

            r.spikeDumpPhaseTimeLeft -= dt;
            return next;
        }

        private void StartNextSpikeDumpPhase(CoinRuntime r)
        {
            if (!r.spikeDumpInitialized
                || (r.spikeDumpPhase == SpikeDumpPhase.Shake && r.spikeDumpShakeMovesLeft <= 0))
            {
                StartSpikeDumpGrowPhase(r);
                return;
            }

            switch (r.spikeDumpPhase)
            {
                case SpikeDumpPhase.Grow:
                    StartSpikeDumpDumpPhase(r);
                    break;
                case SpikeDumpPhase.Dump:
                    StartSpikeDumpFirstShakePhase(r);
                    break;
                case SpikeDumpPhase.Shake:
                    StartSpikeDumpShakePhase(r);
                    break;
            }
        }

        private void StartSpikeDumpGrowPhase(CoinRuntime r)
        {
            var cfg = r.config;
            var growMin = Mathf.Min(cfg.spikeDumpGrowPercentMin, cfg.spikeDumpGrowPercentMax);
            var growMax = Mathf.Max(cfg.spikeDumpGrowPercentMin, cfg.spikeDumpGrowPercentMax);
            r.spikeDumpGrowPercent = UnityEngine.Random.Range(growMin, growMax);

            r.spikeDumpPhase = SpikeDumpPhase.Grow;
            r.spikeDumpPhaseStartPrice = Mathf.Max(0.000001f, r.rawPrice);
            r.spikeDumpTargetPrice = r.spikeDumpPhaseStartPrice * (1f + r.spikeDumpGrowPercent);
            r.spikeDumpPhaseDuration = UnityEngine.Random.Range(
                cfg.spikeDumpGrowDurationMinSeconds,
                cfg.spikeDumpGrowDurationMaxSeconds);
            r.spikeDumpPhaseTimeLeft = r.spikeDumpPhaseDuration;
            r.spikeDumpShakeMovesLeft = 0;
            r.spikeDumpInitialized = true;
        }

        private void StartSpikeDumpDumpPhase(CoinRuntime r)
        {
            var cfg = r.config;
            var dropMultiplier = UnityEngine.Random.Range(cfg.spikeDumpDropOverGrowMin, cfg.spikeDumpDropOverGrowMax);
            var dropPercent = Mathf.Min(cfg.spikeDumpMaxDropPercent, r.spikeDumpGrowPercent * dropMultiplier);
            var minPrice = cfg.initialPrice * cfg.minCorridorLowFromInitial;

            r.spikeDumpPhase = SpikeDumpPhase.Dump;
            r.spikeDumpPhaseStartPrice = Mathf.Max(0.000001f, r.rawPrice);
            r.spikeDumpTargetPrice = Mathf.Max(minPrice, r.spikeDumpPhaseStartPrice * (1f - dropPercent));
            r.spikeDumpPhaseDuration = UnityEngine.Random.Range(
                cfg.spikeDumpDumpDurationMinSeconds,
                cfg.spikeDumpDumpDurationMaxSeconds);
            r.spikeDumpPhaseTimeLeft = r.spikeDumpPhaseDuration;
        }

        private void StartSpikeDumpFirstShakePhase(CoinRuntime r)
        {
            var cfg = r.config;
            var minMoves = Mathf.Min(cfg.spikeDumpShakeMovesMin, cfg.spikeDumpShakeMovesMax);
            var maxMoves = Mathf.Max(cfg.spikeDumpShakeMovesMin, cfg.spikeDumpShakeMovesMax);
            r.spikeDumpShakeMovesLeft = UnityEngine.Random.Range(minMoves, maxMoves + 1);
            StartSpikeDumpShakePhase(r);
        }

        private void StartSpikeDumpShakePhase(CoinRuntime r)
        {
            var cfg = r.config;
            r.spikeDumpPhase = SpikeDumpPhase.Shake;
            r.spikeDumpPhaseStartPrice = Mathf.Max(0.000001f, r.rawPrice);

            var moveMin = Mathf.Min(cfg.spikeDumpShakeMovePercentMin, cfg.spikeDumpShakeMovePercentMax);
            var moveMax = Mathf.Max(cfg.spikeDumpShakeMovePercentMin, cfg.spikeDumpShakeMovePercentMax);
            var direction = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            var movePercent = UnityEngine.Random.Range(moveMin, moveMax);
            var minPrice = cfg.initialPrice * cfg.minCorridorLowFromInitial;

            r.spikeDumpTargetPrice = Mathf.Max(minPrice, r.spikeDumpPhaseStartPrice * (1f + direction * movePercent));
            r.spikeDumpPhaseDuration = UnityEngine.Random.Range(
                cfg.spikeDumpShakeDurationMinSeconds,
                cfg.spikeDumpShakeDurationMaxSeconds);
            r.spikeDumpPhaseTimeLeft = r.spikeDumpPhaseDuration;
            r.spikeDumpShakeMovesLeft--;
        }

        private static void ResetSpikeDump(CoinRuntime r)
        {
            r.spikeDumpPhase = SpikeDumpPhase.Grow;
            r.spikeDumpPhaseStartPrice = 0f;
            r.spikeDumpTargetPrice = 0f;
            r.spikeDumpPhaseDuration = 0f;
            r.spikeDumpPhaseTimeLeft = 0f;
            r.spikeDumpGrowPercent = 0f;
            r.spikeDumpShakeMovesLeft = 0;
            r.spikeDumpInitialized = false;
        }

        private float SimulateDipThenPump(CoinRuntime r, float dt, float noise, float range)
        {
            if (!r.dipPumpInitialized || r.dipPumpPhaseTimeLeft <= 0f)
                StartNextDipPumpPhase(r);

            var cfg = r.config;
            var elapsed = Mathf.Clamp(r.dipPumpPhaseDuration - r.dipPumpPhaseTimeLeft, 0f, r.dipPumpPhaseDuration);
            var progress = Mathf.Clamp01((elapsed + dt) / Mathf.Max(0.0001f, r.dipPumpPhaseDuration));
            var curve = r.dipPumpPhase == DipPumpPhase.Pump
                ? 1f - (1f - progress) * (1f - progress)
                : SmoothStep01(progress);
            var scheduledPrice = Mathf.Lerp(r.dipPumpPhaseStartPrice, r.dipPumpTargetPrice, curve);

            var phaseDistance = Mathf.Abs(r.dipPumpTargetPrice - r.dipPumpPhaseStartPrice);
            var maxCatchupDelta = phaseDistance
                / Mathf.Max(0.5f, r.dipPumpPhaseDuration)
                * dt
                * (r.dipPumpPhase == DipPumpPhase.Pump ? 3.0f : 1.8f);

            var guidedDelta = Mathf.Clamp(
                scheduledPrice - r.rawPrice,
                -maxCatchupDelta,
                maxCatchupDelta);

            var noiseScale = r.dipPumpPhase == DipPumpPhase.Pump ? 0.25f : 1f;
            var noiseDelta = noise * range * cfg.dipPumpNoiseStrength * noiseScale * dt;
            noiseDelta = Mathf.Clamp(noiseDelta, -maxCatchupDelta * 0.35f, maxCatchupDelta * 0.35f);

            var outsidePull = 0f;
            if (r.rawPrice > r.corridorHigh) outsidePull = (r.corridorHigh - r.rawPrice);
            else if (r.rawPrice < r.corridorLow) outsidePull = (r.corridorLow - r.rawPrice);

            var next = r.rawPrice + guidedDelta + noiseDelta + outsidePull * r.stateCorridorPullStrength;

            r.corridorAnchor = Mathf.Lerp(r.corridorAnchor, Mathf.Max(0.000001f, next), 0.025f);
            RebuildCorridor(r, next, force: false);

            r.dipPumpPhaseTimeLeft -= dt;
            return next;
        }

        private void StartNextDipPumpPhase(CoinRuntime r)
        {
            if (!r.dipPumpInitialized
                || (r.dipPumpPhase == DipPumpPhase.Shake && r.dipPumpShakeMovesLeft <= 0))
            {
                StartDipPumpDipPhase(r);
                return;
            }

            switch (r.dipPumpPhase)
            {
                case DipPumpPhase.Dip:
                    StartDipPumpPumpPhase(r);
                    break;
                case DipPumpPhase.Pump:
                    StartDipPumpFirstShakePhase(r);
                    break;
                case DipPumpPhase.Shake:
                    StartDipPumpShakePhase(r);
                    break;
            }
        }

        private void StartDipPumpDipPhase(CoinRuntime r)
        {
            var cfg = r.config;
            var dipMin = Mathf.Min(cfg.dipPumpDipPercentMin, cfg.dipPumpDipPercentMax);
            var dipMax = Mathf.Max(cfg.dipPumpDipPercentMin, cfg.dipPumpDipPercentMax);
            r.dipPumpDipPercent = UnityEngine.Random.Range(dipMin, dipMax);

            r.dipPumpPhase = DipPumpPhase.Dip;
            r.dipPumpPhaseStartPrice = Mathf.Max(0.000001f, r.rawPrice);
            var minPrice = cfg.initialPrice * cfg.minCorridorLowFromInitial;
            r.dipPumpTargetPrice = Mathf.Max(minPrice, r.dipPumpPhaseStartPrice * (1f - r.dipPumpDipPercent));
            r.dipPumpPhaseDuration = UnityEngine.Random.Range(
                cfg.dipPumpDipDurationMinSeconds,
                cfg.dipPumpDipDurationMaxSeconds);
            r.dipPumpPhaseTimeLeft = r.dipPumpPhaseDuration;
            r.dipPumpShakeMovesLeft = 0;
            r.dipPumpInitialized = true;
        }

        private void StartDipPumpPumpPhase(CoinRuntime r)
        {
            var cfg = r.config;
            var pumpMultiplier = UnityEngine.Random.Range(cfg.dipPumpPumpOverDipMin, cfg.dipPumpPumpOverDipMax);
            var pumpPercent = Mathf.Min(cfg.dipPumpMaxPumpPercent, r.dipPumpDipPercent * pumpMultiplier);

            r.dipPumpPhase = DipPumpPhase.Pump;
            r.dipPumpPhaseStartPrice = Mathf.Max(0.000001f, r.rawPrice);
            r.dipPumpTargetPrice = r.dipPumpPhaseStartPrice * (1f + pumpPercent);
            r.dipPumpPhaseDuration = UnityEngine.Random.Range(
                cfg.dipPumpPumpDurationMinSeconds,
                cfg.dipPumpPumpDurationMaxSeconds);
            r.dipPumpPhaseTimeLeft = r.dipPumpPhaseDuration;
        }

        private void StartDipPumpFirstShakePhase(CoinRuntime r)
        {
            var cfg = r.config;
            var minMoves = Mathf.Min(cfg.dipPumpShakeMovesMin, cfg.dipPumpShakeMovesMax);
            var maxMoves = Mathf.Max(cfg.dipPumpShakeMovesMin, cfg.dipPumpShakeMovesMax);
            r.dipPumpShakeMovesLeft = UnityEngine.Random.Range(minMoves, maxMoves + 1);
            StartDipPumpShakePhase(r);
        }

        private void StartDipPumpShakePhase(CoinRuntime r)
        {
            var cfg = r.config;
            r.dipPumpPhase = DipPumpPhase.Shake;
            r.dipPumpPhaseStartPrice = Mathf.Max(0.000001f, r.rawPrice);

            var moveMin = Mathf.Min(cfg.dipPumpShakeMovePercentMin, cfg.dipPumpShakeMovePercentMax);
            var moveMax = Mathf.Max(cfg.dipPumpShakeMovePercentMin, cfg.dipPumpShakeMovePercentMax);
            var direction = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            var movePercent = UnityEngine.Random.Range(moveMin, moveMax);
            var minPrice = cfg.initialPrice * cfg.minCorridorLowFromInitial;

            r.dipPumpTargetPrice = Mathf.Max(minPrice, r.dipPumpPhaseStartPrice * (1f + direction * movePercent));
            r.dipPumpPhaseDuration = UnityEngine.Random.Range(
                cfg.dipPumpShakeDurationMinSeconds,
                cfg.dipPumpShakeDurationMaxSeconds);
            r.dipPumpPhaseTimeLeft = r.dipPumpPhaseDuration;
            r.dipPumpShakeMovesLeft--;
        }

        private static void ResetDipPump(CoinRuntime r)
        {
            r.dipPumpPhase = DipPumpPhase.Dip;
            r.dipPumpPhaseStartPrice = 0f;
            r.dipPumpTargetPrice = 0f;
            r.dipPumpPhaseDuration = 0f;
            r.dipPumpPhaseTimeLeft = 0f;
            r.dipPumpDipPercent = 0f;
            r.dipPumpShakeMovesLeft = 0;
            r.dipPumpInitialized = false;
        }

        private float SimulateDeadFlatThenRocketPump(CoinRuntime r, float dt, float noise, float range)
        {
            if (!r.deadFlatRocketInitialized)
                StartDeadFlatRocket(r);

            var cfg = r.config;
            if (!r.deadFlatRocketPumpStarted)
            {
                var flatNoiseA = Mathf.PerlinNoise((r.seed * 0.0061f) + r.time * 0.18f, 0.17f) * 2f - 1f;
                var flatNoiseB = Mathf.PerlinNoise((r.seed * 0.0073f) + r.time * 0.55f, 0.41f) * 2f - 1f;
                var flatNoise = flatNoiseA * 0.75f + flatNoiseB * 0.25f;
                var flatTarget = r.deadFlatRocketBasePrice
                    * (1f + flatNoise * cfg.deadFlatRocketFlatRange01);
                var flatPull = (flatTarget - r.rawPrice) * 0.22f;
                var micro = noise * range * cfg.deadFlatRocketNoiseStrength * dt;
                var nextFlat = r.rawPrice + flatPull + micro;

                var minFlat = r.deadFlatRocketBasePrice * (1f - cfg.deadFlatRocketFlatRange01);
                var maxFlat = r.deadFlatRocketBasePrice * (1f + cfg.deadFlatRocketFlatRange01);
                nextFlat = Mathf.Clamp(nextFlat, minFlat, maxFlat);

                r.deadFlatRocketFlatTimeLeft -= dt;
                if (r.deadFlatRocketFlatTimeLeft <= 0f)
                    StartDeadFlatRocketPump(r);

                r.corridorAnchor = Mathf.Lerp(r.corridorAnchor, r.deadFlatRocketBasePrice, 0.015f);
                RebuildCorridor(r, nextFlat, force: false);
                return nextFlat;
            }

            var elapsed = Mathf.Clamp(r.deadFlatRocketPumpDuration - r.deadFlatRocketPumpTimeLeft, 0f, r.deadFlatRocketPumpDuration);
            var progress = Mathf.Clamp01((elapsed + dt) / Mathf.Max(0.0001f, r.deadFlatRocketPumpDuration));
            var curve = 1f - Mathf.Pow(1f - progress, 3f);
            var scheduledPrice = Mathf.Lerp(r.deadFlatRocketPumpStartPrice, r.deadFlatRocketTargetPrice, curve);

            var distance = Mathf.Abs(r.deadFlatRocketTargetPrice - r.deadFlatRocketPumpStartPrice);
            var maxCatchupDelta = distance / Mathf.Max(0.25f, r.deadFlatRocketPumpDuration) * dt * 4f;
            var guidedDelta = Mathf.Clamp(scheduledPrice - r.rawPrice, 0f, maxCatchupDelta);
            var next = r.rawPrice + guidedDelta;

            r.corridorAnchor = Mathf.Lerp(r.corridorAnchor, r.deadFlatRocketTargetPrice, 0.12f);
            RebuildCorridor(r, next, force: false);

            r.deadFlatRocketPumpTimeLeft -= dt;
            if (r.deadFlatRocketPumpTimeLeft <= 0f || next >= r.deadFlatRocketTargetPrice * 0.995f)
                r.stateTimeLeft = 0f;

            return next;
        }

        private void StartDeadFlatRocket(CoinRuntime r)
        {
            var cfg = r.config;
            r.deadFlatRocketBasePrice = Mathf.Max(0.000001f, r.rawPrice);
            r.deadFlatRocketFlatTimeLeft = UnityEngine.Random.Range(
                cfg.deadFlatRocketFlatDurationMinSeconds,
                cfg.deadFlatRocketFlatDurationMaxSeconds);
            r.deadFlatRocketPumpDuration = UnityEngine.Random.Range(
                cfg.deadFlatRocketPumpDurationMinSeconds,
                cfg.deadFlatRocketPumpDurationMaxSeconds);
            r.deadFlatRocketPumpStarted = false;
            r.deadFlatRocketInitialized = true;
        }

        private void StartDeadFlatRocketPump(CoinRuntime r)
        {
            var cfg = r.config;
            var minPump = Mathf.Min(cfg.deadFlatRocketPumpPercentMin, cfg.deadFlatRocketPumpPercentMax);
            var maxPump = Mathf.Max(cfg.deadFlatRocketPumpPercentMin, cfg.deadFlatRocketPumpPercentMax);
            var pumpPercent = UnityEngine.Random.Range(minPump, maxPump);

            r.deadFlatRocketPumpStartPrice = Mathf.Max(0.000001f, r.rawPrice);
            r.deadFlatRocketTargetPrice = r.deadFlatRocketPumpStartPrice * (1f + pumpPercent);
            r.deadFlatRocketPumpTimeLeft = r.deadFlatRocketPumpDuration;
            r.deadFlatRocketPumpStarted = true;

            if (ShouldLogActiveCurrency(r))
            {
                Debug.Log(
                    $"[DeadFlatThenRocketPump] {r.config.id}: rocket started. " +
                    $"pump={pumpPercent:P0}, target={r.deadFlatRocketTargetPrice:0.##}, duration={r.deadFlatRocketPumpDuration:0.0}s.",
                    this);
            }
        }

        private static void ResetDeadFlatRocket(CoinRuntime r)
        {
            r.deadFlatRocketBasePrice = 0f;
            r.deadFlatRocketPumpStartPrice = 0f;
            r.deadFlatRocketTargetPrice = 0f;
            r.deadFlatRocketFlatTimeLeft = 0f;
            r.deadFlatRocketPumpDuration = 0f;
            r.deadFlatRocketPumpTimeLeft = 0f;
            r.deadFlatRocketPumpStarted = false;
            r.deadFlatRocketInitialized = false;
        }

        private float SimulateDeadFlatThenRocketDump(CoinRuntime r, float dt, float noise, float range)
        {
            if (!r.deadFlatDumpInitialized)
                StartDeadFlatDump(r);

            var cfg = r.config;
            if (!r.deadFlatDumpStarted)
            {
                var flatNoiseA = Mathf.PerlinNoise((r.seed * 0.0067f) + r.time * 0.18f, 0.23f) * 2f - 1f;
                var flatNoiseB = Mathf.PerlinNoise((r.seed * 0.0079f) + r.time * 0.55f, 0.49f) * 2f - 1f;
                var flatNoise = flatNoiseA * 0.75f + flatNoiseB * 0.25f;
                var flatTarget = r.deadFlatDumpBasePrice
                    * (1f + flatNoise * cfg.deadFlatDumpFlatRange01);
                var flatPull = (flatTarget - r.rawPrice) * 0.22f;
                var micro = noise * range * cfg.deadFlatDumpNoiseStrength * dt;
                var nextFlat = r.rawPrice + flatPull + micro;

                var minFlat = r.deadFlatDumpBasePrice * (1f - cfg.deadFlatDumpFlatRange01);
                var maxFlat = r.deadFlatDumpBasePrice * (1f + cfg.deadFlatDumpFlatRange01);
                nextFlat = Mathf.Clamp(nextFlat, minFlat, maxFlat);

                r.deadFlatDumpFlatTimeLeft -= dt;
                if (r.deadFlatDumpFlatTimeLeft <= 0f)
                    StartDeadFlatRocketDump(r);

                r.corridorAnchor = Mathf.Lerp(r.corridorAnchor, r.deadFlatDumpBasePrice, 0.015f);
                RebuildCorridor(r, nextFlat, force: false);
                return nextFlat;
            }

            var elapsed = Mathf.Clamp(r.deadFlatDumpDuration - r.deadFlatDumpTimeLeft, 0f, r.deadFlatDumpDuration);
            var progress = Mathf.Clamp01((elapsed + dt) / Mathf.Max(0.0001f, r.deadFlatDumpDuration));
            var curve = 1f - Mathf.Pow(1f - progress, 3f);
            var scheduledPrice = Mathf.Lerp(r.deadFlatDumpStartPrice, r.deadFlatDumpTargetPrice, curve);

            var distance = Mathf.Abs(r.deadFlatDumpStartPrice - r.deadFlatDumpTargetPrice);
            var maxCatchupDelta = distance / Mathf.Max(0.25f, r.deadFlatDumpDuration) * dt * 4f;
            var guidedDelta = Mathf.Clamp(scheduledPrice - r.rawPrice, -maxCatchupDelta, 0f);
            var next = r.rawPrice + guidedDelta;

            r.corridorAnchor = Mathf.Lerp(r.corridorAnchor, r.deadFlatDumpTargetPrice, 0.12f);
            RebuildCorridor(r, next, force: false);

            r.deadFlatDumpTimeLeft -= dt;
            if (r.deadFlatDumpTimeLeft <= 0f || next <= r.deadFlatDumpTargetPrice * 1.005f)
                r.stateTimeLeft = 0f;

            return next;
        }

        private void StartDeadFlatDump(CoinRuntime r)
        {
            var cfg = r.config;
            r.deadFlatDumpBasePrice = Mathf.Max(0.000001f, r.rawPrice);
            r.deadFlatDumpFlatTimeLeft = UnityEngine.Random.Range(
                cfg.deadFlatDumpFlatDurationMinSeconds,
                cfg.deadFlatDumpFlatDurationMaxSeconds);
            r.deadFlatDumpDuration = UnityEngine.Random.Range(
                cfg.deadFlatDumpDurationMinSeconds,
                cfg.deadFlatDumpDurationMaxSeconds);
            r.deadFlatDumpStarted = false;
            r.deadFlatDumpInitialized = true;
        }

        private void StartDeadFlatRocketDump(CoinRuntime r)
        {
            var cfg = r.config;
            var minDrop = Mathf.Min(cfg.deadFlatDumpDropPercentMin, cfg.deadFlatDumpDropPercentMax);
            var maxDrop = Mathf.Max(cfg.deadFlatDumpDropPercentMin, cfg.deadFlatDumpDropPercentMax);
            var dropPercent = UnityEngine.Random.Range(minDrop, maxDrop);
            var minPrice = cfg.initialPrice * cfg.minCorridorLowFromInitial;

            r.deadFlatDumpStartPrice = Mathf.Max(0.000001f, r.rawPrice);
            r.deadFlatDumpTargetPrice = Mathf.Max(minPrice, r.deadFlatDumpStartPrice * (1f - dropPercent));
            r.deadFlatDumpTimeLeft = r.deadFlatDumpDuration;
            r.deadFlatDumpStarted = true;

            if (ShouldLogActiveCurrency(r))
            {
                Debug.Log(
                    $"[DeadFlatThenRocketDump] {r.config.id}: dump started. " +
                    $"drop={dropPercent:P0}, target={r.deadFlatDumpTargetPrice:0.##}, duration={r.deadFlatDumpDuration:0.0}s.",
                    this);
            }
        }

        private static void ResetDeadFlatDump(CoinRuntime r)
        {
            r.deadFlatDumpBasePrice = 0f;
            r.deadFlatDumpStartPrice = 0f;
            r.deadFlatDumpTargetPrice = 0f;
            r.deadFlatDumpFlatTimeLeft = 0f;
            r.deadFlatDumpDuration = 0f;
            r.deadFlatDumpTimeLeft = 0f;
            r.deadFlatDumpStarted = false;
            r.deadFlatDumpInitialized = false;
        }

        private void StartNewChopPhase(CoinRuntime r, int direction)
        {
            r.chopDirection = direction >= 0 ? 1 : -1;
            r.chopPhaseDuration = UnityEngine.Random.Range(
                r.config.chopPhaseDurationMinSeconds,
                r.config.chopPhaseDurationMaxSeconds);
            r.chopPhaseTimeLeft = r.chopPhaseDuration;
            r.chopFakeoutUsed = false;
            r.chopFakeoutTimeLeft = 0f;
            r.chopRecoveryTimeLeft = 0f;

            var targetPos = r.chopDirection > 0
                ? UnityEngine.Random.Range(0.62f, 0.88f)
                : UnityEngine.Random.Range(0.12f, 0.38f);

            r.chopTargetPrice = Mathf.Lerp(r.corridorLow, r.corridorHigh, targetPos);
            r.chopFakeoutStartTime = r.chopPhaseDuration > 10f && UnityEngine.Random.value <= r.config.chopFakeoutChancePerPhase
                ? UnityEngine.Random.Range(r.chopPhaseDuration * 0.35f, r.chopPhaseDuration * 0.70f)
                : float.PositiveInfinity;

            if (ShouldLogActiveCurrency(r))
            {
                Debug.Log(
                    $"[ChopInCorridor] {r.config.id}: new phase. " +
                    $"Direction={FormatChopDirection(r.chopDirection)}, " +
                    $"duration={r.chopPhaseDuration:0.0}s, " +
                    $"target={r.chopTargetPrice:0.##}, " +
                    $"targetPos={targetPos:0.00}, " +
                    $"current={r.rawPrice:0.##}, " +
                    $"corridor=[{r.corridorLow:0.##}..{r.corridorHigh:0.##}], " +
                    $"fakeout={(r.chopPhaseDuration > 10f ? $"at {r.chopFakeoutStartTime:0.0}s" : "none")}.",
                    this);
            }
        }

        private bool ShouldLogActiveCurrency(CoinRuntime r)
        {
            return logActiveCurrencyChopDebug
                && market != null
                && market.ActiveCurrency == r.config.id;
        }

        private static string FormatChopDirection(int direction)
        {
            return direction >= 0 ? "UP (above corridor center)" : "DOWN (below corridor center)";
        }

        private static float LimitPriceChangePerTick(CoinRuntime r, float desiredPrice)
        {
            var current = Mathf.Max(0f, r.rawPrice);
            if (current <= 0.000001f)
                return desiredPrice;

            var maxChange01 = GetMaxPriceChangePerTick01(r);
            if (maxChange01 <= 0f)
                return desiredPrice;

            var maxDelta = current * maxChange01;

            // If rounding step is larger than the percent limit, allow at least one visible step.
            var step = GetRoundingStep(r.config, current);
            if (step > 0f)
                maxDelta = Mathf.Max(maxDelta, step);

            return Mathf.MoveTowards(current, desiredPrice, maxDelta);
        }

        private static float GetMaxPriceChangePerTick01(CoinRuntime r)
        {
            var cfg = r.config;
            return r.currentState switch
            {
                MarketStateType.MarketCrash => cfg.crashMaxPriceChangePerTick01,
                MarketStateType.SlowUpThenPumpAndDump => cfg.dipPumpMaxPriceChangePerTick01,
                MarketStateType.SlowUpThenDump => cfg.spikeDumpMaxPriceChangePerTick01,
                MarketStateType.LongUptrend => cfg.longUptrendMaxPriceChangePerTick01,
                MarketStateType.LongDowntrend => cfg.longDowntrendMaxPriceChangePerTick01,
                MarketStateType.DeadFlatThenRocketPump => cfg.deadFlatRocketMaxPriceChangePerTick01,
                MarketStateType.DeadFlatThenRocketDump => cfg.deadFlatDumpMaxPriceChangePerTick01,
                MarketStateType.Calm => cfg.calmMaxPriceChangePerTick01,
                _ => cfg.normalMaxPriceChangePerTick01,
            };
        }

        private static float GetEffectiveTickInterval(CoinRuntime r)
        {
            var multiplier = GetTickSpeedMultiplier(r);
            return r.config.tickIntervalSeconds / Mathf.Max(0.01f, multiplier);
        }

        private static float GetTickSpeedMultiplier(CoinRuntime r)
        {
            var cfg = r.config;
            return r.currentState switch
            {
                MarketStateType.MarketCrash => cfg.crashTickSpeedMultiplier,
                MarketStateType.SlowUpThenPumpAndDump => cfg.pumpTickSpeedMultiplier,
                MarketStateType.SlowUpThenDump => cfg.pumpTickSpeedMultiplier,
                _ => cfg.normalTickSpeedMultiplier,
            };
        }

        [ContextMenu("Debug/Log Active Market States")]
        private void LogActiveMarketStatesFromContextMenu()
        {
            LogActiveMarketStates("Manual market states log");
        }

        [ContextMenu("Debug/Reset all saves (player, market, history, risky)")]
        private void Debug_ResetAllSaves()
        {
            TraidingIDLE.Saves.SaveStorage.DeleteKey("save.player.v1");
            TraidingIDLE.Saves.SaveStorage.DeleteKey("save.market.v1");
            TraidingIDLE.Saves.SaveStorage.DeleteKey("save.history.v1");
            TraidingIDLE.Saves.SaveStorage.DeleteKey("save.risky.v1");
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
                if (!IsDebugStateSelectionTarget(r))
                    continue;

                EnterState(r, debugOverrideStateType, debugStateDurationSeconds, changeCorridor: false);
                applied++;
            }

            Debug.Log(
                $"[{nameof(MarketSimulation)}] Applied debug state {debugOverrideStateType} to {applied} coin(s).",
                this);
        }

        private bool IsDebugStateOverrideTarget(CoinRuntime r)
        {
            return debugOverrideState && IsDebugStateSelectionTarget(r);
        }

        private bool IsDebugStateSelectionTarget(CoinRuntime r)
        {
            if (debugOverrideActiveCurrencyOnly && market != null)
                return market.ActiveCurrency == r.config.id;

            return r.config.id == debugOverrideCurrency;
        }

        private void LogActiveMarketStates(string reason)
        {
            if (_runtime.Count == 0)
            {
                Debug.Log($"[{nameof(MarketSimulation)}] {reason}: no runtime data yet.", this);
                return;
            }

            var message = $"[{nameof(MarketSimulation)}] {reason}\n";
            foreach (var kv in _runtime)
            {
                var r = kv.Value;
                var next = r.plan.Count > 0 ? r.plan.Peek().type.ToString() : "none";
                message += $"{r.config.id}: current={r.currentState}, timeLeft={r.stateTimeLeft:0}s, next={next}, price={r.visiblePrice:0.##}\n";
            }

            Debug.Log(message, this);
        }

        private static void RebuildCorridor(CoinRuntime r, float aroundPrice, bool force)
        {
            var cfg = r.config;

            // Volatility shrinks with price: interpolate width between "low price" and "high price".
            var t = Mathf.Clamp01(aroundPrice / Mathf.Max(0.000001f, cfg.highPriceReference));
            var baseWidth = Mathf.Lerp(cfg.corridorWidthAtLowPrice, cfg.corridorWidthAtHighPrice, t);

            var width = r.stateCorridorWidth > 1.01f
                ? r.stateCorridorWidth
                : baseWidth * (1f + UnityEngine.Random.Range(-cfg.corridorWidthRandomJitter, cfg.corridorWidthRandomJitter));
            width = Mathf.Max(1.01f, width);

            // Center corridor around anchor multiplicatively (symmetric in log-space).
            var half = Mathf.Sqrt(width);
            var anchor = Mathf.Max(0.000001f, r.corridorAnchor);

            var low = anchor / half;
            var high = anchor * half;

            // Ensure corridor covers current price (softly), unless we explicitly want it tight.
            if (!force)
            {
                if (aroundPrice < low) low = Mathf.Lerp(low, aroundPrice, 0.65f);
                if (aroundPrice > high) high = Mathf.Lerp(high, aroundPrice, 0.65f);
            }

            // Clamp minimum low relative to initial price.
            var minLow = cfg.initialPrice * cfg.minCorridorLowFromInitial;
            if (low < minLow)
            {
                var shift = minLow - low;
                low += shift;
                high += shift;
            }

            // Keep sane ordering.
            if (high <= low + 0.000001f)
                high = low + 0.000001f;

            r.corridorLow = low;
            r.corridorHigh = high;
        }

        private void ApplyNextPlannedState(CoinRuntime r)
        {
            EnsurePlannedStates(r);

            var next = r.plan.Dequeue();
            EnterState(r, next.type, next.durationSeconds, changeCorridor: true);
        }

        private void EnterState(CoinRuntime r, MarketStateType state, float durationSeconds, bool changeCorridor)
        {
            r.currentState = state;
            r.stateTimeLeft = Mathf.Max(1f, durationSeconds);

            if (r.currentState == MarketStateType.ChopInCorridor)
                ResetChopInCorridor(r);

            // Сбрасываем фазу «Спокойствие» при любом входе в состояние и при уходе с него (чистые поля).
            ResetCalm(r);
            ResetLongUptrend(r);
            ResetLongDowntrend(r);
            ResetSpikeDump(r);
            ResetDipPump(r);
            ResetDeadFlatRocket(r);
            ResetDeadFlatDump(r);
            ConfigureCorridorForState(r, r.currentState);

            // Legacy/random corridor jumps are only kept for old generic flat-like behavior.
            if (changeCorridor
                && r.currentState == MarketStateType.Flat
                && UnityEngine.Random.value <= corridorChangeChanceOnStateChange)
                ApplyCorridorLevelChange(r);

            // After crash ends (we just entered a new state that is not crash), roll next crash threshold again.
            if (r.currentState != MarketStateType.MarketCrash && r.crashArmed && r.crashThresholdX <= 0f)
                RollCrashThreshold(r);
        }

        private void ConfigureCorridorForState(CoinRuntime r, MarketStateType state)
        {
            var cfg = r.config;
            var width = GetStateCorridorWidth(cfg, state);
            var pull = GetStateCorridorPullStrength(cfg, state);

            r.stateCorridorWidth = Mathf.Max(1.01f, width);
            r.stateCorridorPullStrength = Mathf.Clamp01(pull);
            r.corridorAnchor = Mathf.Max(0.000001f, r.rawPrice);
            RebuildCorridor(r, r.rawPrice, force: true);
        }

        private static float GetStateCorridorWidth(CoinSimulationConfig cfg, MarketStateType state)
        {
            return state switch
            {
                MarketStateType.Calm => cfg.calmCorridorWidth,
                MarketStateType.ChopInCorridor => UnityEngine.Random.Range(
                    Mathf.Min(cfg.chopCorridorWidthMin, cfg.chopCorridorWidthMax),
                    Mathf.Max(cfg.chopCorridorWidthMin, cfg.chopCorridorWidthMax)),
                MarketStateType.LongUptrend => cfg.trendCorridorWidth,
                MarketStateType.LongDowntrend => cfg.trendCorridorWidth,
                MarketStateType.SlowUpThenDump => cfg.scenarioCorridorWidth,
                MarketStateType.SlowUpThenPumpAndDump => cfg.scenarioCorridorWidth,
                MarketStateType.DeadFlatThenRocketPump => cfg.deadFlatCorridorWidth,
                MarketStateType.DeadFlatThenRocketDump => cfg.deadFlatCorridorWidth,
                MarketStateType.MarketCrash => cfg.crashCorridorWidth,
                _ => Mathf.Lerp(cfg.corridorWidthAtLowPrice, cfg.corridorWidthAtHighPrice, 0.5f),
            };
        }

        private static float GetStateCorridorPullStrength(CoinSimulationConfig cfg, MarketStateType state)
        {
            return state switch
            {
                MarketStateType.Calm => cfg.calmCorridorPullStrength,
                MarketStateType.ChopInCorridor => cfg.chopCorridorPullStrength,
                MarketStateType.LongUptrend => cfg.trendCorridorPullStrength,
                MarketStateType.LongDowntrend => cfg.trendCorridorPullStrength,
                MarketStateType.SlowUpThenDump => cfg.scenarioCorridorPullStrength,
                MarketStateType.SlowUpThenPumpAndDump => cfg.scenarioCorridorPullStrength,
                MarketStateType.DeadFlatThenRocketPump => cfg.deadFlatCorridorPullStrength,
                MarketStateType.DeadFlatThenRocketDump => cfg.deadFlatCorridorPullStrength,
                MarketStateType.MarketCrash => cfg.crashCorridorPullStrength,
                _ => 0.35f,
            };
        }

        private void EnsurePlannedStates(CoinRuntime r)
        {
            var targetCount = Mathf.Max(1, r.config.plannedStatesCount);
            while (r.plan.Count < targetCount)
                r.plan.Enqueue(GeneratePlannedState(r));
        }

        private void BuildInitialPlan(CoinRuntime r)
        {
            r.plan.Clear();
            EnsurePlannedStates(r);
        }

        private static void ResetChopInCorridor(CoinRuntime r)
        {
            r.chopTargetPrice = 0f;
            r.chopPhaseDuration = 0f;
            r.chopPhaseTimeLeft = 0f;
            r.chopFakeoutUsed = false;
            r.chopFakeoutStartTime = float.PositiveInfinity;
            r.chopFakeoutTimeLeft = 0f;
            r.chopRecoveryTimeLeft = 0f;
        }

        private PlannedState GeneratePlannedState(CoinRuntime r)
        {
            var cfg = r.config;

            var stateType = PickWeightedPlannedState(cfg);
            var duration = PickStateDuration(cfg, stateType);

            return new PlannedState
            {
                type = stateType,
                durationSeconds = duration,
            };
        }

        private static float PickStateDuration(CoinSimulationConfig cfg, MarketStateType stateType)
        {
            if (stateType == MarketStateType.LongUptrend)
            {
                var min = Mathf.Min(cfg.longUptrendDurationMinSeconds, cfg.longUptrendDurationMaxSeconds);
                var max = Mathf.Max(cfg.longUptrendDurationMinSeconds, cfg.longUptrendDurationMaxSeconds);
                return Mathf.Max(1f, UnityEngine.Random.Range(min, max));
            }

            if (stateType == MarketStateType.LongDowntrend)
            {
                var min = Mathf.Min(cfg.longDowntrendDurationMinSeconds, cfg.longDowntrendDurationMaxSeconds);
                var max = Mathf.Max(cfg.longDowntrendDurationMinSeconds, cfg.longDowntrendDurationMaxSeconds);
                return Mathf.Max(1f, UnityEngine.Random.Range(min, max));
            }

            if (stateType == MarketStateType.DeadFlatThenRocketPump)
            {
                var flatMax = Mathf.Max(cfg.deadFlatRocketFlatDurationMinSeconds, cfg.deadFlatRocketFlatDurationMaxSeconds);
                var pumpMax = Mathf.Max(cfg.deadFlatRocketPumpDurationMinSeconds, cfg.deadFlatRocketPumpDurationMaxSeconds);
                return Mathf.Max(1f, flatMax + pumpMax + 1f);
            }

            if (stateType == MarketStateType.DeadFlatThenRocketDump)
            {
                var flatMax = Mathf.Max(cfg.deadFlatDumpFlatDurationMinSeconds, cfg.deadFlatDumpFlatDurationMaxSeconds);
                var dumpMax = Mathf.Max(cfg.deadFlatDumpDurationMinSeconds, cfg.deadFlatDumpDurationMaxSeconds);
                return Mathf.Max(1f, flatMax + dumpMax + 1f);
            }

            var duration = UnityEngine.Random.Range(cfg.stateDurationMinSeconds, cfg.stateDurationMaxSeconds);
            return Mathf.Max(1f, duration);
        }

        private static MarketStateType PickWeightedPlannedState(CoinSimulationConfig cfg)
        {
            var weights = cfg.plannedStateWeights;
            if (weights == null || weights.Length == 0)
                return PickFallbackPlannedState();

            var totalWeight = 0f;
            for (var i = 0; i < weights.Length; i++)
            {
                var entry = weights[i];
                if (entry == null)
                    continue;

                totalWeight += Mathf.Max(0f, entry.weight);
            }

            if (totalWeight <= 0f)
                return PickFallbackPlannedState();

            var roll = UnityEngine.Random.value * totalWeight;
            for (var i = 0; i < weights.Length; i++)
            {
                var entry = weights[i];
                if (entry == null)
                    continue;

                var weight = Mathf.Max(0f, entry.weight);
                if (weight <= 0f)
                    continue;

                roll -= weight;
                if (roll <= 0f)
                    return entry.type;
            }

            return weights[^1] != null ? weights[^1].type : PickFallbackPlannedState();
        }

        private static MarketStateType PickFallbackPlannedState()
        {
            return UnityEngine.Random.value < 0.5f
                ? MarketStateType.ChopInCorridor
                : MarketStateType.Calm;
        }

        private void ApplyCorridorLevelChange(CoinRuntime r)
        {
            var cfg = r.config;

            // Choose magnitude bucket
            var roll = UnityEngine.Random.value;
            float factor;

            if (roll < 0.60f)
            {
                var percent = UnityEngine.Random.Range(minorChangeMinPercent, minorChangeMaxPercent);
                factor = 1f + percent;
            }
            else if (roll < 0.90f)
            {
                var percent = UnityEngine.Random.Range(majorChangeMinPercent, majorChangeMaxPercent);
                factor = 1f + percent;
            }
            else
            {
                factor = UnityEngine.Random.Range(hugeChangeMinMultiplier, hugeChangeMaxMultiplier);
            }

            // Up or down
            var up = UnityEngine.Random.value >= 0.5f;
            if (!up)
                factor = 1f / Mathf.Max(0.000001f, factor);

            // Shift anchor, then rebuild corridor (keeps width consistent with "shrink as price rises").
            r.corridorAnchor = Mathf.Max(0.000001f, r.corridorAnchor * factor);
            RebuildCorridor(r, r.rawPrice, force: false);

            // Keep minimum low constraint.
            var minLow = cfg.initialPrice * cfg.minCorridorLowFromInitial;
            if (r.corridorLow < minLow)
            {
                var shift = minLow - r.corridorLow;
                r.corridorLow += shift;
                r.corridorHigh += shift;
                r.corridorAnchor += shift;
            }
        }

        private void ArmCrashIfNeeded(CoinRuntime r)
        {
            if (r.crashArmed)
                return;

            if (r.rawPrice >= r.config.crashFirstTriggerPrice)
            {
                r.crashArmed = true;
                RollCrashThreshold(r);
            }
        }

        private void RollCrashThreshold(CoinRuntime r)
        {
            var cfg = r.config;
            var min = Mathf.Min(cfg.crashThresholdRandomMin, cfg.crashThresholdRandomMax);
            var max = Mathf.Max(cfg.crashThresholdRandomMin, cfg.crashThresholdRandomMax);
            r.crashThresholdX = UnityEngine.Random.Range(min, max);
        }

        private void ForceCrashIfThresholdReached(CoinRuntime r)
        {
            if (!r.crashArmed)
                return;

            if (r.crashThresholdX <= 0f)
                return;

            if (r.corridorHigh <= r.crashThresholdX)
                return;

            // Force next planned state to be crash (so we can "know in advance").
            if (r.plan.Count == 0)
                EnsurePlannedStates(r);

            if (r.plan.Count > 0)
            {
                var first = r.plan.Dequeue();
                if (first.type != MarketStateType.MarketCrash)
                {
                    // Put crash as the next one, keep old "next" right after it.
                    var crash = new PlannedState
                    {
                        type = MarketStateType.MarketCrash,
                        durationSeconds = UnityEngine.Random.Range(r.config.stateDurationMinSeconds, r.config.stateDurationMaxSeconds),
                    };
                    r.plan.Enqueue(first); // will be after crash once we rebuild queue order

                    // Rebuild queue so crash is at front.
                    var rebuilt = new Queue<PlannedState>();
                    rebuilt.Enqueue(crash);
                    while (r.plan.Count > 0)
                        rebuilt.Enqueue(r.plan.Dequeue());
                    while (rebuilt.Count < Mathf.Max(1, r.config.plannedStatesCount))
                        rebuilt.Enqueue(GeneratePlannedState(r));
                    r.plan.Clear();
                    foreach (var s in rebuilt)
                        r.plan.Enqueue(s);
                }
                else
                {
                    // already crash next, keep it
                    r.plan.Enqueue(first);
                }
            }

            // Mark threshold as consumed; after crash we roll again.
            r.crashThresholdX = 0f;
        }

        private static float RoundPrice(CoinSimulationConfig cfg, float rawPrice)
        {
            var step = GetRoundingStep(cfg, rawPrice);
            if (step <= 0f)
                return rawPrice;

            return Mathf.Round(rawPrice / step) * step;
        }

        private static float GetRoundingStep(CoinSimulationConfig cfg, float price)
        {
            if (cfg.roundingRules == null || cfg.roundingRules.Length == 0)
                return 0f;

            var step = cfg.roundingRules[0].step;
            var bestMin = cfg.roundingRules[0].minPrice;

            for (var i = 0; i < cfg.roundingRules.Length; i++)
            {
                var r = cfg.roundingRules[i];
                if (price >= r.minPrice && r.minPrice >= bestMin)
                {
                    bestMin = r.minPrice;
                    step = r.step;
                }
            }

            return step;
        }

        private static void NormalizeConfig(CoinSimulationConfig cfg)
        {
            cfg.tickIntervalSeconds = Mathf.Max(0.05f, cfg.tickIntervalSeconds);
            cfg.normalTickSpeedMultiplier = Mathf.Max(0.01f, cfg.normalTickSpeedMultiplier);
            cfg.pumpTickSpeedMultiplier = Mathf.Max(0.01f, cfg.pumpTickSpeedMultiplier);
            cfg.crashTickSpeedMultiplier = Mathf.Max(0.01f, cfg.crashTickSpeedMultiplier);
            cfg.plannedStatesCount = Mathf.Max(1, cfg.plannedStatesCount);

            cfg.stateDurationMinSeconds = Mathf.Max(1f, cfg.stateDurationMinSeconds);
            cfg.stateDurationMaxSeconds = Mathf.Max(cfg.stateDurationMinSeconds, cfg.stateDurationMaxSeconds);
            NormalizeStateWeights(cfg);

            cfg.initialPrice = Mathf.Max(0.000001f, cfg.initialPrice);
            cfg.highPriceReference = Mathf.Max(cfg.initialPrice, cfg.highPriceReference);

            cfg.corridorWidthAtLowPrice = Mathf.Max(1.01f, cfg.corridorWidthAtLowPrice);
            cfg.corridorWidthAtHighPrice = Mathf.Max(1.01f, cfg.corridorWidthAtHighPrice);
            cfg.minCorridorLowFromInitial = Mathf.Max(0.01f, cfg.minCorridorLowFromInitial);

            cfg.calmCorridorWidth = Mathf.Max(1.01f, cfg.calmCorridorWidth);
            cfg.calmCorridorPullStrength = Mathf.Clamp01(cfg.calmCorridorPullStrength);
            cfg.chopCorridorWidthMin = Mathf.Max(1.01f, cfg.chopCorridorWidthMin);
            cfg.chopCorridorWidthMax = Mathf.Max(cfg.chopCorridorWidthMin, cfg.chopCorridorWidthMax);
            cfg.chopCorridorPullStrength = Mathf.Clamp01(cfg.chopCorridorPullStrength);
            cfg.trendCorridorWidth = Mathf.Max(1.01f, cfg.trendCorridorWidth);
            cfg.trendCorridorPullStrength = Mathf.Clamp01(cfg.trendCorridorPullStrength);
            cfg.scenarioCorridorWidth = Mathf.Max(1.01f, cfg.scenarioCorridorWidth);
            cfg.scenarioCorridorPullStrength = Mathf.Clamp01(cfg.scenarioCorridorPullStrength);
            cfg.deadFlatCorridorWidth = Mathf.Max(1.01f, cfg.deadFlatCorridorWidth);
            cfg.deadFlatCorridorPullStrength = Mathf.Clamp01(cfg.deadFlatCorridorPullStrength);
            cfg.crashCorridorWidth = Mathf.Max(1.01f, cfg.crashCorridorWidth);
            cfg.crashCorridorPullStrength = Mathf.Clamp01(cfg.crashCorridorPullStrength);

            cfg.chopPhaseDurationMinSeconds = Mathf.Max(1f, cfg.chopPhaseDurationMinSeconds);
            cfg.chopPhaseDurationMaxSeconds = Mathf.Max(cfg.chopPhaseDurationMinSeconds, cfg.chopPhaseDurationMaxSeconds);
            cfg.chopFakeoutChancePerPhase = Mathf.Clamp01(cfg.chopFakeoutChancePerPhase);
            cfg.chopFakeoutDurationMinSeconds = Mathf.Max(0.5f, cfg.chopFakeoutDurationMinSeconds);
            cfg.chopFakeoutDurationMaxSeconds = Mathf.Max(cfg.chopFakeoutDurationMinSeconds, cfg.chopFakeoutDurationMaxSeconds);
            cfg.chopFakeoutStrength = Mathf.Clamp01(cfg.chopFakeoutStrength);
            cfg.chopRecoveryDurationSeconds = Mathf.Max(0.5f, cfg.chopRecoveryDurationSeconds);
            cfg.chopRecoveryBoost = Mathf.Clamp(cfg.chopRecoveryBoost, 1f, 4f);

            cfg.calmOffsetFromHalfMin01 = Mathf.Clamp(cfg.calmOffsetFromHalfMin01, 0f, 0.49f);
            cfg.calmOffsetFromHalfMax01 = Mathf.Clamp(cfg.calmOffsetFromHalfMax01, 0f, 0.49f);
            if (cfg.calmOffsetFromHalfMax01 < cfg.calmOffsetFromHalfMin01)
                cfg.calmOffsetFromHalfMax01 = cfg.calmOffsetFromHalfMin01;

            cfg.calmLegDurationMinSeconds = Mathf.Max(0.5f, cfg.calmLegDurationMinSeconds);
            cfg.calmLegDurationMaxSeconds = Mathf.Max(cfg.calmLegDurationMinSeconds, cfg.calmLegDurationMaxSeconds);
            cfg.calmShortLegMaxSeconds = Mathf.Max(0.5f, cfg.calmShortLegMaxSeconds);
            cfg.calmSmallMoveRelativeThreshold = Mathf.Clamp01(cfg.calmSmallMoveRelativeThreshold);
            cfg.calmMaxPriceChangePerTick01 = Mathf.Clamp01(cfg.calmMaxPriceChangePerTick01);
            cfg.calmNoiseStrength = Mathf.Max(0f, cfg.calmNoiseStrength);
            cfg.calmApproachStrength = Mathf.Clamp(cfg.calmApproachStrength, 0.05f, 0.6f);

            cfg.longUptrendDurationMinSeconds = Mathf.Max(1f, cfg.longUptrendDurationMinSeconds);
            cfg.longUptrendDurationMaxSeconds = Mathf.Max(cfg.longUptrendDurationMinSeconds, cfg.longUptrendDurationMaxSeconds);
            cfg.longUptrendTargetMultiplierMin = Mathf.Max(1.01f, cfg.longUptrendTargetMultiplierMin);
            cfg.longUptrendTargetMultiplierMax = Mathf.Max(cfg.longUptrendTargetMultiplierMin, cfg.longUptrendTargetMultiplierMax);
            cfg.longUptrendSoftCapAtHighReference01 = Mathf.Clamp(cfg.longUptrendSoftCapAtHighReference01, 0.01f, 1f);
            cfg.longUptrendMinBoostAfterSoftCap01 = Mathf.Clamp01(cfg.longUptrendMinBoostAfterSoftCap01);
            cfg.longUptrendMaxEffectiveMultiplier = Mathf.Max(1.01f, cfg.longUptrendMaxEffectiveMultiplier);
            cfg.longUptrendTargetCorridorPos = Mathf.Clamp(cfg.longUptrendTargetCorridorPos, 0.45f, 0.90f);
            cfg.longUptrendApproachStrength = Mathf.Clamp(cfg.longUptrendApproachStrength, 0.05f, 0.8f);
            cfg.longUptrendNoiseStrength = Mathf.Max(0f, cfg.longUptrendNoiseStrength);
            cfg.longUptrendWobbleStrength = Mathf.Max(0f, cfg.longUptrendWobbleStrength);
            cfg.longUptrendDownWobbleChance = Mathf.Clamp01(cfg.longUptrendDownWobbleChance);
            cfg.longUptrendDownWobbleMultiplier = Mathf.Max(0.1f, cfg.longUptrendDownWobbleMultiplier);
            cfg.longUptrendUpWobbleMultiplier = Mathf.Max(0.1f, cfg.longUptrendUpWobbleMultiplier);
            cfg.longUptrendPullbackChancePerTick = Mathf.Clamp01(cfg.longUptrendPullbackChancePerTick);
            cfg.longUptrendPullbackStrength = Mathf.Clamp01(cfg.longUptrendPullbackStrength);
            cfg.longUptrendPullbackDurationMinSeconds = Mathf.Max(0.5f, cfg.longUptrendPullbackDurationMinSeconds);
            cfg.longUptrendPullbackDurationMaxSeconds = Mathf.Max(cfg.longUptrendPullbackDurationMinSeconds, cfg.longUptrendPullbackDurationMaxSeconds);
            cfg.longUptrendPullbackCooldownSeconds = Mathf.Max(0f, cfg.longUptrendPullbackCooldownSeconds);
            cfg.longUptrendRecoveryDurationSeconds = Mathf.Max(0.5f, cfg.longUptrendRecoveryDurationSeconds);
            cfg.longUptrendRecoveryCatchupMultiplier = Mathf.Clamp(cfg.longUptrendRecoveryCatchupMultiplier, 1f, 4f);
            cfg.longUptrendMaxPriceChangePerTick01 = Mathf.Clamp01(cfg.longUptrendMaxPriceChangePerTick01);

            cfg.longDowntrendDurationMinSeconds = Mathf.Max(1f, cfg.longDowntrendDurationMinSeconds);
            cfg.longDowntrendDurationMaxSeconds = Mathf.Max(cfg.longDowntrendDurationMinSeconds, cfg.longDowntrendDurationMaxSeconds);
            cfg.longDowntrendTargetDividerMin = Mathf.Max(1.01f, cfg.longDowntrendTargetDividerMin);
            cfg.longDowntrendTargetDividerMax = Mathf.Max(cfg.longDowntrendTargetDividerMin, cfg.longDowntrendTargetDividerMax);
            cfg.longDowntrendTargetCorridorPos = Mathf.Clamp(cfg.longDowntrendTargetCorridorPos, 0.10f, 0.55f);
            cfg.longDowntrendApproachStrength = Mathf.Clamp(cfg.longDowntrendApproachStrength, 0.05f, 0.8f);
            cfg.longDowntrendNoiseStrength = Mathf.Max(0f, cfg.longDowntrendNoiseStrength);
            cfg.longDowntrendRallyChancePerTick = Mathf.Clamp01(cfg.longDowntrendRallyChancePerTick);
            cfg.longDowntrendRallyStrength = Mathf.Clamp01(cfg.longDowntrendRallyStrength);
            cfg.longDowntrendRallyDurationMinSeconds = Mathf.Max(0.5f, cfg.longDowntrendRallyDurationMinSeconds);
            cfg.longDowntrendRallyDurationMaxSeconds = Mathf.Max(cfg.longDowntrendRallyDurationMinSeconds, cfg.longDowntrendRallyDurationMaxSeconds);
            cfg.longDowntrendRallyCooldownSeconds = Mathf.Max(0f, cfg.longDowntrendRallyCooldownSeconds);
            cfg.longDowntrendRecoveryDurationSeconds = Mathf.Max(0.5f, cfg.longDowntrendRecoveryDurationSeconds);
            cfg.longDowntrendRecoveryCatchupMultiplier = Mathf.Clamp(cfg.longDowntrendRecoveryCatchupMultiplier, 1f, 4f);
            cfg.longDowntrendMaxPriceChangePerTick01 = Mathf.Clamp01(cfg.longDowntrendMaxPriceChangePerTick01);

            cfg.spikeDumpGrowPercentMin = Mathf.Max(0.01f, cfg.spikeDumpGrowPercentMin);
            cfg.spikeDumpGrowPercentMax = Mathf.Max(cfg.spikeDumpGrowPercentMin, cfg.spikeDumpGrowPercentMax);
            cfg.spikeDumpGrowDurationMinSeconds = Mathf.Max(1f, cfg.spikeDumpGrowDurationMinSeconds);
            cfg.spikeDumpGrowDurationMaxSeconds = Mathf.Max(cfg.spikeDumpGrowDurationMinSeconds, cfg.spikeDumpGrowDurationMaxSeconds);
            cfg.spikeDumpDropOverGrowMin = Mathf.Max(1f, cfg.spikeDumpDropOverGrowMin);
            cfg.spikeDumpDropOverGrowMax = Mathf.Max(cfg.spikeDumpDropOverGrowMin, cfg.spikeDumpDropOverGrowMax);
            cfg.spikeDumpMaxDropPercent = Mathf.Clamp(cfg.spikeDumpMaxDropPercent, 0.01f, 0.95f);
            cfg.spikeDumpDumpDurationMinSeconds = Mathf.Max(0.25f, cfg.spikeDumpDumpDurationMinSeconds);
            cfg.spikeDumpDumpDurationMaxSeconds = Mathf.Max(cfg.spikeDumpDumpDurationMinSeconds, cfg.spikeDumpDumpDurationMaxSeconds);
            cfg.spikeDumpShakeMovesMin = Mathf.Max(1, cfg.spikeDumpShakeMovesMin);
            cfg.spikeDumpShakeMovesMax = Mathf.Max(cfg.spikeDumpShakeMovesMin, cfg.spikeDumpShakeMovesMax);
            cfg.spikeDumpShakeDurationMinSeconds = Mathf.Max(0.5f, cfg.spikeDumpShakeDurationMinSeconds);
            cfg.spikeDumpShakeDurationMaxSeconds = Mathf.Max(cfg.spikeDumpShakeDurationMinSeconds, cfg.spikeDumpShakeDurationMaxSeconds);
            cfg.spikeDumpShakeMovePercentMin = Mathf.Max(0.01f, cfg.spikeDumpShakeMovePercentMin);
            cfg.spikeDumpShakeMovePercentMax = Mathf.Max(cfg.spikeDumpShakeMovePercentMin, cfg.spikeDumpShakeMovePercentMax);
            cfg.spikeDumpNoiseStrength = Mathf.Max(0f, cfg.spikeDumpNoiseStrength);
            cfg.spikeDumpMaxPriceChangePerTick01 = Mathf.Clamp01(cfg.spikeDumpMaxPriceChangePerTick01);

            cfg.dipPumpDipPercentMin = Mathf.Max(0.01f, cfg.dipPumpDipPercentMin);
            cfg.dipPumpDipPercentMax = Mathf.Max(cfg.dipPumpDipPercentMin, cfg.dipPumpDipPercentMax);
            cfg.dipPumpDipDurationMinSeconds = Mathf.Max(1f, cfg.dipPumpDipDurationMinSeconds);
            cfg.dipPumpDipDurationMaxSeconds = Mathf.Max(cfg.dipPumpDipDurationMinSeconds, cfg.dipPumpDipDurationMaxSeconds);
            cfg.dipPumpPumpOverDipMin = Mathf.Max(1f, cfg.dipPumpPumpOverDipMin);
            cfg.dipPumpPumpOverDipMax = Mathf.Max(cfg.dipPumpPumpOverDipMin, cfg.dipPumpPumpOverDipMax);
            cfg.dipPumpMaxPumpPercent = Mathf.Max(0.01f, cfg.dipPumpMaxPumpPercent);
            cfg.dipPumpPumpDurationMinSeconds = Mathf.Max(0.25f, cfg.dipPumpPumpDurationMinSeconds);
            cfg.dipPumpPumpDurationMaxSeconds = Mathf.Max(cfg.dipPumpPumpDurationMinSeconds, cfg.dipPumpPumpDurationMaxSeconds);
            cfg.dipPumpShakeMovesMin = Mathf.Max(1, cfg.dipPumpShakeMovesMin);
            cfg.dipPumpShakeMovesMax = Mathf.Max(cfg.dipPumpShakeMovesMin, cfg.dipPumpShakeMovesMax);
            cfg.dipPumpShakeDurationMinSeconds = Mathf.Max(0.5f, cfg.dipPumpShakeDurationMinSeconds);
            cfg.dipPumpShakeDurationMaxSeconds = Mathf.Max(cfg.dipPumpShakeDurationMinSeconds, cfg.dipPumpShakeDurationMaxSeconds);
            cfg.dipPumpShakeMovePercentMin = Mathf.Max(0.01f, cfg.dipPumpShakeMovePercentMin);
            cfg.dipPumpShakeMovePercentMax = Mathf.Max(cfg.dipPumpShakeMovePercentMin, cfg.dipPumpShakeMovePercentMax);
            cfg.dipPumpNoiseStrength = Mathf.Max(0f, cfg.dipPumpNoiseStrength);
            cfg.dipPumpMaxPriceChangePerTick01 = Mathf.Clamp01(cfg.dipPumpMaxPriceChangePerTick01);

            cfg.deadFlatRocketFlatDurationMinSeconds = Mathf.Max(1f, cfg.deadFlatRocketFlatDurationMinSeconds);
            cfg.deadFlatRocketFlatDurationMaxSeconds = Mathf.Max(cfg.deadFlatRocketFlatDurationMinSeconds, cfg.deadFlatRocketFlatDurationMaxSeconds);
            cfg.deadFlatRocketFlatRange01 = Mathf.Clamp(cfg.deadFlatRocketFlatRange01, 0f, 0.08f);
            cfg.deadFlatRocketNoiseStrength = Mathf.Max(0f, cfg.deadFlatRocketNoiseStrength);
            cfg.deadFlatRocketPumpPercentMin = Mathf.Max(0.01f, cfg.deadFlatRocketPumpPercentMin);
            cfg.deadFlatRocketPumpPercentMax = Mathf.Max(cfg.deadFlatRocketPumpPercentMin, cfg.deadFlatRocketPumpPercentMax);
            cfg.deadFlatRocketPumpDurationMinSeconds = Mathf.Max(0.25f, cfg.deadFlatRocketPumpDurationMinSeconds);
            cfg.deadFlatRocketPumpDurationMaxSeconds = Mathf.Max(cfg.deadFlatRocketPumpDurationMinSeconds, cfg.deadFlatRocketPumpDurationMaxSeconds);
            cfg.deadFlatRocketMaxPriceChangePerTick01 = Mathf.Clamp01(cfg.deadFlatRocketMaxPriceChangePerTick01);

            cfg.deadFlatDumpFlatDurationMinSeconds = Mathf.Max(1f, cfg.deadFlatDumpFlatDurationMinSeconds);
            cfg.deadFlatDumpFlatDurationMaxSeconds = Mathf.Max(cfg.deadFlatDumpFlatDurationMinSeconds, cfg.deadFlatDumpFlatDurationMaxSeconds);
            cfg.deadFlatDumpFlatRange01 = Mathf.Clamp(cfg.deadFlatDumpFlatRange01, 0f, 0.08f);
            cfg.deadFlatDumpNoiseStrength = Mathf.Max(0f, cfg.deadFlatDumpNoiseStrength);
            cfg.deadFlatDumpDropPercentMin = Mathf.Clamp(cfg.deadFlatDumpDropPercentMin, 0.01f, 0.95f);
            cfg.deadFlatDumpDropPercentMax = Mathf.Clamp(cfg.deadFlatDumpDropPercentMax, cfg.deadFlatDumpDropPercentMin, 0.95f);
            cfg.deadFlatDumpDurationMinSeconds = Mathf.Max(0.25f, cfg.deadFlatDumpDurationMinSeconds);
            cfg.deadFlatDumpDurationMaxSeconds = Mathf.Max(cfg.deadFlatDumpDurationMinSeconds, cfg.deadFlatDumpDurationMaxSeconds);
            cfg.deadFlatDumpMaxPriceChangePerTick01 = Mathf.Clamp01(cfg.deadFlatDumpMaxPriceChangePerTick01);

            cfg.normalMaxPriceChangePerTick01 = Mathf.Clamp01(cfg.normalMaxPriceChangePerTick01);
            cfg.pumpMaxPriceChangePerTick01 = Mathf.Clamp01(cfg.pumpMaxPriceChangePerTick01);
            cfg.crashMaxPriceChangePerTick01 = Mathf.Clamp01(cfg.crashMaxPriceChangePerTick01);

            cfg.crashThresholdRandomMin = Mathf.Max(0f, cfg.crashThresholdRandomMin);
            cfg.crashThresholdRandomMax = Mathf.Max(0f, cfg.crashThresholdRandomMax);
        }

        private static void NormalizeStateWeights(CoinSimulationConfig cfg)
        {
            if (cfg.plannedStateWeights == null || cfg.plannedStateWeights.Length == 0)
            {
                cfg.plannedStateWeights = new[]
                {
                    new MarketStateWeight { type = MarketStateType.Calm, weight = 1f },
                    new MarketStateWeight { type = MarketStateType.ChopInCorridor, weight = 1f },
                    new MarketStateWeight { type = MarketStateType.LongUptrend, weight = 0.35f },
                    new MarketStateWeight { type = MarketStateType.LongDowntrend, weight = 0.25f },
                    new MarketStateWeight { type = MarketStateType.SlowUpThenDump, weight = 0.25f },
                    new MarketStateWeight { type = MarketStateType.SlowUpThenPumpAndDump, weight = 0.25f },
                    new MarketStateWeight { type = MarketStateType.DeadFlatThenRocketPump, weight = 0.18f },
                    new MarketStateWeight { type = MarketStateType.DeadFlatThenRocketDump, weight = 0.18f },
                };
                return;
            }

            for (var i = 0; i < cfg.plannedStateWeights.Length; i++)
            {
                if (cfg.plannedStateWeights[i] == null)
                {
                    cfg.plannedStateWeights[i] = new MarketStateWeight();
                    continue;
                }

                cfg.plannedStateWeights[i].weight = Mathf.Max(0f, cfg.plannedStateWeights[i].weight);
            }

            if (!HasStateWeight(cfg, MarketStateType.LongUptrend))
            {
                cfg.plannedStateWeights = cfg.plannedStateWeights
                    .Concat(new[] { new MarketStateWeight { type = MarketStateType.LongUptrend, weight = 0.35f } })
                    .ToArray();
            }

            if (!HasStateWeight(cfg, MarketStateType.LongDowntrend))
            {
                cfg.plannedStateWeights = cfg.plannedStateWeights
                    .Concat(new[] { new MarketStateWeight { type = MarketStateType.LongDowntrend, weight = 0.25f } })
                    .ToArray();
            }

            if (!HasStateWeight(cfg, MarketStateType.SlowUpThenDump))
            {
                cfg.plannedStateWeights = cfg.plannedStateWeights
                    .Concat(new[] { new MarketStateWeight { type = MarketStateType.SlowUpThenDump, weight = 0.25f } })
                    .ToArray();
            }

            if (!HasStateWeight(cfg, MarketStateType.SlowUpThenPumpAndDump))
            {
                cfg.plannedStateWeights = cfg.plannedStateWeights
                    .Concat(new[] { new MarketStateWeight { type = MarketStateType.SlowUpThenPumpAndDump, weight = 0.25f } })
                    .ToArray();
            }

            if (!HasStateWeight(cfg, MarketStateType.DeadFlatThenRocketPump))
            {
                cfg.plannedStateWeights = cfg.plannedStateWeights
                    .Concat(new[] { new MarketStateWeight { type = MarketStateType.DeadFlatThenRocketPump, weight = 0.18f } })
                    .ToArray();
            }

            if (!HasStateWeight(cfg, MarketStateType.DeadFlatThenRocketDump))
            {
                cfg.plannedStateWeights = cfg.plannedStateWeights
                    .Concat(new[] { new MarketStateWeight { type = MarketStateType.DeadFlatThenRocketDump, weight = 0.18f } })
                    .ToArray();
            }
        }

        private static bool HasStateWeight(CoinSimulationConfig cfg, MarketStateType type)
        {
            for (var i = 0; i < cfg.plannedStateWeights.Length; i++)
            {
                var entry = cfg.plannedStateWeights[i];
                if (entry != null && entry.type == type)
                    return true;
            }

            return false;
        }

        /// <summary>Запланировать бизнес-навык, подменяющий следующее рыночное состояние (реализация будет добавлена позже).</summary>
        public void EnqueueBusinessSkillOverrideNextState(CurrencyId currency)
        {
            Debug.Log($"[{nameof(MarketSimulation)}] Бизнес-навык рынка для {currency} — точка входа зарезервирована.", this);
        }
    }
}

