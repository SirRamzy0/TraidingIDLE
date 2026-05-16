using System;
using System.Collections.Generic;
using UnityEngine;
using TraidingIDLE.Currencies;

namespace TraidingIDLE.Currencies.Simulation
{
    [Serializable]
    public sealed class StateMovementSettings
    {
        [Tooltip("General bias: -1 = down, 0 = neutral, 1 = up")]
        [Range(-1f, 1f)] public float directionalBias;

        [Tooltip("How much noise is added to movement.")]
        [Range(0f, 1f)] public float noiseAmountPercent = 0.15f;

        [Tooltip("Higher = smoother noise.")]
        [Range(0.01f, 1f)] public float noiseSmoothness = 0.15f;

        [Tooltip("Chance to reverse direction per tick.")]
        [Range(0f, 1f)] public float reverseMoveChance = 0.35f;

        [Tooltip("Max percent change per tick for this state.")]
        [Range(0f, 1f)] public float maxTickChangePercent = 0.03f;

        [Tooltip("How strongly price is pulled to corridor center.")]
        [Range(0f, 1f)] public float centerAttraction = 0.25f;

        [Tooltip("How strongly price bounces off corridor edges.")]
        [Range(0f, 1f)] public float edgeAttraction = 0.45f;
    }

    [Serializable]
    public sealed class PatternSettings
    {
        [Header("Timing")]
        [Min(0.5f)] public float minDurationSeconds = 3f;
        [Min(0.5f)] public float maxDurationSeconds = 12f;

        [Header("Waves")]
        [Min(1)] public int minWaves = 3;
        [Min(1)] public int maxWaves = 7;

        [Header("Amplitude")]
        [Range(0f, 1f)] public float minAmplitudePercentOfCorridor = 0.4f;
        [Range(0f, 1f)] public float maxAmplitudePercentOfCorridor = 0.9f;

        [Header("Corridor Shift")]
        [Range(0f, 1f)] public float chanceToShiftCorridor = 0.3f;
        [Range(0f, 1f)] public float minCorridorShiftPercent = 0.1f;
        [Range(0f, 1f)] public float maxCorridorShiftPercent = 0.5f;

        [Header("Noise")]
        [Range(0f, 1f)] public float noiseAmountPercent = 0.08f;
        [Range(0.01f, 1f)] public float noiseSmoothness = 0.3f;

        [Header("Safety")]
        [Range(0f, 1f)] public float maxTickChangePercent = 0.05f;

        [Header("Spike")]
        [Tooltip("Used by SpikeUpRevert and SpikeDownRevert.")]
        [Min(1f)] public float minSpikeMultiplier = 2f;

        [Tooltip("Used by SpikeUpRevert and SpikeDownRevert.")]
        [Min(1f)] public float maxSpikeMultiplier = 4f;

        [Tooltip("How long spike keeps the extreme price before returning.")]
        [Min(0.1f)] public float minHoldSeconds = 2f;

        [Tooltip("How long spike keeps the extreme price before returning.")]
        [Min(0.1f)] public float maxHoldSeconds = 5f;
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
        private const int CurrentBalancePresetVersion = 3;

        [HideInInspector] public int balancePresetVersion;

        [Header("Identity")]
        public CurrencyId id = CurrencyId.SHT;

        [Header("Timing")]
        [Min(0.05f)] public float tickIntervalSeconds = 0.6f;
        [Min(1)] public int plannedStatesCount = 4;

        [Header("Start Price")]
        [Min(0.000001f)] public float initialPrice = 1f;

        [Header("Corridor Settings")]
        [Range(0.01f, 2f)] public float startCorridorWidthPercent = 0.25f;
        [Range(0.01f, 2f)] public float minCorridorWidthPercent = 0.12f;
        [Range(0.01f, 3f)] public float maxCorridorWidthPercent = 0.55f;

        [Range(0f, 1f)] public float corridorShiftUpMinPercent = 0.08f;
        [Range(0f, 1f)] public float corridorShiftUpMaxPercent = 0.35f;
        [Range(0f, 1f)] public float corridorShiftDownMinPercent = 0.08f;
        [Range(0f, 1f)] public float corridorShiftDownMaxPercent = 0.3f;

        [Range(0f, 1f)] public float priceReturnToCorridorStrength = 0.55f;
        [Range(0f, 1f)] public float borderBounceStrength = 0.45f;
        public bool allowSoftBreakOutsideCorridor = true;

        [Header("Economic Corridor")]
        public bool useBusinessIncomeEconomicCorridor = true;

        [Tooltip("For SHT use 1000, even if player starts at 500. Extra cap should be monetization upside.")]
        [Min(1f)] public float economyBaseCoinCap = 1000f;

        [Tooltip("Minimum full-cap profit target when business income is still tiny.")]
        [Min(0f)] public float earlyFullCapProfitFloorRubles = 5000000f;

        [Tooltip("How many hours of business income a perfect full-cap trade may be worth early.")]
        [Min(0f)] public float targetFullCapProfitHoursEarly = 3f;

        [Tooltip("How many hours of business income a perfect full-cap trade may be worth later.")]
        [Min(0f)] public float targetFullCapProfitHoursLate = 0.8f;

        [Tooltip("Business income where market starts using late profit hours.")]
        [Min(1f)] public float businessIncomeForLateBalance = 50000000f;

        [Tooltip("Upper/lower ratio of the economic corridor.")]
        [Range(1.05f, 10f)] public float economicCorridorRatio = 3.5f;

        [Range(0.001f, 1f)] public float economicIncomeSmoothing = 0.08f;
        [Min(0.5f)] public float economicCorridorRecalcSeconds = 10f;

        [Tooltip("How strongly corridor shifts are pulled toward economic target.")]
        [Range(0f, 1f)] public float economicShiftInfluence = 0.45f;

