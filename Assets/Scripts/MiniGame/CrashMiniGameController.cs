using System;
using TMPro;
using TraidingIDLE.Player;
using TraidingIDLE.Saves;
using TraidingIDLE.UI;
using UnityEngine;
using UnityEngine.UI;

namespace TraidingIDLE.MiniGame
{
    public sealed class CrashMiniGameController : MonoBehaviour
    {
        private enum Stage
        {
            Betting = 0,
            Playing = 1,
            ResultPause = 2,
        }

        [Serializable]
        private sealed class MultiplierOption
        {
            public string label = "x2";
            [Min(1f)] public float value = 2f;
            public CrashMiniGameOptionButtonUI button;
        }

        [Serializable]
        private sealed class StakeOption
        {
            public string label = "50к";
            [Min(0)] public long rubles = 50_000;
            public CrashMiniGameOptionButtonUI button;
        }

        [Serializable]
        private sealed class SaveData
        {
            public int selectedMultiplierIndex;
            public int selectedStakeIndex;
            public bool firstBetWinConsumed;
            public bool firstX10WinConsumed;
        }

        private struct DropEvent
        {
            public float start01;
            public float duration01;
            public float depth;
        }

        private const string SaveKey = "save.crash-mini-game.v1";
        private const int MaxSamples = 768;
        private const int MaxDropEvents = 8;

        [Header("Refs")]
        [SerializeField] private PlayerProfile profile;
        [SerializeField] private CrashMiniGameChartGraphic chart;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text potentialWinText;
        [SerializeField] private Button placeBetButton;
        [SerializeField] private TMP_Text placeBetButtonText;

        [Header("Options")]
        [SerializeField] private MultiplierOption[] multipliers =
        {
            new() { label = "x2", value = 2f },
            new() { label = "x5", value = 5f },
            new() { label = "x10", value = 10f },
            new() { label = "x20", value = 20f },
        };
        [SerializeField] private StakeOption[] stakes =
        {
            new() { label = "50к", rubles = 50_000 },
            new() { label = "150к", rubles = 150_000 },
            new() { label = "300к", rubles = 300_000 },
            new() { label = "500к", rubles = 500_000 },
        };

        [Header("Timing")]
        [SerializeField, Min(1f)] private float bettingSeconds = 20f;
        [SerializeField, Min(0.5f)] private float playSecondsMin = 4f;
        [SerializeField, Min(0.5f)] private float playSecondsMax = 8f;
        [SerializeField, Min(0.05f)] private float crashFallSeconds = 0.45f;
        [SerializeField, Min(0f)] private float resultPauseSeconds = 5f;

        [Header("Balance")]
        [Tooltip("Expected payout return by stake tier. Values below 1 keep the mini-game from printing money long-term.")]
        [SerializeField, Range(0f, 1f)] private float[] targetReturnByStake =
        {
            0.84f,
            0.80f,
            0.76f,
            0.72f,
        };
        [Tooltip("Higher values make large multipliers statistically rarer and lower their expected return.")]
        [SerializeField, Range(0.75f, 1.35f)] private float multiplierChancePower = 1.05f;
        [SerializeField, Range(0f, 1f)] private float maxPlayerWinChance = 0.46f;
        [SerializeField, Range(0f, 0.2f)] private float minPlayerWinChance = 0.01f;
        [Tooltip("No-bet rounds can be more generous visually because they do not pay the player.")]
        [SerializeField, Range(0f, 2f)] private float noBetTargetReturn = 1.05f;
        [SerializeField, Range(0f, 1f)] private float nearWinWeight = 0.42f;
        [SerializeField, Range(0f, 1f)] private float fomoWinWeight = 0.28f;
        [SerializeField, Range(0f, 1f)] private float nearLoseWeight = 0.68f;

        [Header("Tutorial safety")]
        [SerializeField] private bool guaranteeFirstWin = true;
        [SerializeField, Min(1f)] private float firstGuaranteedWinMaxMultiplier = 20f;
        [SerializeField, Min(1f)] private float firstGuaranteedCrashMultiplier = 20f;
        [SerializeField] private bool guaranteeFirstTenMultiplierWin = false;

        [Header("Background")]
        [SerializeField] private bool runWhileInactive = true;
        [SerializeField, Min(0.1f)] private float backgroundTickSeconds = 1f;

        [Header("Chart scale")]
        [SerializeField, Min(0.25f)] private float visibleMultiplierSpan = 4.8f;
        [SerializeField, Min(0.1f)] private float topPaddingMultiplier = 0.45f;
        [SerializeField, Min(2f)] private float idleChartMaxMultiplier = 15.5f;
        [SerializeField, Min(0.03f)] private float graphUpdateIntervalSeconds = 0.22f;
        [SerializeField, Min(0f)] private float graphJaggedness = 0.55f;
        [SerializeField, Range(0.2f, 2f)] private float graphGrowthPower = 0.82f;
        [SerializeField, Min(0f)] private float sawTeethPerSecond = 2.4f;
        [SerializeField, Range(0f, 0.4f)] private float crashTimeJitterPercent = 0.14f;

