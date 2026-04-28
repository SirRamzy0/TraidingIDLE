using System;
using UnityEngine;
using TraidingIDLE.Currencies;

namespace TraidingIDLE.Currencies.Simulation
{
    [Serializable]
    public sealed class MarketStateWeight
    {
        public MarketStateType type = MarketStateType.Calm;
        [Min(0f)]
        public float weight = 1f;
    }

    [Serializable]
    public sealed class CoinSimulationConfig
    {
        [Header("Identity")]
        public CurrencyId id = CurrencyId.SHT;

        [Header("Timing")]
        [Min(0.05f)]
        public float tickIntervalSeconds = 1f;

        [Header("State tick speed")]
        [Min(0.01f)]
        public float normalTickSpeedMultiplier = 1f;
        [Min(0.01f)]
        public float pumpTickSpeedMultiplier = 1.25f;
        [Min(0.01f)]
        public float crashTickSpeedMultiplier = 1.5f;

        [Min(1)]
        public int plannedStatesCount = 3;

        [Header("Start")]
        [Min(0.000001f)]
        public float initialPrice = 1f;

        [Header("State durations")]
        [Min(1f)]
        public float stateDurationMinSeconds = 30f;
        [Min(1f)]
        public float stateDurationMaxSeconds = 300f;

        [Header("State weights")]
        [Tooltip("Random planned state weights. Weight 0 disables the state. MarketCrash can still be forced by crash logic.")]
        public MarketStateWeight[] plannedStateWeights =
        {
            new() { type = MarketStateType.Calm, weight = 1f },
            new() { type = MarketStateType.ChopInCorridor, weight = 1f },
            new() { type = MarketStateType.LongUptrend, weight = 0.35f },
            new() { type = MarketStateType.LongDowntrend, weight = 0.25f },
        };

        [Header("Corridor (volatility shrinks with price)")]
        [Min(1.01f)]
        public float corridorWidthAtLowPrice = 2.5f;   // high/low when "cheap"
        [Min(1.01f)]
        public float corridorWidthAtHighPrice = 1.25f; // high/low when "expensive"
        [Min(0.000001f)]
        public float highPriceReference = 100000f;
        [Range(0f, 0.5f)]
        public float corridorWidthRandomJitter = 0.08f;

        [Header("Corridor limits")]
        [Min(0.01f)]
        public float minCorridorLowFromInitial = 0.5f;

        [Header("Movement & noise")]
        [Min(0f)]
        public float noiseStrength = 0.35f;
        [Min(0f)]
        public float trendStrength = 0.25f;
        [Min(0f)]
        public float meanReversionStrength = 0.35f;

        [Header("Chop in corridor")]
        [Min(1f)]
        public float chopPhaseDurationMinSeconds = 10f;
        [Min(1f)]
        public float chopPhaseDurationMaxSeconds = 35f;
        [Tooltip("Chance that a chop leg gets a multi-second fakeout against the main target.")]
        [Range(0f, 1f)]
        public float chopFakeoutChancePerPhase = 0.75f;
        [Min(0.5f)]
        public float chopFakeoutDurationMinSeconds = 2f;
        [Min(0.5f)]
        public float chopFakeoutDurationMaxSeconds = 6f;
        [Range(0f, 1f)]
        public float chopFakeoutStrength = 0.42f;
        [Min(0.5f)]
        public float chopRecoveryDurationSeconds = 3f;
        [Range(1f, 4f)]
        public float chopRecoveryBoost = 1.8f;

        [Header("Calm (Спокойствие)")]
        [Tooltip("Доля от ПОЛОВИНЫ ширины коридора: цель = центр ± Random(min..max) * (high-low)/2.")]
        [Range(0f, 0.49f)]
        public float calmOffsetFromHalfMin01 = 0.10f;
        [Range(0f, 0.49f)]
        public float calmOffsetFromHalfMax01 = 0.30f;
        [Min(1f)]
        public float calmLegDurationMinSeconds = 10f;
        [Min(1f)]
        public float calmLegDurationMaxSeconds = 60f;
        [Min(0.5f)]
        public float calmShortLegMaxSeconds = 20f;
        [Tooltip("Если |новая цель − предыдущая точка| / ширина коридора ≤ порога — длительность фазы не больше calmShortLegMaxSeconds.")]
        [Range(0.01f, 1f)]
        public float calmSmallMoveRelativeThreshold = 0.20f;
        [Range(0f, 1f)]
        public float calmMaxPriceChangePerTick01 = 0.045f;
        [Min(0f)]
        public float calmNoiseStrength = 0.08f;
        [Tooltip("Насколько агрессивно тянем к цели за оставшееся время фазы (меньше — плавнее).")]
        [Range(0.05f, 0.6f)]
        public float calmApproachStrength = 0.22f;