        [Range(0f, 1f)] public float maxEconomicCenterGrowthPerRecalc = 0.20f;
        [Range(0f, 1f)] public float maxEconomicCenterDropPerRecalc = 0.12f;

        [Header("General Timing")]
        [Min(0.5f)] public float minStateDurationSeconds = 8f;
        [Min(0.5f)] public float maxStateDurationSeconds = 42f;
        [Min(0f)] public float minPatternCooldownSeconds = 5f;
        [Min(0f)] public float maxPatternCooldownSeconds = 25f;
        [Range(0f, 1f)] public float chanceToStartPatternAfterState = 0.25f;

        [Header("State Weights")]
        public StateWeight[] stateWeights = new StateWeight[0];

        [Header("Pattern Weights")]
        public PatternWeight[] patternWeights = new PatternWeight[0];

        [Header("Movement Settings")]
        public StateMovementSettings calmCorridor = new StateMovementSettings();

        public StateMovementSettings activeCorridor = new StateMovementSettings
        {
            maxTickChangePercent = 0.04f,
            noiseAmountPercent = 0.12f,
            reverseMoveChance = 0.28f,
            centerAttraction = 0.08f,
            edgeAttraction = 0.25f,
        };

        public StateMovementSettings scalpingWindow = new StateMovementSettings
        {
            maxTickChangePercent = 0.05f,
            noiseAmountPercent = 0.1f,
            reverseMoveChance = 0.25f,
            centerAttraction = 0.1f,
            edgeAttraction = 0.3f,
        };

        public StateMovementSettings pressureUp = new StateMovementSettings
        {
            directionalBias = 0.6f,
            maxTickChangePercent = 0.04f,
            noiseAmountPercent = 0.1f,
            reverseMoveChance = 0.22f,
            centerAttraction = 0.05f,
            edgeAttraction = 0.25f,
        };

        public StateMovementSettings pressureDown = new StateMovementSettings
        {
            directionalBias = -0.6f,
            maxTickChangePercent = 0.04f,
            noiseAmountPercent = 0.1f,
            reverseMoveChance = 0.22f,
            centerAttraction = 0.05f,
            edgeAttraction = 0.25f,
        };

        public StateMovementSettings slowTrendUp = new StateMovementSettings
        {
            directionalBias = 0.45f,
            maxTickChangePercent = 0.035f,
            noiseAmountPercent = 0.1f,
            reverseMoveChance = 0.25f,
            centerAttraction = 0.08f,
            edgeAttraction = 0.25f,
        };

        public StateMovementSettings slowTrendDown = new StateMovementSettings
        {
            directionalBias = -0.45f,
            maxTickChangePercent = 0.035f,
            noiseAmountPercent = 0.1f,
            reverseMoveChance = 0.25f,
            centerAttraction = 0.08f,
            edgeAttraction = 0.25f,
        };

        public StateMovementSettings volatileChop = new StateMovementSettings
        {
            maxTickChangePercent = 0.06f,
            noiseAmountPercent = 0.25f,
            reverseMoveChance = 0.55f,
            centerAttraction = 0.15f,
            edgeAttraction = 0.45f,
        };

        public StateMovementSettings accumulation = new StateMovementSettings
        {
            directionalBias = 0.1f,
            maxTickChangePercent = 0.025f,
            noiseAmountPercent = 0.08f,
            reverseMoveChance = 0.35f,
            centerAttraction = 0.22f,
            edgeAttraction = 0.35f,
        };

        public StateMovementSettings distribution = new StateMovementSettings
        {
            directionalBias = -0.1f,
            maxTickChangePercent = 0.025f,
            noiseAmountPercent = 0.08f,
            reverseMoveChance = 0.35f,
            centerAttraction = 0.22f,
            edgeAttraction = 0.35f,
        };

        [Header("Pattern Settings")]
        public PatternSettings bigSaw = new PatternSettings
        {
            minWaves = 3,
            maxWaves = 7,
            minAmplitudePercentOfCorridor = 0.4f,
            maxAmplitudePercentOfCorridor = 0.9f,
            chanceToShiftCorridor = 0f,
            maxTickChangePercent = 0.08f,
        };

        public PatternSettings staircaseShiftUp = new PatternSettings
        {
            minDurationSeconds = 4f,
            maxDurationSeconds = 12f,
            minWaves = 3,
            maxWaves = 6,
            chanceToShiftCorridor = 0.7f,
            minCorridorShiftPercent = 0.05f,
            maxCorridorShiftPercent = 0.22f,
            maxTickChangePercent = 0.075f,
        };

        public PatternSettings staircaseShiftDown = new PatternSettings
        {
            minDurationSeconds = 4f,
            maxDurationSeconds = 12f,
            minWaves = 3,
            maxWaves = 6,
            chanceToShiftCorridor = 0.6f,
            minCorridorShiftPercent = 0.04f,
            maxCorridorShiftPercent = 0.18f,
            maxTickChangePercent = 0.075f,
        };

        public PatternSettings falseBreakoutUp = new PatternSettings
        {
            minDurationSeconds = 2f,
            maxDurationSeconds = 6f,
            minWaves = 2,
            maxWaves = 2,
            chanceToShiftCorridor = 0f,
            minAmplitudePercentOfCorridor = 0.45f,
            maxAmplitudePercentOfCorridor = 0.8f,
            maxTickChangePercent = 0.07f,
        };

        public PatternSettings falseBreakoutDown = new PatternSettings
        {
            minDurationSeconds = 2f,
            maxDurationSeconds = 6f,
            minWaves = 2,
            maxWaves = 2,
            chanceToShiftCorridor = 0f,
            minAmplitudePercentOfCorridor = 0.45f,
            maxAmplitudePercentOfCorridor = 0.8f,
            maxTickChangePercent = 0.07f,
        };