        [Header("Chart drops")]
        [SerializeField, Min(0)] private int minDropEvents = 1;
        [SerializeField, Min(0)] private int maxDropEvents = 4;
        [SerializeField] private Vector2 dropDepthPercent = new(0.06f, 0.18f);
        [SerializeField] private Vector2 dropDurationSeconds = new(0.18f, 0.55f);
        [SerializeField, Range(0.05f, 0.5f)] private float dropAttackShare = 0.18f;
        [SerializeField, Range(0f, 1f)] private float thresholdDropChance = 0.55f;
        [SerializeField, Range(0f, 1f)] private float losingFinalDropChance = 0.75f;

        [Header("Texts")]
        [SerializeField] private string bettingStatusFormat = "Прием ставок: {0}";
        [SerializeField] private string playingStatusText = "Ожидание результата";
        [SerializeField] private string winStatusFormat = "Выигрыш: {0} руб";
        [SerializeField] private string loseStatusText = "Ставка не сыграла";
        [SerializeField] private string idleResultStatusText = "Раунд завершен";
        [SerializeField] private string potentialWinFormat = "{0} руб";
        [SerializeField] private string placeBetText = "Сделать ставку";
        [SerializeField] private string betAcceptedText = "Ставка принята";
        [SerializeField] private string waitNextRoundText = "Жди следующий раунд";
        [SerializeField] private string thousandSep = ".";

        private readonly CrashMiniGameChartGraphic.Sample[] _samples = new CrashMiniGameChartGraphic.Sample[MaxSamples];
        private Stage _stage;
        private float _stageTimeLeft;
        private int _sampleCount;
        private int _selectedMultiplierIndex;
        private int _selectedStakeIndex;
        private bool _firstBetWinConsumed;
        private bool _firstX10WinConsumed;
        private bool _betPlaced;
        private long _roundStake;
        private float _roundTargetMultiplier;
        private float _roundCrashMultiplier;
        private bool _roundPlayerWon;
        private bool _roundHadPlayer;
        private float _lastDisplayedMultiplier = 1f;
        private float _lastGraphUpdateElapsedSeconds;
        private float _roundNoiseSeed;
        private float _roundCrashAtSeconds;
        private float _roundTotalSeconds;
        private bool _crashPeakSamplePushed;
        private bool _forceFirstGuaranteedCrash;
        private float _backgroundTickAccumulator;
        private int _dropEventCount;
        private readonly DropEvent[] _dropEvents = new DropEvent[MaxDropEvents];
        private readonly float[] _chartThresholdValues = new float[MaxDropEvents];

        private static BackgroundRunner _backgroundRunner;

        private sealed class BackgroundRunner : MonoBehaviour
        {
            private readonly System.Collections.Generic.List<CrashMiniGameController> _controllers = new();

            public static void Register(CrashMiniGameController controller)
            {
                if (controller == null || !controller.runWhileInactive)
                    return;

                var runner = GetOrCreate();
                if (!runner._controllers.Contains(controller))
                    runner._controllers.Add(controller);
            }

            public static void Unregister(CrashMiniGameController controller)
            {
                if (_backgroundRunner == null || controller == null)
                    return;

                _backgroundRunner._controllers.Remove(controller);
                _backgroundRunner.DestroyIfEmpty();
            }

            private static BackgroundRunner GetOrCreate()
            {
                if (_backgroundRunner != null)
                    return _backgroundRunner;

                var go = new GameObject(nameof(CrashMiniGameController) + "BackgroundRunner");
                DontDestroyOnLoad(go);
                _backgroundRunner = go.AddComponent<BackgroundRunner>();
                return _backgroundRunner;
            }

            private void Update()
            {
                var dt = Time.deltaTime;
                if (dt <= 0f)
                    return;

                for (var i = _controllers.Count - 1; i >= 0; i--)
                {
                    var controller = _controllers[i];
                    if (controller == null || controller.isActiveAndEnabled || !controller.runWhileInactive)
                    {
                        _controllers.RemoveAt(i);
                        continue;
                    }

                    controller.AdvanceInBackground(dt);
                }

                DestroyIfEmpty();
            }

            private void OnDestroy()
            {
                if (_backgroundRunner == this)
                    _backgroundRunner = null;
            }

