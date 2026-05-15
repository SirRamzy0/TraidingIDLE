using System;
using UnityEngine;
using TraidingIDLE.Currencies;

namespace TraidingIDLE.Currencies.Simulation
{
    [Serializable]
    public sealed class StateMovementSettings
    {
        [Tooltip("General bias: -1 = down, 0 = neutral, 1 = up")]
        [Range(-1f, 1f)] public float directionalBias;
        [Tooltip("How much noise is added to movement (0..1)")]
        [Range(0f, 1f)] public float noiseAmountPercent = 0.15f;
        [Tooltip("Smoothness of noise transitions (0..1). Higher = smoother noise")]
        [Range(0.01f, 1f)] public float noiseSmoothness = 0.15f;
        [Tooltip("Chance to reverse direction per tick (0..1)")]
        [Range(0f, 1f)] public float reverseMoveChance = 0.35f;
        [Tooltip("Max percent change per tick for this state")]
        [Range(0f, 1f)] public float maxTickChangePercent = 0.03f;
        [Tooltip("How strongly price is pulled to corridor center (0..1)")]
        [Range(0f, 1f)] public float centerAttraction = 0.25f;
        [Tooltip("How strongly price bounces off corridor edges (0..1)")]
        [Range(0f, 1f)] public float edgeAttraction = 0.45f;
    }

    [Serializable]
    public sealed class PatternSettings
    {
        [Min(0.5f)] public float minDurationSeconds = 3f;
        [Min(0.5f)] public float maxDurationSeconds = 12f;
        [Min(1)] public int minWaves = 3;
        [Min(1)] public int maxWaves = 7;
        [Range(0f, 1f)] public float minAmplitudePercentOfCorridor = 0.4f;
        [Range(0f, 1f)] public float maxAmplitudePercentOfCorridor = 0.9f;
        [Range(0f, 1f)] public float chanceToShiftCorridor = 0.3f;
        [Range(0f, 1f)] public float minCorridorShiftPercent = 0.1f;
        [Range(0f, 1f)] public float maxCorridorShiftPercent = 0.5f;
        [Range(0f, 1f)] public float noiseAmountPercent = 0.08f;
        [Range(0.01f, 1f)] public float noiseSmoothness = 0.3f;
        [Range(0f, 1f)] public float maxTickChangePercent = 0.05f;
    }

    [Serializable]
    public sealed class StateWeight
    {
        public MarketStateType type;
        [Min(0f)] public float weight = 1f;
    }

    [Serializable]
    public sealed class PatternWeight
    {
        public MarketStateType type;
        [Min(0f)] public float weight = 1f;
    }

    [Serializable]
    public sealed class CoinSimulationConfig
    {
        [Header("Identity")]
        public CurrencyId id = CurrencyId.SHT;

        [Header("Timing")]
        [Min(0.05f)]
        public float tickIntervalSeconds = 0.6f;

        [Min(1)]
        public int plannedStatesCount = 4;

        [Header("Start Price")]
        [Min(0.000001f)]
        public float initialPrice = 1f;

        [Header("Corridor Settings")]
        [Range(0.01f, 2f)]
        public float startCorridorWidthPercent = 0.25f;
        [Range(0.01f, 2f)]
        public float minCorridorWidthPercent = 0.12f;
        [Range(0.01f, 3f)]
        public float maxCorridorWidthPercent = 0.55f;
        [Range(0f, 1f)]
        public float corridorShiftUpMinPercent = 0.08f;
        [Range(0f, 1f)]
        public float corridorShiftUpMaxPercent = 0.35f;
        [Range(0f, 1f)]
        public float corridorShiftDownMinPercent = 0.08f;
        [Range(0f, 1f)]
        public float corridorShiftDownMaxPercent = 0.3f;
        [Range(0f, 1f)]
        public float priceReturnToCorridorStrength = 0.55f;
        [Range(0f, 1f)]
        public float borderBounceStrength = 0.45f;
        public bool allowSoftBreakOutsideCorridor = true;

        [Header("General Timing")]
        [Min(0.5f)]
        public float minStateDurationSeconds = 8f;
        [Min(0.5f)]
        public float maxStateDurationSeconds = 42f;
        [Min(0f)]
        public float minPatternCooldownSeconds = 5f;
        [Min(0f)]
        public float maxPatternCooldownSeconds = 25f;
        [Range(0f, 1f)]
        public float chanceToStartPatternAfterState = 0.25f;

        [Header("State Weights")]
        public StateWeight[] stateWeights = new StateWeight[0];

        [Header("Pattern Weights")]
        public PatternWeight[] patternWeights = new PatternWeight[0];

