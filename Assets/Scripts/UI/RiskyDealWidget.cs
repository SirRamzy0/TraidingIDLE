using System;
using System.Globalization;
using TMPro;
using TraidingIDLE.Player;
using TraidingIDLE.Saves;
using UnityEngine;
using UnityEngine.UI;

namespace TraidingIDLE.UI
{
    public sealed class RiskyDealWidget : MonoBehaviour
    {
        private enum State
        {
            Offer,
            Analysis,
            Success,
            Fail,
            IdleBetweenOffers,
        }

        [Header("Refs")]
        [SerializeField] private PlayerProfile profile = null!;

        [Header("Dialogs (roots)")]
        [SerializeField] private GameObject dealOfferDialog = null!;
        [SerializeField] private GameObject dealAnalysisDialog = null!;
        [SerializeField] private GameObject dealSuccessDialog = null!;
        [SerializeField] private GameObject dealFailDialog = null!;

        [Header("Offer UI")]
        [SerializeField] private TMP_Text offerChanceText = null!;
        [SerializeField] private TMP_Text offerTimerText = null!;
        [SerializeField] private TMP_Text offerStakeText = null!;
        [SerializeField] private Button offerBetButton = null!;
        [SerializeField] private TMP_Text offerBetButtonLabel = null!;

        [Header("Analysis UI")]
        [SerializeField] private TMP_Text analysisTimerText = null!;
        [SerializeField] private TMP_Text analysisStakeText = null!;
        [SerializeField] private Button analysisWaitButton = null!;

        [Header("Success UI")]
        [SerializeField] private TMP_Text successProfitText = null!;
        [SerializeField] private TMP_Text successStakeText = null!;
        [SerializeField] private Button successClaimButton = null!;
        [SerializeField] private TMP_Text successClaimButtonLabel = null!;

        [Header("Fail UI")]
        [SerializeField] private TMP_Text failStakeText = null!;
        [SerializeField] private Button failClaimButton = null!;

        [Header("Timing")]
        [Tooltip("How often the offer appears (random range).")]
        [SerializeField, Min(1f)] private float offerIntervalMinSeconds = 300f;
        [SerializeField, Min(1f)] private float offerIntervalMaxSeconds = 600f;
        [Tooltip("Time player has to place the bet.")]
        [SerializeField, Min(1f)] private float offerDurationSeconds = 120f;
        [Tooltip("Analysis duration after bet window ends (or immediately after bet).")]
        [SerializeField, Min(1f)] private float analysisDurationSeconds = 30f;
        [Tooltip("If true, the very first cycle starts immediately in Offer (no initial wait).")]
        [SerializeField] private bool startWithImmediateOffer = true;

        [Header("Random ranges")]
        [Range(1f, 99f)]
        [SerializeField] private float chanceMinPercent = 15f;
        [Range(1f, 99f)]
        [SerializeField] private float chanceMaxPercent = 65f;

        [SerializeField, Min(0)] private long stakeMinRubles = 100_000;
        [SerializeField, Min(0)] private long stakeMaxRubles = 5_000_000;

        [Header("Payout multipliers")]
        [Tooltip("If player placed a bet, payout multiplier is picked as: x2(50%), x3(30%), x5(20%).")]
        [SerializeField] private int payoutX2Weight = 50;
        [SerializeField] private int payoutX3Weight = 30;
        [SerializeField] private int payoutX5Weight = 20;

        [Header("Formats")]
        [SerializeField] private string chanceFormat = "{0}";
        [SerializeField] private string offerStakePromptFormat = "Вложить: {0}";
        [SerializeField] private string offerStakeCommittedFormat = "Вложено: {0}";
        [SerializeField] private string analysisStakeFormat = "Вложено: {0}";
        [SerializeField] private string successStakeFormat = "Вложено: {0}";
        [SerializeField] private string profitFormat = "Прибыль: +{0}";

        [Header("Button labels")]
        [SerializeField] private string offerBetEnterLabel = "Войти";
        [SerializeField] private string offerBetWaitingLabel = "Ожидание";
        [SerializeField] private string successClaimLabel = "Получить";
        [SerializeField] private string successClaimedLabel = "Получено";