            private void DestroyIfEmpty()
            {
                if (_controllers.Count > 0)
                    return;

                if (_backgroundRunner == this)
                    _backgroundRunner = null;

                if (Application.isPlaying)
                    Destroy(gameObject);
                else
                    DestroyImmediate(gameObject);
            }
        }

        private void Awake()
        {
            ResolveReferences();
            NormalizeDefaultMultiplierOptions();
            Load();
            BindOptionButtons();
            SyncChartThresholds();
            StartBettingRound();
        }

        private void OnEnable()
        {
            BackgroundRunner.Unregister(this);
            _backgroundTickAccumulator = 0f;

            if (profile != null)
                profile.RublesChanged += OnRublesChanged;

            if (placeBetButton != null)
                placeBetButton.onClick.AddListener(TryPlaceBet);

            UpdateChartView();
            RefreshAllUi();
        }

        private void OnDisable()
        {
            if (profile != null)
                profile.RublesChanged -= OnRublesChanged;

            if (placeBetButton != null)
                placeBetButton.onClick.RemoveListener(TryPlaceBet);

            Save();

            if (runWhileInactive && Application.isPlaying)
                BackgroundRunner.Register(this);
        }

        private void OnDestroy()
        {
            BackgroundRunner.Unregister(this);
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause)
                Save();
        }

        private void OnApplicationQuit()
        {
            Save();
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            AdvanceRoundTime(dt, updateGraph: true);
        }

        private void AdvanceInBackground(float dt)
        {
            if (dt <= 0f)
                return;

            _backgroundTickAccumulator += dt;
            var interval = Mathf.Max(0.1f, backgroundTickSeconds);
            if (_backgroundTickAccumulator < interval)
                return;

            var step = _backgroundTickAccumulator;
            _backgroundTickAccumulator = 0f;
            AdvanceRoundTime(step, updateGraph: false);
        }

        private void AdvanceRoundTime(float dt, bool updateGraph)
        {
            if (dt <= 0f)
                return;

            _stageTimeLeft -= dt;
            switch (_stage)
            {
                case Stage.Betting:
                    if (_stageTimeLeft <= 0f)
                        StartPlayingRound();
                    break;

                case Stage.Playing:
                    if (updateGraph)
                        UpdatePlayingGraph();
                    if (_stageTimeLeft <= 0f)
                        FinishPlayingRound();
                    break;

                case Stage.ResultPause:
                    if (_stageTimeLeft <= 0f)
                        StartBettingRound();
                    break;
            }

            if (updateGraph)
                RefreshDynamicUi();
        }

        private void BindOptionButtons()
        {
            for (var i = 0; i < multipliers.Length; i++)
            {
                var index = i;
                var option = multipliers[i];
                if (option?.button == null)
                    continue;

                option.button.SetLabel(option.label);
                option.button.Bind(() => SelectMultiplier(index));
            }

            for (var i = 0; i < stakes.Length; i++)
            {
                var index = i;
                var option = stakes[i];
                if (option?.button == null)
                    continue;

                option.button.SetLabel(option.label);
                option.button.Bind(() => SelectStake(index));
            }
        }

        private void SelectMultiplier(int index)
        {
            if (!CanChangeBetOptions() || !IsValidMultiplierIndex(index))
                return;

            _selectedMultiplierIndex = index;
            Save();
            RefreshAllUi();
        }

        private void SelectStake(int index)
        {
            if (!CanChangeBetOptions() || !IsValidStakeIndex(index))
                return;

            var stake = stakes[index].rubles;
            if (!CanSpend(stake))
            {
                stakes[index].button?.BlinkUnavailable();
                return;
            }

            _selectedStakeIndex = index;
            Save();
            RefreshAllUi();
        }

        private void TryPlaceBet()
        {
            if (_stage != Stage.Betting || _betPlaced)
                return;

            var stake = GetSelectedStakeRubles();
            if (!CanSpend(stake))
            {
                GetSelectedStakeButton()?.BlinkUnavailable();
                return;
            }

            if (profile == null || !profile.TrySpendRubles(stake))
            {
                GetSelectedStakeButton()?.BlinkUnavailable();
                return;
            }

            _betPlaced = true;
            _roundStake = stake;
            _roundTargetMultiplier = GetSelectedMultiplier();
            RefreshAllUi();
        }

        private void StartBettingRound()
        {
            _stage = Stage.Betting;
            _stageTimeLeft = bettingSeconds;
            _betPlaced = false;
            _roundStake = 0;
            _roundTargetMultiplier = 0f;
            _roundCrashMultiplier = 1f;
            _roundPlayerWon = false;
            _roundHadPlayer = false;
            _lastDisplayedMultiplier = 1f;
            _lastGraphUpdateElapsedSeconds = 0f;
            _roundCrashAtSeconds = 0f;
            _roundTotalSeconds = 0f;
            _crashPeakSamplePushed = false;
            _forceFirstGuaranteedCrash = false;
            _dropEventCount = 0;
            _sampleCount = 0;
            chart?.SetSamples(_samples, 0, 0f, idleChartMaxMultiplier);
            chart?.SetCurrentValue(0f, 0f, false);
            RefreshAllUi();
        }