        public PatternSettings pumpAndCorrection = new PatternSettings
        {
            minDurationSeconds = 4f,
            maxDurationSeconds = 14f,
            minWaves = 2,
            maxWaves = 4,
            chanceToShiftCorridor = 0.15f,
            minAmplitudePercentOfCorridor = 0.55f,
            maxAmplitudePercentOfCorridor = 1f,
            maxTickChangePercent = 0.09f,
        };

        public PatternSettings dumpAndRecovery = new PatternSettings
        {
            minDurationSeconds = 4f,
            maxDurationSeconds = 14f,
            minWaves = 2,
            maxWaves = 4,
            chanceToShiftCorridor = 0.15f,
            minAmplitudePercentOfCorridor = 0.55f,
            maxAmplitudePercentOfCorridor = 1f,
            maxTickChangePercent = 0.09f,
        };

        public PatternSettings compressionBreakoutNew = new PatternSettings
        {
            minDurationSeconds = 5f,
            maxDurationSeconds = 18f,
            minWaves = 4,
            maxWaves = 7,
            chanceToShiftCorridor = 0.45f,
            minCorridorShiftPercent = 0.04f,
            maxCorridorShiftPercent = 0.22f,
            minAmplitudePercentOfCorridor = 0.25f,
            maxAmplitudePercentOfCorridor = 0.85f,
            maxTickChangePercent = 0.08f,
        };

        public PatternSettings spikeUpRevert = new PatternSettings
        {
            minDurationSeconds = 3f,
            maxDurationSeconds = 8f,
            minWaves = 3,
            maxWaves = 3,
            chanceToShiftCorridor = 0f,
            noiseAmountPercent = 0.04f,
            maxTickChangePercent = 0.10f,
            minSpikeMultiplier = 2f,
            maxSpikeMultiplier = 4f,
            minHoldSeconds = 2f,
            maxHoldSeconds = 5f,
        };

        public PatternSettings spikeDownRevert = new PatternSettings
        {
            minDurationSeconds = 3f,
            maxDurationSeconds = 8f,
            minWaves = 3,
            maxWaves = 3,
            chanceToShiftCorridor = 0f,
            noiseAmountPercent = 0.04f,
            maxTickChangePercent = 0.10f,
            minSpikeMultiplier = 1.7f,
            maxSpikeMultiplier = 2.9f,
            minHoldSeconds = 2f,
            maxHoldSeconds = 5f,
        };

        [Header("Safety Settings")]
        [Min(0.000001f)] public float minPrice = 1f;
        [Min(1f)] public float maxPrice = 1000000000f;
        [Range(0f, 1f)] public float maxNormalTickChangePercent = 0.07f;
        [Range(0f, 1f)] public float maxEventTickChangePercent = 0.18f;

        [Header("Rounding (minPrice → step)")]
        public PriceRoundingRule[] roundingRules =
        {
            new PriceRoundingRule { minPrice = 0f, step = 1f },
        };

#if UNITY_EDITOR
        private void OnValidate()
        {
            EnsureDefaults();
        }
#endif

        public void ValidateForInspector()
        {
            EnsureDefaults();
        }

        public void EnsureDefaults()
        {
            EnsureNestedSettings();

            if (roundingRules == null || roundingRules.Length == 0)
            {
                roundingRules = new[]
                {
                    new PriceRoundingRule { minPrice = 0f, step = 1f },
                };
            }

            if (balancePresetVersion < CurrentBalancePresetVersion)
            {
                ApplyCoinRoleDefaults();
                balancePresetVersion = CurrentBalancePresetVersion;
            }

            stateWeights = MergeStateWeights(stateWeights, BuildDefaultStateWeights(id));
            patternWeights = MergePatternWeights(patternWeights, BuildDefaultPatternWeights(id));

            NormalizeRanges();
        }

        private void EnsureNestedSettings()
        {
            if (calmCorridor == null)
                calmCorridor = new StateMovementSettings();
            if (activeCorridor == null)
                activeCorridor = new StateMovementSettings();
            if (scalpingWindow == null)
                scalpingWindow = new StateMovementSettings();
            if (pressureUp == null)
                pressureUp = new StateMovementSettings();
            if (pressureDown == null)
                pressureDown = new StateMovementSettings();
            if (slowTrendUp == null)
                slowTrendUp = new StateMovementSettings();
            if (slowTrendDown == null)
                slowTrendDown = new StateMovementSettings();
            if (volatileChop == null)
                volatileChop = new StateMovementSettings();
            if (accumulation == null)
                accumulation = new StateMovementSettings();
            if (distribution == null)
                distribution = new StateMovementSettings();

            if (bigSaw == null)
                bigSaw = new PatternSettings();
            if (staircaseShiftUp == null)
                staircaseShiftUp = new PatternSettings();
            if (staircaseShiftDown == null)
                staircaseShiftDown = new PatternSettings();
            if (falseBreakoutUp == null)
                falseBreakoutUp = new PatternSettings();
            if (falseBreakoutDown == null)
                falseBreakoutDown = new PatternSettings();
            if (pumpAndCorrection == null)
                pumpAndCorrection = new PatternSettings();
            if (dumpAndRecovery == null)
                dumpAndRecovery = new PatternSettings();
            if (compressionBreakoutNew == null)
                compressionBreakoutNew = new PatternSettings();
            if (spikeUpRevert == null)
                spikeUpRevert = new PatternSettings();
            if (spikeDownRevert == null)
                spikeDownRevert = new PatternSettings();
        }

        private void ApplyCoinRoleDefaults()
        {
            if (id == CurrencyId.SHT)
            {
                ApplyShtDefaults();
                return;
            }

            if (id == CurrencyId.ETH)
            {
                ApplyEthDefaults();
                return;
            }

            if (id == CurrencyId.BTC)
            {
                ApplyBtcDefaults();
                return;
            }
        }