        [Header("Long uptrend")]
        [Min(1f)]
        public float longUptrendDurationMinSeconds = 20f;
        [Min(1f)]
        public float longUptrendDurationMaxSeconds = 90f;
        [Tooltip("Raw desired corridor-anchor multiplier before soft cap. Cheap coins can feel very punchy.")]
        [Min(1.01f)]
        public float longUptrendTargetMultiplierMin = 2f;
        [Min(1.01f)]
        public float longUptrendTargetMultiplierMax = 4f;
        [Tooltip("At or above this part of highPriceReference, big multipliers are compressed.")]
        [Range(0.01f, 1f)]
        public float longUptrendSoftCapAtHighReference01 = 0.35f;
        [Tooltip("Minimum part of the rolled x2/x4 boost that remains after soft cap, so growth is still noticeable.")]
        [Range(0.05f, 1f)]
        public float longUptrendMinBoostAfterSoftCap01 = 0.25f;
        [Tooltip("Final target price is capped to current * this value after the soft cap.")]
        [Min(1.01f)]
        public float longUptrendMaxEffectiveMultiplier = 2.25f;
        [Range(0.45f, 0.90f)]
        public float longUptrendTargetCorridorPos = 0.62f;
        [Range(0.05f, 0.8f)]
        public float longUptrendApproachStrength = 0.35f;
        [Min(0f)]
        public float longUptrendNoiseStrength = 0.08f;
        [Tooltip("Chance per simulation tick to start a multi-tick pullback phase during LongUptrend.")]
        [Range(0f, 0.5f)]
        public float longUptrendPullbackChancePerTick = 0.16f;
        [Range(0f, 0.5f)]
        public float longUptrendPullbackStrength = 0.10f;
        [Min(0.5f)]
        public float longUptrendPullbackDurationMinSeconds = 2f;
        [Min(0.5f)]
        public float longUptrendPullbackDurationMaxSeconds = 6f;
        [Min(0f)]
        public float longUptrendPullbackCooldownSeconds = 8f;
        [Min(0.5f)]
        public float longUptrendRecoveryDurationSeconds = 4f;
        [Range(1f, 4f)]
        public float longUptrendRecoveryCatchupMultiplier = 2f;
        [Range(0f, 1f)]
        public float longUptrendMaxPriceChangePerTick01 = 0.06f;

        [Header("Long downtrend")]
        [Min(1f)]
        public float longDowntrendDurationMinSeconds = 20f;
        [Min(1f)]
        public float longDowntrendDurationMaxSeconds = 90f;
        [Min(1.01f)]
        public float longDowntrendTargetDividerMin = 1.35f;
        [Min(1.01f)]
        public float longDowntrendTargetDividerMax = 2.6f;
        [Range(0.10f, 0.55f)]
        public float longDowntrendTargetCorridorPos = 0.35f;
        [Range(0.05f, 0.8f)]
        public float longDowntrendApproachStrength = 0.35f;
        [Min(0f)]
        public float longDowntrendNoiseStrength = 0.08f;
        [Tooltip("Chance per simulation tick to start a multi-tick fake rally during LongDowntrend.")]
        [Range(0f, 0.5f)]
        public float longDowntrendRallyChancePerTick = 0.16f;
        [Range(0f, 0.5f)]
        public float longDowntrendRallyStrength = 0.10f;
        [Min(0.5f)]
        public float longDowntrendRallyDurationMinSeconds = 2f;
        [Min(0.5f)]
        public float longDowntrendRallyDurationMaxSeconds = 6f;
        [Min(0f)]
        public float longDowntrendRallyCooldownSeconds = 8f;
        [Min(0.5f)]
        public float longDowntrendRecoveryDurationSeconds = 4f;
        [Range(1f, 4f)]
        public float longDowntrendRecoveryCatchupMultiplier = 2f;
        [Range(0f, 1f)]
        public float longDowntrendMaxPriceChangePerTick01 = 0.06f;

        [Header("Max price change per tick")]
        [Range(0f, 1f)]
        public float normalMaxPriceChangePerTick01 = 0.10f;
        [Range(0f, 1f)]
        public float pumpMaxPriceChangePerTick01 = 0.25f;
        [Range(0f, 1f)]
        public float crashMaxPriceChangePerTick01 = 0.35f;

        [Header("Crash logic")]
        [Min(0.000001f)]
        public float crashFirstTriggerPrice = 1000f;

        [Min(0f)]
        public float crashThresholdRandomMin = 10000f;
        [Min(0f)]
        public float crashThresholdRandomMax = 1000000f;

        [Range(0.5f, 1.5f)]
        public float crashRecoverToInitialMultiplier = 1.05f;

        [Header("Rounding (minPrice → step)")]
        public PriceRoundingRule[] roundingRules =
        {
            new() { minPrice = 0f, step = 5f },
        };
    }
}