        private const string SaveKey = "save.risky.v1";

        [Serializable]
        private sealed class SaveData
        {
            public int state;
            public int idleVisibleDialog;
            public float stateTimeLeft;
            public float nextOfferTimeLeft;
            public bool betPlaced;
            public bool fomoNoBet;
            public bool claimTaken;
            public long stakeRubles;
            public long displayStakeRubles;
            public float chance01;
            public long profitRubles;
        }

        private State _state;
        private float _stateTimeLeft;
        private float _nextOfferTimeLeft;
        private State _idleVisibleDialog;

        private bool _betPlaced;
        private bool _fomoNoBet;
        private bool _claimTaken;
        private long _stakeRubles;
        private long _displayStakeRubles;
        private float _chance01;
        private long _profitRubles;

        private void Awake()
        {
            if (profile == null)
                profile = FindFirstObjectByType<PlayerProfile>();

            CacheButtonLabelsIfNeeded();
            ResetOfferBetButtonLabel();
            ResetSuccessClaimButtonLabel();

            if (LoadFromStorage())
            {
                ApplyLoadedLabelsAndUI();
                return;
            }

            if (startWithImmediateOffer)
            {
                // First run: show offer immediately so the widget is always "on screen".
                StartNewOffer();
                return;
            }

            ScheduleNextOffer();
            _idleVisibleDialog = State.Fail;
            SetState(State.IdleBetweenOffers, force: true);
            RefreshAllTexts();
            UpdateButtons();
            SaveToStorage();
        }

        private void OnEnable()
        {
            if (offerBetButton != null) offerBetButton.onClick.AddListener(OnBetClicked);
            if (successClaimButton != null) successClaimButton.onClick.AddListener(OnClaimClicked);

            SyncDialogVisibility();
            RefreshAllTexts();
            UpdateButtons();
        }

        private void OnDisable()
        {
            if (offerBetButton != null) offerBetButton.onClick.RemoveListener(OnBetClicked);
            if (successClaimButton != null) successClaimButton.onClick.RemoveListener(OnClaimClicked);
        }

        private void Update()
        {
            var dt = Time.deltaTime;

            var prevTimerSlot = (int)Mathf.Ceil(_stateTimeLeft);
            var prevNextOfferSlot = (int)Mathf.Ceil(_nextOfferTimeLeft);

            switch (_state)
            {
                case State.IdleBetweenOffers:
                    // If player has a real win to claim, keep success visible forever until claim click.
                    if (!(_idleVisibleDialog == State.Success && _betPlaced && !_claimTaken))
                    {
                        _nextOfferTimeLeft -= dt;
                        if (_nextOfferTimeLeft <= 0f)
                            StartNewOffer();
                    }
                    break;

                case State.Offer:
                    _stateTimeLeft -= dt;
                    if (_stateTimeLeft <= 0f)
                    {
                        _fomoNoBet = !_betPlaced;
                        StartAnalysis();
                    }
                    break;

                case State.Analysis:
                    _stateTimeLeft -= dt;
                    if (_stateTimeLeft <= 0f)
                        Resolve();
                    break;

                case State.Success:
                case State.Fail:
                    break;
            }

            RefreshTimersOnly();

            var newTimerSlot = (int)Mathf.Ceil(_stateTimeLeft);
            var newNextOfferSlot = (int)Mathf.Ceil(_nextOfferTimeLeft);
            if (newTimerSlot != prevTimerSlot || newNextOfferSlot != prevNextOfferSlot)
                SaveToStorage();
        }

        private void ScheduleNextOffer()
        {
            _nextOfferTimeLeft = UnityEngine.Random.Range(
                Mathf.Min(offerIntervalMinSeconds, offerIntervalMaxSeconds),
                Mathf.Max(offerIntervalMinSeconds, offerIntervalMaxSeconds));
        }