        private void ApplyShtDefaults()
        {
            tickIntervalSeconds = 0.6f;

            economyBaseCoinCap = 1000f;
            earlyFullCapProfitFloorRubles = 13000000f;
            targetFullCapProfitHoursEarly = 3.2f;
            targetFullCapProfitHoursLate = 0.85f;
            businessIncomeForLateBalance = 50000000f;
            economicCorridorRatio = 3.6f;
            economicIncomeSmoothing = 0.08f;
            economicCorridorRecalcSeconds = 8f;
            economicShiftInfluence = 0.45f;
            maxEconomicCenterGrowthPerRecalc = 0.22f;
            maxEconomicCenterDropPerRecalc = 0.12f;

            startCorridorWidthPercent = 1.05f;
            minCorridorWidthPercent = 0.65f;
            maxCorridorWidthPercent = 1.45f;

            minStateDurationSeconds = 6f;
            maxStateDurationSeconds = 24f;
            minPatternCooldownSeconds = 4f;
            maxPatternCooldownSeconds = 14f;
            chanceToStartPatternAfterState = 0.38f;

            maxNormalTickChangePercent = 0.085f;
            maxEventTickChangePercent = 0.24f;

            ConfigureMovement(calmCorridor, 0f, 0.20f, 0.22f, 0.42f, 0.045f, 0.12f, 0.30f);
            ConfigureMovement(activeCorridor, 0f, 0.24f, 0.24f, 0.38f, 0.065f, 0.05f, 0.20f);
            ConfigureMovement(scalpingWindow, 0f, 0.28f, 0.20f, 0.44f, 0.080f, 0.03f, 0.18f);
            ConfigureMovement(pressureUp, 0.70f, 0.22f, 0.22f, 0.30f, 0.070f, 0.02f, 0.16f);
            ConfigureMovement(pressureDown, -0.78f, 0.27f, 0.18f, 0.18f, 0.095f, 0.02f, 0.12f);
            ConfigureMovement(slowTrendUp, 0.45f, 0.18f, 0.25f, 0.34f, 0.050f, 0.06f, 0.20f);
            ConfigureMovement(slowTrendDown, -0.55f, 0.22f, 0.20f, 0.22f, 0.070f, 0.04f, 0.16f);
            ConfigureMovement(volatileChop, 0f, 0.38f, 0.13f, 0.65f, 0.095f, 0.10f, 0.30f);
            ConfigureMovement(accumulation, 0.16f, 0.18f, 0.25f, 0.38f, 0.045f, 0.14f, 0.24f);
            ConfigureMovement(distribution, -0.22f, 0.24f, 0.19f, 0.28f, 0.065f, 0.10f, 0.20f);

            ConfigurePattern(bigSaw, 3f, 8f, 3, 7, 0.45f, 1.0f, 0f, 0f, 0f, 0.16f, 0.20f, 0.16f);
            ConfigurePattern(staircaseShiftUp, 4f, 10f, 3, 6, 0.40f, 1.0f, 0.65f, 0.06f, 0.24f, 0.14f, 0.16f, 0.15f);
            ConfigurePattern(staircaseShiftDown, 3f, 8f, 3, 6, 0.50f, 1.0f, 0.52f, 0.05f, 0.20f, 0.18f, 0.22f, 0.13f);
            ConfigurePattern(falseBreakoutUp, 2f, 5f, 2, 2, 0.50f, 0.90f, 0f, 0f, 0f, 0.13f, 0.16f, 0.14f);
            ConfigurePattern(falseBreakoutDown, 2f, 5f, 2, 2, 0.55f, 1.0f, 0f, 0f, 0f, 0.16f, 0.20f, 0.12f);
            ConfigurePattern(pumpAndCorrection, 3f, 9f, 2, 4, 0.55f, 1.1f, 0.10f, 0.04f, 0.12f, 0.16f, 0.18f, 0.16f);
            ConfigurePattern(dumpAndRecovery, 3f, 8f, 2, 4, 0.60f, 1.15f, 0.08f, 0.04f, 0.12f, 0.20f, 0.22f, 0.13f);
            ConfigurePattern(compressionBreakoutNew, 4f, 12f, 4, 7, 0.25f, 0.95f, 0.38f, 0.05f, 0.22f, 0.15f, 0.18f, 0.14f);

            ConfigurePattern(spikeUpRevert, 2f, 5f, 3, 3, 0f, 0f, 0f, 0f, 0f, 0.22f, 0.14f, 0.18f);
            spikeUpRevert.minSpikeMultiplier = 2f;
            spikeUpRevert.maxSpikeMultiplier = 4f;
            spikeUpRevert.minHoldSeconds = 1.8f;
            spikeUpRevert.maxHoldSeconds = 4f;

            ConfigurePattern(spikeDownRevert, 2f, 5f, 3, 3, 0f, 0f, 0f, 0f, 0f, 0.24f, 0.16f, 0.14f);
            spikeDownRevert.minSpikeMultiplier = 1.6f;
            spikeDownRevert.maxSpikeMultiplier = 2.6f;
            spikeDownRevert.minHoldSeconds = 1.5f;
            spikeDownRevert.maxHoldSeconds = 3.5f;
        }

