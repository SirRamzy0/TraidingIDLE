using System;
using UnityEngine;
using TraidingIDLE.Currencies;

namespace TraidingIDLE.Currencies.Simulation
{
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