        private void StartNewOffer()
        {
            _betPlaced = false;
            _fomoNoBet = false;
            _claimTaken = false;

            _stakeRubles = RollStake();
            _displayStakeRubles = _stakeRubles;
            _chance01 = RollChance01();
            _profitRubles = 0;

            ResetOfferBetButtonLabel();
            ResetSuccessClaimButtonLabel();

            _stateTimeLeft = offerDurationSeconds;
            SetState(State.Offer);
            RefreshAllTexts();
            UpdateButtons();
            SaveToStorage();
        }

        private void StartAnalysis()
        {
            _stateTimeLeft = analysisDurationSeconds;
            SetState(State.Analysis);
            RefreshAllTexts();
            UpdateButtons();
            SaveToStorage();
        }

        private void Resolve()
        {
            var successChance01 = _fomoNoBet ? 0.5f : _chance01;
            var success = UnityEngine.Random.value < successChance01;
            if (success)
            {
                var payoutMultiplier = _fomoNoBet
                    ? RollPayoutMultiplierUniform()
                    : RollPayoutMultiplierWeighted();
                _profitRubles = Math.Max(0, _stakeRubles * (payoutMultiplier - 1));
                _displayStakeRubles = _betPlaced ? _stakeRubles : 0;
                // Start next-cycle timer only if there is no reward to claim.
                if (!_betPlaced)
                    ScheduleNextOffer();
                _idleVisibleDialog = State.Success;
                SetState(State.IdleBetweenOffers);
                UpdateButtons();
                RefreshAllTexts();
                SaveToStorage();
                return;
            }

            _profitRubles = 0;
            _displayStakeRubles = _betPlaced ? _stakeRubles : 0;
            ScheduleNextOffer();
            _idleVisibleDialog = State.Fail;
            SetState(State.IdleBetweenOffers);

            if (failClaimButton != null)
                failClaimButton.interactable = false;

            RefreshAllTexts();
            UpdateButtons();
            SaveToStorage();
        }

        private void OnBetClicked()
        {
            if (_state != State.Offer)
                return;

            if (_betPlaced)
                return;

            if (_stateTimeLeft <= 0f)
                return;

            if (profile == null)
                return;

            if (profile.Rubles < _stakeRubles)
                return;

            profile.AddRubles(-_stakeRubles);
            _betPlaced = true;
            SetOfferBetButtonLabel(offerBetWaitingLabel);
            if (offerBetButton != null)
                offerBetButton.interactable = false;
            UpdateButtons();
            RefreshAllTexts();
            SaveToStorage();
        }

        private void OnClaimClicked()
        {
            if (!IsSuccessClaimable())
                return;

            if (_claimTaken)
                return;

            if (!_betPlaced)
                return;

            if (profile == null)
                return;

            var payout = _stakeRubles + _profitRubles;
            profile.AddRubles(payout);

            _claimTaken = true;
            SetSuccessClaimButtonLabel(successClaimedLabel);
            if (successClaimButton != null)
                successClaimButton.interactable = false;
            ScheduleNextOffer();
            UpdateButtons();
            SaveToStorage();
        }

        private bool IsSuccessClaimable()
        {
            if (_state == State.Success)
                return true;

            return _state == State.IdleBetweenOffers && _idleVisibleDialog == State.Success;
        }

        private void SetState(State s, bool force = false)
        {
            if (!force && _state == s)
                return;

            _state = s;
            SyncDialogVisibility();
        }

        private void SyncDialogVisibility()
        {
            var showOffer = _state == State.Offer;
            var showAnalysis = _state == State.Analysis;
            var showSuccess = _state == State.Success || (_state == State.IdleBetweenOffers && _idleVisibleDialog == State.Success);
            var showFail = _state == State.Fail || (_state == State.IdleBetweenOffers && _idleVisibleDialog == State.Fail);

            SetActiveSafe(dealOfferDialog, showOffer);
            SetActiveSafe(dealAnalysisDialog, showAnalysis);
            SetActiveSafe(dealSuccessDialog, showSuccess);
            SetActiveSafe(dealFailDialog, showFail);
        }