        private void ApplyEthDefaults()
        {
            tickIntervalSeconds = 0.75f;

            economyBaseCoinCap = 150f;
            earlyFullCapProfitFloorRubles = 18000000f;
            targetFullCapProfitHoursEarly = 2.2f;
            targetFullCapProfitHoursLate = 0.9f;
            businessIncomeForLateBalance = 150000000f;
            economicCorridorRatio = 2.35f;
            economicIncomeSmoothing = 0.06f;
            economicCorridorRecalcSeconds = 12f;
            economicShiftInfluence = 0.35f;
            maxEconomicCenterGrowthPerRecalc = 0.14f;
            maxEconomicCenterDropPerRecalc = 0.10f;

            startCorridorWidthPercent = 0.65f;
            minCorridorWidthPercent = 0.35f;
            maxCorridorWidthPercent = 0.95f;

            minStateDurationSeconds = 12f;
            maxStateDurationSeconds = 38f;
            minPatternCooldownSeconds = 12f;
            maxPatternCooldownSeconds = 32f;
            chanceToStartPatternAfterState = 0.24f;

            maxNormalTickChangePercent = 0.060f;
            maxEventTickChangePercent = 0.17f;

            ConfigureMovement(calmCorridor, 0f, 0.13f, 0.25f, 0.36f, 0.030f, 0.18f, 0.30f);
            ConfigureMovement(activeCorridor, 0f, 0.16f, 0.24f, 0.34f, 0.045f, 0.08f, 0.24f);
            ConfigureMovement(scalpingWindow, 0f, 0.18f, 0.22f, 0.36f, 0.055f, 0.06f, 0.22f);
            ConfigureMovement(pressureUp, 0.58f, 0.14f, 0.25f, 0.26f, 0.045f, 0.04f, 0.20f);
            ConfigureMovement(pressureDown, -0.62f, 0.18f, 0.22f, 0.22f, 0.060f, 0.04f, 0.18f);
            ConfigureMovement(slowTrendUp, 0.48f, 0.12f, 0.30f, 0.24f, 0.038f, 0.08f, 0.22f);
            ConfigureMovement(slowTrendDown, -0.52f, 0.16f, 0.26f, 0.22f, 0.050f, 0.08f, 0.20f);
            ConfigureMovement(volatileChop, 0f, 0.25f, 0.18f, 0.58f, 0.065f, 0.16f, 0.32f);
            ConfigureMovement(accumulation, 0.12f, 0.12f, 0.30f, 0.32f, 0.032f, 0.20f, 0.28f);
            ConfigureMovement(distribution, -0.16f, 0.16f, 0.26f, 0.28f, 0.045f, 0.16f, 0.26f);

            ConfigurePattern(bigSaw, 4f, 12f, 3, 6, 0.35f, 0.80f, 0f, 0f, 0f, 0.10f, 0.12f, 0.20f);
            ConfigurePattern(staircaseShiftUp, 6f, 16f, 3, 6, 0.35f, 0.85f, 0.50f, 0.04f, 0.16f, 0.095f, 0.11f, 0.20f);
            ConfigurePattern(staircaseShiftDown, 5f, 14f, 3, 6, 0.40f, 0.90f, 0.45f, 0.035f, 0.14f, 0.12f, 0.14f, 0.18f);
            ConfigurePattern(falseBreakoutUp, 3f, 7f, 2, 2, 0.40f, 0.75f, 0f, 0f, 0f, 0.10f, 0.10f, 0.18f);
            ConfigurePattern(falseBreakoutDown, 3f, 7f, 2, 2, 0.45f, 0.80f, 0f, 0f, 0f, 0.12f, 0.12f, 0.16f);
            ConfigurePattern(pumpAndCorrection, 5f, 14f, 2, 4, 0.45f, 0.85f, 0.10f, 0.03f, 0.10f, 0.12f, 0.12f, 0.18f);
            ConfigurePattern(dumpAndRecovery, 4f, 12f, 2, 4, 0.50f, 0.90f, 0.10f, 0.03f, 0.10f, 0.14f, 0.14f, 0.16f);
            ConfigurePattern(compressionBreakoutNew, 6f, 18f, 4, 7, 0.25f, 0.75f, 0.38f, 0.035f, 0.16f, 0.11f, 0.10f, 0.18f);

            ConfigurePattern(spikeUpRevert, 3f, 7f, 3, 3, 0f, 0f, 0f, 0f, 0f, 0.16f, 0.09f, 0.22f);
            spikeUpRevert.minSpikeMultiplier = 1.5f;
            spikeUpRevert.maxSpikeMultiplier = 2.5f;
            spikeUpRevert.minHoldSeconds = 2f;
            spikeUpRevert.maxHoldSeconds = 4.5f;

            ConfigurePattern(spikeDownRevert, 3f, 7f, 3, 3, 0f, 0f, 0f, 0f, 0f, 0.17f, 0.10f, 0.18f);
            spikeDownRevert.minSpikeMultiplier = 1.35f;
            spikeDownRevert.maxSpikeMultiplier = 2f;
            spikeDownRevert.minHoldSeconds = 2f;
            spikeDownRevert.maxHoldSeconds = 4f;
        }