        [Header("Movement Settings")]
        public StateMovementSettings calmCorridor = new StateMovementSettings();
        public StateMovementSettings activeCorridor = new StateMovementSettings();
        public StateMovementSettings scalpingWindow = new StateMovementSettings() { maxTickChangePercent = 0.05f, noiseAmountPercent = 0.1f };
        public StateMovementSettings pressureUp = new StateMovementSettings() { directionalBias = 0.6f, maxTickChangePercent = 0.04f };
        public StateMovementSettings pressureDown = new StateMovementSettings() { directionalBias = -0.6f, maxTickChangePercent = 0.04f };
        public StateMovementSettings slowTrendUp = new StateMovementSettings() { directionalBias = 0.45f, maxTickChangePercent = 0.035f, noiseAmountPercent = 0.1f };
        public StateMovementSettings slowTrendDown = new StateMovementSettings() { directionalBias = -0.45f, maxTickChangePercent = 0.035f, noiseAmountPercent = 0.1f };
        public StateMovementSettings volatileChop = new StateMovementSettings() { maxTickChangePercent = 0.06f, noiseAmountPercent = 0.25f, reverseMoveChance = 0.55f };
        public StateMovementSettings accumulation = new StateMovementSettings() { directionalBias = 0.1f, maxTickChangePercent = 0.025f, noiseAmountPercent = 0.08f };
        public StateMovementSettings distribution = new StateMovementSettings() { directionalBias = -0.1f, maxTickChangePercent = 0.025f, noiseAmountPercent = 0.08f };

        [Header("Pattern Settings")]
        public PatternSettings bigSaw = new PatternSettings() { minWaves = 3, maxWaves = 7, minAmplitudePercentOfCorridor = 0.4f, maxAmplitudePercentOfCorridor = 0.9f };
        public PatternSettings staircaseShiftUp = new PatternSettings() { chanceToShiftCorridor = 0.85f, minCorridorShiftPercent = 0.08f, maxCorridorShiftPercent = 0.35f };
        public PatternSettings staircaseShiftDown = new PatternSettings() { chanceToShiftCorridor = 0.85f, minCorridorShiftPercent = 0.08f, maxCorridorShiftPercent = 0.3f };
        public PatternSettings falseBreakoutUp = new PatternSettings() { minDurationSeconds = 2f, maxDurationSeconds = 6f, chanceToShiftCorridor = 0f };
        public PatternSettings falseBreakoutDown = new PatternSettings() { minDurationSeconds = 2f, maxDurationSeconds = 6f, chanceToShiftCorridor = 0f };
        public PatternSettings pumpAndCorrection = new PatternSettings() { minDurationSeconds = 4f, maxDurationSeconds = 14f, chanceToShiftCorridor = 0.15f };
        public PatternSettings dumpAndRecovery = new PatternSettings() { minDurationSeconds = 4f, maxDurationSeconds = 14f, chanceToShiftCorridor = 0.15f };
        public PatternSettings compressionBreakoutNew = new PatternSettings() { minDurationSeconds = 5f, maxDurationSeconds = 18f, chanceToShiftCorridor = 0.5f, minCorridorShiftPercent = 0.05f, maxCorridorShiftPercent = 0.4f };

        [Header("Safety Settings")]
        [Min(0.000001f)]
        public float minPrice = 1f;
        [Min(1f)]
        public float maxPrice = 1000000000f;
        [Range(0f, 1f)]
        public float maxNormalTickChangePercent = 0.06f;
        [Range(0f, 1f)]
        public float maxEventTickChangePercent = 0.1f;

        [Header("Rounding (minPrice → step)")]
        public PriceRoundingRule[] roundingRules =
        {
            new PriceRoundingRule() { minPrice = 0f, step = 1f },
        };

        public void EnsureDefaults()
        {
            if (roundingRules == null || roundingRules.Length == 0)
            {
                roundingRules = new[]
                {
                    new PriceRoundingRule() { minPrice = 0f, step = 1f },
                };
            }

            if (stateWeights == null || stateWeights.Length == 0)
            {
                stateWeights = BuildDefaultStateWeights(id);
            }

            if (patternWeights == null || patternWeights.Length == 0)
            {
                patternWeights = BuildDefaultPatternWeights(id);
            }
        }