        private void UpdateButtons()
        {
            if (offerBetButton != null)
                offerBetButton.interactable = _state == State.Offer && !_betPlaced && profile != null && profile.Rubles >= _stakeRubles && _stateTimeLeft > 0f;

            if (analysisWaitButton != null)
                analysisWaitButton.interactable = false;

            if (successClaimButton != null)
                successClaimButton.interactable = IsSuccessClaimable() && !_claimTaken && _betPlaced;
        }

        private void RefreshTimersOnly()
        {
            if (_state == State.Offer)
            {
                if (offerTimerText != null)
                    offerTimerText.text = FormatTimerLabel(_stateTimeLeft);
                UpdateButtons();
            }
            else if (_state == State.Analysis)
            {
                if (analysisTimerText != null)
                    analysisTimerText.text = FormatTimerLabel(_stateTimeLeft);
            }
        }

        private void RefreshAllTexts()
        {
            if (offerChanceText != null)
                offerChanceText.text = string.Format(SafeFormat(chanceFormat, "{0}"), Mathf.RoundToInt(_chance01 * 100f));

            if (offerStakeText != null)
            {
                var fmt = _betPlaced ? offerStakeCommittedFormat : offerStakePromptFormat;
                offerStakeText.text = string.Format(SafeFormat(fmt, "{0}"), FormatRubles(_stakeRubles));
            }

            if (offerTimerText != null && _state == State.Offer)
                offerTimerText.text = FormatTimerLabel(_stateTimeLeft);

            if (analysisStakeText != null)
            {
                var analysisStake = _betPlaced ? _stakeRubles : 0;
                analysisStakeText.text = string.Format(SafeFormat(analysisStakeFormat, "{0}"), FormatRubles(analysisStake));
            }

            if (analysisTimerText != null && _state == State.Analysis)
                analysisTimerText.text = FormatTimerLabel(_stateTimeLeft);

            if (successStakeText != null)
                successStakeText.text = string.Format(SafeFormat(successStakeFormat, "{0}"), FormatRubles(_displayStakeRubles));

            if (successProfitText != null)
                successProfitText.text = string.Format(SafeFormat(profitFormat, "{0}"), FormatRubles(_profitRubles));

            if (failStakeText != null)
                failStakeText.text = string.Format(SafeFormat(successStakeFormat, "{0}"), FormatRubles(_displayStakeRubles));
        }

        private long RollStake()
        {
            var min = Math.Min(stakeMinRubles, stakeMaxRubles);
            var max = Math.Max(stakeMinRubles, stakeMaxRubles);
            if (max <= 0)
                return 0;

            if (min == max)
                return QuantizeStakeToStep(min);

            // Unity Random for long range via double.
            var v = UnityEngine.Random.value;
            var rolled = min + (long)Math.Round((max - min) * v);
            return QuantizeStakeToStep(Math.Max(0, rolled));
        }

        private static long QuantizeStakeToStep(long value)
        {
            const long StakeStep = 20_000;
            if (value <= 0)
                return 0;

            var quantized = (long)Math.Round((double)value / StakeStep) * StakeStep;
            return Math.Max(StakeStep, quantized);
        }

        private float RollChance01()
        {
            var min = Mathf.Min(chanceMinPercent, chanceMaxPercent);
            var max = Mathf.Max(chanceMinPercent, chanceMaxPercent);
            var percent = UnityEngine.Random.Range(min, max);
            return Mathf.Clamp01(percent / 100f);
        }

        private int RollPayoutMultiplierWeighted()
        {
            var w2 = Mathf.Max(0, payoutX2Weight);
            var w3 = Mathf.Max(0, payoutX3Weight);
            var w5 = Mathf.Max(0, payoutX5Weight);
            var sum = w2 + w3 + w5;
            if (sum <= 0)
                return 2;

            var roll = UnityEngine.Random.Range(0, sum);
            if (roll < w2) return 2;
            roll -= w2;
            if (roll < w3) return 3;
            return 5;
        }