        private void ApplyBtcDefaults()
        {
            tickIntervalSeconds = 0.8f;

            economyBaseCoinCap = 50f;
            earlyFullCapProfitFloorRubles = 35000000f;
            targetFullCapProfitHoursEarly = 2.5f;
            targetFullCapProfitHoursLate = 1.0f;
            businessIncomeForLateBalance = 500000000f;
            economicCorridorRatio = 3.0f;
            economicIncomeSmoothing = 0.05f;
            economicCorridorRecalcSeconds = 14f;
            economicShiftInfluence = 0.30f;
            maxEconomicCenterGrowthPerRecalc = 0.16f;
            maxEconomicCenterDropPerRecalc = 0.14f;

            startCorridorWidthPercent = 0.85f;
            minCorridorWidthPercent = 0.45f;
            maxCorridorWidthPercent = 1.20f;

            minStateDurationSeconds = 14f;
            maxStateDurationSeconds = 46f;
            minPatternCooldownSeconds = 14f;
            maxPatternCooldownSeconds = 40f;
            chanceToStartPatternAfterState = 0.22f;

            maxNormalTickChangePercent = 0.075f;
            maxEventTickChangePercent = 0.22f;

            ConfigureMovement(calmCorridor, 0f, 0.17f, 0.22f, 0.42f, 0.040f, 0.16f, 0.30f);
            ConfigureMovement(activeCorridor, 0f, 0.22f, 0.20f, 0.40f, 0.055f, 0.08f, 0.26f);
            ConfigureMovement(scalpingWindow, 0f, 0.25f, 0.18f, 0.48f, 0.070f, 0.06f, 0.24f);
            ConfigureMovement(pressureUp, 0.58f, 0.18f, 0.22f, 0.28f, 0.055f, 0.05f, 0.22f);
            ConfigureMovement(pressureDown, -0.68f, 0.24f, 0.18f, 0.22f, 0.078f, 0.04f, 0.18f);
            ConfigureMovement(slowTrendUp, 0.42f, 0.16f, 0.25f, 0.28f, 0.045f, 0.08f, 0.22f);
            ConfigureMovement(slowTrendDown, -0.48f, 0.20f, 0.22f, 0.24f, 0.060f, 0.08f, 0.20f);
            ConfigureMovement(volatileChop, 0f, 0.35f, 0.14f, 0.65f, 0.090f, 0.12f, 0.32f);
            ConfigureMovement(accumulation, 0.10f, 0.16f, 0.25f, 0.34f, 0.040f, 0.18f, 0.28f);
            ConfigureMovement(distribution, -0.14f, 0.22f, 0.20f, 0.30f, 0.055f, 0.16f, 0.26f);

            ConfigurePattern(bigSaw, 4f, 12f, 3, 7, 0.45f, 1.0f, 0f, 0f, 0f, 0.14f, 0.17f, 0.16f);
            ConfigurePattern(staircaseShiftUp, 6f, 16f, 3, 6, 0.40f, 0.95f, 0.45f, 0.04f, 0.18f, 0.12f, 0.14f, 0.16f);
            ConfigurePattern(staircaseShiftDown, 5f, 14f, 3, 6, 0.48f, 1.0f, 0.45f, 0.04f, 0.18f, 0.16f, 0.18f, 0.14f);
            ConfigurePattern(falseBreakoutUp, 3f, 7f, 2, 2, 0.55f, 0.95f, 0f, 0f, 0f, 0.15f, 0.14f, 0.14f);
            ConfigurePattern(falseBreakoutDown, 3f, 7f, 2, 2, 0.60f, 1.0f, 0f, 0f, 0f, 0.17f, 0.16f, 0.12f);
            ConfigurePattern(pumpAndCorrection, 4f, 12f, 2, 4, 0.55f, 1.1f, 0.12f, 0.04f, 0.14f, 0.18f, 0.16f, 0.14f);
            ConfigurePattern(dumpAndRecovery, 4f, 11f, 2, 4, 0.60f, 1.15f, 0.12f, 0.04f, 0.14f, 0.20f, 0.18f, 0.12f);
            ConfigurePattern(compressionBreakoutNew, 6f, 18f, 4, 7, 0.30f, 0.90f, 0.35f, 0.04f, 0.18f, 0.14f, 0.14f, 0.14f);

            ConfigurePattern(spikeUpRevert, 3f, 7f, 3, 3, 0f, 0f, 0f, 0f, 0f, 0.22f, 0.12f, 0.16f);
            spikeUpRevert.minSpikeMultiplier = 2f;
            spikeUpRevert.maxSpikeMultiplier = 4f;
            spikeUpRevert.minHoldSeconds = 1.8f;
            spikeUpRevert.maxHoldSeconds = 4f;

            ConfigurePattern(spikeDownRevert, 3f, 7f, 3, 3, 0f, 0f, 0f, 0f, 0f, 0.23f, 0.14f, 0.12f);
            spikeDownRevert.minSpikeMultiplier = 1.45f;
            spikeDownRevert.maxSpikeMultiplier = 2.85f;
            spikeDownRevert.minHoldSeconds = 1.5f;
            spikeDownRevert.maxHoldSeconds = 3.5f;
        }

        private static void ConfigureMovement(
            StateMovementSettings movement,
            float directionalBias,
            float noiseAmountPercent,
            float noiseSmoothness,
            float reverseMoveChance,
            float maxTickChangePercent,
            float centerAttraction,
            float edgeAttraction)
        {
            if (movement == null)
                return;

            movement.directionalBias = directionalBias;
            movement.noiseAmountPercent = noiseAmountPercent;
            movement.noiseSmoothness = noiseSmoothness;
            movement.reverseMoveChance = reverseMoveChance;
            movement.maxTickChangePercent = maxTickChangePercent;
            movement.centerAttraction = centerAttraction;
            movement.edgeAttraction = edgeAttraction;
        }

        private static void ConfigurePattern(
            PatternSettings pattern,
            float minDurationSeconds,
            float maxDurationSeconds,
            int minWaves,
            int maxWaves,
            float minAmplitudePercentOfCorridor,
            float maxAmplitudePercentOfCorridor,
            float chanceToShiftCorridor,
            float minCorridorShiftPercent,
            float maxCorridorShiftPercent,
            float maxTickChangePercent,
            float noiseAmountPercent,
            float noiseSmoothness)
        {
            if (pattern == null)
                return;

            pattern.minDurationSeconds = minDurationSeconds;
            pattern.maxDurationSeconds = maxDurationSeconds;
            pattern.minWaves = minWaves;
            pattern.maxWaves = maxWaves;
            pattern.minAmplitudePercentOfCorridor = minAmplitudePercentOfCorridor;
            pattern.maxAmplitudePercentOfCorridor = maxAmplitudePercentOfCorridor;
            pattern.chanceToShiftCorridor = chanceToShiftCorridor;
            pattern.minCorridorShiftPercent = minCorridorShiftPercent;
            pattern.maxCorridorShiftPercent = maxCorridorShiftPercent;
            pattern.maxTickChangePercent = maxTickChangePercent;
            pattern.noiseAmountPercent = noiseAmountPercent;
            pattern.noiseSmoothness = noiseSmoothness;
        }