        private void StartPlayingRound()
        {
            _stage = Stage.Playing;
            _sampleCount = 0;
            _lastDisplayedMultiplier = 1f;
            _lastGraphUpdateElapsedSeconds = 0f;
            _roundNoiseSeed = UnityEngine.Random.Range(0f, 1000f);
            _crashPeakSamplePushed = false;
            GenerateRoundResult();
            _roundCrashAtSeconds = RollRoundCrashTimeSeconds(_roundCrashMultiplier);
            _roundTotalSeconds = _roundCrashAtSeconds + Mathf.Max(0.05f, crashFallSeconds);
            _stageTimeLeft = _roundTotalSeconds;
            GenerateDropEvents();
            PushSample(0f, 1f);
            UpdateChartView();
            RefreshAllUi();
        }

        private void UpdatePlayingGraph()
        {
            var elapsed = _roundTotalSeconds - Mathf.Max(0f, _stageTimeLeft);
            var time01 = Mathf.Clamp01(elapsed / GetGraphTimeAxisSeconds());
            var hasCrashed = elapsed >= _roundCrashAtSeconds;

            if (!hasCrashed)
            {
                var t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, _roundCrashAtSeconds));
                var multiplier = CalculateRocketMultiplier(t);

                if (elapsed - _lastGraphUpdateElapsedSeconds >= graphUpdateIntervalSeconds
                    || elapsed >= _roundCrashAtSeconds - 0.01f)
                {
                    _lastDisplayedMultiplier = multiplier;
                    PushSample(time01, multiplier);
                    _lastGraphUpdateElapsedSeconds = elapsed;
                }
            }
            else
            {
                if (!_crashPeakSamplePushed)
                {
                    PushSample(Mathf.Clamp01(_roundCrashAtSeconds / GetGraphTimeAxisSeconds()), _roundCrashMultiplier);
                    _crashPeakSamplePushed = true;
                }

                var t = Mathf.InverseLerp(
                    _roundCrashAtSeconds,
                    _roundCrashAtSeconds + Mathf.Max(0.05f, crashFallSeconds),
                    elapsed);
                var multiplier = Mathf.Lerp(_roundCrashMultiplier, 0f, t * t);
                if (elapsed - _lastGraphUpdateElapsedSeconds >= graphUpdateIntervalSeconds * 0.5f
                    || _stageTimeLeft <= 0.01f)
                {
                    PushSample(time01, multiplier);
                    _lastGraphUpdateElapsedSeconds = elapsed;
                }
            }

            UpdateChartView();
        }

        private void FinishPlayingRound()
        {
            if (!_crashPeakSamplePushed)
            {
                PushSample(Mathf.Clamp01(_roundCrashAtSeconds / GetGraphTimeAxisSeconds()), _roundCrashMultiplier);
                _crashPeakSamplePushed = true;
            }

            PushSample(Mathf.Clamp01(_roundTotalSeconds / GetGraphTimeAxisSeconds()), 0f);
            UpdateChartView();

            if (_roundHadPlayer && _roundPlayerWon)
            {
                var payout = CalculatePayout(_roundStake, _roundTargetMultiplier);
                profile?.AddRubles(payout);
            }

            _stage = Stage.ResultPause;
            _stageTimeLeft = resultPauseSeconds;
            RefreshAllUi();
        }

        private float RollRoundCrashTimeSeconds(float crashMultiplier)
        {
            var min = Mathf.Max(0.5f, Mathf.Min(playSecondsMin, playSecondsMax));
            var max = Mathf.Max(min, Mathf.Max(playSecondsMin, playSecondsMax));
            var normalized = Mathf.InverseLerp(1.05f, idleChartMaxMultiplier, Mathf.Max(1.05f, crashMultiplier));
            var baseSeconds = Mathf.Lerp(min, max, Mathf.Clamp01(normalized));
            var jitter = (max - min) * crashTimeJitterPercent;
            return Mathf.Clamp(UnityEngine.Random.Range(baseSeconds - jitter, baseSeconds + jitter), min, max);
        }

        private float GetGraphTimeAxisSeconds()
        {
            return Mathf.Max(0.5f, Mathf.Max(playSecondsMin, playSecondsMax)) + Mathf.Max(0.05f, crashFallSeconds);
        }

        private void GenerateDropEvents()
        {
            _dropEventCount = 0;
            var maxEvents = Mathf.Clamp(Mathf.Max(minDropEvents, maxDropEvents), 0, _dropEvents.Length);
            var minEvents = Mathf.Clamp(Mathf.Min(minDropEvents, maxDropEvents), 0, maxEvents);
            if (maxEvents <= 0 || _roundCrashAtSeconds <= 0.25f)
                return;

            _dropEventCount = UnityEngine.Random.Range(minEvents, maxEvents + 1);
            var desiredCount = _dropEventCount;
            _dropEventCount = 0;

            if (multipliers != null)
            {
                for (var i = 0; i < multipliers.Length; i++)
                {
                    if (multipliers[i] != null)
                        TryAddThresholdDrop(Mathf.Max(1f, multipliers[i].value), desiredCount);
                }
            }

            if (_roundHadPlayer && !_roundPlayerWon && UnityEngine.Random.value < losingFinalDropChance)
                AddDropEvent(UnityEngine.Random.Range(0.66f, 0.84f), 1.2f, desiredCount);

            while (_dropEventCount < desiredCount)
                AddDropEvent(UnityEngine.Random.Range(0.12f, 0.86f), 1f, desiredCount);
        }

        private void TryAddThresholdDrop(float threshold, int desiredCount)
        {
            if (_dropEventCount >= desiredCount || threshold >= _roundCrashMultiplier * 0.96f)
                return;

            if (UnityEngine.Random.value > thresholdDropChance)
                return;

            var crossingTime = EstimateThresholdCrossingTime01(threshold);
            AddDropEvent(crossingTime - UnityEngine.Random.Range(0.02f, 0.08f), 1f, desiredCount);
        }

        private float EstimateThresholdCrossingTime01(float threshold)
        {
            var logCrash = Mathf.Log(Mathf.Max(1.01f, _roundCrashMultiplier));
            var curve = Mathf.Clamp01(Mathf.Log(Mathf.Max(1.01f, threshold)) / logCrash);
            return Mathf.Pow(curve, 1f / GetEffectiveGrowthPower());
        }

        private void AddDropEvent(float start01, float depthScale, int desiredCount)
        {
            if (_dropEventCount >= desiredCount || _dropEventCount >= _dropEvents.Length)
                return;

            var durationSeconds = UnityEngine.Random.Range(
                Mathf.Min(dropDurationSeconds.x, dropDurationSeconds.y),
                Mathf.Max(dropDurationSeconds.x, dropDurationSeconds.y));
            var duration01 = Mathf.Clamp(durationSeconds / _roundCrashAtSeconds, 0.04f, 0.35f);
            _dropEvents[_dropEventCount++] = new DropEvent
            {
                start01 = Mathf.Clamp(start01, 0.1f, Mathf.Max(0.11f, 0.9f - duration01)),
                duration01 = duration01,
                depth = UnityEngine.Random.Range(
                    Mathf.Min(dropDepthPercent.x, dropDepthPercent.y),
                    Mathf.Max(dropDepthPercent.x, dropDepthPercent.y)) * Mathf.Max(0f, depthScale),
            };
        }

        private float CalculateRocketMultiplier(float t)
        {
            var curve = Mathf.Pow(Mathf.Clamp01(t), GetEffectiveGrowthPower());
            var baseMultiplier = Mathf.Exp(Mathf.Log(Mathf.Max(1.01f, _roundCrashMultiplier)) * curve);
            var saw = RocketSawWave(t * Mathf.Max(0f, sawTeethPerSecond) * Mathf.Max(0.25f, _roundCrashAtSeconds) + _roundNoiseSeed * 0.017f);
            var segment = Mathf.Floor(t * Mathf.Max(4f, _roundCrashAtSeconds * 7f));
            var noise = Mathf.PerlinNoise(_roundNoiseSeed + segment * 0.31f, 0.57f) * 2f - 1f;
            var volatility = graphJaggedness * Mathf.Lerp(0.07f, 0.18f, Mathf.Clamp01(t));
            var multiplier = baseMultiplier * (1f + saw * volatility + noise * volatility * 0.45f);

            var drop = GetDropPercent(t);
            multiplier *= 1f - drop;

            if (t > 0.9f)
            {
                var finishBlend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.9f, 1f, t));
                multiplier = Mathf.Lerp(multiplier, _roundCrashMultiplier, finishBlend);
            }

            var maxDropFromLastSample = Mathf.Max(0.16f, _lastDisplayedMultiplier * 0.22f);
            multiplier = Mathf.Max(multiplier, _lastDisplayedMultiplier - maxDropFromLastSample);
            return Mathf.Clamp(multiplier, 1f, _roundCrashMultiplier);
        }

        private float GetEffectiveGrowthPower()
        {
            var power = Mathf.Clamp(graphGrowthPower, 0.2f, 2f);
            return power > 1f ? 1f / power : power;
        }

        private float GetDropPercent(float t)
        {
            var total = 0f;
            for (var i = 0; i < _dropEventCount; i++)
            {
                var dropEvent = _dropEvents[i];
                var local = Mathf.InverseLerp(dropEvent.start01, dropEvent.start01 + dropEvent.duration01, t);
                if (local <= 0f || local >= 1f)
                    continue;

                var attack = Mathf.Clamp(dropAttackShare, 0.05f, 0.5f);
                var shape = local < attack
                    ? Mathf.SmoothStep(0f, 1f, local / attack)
                    : 1f - Mathf.SmoothStep(0f, 1f, (local - attack) / (1f - attack));
                total += Mathf.Max(0f, dropEvent.depth) * shape;
            }

            return Mathf.Clamp01(total);
        }

        private static float RocketSawWave(float phase)
        {
            phase -= Mathf.Floor(phase);
            if (phase < 0.68f)
                return Mathf.Lerp(-0.35f, 1f, phase / 0.68f);

            return Mathf.Lerp(1f, -0.75f, (phase - 0.68f) / 0.32f);
        }

        private void GenerateRoundResult()
        {
            _roundHadPlayer = _betPlaced;
            _forceFirstGuaranteedCrash = false;
            var target = _betPlaced
                ? Mathf.Max(1f, _roundTargetMultiplier)
                : GetRandomMultiplierOption();

            var win = _betPlaced ? RollPlayerOutcome(target) : RollNoBetOutcome(target);
            _roundPlayerWon = _betPlaced && win;
            _roundCrashMultiplier = win
                ? RollWinningCrashMultiplier(target)
                : RollLosingCrashMultiplier(target);
            if (_roundPlayerWon && _forceFirstGuaranteedCrash)
                _roundCrashMultiplier = Mathf.Max(_roundCrashMultiplier, RollFirstGuaranteedCrashMultiplier(target));

            if (_betPlaced)
                Save();
        }

        private bool RollPlayerOutcome(float target)
        {
            if (!_firstBetWinConsumed)
            {
                _firstBetWinConsumed = true;

                if (guaranteeFirstWin && target <= Mathf.Max(1f, firstGuaranteedWinMaxMultiplier))
                {
                    _forceFirstGuaranteedCrash = true;
                    return true;
                }
            }

            if (guaranteeFirstTenMultiplierWin
                && IsTenMultiplier(target)
                && !_firstX10WinConsumed)
            {
                _firstX10WinConsumed = true;
                return true;
            }

            if (IsTenMultiplier(target))
                _firstX10WinConsumed = true;

            return UnityEngine.Random.value < GetPlayerWinChance(target);
        }

        private bool RollNoBetOutcome(float target)
        {
            return UnityEngine.Random.value < GetNoBetWinChance(target);
        }

        private static bool IsTenMultiplier(float target)
        {
            return Mathf.Abs(target - 10f) <= 0.01f;
        }

        private float RollFirstGuaranteedCrashMultiplier(float target)
        {
            var floor = Mathf.Max(target + 0.05f, firstGuaranteedCrashMultiplier);
            var max = Mathf.Max(floor + 0.15f, floor * 1.08f);
            return UnityEngine.Random.Range(floor, max);
        }

        private float RollWinningCrashMultiplier(float target)
        {
            var roll = UnityEngine.Random.value;
            if (roll < nearWinWeight)
                return target + UnityEngine.Random.Range(0.05f, Mathf.Max(0.08f, target * 0.12f));

            if (roll < nearWinWeight + fomoWinWeight)
            {
                var max = Mathf.Max(target + 0.4f, idleChartMaxMultiplier);
                return UnityEngine.Random.Range(target + 0.35f, max);
            }

            return UnityEngine.Random.Range(target + Mathf.Max(0.2f, target * 0.12f), target + Mathf.Max(0.55f, target * 0.55f));
        }

        private float RollLosingCrashMultiplier(float target)
        {
            if (target <= 1.1f)
                return 1f;

            if (UnityEngine.Random.value < nearLoseWeight)
                return Mathf.Max(1.02f, target - UnityEngine.Random.Range(0.05f, Mathf.Max(0.08f, target * 0.16f)));

            return UnityEngine.Random.Range(1.02f, Mathf.Max(1.03f, target - Mathf.Max(0.12f, target * 0.25f)));
        }

        private void PushSample(float time01, float multiplier)
        {
            if (_sampleCount >= _samples.Length)
            {
                _samples[_samples.Length - 1] = new CrashMiniGameChartGraphic.Sample
                {
                    time01 = Mathf.Clamp01(time01),
                    multiplier = Mathf.Max(0f, multiplier),
                };
                return;
            }

            _samples[_sampleCount++] = new CrashMiniGameChartGraphic.Sample
            {
                time01 = Mathf.Clamp01(time01),
                multiplier = Mathf.Max(0f, multiplier),
            };
        }

        private void UpdateChartView()
        {
            if (chart == null)
                return;

            var maxVisible = Mathf.Max(2.25f, GetMaxVisibleMultiplier() + topPaddingMultiplier);
            var minVisible = Mathf.Max(0f, maxVisible - visibleMultiplierSpan);
            if (minVisible < 1f)
                minVisible = 0f;

            chart.SetSamples(_samples, _sampleCount, minVisible, maxVisible);
            UpdateChartCurrentValue();
        }

        private void UpdateChartCurrentValue()
        {
            if (chart == null || _stage == Stage.Betting || _sampleCount <= 0)
            {
                chart?.SetCurrentValue(0f, 0f, false);
                return;
            }

            if (_stage == Stage.Playing && !_crashPeakSamplePushed)
            {
                var currentSample = _samples[Mathf.Max(0, _sampleCount - 1)];
                chart.SetCurrentValue(currentSample.time01, currentSample.multiplier, true);
                return;
            }

            chart.SetCurrentValue(
                Mathf.Clamp01(_roundCrashAtSeconds / GetGraphTimeAxisSeconds()),
                _roundCrashMultiplier,
                true);
        }

        private float GetMaxVisibleMultiplier()
        {
            var max = 1f;
            for (var i = 0; i < _sampleCount; i++)
                max = Mathf.Max(max, _samples[i].multiplier);

            return max;
        }

        private void RefreshAllUi()
        {
            RefreshOptionButtons();
            RefreshDynamicUi();
        }

        private void RefreshDynamicUi()
        {
            if (statusText != null)
                statusText.text = GetStatusText();

            if (potentialWinText != null)
                potentialWinText.text = GameTextFormatter.Format(
                    potentialWinFormat,
                    "{0} руб",
                    FormatRubles(CalculatePayout(GetSelectedStakeRubles(), GetSelectedMultiplier())));

            if (placeBetButtonText != null)
                placeBetButtonText.text = GetPlaceButtonText();

            if (placeBetButton != null)
                placeBetButton.interactable = _stage == Stage.Betting && !_betPlaced;
        }

        private void RefreshOptionButtons()
        {
            var canChange = CanChangeBetOptions();
            for (var i = 0; i < multipliers.Length; i++)
                multipliers[i]?.button?.SetState(i == _selectedMultiplierIndex, canChange);

            for (var i = 0; i < stakes.Length; i++)
                stakes[i]?.button?.SetState(i == _selectedStakeIndex, canChange);
        }

        private string GetStatusText()
        {
            switch (_stage)
            {
                case Stage.Betting:
                    return GameTextFormatter.Format(
                        bettingStatusFormat,
                        "Прием ставок: {0}",
                        GameTextFormatter.CountdownMinutes(_stageTimeLeft));

                case Stage.Playing:
                    return playingStatusText;

                case Stage.ResultPause:
                    if (!_roundHadPlayer)
                        return idleResultStatusText;
                    if (_roundPlayerWon)
                    {
                        return GameTextFormatter.Format(
                            winStatusFormat,
                            "Выигрыш: {0} руб",
                            FormatRubles(CalculatePayout(_roundStake, _roundTargetMultiplier)));
                    }

                    return loseStatusText;

                default:
                    return "";
            }
        }

        private string GetPlaceButtonText()
        {
            if (_stage != Stage.Betting)
                return waitNextRoundText;

            return _betPlaced ? betAcceptedText : placeBetText;
        }

        private bool CanChangeBetOptions()
        {
            return _stage == Stage.Betting && !_betPlaced;
        }

        private bool CanSpend(long rubles)
        {
            return profile != null && rubles >= 0 && profile.Rubles >= rubles;
        }

        private long GetSelectedStakeRubles()
        {
            return IsValidStakeIndex(_selectedStakeIndex) ? Math.Max(0, stakes[_selectedStakeIndex].rubles) : 0;
        }

        private float GetSelectedMultiplier()
        {
            return IsValidMultiplierIndex(_selectedMultiplierIndex)
                ? Mathf.Max(1f, multipliers[_selectedMultiplierIndex].value)
                : 2f;
        }

        private CrashMiniGameOptionButtonUI GetSelectedStakeButton()
        {
            return IsValidStakeIndex(_selectedStakeIndex) ? stakes[_selectedStakeIndex].button : null;
        }

        private float GetRandomMultiplierOption()
        {
            if (multipliers == null || multipliers.Length == 0)
                return 2f;

            var index = UnityEngine.Random.Range(0, multipliers.Length);
            return Mathf.Max(1f, multipliers[index].value);
        }

        private float GetPlayerWinChance(float target)
        {
            var expectedReturn = GetTargetReturnForSelectedStake();
            var chance = expectedReturn / Mathf.Pow(Mathf.Max(1f, target), Mathf.Max(0.75f, multiplierChancePower));
            return Mathf.Clamp(chance, Mathf.Clamp01(minPlayerWinChance), Mathf.Clamp01(maxPlayerWinChance));
        }

        private float GetNoBetWinChance(float target)
        {
            var chance = Mathf.Max(0f, noBetTargetReturn)
                / Mathf.Pow(Mathf.Max(1f, target), Mathf.Max(0.75f, multiplierChancePower));
            return Mathf.Clamp01(chance);
        }

        private float GetTargetReturnForSelectedStake()
        {
            if (targetReturnByStake != null && _selectedStakeIndex >= 0 && _selectedStakeIndex < targetReturnByStake.Length)
                return Mathf.Clamp01(targetReturnByStake[_selectedStakeIndex]);

            return 0.78f;
        }

        private static long CalculatePayout(long stake, float multiplier)
        {
            return Math.Max(0, (long)Math.Round(stake * Math.Max(1f, multiplier)));
        }

        private string FormatRubles(long value)
        {
            return GameTextFormatter.WholeNumber(value, thousandSep);
        }

        private bool IsValidMultiplierIndex(int index)
        {
            return multipliers != null && index >= 0 && index < multipliers.Length && multipliers[index] != null;
        }

        private bool IsValidStakeIndex(int index)
        {
            return stakes != null && index >= 0 && index < stakes.Length && stakes[index] != null;
        }

        private void ResolveReferences()
        {
            if (profile == null)
                profile = FindAnyObjectByType<PlayerProfile>();

            if (placeBetButtonText == null && placeBetButton != null)
                placeBetButtonText = placeBetButton.GetComponentInChildren<TMP_Text>(true);
        }

        private void NormalizeDefaultMultiplierOptions()
        {
            if (multipliers == null || multipliers.Length != 4)
                return;

            if (!ApproximatelyMultiplierSet(2f, 3f, 5f, 10f)
                && !ApproximatelyMultiplierSet(2f, 5f, 10f, 15f))
                return;

            SetMultiplierOption(0, "x2", 2f);
            SetMultiplierOption(1, "x5", 5f);
            SetMultiplierOption(2, "x10", 10f);
            SetMultiplierOption(3, "x20", 20f);
        }

        private bool ApproximatelyMultiplierSet(float a, float b, float c, float d)
        {
            return multipliers[0] != null
                && multipliers[1] != null
                && multipliers[2] != null
                && multipliers[3] != null
                && Mathf.Approximately(multipliers[0].value, a)
                && Mathf.Approximately(multipliers[1].value, b)
                && Mathf.Approximately(multipliers[2].value, c)
                && Mathf.Approximately(multipliers[3].value, d);
        }

        private void SetMultiplierOption(int index, string optionLabel, float optionValue)
        {
            if (multipliers[index] == null)
                multipliers[index] = new MultiplierOption();

            multipliers[index].label = optionLabel;
            multipliers[index].value = optionValue;
        }

        private void SyncChartThresholds()
        {
            if (chart == null || multipliers == null)
                return;

            var count = Mathf.Min(multipliers.Length, _chartThresholdValues.Length);
            for (var i = 0; i < count; i++)
                _chartThresholdValues[i] = multipliers[i] != null ? Mathf.Max(1f, multipliers[i].value) : 1f;

            chart.SetThresholdMultipliers(_chartThresholdValues, count);
        }

        private void OnRublesChanged(long _)
        {
            RefreshAllUi();
        }

        private void Save()
        {
            SaveStorage.SaveJson(SaveKey, new SaveData
            {
                selectedMultiplierIndex = _selectedMultiplierIndex,
                selectedStakeIndex = _selectedStakeIndex,
                firstBetWinConsumed = _firstBetWinConsumed,
                firstX10WinConsumed = _firstX10WinConsumed,
            });
            SaveStorage.Flush();
        }

        private void Load()
        {
            _selectedMultiplierIndex = 0;
            _selectedStakeIndex = 0;

            if (!SaveStorage.TryLoadJson(SaveKey, out SaveData data))
                return;

            _selectedMultiplierIndex = IsValidMultiplierIndex(data.selectedMultiplierIndex)
                ? data.selectedMultiplierIndex
                : 0;
            _selectedStakeIndex = IsValidStakeIndex(data.selectedStakeIndex)
                ? data.selectedStakeIndex
                : 0;
            _firstBetWinConsumed = data.firstBetWinConsumed;
            _firstX10WinConsumed = data.firstX10WinConsumed;
        }
    }
}
