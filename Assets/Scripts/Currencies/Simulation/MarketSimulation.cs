using System;
using System.Collections.Generic;
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
            public int chopFakeoutTicksLeft;
            public int chopReturnBoostTicksLeft;

            public float calmTargetPrice;
            public float calmLegDuration;
            public float calmLegTimeLeft;
            public bool calmLegInitialized;

            public int seed;
            public float time;

            /// <summary>Следующий запланированный тип между Chop и Calm (чередование, без длинных полос RNG).</summary>
            public bool planNextPreferChop;
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
                    planNextPreferChop = UnityEngine.Random.value < 0.5f,
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
            r.stateTimeLeft -= dt;
            if (r.stateTimeLeft <= 0f)
            {
                ApplyNextPlannedState(r);
                EnsurePlannedStates(r);

                if (logMarketStatesOnStateChange)
                    LogActiveMarketStates($"{r.config.id} changed market state");
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
                case MarketStateType.LongUptrend:
                    desiredPos = 0.75f;
                    impulse = 0.20f;
                    break;
                case MarketStateType.LongDowntrend:
                    desiredPos = 0.25f;
                    impulse = -0.20f;
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
                r.chopFakeoutTicksLeft = UnityEngine.Random.Range(2, 5);
                r.chopReturnBoostTicksLeft = UnityEngine.Random.Range(2, 4);

                if (ShouldLogActiveCurrency(r))
                {
                    Debug.Log(
                        $"[ChopInCorridor] {r.config.id}: fakeout started. " +
                        $"Main direction={FormatChopDirection(r.chopDirection)}, " +
                        $"fakeout ticks={r.chopFakeoutTicksLeft}, return boost ticks={r.chopReturnBoostTicksLeft}.",
                        this);
                }
            }

            var effectiveDirection = r.chopDirection;
            var fakeoutActive = r.chopFakeoutTicksLeft > 0;
            if (fakeoutActive)
            {
                effectiveDirection *= -1;
                r.chopFakeoutTicksLeft--;
            }

            var boost = 1f;
            if (!fakeoutActive && r.chopReturnBoostTicksLeft > 0)
            {
                boost = 1.65f;
                r.chopReturnBoostTicksLeft--;
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
                noisyDelta = effectiveDirection * range * r.config.trendStrength * 0.42f * dt;
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

        private void StartNewChopPhase(CoinRuntime r, int direction)
        {
            r.chopDirection = direction >= 0 ? 1 : -1;
            r.chopPhaseDuration = UnityEngine.Random.Range(
                r.config.chopPhaseDurationMinSeconds,
                r.config.chopPhaseDurationMaxSeconds);
            r.chopPhaseTimeLeft = r.chopPhaseDuration;
            r.chopFakeoutUsed = false;
            r.chopFakeoutTicksLeft = 0;
            r.chopReturnBoostTicksLeft = 0;

            var targetPos = r.chopDirection > 0
                ? UnityEngine.Random.Range(0.62f, 0.88f)
                : UnityEngine.Random.Range(0.12f, 0.38f);

            r.chopTargetPrice = Mathf.Lerp(r.corridorLow, r.corridorHigh, targetPos);
            r.chopFakeoutStartTime = r.chopPhaseDuration > 10f
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
            r.currentState = next.type;
            r.stateTimeLeft = next.durationSeconds;

            if (r.currentState == MarketStateType.ChopInCorridor)
                ResetChopInCorridor(r);

            // Сбрасываем фазу «Спокойствие» при любом входе в состояние и при уходе с него (чистые поля).
            ResetCalm(r);

            // On state change, sometimes change corridor level up/down (with your minor/major/huge rules).
            if (UnityEngine.Random.value <= corridorChangeChanceOnStateChange)
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
            r.chopFakeoutTicksLeft = 0;
            r.chopReturnBoostTicksLeft = 0;
        }

        private PlannedState GeneratePlannedState(CoinRuntime r)
        {
            var cfg = r.config;

            var duration = UnityEngine.Random.Range(cfg.stateDurationMinSeconds, cfg.stateDurationMaxSeconds);
            duration = Mathf.Max(1f, duration);

            var stateType = r.planNextPreferChop
                ? MarketStateType.ChopInCorridor
                : MarketStateType.Calm;
            r.planNextPreferChop = !r.planNextPreferChop;

            return new PlannedState
            {
                type = stateType,
                durationSeconds = duration,
            };
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

            cfg.initialPrice = Mathf.Max(0.000001f, cfg.initialPrice);
            cfg.highPriceReference = Mathf.Max(cfg.initialPrice, cfg.highPriceReference);

            cfg.corridorWidthAtLowPrice = Mathf.Max(1.01f, cfg.corridorWidthAtLowPrice);
            cfg.corridorWidthAtHighPrice = Mathf.Max(1.01f, cfg.corridorWidthAtHighPrice);
            cfg.minCorridorLowFromInitial = Mathf.Max(0.01f, cfg.minCorridorLowFromInitial);

            cfg.chopPhaseDurationMinSeconds = Mathf.Max(1f, cfg.chopPhaseDurationMinSeconds);
            cfg.chopPhaseDurationMaxSeconds = Mathf.Max(cfg.chopPhaseDurationMinSeconds, cfg.chopPhaseDurationMaxSeconds);

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

            cfg.normalMaxPriceChangePerTick01 = Mathf.Clamp01(cfg.normalMaxPriceChangePerTick01);
            cfg.pumpMaxPriceChangePerTick01 = Mathf.Clamp01(cfg.pumpMaxPriceChangePerTick01);
            cfg.crashMaxPriceChangePerTick01 = Mathf.Clamp01(cfg.crashMaxPriceChangePerTick01);

            cfg.crashThresholdRandomMin = Mathf.Max(0f, cfg.crashThresholdRandomMin);
            cfg.crashThresholdRandomMax = Mathf.Max(0f, cfg.crashThresholdRandomMax);
        }
    }
}

