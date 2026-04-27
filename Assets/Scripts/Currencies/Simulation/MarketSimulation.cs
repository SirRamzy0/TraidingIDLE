using System;
using System.Collections.Generic;
using UnityEngine;
using TraidingIDLE.Currencies;

namespace TraidingIDLE.Currencies.Simulation
{
    public sealed class MarketSimulation : MonoBehaviour
    {
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

            public int seed;
            public float time;
        }

        [Header("Target market")]
        [SerializeField] private CurrencyMarket market = null!;

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

        private readonly Dictionary<CurrencyId, CoinRuntime> _runtime = new();

        private void Awake()
        {
            if (market == null)
                market = GetComponent<CurrencyMarket>();
        }

        private void Start()
        {
            _runtime.Clear();

            for (var i = 0; i < coins.Length; i++)
            {
                var cfg = coins[i];
                if (cfg == null)
                    continue;

                NormalizeConfig(cfg);

                var r = new CoinRuntime
                {
                    config = cfg,
                    rawPrice = cfg.initialPrice,
                    visiblePrice = RoundPrice(cfg, cfg.initialPrice),
                    corridorAnchor = cfg.initialPrice,
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

        private static float SimulateNextRawPrice(CoinRuntime r, float dt)
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
                    desiredPos = 0.5f + noise * 0.15f;
                    impulse = 0.0f;
                    break;
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

        private PlannedState GeneratePlannedState(CoinRuntime r)
        {
            var cfg = r.config;

            var duration = UnityEngine.Random.Range(cfg.stateDurationMinSeconds, cfg.stateDurationMaxSeconds);
            duration = Mathf.Max(1f, duration);

            var type = PickWeightedState(cfg.id);
            return new PlannedState { type = type, durationSeconds = duration };
        }

        private static MarketStateType PickWeightedState(CurrencyId id)
        {
            // Simple first version: per-coin "character" via weights.
            // Later можно вынести веса в публичные настройки, если понадобится.
            float chop, flat, slowUpDump, slowUpPumpDump, up, down;
            switch (id)
            {
                case CurrencyId.SHT:
                    chop = 0.28f; flat = 0.10f; slowUpDump = 0.20f; slowUpPumpDump = 0.22f; up = 0.10f; down = 0.10f;
                    break;
                case CurrencyId.ETH:
                    chop = 0.22f; flat = 0.18f; slowUpDump = 0.18f; slowUpPumpDump = 0.14f; up = 0.16f; down = 0.12f;
                    break;
                case CurrencyId.BTC:
                    chop = 0.16f; flat = 0.22f; slowUpDump = 0.14f; slowUpPumpDump = 0.08f; up = 0.22f; down = 0.18f;
                    break;
                default:
                    chop = 0.2f; flat = 0.2f; slowUpDump = 0.2f; slowUpPumpDump = 0.1f; up = 0.15f; down = 0.15f;
                    break;
            }

            var r = UnityEngine.Random.value;
            var s = 0f;
            s += chop; if (r <= s) return MarketStateType.ChopInCorridor;
            s += flat; if (r <= s) return MarketStateType.Flat;
            s += slowUpDump; if (r <= s) return MarketStateType.SlowUpThenDump;
            s += slowUpPumpDump; if (r <= s) return MarketStateType.SlowUpThenPumpAndDump;
            s += up; if (r <= s) return MarketStateType.LongUptrend;
            return MarketStateType.LongDowntrend;
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

            cfg.normalMaxPriceChangePerTick01 = Mathf.Clamp01(cfg.normalMaxPriceChangePerTick01);
            cfg.pumpMaxPriceChangePerTick01 = Mathf.Clamp01(cfg.pumpMaxPriceChangePerTick01);
            cfg.crashMaxPriceChangePerTick01 = Mathf.Clamp01(cfg.crashMaxPriceChangePerTick01);

            cfg.crashThresholdRandomMin = Mathf.Max(0f, cfg.crashThresholdRandomMin);
            cfg.crashThresholdRandomMax = Mathf.Max(0f, cfg.crashThresholdRandomMax);
        }
    }
}

