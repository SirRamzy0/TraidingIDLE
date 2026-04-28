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

            public int seed;
            public float time;
        }

        [Header("Target market")]
        [SerializeField] private CurrencyMarket market = null!;
        [SerializeField] private bool useCurrencyMarketPricesOnStart = false;

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

        private void Awake()
        {
            if (market == null)
                market = GetComponent<CurrencyMarket>();
        }

        private void Start()
        {
            _runtime.Clear();

            if (applyGameStartSettings)
                ApplyGameStartSettings();

            for (var i = 0; i < coins.Length; i++)
            {
                var cfg = coins[i];
                if (cfg == null)
                    continue;

                NormalizeConfig(cfg);

                var startPrice = cfg.initialPrice;
                if (useCurrencyMarketPricesOnStart && market != null)
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

            if (logMarketStatesOnStart)
                LogActiveMarketStates("Market simulation started");
        }

        private void ApplyGameStartSettings()
        {
            if (market != null)
                market.SetActiveCurrency(startActiveCurrency);

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

                    if (logMarketStatesOnStateChange)
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

            if (writeToCurrencyMarket && market != null)
                market.SetPrice(r.config.id, r.visiblePrice);
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
                case MarketStateType.Flat:
                    desiredPos = Mathf.Clamp01(0.5f + noise * 0.05f);
                    impulse = -0.05f;
                    break;
                case MarketStateType.SlowUpThenDump:
                    // Spend most time crawling up; if near top, dump down.
                    desiredPos = pos > 0.85f ? 0.25f : 0.90f;
                    impulse = pos > 0.85f ? -1.2f : 0.25f;
                    break;
                case MarketStateType.SlowUpThenPumpAndDump:
                    // Crawl up, then short pump near top, then dump.
                    if (pos < 0.80f) { desiredPos = 0.88f; impulse = 0.30f; }
                    else if (pos < 0.93f) { desiredPos = 0.99f; impulse = 1.2f; }
                    else { desiredPos = 0.30f; impulse = -1.5f; }
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
            delta += outsidePull * 0.35f; // pull back if we leaked out

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

            var next = r.rawPrice + (guidedDelta + noisyDelta) * boost + outsidePull * 0.35f;

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

            var next = r.rawPrice + guidedDelta + noisyDelta + outsidePull * 0.35f;

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
            var targetProgress = SmoothStep01(nextProgress);
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

            UpdateLongUptrendPullbackPhase(r, dt, progress);

            var pullback = 0f;
            if (r.longUptrendPullbackTimeLeft > 0f)
            {
                // Suppress upward guidance during the scare phase so several red candles can happen in a row.
                guidedDelta = Mathf.Min(guidedDelta, maxCatchupDelta * 0.12f);
                pullback = -Mathf.Min(
                    range * cfg.longUptrendPullbackStrength * r.longUptrendPullbackPower * dt,
                    maxCatchupDelta * 1.35f);
            }

            var outsidePull = 0f;
            if (r.rawPrice > r.corridorHigh) outsidePull = (r.corridorHigh - r.rawPrice);
            else if (r.rawPrice < r.corridorLow) outsidePull = (r.corridorLow - r.rawPrice);

            var next = r.rawPrice + guidedDelta + smoothNoise + microNoise + pullback + outsidePull * 0.20f;

            // The uptrend owns its target anchor for the whole state. This prevents exponential corridor creep.
            r.corridorAnchor = Mathf.Lerp(r.corridorAnchor, r.longUptrendTargetAnchor, 0.035f);
            RebuildCorridor(r, next, force: false);

            r.longUptrendTimeLeft -= dt;
            if (r.longUptrendRecoveryTimeLeft > 0f)
                r.longUptrendRecoveryTimeLeft -= dt;

            return next;
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
                    r.longUptrendPullbackCooldown = cfg.longUptrendPullbackCooldownSeconds;
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
            r.longUptrendPullbackTimeLeft = UnityEngine.Random.Range(minDuration, maxDuration);
            r.longUptrendPullbackPower = UnityEngine.Random.Range(0.75f, 1.35f);
            r.longUptrendRecoveryTimeLeft = 0f;
        }

        private static float SmoothStep01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
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

            var next = r.rawPrice + guidedDelta + smoothNoise + microNoise + rally + outsidePull * 0.20f;

            r.corridorAnchor = Mathf.Lerp(r.corridorAnchor, r.longDowntrendTargetAnchor, 0.035f);
            RebuildCorridor(r, next, force: false);

            r.longDowntrendTimeLeft -= dt;
            if (r.longDowntrendRecoveryTimeLeft > 0f)
                r.longDowntrendRecoveryTimeLeft -= dt;

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
                MarketStateType.SlowUpThenPumpAndDump => cfg.pumpMaxPriceChangePerTick01,
                MarketStateType.SlowUpThenDump => cfg.pumpMaxPriceChangePerTick01,
                MarketStateType.LongUptrend => cfg.longUptrendMaxPriceChangePerTick01,
                MarketStateType.LongDowntrend => cfg.longDowntrendMaxPriceChangePerTick01,
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

            var jitter = 1f + UnityEngine.Random.Range(-cfg.corridorWidthRandomJitter, cfg.corridorWidthRandomJitter);
            var width = Mathf.Max(1.01f, baseWidth * jitter);

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

            // On state change, sometimes change corridor level up/down (with your minor/major/huge rules).
            if (changeCorridor
                && r.currentState != MarketStateType.LongUptrend
                && r.currentState != MarketStateType.LongDowntrend
                && UnityEngine.Random.value <= corridorChangeChanceOnStateChange)
                ApplyCorridorLevelChange(r);

            // After crash ends (we just entered a new state that is not crash), roll next crash threshold again.
            if (r.currentState != MarketStateType.MarketCrash && r.crashArmed && r.crashThresholdX <= 0f)
                RollCrashThreshold(r);
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
    }
}

