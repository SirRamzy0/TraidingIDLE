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
        private struct PlannedState
        {
            public MarketStateType type;
            public float durationSeconds;
            public float businessGrowDurationSeconds;
            public float businessDumpDurationSeconds;
            public float businessGrowthPercent;
            public float businessReturnTolerancePercent;
        }

        private readonly struct StateChoice
        {
            public StateChoice(MarketStateType type, float weight)
            {
                this.type = type;
                this.weight = weight;
            }

            public readonly MarketStateType type;
            public readonly float weight;
        }

        private struct BalanceRunStats
        {
            public CurrencyId id;
            public double startPrice;
            public double finalPrice;
            public double checkpoint15mPrice;
            public double checkpoint30mPrice;
            public double checkpoint60mPrice;
            public double minPrice;
            public double maxPrice;
            public double maxDrawdown01;
            public double bestSwing01;
            public double averageAbsTickMove01;
            public double nearFloorSeconds;
            public int ticks;
        }

        private sealed class BalanceAggregateStats
        {
            public CurrencyId id;
            public int runs;
            public double startPriceSum;
            public double finalPriceSum;
            public double checkpoint15mPriceSum;
            public double checkpoint30mPriceSum;
            public double checkpoint60mPriceSum;
            public double minPriceSum;
            public double maxPriceSum;
            public double maxDrawdownSum;
            public double maxDrawdownWorst;
            public double bestSwingSum;
            public double bestSwingBest;
            public double averageAbsTickMoveSum;
            public double nearFloorSecondsSum;

            public void Add(BalanceRunStats stats)
            {
                id = stats.id;
                runs++;
                startPriceSum += stats.startPrice;
                finalPriceSum += stats.finalPrice;
                checkpoint15mPriceSum += stats.checkpoint15mPrice;
                checkpoint30mPriceSum += stats.checkpoint30mPrice;
                checkpoint60mPriceSum += stats.checkpoint60mPrice;
                minPriceSum += stats.minPrice;
                maxPriceSum += stats.maxPrice;
                maxDrawdownSum += stats.maxDrawdown01;
                maxDrawdownWorst = Math.Max(maxDrawdownWorst, stats.maxDrawdown01);
                bestSwingSum += stats.bestSwing01;
                bestSwingBest = Math.Max(bestSwingBest, stats.bestSwing01);
                averageAbsTickMoveSum += stats.averageAbsTickMove01;
                nearFloorSecondsSum += stats.nearFloorSeconds;
            }
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
            public float deadFlatRocketFlatDuration;
            public float deadFlatRocketPumpDuration;
            public float deadFlatRocketPumpTimeLeft;
            public bool deadFlatRocketPumpStarted;
            public bool deadFlatRocketInitialized;

            public float deadFlatDumpBasePrice;
            public float deadFlatDumpStartPrice;
            public float deadFlatDumpTargetPrice;
            public float deadFlatDumpFlatTimeLeft;
            public float deadFlatDumpFlatDuration;
            public float deadFlatDumpDuration;
            public float deadFlatDumpTimeLeft;
            public bool deadFlatDumpStarted;
            public bool deadFlatDumpInitialized;

            public float businessSkillBasePrice;
            public float businessSkillGrowStartPrice;
            public float businessSkillTargetPrice;
            public float businessSkillDumpStartPrice;
            public float businessSkillReturnPrice;
            public float businessSkillGrowDuration;
            public float businessSkillGrowTimeLeft;
            public float businessSkillDumpDuration;
            public float businessSkillDumpTimeLeft;
            public bool businessSkillDumpStarted;
            public bool businessSkillInitialized;

            public int patternLegIndex;
            public int patternLegsTotal;
            public int patternDirection = 1;
            public float patternBasePrice;
            public float patternLastMajorPrice;
            public float patternLegStartPrice;
            public float patternLegTargetPrice;
            public float patternLegDuration;
            public float patternLegTimeLeft;
            public bool patternInitialized;

            public float starterAssistTimeLeft;
            public float starterAssistElapsed;
            public float economyPriceTarget;

            public int seed;
            public float time;
        }

        [Header("Target market")]
        [SerializeField] private CurrencyMarket market = null!;
        [SerializeField] private PriceHistoryStore? priceHistoryStore = null;
        [SerializeField] private PlayerProfile? playerProfile = null;
        [SerializeField] private BusinessController? businessProgressLink = null;
        [SerializeField] private bool useCurrencyMarketPricesOnStart = false;
        [Tooltip("On first launch (no saved history), warm up each coin by running this many ticks so the chart isn't empty.")]
        [SerializeField, Min(0)] private int initialHistoryWarmupTicks = 50;

        [Header("Game start")]
        [SerializeField] private bool applyGameStartSettings = true;
        [SerializeField] private CurrencyId startActiveCurrency = CurrencyId.SHT;
        [SerializeField] private StartPrice[] startPrices =
        {
            new() { id = CurrencyId.SHT, price = 5000f },
            new() { id = CurrencyId.ETH, price = 100000f },
            new() { id = CurrencyId.BTC, price = 2500000f },
        };

        [Header("Coins (3 configs)")]
        [SerializeField] private CoinSimulationConfig[] coins =
        {
            new()
            {
                id = CurrencyId.SHT,
                tickIntervalSeconds = 0.6f,
                normalTickSpeedMultiplier = 1f,
                pumpTickSpeedMultiplier = 1.55f,
                crashTickSpeedMultiplier = 1.85f,
                plannedStatesCount = 4,
                initialPrice = 5000f,
                stateDurationMinSeconds = 8f,
                stateDurationMaxSeconds = 42f,
                plannedStateWeights = new[]
                {
                    new MarketStateWeight { type = MarketStateType.Calm, weight = 0.04f },
                    new MarketStateWeight { type = MarketStateType.ChopInCorridor, weight = 1.95f },
                    new MarketStateWeight { type = MarketStateType.LongUptrend, weight = 0.85f },
                    new MarketStateWeight { type = MarketStateType.LongDowntrend, weight = 0.62f },
                    new MarketStateWeight { type = MarketStateType.SlowUpThenDump, weight = 0.62f },
                    new MarketStateWeight { type = MarketStateType.SlowUpThenPumpAndDump, weight = 0.82f },
                    new MarketStateWeight { type = MarketStateType.DeadFlatThenRocketPump, weight = 0.16f },
                    new MarketStateWeight { type = MarketStateType.DeadFlatThenRocketDump, weight = 0.14f },
                },
                corridorWidthAtLowPrice = 4.4f,
                corridorWidthAtHighPrice = 1.65f,
                highPriceReference = 120000f,
                corridorWidthRandomJitter = 0.22f,
                minCorridorLowFromInitial = 0.25f,
                calmCorridorWidth = 1.55f,
                calmCorridorPullStrength = 0.20f,
                chopCorridorWidthMin = 3.4f,
                chopCorridorWidthMax = 6.2f,
                chopCorridorPullStrength = 0.18f,
                trendCorridorWidth = 2.55f,
                trendCorridorPullStrength = 0.045f,
                scenarioCorridorWidth = 2.20f,
                scenarioCorridorPullStrength = 0.045f,
                deadFlatCorridorWidth = 1.08f,
                deadFlatCorridorPullStrength = 0.010f,
                crashCorridorWidth = 1.35f,
                crashCorridorPullStrength = 0.16f,
                noiseStrength = 0.88f,
                trendStrength = 0.44f,
                meanReversionStrength = 0.16f,
                chopPhaseDurationMinSeconds = 3.5f,
                chopPhaseDurationMaxSeconds = 11f,
                chopFakeoutChancePerPhase = 0.92f,
                chopFakeoutDurationMinSeconds = 0.8f,
                chopFakeoutDurationMaxSeconds = 3.2f,
                chopFakeoutStrength = 0.78f,
                chopRecoveryDurationSeconds = 1.4f,
                chopRecoveryBoost = 2.8f,
                calmOffsetFromHalfMin01 = 0.14f,
                calmOffsetFromHalfMax01 = 0.42f,
                calmLegDurationMinSeconds = 2.5f,
                calmLegDurationMaxSeconds = 8f,
                calmShortLegMaxSeconds = 4f,
                calmSmallMoveRelativeThreshold = 0.08f,
                calmMaxPriceChangePerTick01 = 0.12f,
                calmNoiseStrength = 0.32f,
                calmApproachStrength = 0.36f,
                longUptrendDurationMinSeconds = 9f,
                longUptrendDurationMaxSeconds = 32f,
                longUptrendTargetMultiplierMin = 2.00f,
                longUptrendTargetMultiplierMax = 5.20f,
                longUptrendSoftCapAtHighReference01 = 0.55f,
                longUptrendMinBoostAfterSoftCap01 = 0.35f,
                longUptrendMaxEffectiveMultiplier = 4.80f,
                longUptrendTargetCorridorPos = 0.74f,
                longUptrendApproachStrength = 0.48f,
                longUptrendNoiseStrength = 0.30f,
                longUptrendWobbleStrength = 0.92f,
                longUptrendDownWobbleChance = 0.52f,
                longUptrendDownWobbleMultiplier = 1.80f,
                longUptrendUpWobbleMultiplier = 0.85f,
                longUptrendPullbackChancePerTick = 0.30f,
                longUptrendPullbackStrength = 0.20f,
                longUptrendPullbackDurationMinSeconds = 0.6f,
                longUptrendPullbackDurationMaxSeconds = 1.8f,
                longUptrendPullbackCooldownSeconds = 1.1f,
                longUptrendRecoveryDurationSeconds = 1.0f,
                longUptrendRecoveryCatchupMultiplier = 3.0f,
                longUptrendMaxPriceChangePerTick01 = 0.22f,
                longDowntrendDurationMinSeconds = 8f,
                longDowntrendDurationMaxSeconds = 28f,
                longDowntrendTargetDividerMin = 1.75f,
                longDowntrendTargetDividerMax = 4.20f,
                longDowntrendTargetCorridorPos = 0.22f,
                longDowntrendApproachStrength = 0.48f,
                longDowntrendNoiseStrength = 0.28f,
                longDowntrendRallyChancePerTick = 0.30f,
                longDowntrendRallyStrength = 0.22f,
                longDowntrendRallyDurationMinSeconds = 0.9f,
                longDowntrendRallyDurationMaxSeconds = 3.5f,
                longDowntrendRallyCooldownSeconds = 2.2f,
                longDowntrendRecoveryDurationSeconds = 1.8f,
                longDowntrendRecoveryCatchupMultiplier = 2.8f,
                longDowntrendMaxPriceChangePerTick01 = 0.24f,
                spikeDumpGrowPercentMin = 0.55f,
                spikeDumpGrowPercentMax = 1.45f,
                spikeDumpGrowDurationMinSeconds = 5f,
                spikeDumpGrowDurationMaxSeconds = 16f,
                spikeDumpDropOverGrowMin = 1.05f,
                spikeDumpDropOverGrowMax = 1.75f,
                spikeDumpMaxDropPercent = 0.92f,
                spikeDumpDumpDurationMinSeconds = 1f,
                spikeDumpDumpDurationMaxSeconds = 4f,
                spikeDumpShakeMovesMin = 3,
                spikeDumpShakeMovesMax = 7,
                spikeDumpShakeDurationMinSeconds = 1.5f,
                spikeDumpShakeDurationMaxSeconds = 6f,
                spikeDumpShakeMovePercentMin = 0.10f,
                spikeDumpShakeMovePercentMax = 0.38f,
                spikeDumpNoiseStrength = 0.20f,
                spikeDumpMaxPriceChangePerTick01 = 0.34f,
                dipPumpDipPercentMin = 0.35f,
                dipPumpDipPercentMax = 0.72f,
                dipPumpDipDurationMinSeconds = 5f,
                dipPumpDipDurationMaxSeconds = 16f,
                dipPumpPumpOverDipMin = 1.25f,
                dipPumpPumpOverDipMax = 2.40f,
                dipPumpMaxPumpPercent = 1.85f,
                dipPumpPumpDurationMinSeconds = 1f,
                dipPumpPumpDurationMaxSeconds = 4f,
                dipPumpShakeMovesMin = 3,
                dipPumpShakeMovesMax = 7,
                dipPumpShakeDurationMinSeconds = 1.5f,
                dipPumpShakeDurationMaxSeconds = 6f,
                dipPumpShakeMovePercentMin = 0.10f,
                dipPumpShakeMovePercentMax = 0.38f,
                dipPumpNoiseStrength = 0.20f,
                dipPumpMaxPriceChangePerTick01 = 0.34f,
                deadFlatRocketFlatDurationMinSeconds = 3f,
                deadFlatRocketFlatDurationMaxSeconds = 8f,
                deadFlatRocketFlatRange01 = 0.045f,
                deadFlatRocketNoiseStrength = 0.055f,
                deadFlatRocketPumpPercentMin = 0.60f,
                deadFlatRocketPumpPercentMax = 1.90f,
                deadFlatRocketPumpDurationMinSeconds = 0.8f,
                deadFlatRocketPumpDurationMaxSeconds = 3.2f,
                deadFlatRocketMaxPriceChangePerTick01 = 0.48f,
                deadFlatDumpFlatDurationMinSeconds = 3f,
                deadFlatDumpFlatDurationMaxSeconds = 8f,
                deadFlatDumpFlatRange01 = 0.045f,
                deadFlatDumpNoiseStrength = 0.055f,
                deadFlatDumpDropPercentMin = 0.35f,
                deadFlatDumpDropPercentMax = 0.78f,
                deadFlatDumpDurationMinSeconds = 0.8f,
                deadFlatDumpDurationMaxSeconds = 3.2f,
                deadFlatDumpMaxPriceChangePerTick01 = 0.48f,
                normalMaxPriceChangePerTick01 = 0.18f,
                pumpMaxPriceChangePerTick01 = 0.48f,
                crashMaxPriceChangePerTick01 = 0.60f,
                roundingRules = new[] { new PriceRoundingRule { minPrice = 0f, step = 1f } },
                crashFirstTriggerPrice = 120000f,
                crashThresholdRandomMin = 150000f,
                crashThresholdRandomMax = 650000f,
                crashRecoverToInitialMultiplier = 1.05f,
            },
            new()
            {
                id = CurrencyId.ETH,
                tickIntervalSeconds = 0.85f,
                normalTickSpeedMultiplier = 1f,
                pumpTickSpeedMultiplier = 1.18f,
                crashTickSpeedMultiplier = 1.35f,
                plannedStatesCount = 3,
                initialPrice = 100000f,
                stateDurationMinSeconds = 45f,
                stateDurationMaxSeconds = 170f,
                plannedStateWeights = new[]
                {
                    new MarketStateWeight { type = MarketStateType.Calm, weight = 1.40f },
                    new MarketStateWeight { type = MarketStateType.ChopInCorridor, weight = 0.80f },
                    new MarketStateWeight { type = MarketStateType.LongUptrend, weight = 0.28f },
                    new MarketStateWeight { type = MarketStateType.LongDowntrend, weight = 0.35f },
                    new MarketStateWeight { type = MarketStateType.SlowUpThenDump, weight = 0.25f },
                    new MarketStateWeight { type = MarketStateType.SlowUpThenPumpAndDump, weight = 0.22f },
                    new MarketStateWeight { type = MarketStateType.DeadFlatThenRocketPump, weight = 0.08f },
                    new MarketStateWeight { type = MarketStateType.DeadFlatThenRocketDump, weight = 0.12f },
                },
                corridorWidthAtLowPrice = 1.55f,
                corridorWidthAtHighPrice = 1.20f,
                highPriceReference = 2000000f,
                corridorWidthRandomJitter = 0.08f,
                minCorridorLowFromInitial = 0.55f,
                calmCorridorWidth = 1.10f,
                calmCorridorPullStrength = 0.44f,
                chopCorridorWidthMin = 1.35f,
                chopCorridorWidthMax = 2.10f,
                chopCorridorPullStrength = 0.38f,
                trendCorridorWidth = 1.45f,
                trendCorridorPullStrength = 0.075f,
                scenarioCorridorWidth = 1.28f,
                scenarioCorridorPullStrength = 0.065f,
                deadFlatCorridorWidth = 1.025f,
                deadFlatCorridorPullStrength = 0.018f,
                crashCorridorWidth = 1.38f,
                crashCorridorPullStrength = 0.14f,
                noiseStrength = 0.30f,
                trendStrength = 0.24f,
                meanReversionStrength = 0.42f,
                chopPhaseDurationMinSeconds = 16f,
                chopPhaseDurationMaxSeconds = 46f,
                chopFakeoutChancePerPhase = 0.55f,
                chopFakeoutDurationMinSeconds = 3f,
                chopFakeoutDurationMaxSeconds = 8f,
                chopFakeoutStrength = 0.32f,
                chopRecoveryDurationSeconds = 5f,
                chopRecoveryBoost = 1.55f,
                calmOffsetFromHalfMin01 = 0.11f,
                calmOffsetFromHalfMax01 = 0.30f,
                calmLegDurationMinSeconds = 16f,
                calmLegDurationMaxSeconds = 65f,
                calmShortLegMaxSeconds = 22f,
                calmSmallMoveRelativeThreshold = 0.24f,
                calmMaxPriceChangePerTick01 = 0.038f,
                calmNoiseStrength = 0.070f,
                calmApproachStrength = 0.18f,
                longUptrendDurationMinSeconds = 50f,
                longUptrendDurationMaxSeconds = 145f,
                longUptrendTargetMultiplierMin = 1.75f,
                longUptrendTargetMultiplierMax = 3.20f,
                longUptrendSoftCapAtHighReference01 = 0.30f,
                longUptrendMinBoostAfterSoftCap01 = 0.22f,
                longUptrendMaxEffectiveMultiplier = 2.80f,
                longUptrendTargetCorridorPos = 0.66f,
                longUptrendApproachStrength = 0.27f,
                longUptrendNoiseStrength = 0.055f,
                longUptrendWobbleStrength = 0.26f,
                longUptrendDownWobbleChance = 0.54f,
                longUptrendDownWobbleMultiplier = 1.15f,
                longUptrendUpWobbleMultiplier = 0.45f,
                longUptrendPullbackChancePerTick = 0.12f,
                longUptrendPullbackStrength = 0.075f,
                longUptrendPullbackDurationMinSeconds = 1.5f,
                longUptrendPullbackDurationMaxSeconds = 4.5f,
                longUptrendPullbackCooldownSeconds = 5f,
                longUptrendRecoveryDurationSeconds = 4f,
                longUptrendRecoveryCatchupMultiplier = 1.65f,
                longUptrendMaxPriceChangePerTick01 = 0.055f,
                longDowntrendDurationMinSeconds = 45f,
                longDowntrendDurationMaxSeconds = 140f,
                longDowntrendTargetDividerMin = 1.30f,
                longDowntrendTargetDividerMax = 2.25f,
                longDowntrendTargetCorridorPos = 0.33f,
                longDowntrendApproachStrength = 0.30f,
                longDowntrendNoiseStrength = 0.06f,
                longDowntrendRallyChancePerTick = 0.12f,
                longDowntrendRallyStrength = 0.08f,
                longDowntrendRallyDurationMinSeconds = 3f,
                longDowntrendRallyDurationMaxSeconds = 8f,
                longDowntrendRallyCooldownSeconds = 10f,
                longDowntrendRecoveryDurationSeconds = 7f,
                longDowntrendRecoveryCatchupMultiplier = 1.55f,
                longDowntrendMaxPriceChangePerTick01 = 0.055f,
                spikeDumpGrowPercentMin = 0.30f,
                spikeDumpGrowPercentMax = 0.95f,
                spikeDumpGrowDurationMinSeconds = 18f,
                spikeDumpGrowDurationMaxSeconds = 48f,
                spikeDumpDropOverGrowMin = 1.05f,
                spikeDumpDropOverGrowMax = 1.45f,
                spikeDumpMaxDropPercent = 0.82f,
                spikeDumpDumpDurationMinSeconds = 3f,
                spikeDumpDumpDurationMaxSeconds = 9f,
                spikeDumpShakeMovesMin = 2,
                spikeDumpShakeMovesMax = 4,
                spikeDumpShakeDurationMinSeconds = 5f,
                spikeDumpShakeDurationMaxSeconds = 14f,
                spikeDumpShakeMovePercentMin = 0.04f,
                spikeDumpShakeMovePercentMax = 0.14f,
                spikeDumpNoiseStrength = 0.045f,
                spikeDumpMaxPriceChangePerTick01 = 0.14f,
                dipPumpDipPercentMin = 0.18f,
                dipPumpDipPercentMax = 0.50f,
                dipPumpDipDurationMinSeconds = 18f,
                dipPumpDipDurationMaxSeconds = 52f,
                dipPumpPumpOverDipMin = 1.25f,
                dipPumpPumpOverDipMax = 1.95f,
                dipPumpMaxPumpPercent = 1.30f,
                dipPumpPumpDurationMinSeconds = 3f,
                dipPumpPumpDurationMaxSeconds = 9f,
                dipPumpShakeMovesMin = 2,
                dipPumpShakeMovesMax = 4,
                dipPumpShakeDurationMinSeconds = 5f,
                dipPumpShakeDurationMaxSeconds = 14f,
                dipPumpShakeMovePercentMin = 0.04f,
                dipPumpShakeMovePercentMax = 0.14f,
                dipPumpNoiseStrength = 0.045f,
                dipPumpMaxPriceChangePerTick01 = 0.14f,
                deadFlatRocketFlatDurationMinSeconds = 45f,
                deadFlatRocketFlatDurationMaxSeconds = 110f,
                deadFlatRocketFlatRange01 = 0.007f,
                deadFlatRocketNoiseStrength = 0.006f,
                deadFlatRocketPumpPercentMin = 0.45f,
                deadFlatRocketPumpPercentMax = 1.20f,
                deadFlatRocketPumpDurationMinSeconds = 4f,
                deadFlatRocketPumpDurationMaxSeconds = 10f,
                deadFlatRocketMaxPriceChangePerTick01 = 0.18f,
                deadFlatDumpFlatDurationMinSeconds = 35f,
                deadFlatDumpFlatDurationMaxSeconds = 95f,
                deadFlatDumpFlatRange01 = 0.007f,
                deadFlatDumpNoiseStrength = 0.006f,
                deadFlatDumpDropPercentMin = 0.22f,
                deadFlatDumpDropPercentMax = 0.58f,
                deadFlatDumpDurationMinSeconds = 4f,
                deadFlatDumpDurationMaxSeconds = 10f,
                deadFlatDumpMaxPriceChangePerTick01 = 0.18f,
                normalMaxPriceChangePerTick01 = 0.045f,
                pumpMaxPriceChangePerTick01 = 0.18f,
                crashMaxPriceChangePerTick01 = 0.28f,
                roundingRules = new[]
                {
                    new PriceRoundingRule { minPrice = 0f, step = 1f },
                    new PriceRoundingRule { minPrice = 1000f, step = 5f },
                    new PriceRoundingRule { minPrice = 5000f, step = 10f },
                    new PriceRoundingRule { minPrice = 20000f, step = 50f },
                },
                crashFirstTriggerPrice = 500000f,
                crashThresholdRandomMin = 650000f,
                crashThresholdRandomMax = 2500000f,
                crashRecoverToInitialMultiplier = 1.02f,
            },
            new()
            {
                id = CurrencyId.BTC,
                tickIntervalSeconds = 0.95f,
                normalTickSpeedMultiplier = 1f,
                pumpTickSpeedMultiplier = 1.25f,
                crashTickSpeedMultiplier = 1.45f,
                plannedStatesCount = 3,
                initialPrice = 2500000f,
                stateDurationMinSeconds = 35f,
                stateDurationMaxSeconds = 130f,
                plannedStateWeights = new[]
                {
                    new MarketStateWeight { type = MarketStateType.Calm, weight = 0.45f },
                    new MarketStateWeight { type = MarketStateType.ChopInCorridor, weight = 1.15f },
                    new MarketStateWeight { type = MarketStateType.LongUptrend, weight = 0.36f },
                    new MarketStateWeight { type = MarketStateType.LongDowntrend, weight = 0.62f },
                    new MarketStateWeight { type = MarketStateType.SlowUpThenDump, weight = 0.36f },
                    new MarketStateWeight { type = MarketStateType.SlowUpThenPumpAndDump, weight = 0.48f },
                    new MarketStateWeight { type = MarketStateType.DeadFlatThenRocketPump, weight = 0.07f },
                    new MarketStateWeight { type = MarketStateType.DeadFlatThenRocketDump, weight = 0.14f },
                },
                corridorWidthAtLowPrice = 2.20f,
                corridorWidthAtHighPrice = 1.35f,
                highPriceReference = 25000000f,
                corridorWidthRandomJitter = 0.14f,
                minCorridorLowFromInitial = 0.45f,
                calmCorridorWidth = 1.16f,
                calmCorridorPullStrength = 0.30f,
                chopCorridorWidthMin = 1.95f,
                chopCorridorWidthMax = 3.45f,
                chopCorridorPullStrength = 0.24f,
                trendCorridorWidth = 1.90f,
                trendCorridorPullStrength = 0.050f,
                scenarioCorridorWidth = 1.70f,
                scenarioCorridorPullStrength = 0.050f,
                deadFlatCorridorWidth = 1.06f,
                deadFlatCorridorPullStrength = 0.012f,
                crashCorridorWidth = 1.55f,
                crashCorridorPullStrength = 0.13f,
                noiseStrength = 0.62f,
                trendStrength = 0.28f,
                meanReversionStrength = 0.26f,
                chopPhaseDurationMinSeconds = 9f,
                chopPhaseDurationMaxSeconds = 28f,
                chopFakeoutChancePerPhase = 0.78f,
                chopFakeoutDurationMinSeconds = 2f,
                chopFakeoutDurationMaxSeconds = 6f,
                chopFakeoutStrength = 0.52f,
                chopRecoveryDurationSeconds = 3.6f,
                chopRecoveryBoost = 1.85f,
                calmOffsetFromHalfMin01 = 0.10f,
                calmOffsetFromHalfMax01 = 0.34f,
                calmLegDurationMinSeconds = 10f,
                calmLegDurationMaxSeconds = 38f,
                calmShortLegMaxSeconds = 14f,
                calmSmallMoveRelativeThreshold = 0.16f,
                calmMaxPriceChangePerTick01 = 0.045f,
                calmNoiseStrength = 0.090f,
                calmApproachStrength = 0.22f,
                longUptrendDurationMinSeconds = 55f,
                longUptrendDurationMaxSeconds = 150f,
                longUptrendTargetMultiplierMin = 1.70f,
                longUptrendTargetMultiplierMax = 3.80f,
                longUptrendSoftCapAtHighReference01 = 0.32f,
                longUptrendMinBoostAfterSoftCap01 = 0.24f,
                longUptrendMaxEffectiveMultiplier = 3.20f,
                longUptrendTargetCorridorPos = 0.72f,
                longUptrendApproachStrength = 0.30f,
                longUptrendNoiseStrength = 0.120f,
                longUptrendWobbleStrength = 0.52f,
                longUptrendDownWobbleChance = 0.58f,
                longUptrendDownWobbleMultiplier = 1.55f,
                longUptrendUpWobbleMultiplier = 0.58f,
                longUptrendPullbackChancePerTick = 0.20f,
                longUptrendPullbackStrength = 0.12f,
                longUptrendPullbackDurationMinSeconds = 1.8f,
                longUptrendPullbackDurationMaxSeconds = 5f,
                longUptrendPullbackCooldownSeconds = 4.5f,
                longUptrendRecoveryDurationSeconds = 3.4f,
                longUptrendRecoveryCatchupMultiplier = 1.80f,
                longUptrendMaxPriceChangePerTick01 = 0.070f,
                longDowntrendDurationMinSeconds = 35f,
                longDowntrendDurationMaxSeconds = 105f,
                longDowntrendTargetDividerMin = 1.60f,
                longDowntrendTargetDividerMax = 3.50f,
                longDowntrendTargetCorridorPos = 0.26f,
                longDowntrendApproachStrength = 0.36f,
                longDowntrendNoiseStrength = 0.140f,
                longDowntrendRallyChancePerTick = 0.18f,
                longDowntrendRallyStrength = 0.13f,
                longDowntrendRallyDurationMinSeconds = 2.5f,
                longDowntrendRallyDurationMaxSeconds = 8f,
                longDowntrendRallyCooldownSeconds = 7f,
                longDowntrendRecoveryDurationSeconds = 5f,
                longDowntrendRecoveryCatchupMultiplier = 1.80f,
                longDowntrendMaxPriceChangePerTick01 = 0.080f,
                spikeDumpGrowPercentMin = 0.35f,
                spikeDumpGrowPercentMax = 1.05f,
                spikeDumpGrowDurationMinSeconds = 18f,
                spikeDumpGrowDurationMaxSeconds = 52f,
                spikeDumpDropOverGrowMin = 1.10f,
                spikeDumpDropOverGrowMax = 1.70f,
                spikeDumpMaxDropPercent = 0.86f,
                spikeDumpDumpDurationMinSeconds = 3f,
                spikeDumpDumpDurationMaxSeconds = 10f,
                spikeDumpShakeMovesMin = 3,
                spikeDumpShakeMovesMax = 6,
                spikeDumpShakeDurationMinSeconds = 4f,
                spikeDumpShakeDurationMaxSeconds = 12f,
                spikeDumpShakeMovePercentMin = 0.07f,
                spikeDumpShakeMovePercentMax = 0.24f,
                spikeDumpNoiseStrength = 0.110f,
                spikeDumpMaxPriceChangePerTick01 = 0.14f,
                dipPumpDipPercentMin = 0.26f,
                dipPumpDipPercentMax = 0.66f,
                dipPumpDipDurationMinSeconds = 18f,
                dipPumpDipDurationMaxSeconds = 62f,
                dipPumpPumpOverDipMin = 1.45f,
                dipPumpPumpOverDipMax = 2.55f,
                dipPumpMaxPumpPercent = 1.75f,
                dipPumpPumpDurationMinSeconds = 4f,
                dipPumpPumpDurationMaxSeconds = 12f,
                dipPumpShakeMovesMin = 3,
                dipPumpShakeMovesMax = 6,
                dipPumpShakeDurationMinSeconds = 4f,
                dipPumpShakeDurationMaxSeconds = 12f,
                dipPumpShakeMovePercentMin = 0.07f,
                dipPumpShakeMovePercentMax = 0.24f,
                dipPumpNoiseStrength = 0.110f,
                dipPumpMaxPriceChangePerTick01 = 0.14f,
                deadFlatRocketFlatDurationMinSeconds = 24f,
                deadFlatRocketFlatDurationMaxSeconds = 70f,
                deadFlatRocketFlatRange01 = 0.020f,
                deadFlatRocketNoiseStrength = 0.018f,
                deadFlatRocketPumpPercentMin = 0.55f,
                deadFlatRocketPumpPercentMax = 1.45f,
                deadFlatRocketPumpDurationMinSeconds = 4f,
                deadFlatRocketPumpDurationMaxSeconds = 11f,
                deadFlatRocketMaxPriceChangePerTick01 = 0.16f,
                deadFlatDumpFlatDurationMinSeconds = 18f,
                deadFlatDumpFlatDurationMaxSeconds = 58f,
                deadFlatDumpFlatRange01 = 0.020f,
                deadFlatDumpNoiseStrength = 0.018f,
                deadFlatDumpDropPercentMin = 0.32f,
                deadFlatDumpDropPercentMax = 0.76f,
                deadFlatDumpDurationMinSeconds = 4f,
                deadFlatDumpDurationMaxSeconds = 12f,
                deadFlatDumpMaxPriceChangePerTick01 = 0.16f,
                normalMaxPriceChangePerTick01 = 0.065f,
                pumpMaxPriceChangePerTick01 = 0.18f,
                crashMaxPriceChangePerTick01 = 0.30f,
                roundingRules = new[]
                {
                    new PriceRoundingRule { minPrice = 0f, step = 10f },
                    new PriceRoundingRule { minPrice = 10000f, step = 50f },
                    new PriceRoundingRule { minPrice = 50000f, step = 100f },
                    new PriceRoundingRule { minPrice = 200000f, step = 500f },
                    new PriceRoundingRule { minPrice = 500000f, step = 1000f },
                },
                crashFirstTriggerPrice = 8500000f,
                crashThresholdRandomMin = 10000000f,
                crashThresholdRandomMax = 42000000f,
                crashRecoverToInitialMultiplier = 0.98f,
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

        [Header("Low price recovery")]
        [SerializeField] private bool preventPlayablePriceCollapse = true;
        [Range(1f, 2f)]
        [SerializeField] private float lowPriceRecoveryTriggerMultiplier = 1.20f;
        [Range(1.05f, 4f)]
        [SerializeField] private float lowPriceRecoveryTargetMultiplier = 1.80f;
        [Range(0f, 1f)]
        [SerializeField] private float lowPriceRecoveryPullStrength = 0.35f;
        [Min(1f)]
        [SerializeField] private float lowPriceRecoveryDurationMinSeconds = 8f;
        [Min(1f)]
        [SerializeField] private float lowPriceRecoveryDurationMaxSeconds = 20f;

        [Header("Market growth")]
        [SerializeField] private bool useGlobalMarketGrowth = true;
        [SerializeField] private bool useRecommendedMarketPersonalityTuning = true;
        [Tooltip("Slow background growth that keeps trading relevant as the rest of the economy grows.")]
        [Range(0f, 0.08f)]
        [SerializeField] private float globalGrowthPerMinute01 = 0.026f;
        [Range(0f, 2f)]
        [SerializeField] private float shtGlobalGrowthMultiplier = 1.15f;
        [Range(0f, 2f)]
        [SerializeField] private float ethGlobalGrowthMultiplier = 1.12f;
        [Range(0f, 2f)]
        [SerializeField] private float btcGlobalGrowthMultiplier = 0.70f;
        [Range(0f, 1f)]
        [SerializeField] private float globalGrowthNoiseStrength = 0.20f;
        [Tooltip("Softly raises coin price corridors when business income grows, so trading remains worth attention later.")]
        [SerializeField] private bool scaleMarketWithBusinessIncome = true;
        [SerializeField, Min(0.5f)] private float economyTargetRefreshSeconds = 5f;
        [Min(0)]
        [SerializeField] private double businessIncomeIgnoredBelowRublesPerHour = 100_000d;
        [Range(0f, 0.95f)]
        [SerializeField] private float economyCorridorCatchUpPerMinute01 = 0.55f;
        [Range(0f, 3f)]
        [SerializeField] private float shtTargetExposureHours = 0.045f;
        [Range(0f, 3f)]
        [SerializeField] private float ethTargetExposureHours = 0.25f;
        [Range(0f, 3f)]
        [SerializeField] private float btcTargetExposureHours = 0.60f;

        [Header("SHT action assist")]
        [SerializeField] private bool useShtStarterAssist = true;
        [Tooltip("Keeps SHT punchy and rebound-heavy. When enabled, SHT keeps the same high-action personality after the early game.")]
        [SerializeField] private bool keepShtStarterAssistAlwaysActive = true;
        [Tooltip("Early SHT window for new players when always-active mode is disabled. Existing saves do not restart the timer.")]
        [Min(0f)]
        [SerializeField] private float shtStarterAssistDurationSeconds = 8f * 60f;
        [Min(0f)]
        [SerializeField] private float shtStarterAssistMinimumSeconds = 3f * 60f;
        [Min(1.01f)]
        [SerializeField] private float shtStarterAssistProfitExitMultiplier = 2.25f;
        [Range(0.1f, 1f)]
        [SerializeField] private float shtStarterAssistSoftFloorFromInitial = 0.78f;
        [Range(0f, 4f)]
        [SerializeField] private float shtStarterAssistReboundPerMinute01 = 2.20f;
        [Range(0f, 1f)]
        [SerializeField] private float shtStarterAssistDownMoveDamping = 0.45f;
        [Tooltip("Extra protection for the first minutes of a save: avoids the player buying SHT and being stuck with nothing to do.")]
        [SerializeField] private bool useEarlyPlayerShtAssist = true;
        [Min(0)]
        [SerializeField] private long earlyAssistRublesThreshold = 250_000;
        [Min(0)]
        [SerializeField] private double earlyAssistBusinessIncomeThresholdPerHour = 100_000d;
        [Range(0f, 1f)]
        [SerializeField] private float earlyAssistDownMoveDamping = 0.72f;
        [Range(0f, 5f)]
        [SerializeField] private float earlyAssistReboundMultiplier = 1.75f;

        [Header("Balance simulation")]
        [SerializeField, Min(1)] private int balanceSimulationRuns = 24;
        [SerializeField, Min(1f)] private float balanceSimulationMinutes = 60f;

        [Header("Runtime timing")]
        [SerializeField] private bool useRealtimeCatchUp = true;
        [Tooltip("Limits one resume catch-up so a tab left in background for hours does not freeze the browser.")]
        [SerializeField, Min(1f)] private float maxRealtimeCatchUpSeconds = 15f * 60f;

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
        private bool _balanceSimulationInProgress;
        private double _lastRealtimeUtcSeconds;
        private bool _resumeCatchUpPending;
        private float _economyTargetRefreshTimer;
        private double _cachedBusinessIncomePerHour;

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

                if (useRecommendedMarketPersonalityTuning)
                    ApplyRecommendedMarketPersonalityTuning(cfg);

                NormalizeConfig(cfg);

                var startPrice = ClampToPlayableFloor(cfg, cfg.initialPrice);
                if ((useCurrencyMarketPricesOnStart || loadedFromSave) && market != null)
                    startPrice = ClampToPlayableFloor(cfg, market.GetPrice(cfg.id));

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

            RefreshEconomyPriceTargets(force: true);
            SanitizeStoredHistoryFloors();
            WarmupHistoryIfNeeded();
            ActivateShtStarterAssistIfNeeded(loadedFromSave);

            ResetRealtimeClock();

            if (logMarketStatesOnStart)
                LogActiveMarketStates("Market simulation started");
        }

        private void ActivateShtStarterAssistIfNeeded(bool loadedFromSave)
        {
            if (!useShtStarterAssist || loadedFromSave || !_runtime.TryGetValue(CurrencyId.SHT, out var r))
                return;

            if (!ShouldStartShtStarterAssist(r.config, loadedFromSave))
                return;

            var startPrice = ClampToPlayableFloor(r.config, r.config.initialPrice);
            r.rawPrice = startPrice;
            r.visiblePrice = RoundPrice(r.config, startPrice);
            r.corridorAnchor = startPrice;
            r.starterAssistElapsed = 0f;
            r.starterAssistTimeLeft = Mathf.Max(0f, shtStarterAssistDurationSeconds);
            RebuildCorridor(r, r.rawPrice, force: true);
            BuildInitialPlan(r);
            ApplyNextPlannedState(r);

            if (writeToCurrencyMarket && market != null)
                market.SetPrice(r.config.id, r.visiblePrice);

            if (priceHistoryStore != null)
                priceHistoryStore.Push(r.config.id, r.visiblePrice);
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

        private void SanitizeStoredHistoryFloors()
        {
            if (priceHistoryStore == null)
                return;

            foreach (var kv in _runtime)
            {
                var id = kv.Key;
                var r = kv.Value;
                if (!priceHistoryStore.TryGet(id, out var prices))
                    continue;

                var changed = false;
                var sanitized = new List<float>(prices.Count);
                for (var i = 0; i < prices.Count; i++)
                {
                    var price = ClampToPlayableFloor(r.config, prices[i]);
                    if (!Mathf.Approximately(price, prices[i]))
                        changed = true;
                    sanitized.Add(price);
                }

                if (changed)
                    priceHistoryStore.SetAll(id, sanitized);
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

                price = CurrencyMarket.SanitizePrice(startPrices[i].price);
                return true;
            }

            price = 0f;
            return false;
        }

        private bool ShouldStartShtStarterAssist(CoinSimulationConfig cfg, bool loadedFromSave)
        {
            return useShtStarterAssist
                && !loadedFromSave
                && cfg != null
                && cfg.id == CurrencyId.SHT
                && shtStarterAssistDurationSeconds > 0f;
        }

        private void Update()
        {
            var dt = GetRuntimeDeltaSeconds();
            if (dt <= 0f)
                return;

            RefreshEconomyPriceTargets(dt);
            foreach (var kv in _runtime)
                AdvanceRuntime(kv.Value, dt);
        }

        public void RefreshEconomyScaleNow()
        {
            RefreshEconomyPriceTargets(force: true);
        }

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

            if (!useRealtimeCatchUp)
                return Time.deltaTime;

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

            return Mathf.Min((float)elapsed, maxRealtimeCatchUpSeconds);
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

        public void FlushPendingTicks()
        {
            foreach (var kv in _runtime)
                AdvanceRuntime(kv.Value, 0f);
        }

        private void AdvanceRuntime(CoinRuntime r, float dt)
        {
            if (r == null)
                return;

            r.tickTimer += Mathf.Max(0f, dt);
            r.time += Mathf.Max(0f, dt);

            var interval = GetEffectiveTickInterval(r);
            while (r.tickTimer >= interval)
            {
                r.tickTimer -= interval;
                Tick(r, interval);
                interval = GetEffectiveTickInterval(r);
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

            if (r.currentState != MarketStateType.BusinessSkillPumpDump)
            {
                ForceLowPriceRecoveryIfNeeded(r);
                ArmCrashIfNeeded(r);
                ForceCrashIfThresholdReached(r);
            }

            ApplyEconomyCorridorShift(r, dt);

            var nextRaw = SimulateNextRawPrice(r, dt);
            nextRaw = ApplyShtStarterAssist(r, nextRaw, dt);
            nextRaw = ApplyGlobalMarketGrowth(r, nextRaw, dt);
            nextRaw = ApplyHighPriceCooling(r, nextRaw, dt);
            nextRaw = ApplyEconomyCorridorFloorPressure(r, nextRaw, dt);
            nextRaw = ClampToPlayableFloor(r.config, nextRaw);
            nextRaw = ApplyLowPriceRecovery(r, nextRaw);
            nextRaw = LimitPriceChangePerTick(r, nextRaw);
            nextRaw = ClampToPlayableFloor(r.config, nextRaw);

            r.rawPrice = nextRaw;
            r.visiblePrice = RoundPrice(r.config, r.rawPrice);

            AdvanceShtStarterAssist(r, dt);

            if (_warmupInProgress)
                return;

            if (writeToCurrencyMarket && market != null)
                market.SetPrice(r.config.id, r.visiblePrice);

            if (priceHistoryStore != null)
                priceHistoryStore.Push(r.config.id, r.visiblePrice);
        }

        private void ForceLowPriceRecoveryIfNeeded(CoinRuntime r)
        {
            if (!preventPlayablePriceCollapse || IsDebugStateOverrideTarget(r))
                return;

            if (r.currentState == MarketStateType.BusinessSkillPumpDump || IsLowPriceRecoveryState(r.currentState))
                return;

            var floor = GetPlayablePriceFloor(r.config);
            var trigger = floor * Mathf.Max(1f, lowPriceRecoveryTriggerMultiplier);
            if (r.rawPrice > trigger && r.visiblePrice > trigger)
                return;

            r.crashArmed = false;
            r.crashThresholdX = 0f;

            var minDuration = Mathf.Max(1f, lowPriceRecoveryDurationMinSeconds);
            var maxDuration = Mathf.Max(minDuration, lowPriceRecoveryDurationMaxSeconds);
            EnterState(
                r,
                MarketStateType.LongUptrend,
                UnityEngine.Random.Range(minDuration, maxDuration),
                changeCorridor: true);

            r.corridorAnchor = Mathf.Max(r.corridorAnchor, floor);
            RebuildCorridor(r, r.rawPrice, force: false);
        }

        private float ApplyLowPriceRecovery(CoinRuntime r, float nextRaw)
        {
            var floor = GetPlayablePriceFloor(r.config);
            nextRaw = Mathf.Max(nextRaw, floor);

            if (!preventPlayablePriceCollapse)
                return nextRaw;

            var trigger = floor * Mathf.Max(1f, lowPriceRecoveryTriggerMultiplier);
            var lowZonePrice = Mathf.Min(r.rawPrice, nextRaw);
            if (lowZonePrice > trigger)
                return nextRaw;

            var target = GetLowPriceRecoveryTarget(r.config);
            var depth = Mathf.InverseLerp(trigger, floor, lowZonePrice);
            var strength = Mathf.Clamp01(lowPriceRecoveryPullStrength) * Mathf.Lerp(0.35f, 1f, depth);

            nextRaw = Mathf.Lerp(nextRaw, Mathf.Max(nextRaw, target), strength);
            r.corridorAnchor = Mathf.Lerp(
                Mathf.Max(r.corridorAnchor, floor),
                Mathf.Max(r.corridorAnchor, target),
                strength * 0.4f);
            RebuildCorridor(r, nextRaw, force: false);

            return Mathf.Max(nextRaw, floor);
        }

        private float ApplyGlobalMarketGrowth(CoinRuntime r, float nextRaw, float dt)
        {
            if (!useGlobalMarketGrowth || dt <= 0f)
                return nextRaw;

            if (r.currentState == MarketStateType.BusinessSkillPumpDump)
                return nextRaw;

            var perMinute = Mathf.Max(0f, globalGrowthPerMinute01) * GetGlobalGrowthCoinMultiplier(r.config.id);
            if (perMinute <= 0f)
                return nextRaw;

            var stateMultiplier = GetGlobalGrowthStateMultiplier(r.currentState);
            if (stateMultiplier <= 0f)
                return nextRaw;

            var perTick01 = Mathf.Pow(1f + perMinute, dt / 60f) - 1f;
            var noise = Mathf.PerlinNoise((r.seed * 0.011f) + r.time * 0.08f, 0.64f) * 2f - 1f;
            var noiseMultiplier = Mathf.Clamp(1f + noise * globalGrowthNoiseStrength, 0.35f, 1.65f);
            var drift = r.rawPrice * perTick01 * stateMultiplier * noiseMultiplier;

            if (drift <= 0f)
                return nextRaw;

            r.corridorAnchor = Mathf.Max(GetPlayablePriceFloor(r.config), r.corridorAnchor + drift * 0.65f);
            return nextRaw + drift;
        }

        private void RefreshEconomyPriceTargets(float dt = 0f, bool force = false)
        {
            if (!scaleMarketWithBusinessIncome)
            {
                _cachedBusinessIncomePerHour = 0d;
                foreach (var kv in _runtime)
                    kv.Value.economyPriceTarget = 0f;
                return;
            }

            _economyTargetRefreshTimer -= Mathf.Max(0f, dt);
            if (!force && _economyTargetRefreshTimer > 0f)
                return;

            _economyTargetRefreshTimer = Mathf.Max(0.5f, economyTargetRefreshSeconds);
            ResolveEconomyLinksIfNeeded();
            _cachedBusinessIncomePerHour = businessProgressLink != null
                ? Math.Max(0d, businessProgressLink.GetTotalEffectiveIncomePerHour())
                : 0d;

            foreach (var kv in _runtime)
            {
                var r = kv.Value;
                r.economyPriceTarget = CalculateEconomyPriceTarget(r.config, _cachedBusinessIncomePerHour);

                if (IsBelowEconomyTargetZone(r))
                    QueueEconomyRecoveryState(r);
            }
        }

        private void ResolveEconomyLinksIfNeeded()
        {
            if (playerProfile == null)
                playerProfile = FindAnyObjectByType<PlayerProfile>(FindObjectsInactive.Include);
            if (businessProgressLink == null)
                businessProgressLink = FindAnyObjectByType<BusinessController>(FindObjectsInactive.Include);
        }

        private float CalculateEconomyPriceTarget(CoinSimulationConfig cfg, double incomePerHour)
        {
            if (cfg == null || incomePerHour <= Math.Max(0d, businessIncomeIgnoredBelowRublesPerHour))
                return 0f;

            var cap = playerProfile != null ? Math.Max(1, playerProfile.GetCap(cfg.id)) : GetDefaultTradingCap(cfg.id);
            var capDivisor = GetEconomyTargetCapDivisor(cfg.id, cap);
            var exposureHours = GetBusinessTargetExposureHours(cfg.id);
            if (exposureHours <= 0f || capDivisor <= 0d)
                return 0f;

            var target = incomePerHour * exposureHours / capDivisor;
            if (double.IsNaN(target) || double.IsInfinity(target) || target <= 0d)
                return 0f;

            var targetCeiling = GetEconomyTargetPriceCeiling(cfg);
            if (targetCeiling > 0f)
                target = Math.Min(target, targetCeiling);

            return ClampToPlayableFloor(cfg, (float)Math.Min(target, float.MaxValue));
        }

        private static float GetEconomyTargetPriceCeiling(CoinSimulationConfig cfg)
        {
            if (cfg == null)
                return 0f;

            var multiplier = cfg.id switch
            {
                CurrencyId.SHT => 14f,
                CurrencyId.ETH => 18f,
                CurrencyId.BTC => 8f,
                _ => 32f,
            };

            return Mathf.Max(0f, cfg.initialPrice * multiplier);
        }

        private static double GetEconomyTargetCapDivisor(CurrencyId id, int currentCap)
        {
            var baseCap = Math.Max(1, GetDefaultTradingCap(id));
            var cap = Math.Max(1, currentCap);
            return Math.Sqrt(baseCap * cap);
        }

        private void ApplyEconomyCorridorShift(CoinRuntime r, float dt)
        {
            if (!scaleMarketWithBusinessIncome || r.economyPriceTarget <= 0f || dt <= 0f)
                return;

            var target = Mathf.Max(GetPlayablePriceFloor(r.config), r.economyPriceTarget);
            var catchUp = 1f - Mathf.Pow(
                1f - Mathf.Clamp01(economyCorridorCatchUpPerMinute01),
                dt / 60f);
            catchUp *= GetEconomyCorridorStateMultiplier(r.currentState);
            catchUp *= GetEconomyCorridorCoinMultiplier(r.config.id);

            if (target < r.corridorAnchor)
                catchUp *= 0.35f;

            r.corridorAnchor = Mathf.Lerp(r.corridorAnchor, target, Mathf.Clamp01(catchUp));
            RebuildCorridor(r, r.rawPrice, force: false);
        }

        private static float ApplyEconomyCorridorFloorPressure(CoinRuntime r, float nextRaw, float dt)
        {
            if (r.config == null || r.economyPriceTarget <= 0f || dt <= 0f)
                return nextRaw;

            var floor = GetEconomyCorridorLowFloor(r);
            if (floor <= 0f || nextRaw >= floor)
                return nextRaw;

            var depth = Mathf.Clamp01(floor / Mathf.Max(1f, nextRaw) - 1f);
            var pullPerSecond = r.config.id switch
            {
                CurrencyId.SHT => 0.95f,
                CurrencyId.ETH => 0.80f,
                CurrencyId.BTC => 0.55f,
                _ => 0.50f,
            };
            var strength = Mathf.Clamp01(pullPerSecond * dt * Mathf.Lerp(0.35f, 1f, depth));
            return Mathf.Lerp(nextRaw, floor, strength);
        }

        private static float GetEconomyCorridorStateMultiplier(MarketStateType state)
        {
            return state switch
            {
                MarketStateType.MarketCrash => 0.16f,
                MarketStateType.LongDowntrend => 0.35f,
                MarketStateType.StairDown => 0.35f,
                MarketStateType.SlowUpThenDump => 0.42f,
                MarketStateType.DeadFlatThenRocketDump => 0.28f,
                MarketStateType.BusinessSkillPumpDump => 0f,
                MarketStateType.LowRangeRecovery => 1.55f,
                MarketStateType.StairUp => 1.30f,
                MarketStateType.LongUptrend => 1.22f,
                MarketStateType.AccumulationRun => 1.25f,
                _ => 1f,
            };
        }

        private static float GetEconomyCorridorCoinMultiplier(CurrencyId id)
        {
            return id switch
            {
                CurrencyId.SHT => 0.45f,
                CurrencyId.ETH => 0.75f,
                CurrencyId.BTC => 0.60f,
                _ => 1f,
            };
        }

        private void QueueEconomyRecoveryState(CoinRuntime r)
        {
            if (r.currentState == MarketStateType.BusinessSkillPumpDump || IsEconomyRecoveryState(r.currentState))
                return;

            if (r.plan.Count > 0 && IsEconomyRecoveryState(r.plan.Peek().type))
            {
                r.stateTimeLeft = Mathf.Min(r.stateTimeLeft, GetEconomyRecoverySwitchDelay(r.config.id));
                return;
            }

            var nextType = PickEconomyRecoveryState(r);
            var planned = new PlannedState
            {
                type = nextType,
                durationSeconds = PickStateDuration(r.config, nextType),
            };

            var existing = r.plan.ToArray();
            r.plan.Clear();
            r.plan.Enqueue(planned);
            for (var i = 0; i < existing.Length && r.plan.Count < Mathf.Max(1, r.config.plannedStatesCount); i++)
                r.plan.Enqueue(existing[i]);

            r.stateTimeLeft = Mathf.Min(r.stateTimeLeft, GetEconomyRecoverySwitchDelay(r.config.id));
        }

        private static float GetEconomyRecoverySwitchDelay(CurrencyId id)
        {
            return id switch
            {
                CurrencyId.SHT => 4f,
                CurrencyId.ETH => 7f,
                CurrencyId.BTC => 12f,
                _ => 8f,
            };
        }

        private bool IsBelowEconomyTargetZone(CoinRuntime r)
        {
            if (r == null || r.economyPriceTarget <= 0f)
                return false;

            return r.rawPrice < r.economyPriceTarget * GetEconomyUndervalueThreshold(r.config.id);
        }

        private static float GetEconomyUndervalueThreshold(CurrencyId id)
        {
            return id switch
            {
                CurrencyId.SHT => 0.50f,
                CurrencyId.ETH => 0.62f,
                CurrencyId.BTC => 0.54f,
                _ => 0.60f,
            };
        }

        private static bool IsEconomyRecoveryState(MarketStateType state)
        {
            return state is MarketStateType.LowRangeRecovery
                or MarketStateType.StairUp
                or MarketStateType.LongUptrend
                or MarketStateType.CompressionBreakout
                or MarketStateType.AccumulationRun;
        }

        private static MarketStateType PickEconomyRecoveryState(CoinRuntime r)
        {
            return r.config.id switch
            {
                CurrencyId.SHT => PickWeightedChoice(
                    new StateChoice(MarketStateType.LowRangeRecovery, 0.85f),
                    new StateChoice(MarketStateType.StairUp, 0.65f),
                    new StateChoice(MarketStateType.CompressionBreakout, 0.35f),
                    new StateChoice(MarketStateType.ChopInCorridor, 0.65f)),
                CurrencyId.ETH => PickWeightedChoice(
                    new StateChoice(MarketStateType.LowRangeRecovery, 1.10f),
                    new StateChoice(MarketStateType.StairUp, 0.95f),
                    new StateChoice(MarketStateType.CompressionBreakout, 0.70f),
                    new StateChoice(MarketStateType.ChopInCorridor, 0.30f)),
                CurrencyId.BTC => PickWeightedChoice(
                    new StateChoice(MarketStateType.AccumulationRun, 1.25f),
                    new StateChoice(MarketStateType.LowRangeRecovery, 0.95f),
                    new StateChoice(MarketStateType.StairUp, 0.70f),
                    new StateChoice(MarketStateType.ChopInCorridor, 0.25f)),
                _ => MarketStateType.LowRangeRecovery,
            };
        }

        private float GetBusinessTargetExposureHours(CurrencyId id)
        {
            return id switch
            {
                CurrencyId.SHT => shtTargetExposureHours,
                CurrencyId.ETH => ethTargetExposureHours,
                CurrencyId.BTC => btcTargetExposureHours,
                _ => ethTargetExposureHours,
            };
        }

        private static int GetDefaultTradingCap(CurrencyId id)
        {
            return id switch
            {
                CurrencyId.SHT => 500,
                CurrencyId.ETH => 150,
                CurrencyId.BTC => 50,
                _ => 100,
            };
        }

        private float GetGlobalGrowthCoinMultiplier(CurrencyId id)
        {
            return id switch
            {
                CurrencyId.SHT => shtGlobalGrowthMultiplier,
                CurrencyId.ETH => ethGlobalGrowthMultiplier,
                CurrencyId.BTC => btcGlobalGrowthMultiplier,
                _ => 1f,
            };
        }

        private static float GetGlobalGrowthStateMultiplier(MarketStateType state)
        {
            return state switch
            {
                MarketStateType.MarketCrash => 0f,
                MarketStateType.LongDowntrend => 0.18f,
                MarketStateType.StairDown => 0.18f,
                MarketStateType.SlowUpThenDump => 0.22f,
                MarketStateType.DeadFlatThenRocketDump => 0.12f,
                MarketStateType.LowRangeRecovery => 0.35f,
                MarketStateType.LongUptrend => 1.18f,
                MarketStateType.StairUp => 1.15f,
                MarketStateType.AccumulationRun => 1.28f,
                MarketStateType.DeadFlatThenRocketPump => 1.12f,
                MarketStateType.CompressionBreakout => 0.75f,
                _ => 0.85f,
            };
        }

        private static float ApplyHighPriceCooling(CoinRuntime r, float nextRaw, float dt)
        {
            if (r.currentState == MarketStateType.BusinessSkillPumpDump || dt <= 0f)
                return nextRaw;

            var ceiling = GetUpwardMoveCeiling(r);
            if (nextRaw <= ceiling)
                return nextRaw;

            var excess = Mathf.Max(0f, nextRaw / Mathf.Max(1f, ceiling) - 1f);
            var baseCooling = r.config.id switch
            {
                CurrencyId.SHT => 0.20f,
                CurrencyId.ETH => 0.12f,
                CurrencyId.BTC => 0.035f,
                _ => 0.10f,
            };
            var strength = Mathf.Clamp01(baseCooling * dt * (1f + excess * 2f));
            r.corridorAnchor = Mathf.Lerp(r.corridorAnchor, ceiling, strength * 0.75f);
            return Mathf.Lerp(nextRaw, ceiling, strength);
        }

        private float ApplyShtStarterAssist(CoinRuntime r, float nextRaw, float dt)
        {
            if (!IsShtStarterAssistActive(r) || dt <= 0f)
                return nextRaw;

            var cfg = r.config;
            var startPrice = Mathf.Max(GetPlayablePriceFloor(cfg), cfg.initialPrice);
            var softFloor = Mathf.Max(GetPlayablePriceFloor(cfg), startPrice * shtStarterAssistSoftFloorFromInitial);
            var protectedNext = nextRaw;
            var earlyAssist = IsEarlyPlayerShtAssistActive();
            var downMoveDamping = earlyAssist
                ? Mathf.Max(shtStarterAssistDownMoveDamping, earlyAssistDownMoveDamping)
                : shtStarterAssistDownMoveDamping;

            if (IsNegativeStarterAssistState(r.currentState) && protectedNext < r.rawPrice)
                protectedNext = Mathf.Lerp(protectedNext, r.rawPrice, downMoveDamping);

            if (protectedNext < softFloor)
            {
                var depth = Mathf.InverseLerp(softFloor, GetPlayablePriceFloor(cfg), protectedNext);
                var strength = Mathf.Lerp(0.35f, 0.75f, depth);
                protectedNext = Mathf.Lerp(protectedNext, softFloor, strength);
            }

            var lowReference = Mathf.Min(r.rawPrice, protectedNext);
            var drawdown01 = Mathf.Clamp01((startPrice - lowReference) / startPrice);
            if (drawdown01 > 0.05f)
            {
                var reboundPower = Mathf.InverseLerp(0.05f, 0.28f, drawdown01);
                var reboundMultiplier = earlyAssist ? Mathf.Max(1f, earlyAssistReboundMultiplier) : 1f;
                var perTick01 = Mathf.Max(0f, shtStarterAssistReboundPerMinute01) * reboundMultiplier * dt / 60f;
                protectedNext += startPrice * perTick01 * reboundPower;
            }

            if (protectedNext > nextRaw)
            {
                r.corridorAnchor = Mathf.Max(r.corridorAnchor, Mathf.Lerp(r.corridorAnchor, protectedNext, 0.35f));
                RebuildCorridor(r, protectedNext, force: false);
            }

            return protectedNext;
        }

        private bool IsEarlyPlayerShtAssistActive()
        {
            if (!useEarlyPlayerShtAssist)
                return false;

            var rubles = playerProfile != null ? playerProfile.Rubles : long.MaxValue;
            if (rubles > Math.Max(0, earlyAssistRublesThreshold))
                return false;

            return _cachedBusinessIncomePerHour <= Math.Max(0d, earlyAssistBusinessIncomeThresholdPerHour);
        }

        private void AdvanceShtStarterAssist(CoinRuntime r, float dt)
        {
            if (!IsShtStarterAssistActive(r) || dt <= 0f || _warmupInProgress)
                return;

            if (keepShtStarterAssistAlwaysActive)
                return;

            r.starterAssistElapsed += dt;

            var minDuration = Mathf.Max(0f, shtStarterAssistMinimumSeconds);
            var canExitAfterProfit = r.starterAssistElapsed >= minDuration
                && r.rawPrice >= Mathf.Max(GetPlayablePriceFloor(r.config), r.config.initialPrice) * shtStarterAssistProfitExitMultiplier;

            if (canExitAfterProfit)
                r.starterAssistTimeLeft = Mathf.Min(r.starterAssistTimeLeft, 60f);

            r.starterAssistTimeLeft = Mathf.Max(0f, r.starterAssistTimeLeft - dt);
        }

        private bool IsShtStarterAssistActive(CoinRuntime r)
        {
            return useShtStarterAssist
                && r != null
                && r.config != null
                && r.config.id == CurrencyId.SHT
                && (keepShtStarterAssistAlwaysActive || r.starterAssistTimeLeft > 0f);
        }

        private static bool IsNegativeStarterAssistState(MarketStateType state)
        {
            return state == MarketStateType.LongDowntrend
                || state == MarketStateType.StairDown
                || state == MarketStateType.SlowUpThenDump
                || state == MarketStateType.DeadFlatThenRocketDump
                || state == MarketStateType.MarketCrash;
        }

        private float GetLowPriceRecoveryTarget(CoinSimulationConfig cfg)
        {
            return ClampToPlayableFloor(
                cfg,
                GetPlayablePriceFloor(cfg) * Mathf.Max(1.05f, lowPriceRecoveryTargetMultiplier));
        }

        private static bool IsLowPriceRecoveryState(MarketStateType state)
        {
            return state == MarketStateType.LongUptrend
                || state == MarketStateType.StairUp
                || state == MarketStateType.LowRangeRecovery
                || state == MarketStateType.DeadFlatThenRocketPump;
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
                case MarketStateType.BusinessSkillPumpDump:
                    return SimulateBusinessSkillPumpDump(r, dt, noise, range);
                case MarketStateType.StairUp:
                case MarketStateType.StairDown:
                case MarketStateType.CompressionBreakout:
                case MarketStateType.LowRangeRecovery:
                case MarketStateType.AccumulationRun:
                    return SimulatePatternState(r, dt, noise, range);
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
            var canFakeout = r.chopPhaseDuration >= GetMinimumChopFakeoutPhaseDuration(r.config);
            if (!r.chopFakeoutUsed && canFakeout && elapsed >= r.chopFakeoutStartTime)
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

            var highVolatility = r.config.noiseStrength >= 0.60f;
            var volatilityBoost = highVolatility ? 1.35f : 1f;
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
            var chanceTowardTarget = fakeoutActive ? 0.18f : highVolatility ? 0.58f : 0.68f;
            var randomDirection = UnityEngine.Random.value <= chanceTowardTarget ? targetDirection : -targetDirection;
            var randomMagnitude = UnityEngine.Random.Range(0.45f, highVolatility ? 1.90f : 1.35f);
            var noisyDelta = randomDirection * range * r.config.noiseStrength * 0.30f * volatilityBoost * dt * randomMagnitude;

            // Smooth noise prevents the random walk from looking like pure coin flips.
            noisyDelta += noise * range * r.config.noiseStrength * (highVolatility ? 0.18f : 0.12f) * dt;

            if (fakeoutActive)
            {
                // Fakeout: several ticks against the target, then the boost above pulls it back.
                guidedDelta = 0f;
                noisyDelta = effectiveDirection * range * r.config.trendStrength * r.config.chopFakeoutStrength * volatilityBoost * dt;
                noisyDelta += noise * range * r.config.noiseStrength * (highVolatility ? 0.14f : 0.08f) * dt;
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
            noisyDelta += BuildBoundaryEscapeDelta(r, range, cfg.calmNoiseStrength, dt);

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

        private static float BuildBoundaryEscapeDelta(CoinRuntime r, float range, float strength, float dt)
        {
            if (range <= 0f || strength <= 0f || dt <= 0f)
                return 0f;

            var pos = Mathf.InverseLerp(r.corridorLow, r.corridorHigh, r.rawPrice);
            var edgePower = 0f;
            var direction = 0f;

            if (pos > 0.82f)
            {
                edgePower = Mathf.InverseLerp(0.82f, 1f, pos);
                direction = -1f;
            }
            else if (pos < 0.18f)
            {
                edgePower = Mathf.InverseLerp(0.18f, 0f, pos);
                direction = 1f;
            }

            if (edgePower <= 0f)
                return 0f;

            var jitter = UnityEngine.Random.Range(0.55f, 1.35f);
            return direction * range * strength * Mathf.Lerp(0.25f, 0.75f, edgePower) * dt * jitter;
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
            var impulse = BuildTickImpulse(r, range, maxCatchupDelta, trendDirection: 1);

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

            var next = r.rawPrice + guidedDelta + smoothNoise + microNoise + wobble + impulse + pullback + outsidePull * r.stateCorridorPullStrength;
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

        private static float BuildTickImpulse(CoinRuntime r, float range, float maxCatchupDelta, int trendDirection)
        {
            var coinMultiplier = r.config.id switch
            {
                CurrencyId.SHT => 1f,
                CurrencyId.ETH => 0.42f,
                CurrencyId.BTC => 0.30f,
                _ => 0f,
            };

            if (coinMultiplier <= 0f)
                return 0f;

            var baseChance = r.currentState switch
            {
                MarketStateType.LongUptrend => 0.32f,
                MarketStateType.LongDowntrend => 0.34f,
                MarketStateType.StairUp => 0.38f,
                MarketStateType.StairDown => 0.40f,
                MarketStateType.CompressionBreakout => 0.30f,
                MarketStateType.LowRangeRecovery => 0.36f,
                _ => 0.26f,
            };
            var chance = baseChance * (r.config.id switch
            {
                CurrencyId.SHT => 1f,
                CurrencyId.ETH => 1.05f,
                CurrencyId.BTC => 0.58f,
                _ => 0f,
            });

            if (UnityEngine.Random.value > chance)
                return 0f;

            var step = GetRoundingStep(r.config, r.rawPrice);
            var stepMove = step * (r.config.id switch
            {
                CurrencyId.SHT => 2.0f,
                CurrencyId.ETH => 1.3f,
                CurrencyId.BTC => 1.1f,
                _ => 1f,
            });
            var baseMove = Mathf.Max(stepMove, maxCatchupDelta * UnityEngine.Random.Range(0.65f, 1.90f));
            var rangeMove = Mathf.Max(0f, range) * UnityEngine.Random.Range(0.006f, 0.020f) * coinMultiplier;
            var power = Mathf.Max(baseMove * coinMultiplier, rangeMove);

            var againstTrendChance = r.currentState switch
            {
                MarketStateType.CompressionBreakout => 0.46f,
                MarketStateType.LowRangeRecovery => 0.58f,
                _ => 0.62f,
            };
            var direction = UnityEngine.Random.value < againstTrendChance ? -trendDirection : trendDirection;

            var largeImpulseChance = r.config.id switch
            {
                CurrencyId.SHT => 0.14f,
                CurrencyId.ETH => 0.14f,
                CurrencyId.BTC => 0.055f,
                _ => 0f,
            };
            if (UnityEngine.Random.value < largeImpulseChance)
                power *= UnityEngine.Random.Range(1.45f, 2.45f);

            return direction * power;
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
            r.longUptrendTargetPrice = ApplyUpwardMovePressure(r, r.longUptrendTargetPrice);

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

            var impulse = BuildTickImpulse(r, range, maxCatchupDelta, trendDirection: -1);
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

            var next = r.rawPrice + guidedDelta + smoothNoise + microNoise + impulse + rally + outsidePull * r.stateCorridorPullStrength;

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
                GetPlayablePriceFloor(cfg),
                r.corridorAnchor / divider);

            var previousAnchor = r.corridorAnchor;
            r.corridorAnchor = r.longDowntrendTargetAnchor;
            RebuildCorridor(r, r.rawPrice, force: false);
            r.longDowntrendTargetPrice = Mathf.Lerp(
                r.corridorLow,
                r.corridorHigh,
                cfg.longDowntrendTargetCorridorPos);
            r.corridorAnchor = previousAnchor;

            var minTarget = GetPlayablePriceFloor(cfg);
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
            var curve = SmoothStep01(progress);
            var scheduledPrice = Mathf.Lerp(r.spikeDumpPhaseStartPrice, r.spikeDumpTargetPrice, curve);

            var phaseDistance = Mathf.Abs(r.spikeDumpTargetPrice - r.spikeDumpPhaseStartPrice);
            var maxCatchupDelta = phaseDistance
                / Mathf.Max(0.5f, r.spikeDumpPhaseDuration)
                * dt
                * (r.spikeDumpPhase == SpikeDumpPhase.Dump ? 1.65f : 1.25f);

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
            r.spikeDumpTargetPrice = ApplyUpwardMovePressure(
                r,
                r.spikeDumpPhaseStartPrice * (1f + r.spikeDumpGrowPercent));
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
            var minPrice = GetPlayablePriceFloor(cfg);

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
            var minPrice = GetPlayablePriceFloor(cfg);

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
            var curve = SmoothStep01(progress);
            var scheduledPrice = Mathf.Lerp(r.dipPumpPhaseStartPrice, r.dipPumpTargetPrice, curve);

            var phaseDistance = Mathf.Abs(r.dipPumpTargetPrice - r.dipPumpPhaseStartPrice);
            var maxCatchupDelta = phaseDistance
                / Mathf.Max(0.5f, r.dipPumpPhaseDuration)
                * dt
                * (r.dipPumpPhase == DipPumpPhase.Pump ? 1.65f : 1.25f);

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
            var minPrice = GetPlayablePriceFloor(cfg);
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
            r.dipPumpTargetPrice = ApplyUpwardMovePressure(
                r,
                r.dipPumpPhaseStartPrice * (1f + pumpPercent));
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
            var minPrice = GetPlayablePriceFloor(cfg);

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
                var flatProgress = 1f - Mathf.Clamp01(
                    r.deadFlatRocketFlatTimeLeft / Mathf.Max(0.0001f, r.deadFlatRocketFlatDuration));
                var compression = Mathf.Lerp(1.75f, 0.18f, SmoothStep01(flatProgress));
                var flatNoiseA = Mathf.PerlinNoise((r.seed * 0.0061f) + r.time * 0.18f, 0.17f) * 2f - 1f;
                var flatNoiseB = Mathf.PerlinNoise((r.seed * 0.0073f) + r.time * 0.55f, 0.41f) * 2f - 1f;
                var flatNoise = flatNoiseA * 0.75f + flatNoiseB * 0.25f;
                var flatTarget = r.deadFlatRocketBasePrice
                    * (1f + flatNoise * cfg.deadFlatRocketFlatRange01 * compression);
                var flatPull = (flatTarget - r.rawPrice) * Mathf.Lerp(0.16f, 0.34f, flatProgress);
                var micro = noise * range * cfg.deadFlatRocketNoiseStrength * compression * dt;
                micro += BuildBoundaryEscapeDelta(r, range, cfg.deadFlatRocketNoiseStrength * compression, dt);
                var nextFlat = r.rawPrice + flatPull + micro;

                var clampRange = cfg.deadFlatRocketFlatRange01 * compression;
                var minFlat = r.deadFlatRocketBasePrice * (1f - clampRange);
                var maxFlat = r.deadFlatRocketBasePrice * (1f + clampRange);
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
            var curve = SmoothStep01(progress);
            var scheduledPrice = Mathf.Lerp(r.deadFlatRocketPumpStartPrice, r.deadFlatRocketTargetPrice, curve);

            var distance = Mathf.Abs(r.deadFlatRocketTargetPrice - r.deadFlatRocketPumpStartPrice);
            var maxCatchupDelta = distance / Mathf.Max(0.25f, r.deadFlatRocketPumpDuration) * dt * 1.85f;
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
            r.deadFlatRocketFlatDuration = UnityEngine.Random.Range(
                cfg.deadFlatRocketFlatDurationMinSeconds,
                cfg.deadFlatRocketFlatDurationMaxSeconds);
            r.deadFlatRocketFlatTimeLeft = r.deadFlatRocketFlatDuration;
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
            r.deadFlatRocketTargetPrice = ApplyUpwardMovePressure(
                r,
                r.deadFlatRocketPumpStartPrice * (1f + pumpPercent));
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
            r.deadFlatRocketFlatDuration = 0f;
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
                var flatProgress = 1f - Mathf.Clamp01(
                    r.deadFlatDumpFlatTimeLeft / Mathf.Max(0.0001f, r.deadFlatDumpFlatDuration));
                var compression = Mathf.Lerp(1.75f, 0.18f, SmoothStep01(flatProgress));
                var flatNoiseA = Mathf.PerlinNoise((r.seed * 0.0067f) + r.time * 0.18f, 0.23f) * 2f - 1f;
                var flatNoiseB = Mathf.PerlinNoise((r.seed * 0.0079f) + r.time * 0.55f, 0.49f) * 2f - 1f;
                var flatNoise = flatNoiseA * 0.75f + flatNoiseB * 0.25f;
                var flatTarget = r.deadFlatDumpBasePrice
                    * (1f + flatNoise * cfg.deadFlatDumpFlatRange01 * compression);
                var flatPull = (flatTarget - r.rawPrice) * Mathf.Lerp(0.16f, 0.34f, flatProgress);
                var micro = noise * range * cfg.deadFlatDumpNoiseStrength * compression * dt;
                micro += BuildBoundaryEscapeDelta(r, range, cfg.deadFlatDumpNoiseStrength * compression, dt);
                var nextFlat = r.rawPrice + flatPull + micro;

                var clampRange = cfg.deadFlatDumpFlatRange01 * compression;
                var minFlat = r.deadFlatDumpBasePrice * (1f - clampRange);
                var maxFlat = r.deadFlatDumpBasePrice * (1f + clampRange);
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
            var curve = SmoothStep01(progress);
            var scheduledPrice = Mathf.Lerp(r.deadFlatDumpStartPrice, r.deadFlatDumpTargetPrice, curve);

            var distance = Mathf.Abs(r.deadFlatDumpStartPrice - r.deadFlatDumpTargetPrice);
            var maxCatchupDelta = distance / Mathf.Max(0.25f, r.deadFlatDumpDuration) * dt * 1.85f;
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
            r.deadFlatDumpFlatDuration = UnityEngine.Random.Range(
                cfg.deadFlatDumpFlatDurationMinSeconds,
                cfg.deadFlatDumpFlatDurationMaxSeconds);
            r.deadFlatDumpFlatTimeLeft = r.deadFlatDumpFlatDuration;
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
            var minPrice = GetPlayablePriceFloor(cfg);

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
            r.deadFlatDumpFlatDuration = 0f;
            r.deadFlatDumpDuration = 0f;
            r.deadFlatDumpTimeLeft = 0f;
            r.deadFlatDumpStarted = false;
            r.deadFlatDumpInitialized = false;
        }

        private float SimulateBusinessSkillPumpDump(CoinRuntime r, float dt, float noise, float range)
        {
            if (!r.businessSkillInitialized)
                StartBusinessSkillPumpDump(r, default);

            if (!r.businessSkillDumpStarted)
            {
                var elapsed = Mathf.Clamp(
                    r.businessSkillGrowDuration - r.businessSkillGrowTimeLeft,
                    0f,
                    r.businessSkillGrowDuration);
                var progress = Mathf.Clamp01((elapsed + dt) / Mathf.Max(0.0001f, r.businessSkillGrowDuration));
                var curve = SmoothStep01(progress);
                var scheduledPrice = Mathf.Lerp(r.businessSkillGrowStartPrice, r.businessSkillTargetPrice, curve);
                var maxCatchupDelta = Mathf.Abs(r.businessSkillTargetPrice - r.businessSkillGrowStartPrice)
                    / Mathf.Max(0.5f, r.businessSkillGrowDuration)
                    * dt
                    * 2.4f;

                var guidedDelta = Mathf.Clamp(scheduledPrice - r.rawPrice, 0f, maxCatchupDelta);
                var noiseDelta = noise * range * 0.035f * dt;
                noiseDelta = Mathf.Clamp(noiseDelta, -maxCatchupDelta * 0.15f, maxCatchupDelta * 0.15f);
                var next = r.rawPrice + guidedDelta + noiseDelta;

                r.corridorAnchor = Mathf.Lerp(r.corridorAnchor, r.businessSkillTargetPrice, 0.08f);
                RebuildCorridor(r, next, force: false);

                r.businessSkillGrowTimeLeft -= dt;
                if (r.businessSkillGrowTimeLeft <= 0f || next >= r.businessSkillTargetPrice * 0.995f)
                    StartBusinessSkillDumpPhase(r);

                return next;
            }

            var dumpElapsed = Mathf.Clamp(
                r.businessSkillDumpDuration - r.businessSkillDumpTimeLeft,
                0f,
                r.businessSkillDumpDuration);
            var dumpProgress = Mathf.Clamp01((dumpElapsed + dt) / Mathf.Max(0.0001f, r.businessSkillDumpDuration));
            var dumpCurve = 1f - Mathf.Pow(1f - dumpProgress, 3f);
            var dumpScheduledPrice = Mathf.Lerp(r.businessSkillDumpStartPrice, r.businessSkillReturnPrice, dumpCurve);
            var dumpDistance = Mathf.Abs(r.businessSkillDumpStartPrice - r.businessSkillReturnPrice);
            var dumpMaxCatchupDelta = dumpDistance / Mathf.Max(0.25f, r.businessSkillDumpDuration) * dt * 3.5f;

            var dumpGuidedDelta = Mathf.Clamp(dumpScheduledPrice - r.rawPrice, -dumpMaxCatchupDelta, dumpMaxCatchupDelta);
            var dumpNext = r.rawPrice + dumpGuidedDelta;

            r.corridorAnchor = Mathf.Lerp(r.corridorAnchor, r.businessSkillReturnPrice, 0.18f);
            RebuildCorridor(r, dumpNext, force: false);

            r.businessSkillDumpTimeLeft -= dt;
            if (r.businessSkillDumpTimeLeft <= 0f
                || Mathf.Abs(dumpNext - r.businessSkillReturnPrice) <= Mathf.Max(0.000001f, r.businessSkillReturnPrice * 0.01f))
                r.stateTimeLeft = 0f;

            return dumpNext;
        }

        private void StartBusinessSkillPumpDump(CoinRuntime r, PlannedState planned)
        {
            var growDuration = planned.businessGrowDurationSeconds > 0f
                ? planned.businessGrowDurationSeconds
                : 15f;
            var dumpDuration = planned.businessDumpDurationSeconds > 0f
                ? planned.businessDumpDurationSeconds
                : 7f;
            var growthPercent = planned.businessGrowthPercent > 0f
                ? planned.businessGrowthPercent
                : 0.35f;
            var tolerance = Mathf.Clamp(planned.businessReturnTolerancePercent, 0f, 0.3f);
            var returnJitter = UnityEngine.Random.Range(-tolerance, tolerance);

            r.businessSkillBasePrice = Mathf.Max(0.000001f, r.rawPrice);
            r.businessSkillGrowStartPrice = r.businessSkillBasePrice;
            r.businessSkillTargetPrice = r.businessSkillBasePrice * (1f + growthPercent);
            r.businessSkillReturnPrice = Mathf.Max(0.000001f, r.businessSkillBasePrice * (1f + returnJitter));
            r.businessSkillGrowDuration = Mathf.Max(1f, growDuration);
            r.businessSkillGrowTimeLeft = r.businessSkillGrowDuration;
            r.businessSkillDumpDuration = Mathf.Max(0.25f, dumpDuration);
            r.businessSkillDumpTimeLeft = r.businessSkillDumpDuration;
            r.businessSkillDumpStarted = false;
            r.businessSkillInitialized = true;
        }

        private static void StartBusinessSkillDumpPhase(CoinRuntime r)
        {
            r.businessSkillDumpStartPrice = Mathf.Max(0.000001f, r.rawPrice);
            r.businessSkillDumpStarted = true;
        }

        private static void ResetBusinessSkillPumpDump(CoinRuntime r)
        {
            r.businessSkillBasePrice = 0f;
            r.businessSkillGrowStartPrice = 0f;
            r.businessSkillTargetPrice = 0f;
            r.businessSkillDumpStartPrice = 0f;
            r.businessSkillReturnPrice = 0f;
            r.businessSkillGrowDuration = 0f;
            r.businessSkillGrowTimeLeft = 0f;
            r.businessSkillDumpDuration = 0f;
            r.businessSkillDumpTimeLeft = 0f;
            r.businessSkillDumpStarted = false;
            r.businessSkillInitialized = false;
        }

        private float SimulatePatternState(CoinRuntime r, float dt, float noise, float range)
        {
            if (!r.patternInitialized)
                StartPatternState(r);

            if (r.patternLegTimeLeft <= 0f)
                StartNextPatternLeg(r);

            var duration = Mathf.Max(0.0001f, r.patternLegDuration);
            var elapsed = Mathf.Clamp(duration - r.patternLegTimeLeft, 0f, duration);
            var progress = Mathf.Clamp01((elapsed + dt) / duration);
            var curve = GetPatternCurve(r.currentState, progress);
            var scheduledPrice = Mathf.Lerp(r.patternLegStartPrice, r.patternLegTargetPrice, curve);

            var distance = Mathf.Abs(r.patternLegTargetPrice - r.patternLegStartPrice);
            var maxCatchupDelta = Mathf.Max(GetRoundingStep(r.config, r.rawPrice), distance / duration * dt)
                * GetPatternCatchupMultiplier(r.currentState);
            var guidedDelta = Mathf.Clamp(scheduledPrice - r.rawPrice, -maxCatchupDelta, maxCatchupDelta);

            var noiseStrength = GetPatternNoiseStrength(r);
            var noiseDelta = noise * range * noiseStrength * dt;
            var microNoise = (Mathf.PerlinNoise((r.seed * 0.0091f) + r.time * 1.2f, 0.91f) * 2f - 1f)
                * range
                * noiseStrength
                * 0.35f
                * dt;
            var maxNoise = Mathf.Max(GetRoundingStep(r.config, r.rawPrice), maxCatchupDelta * 0.55f);
            noiseDelta = Mathf.Clamp(noiseDelta + microNoise, -maxNoise, maxNoise);
            var legDirection = r.patternLegTargetPrice >= r.patternLegStartPrice ? 1 : -1;
            var impulse = BuildTickImpulse(r, range, maxCatchupDelta, legDirection);

            var outsidePull = 0f;
            if (r.rawPrice > r.corridorHigh) outsidePull = (r.corridorHigh - r.rawPrice);
            else if (r.rawPrice < r.corridorLow) outsidePull = (r.corridorLow - r.rawPrice);

            var next = r.rawPrice + guidedDelta + noiseDelta + impulse + outsidePull * r.stateCorridorPullStrength;

            var anchorTarget = GetPatternAnchorTarget(r, next);
            r.corridorAnchor = Mathf.Lerp(r.corridorAnchor, anchorTarget, GetPatternAnchorLerp(r.currentState));
            RebuildCorridor(r, next, force: false);

            r.patternLegTimeLeft -= dt;
            if (r.patternLegTimeLeft <= 0f)
                StartNextPatternLeg(r);

            return next;
        }

        private void StartPatternState(CoinRuntime r)
        {
            r.patternInitialized = true;
            r.patternLegIndex = 0;
            r.patternBasePrice = Mathf.Max(GetPlayablePriceFloor(r.config), r.rawPrice);
            r.patternLastMajorPrice = r.patternBasePrice;
            r.patternDirection = PickPatternDirection(r);
            r.patternLegsTotal = PickPatternLegCount(r);
            r.patternLegTimeLeft = 0f;
        }

        private void StartNextPatternLeg(CoinRuntime r)
        {
            if (r.patternLegIndex >= r.patternLegsTotal)
            {
                r.stateTimeLeft = 0f;
                r.patternLegTimeLeft = 1f;
                return;
            }

            var leg = r.patternLegIndex;
            r.patternLegIndex++;
            r.patternLegStartPrice = Mathf.Max(GetPlayablePriceFloor(r.config), r.rawPrice);
            r.patternLegTargetPrice = ClampToPlayableFloor(r.config, ApplyUpwardMovePressure(r, PickPatternLegTarget(r, leg)));
            r.patternLegDuration = PickPatternLegDuration(r);
            r.patternLegTimeLeft = r.patternLegDuration;
        }

        private float PickPatternLegTarget(CoinRuntime r, int leg)
        {
            return r.currentState switch
            {
                MarketStateType.StairUp => PickStairUpTarget(r, leg, accumulation: false),
                MarketStateType.AccumulationRun => PickStairUpTarget(r, leg, accumulation: true),
                MarketStateType.StairDown => PickStairDownTarget(r, leg),
                MarketStateType.CompressionBreakout => PickCompressionTarget(r, leg),
                MarketStateType.LowRangeRecovery => PickLowRangeRecoveryTarget(r, leg),
                _ => r.rawPrice,
            };
        }

        private float PickStairUpTarget(CoinRuntime r, int leg, bool accumulation)
        {
            var step = PickPatternStepPercent(r.config.id, upMove: true, accumulation);
            if (leg % 2 == 0)
            {
                var target = r.patternLegStartPrice * (1f + step);
                r.patternLastMajorPrice = Mathf.Max(r.patternLastMajorPrice, target);
                return target;
            }

            var pullback = step * UnityEngine.Random.Range(accumulation ? 0.28f : 0.38f, accumulation ? 0.48f : 0.64f);
            var higherLow = r.patternBasePrice * (1f + GetHigherLowGuard01(r.config.id, leg, accumulation));
            return Mathf.Max(higherLow, r.patternLegStartPrice * (1f - pullback));
        }

        private float PickStairDownTarget(CoinRuntime r, int leg)
        {
            var step = PickPatternStepPercent(r.config.id, upMove: false, accumulation: false);
            if (leg % 2 == 0)
            {
                var target = r.patternLegStartPrice * (1f - step);
                r.patternLastMajorPrice = Mathf.Min(r.patternLastMajorPrice, target);
                return target;
            }

            var rally = step * UnityEngine.Random.Range(0.35f, 0.58f);
            var lowerHigh = r.patternBasePrice * (1f - GetLowerHighGuard01(r.config.id, leg));
            return Mathf.Min(lowerHigh, r.patternLegStartPrice * (1f + rally));
        }

        private float PickCompressionTarget(CoinRuntime r, int leg)
        {
            var lastLeg = Mathf.Max(0, r.patternLegsTotal - 1);
            if (leg >= lastLeg)
            {
                var breakout = PickCompressionBreakoutPercent(r.config.id);
                return r.patternLegStartPrice * (1f + breakout * r.patternDirection);
            }

            var baseAmplitude = PickCompressionAmplitude01(r.config.id);
            var t = lastLeg <= 1 ? 1f : (float)leg / (lastLeg - 1);
            var amplitude = baseAmplitude * Mathf.Lerp(1f, 0.25f, t);
            var direction = ((leg % 2 == 0) ? 1f : -1f) * -r.patternDirection;
            return r.patternBasePrice * (1f + amplitude * direction);
        }

        private float PickLowRangeRecoveryTarget(CoinRuntime r, int leg)
        {
            if (leg == 0)
            {
                var drop = PickLowRangeDropPercent(r.config.id);
                var target = r.patternLegStartPrice * (1f - drop);
                r.patternLastMajorPrice = target;
                return target;
            }

            var lastLeg = Mathf.Max(0, r.patternLegsTotal - 1);
            if (leg >= lastLeg)
            {
                var recovery = PickLowRangeRecoveryPercent(r.config.id);
                return Mathf.Min(r.patternBasePrice * 0.97f, r.patternLastMajorPrice * (1f + recovery));
            }

            var range = PickLowRangeSidewaysPercent(r.config.id);
            var direction = leg % 2 == 0 ? 1f : -1f;
            return r.patternLastMajorPrice * (1f + direction * range * UnityEngine.Random.Range(0.35f, 1f));
        }

        private static int PickPatternLegCount(CoinRuntime r)
        {
            return r.currentState switch
            {
                MarketStateType.StairUp => UnityEngine.Random.Range(5, 8),
                MarketStateType.StairDown => UnityEngine.Random.Range(5, 8),
                MarketStateType.CompressionBreakout => UnityEngine.Random.Range(5, 7),
                MarketStateType.LowRangeRecovery => UnityEngine.Random.Range(5, 7),
                MarketStateType.AccumulationRun when r.config.id == CurrencyId.BTC => UnityEngine.Random.Range(9, 12),
                MarketStateType.AccumulationRun => UnityEngine.Random.Range(6, 9),
                _ => 4,
            };
        }

        private static int PickPatternDirection(CoinRuntime r)
        {
            var upChance = r.config.id switch
            {
                CurrencyId.SHT => 0.52f,
                CurrencyId.ETH => 0.55f,
                CurrencyId.BTC => 0.68f,
                _ => 0.55f,
            };

            if (r.currentState == MarketStateType.StairDown || r.currentState == MarketStateType.LowRangeRecovery)
                upChance = 0.35f;
            else if (r.currentState == MarketStateType.AccumulationRun)
                upChance = 0.82f;

            return UnityEngine.Random.value <= upChance ? 1 : -1;
        }

        private static float PickPatternStepPercent(CurrencyId id, bool upMove, bool accumulation)
        {
            if (accumulation)
            {
                return id switch
                {
                    CurrencyId.BTC => UnityEngine.Random.Range(0.036f, 0.100f),
                    CurrencyId.ETH => UnityEngine.Random.Range(0.045f, 0.120f),
                    _ => UnityEngine.Random.Range(0.070f, 0.165f),
                };
            }

            if (upMove)
            {
                return id switch
                {
                    CurrencyId.SHT => UnityEngine.Random.Range(0.085f, 0.235f),
                    CurrencyId.ETH => UnityEngine.Random.Range(0.055f, 0.140f),
                    CurrencyId.BTC => UnityEngine.Random.Range(0.044f, 0.118f),
                    _ => UnityEngine.Random.Range(0.06f, 0.16f),
                };
            }

            return id switch
            {
                CurrencyId.SHT => UnityEngine.Random.Range(0.085f, 0.220f),
                CurrencyId.ETH => UnityEngine.Random.Range(0.028f, 0.074f),
                CurrencyId.BTC => UnityEngine.Random.Range(0.024f, 0.065f),
                _ => UnityEngine.Random.Range(0.06f, 0.16f),
            };
        }

        private static float PickCompressionAmplitude01(CurrencyId id)
        {
            return id switch
            {
                CurrencyId.SHT => UnityEngine.Random.Range(0.030f, 0.090f),
                CurrencyId.ETH => UnityEngine.Random.Range(0.026f, 0.065f),
                CurrencyId.BTC => UnityEngine.Random.Range(0.020f, 0.052f),
                _ => UnityEngine.Random.Range(0.018f, 0.050f),
            };
        }

        private static float PickCompressionBreakoutPercent(CurrencyId id)
        {
            return id switch
            {
                CurrencyId.SHT => UnityEngine.Random.Range(0.120f, 0.380f),
                CurrencyId.ETH => UnityEngine.Random.Range(0.085f, 0.230f),
                CurrencyId.BTC => UnityEngine.Random.Range(0.065f, 0.180f),
                _ => UnityEngine.Random.Range(0.10f, 0.28f),
            };
        }

        private static float PickLowRangeDropPercent(CurrencyId id)
        {
            return id switch
            {
                CurrencyId.SHT => UnityEngine.Random.Range(0.130f, 0.320f),
                CurrencyId.ETH => UnityEngine.Random.Range(0.060f, 0.170f),
                CurrencyId.BTC => UnityEngine.Random.Range(0.050f, 0.135f),
                _ => UnityEngine.Random.Range(0.10f, 0.24f),
            };
        }

        private static float PickLowRangeSidewaysPercent(CurrencyId id)
        {
            return id switch
            {
                CurrencyId.SHT => UnityEngine.Random.Range(0.040f, 0.125f),
                CurrencyId.ETH => UnityEngine.Random.Range(0.030f, 0.078f),
                CurrencyId.BTC => UnityEngine.Random.Range(0.022f, 0.060f),
                _ => UnityEngine.Random.Range(0.025f, 0.065f),
            };
        }

        private static float PickLowRangeRecoveryPercent(CurrencyId id)
        {
            return id switch
            {
                CurrencyId.SHT => UnityEngine.Random.Range(0.150f, 0.420f),
                CurrencyId.ETH => UnityEngine.Random.Range(0.125f, 0.335f),
                CurrencyId.BTC => UnityEngine.Random.Range(0.105f, 0.285f),
                _ => UnityEngine.Random.Range(0.14f, 0.34f),
            };
        }

        private static float GetHigherLowGuard01(CurrencyId id, int leg, bool accumulation)
        {
            var step = accumulation ? 0.006f : id switch
            {
                CurrencyId.SHT => 0.042f,
                CurrencyId.ETH => 0.022f,
                CurrencyId.BTC => 0.014f,
                _ => 0.014f,
            };

            return step * Mathf.Max(1, leg);
        }

        private static float GetLowerHighGuard01(CurrencyId id, int leg)
        {
            var step = id switch
            {
                CurrencyId.SHT => 0.038f,
                CurrencyId.ETH => 0.024f,
                CurrencyId.BTC => 0.017f,
                _ => 0.016f,
            };

            return step * Mathf.Max(1, leg);
        }

        private static float PickPatternLegDuration(CoinRuntime r)
        {
            var remainingLegs = Mathf.Max(1, r.patternLegsTotal - r.patternLegIndex + 1);
            var averageBudget = Mathf.Max(1f, r.stateTimeLeft / remainingLegs);
            var duration = averageBudget * UnityEngine.Random.Range(0.78f, 1.22f);

            var minMax = GetPatternLegDurationBounds(r.config.id, r.currentState);
            return Mathf.Clamp(duration, minMax.x, minMax.y);
        }

        private static Vector2 GetPatternLegDurationBounds(CurrencyId id, MarketStateType state)
        {
            if (state == MarketStateType.AccumulationRun)
            {
                return id switch
                {
                    CurrencyId.BTC => new Vector2(26f, 72f),
                    CurrencyId.ETH => new Vector2(10f, 30f),
                    _ => new Vector2(5f, 15f),
                };
            }

            return id switch
            {
                CurrencyId.SHT => new Vector2(2.8f, 8.5f),
                CurrencyId.ETH => new Vector2(5.5f, 19f),
                CurrencyId.BTC => new Vector2(10f, 34f),
                _ => new Vector2(5f, 20f),
            };
        }

        private static float GetPatternCurve(MarketStateType state, float progress)
        {
            progress = Mathf.Clamp01(progress);
            return state switch
            {
                MarketStateType.CompressionBreakout => SmoothStep01(progress),
                MarketStateType.LowRangeRecovery => SmoothStep01(progress),
                MarketStateType.AccumulationRun => Mathf.Pow(progress, 1.08f),
                _ => SmoothStep01(progress),
            };
        }

        private static float GetPatternCatchupMultiplier(MarketStateType state)
        {
            return state switch
            {
                MarketStateType.CompressionBreakout => 2.35f,
                MarketStateType.LowRangeRecovery => 1.70f,
                MarketStateType.AccumulationRun => 1.45f,
                _ => 1.95f,
            };
        }

        private static float GetPatternNoiseStrength(CoinRuntime r)
        {
            var baseNoise = r.config.id switch
            {
                CurrencyId.SHT => 0.225f,
                CurrencyId.ETH => 0.105f,
                CurrencyId.BTC => 0.070f,
                _ => 0.065f,
            };

            if (r.currentState == MarketStateType.CompressionBreakout
                && r.patternLegIndex < r.patternLegsTotal)
                baseNoise *= 0.55f;
            else if (r.currentState == MarketStateType.AccumulationRun)
                baseNoise *= 0.75f;
            else if (r.currentState == MarketStateType.LowRangeRecovery)
                baseNoise *= 1.15f;

            return baseNoise;
        }

        private static float GetPatternAnchorTarget(CoinRuntime r, float next)
        {
            return r.currentState switch
            {
                MarketStateType.LowRangeRecovery => Mathf.Lerp(r.patternLastMajorPrice, next, 0.35f),
                MarketStateType.CompressionBreakout => Mathf.Lerp(r.patternBasePrice, next, 0.45f),
                _ => next,
            };
        }

        private static float GetPatternAnchorLerp(MarketStateType state)
        {
            return state switch
            {
                MarketStateType.CompressionBreakout => 0.025f,
                MarketStateType.LowRangeRecovery => 0.035f,
                MarketStateType.AccumulationRun => 0.020f,
                _ => 0.040f,
            };
        }

        private static void ResetPatternState(CoinRuntime r)
        {
            r.patternLegIndex = 0;
            r.patternLegsTotal = 0;
            r.patternDirection = 1;
            r.patternBasePrice = 0f;
            r.patternLastMajorPrice = 0f;
            r.patternLegStartPrice = 0f;
            r.patternLegTargetPrice = 0f;
            r.patternLegDuration = 0f;
            r.patternLegTimeLeft = 0f;
            r.patternInitialized = false;
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

            var highVolatility = r.config.noiseStrength >= 0.60f;
            var targetPos = r.chopDirection > 0
                ? UnityEngine.Random.Range(highVolatility ? 0.68f : 0.62f, highVolatility ? 0.94f : 0.88f)
                : UnityEngine.Random.Range(highVolatility ? 0.06f : 0.12f, highVolatility ? 0.32f : 0.38f);

            r.chopTargetPrice = Mathf.Lerp(r.corridorLow, r.corridorHigh, targetPos);
            var canFakeout = r.chopPhaseDuration >= GetMinimumChopFakeoutPhaseDuration(r.config);
            r.chopFakeoutStartTime = canFakeout && UnityEngine.Random.value <= r.config.chopFakeoutChancePerPhase
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
                    $"fakeout={(canFakeout && !float.IsPositiveInfinity(r.chopFakeoutStartTime) ? $"at {r.chopFakeoutStartTime:0.0}s" : "none")}.",
                    this);
            }
        }

        private static float GetMinimumChopFakeoutPhaseDuration(CoinSimulationConfig cfg)
        {
            return Mathf.Max(2.5f, cfg.chopFakeoutDurationMinSeconds + 0.5f);
        }

        private bool ShouldLogActiveCurrency(CoinRuntime r)
        {
            if (_warmupInProgress || _balanceSimulationInProgress)
                return false;

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
                MarketStateType.StairUp => cfg.longUptrendMaxPriceChangePerTick01,
                MarketStateType.AccumulationRun => cfg.longUptrendMaxPriceChangePerTick01,
                MarketStateType.StairDown => cfg.longDowntrendMaxPriceChangePerTick01,
                MarketStateType.LowRangeRecovery => cfg.longDowntrendMaxPriceChangePerTick01,
                MarketStateType.CompressionBreakout => cfg.normalMaxPriceChangePerTick01,
                MarketStateType.DeadFlatThenRocketPump => cfg.deadFlatRocketMaxPriceChangePerTick01,
                MarketStateType.DeadFlatThenRocketDump => cfg.deadFlatDumpMaxPriceChangePerTick01,
                MarketStateType.BusinessSkillPumpDump => cfg.pumpMaxPriceChangePerTick01,
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
                MarketStateType.BusinessSkillPumpDump => cfg.pumpTickSpeedMultiplier,
                _ => cfg.normalTickSpeedMultiplier,
            };
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

        [ContextMenu("Debug/Run Market Balance Simulation")]
        private void Debug_RunMarketBalanceSimulation()
        {
            Debug.Log(
                RunMarketBalanceSimulationReport(balanceSimulationMinutes, balanceSimulationRuns),
                this);
        }

        public string RunMarketBalanceSimulationReport(float minutes, int runs)
        {
            minutes = Mathf.Max(1f, minutes);
            runs = Mathf.Max(1, runs);

            var randomState = UnityEngine.Random.state;
            var wasWarmup = _warmupInProgress;
            var wasBalanceSimulation = _balanceSimulationInProgress;
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

                        var cfg = CloneSimulationConfig(source);
                        if (useRecommendedMarketPersonalityTuning)
                            ApplyRecommendedMarketPersonalityTuning(cfg);
                        NormalizeConfig(cfg);

                        UnityEngine.Random.InitState(BuildBalanceSimulationSeed(cfg.id, run));
                        var stats = SimulateBalanceRun(cfg, simSeconds);

                        if (!aggregates.TryGetValue(stats.id, out var aggregate))
                        {
                            aggregate = new BalanceAggregateStats { id = stats.id };
                            aggregates.Add(stats.id, aggregate);
                        }

                        aggregate.Add(stats);
                    }
                }

                return BuildBalanceSimulationReport(minutes, runs, aggregates);
            }
            finally
            {
                UnityEngine.Random.state = randomState;
                _warmupInProgress = wasWarmup;
                _balanceSimulationInProgress = wasBalanceSimulation;
            }
        }

        private BalanceRunStats SimulateBalanceRun(CoinSimulationConfig cfg, float simSeconds)
        {
            var r = CreateBalanceRuntime(cfg);
            var startPrice = r.visiblePrice;
            var floor = GetPlayablePriceFloor(cfg);
            var stats = new BalanceRunStats
            {
                id = cfg.id,
                startPrice = startPrice,
                finalPrice = startPrice,
                checkpoint15mPrice = startPrice,
                checkpoint30mPrice = startPrice,
                checkpoint60mPrice = startPrice,
                minPrice = startPrice,
                maxPrice = startPrice,
            };

            var elapsed = 0f;
            var previousPrice = (double)startPrice;
            var peak = previousPrice;
            var trough = previousPrice;
            var absMoveSum = 0d;
            var checkpoint15Set = false;
            var checkpoint30Set = false;
            var checkpoint60Set = false;

            while (elapsed < simSeconds)
            {
                var dt = Mathf.Min(GetEffectiveTickInterval(r), simSeconds - elapsed);
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

                if (price <= floor * 1.25d)
                    stats.nearFloorSeconds += dt;

                stats.ticks++;
                previousPrice = price;

                if (!checkpoint15Set && elapsed >= 15f * 60f)
                {
                    stats.checkpoint15mPrice = price;
                    checkpoint15Set = true;
                }

                if (!checkpoint30Set && elapsed >= 30f * 60f)
                {
                    stats.checkpoint30mPrice = price;
                    checkpoint30Set = true;
                }

                if (!checkpoint60Set && elapsed >= 60f * 60f)
                {
                    stats.checkpoint60mPrice = price;
                    checkpoint60Set = true;
                }
            }

            if (!checkpoint15Set)
                stats.checkpoint15mPrice = stats.finalPrice;
            if (!checkpoint30Set)
                stats.checkpoint30mPrice = stats.finalPrice;
            if (!checkpoint60Set)
                stats.checkpoint60mPrice = stats.finalPrice;

            stats.averageAbsTickMove01 = stats.ticks > 0 ? absMoveSum / stats.ticks : 0d;
            return stats;
        }

        private CoinRuntime CreateBalanceRuntime(CoinSimulationConfig cfg)
        {
            var startPrice = ClampToPlayableFloor(cfg, cfg.initialPrice);
            var r = new CoinRuntime
            {
                config = cfg,
                rawPrice = startPrice,
                visiblePrice = RoundPrice(cfg, startPrice),
                corridorAnchor = startPrice,
                starterAssistTimeLeft = cfg.id == CurrencyId.SHT && useShtStarterAssist
                    ? Mathf.Max(0f, shtStarterAssistDurationSeconds)
                    : 0f,
                seed = UnityEngine.Random.Range(1, int.MaxValue),
            };

            RebuildCorridor(r, r.rawPrice, force: true);
            BuildInitialPlan(r);
            ApplyNextPlannedState(r);
            return r;
        }

        private static CoinSimulationConfig CloneSimulationConfig(CoinSimulationConfig source)
        {
            var json = JsonUtility.ToJson(source);
            return JsonUtility.FromJson<CoinSimulationConfig>(json);
        }

        private static int BuildBalanceSimulationSeed(CurrencyId id, int run)
        {
            return 104729 + ((int)id + 1) * 1009 + run * 7919;
        }

        private static string BuildBalanceSimulationReport(
            float minutes,
            int runs,
            Dictionary<CurrencyId, BalanceAggregateStats> aggregates)
        {
            var sb = new StringBuilder(2048);
            sb.AppendLine($"[Market Balance Simulation] {minutes:0.#}m, {runs} runs");

            AppendBalanceAggregate(sb, aggregates, CurrencyId.SHT, minutes);
            AppendBalanceAggregate(sb, aggregates, CurrencyId.ETH, minutes);
            AppendBalanceAggregate(sb, aggregates, CurrencyId.BTC, minutes);

            return sb.ToString();
        }

        private static void AppendBalanceAggregate(
            StringBuilder sb,
            Dictionary<CurrencyId, BalanceAggregateStats> aggregates,
            CurrencyId id,
            float minutes)
        {
            if (!aggregates.TryGetValue(id, out var stats) || stats.runs <= 0)
                return;

            var runs = stats.runs;
            var start = stats.startPriceSum / runs;
            var final = stats.finalPriceSum / runs;
            var min = stats.minPriceSum / runs;
            var max = stats.maxPriceSum / runs;
            var checkpoint15 = stats.checkpoint15mPriceSum / runs;
            var checkpoint30 = stats.checkpoint30mPriceSum / runs;
            var checkpoint60 = stats.checkpoint60mPriceSum / runs;
            var nearFloorMinutes = stats.nearFloorSecondsSum / runs / 60d;

            sb.AppendLine();
            sb.AppendLine($"{id}:");
            sb.AppendLine($"  start avg: {FormatBalanceMoney(start)}");
            sb.AppendLine($"  15m avg: {FormatBalanceMoney(checkpoint15)} ({FormatBalanceMultiplier(checkpoint15 / start)})");
            sb.AppendLine($"  30m avg: {FormatBalanceMoney(checkpoint30)} ({FormatBalanceMultiplier(checkpoint30 / start)})");
            sb.AppendLine($"  60m avg: {FormatBalanceMoney(checkpoint60)} ({FormatBalanceMultiplier(checkpoint60 / start)})");
            sb.AppendLine($"  final avg: {FormatBalanceMoney(final)} ({FormatBalanceMultiplier(final / start)})");
            sb.AppendLine($"  min avg: {FormatBalanceMoney(min)}, max avg: {FormatBalanceMoney(max)}");
            sb.AppendLine($"  best swing avg/best: {FormatBalancePercent(stats.bestSwingSum / runs)} / {FormatBalancePercent(stats.bestSwingBest)}");
            sb.AppendLine($"  drawdown avg/worst: {FormatBalancePercent(stats.maxDrawdownSum / runs)} / {FormatBalancePercent(stats.maxDrawdownWorst)}");
            sb.AppendLine($"  avg tick move: {FormatBalancePercent(stats.averageAbsTickMoveSum / runs)}");
            sb.AppendLine($"  near floor avg: {nearFloorMinutes:0.0}m of {minutes:0.#}m");
            sb.AppendLine($"  verdict: {BuildBalanceVerdict(id, start, final, stats.bestSwingSum / runs, stats.maxDrawdownSum / runs, nearFloorMinutes, minutes)}");
        }

        private static string BuildBalanceVerdict(
            CurrencyId id,
            double start,
            double final,
            double bestSwing,
            double drawdown,
            double nearFloorMinutes,
            double totalMinutes)
        {
            var finalMultiplier = start > 0d ? final / start : 1d;
            var nearFloorPart = totalMinutes > 0d ? nearFloorMinutes / totalMinutes : 0d;

            return id switch
            {
                CurrencyId.SHT when nearFloorPart > 0.16d => "SHT too often lives near floor",
                CurrencyId.SHT when bestSwing < 1.00d => "SHT may be too calm for high-risk trading",
                CurrencyId.SHT when bestSwing > 11.0d => "SHT may be too explosive",
                CurrencyId.ETH when bestSwing < 0.28d => "ETH may be too flat",
                CurrencyId.ETH when drawdown > 0.72d => "ETH drops may be too punishing",
                CurrencyId.ETH when finalMultiplier > 7.5d => "ETH background growth may be too high",
                CurrencyId.BTC when finalMultiplier < 1.55d => "BTC may be too weak for long holding",
                CurrencyId.BTC when finalMultiplier > 5.5d => "BTC may be too free for long holding",
                CurrencyId.BTC when drawdown < 0.16d => "BTC may be too safe",
                _ => "inside target band",
            };
        }

        private static string FormatBalanceMoney(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return "0";

            return Math.Round(value).ToString("#,0").Replace(",", ".");
        }

        private static string FormatBalanceMultiplier(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return "x0";

            return $"x{value:0.00}";
        }

        private static string FormatBalancePercent(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return "0%";

            return $"{value * 100d:0.#}%";
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
            if (_balanceSimulationInProgress)
                return false;

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
            aroundPrice = ClampToPlayableFloor(cfg, aroundPrice);

            // Volatility shrinks with price: interpolate width between "low price" and "high price".
            var t = Mathf.Clamp01(aroundPrice / Mathf.Max(0.000001f, cfg.highPriceReference));
            var baseWidth = Mathf.Lerp(cfg.corridorWidthAtLowPrice, cfg.corridorWidthAtHighPrice, t);

            var width = r.stateCorridorWidth > 1.01f
                ? r.stateCorridorWidth
                : baseWidth * (1f + UnityEngine.Random.Range(-cfg.corridorWidthRandomJitter, cfg.corridorWidthRandomJitter));
            width = Mathf.Max(1.01f, width);

            // Center corridor around anchor multiplicatively (symmetric in log-space).
            var half = Mathf.Sqrt(width);
            var anchor = ClampToPlayableFloor(cfg, r.corridorAnchor);

            var low = anchor / half;
            var high = anchor * half;

            // Ensure corridor covers current price (softly), unless we explicitly want it tight.
            if (!force)
            {
                if (aroundPrice < low) low = Mathf.Lerp(low, aroundPrice, 0.65f);
                if (aroundPrice > high) high = Mathf.Lerp(high, aroundPrice, 0.65f);
            }

            // Clamp minimum low relative to initial price and the current economy tier.
            var minLow = Mathf.Max(GetPlayablePriceFloor(cfg), GetEconomyCorridorLowFloor(r));
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

        private static float GetEconomyCorridorLowFloor(CoinRuntime r)
        {
            if (r == null || r.economyPriceTarget <= 0f)
                return 0f;

            var floorMultiplier = r.config.id switch
            {
                CurrencyId.SHT => 0.38f,
                CurrencyId.ETH => 0.58f,
                CurrencyId.BTC => 0.72f,
                _ => 0.50f,
            };

            return Mathf.Max(GetPlayablePriceFloor(r.config), r.economyPriceTarget * floorMultiplier);
        }

        private void ApplyNextPlannedState(CoinRuntime r)
        {
            EnsurePlannedStates(r);

            var next = r.plan.Dequeue();
            EnterState(r, next, changeCorridor: true);
        }

        private void EnterState(CoinRuntime r, MarketStateType state, float durationSeconds, bool changeCorridor)
        {
            EnterState(
                r,
                new PlannedState
                {
                    type = state,
                    durationSeconds = durationSeconds,
                },
                changeCorridor);
        }

        private void EnterState(CoinRuntime r, PlannedState planned, bool changeCorridor)
        {
            r.currentState = planned.type;
            r.stateTimeLeft = Mathf.Max(1f, planned.durationSeconds);

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
            ResetBusinessSkillPumpDump(r);
            ResetPatternState(r);

            if (r.currentState == MarketStateType.BusinessSkillPumpDump)
                StartBusinessSkillPumpDump(r, planned);

            ConfigureCorridorForState(r, r.currentState);

            // Random corridor jumps are only kept for generic flat-like behavior.
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
            ApplyEconomyStateChangeAnchorShift(r, state);
            RebuildCorridor(r, r.rawPrice, force: false);
        }

        private void ApplyEconomyStateChangeAnchorShift(CoinRuntime r, MarketStateType state)
        {
            if (!scaleMarketWithBusinessIncome || r.economyPriceTarget <= 0f)
                return;

            var target = Mathf.Max(GetPlayablePriceFloor(r.config), r.economyPriceTarget);
            var baseBlend = r.config.id switch
            {
                CurrencyId.SHT => 0.12f,
                CurrencyId.ETH => 0.16f,
                CurrencyId.BTC => 0.10f,
                _ => 0.20f,
            };
            var stateBlend = state switch
            {
                MarketStateType.MarketCrash => 0.10f,
                MarketStateType.LongDowntrend => 0.20f,
                MarketStateType.StairDown => 0.20f,
                MarketStateType.SlowUpThenDump => 0.24f,
                MarketStateType.DeadFlatThenRocketDump => 0.16f,
                MarketStateType.LowRangeRecovery => 1.35f,
                MarketStateType.StairUp => 1.20f,
                MarketStateType.LongUptrend => 1.10f,
                MarketStateType.AccumulationRun => 1.10f,
                _ => 0.75f,
            };

            if (target < r.corridorAnchor)
                stateBlend *= 0.35f;

            r.corridorAnchor = Mathf.Lerp(r.corridorAnchor, target, Mathf.Clamp01(baseBlend * stateBlend));
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
                MarketStateType.StairUp => cfg.trendCorridorWidth,
                MarketStateType.StairDown => cfg.trendCorridorWidth,
                MarketStateType.CompressionBreakout => cfg.scenarioCorridorWidth,
                MarketStateType.LowRangeRecovery => cfg.scenarioCorridorWidth,
                MarketStateType.AccumulationRun => cfg.trendCorridorWidth,
                MarketStateType.SlowUpThenDump => cfg.scenarioCorridorWidth,
                MarketStateType.SlowUpThenPumpAndDump => cfg.scenarioCorridorWidth,
                MarketStateType.DeadFlatThenRocketPump => cfg.deadFlatCorridorWidth,
                MarketStateType.DeadFlatThenRocketDump => cfg.deadFlatCorridorWidth,
                MarketStateType.BusinessSkillPumpDump => cfg.scenarioCorridorWidth,
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
                MarketStateType.StairUp => cfg.trendCorridorPullStrength,
                MarketStateType.StairDown => cfg.trendCorridorPullStrength,
                MarketStateType.CompressionBreakout => cfg.scenarioCorridorPullStrength,
                MarketStateType.LowRangeRecovery => cfg.scenarioCorridorPullStrength,
                MarketStateType.AccumulationRun => cfg.trendCorridorPullStrength,
                MarketStateType.SlowUpThenDump => cfg.scenarioCorridorPullStrength,
                MarketStateType.SlowUpThenPumpAndDump => cfg.scenarioCorridorPullStrength,
                MarketStateType.DeadFlatThenRocketPump => cfg.deadFlatCorridorPullStrength,
                MarketStateType.DeadFlatThenRocketDump => cfg.deadFlatCorridorPullStrength,
                MarketStateType.BusinessSkillPumpDump => cfg.scenarioCorridorPullStrength,
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
            var stateType = PickMemoryPlannedState(r);
            var duration = PickStateDuration(r.config, stateType);
            duration = AdjustStarterAssistStateDuration(r, stateType, duration);

            return new PlannedState
            {
                type = stateType,
                durationSeconds = duration,
            };
        }

        private MarketStateType PickMemoryPlannedState(CoinRuntime r)
        {
            if (IsInPlayableLowZone(r))
            {
                if (IsShtStarterAssistActive(r))
                {
                    return PickWeightedChoice(
                        new StateChoice(MarketStateType.LowRangeRecovery, 1.50f),
                        new StateChoice(MarketStateType.StairUp, 1.10f),
                        new StateChoice(MarketStateType.CompressionBreakout, 0.50f),
                        new StateChoice(MarketStateType.DeadFlatThenRocketPump, 0.24f),
                        new StateChoice(MarketStateType.ChopInCorridor, 0.28f));
                }

                return PickWeightedChoice(
                    new StateChoice(MarketStateType.LowRangeRecovery, 1.10f),
                    new StateChoice(MarketStateType.StairUp, 0.85f),
                    new StateChoice(MarketStateType.DeadFlatThenRocketPump, 0.35f),
                    new StateChoice(MarketStateType.ChopInCorridor, 0.15f));
            }

            var previous = GetPlanningPreviousState(r);
            if (IsBelowEconomyTargetZone(r))
                return PickEconomyRecoveryState(r);

            if (IsShtStarterAssistActive(r))
                return PickShtStarterAssistNextState(r, previous);

            return r.config.id switch
            {
                CurrencyId.SHT => PickShtNextState(previous),
                CurrencyId.ETH => PickEthNextState(previous),
                CurrencyId.BTC => PickBtcNextState(previous),
                _ => PickWeightedPlannedState(r.config),
            };
        }

        private static MarketStateType GetPlanningPreviousState(CoinRuntime r)
        {
            if (r.plan.Count > 0)
                return r.plan.Last().type;

            return r.currentState;
        }

        private static MarketStateType PickShtStarterAssistNextState(CoinRuntime r, MarketStateType previous)
        {
            var startPrice = Mathf.Max(GetPlayablePriceFloor(r.config), r.config.initialPrice);
            var belowStart = r.rawPrice < startPrice * 0.98f;

            if (belowStart || IsNegativeStarterAssistState(previous))
            {
                return PickWeightedChoice(
                    new StateChoice(MarketStateType.LowRangeRecovery, 1.65f),
                    new StateChoice(MarketStateType.StairUp, 1.20f),
                    new StateChoice(MarketStateType.DeadFlatThenRocketPump, 0.28f),
                    new StateChoice(MarketStateType.CompressionBreakout, 0.45f),
                    new StateChoice(MarketStateType.ChopInCorridor, 0.30f));
            }

            return previous switch
            {
                MarketStateType.StairUp or MarketStateType.LowRangeRecovery => PickWeightedChoice(
                    new StateChoice(MarketStateType.ChopInCorridor, 0.90f),
                    new StateChoice(MarketStateType.CompressionBreakout, 0.75f),
                    new StateChoice(MarketStateType.StairUp, 0.55f),
                    new StateChoice(MarketStateType.SlowUpThenPumpAndDump, 0.35f),
                    new StateChoice(MarketStateType.StairDown, 0.16f)),
                MarketStateType.CompressionBreakout => PickWeightedChoice(
                    new StateChoice(MarketStateType.StairUp, 1.15f),
                    new StateChoice(MarketStateType.ChopInCorridor, 0.65f),
                    new StateChoice(MarketStateType.LowRangeRecovery, 0.32f),
                    new StateChoice(MarketStateType.StairDown, 0.16f)),
                MarketStateType.ChopInCorridor => PickWeightedChoice(
                    new StateChoice(MarketStateType.CompressionBreakout, 1.05f),
                    new StateChoice(MarketStateType.StairUp, 0.95f),
                    new StateChoice(MarketStateType.LowRangeRecovery, 0.38f),
                    new StateChoice(MarketStateType.SlowUpThenPumpAndDump, 0.35f),
                    new StateChoice(MarketStateType.StairDown, 0.18f)),
                _ => PickWeightedChoice(
                    new StateChoice(MarketStateType.StairUp, 1.05f),
                    new StateChoice(MarketStateType.CompressionBreakout, 1.00f),
                    new StateChoice(MarketStateType.LowRangeRecovery, 0.70f),
                    new StateChoice(MarketStateType.ChopInCorridor, 0.65f),
                    new StateChoice(MarketStateType.DeadFlatThenRocketPump, 0.14f),
                    new StateChoice(MarketStateType.StairDown, 0.16f)),
            };
        }

        private float AdjustStarterAssistStateDuration(CoinRuntime r, MarketStateType stateType, float duration)
        {
            if (!IsShtStarterAssistActive(r))
                return duration;

            if (IsNegativeStarterAssistState(stateType))
                return Mathf.Min(duration, 14f);

            return stateType switch
            {
                MarketStateType.DeadFlatThenRocketPump => Mathf.Min(duration, 12f),
                MarketStateType.LowRangeRecovery => Mathf.Min(duration, 34f),
                MarketStateType.StairUp => Mathf.Min(duration, 36f),
                MarketStateType.CompressionBreakout => Mathf.Min(duration, 32f),
                _ => Mathf.Min(duration, 30f),
            };
        }

        private static bool IsInPlayableLowZone(CoinRuntime r)
        {
            var floor = GetPlayablePriceFloor(r.config);
            var trigger = floor * 1.65f;
            return r.rawPrice <= trigger || r.visiblePrice <= trigger;
        }

        private static MarketStateType PickShtNextState(MarketStateType previous)
        {
            return previous switch
            {
                MarketStateType.CompressionBreakout => PickWeightedChoice(
                    new StateChoice(MarketStateType.StairUp, 0.85f),
                    new StateChoice(MarketStateType.StairDown, 0.55f),
                    new StateChoice(MarketStateType.SlowUpThenPumpAndDump, 0.55f),
                    new StateChoice(MarketStateType.ChopInCorridor, 0.35f)),
                MarketStateType.StairUp => PickWeightedChoice(
                    new StateChoice(MarketStateType.SlowUpThenDump, 0.80f),
                    new StateChoice(MarketStateType.ChopInCorridor, 0.75f),
                    new StateChoice(MarketStateType.CompressionBreakout, 0.35f),
                    new StateChoice(MarketStateType.StairUp, 0.25f)),
                MarketStateType.StairDown or MarketStateType.LongDowntrend or MarketStateType.SlowUpThenDump => PickWeightedChoice(
                    new StateChoice(MarketStateType.LowRangeRecovery, 0.90f),
                    new StateChoice(MarketStateType.DeadFlatThenRocketPump, 0.45f),
                    new StateChoice(MarketStateType.ChopInCorridor, 0.55f),
                    new StateChoice(MarketStateType.StairDown, 0.20f)),
                MarketStateType.LowRangeRecovery => PickWeightedChoice(
                    new StateChoice(MarketStateType.ChopInCorridor, 0.75f),
                    new StateChoice(MarketStateType.StairUp, 0.65f),
                    new StateChoice(MarketStateType.CompressionBreakout, 0.45f)),
                MarketStateType.ChopInCorridor => PickWeightedChoice(
                    new StateChoice(MarketStateType.CompressionBreakout, 0.80f),
                    new StateChoice(MarketStateType.StairUp, 0.75f),
                    new StateChoice(MarketStateType.StairDown, 0.55f),
                    new StateChoice(MarketStateType.SlowUpThenPumpAndDump, 0.65f),
                    new StateChoice(MarketStateType.DeadFlatThenRocketDump, 0.15f)),
                _ => PickWeightedChoice(
                    new StateChoice(MarketStateType.ChopInCorridor, 1.25f),
                    new StateChoice(MarketStateType.CompressionBreakout, 0.90f),
                    new StateChoice(MarketStateType.StairUp, 0.80f),
                    new StateChoice(MarketStateType.StairDown, 0.62f),
                    new StateChoice(MarketStateType.SlowUpThenPumpAndDump, 0.70f),
                    new StateChoice(MarketStateType.SlowUpThenDump, 0.48f),
                    new StateChoice(MarketStateType.DeadFlatThenRocketPump, 0.16f),
                    new StateChoice(MarketStateType.DeadFlatThenRocketDump, 0.09f)),
            };
        }

        private static MarketStateType PickEthNextState(MarketStateType previous)
        {
            return previous switch
            {
                MarketStateType.CompressionBreakout => PickWeightedChoice(
                    new StateChoice(MarketStateType.StairUp, 1.15f),
                    new StateChoice(MarketStateType.StairDown, 0.50f),
                    new StateChoice(MarketStateType.LowRangeRecovery, 0.45f),
                    new StateChoice(MarketStateType.ChopInCorridor, 0.40f)),
                MarketStateType.StairUp or MarketStateType.LongUptrend => PickWeightedChoice(
                    new StateChoice(MarketStateType.ChopInCorridor, 0.78f),
                    new StateChoice(MarketStateType.CompressionBreakout, 0.70f),
                    new StateChoice(MarketStateType.SlowUpThenDump, 0.35f),
                    new StateChoice(MarketStateType.StairUp, 0.38f)),
                MarketStateType.StairDown or MarketStateType.LongDowntrend or MarketStateType.SlowUpThenDump => PickWeightedChoice(
                    new StateChoice(MarketStateType.LowRangeRecovery, 1.35f),
                    new StateChoice(MarketStateType.StairUp, 0.75f),
                    new StateChoice(MarketStateType.ChopInCorridor, 0.55f),
                    new StateChoice(MarketStateType.CompressionBreakout, 0.50f),
                    new StateChoice(MarketStateType.StairDown, 0.14f)),
                MarketStateType.LowRangeRecovery => PickWeightedChoice(
                    new StateChoice(MarketStateType.ChopInCorridor, 0.75f),
                    new StateChoice(MarketStateType.StairUp, 0.92f),
                    new StateChoice(MarketStateType.CompressionBreakout, 0.62f)),
                MarketStateType.ChopInCorridor => PickWeightedChoice(
                    new StateChoice(MarketStateType.StairUp, 0.96f),
                    new StateChoice(MarketStateType.StairDown, 0.42f),
                    new StateChoice(MarketStateType.CompressionBreakout, 0.92f),
                    new StateChoice(MarketStateType.LowRangeRecovery, 0.42f),
                    new StateChoice(MarketStateType.SlowUpThenDump, 0.16f),
                    new StateChoice(MarketStateType.SlowUpThenPumpAndDump, 0.42f)),
                _ => PickWeightedChoice(
                    new StateChoice(MarketStateType.ChopInCorridor, 0.88f),
                    new StateChoice(MarketStateType.StairUp, 1.02f),
                    new StateChoice(MarketStateType.StairDown, 0.42f),
                    new StateChoice(MarketStateType.CompressionBreakout, 0.95f),
                    new StateChoice(MarketStateType.LowRangeRecovery, 0.62f),
                    new StateChoice(MarketStateType.SlowUpThenPumpAndDump, 0.38f),
                    new StateChoice(MarketStateType.SlowUpThenDump, 0.14f),
                    new StateChoice(MarketStateType.Calm, 0.06f)),
            };
        }

        private static MarketStateType PickBtcNextState(MarketStateType previous)
        {
            return previous switch
            {
                MarketStateType.AccumulationRun => PickWeightedChoice(
                    new StateChoice(MarketStateType.CompressionBreakout, 0.72f),
                    new StateChoice(MarketStateType.SlowUpThenDump, 0.32f),
                    new StateChoice(MarketStateType.StairUp, 0.62f),
                    new StateChoice(MarketStateType.ChopInCorridor, 0.36f)),
                MarketStateType.CompressionBreakout => PickWeightedChoice(
                    new StateChoice(MarketStateType.AccumulationRun, 0.90f),
                    new StateChoice(MarketStateType.StairUp, 0.84f),
                    new StateChoice(MarketStateType.LowRangeRecovery, 0.38f),
                    new StateChoice(MarketStateType.StairDown, 0.18f)),
                MarketStateType.SlowUpThenDump or MarketStateType.StairDown or MarketStateType.LongDowntrend => PickWeightedChoice(
                    new StateChoice(MarketStateType.LowRangeRecovery, 1.05f),
                    new StateChoice(MarketStateType.AccumulationRun, 0.88f),
                    new StateChoice(MarketStateType.StairUp, 0.45f),
                    new StateChoice(MarketStateType.ChopInCorridor, 0.35f)),
                MarketStateType.LowRangeRecovery => PickWeightedChoice(
                    new StateChoice(MarketStateType.AccumulationRun, 1.10f),
                    new StateChoice(MarketStateType.StairUp, 0.70f),
                    new StateChoice(MarketStateType.CompressionBreakout, 0.42f),
                    new StateChoice(MarketStateType.ChopInCorridor, 0.25f)),
                _ => PickWeightedChoice(
                    new StateChoice(MarketStateType.AccumulationRun, 1.28f),
                    new StateChoice(MarketStateType.StairUp, 0.78f),
                    new StateChoice(MarketStateType.CompressionBreakout, 0.58f),
                    new StateChoice(MarketStateType.SlowUpThenDump, 0.20f),
                    new StateChoice(MarketStateType.LowRangeRecovery, 0.36f),
                    new StateChoice(MarketStateType.ChopInCorridor, 0.36f),
                    new StateChoice(MarketStateType.DeadFlatThenRocketPump, 0.10f)),
            };
        }

        private static MarketStateType PickWeightedChoice(params StateChoice[] choices)
        {
            var total = 0f;
            for (var i = 0; i < choices.Length; i++)
                total += Mathf.Max(0f, choices[i].weight);

            if (total <= 0f)
                return PickFallbackPlannedState();

            var roll = UnityEngine.Random.value * total;
            for (var i = 0; i < choices.Length; i++)
            {
                var weight = Mathf.Max(0f, choices[i].weight);
                if (weight <= 0f)
                    continue;

                roll -= weight;
                if (roll <= 0f)
                    return choices[i].type;
            }

            return choices[^1].type;
        }

        private static float PickStateDuration(CoinSimulationConfig cfg, MarketStateType stateType)
        {
            if (IsPatternState(stateType))
                return PickPatternStateDuration(cfg, stateType);

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

            if (stateType == MarketStateType.SlowUpThenDump)
                return PickReadableScenarioDuration(cfg, upThenDump: true);

            if (stateType == MarketStateType.SlowUpThenPumpAndDump)
                return PickReadableScenarioDuration(cfg, upThenDump: false);

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

        private static float PickReadableScenarioDuration(CoinSimulationConfig cfg, bool upThenDump)
        {
            return cfg.id switch
            {
                CurrencyId.SHT => UnityEngine.Random.Range(upThenDump ? 18f : 20f, upThenDump ? 34f : 36f),
                CurrencyId.ETH => UnityEngine.Random.Range(upThenDump ? 34f : 38f, upThenDump ? 66f : 72f),
                CurrencyId.BTC => UnityEngine.Random.Range(upThenDump ? 90f : 100f, upThenDump ? 170f : 190f),
                _ => UnityEngine.Random.Range(35f, 80f),
            };
        }

        private static bool IsPatternState(MarketStateType stateType)
        {
            return stateType is MarketStateType.StairUp
                or MarketStateType.StairDown
                or MarketStateType.CompressionBreakout
                or MarketStateType.LowRangeRecovery
                or MarketStateType.AccumulationRun;
        }

        private static float PickPatternStateDuration(CoinSimulationConfig cfg, MarketStateType stateType)
        {
            var range = stateType switch
            {
                MarketStateType.StairUp => cfg.id switch
                {
                    CurrencyId.SHT => new Vector2(24f, 46f),
                    CurrencyId.ETH => new Vector2(30f, 66f),
                    CurrencyId.BTC => new Vector2(100f, 210f),
                    _ => new Vector2(40f, 100f),
                },
                MarketStateType.StairDown => cfg.id switch
                {
                    CurrencyId.SHT => new Vector2(22f, 42f),
                    CurrencyId.ETH => new Vector2(28f, 62f),
                    CurrencyId.BTC => new Vector2(70f, 150f),
                    _ => new Vector2(40f, 100f),
                },
                MarketStateType.CompressionBreakout => cfg.id switch
                {
                    CurrencyId.SHT => new Vector2(20f, 38f),
                    CurrencyId.ETH => new Vector2(26f, 56f),
                    CurrencyId.BTC => new Vector2(65f, 135f),
                    _ => new Vector2(30f, 80f),
                },
                MarketStateType.LowRangeRecovery => cfg.id switch
                {
                    CurrencyId.SHT => new Vector2(28f, 56f),
                    CurrencyId.ETH => new Vector2(40f, 86f),
                    CurrencyId.BTC => new Vector2(140f, 280f),
                    _ => new Vector2(70f, 160f),
                },
                MarketStateType.AccumulationRun => cfg.id switch
                {
                    CurrencyId.BTC => new Vector2(600f, 900f),
                    CurrencyId.ETH => new Vector2(55f, 120f),
                    CurrencyId.SHT => new Vector2(42f, 90f),
                    _ => new Vector2(120f, 240f),
                },
                _ => new Vector2(cfg.stateDurationMinSeconds, cfg.stateDurationMaxSeconds),
            };

            return UnityEngine.Random.Range(range.x, range.y);
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
            var minLow = GetPlayablePriceFloor(cfg);
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
            rawPrice = ClampToPlayableFloor(cfg, rawPrice);
            var step = GetRoundingStep(cfg, rawPrice);
            if (step <= 0f)
                return rawPrice;

            return ClampToPlayableFloor(cfg, Mathf.Round(rawPrice / step) * step);
        }

        private static float ClampToPlayableFloor(CoinSimulationConfig cfg, float price)
        {
            return Mathf.Max(CurrencyMarket.SanitizePrice(price), GetPlayablePriceFloor(cfg));
        }

        private static float GetPlayablePriceFloor(CoinSimulationConfig cfg)
        {
            return CurrencyMarket.SanitizePrice(
                cfg.initialPrice * Mathf.Clamp(cfg.minCorridorLowFromInitial, 0.01f, 0.95f));
        }

        private static float ApplyUpwardMovePressure(CoinRuntime r, float targetPrice)
        {
            var current = Mathf.Max(GetPlayablePriceFloor(r.config), r.rawPrice);
            if (targetPrice <= current)
                return targetPrice;

            var cfg = r.config;
            var pressureStart = cfg.id switch
            {
                CurrencyId.SHT => cfg.initialPrice * 2.0f,
                CurrencyId.ETH => cfg.initialPrice * 1.6f,
                CurrencyId.BTC => cfg.initialPrice * 2.6f,
                _ => cfg.highPriceReference * 0.25f,
            };
            var pressureFull = cfg.id switch
            {
                CurrencyId.SHT => cfg.initialPrice * 6.0f,
                CurrencyId.ETH => cfg.initialPrice * 4.5f,
                CurrencyId.BTC => cfg.initialPrice * 8.0f,
                _ => cfg.highPriceReference,
            };
            var minBoostKeep = cfg.id switch
            {
                CurrencyId.SHT => 0.060f,
                CurrencyId.ETH => 0.18f,
                CurrencyId.BTC => 0.46f,
                _ => 0.35f,
            };

            var pressure = Mathf.InverseLerp(pressureStart, Mathf.Max(pressureStart + 1f, pressureFull), current);
            if (pressure <= 0f)
                return ApplyUpwardMoveCeiling(r, current, targetPrice);

            var boost = targetPrice / current - 1f;
            var keep = Mathf.Lerp(1f, minBoostKeep, pressure);
            return ApplyUpwardMoveCeiling(r, current, current * (1f + boost * keep));
        }

        private static float ApplyUpwardMoveCeiling(CoinRuntime r, float current, float targetPrice)
        {
            if (targetPrice <= current)
                return targetPrice;

            var ceiling = GetUpwardMoveCeiling(r);
            if (targetPrice <= ceiling)
                return targetPrice;

            var leak = r.config.id switch
            {
                CurrencyId.SHT => 0.04f,
                CurrencyId.ETH => 0.08f,
                CurrencyId.BTC => 0.28f,
                _ => 0.12f,
            };

            return Mathf.Max(current, Mathf.Lerp(ceiling, targetPrice, leak));
        }

        private static float GetUpwardMoveCeiling(CoinRuntime r)
        {
            var cfg = r.config;
            var hours = Mathf.Max(0f, r.time / 3600f);
            var timeScale = cfg.id switch
            {
                CurrencyId.SHT => 1f + hours * 0.25f,
                CurrencyId.ETH => 1f + hours * 0.20f,
                CurrencyId.BTC => 1f + hours * 0.55f,
                _ => 1f + hours * 0.25f,
            };
            var baseMultiplier = cfg.id switch
            {
                CurrencyId.SHT => 3.20f,
                CurrencyId.ETH => 2.10f,
                CurrencyId.BTC => 4.25f,
                _ => 3f,
            };

            var timeCeiling = cfg.initialPrice * baseMultiplier * timeScale;
            var businessCeiling = r.economyPriceTarget > 0f
                ? r.economyPriceTarget * GetBusinessCeilingPadding(cfg.id)
                : 0f;

            return Mathf.Max(GetPlayablePriceFloor(cfg), timeCeiling, businessCeiling);
        }

        private static float GetBusinessCeilingPadding(CurrencyId id)
        {
            return id switch
            {
                CurrencyId.SHT => 1.05f,
                CurrencyId.ETH => 1.18f,
                CurrencyId.BTC => 1.12f,
                _ => 1.15f,
            };
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

        private static void ApplyRecommendedMarketPersonalityTuning(CoinSimulationConfig cfg)
        {
            switch (cfg.id)
            {
                case CurrencyId.SHT:
                    ApplyShtMarketTuning(cfg);
                    break;
                case CurrencyId.ETH:
                    ApplyEthMarketTuning(cfg);
                    break;
                case CurrencyId.BTC:
                    ApplyBtcMarketTuning(cfg);
                    break;
            }
        }

        private static void ApplyShtMarketTuning(CoinSimulationConfig cfg)
        {
            cfg.tickIntervalSeconds = 0.68f;
            cfg.normalTickSpeedMultiplier = 1f;
            cfg.pumpTickSpeedMultiplier = 1.24f;
            cfg.crashTickSpeedMultiplier = 1.45f;
            cfg.plannedStatesCount = 5;
            cfg.stateDurationMinSeconds = 6f;
            cfg.stateDurationMaxSeconds = 22f;
            cfg.plannedStateWeights = new[]
            {
                new MarketStateWeight { type = MarketStateType.ChopInCorridor, weight = 1.30f },
                new MarketStateWeight { type = MarketStateType.StairUp, weight = 0.90f },
                new MarketStateWeight { type = MarketStateType.StairDown, weight = 0.70f },
                new MarketStateWeight { type = MarketStateType.CompressionBreakout, weight = 0.95f },
                new MarketStateWeight { type = MarketStateType.LowRangeRecovery, weight = 0.55f },
                new MarketStateWeight { type = MarketStateType.SlowUpThenPumpAndDump, weight = 0.70f },
                new MarketStateWeight { type = MarketStateType.SlowUpThenDump, weight = 0.45f },
                new MarketStateWeight { type = MarketStateType.DeadFlatThenRocketPump, weight = 0.16f },
                new MarketStateWeight { type = MarketStateType.DeadFlatThenRocketDump, weight = 0.08f },
                new MarketStateWeight { type = MarketStateType.Calm, weight = 0.03f },
            };

            cfg.corridorWidthAtLowPrice = 4.2f;
            cfg.corridorWidthAtHighPrice = 1.65f;
            cfg.highPriceReference = 120000f;
            cfg.corridorWidthRandomJitter = 0.28f;
            cfg.minCorridorLowFromInitial = 0.58f;
            cfg.chopCorridorWidthMin = 3.35f;
            cfg.chopCorridorWidthMax = 6.4f;
            cfg.chopCorridorPullStrength = 0.18f;
            cfg.trendCorridorWidth = 2.45f;
            cfg.trendCorridorPullStrength = 0.045f;
            cfg.scenarioCorridorWidth = 2.10f;
            cfg.scenarioCorridorPullStrength = 0.050f;
            cfg.deadFlatCorridorWidth = 1.10f;
            cfg.deadFlatCorridorPullStrength = 0.012f;
            cfg.noiseStrength = 1.05f;
            cfg.trendStrength = 0.50f;
            cfg.meanReversionStrength = 0.16f;

            cfg.chopPhaseDurationMinSeconds = 4.2f;
            cfg.chopPhaseDurationMaxSeconds = 12.5f;
            cfg.chopFakeoutChancePerPhase = 0.90f;
            cfg.chopFakeoutDurationMinSeconds = 1.2f;
            cfg.chopFakeoutDurationMaxSeconds = 3.8f;
            cfg.chopFakeoutStrength = 0.75f;
            cfg.chopRecoveryDurationSeconds = 1.8f;
            cfg.chopRecoveryBoost = 2.45f;

            cfg.longUptrendDurationMinSeconds = 12f;
            cfg.longUptrendDurationMaxSeconds = 34f;
            cfg.longUptrendTargetMultiplierMin = 1.35f;
            cfg.longUptrendTargetMultiplierMax = 2.45f;
            cfg.longUptrendMaxEffectiveMultiplier = 2.20f;
            cfg.longUptrendNoiseStrength = 0.46f;
            cfg.longUptrendWobbleStrength = 1.35f;
            cfg.longUptrendDownWobbleChance = 0.52f;
            cfg.longUptrendDownWobbleMultiplier = 2.15f;
            cfg.longUptrendUpWobbleMultiplier = 1.05f;
            cfg.longUptrendPullbackChancePerTick = 0.30f;
            cfg.longUptrendPullbackStrength = 0.24f;
            cfg.longUptrendPullbackDurationMinSeconds = 1.1f;
            cfg.longUptrendPullbackDurationMaxSeconds = 3.4f;
            cfg.longUptrendPullbackCooldownSeconds = 1.5f;
            cfg.longUptrendRecoveryDurationSeconds = 1.8f;
            cfg.longUptrendRecoveryCatchupMultiplier = 2.65f;
            cfg.longUptrendMaxPriceChangePerTick01 = 0.22f;
            cfg.longDowntrendDurationMinSeconds = 12f;
            cfg.longDowntrendDurationMaxSeconds = 32f;
            cfg.longDowntrendTargetDividerMin = 1.24f;
            cfg.longDowntrendTargetDividerMax = 1.95f;
            cfg.longDowntrendNoiseStrength = 0.42f;
            cfg.longDowntrendRallyChancePerTick = 0.32f;
            cfg.longDowntrendRallyStrength = 0.24f;
            cfg.longDowntrendRallyDurationMinSeconds = 1.4f;
            cfg.longDowntrendRallyDurationMaxSeconds = 4.8f;
            cfg.longDowntrendRallyCooldownSeconds = 2.2f;
            cfg.longDowntrendRecoveryDurationSeconds = 2.0f;
            cfg.longDowntrendRecoveryCatchupMultiplier = 2.35f;
            cfg.longDowntrendMaxPriceChangePerTick01 = 0.22f;

            cfg.spikeDumpGrowPercentMin = 0.22f;
            cfg.spikeDumpGrowPercentMax = 0.58f;
            cfg.spikeDumpGrowDurationMinSeconds = 6f;
            cfg.spikeDumpGrowDurationMaxSeconds = 16f;
            cfg.spikeDumpDropOverGrowMin = 1.05f;
            cfg.spikeDumpDropOverGrowMax = 1.45f;
            cfg.spikeDumpMaxDropPercent = 0.58f;
            cfg.spikeDumpDumpDurationMinSeconds = 3.2f;
            cfg.spikeDumpDumpDurationMaxSeconds = 6.2f;
            cfg.spikeDumpShakeDurationMinSeconds = 2.5f;
            cfg.spikeDumpShakeDurationMaxSeconds = 6f;
            cfg.spikeDumpShakeMovePercentMin = 0.12f;
            cfg.spikeDumpShakeMovePercentMax = 0.44f;
            cfg.spikeDumpNoiseStrength = 0.28f;
            cfg.spikeDumpMaxPriceChangePerTick01 = 0.22f;

            cfg.dipPumpDipPercentMin = 0.18f;
            cfg.dipPumpDipPercentMax = 0.40f;
            cfg.dipPumpDipDurationMinSeconds = 6f;
            cfg.dipPumpDipDurationMaxSeconds = 16f;
            cfg.dipPumpPumpOverDipMin = 1.20f;
            cfg.dipPumpPumpOverDipMax = 1.85f;
            cfg.dipPumpMaxPumpPercent = 0.72f;
            cfg.dipPumpPumpDurationMinSeconds = 3.2f;
            cfg.dipPumpPumpDurationMaxSeconds = 6.2f;
            cfg.dipPumpShakeDurationMinSeconds = 2.5f;
            cfg.dipPumpShakeDurationMaxSeconds = 6f;
            cfg.dipPumpShakeMovePercentMin = 0.12f;
            cfg.dipPumpShakeMovePercentMax = 0.44f;
            cfg.dipPumpNoiseStrength = 0.28f;
            cfg.dipPumpMaxPriceChangePerTick01 = 0.22f;

            cfg.deadFlatRocketFlatDurationMinSeconds = 3f;
            cfg.deadFlatRocketFlatDurationMaxSeconds = 6f;
            cfg.deadFlatRocketFlatRange01 = 0.070f;
            cfg.deadFlatRocketNoiseStrength = 0.135f;
            cfg.deadFlatRocketPumpPercentMin = 0.24f;
            cfg.deadFlatRocketPumpPercentMax = 0.62f;
            cfg.deadFlatRocketPumpDurationMinSeconds = 1.5f;
            cfg.deadFlatRocketPumpDurationMaxSeconds = 3.2f;
            cfg.deadFlatDumpFlatDurationMinSeconds = 3f;
            cfg.deadFlatDumpFlatDurationMaxSeconds = 6f;
            cfg.deadFlatDumpFlatRange01 = 0.070f;
            cfg.deadFlatDumpNoiseStrength = 0.135f;
            cfg.deadFlatDumpDurationMinSeconds = 1.5f;
            cfg.deadFlatDumpDurationMaxSeconds = 3.2f;
            cfg.deadFlatDumpDropPercentMin = 0.20f;
            cfg.deadFlatDumpDropPercentMax = 0.46f;
            cfg.normalMaxPriceChangePerTick01 = 0.20f;
            cfg.pumpMaxPriceChangePerTick01 = 0.36f;
            cfg.crashMaxPriceChangePerTick01 = 0.48f;
            cfg.roundingRules = new[]
            {
                new PriceRoundingRule { minPrice = 0f, step = 5f },
                new PriceRoundingRule { minPrice = 5000f, step = 10f },
                new PriceRoundingRule { minPrice = 15000f, step = 25f },
                new PriceRoundingRule { minPrice = 40000f, step = 50f },
            };
        }

        private static void ApplyEthMarketTuning(CoinSimulationConfig cfg)
        {
            cfg.tickIntervalSeconds = 0.66f;
            cfg.normalTickSpeedMultiplier = 1f;
            cfg.pumpTickSpeedMultiplier = 1.20f;
            cfg.crashTickSpeedMultiplier = 1.30f;
            cfg.plannedStatesCount = 5;
            cfg.stateDurationMinSeconds = 12f;
            cfg.stateDurationMaxSeconds = 36f;
            cfg.plannedStateWeights = new[]
            {
                new MarketStateWeight { type = MarketStateType.ChopInCorridor, weight = 0.90f },
                new MarketStateWeight { type = MarketStateType.StairUp, weight = 1.08f },
                new MarketStateWeight { type = MarketStateType.StairDown, weight = 0.46f },
                new MarketStateWeight { type = MarketStateType.CompressionBreakout, weight = 0.98f },
                new MarketStateWeight { type = MarketStateType.LowRangeRecovery, weight = 0.70f },
                new MarketStateWeight { type = MarketStateType.SlowUpThenPumpAndDump, weight = 0.30f },
                new MarketStateWeight { type = MarketStateType.SlowUpThenDump, weight = 0.10f },
                new MarketStateWeight { type = MarketStateType.LongUptrend, weight = 0.26f },
                new MarketStateWeight { type = MarketStateType.LongDowntrend, weight = 0.12f },
                new MarketStateWeight { type = MarketStateType.Calm, weight = 0.02f },
            };

            cfg.corridorWidthAtLowPrice = 1.95f;
            cfg.corridorWidthAtHighPrice = 1.26f;
            cfg.highPriceReference = 2400000f;
            cfg.corridorWidthRandomJitter = 0.18f;
            cfg.minCorridorLowFromInitial = 0.55f;
            cfg.calmCorridorWidth = 1.14f;
            cfg.calmCorridorPullStrength = 0.28f;
            cfg.chopCorridorWidthMin = 2.05f;
            cfg.chopCorridorWidthMax = 3.35f;
            cfg.chopCorridorPullStrength = 0.24f;
            cfg.trendCorridorWidth = 2.02f;
            cfg.trendCorridorPullStrength = 0.048f;
            cfg.scenarioCorridorWidth = 1.82f;
            cfg.scenarioCorridorPullStrength = 0.048f;
            cfg.deadFlatCorridorWidth = 1.060f;
            cfg.deadFlatCorridorPullStrength = 0.020f;
            cfg.noiseStrength = 0.76f;
            cfg.trendStrength = 0.42f;
            cfg.meanReversionStrength = 0.18f;

            cfg.chopPhaseDurationMinSeconds = 7f;
            cfg.chopPhaseDurationMaxSeconds = 22f;
            cfg.chopFakeoutChancePerPhase = 0.84f;
            cfg.chopFakeoutDurationMinSeconds = 2.4f;
            cfg.chopFakeoutDurationMaxSeconds = 6.5f;
            cfg.chopFakeoutStrength = 0.66f;
            cfg.chopRecoveryDurationSeconds = 3.6f;
            cfg.chopRecoveryBoost = 1.90f;

            cfg.calmLegDurationMinSeconds = 4f;
            cfg.calmLegDurationMaxSeconds = 14f;
            cfg.calmShortLegMaxSeconds = 7f;
            cfg.calmSmallMoveRelativeThreshold = 0.12f;
            cfg.calmMaxPriceChangePerTick01 = 0.085f;
            cfg.calmNoiseStrength = 0.170f;
            cfg.calmApproachStrength = 0.30f;

            cfg.longUptrendDurationMinSeconds = 34f;
            cfg.longUptrendDurationMaxSeconds = 92f;
            cfg.longUptrendTargetMultiplierMin = 1.30f;
            cfg.longUptrendTargetMultiplierMax = 2.05f;
            cfg.longUptrendMaxEffectiveMultiplier = 1.86f;
            cfg.longUptrendNoiseStrength = 0.190f;
            cfg.longUptrendWobbleStrength = 0.92f;
            cfg.longUptrendDownWobbleChance = 0.54f;
            cfg.longUptrendDownWobbleMultiplier = 1.75f;
            cfg.longUptrendUpWobbleMultiplier = 1.18f;
            cfg.longUptrendPullbackChancePerTick = 0.20f;
            cfg.longUptrendPullbackStrength = 0.120f;
            cfg.longUptrendPullbackDurationMinSeconds = 2.0f;
            cfg.longUptrendPullbackDurationMaxSeconds = 5.8f;
            cfg.longUptrendPullbackCooldownSeconds = 2.4f;
            cfg.longUptrendRecoveryDurationSeconds = 3.0f;
            cfg.longUptrendRecoveryCatchupMultiplier = 1.95f;
            cfg.longUptrendMaxPriceChangePerTick01 = 0.095f;

            cfg.longDowntrendDurationMinSeconds = 38f;
            cfg.longDowntrendDurationMaxSeconds = 104f;
            cfg.longDowntrendTargetDividerMin = 1.12f;
            cfg.longDowntrendTargetDividerMax = 1.42f;
            cfg.longDowntrendNoiseStrength = 0.260f;
            cfg.longDowntrendRallyChancePerTick = 0.30f;
            cfg.longDowntrendRallyStrength = 0.200f;
            cfg.longDowntrendRallyDurationMinSeconds = 2.0f;
            cfg.longDowntrendRallyDurationMaxSeconds = 5.8f;
            cfg.longDowntrendRallyCooldownSeconds = 2.4f;
            cfg.longDowntrendRecoveryDurationSeconds = 3.2f;
            cfg.longDowntrendRecoveryCatchupMultiplier = 2.00f;
            cfg.longDowntrendMaxPriceChangePerTick01 = 0.095f;

            cfg.spikeDumpGrowPercentMin = 0.14f;
            cfg.spikeDumpGrowPercentMax = 0.38f;
            cfg.spikeDumpGrowDurationMinSeconds = 18f;
            cfg.spikeDumpGrowDurationMaxSeconds = 46f;
            cfg.spikeDumpDropOverGrowMin = 1.02f;
            cfg.spikeDumpDropOverGrowMax = 1.28f;
            cfg.spikeDumpMaxDropPercent = 0.34f;
            cfg.spikeDumpDumpDurationMinSeconds = 6f;
            cfg.spikeDumpDumpDurationMaxSeconds = 13f;
            cfg.spikeDumpShakeDurationMinSeconds = 5f;
            cfg.spikeDumpShakeDurationMaxSeconds = 13f;
            cfg.spikeDumpMaxPriceChangePerTick01 = 0.105f;

            cfg.dipPumpDipPercentMin = 0.10f;
            cfg.dipPumpDipPercentMax = 0.28f;
            cfg.dipPumpDipDurationMinSeconds = 18f;
            cfg.dipPumpDipDurationMaxSeconds = 48f;
            cfg.dipPumpPumpOverDipMin = 1.25f;
            cfg.dipPumpPumpOverDipMax = 1.85f;
            cfg.dipPumpMaxPumpPercent = 0.58f;
            cfg.dipPumpPumpDurationMinSeconds = 6f;
            cfg.dipPumpPumpDurationMaxSeconds = 13f;
            cfg.dipPumpShakeDurationMinSeconds = 5f;
            cfg.dipPumpShakeDurationMaxSeconds = 13f;
            cfg.dipPumpMaxPriceChangePerTick01 = 0.115f;

            cfg.deadFlatRocketFlatDurationMinSeconds = 7f;
            cfg.deadFlatRocketFlatDurationMaxSeconds = 15f;
            cfg.deadFlatRocketFlatRange01 = 0.045f;
            cfg.deadFlatRocketNoiseStrength = 0.090f;
            cfg.deadFlatRocketPumpPercentMin = 0.12f;
            cfg.deadFlatRocketPumpPercentMax = 0.34f;
            cfg.deadFlatRocketPumpDurationMinSeconds = 3f;
            cfg.deadFlatRocketPumpDurationMaxSeconds = 7f;
            cfg.deadFlatDumpFlatDurationMinSeconds = 7f;
            cfg.deadFlatDumpFlatDurationMaxSeconds = 15f;
            cfg.deadFlatDumpFlatRange01 = 0.045f;
            cfg.deadFlatDumpNoiseStrength = 0.095f;
            cfg.deadFlatDumpDropPercentMin = 0.10f;
            cfg.deadFlatDumpDropPercentMax = 0.28f;
            cfg.deadFlatDumpDurationMinSeconds = 3f;
            cfg.deadFlatDumpDurationMaxSeconds = 7f;

            cfg.normalMaxPriceChangePerTick01 = 0.105f;
            cfg.pumpMaxPriceChangePerTick01 = 0.22f;
            cfg.crashMaxPriceChangePerTick01 = 0.28f;
        }

        private static void ApplyBtcMarketTuning(CoinSimulationConfig cfg)
        {
            cfg.tickIntervalSeconds = 0.74f;
            cfg.normalTickSpeedMultiplier = 1f;
            cfg.pumpTickSpeedMultiplier = 1.12f;
            cfg.crashTickSpeedMultiplier = 1.20f;
            cfg.plannedStatesCount = 4;
            cfg.stateDurationMinSeconds = 18f;
            cfg.stateDurationMaxSeconds = 62f;
            cfg.plannedStateWeights = new[]
            {
                new MarketStateWeight { type = MarketStateType.AccumulationRun, weight = 1.35f },
                new MarketStateWeight { type = MarketStateType.StairUp, weight = 0.74f },
                new MarketStateWeight { type = MarketStateType.CompressionBreakout, weight = 0.58f },
                new MarketStateWeight { type = MarketStateType.SlowUpThenDump, weight = 0.24f },
                new MarketStateWeight { type = MarketStateType.LowRangeRecovery, weight = 0.38f },
                new MarketStateWeight { type = MarketStateType.ChopInCorridor, weight = 0.40f },
                new MarketStateWeight { type = MarketStateType.StairDown, weight = 0.14f },
                new MarketStateWeight { type = MarketStateType.DeadFlatThenRocketPump, weight = 0.09f },
                new MarketStateWeight { type = MarketStateType.DeadFlatThenRocketDump, weight = 0.05f },
            };

            cfg.corridorWidthAtLowPrice = 1.90f;
            cfg.corridorWidthAtHighPrice = 1.30f;
            cfg.highPriceReference = 32000000f;
            cfg.corridorWidthRandomJitter = 0.18f;
            cfg.minCorridorLowFromInitial = 0.42f;
            cfg.calmCorridorWidth = 1.14f;
            cfg.calmCorridorPullStrength = 0.28f;
            cfg.chopCorridorWidthMin = 1.95f;
            cfg.chopCorridorWidthMax = 3.05f;
            cfg.chopCorridorPullStrength = 0.20f;
            cfg.trendCorridorWidth = 2.10f;
            cfg.trendCorridorPullStrength = 0.036f;
            cfg.scenarioCorridorWidth = 1.86f;
            cfg.scenarioCorridorPullStrength = 0.040f;
            cfg.noiseStrength = 0.68f;
            cfg.trendStrength = 0.39f;
            cfg.meanReversionStrength = 0.15f;

            cfg.chopPhaseDurationMinSeconds = 11f;
            cfg.chopPhaseDurationMaxSeconds = 34f;
            cfg.chopFakeoutChancePerPhase = 0.80f;
            cfg.chopFakeoutDurationMinSeconds = 3f;
            cfg.chopFakeoutDurationMaxSeconds = 8f;
            cfg.chopFakeoutStrength = 0.60f;
            cfg.chopRecoveryDurationSeconds = 4.8f;
            cfg.chopRecoveryBoost = 1.55f;

            cfg.longUptrendDurationMinSeconds = 70f;
            cfg.longUptrendDurationMaxSeconds = 185f;
            cfg.longUptrendTargetMultiplierMin = 1.34f;
            cfg.longUptrendTargetMultiplierMax = 2.28f;
            cfg.longUptrendMaxEffectiveMultiplier = 2.05f;
            cfg.longUptrendNoiseStrength = 0.155f;
            cfg.longUptrendWobbleStrength = 0.72f;
            cfg.longUptrendDownWobbleChance = 0.48f;
            cfg.longUptrendDownWobbleMultiplier = 1.48f;
            cfg.longUptrendUpWobbleMultiplier = 1.12f;
            cfg.longUptrendPullbackChancePerTick = 0.19f;
            cfg.longUptrendPullbackStrength = 0.105f;
            cfg.longUptrendPullbackDurationMinSeconds = 1.8f;
            cfg.longUptrendPullbackDurationMaxSeconds = 6.5f;
            cfg.longUptrendPullbackCooldownSeconds = 2.6f;
            cfg.longUptrendRecoveryDurationSeconds = 4.0f;
            cfg.longUptrendRecoveryCatchupMultiplier = 1.75f;
            cfg.longUptrendMaxPriceChangePerTick01 = 0.064f;

            cfg.longDowntrendDurationMinSeconds = 48f;
            cfg.longDowntrendDurationMaxSeconds = 118f;
            cfg.longDowntrendTargetDividerMin = 1.12f;
            cfg.longDowntrendTargetDividerMax = 1.48f;
            cfg.longDowntrendNoiseStrength = 0.155f;
            cfg.longDowntrendRallyChancePerTick = 0.26f;
            cfg.longDowntrendRallyStrength = 0.140f;
            cfg.longDowntrendRallyDurationMinSeconds = 3.0f;
            cfg.longDowntrendRallyDurationMaxSeconds = 8.5f;
            cfg.longDowntrendRallyCooldownSeconds = 4.2f;
            cfg.longDowntrendRecoveryDurationSeconds = 5.2f;
            cfg.longDowntrendRecoveryCatchupMultiplier = 1.65f;
            cfg.longDowntrendMaxPriceChangePerTick01 = 0.070f;

            cfg.spikeDumpGrowPercentMin = 0.16f;
            cfg.spikeDumpGrowPercentMax = 0.46f;
            cfg.spikeDumpGrowDurationMinSeconds = 35f;
            cfg.spikeDumpGrowDurationMaxSeconds = 90f;
            cfg.spikeDumpDropOverGrowMin = 1.02f;
            cfg.spikeDumpDropOverGrowMax = 1.30f;
            cfg.spikeDumpMaxDropPercent = 0.34f;
            cfg.spikeDumpDumpDurationMinSeconds = 10f;
            cfg.spikeDumpDumpDurationMaxSeconds = 22f;
            cfg.spikeDumpShakeDurationMinSeconds = 8f;
            cfg.spikeDumpShakeDurationMaxSeconds = 20f;
            cfg.spikeDumpMaxPriceChangePerTick01 = 0.085f;

            cfg.dipPumpDipPercentMin = 0.10f;
            cfg.dipPumpDipPercentMax = 0.26f;
            cfg.dipPumpDipDurationMinSeconds = 32f;
            cfg.dipPumpDipDurationMaxSeconds = 90f;
            cfg.dipPumpPumpOverDipMin = 1.25f;
            cfg.dipPumpPumpOverDipMax = 1.82f;
            cfg.dipPumpMaxPumpPercent = 0.60f;
            cfg.dipPumpPumpDurationMinSeconds = 10f;
            cfg.dipPumpPumpDurationMaxSeconds = 24f;
            cfg.dipPumpShakeDurationMinSeconds = 8f;
            cfg.dipPumpShakeDurationMaxSeconds = 20f;
            cfg.dipPumpMaxPriceChangePerTick01 = 0.090f;

            cfg.deadFlatRocketFlatDurationMinSeconds = 36f;
            cfg.deadFlatRocketFlatDurationMaxSeconds = 90f;
            cfg.deadFlatRocketPumpPercentMin = 0.12f;
            cfg.deadFlatRocketPumpPercentMax = 0.34f;
            cfg.deadFlatRocketPumpDurationMinSeconds = 8f;
            cfg.deadFlatRocketPumpDurationMaxSeconds = 20f;
            cfg.deadFlatDumpFlatDurationMinSeconds = 28f;
            cfg.deadFlatDumpFlatDurationMaxSeconds = 75f;
            cfg.deadFlatDumpDropPercentMin = 0.12f;
            cfg.deadFlatDumpDropPercentMax = 0.32f;
            cfg.deadFlatDumpDurationMinSeconds = 8f;
            cfg.deadFlatDumpDurationMaxSeconds = 20f;

            cfg.normalMaxPriceChangePerTick01 = 0.080f;
            cfg.pumpMaxPriceChangePerTick01 = 0.160f;
            cfg.crashMaxPriceChangePerTick01 = 0.220f;
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

            cfg.initialPrice = CurrencyMarket.SanitizePrice(cfg.initialPrice);
            cfg.highPriceReference = Mathf.Max(cfg.initialPrice, cfg.highPriceReference);

            cfg.corridorWidthAtLowPrice = Mathf.Max(1.01f, cfg.corridorWidthAtLowPrice);
            cfg.corridorWidthAtHighPrice = Mathf.Max(1.01f, cfg.corridorWidthAtHighPrice);
            cfg.minCorridorLowFromInitial = Mathf.Clamp(cfg.minCorridorLowFromInitial, 0.01f, 0.95f);

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
                    new MarketStateWeight { type = MarketStateType.StairUp, weight = 0.45f },
                    new MarketStateWeight { type = MarketStateType.StairDown, weight = 0.35f },
                    new MarketStateWeight { type = MarketStateType.CompressionBreakout, weight = 0.35f },
                    new MarketStateWeight { type = MarketStateType.LowRangeRecovery, weight = 0.25f },
                    new MarketStateWeight { type = MarketStateType.AccumulationRun, weight = 0.20f },
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

            if (!HasStateWeight(cfg, MarketStateType.StairUp))
            {
                cfg.plannedStateWeights = cfg.plannedStateWeights
                    .Concat(new[] { new MarketStateWeight { type = MarketStateType.StairUp, weight = 0.45f } })
                    .ToArray();
            }

            if (!HasStateWeight(cfg, MarketStateType.StairDown))
            {
                cfg.plannedStateWeights = cfg.plannedStateWeights
                    .Concat(new[] { new MarketStateWeight { type = MarketStateType.StairDown, weight = 0.35f } })
                    .ToArray();
            }

            if (!HasStateWeight(cfg, MarketStateType.CompressionBreakout))
            {
                cfg.plannedStateWeights = cfg.plannedStateWeights
                    .Concat(new[] { new MarketStateWeight { type = MarketStateType.CompressionBreakout, weight = 0.35f } })
                    .ToArray();
            }

            if (!HasStateWeight(cfg, MarketStateType.LowRangeRecovery))
            {
                cfg.plannedStateWeights = cfg.plannedStateWeights
                    .Concat(new[] { new MarketStateWeight { type = MarketStateType.LowRangeRecovery, weight = 0.25f } })
                    .ToArray();
            }

            if (!HasStateWeight(cfg, MarketStateType.AccumulationRun))
            {
                cfg.plannedStateWeights = cfg.plannedStateWeights
                    .Concat(new[] { new MarketStateWeight { type = MarketStateType.AccumulationRun, weight = 0.20f } })
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

        public void EnqueueBusinessSkillOverrideNextState(
            CurrencyId currency,
            Vector2 growDurationSeconds,
            Vector2 dumpDurationSeconds,
            Vector2 growthPercent,
            float returnTolerancePercent)
        {
            if (!_runtime.TryGetValue(currency, out var runtime))
            {
                Debug.LogWarning($"[{nameof(MarketSimulation)}] No runtime market for {currency}.", this);
                return;
            }

            EnsurePlannedStates(runtime);

            var growDuration = PickRange(growDurationSeconds, 1f);
            var dumpDuration = PickRange(dumpDurationSeconds, 0.25f);
            var growth = PickRange(growthPercent, 0.01f);
            var planned = new PlannedState
            {
                type = MarketStateType.BusinessSkillPumpDump,
                durationSeconds = growDuration + dumpDuration + 1f,
                businessGrowDurationSeconds = growDuration,
                businessDumpDurationSeconds = dumpDuration,
                businessGrowthPercent = growth,
                businessReturnTolerancePercent = Mathf.Clamp(returnTolerancePercent, 0f, 0.3f),
            };

            var existing = runtime.plan.ToArray();
            runtime.plan.Clear();
            runtime.plan.Enqueue(planned);

            for (var i = 1; i < existing.Length; i++)
                runtime.plan.Enqueue(existing[i]);

            EnsurePlannedStates(runtime);
            Debug.Log(
                $"[{nameof(MarketSimulation)}] Business skill queued for {currency}: " +
                $"growth={growth:P0}, grow={growDuration:0.0}s, dump={dumpDuration:0.0}s.",
                this);
        }

        private static float PickRange(Vector2 range, float minValue)
        {
            var min = Mathf.Max(minValue, Mathf.Min(range.x, range.y));
            var max = Mathf.Max(min, Mathf.Max(range.x, range.y));
            return UnityEngine.Random.Range(min, max);
        }
    }
}