        private void NormalizeRanges()
        {
            minCorridorWidthPercent = Mathf.Max(0.01f, minCorridorWidthPercent);
            maxCorridorWidthPercent = Mathf.Max(minCorridorWidthPercent, maxCorridorWidthPercent);
            startCorridorWidthPercent = Mathf.Clamp(startCorridorWidthPercent, minCorridorWidthPercent, maxCorridorWidthPercent);

            minStateDurationSeconds = Mathf.Max(0.5f, minStateDurationSeconds);
            maxStateDurationSeconds = Mathf.Max(minStateDurationSeconds, maxStateDurationSeconds);

            minPatternCooldownSeconds = Mathf.Max(0f, minPatternCooldownSeconds);
            maxPatternCooldownSeconds = Mathf.Max(minPatternCooldownSeconds, maxPatternCooldownSeconds);

            economicCorridorRatio = Mathf.Max(1.05f, economicCorridorRatio);
            economyBaseCoinCap = Mathf.Max(1f, economyBaseCoinCap);
            economicCorridorRecalcSeconds = Mathf.Max(0.5f, economicCorridorRecalcSeconds);

            minPrice = Mathf.Max(0.000001f, minPrice);
            maxPrice = Mathf.Max(minPrice + 1f, maxPrice);

            NormalizePattern(bigSaw);
            NormalizePattern(staircaseShiftUp);
            NormalizePattern(staircaseShiftDown);
            NormalizePattern(falseBreakoutUp);
            NormalizePattern(falseBreakoutDown);
            NormalizePattern(pumpAndCorrection);
            NormalizePattern(dumpAndRecovery);
            NormalizePattern(compressionBreakoutNew);
            NormalizePattern(spikeUpRevert);
            NormalizePattern(spikeDownRevert);
        }

        private static void NormalizePattern(PatternSettings pattern)
        {
            if (pattern == null)
                return;

            pattern.minDurationSeconds = Mathf.Max(0.5f, pattern.minDurationSeconds);
            pattern.maxDurationSeconds = Mathf.Max(pattern.minDurationSeconds, pattern.maxDurationSeconds);

            pattern.minWaves = Mathf.Max(1, pattern.minWaves);
            pattern.maxWaves = Mathf.Max(pattern.minWaves, pattern.maxWaves);

            pattern.maxAmplitudePercentOfCorridor = Mathf.Max(
                pattern.minAmplitudePercentOfCorridor,
                pattern.maxAmplitudePercentOfCorridor);

            pattern.maxCorridorShiftPercent = Mathf.Max(
                pattern.minCorridorShiftPercent,
                pattern.maxCorridorShiftPercent);

            pattern.maxTickChangePercent = Mathf.Max(0f, pattern.maxTickChangePercent);

            pattern.minSpikeMultiplier = Mathf.Max(1f, pattern.minSpikeMultiplier);
            pattern.maxSpikeMultiplier = Mathf.Max(pattern.minSpikeMultiplier, pattern.maxSpikeMultiplier);

            pattern.minHoldSeconds = Mathf.Max(0.1f, pattern.minHoldSeconds);
            pattern.maxHoldSeconds = Mathf.Max(pattern.minHoldSeconds, pattern.maxHoldSeconds);
        }

        private static StateWeight[] MergeStateWeights(StateWeight[] existing, StateWeight[] defaults)
        {
            if (defaults == null || defaults.Length == 0)
                return existing ?? new StateWeight[0];

            if (existing == null || existing.Length == 0)
                return defaults;

            var result = new List<StateWeight>(existing);
            for (var i = 0; i < defaults.Length; i++)
            {
                var item = defaults[i];
                if (item == null)
                    continue;

                if (!HasStateWeight(result, item.type))
                {
                    result.Add(new StateWeight
                    {
                        type = item.type,
                        weight = item.weight,
                    });
                }
            }

            return result.ToArray();
        }

        private static PatternWeight[] MergePatternWeights(PatternWeight[] existing, PatternWeight[] defaults)
        {
            if (defaults == null || defaults.Length == 0)
                return existing ?? new PatternWeight[0];

            if (existing == null || existing.Length == 0)
                return defaults;

            var result = new List<PatternWeight>(existing);
            for (var i = 0; i < defaults.Length; i++)
            {
                var item = defaults[i];
                if (item == null)
                    continue;

                if (!HasPatternWeight(result, item.type))
                {
                    result.Add(new PatternWeight
                    {
                        type = item.type,
                        weight = item.weight,
                    });
                }
            }

            return result.ToArray();
        }