        private static StateWeight[] BuildDefaultStateWeights(CurrencyId id)
        {
            if (id == CurrencyId.SHT)
            {
                return new StateWeight[]
                {
                    new StateWeight() { type = MarketStateType.CalmCorridor, weight = 0.5f },
                    new StateWeight() { type = MarketStateType.ActiveCorridor, weight = 2.0f },
                    new StateWeight() { type = MarketStateType.ScalpingWindow, weight = 1.5f },
                    new StateWeight() { type = MarketStateType.PressureUp, weight = 1.2f },
                    new StateWeight() { type = MarketStateType.PressureDown, weight = 0.4f },
                    new StateWeight() { type = MarketStateType.SlowTrendUp, weight = 0.6f },
                    new StateWeight() { type = MarketStateType.SlowTrendDown, weight = 0.5f },
                    new StateWeight() { type = MarketStateType.VolatileChop, weight = 0.3f },
                    new StateWeight() { type = MarketStateType.Accumulation, weight = 1.0f },
                    new StateWeight() { type = MarketStateType.Distribution, weight = 0.5f },
                };
            }

            if (id == CurrencyId.ETH)
            {
                return new StateWeight[]
                {
                    new StateWeight() { type = MarketStateType.CalmCorridor, weight = 1.0f },
                    new StateWeight() { type = MarketStateType.ActiveCorridor, weight = 0.8f },
                    new StateWeight() { type = MarketStateType.ScalpingWindow, weight = 0.4f },
                    new StateWeight() { type = MarketStateType.PressureUp, weight = 0.9f },
                    new StateWeight() { type = MarketStateType.PressureDown, weight = 0.9f },
                    new StateWeight() { type = MarketStateType.SlowTrendUp, weight = 1.4f },
                    new StateWeight() { type = MarketStateType.SlowTrendDown, weight = 1.2f },
                    new StateWeight() { type = MarketStateType.VolatileChop, weight = 0.6f },
                    new StateWeight() { type = MarketStateType.Accumulation, weight = 1.3f },
                    new StateWeight() { type = MarketStateType.Distribution, weight = 1.1f },
                };
            }

            if (id == CurrencyId.BTC)
            {
                return new StateWeight[]
                {
                    new StateWeight() { type = MarketStateType.CalmCorridor, weight = 0.8f },
                    new StateWeight() { type = MarketStateType.ActiveCorridor, weight = 0.6f },
                    new StateWeight() { type = MarketStateType.ScalpingWindow, weight = 0.2f },
                    new StateWeight() { type = MarketStateType.PressureUp, weight = 0.8f },
                    new StateWeight() { type = MarketStateType.PressureDown, weight = 0.8f },
                    new StateWeight() { type = MarketStateType.SlowTrendUp, weight = 0.9f },
                    new StateWeight() { type = MarketStateType.SlowTrendDown, weight = 0.9f },
                    new StateWeight() { type = MarketStateType.VolatileChop, weight = 1.5f },
                    new StateWeight() { type = MarketStateType.Accumulation, weight = 0.9f },
                    new StateWeight() { type = MarketStateType.Distribution, weight = 0.9f },
                };
            }

            return new StateWeight[]
            {
                new StateWeight() { type = MarketStateType.CalmCorridor, weight = 1f },
                new StateWeight() { type = MarketStateType.ActiveCorridor, weight = 1f },
            };
        }

        private static PatternWeight[] BuildDefaultPatternWeights(CurrencyId id)
        {
            if (id == CurrencyId.SHT)
            {
                return new PatternWeight[]
                {
                    new PatternWeight() { type = MarketStateType.BigSaw, weight = 1.2f },
                    new PatternWeight() { type = MarketStateType.StaircaseShiftUp, weight = 1.5f },
                    new PatternWeight() { type = MarketStateType.StaircaseShiftDown, weight = 0.4f },
                    new PatternWeight() { type = MarketStateType.FalseBreakoutUp, weight = 0.8f },
                    new PatternWeight() { type = MarketStateType.FalseBreakoutDown, weight = 0.3f },
                    new PatternWeight() { type = MarketStateType.PumpAndCorrection, weight = 1.0f },
                    new PatternWeight() { type = MarketStateType.DumpAndRecovery, weight = 0.4f },
                    new PatternWeight() { type = MarketStateType.CompressionBreakoutNew, weight = 1.3f },
                };
            }

            if (id == CurrencyId.ETH)
            {
                return new PatternWeight[]
                {
                    new PatternWeight() { type = MarketStateType.BigSaw, weight = 0.9f },
                    new PatternWeight() { type = MarketStateType.StaircaseShiftUp, weight = 1.0f },
                    new PatternWeight() { type = MarketStateType.StaircaseShiftDown, weight = 1.0f },
                    new PatternWeight() { type = MarketStateType.FalseBreakoutUp, weight = 0.7f },
                    new PatternWeight() { type = MarketStateType.FalseBreakoutDown, weight = 0.7f },
                    new PatternWeight() { type = MarketStateType.PumpAndCorrection, weight = 0.8f },
                    new PatternWeight() { type = MarketStateType.DumpAndRecovery, weight = 0.8f },
                    new PatternWeight() { type = MarketStateType.CompressionBreakoutNew, weight = 1.2f },
                };
            }

            if (id == CurrencyId.BTC)
            {
                return new PatternWeight[]
                {
                    new PatternWeight() { type = MarketStateType.BigSaw, weight = 0.8f },
                    new PatternWeight() { type = MarketStateType.StaircaseShiftUp, weight = 0.8f },
                    new PatternWeight() { type = MarketStateType.StaircaseShiftDown, weight = 0.8f },
                    new PatternWeight() { type = MarketStateType.FalseBreakoutUp, weight = 1.3f },
                    new PatternWeight() { type = MarketStateType.FalseBreakoutDown, weight = 1.3f },
                    new PatternWeight() { type = MarketStateType.PumpAndCorrection, weight = 1.5f },
                    new PatternWeight() { type = MarketStateType.DumpAndRecovery, weight = 1.5f },
                    new PatternWeight() { type = MarketStateType.CompressionBreakoutNew, weight = 0.9f },
                };
            }

            return new PatternWeight[]
            {
                new PatternWeight() { type = MarketStateType.BigSaw, weight = 1f },
            };
        }
    }
}