        private static int RollPayoutMultiplierUniform()
        {
            // Equal chance between x2, x3, x5.
            var roll = UnityEngine.Random.Range(0, 3);
            return roll switch
            {
                0 => 2,
                1 => 3,
                _ => 5,
            };
        }

        private static string FormatRubles(long value)
        {
            return value
                .ToString("N0", CultureInfo.InvariantCulture)
                .Replace(",", ".");
        }

        private static string FormatTimer(float seconds)
        {
            seconds = Mathf.Max(0f, seconds);
            var total = Mathf.CeilToInt(seconds);
            var m = total / 60;
            var s = total % 60;
            return $"{m:00}:{s:00}";
        }

        private static string FormatTimerLabel(float seconds)
        {
            return " " + FormatTimer(seconds);
        }

        private void CacheButtonLabelsIfNeeded()
        {
            if (offerBetButtonLabel == null && offerBetButton != null)
                offerBetButtonLabel = offerBetButton.GetComponentInChildren<TMP_Text>(true);

            if (successClaimButtonLabel == null && successClaimButton != null)
                successClaimButtonLabel = successClaimButton.GetComponentInChildren<TMP_Text>(true);
        }

        private void SetOfferBetButtonLabel(string text)
        {
            if (offerBetButtonLabel != null)
                offerBetButtonLabel.text = text;
        }

        private void ResetOfferBetButtonLabel()
        {
            SetOfferBetButtonLabel(offerBetEnterLabel);
        }

        private void SetSuccessClaimButtonLabel(string text)
        {
            if (successClaimButtonLabel != null)
                successClaimButtonLabel.text = text;
        }

        private void ResetSuccessClaimButtonLabel()
        {
            SetSuccessClaimButtonLabel(successClaimLabel);
        }

        private static void SetActiveSafe(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active)
                go.SetActive(active);
        }

        private static string SafeFormat(string format, string fallback)
        {
            return string.IsNullOrEmpty(format) ? fallback : format;
        }

        private void SaveToStorage()
        {
            var data = new SaveData
            {
                state = (int)_state,
                idleVisibleDialog = (int)_idleVisibleDialog,
                stateTimeLeft = _stateTimeLeft,
                nextOfferTimeLeft = _nextOfferTimeLeft,
                betPlaced = _betPlaced,
                fomoNoBet = _fomoNoBet,
                claimTaken = _claimTaken,
                stakeRubles = _stakeRubles,
                displayStakeRubles = _displayStakeRubles,
                chance01 = _chance01,
                profitRubles = _profitRubles,
            };
            SaveStorage.SaveJson(SaveKey, data);
        }

        private bool LoadFromStorage()
        {
            if (!SaveStorage.TryLoadJson<SaveData>(SaveKey, out var data))
                return false;

            _state = ClampState(data.state);
            _idleVisibleDialog = ClampState(data.idleVisibleDialog);
            _stateTimeLeft = Mathf.Max(0f, data.stateTimeLeft);
            _nextOfferTimeLeft = Mathf.Max(0f, data.nextOfferTimeLeft);
            _betPlaced = data.betPlaced;
            _fomoNoBet = data.fomoNoBet;
            _claimTaken = data.claimTaken;
            _stakeRubles = Math.Max(0, data.stakeRubles);
            _displayStakeRubles = Math.Max(0, data.displayStakeRubles);
            _chance01 = Mathf.Clamp01(data.chance01);
            _profitRubles = Math.Max(0, data.profitRubles);

            return true;
        }

        private static State ClampState(int v)
        {
            if (v < 0 || v > (int)State.IdleBetweenOffers)
                return State.IdleBetweenOffers;
            return (State)v;
        }

        private void ApplyLoadedLabelsAndUI()
        {
            if (_betPlaced)
                SetOfferBetButtonLabel(offerBetWaitingLabel);
            else
                ResetOfferBetButtonLabel();

            if (_claimTaken)
                SetSuccessClaimButtonLabel(successClaimedLabel);
            else
                ResetSuccessClaimButtonLabel();

            SetState(_state, force: true);
            RefreshAllTexts();
            UpdateButtons();
        }
    }
}