        private static bool HasStateWeight(List<StateWeight> list, MarketStateType type)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].type == type)
                    return true;
            }

            return false;
        }

        private static bool HasPatternWeight(List<PatternWeight> list, MarketStateType type)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].type == type)
                    return true;
            }

            return false;
        }

        private static StateWeight[] BuildDefaultStateWeights(CurrencyId id)
        {
            if (id == CurrencyId.SHT)
            {
                return new[]
                {
                    new StateWeight { type = MarketStateType.CalmCorridor, weight = 0.35f },
                    new StateWeight { type = MarketStateType.ActiveCorridor, weight = 2.6f },
                    new StateWeight { type = MarketStateType.ScalpingWindow, weight = 2.2f },
                    new StateWeight { type = MarketStateType.PressureUp, weight = 1.8f },
                    new StateWeight { type = MarketStateType.PressureDown, weight = 0.55f },
                    new StateWeight { type = MarketStateType.SlowTrendUp, weight = 0.75f },
                    new StateWeight { type = MarketStateType.SlowTrendDown, weight = 0.45f },
                    new StateWeight { type = MarketStateType.VolatileChop, weight = 0.35f },
                    new StateWeight { type = MarketStateType.Accumulation, weight = 1.3f },
                    new StateWeight { type = MarketStateType.Distribution, weight = 0.45f },
                };
            }

            if (id == CurrencyId.ETH)
            {
                return new[]
                {
                    new StateWeight { type = MarketStateType.CalmCorridor, weight = 0.9f },
                    new StateWeight { type = MarketStateType.ActiveCorridor, weight = 0.95f },
                    new StateWeight { type = MarketStateType.ScalpingWindow, weight = 0.45f },
                    new StateWeight { type = MarketStateType.PressureUp, weight = 1.0f },
                    new StateWeight { type = MarketStateType.PressureDown, weight = 0.85f },
                    new StateWeight { type = MarketStateType.SlowTrendUp, weight = 1.45f },
                    new StateWeight { type = MarketStateType.SlowTrendDown, weight = 1.15f },
                    new StateWeight { type = MarketStateType.VolatileChop, weight = 0.65f },
                    new StateWeight { type = MarketStateType.Accumulation, weight = 1.35f },
                    new StateWeight { type = MarketStateType.Distribution, weight = 1.05f },
                };
            }

            if (id == CurrencyId.BTC)
            {
                return new[]
                {
                    new StateWeight { type = MarketStateType.CalmCorridor, weight = 0.75f },
                    new StateWeight { type = MarketStateType.ActiveCorridor, weight = 0.75f },
                    new StateWeight { type = MarketStateType.ScalpingWindow, weight = 0.25f },
                    new StateWeight { type = MarketStateType.PressureUp, weight = 0.9f },
                    new StateWeight { type = MarketStateType.PressureDown, weight = 0.9f },
                    new StateWeight { type = MarketStateType.SlowTrendUp, weight = 0.95f },
                    new StateWeight { type = MarketStateType.SlowTrendDown, weight = 0.95f },
                    new StateWeight { type = MarketStateType.VolatileChop, weight = 1.7f },
                    new StateWeight { type = MarketStateType.Accumulation, weight = 0.95f },
                    new StateWeight { type = MarketStateType.Distribution, weight = 0.95f },
                };
            }

            return new[]
            {
                new StateWeight { type = MarketStateType.CalmCorridor, weight = 1f },
                new StateWeight { type = MarketStateType.ActiveCorridor, weight = 1f },
            };
        }

        private static PatternWeight[] BuildDefaultPatternWeights(CurrencyId id)
        {
            if (id == CurrencyId.SHT)
            {
                return new[]
                {
                    new PatternWeight { type = MarketStateType.BigSaw, weight = 1.35f },
                    new PatternWeight { type = MarketStateType.StaircaseShiftUp, weight = 1.05f },
                    new PatternWeight { type = MarketStateType.StaircaseShiftDown, weight = 0.45f },
                    new PatternWeight { type = MarketStateType.FalseBreakoutUp, weight = 0.55f },
                    new PatternWeight { type = MarketStateType.FalseBreakoutDown, weight = 0.35f },
                    new PatternWeight { type = MarketStateType.PumpAndCorrection, weight = 1.0f },
                    new PatternWeight { type = MarketStateType.DumpAndRecovery, weight = 0.45f },
                    new PatternWeight { type = MarketStateType.CompressionBreakoutNew, weight = 0.8f },
                    new PatternWeight { type = MarketStateType.SpikeUpRevert, weight = 0.85f },
                    new PatternWeight { type = MarketStateType.SpikeDownRevert, weight = 0.55f },
                };
            }

            if (id == CurrencyId.ETH)
            {
                return new[]
                {
                    new PatternWeight { type = MarketStateType.BigSaw, weight = 0.8f },
                    new PatternWeight { type = MarketStateType.StaircaseShiftUp, weight = 0.85f },
                    new PatternWeight { type = MarketStateType.StaircaseShiftDown, weight = 0.75f },
                    new PatternWeight { type = MarketStateType.FalseBreakoutUp, weight = 0.55f },
                    new PatternWeight { type = MarketStateType.FalseBreakoutDown, weight = 0.55f },
                    new PatternWeight { type = MarketStateType.PumpAndCorrection, weight = 0.7f },
                    new PatternWeight { type = MarketStateType.DumpAndRecovery, weight = 0.65f },
                    new PatternWeight { type = MarketStateType.CompressionBreakoutNew, weight = 1.05f },
                    new PatternWeight { type = MarketStateType.SpikeUpRevert, weight = 0.35f },
                    new PatternWeight { type = MarketStateType.SpikeDownRevert, weight = 0.25f },
                };
            }

            if (id == CurrencyId.BTC)
            {
                return new[]
                {
                    new PatternWeight { type = MarketStateType.BigSaw, weight = 0.9f },
                    new PatternWeight { type = MarketStateType.StaircaseShiftUp, weight = 0.75f },
                    new PatternWeight { type = MarketStateType.StaircaseShiftDown, weight = 0.75f },
                    new PatternWeight { type = MarketStateType.FalseBreakoutUp, weight = 1.3f },
                    new PatternWeight { type = MarketStateType.FalseBreakoutDown, weight = 1.3f },
                    new PatternWeight { type = MarketStateType.PumpAndCorrection, weight = 1.3f },
                    new PatternWeight { type = MarketStateType.DumpAndRecovery, weight = 1.25f },
                    new PatternWeight { type = MarketStateType.CompressionBreakoutNew, weight = 0.8f },
                    new PatternWeight { type = MarketStateType.SpikeUpRevert, weight = 0.45f },
                    new PatternWeight { type = MarketStateType.SpikeDownRevert, weight = 0.40f },
                };
            }

            return new[]
            {
                new PatternWeight { type = MarketStateType.BigSaw, weight = 1f },
                new PatternWeight { type = MarketStateType.SpikeUpRevert, weight = 0.25f },
                new PatternWeight { type = MarketStateType.SpikeDownRevert, weight = 0.25f },
            };
        }
    }
}