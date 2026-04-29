using System;
using System.Globalization;
using TMPro;
using TraidingIDLE.Player;
using UnityEngine;
using UnityEngine.UI;

namespace TraidingIDLE.UI
{
    public sealed class RiskyDealWidget : MonoBehaviour
    {
        private enum State
        {
            WaitingForNextOffer,
            Offer,
            Analysis,
            Success,
            Fail,
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

        [Header("Analysis UI")]
        [SerializeField] private TMP_Text analysisTimerText = null!;
        [SerializeField] private TMP_Text analysisStakeText = null!;

        [Header("Success UI")]
        [SerializeField] private TMP_Text successProfitText = null!;
        [SerializeField] private TMP_Text successStakeText = null!;
        [SerializeField] private Button successClaimButton = null!;

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
        [Tooltip("How long to keep result visible before next offer cycle.")]
        [SerializeField, Min(0f)] private float resultHoldSeconds = 4f;

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
        [SerializeField] private string chanceFormat = "Шанс: {0}%";
        [SerializeField] private string stakeFormat = "Вложить: {0}";
        [SerializeField] private string stakeFormatShort = "{0}";
        [SerializeField] private string profitFormat = "Прибыль: +{0}";

        private State _state;
        private float _stateTimeLeft;
        private float _nextOfferTimeLeft;

        private bool _betPlaced;
        private bool _fomoNoBet;
        private bool _claimTaken;
        private long _stakeRubles;
        private float _chance01;
        private long _profitRubles;

        private void Awake()
        {
            if (profile == null)
                profile = FindFirstObjectByType<PlayerProfile>();

            SetState(State.WaitingForNextOffer, force: true);
        }

        private void OnEnable()
        {
            if (offerBetButton != null) offerBetButton.onClick.AddListener(OnBetClicked);
            if (successClaimButton != null) successClaimButton.onClick.AddListener(OnClaimClicked);

            SyncDialogVisibility();
            RefreshAllTexts();
        }

        private void OnDisable()
        {
            if (offerBetButton != null) offerBetButton.onClick.RemoveListener(OnBetClicked);
            if (successClaimButton != null) successClaimButton.onClick.RemoveListener(OnClaimClicked);
        }

        private void Update()
        {
            var dt = Time.deltaTime;

            switch (_state)
            {
                case State.WaitingForNextOffer:
                    _nextOfferTimeLeft -= dt;
                    if (_nextOfferTimeLeft <= 0f)
                        StartNewOffer();
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
                    if (resultHoldSeconds > 0f)
                    {
                        _stateTimeLeft -= dt;
                        if (_stateTimeLeft <= 0f)
                            StartWaitingForNextOffer();
                    }
                    break;
            }

            RefreshTimersOnly();
        }

        private void StartWaitingForNextOffer()
        {
            _betPlaced = false;
            _fomoNoBet = false;
            _claimTaken = false;
            _stakeRubles = 0;
            _profitRubles = 0;
            _chance01 = 0f;

            _nextOfferTimeLeft = UnityEngine.Random.Range(
                Mathf.Min(offerIntervalMinSeconds, offerIntervalMaxSeconds),
                Mathf.Max(offerIntervalMinSeconds, offerIntervalMaxSeconds));

            SetState(State.WaitingForNextOffer);
        }

        private void StartNewOffer()
        {
            _betPlaced = false;
            _fomoNoBet = false;
            _claimTaken = false;

            _stakeRubles = RollStake();
            _chance01 = RollChance01();
            _profitRubles = 0;

            _stateTimeLeft = offerDurationSeconds;
            SetState(State.Offer);
            RefreshAllTexts();
            UpdateButtons();
        }

        private void StartAnalysis()
        {
            _stateTimeLeft = analysisDurationSeconds;
            SetState(State.Analysis);
            RefreshAllTexts();
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
                _stateTimeLeft = resultHoldSeconds;
                SetState(State.Success);
                UpdateButtons();
                RefreshAllTexts();
                return;
            }

            _profitRubles = 0;
            _stateTimeLeft = resultHoldSeconds;
            SetState(State.Fail);

            if (failClaimButton != null)
                failClaimButton.interactable = false;

            RefreshAllTexts();
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
            UpdateButtons();
            RefreshAllTexts();
        }

        private void OnClaimClicked()
        {
            if (_state != State.Success)
                return;

            if (_claimTaken)
                return;

            if (profile == null)
                return;

            var payout = _stakeRubles + _profitRubles;
            profile.AddRubles(payout);

            _claimTaken = true;
            UpdateButtons();

            if (resultHoldSeconds <= 0f)
                StartWaitingForNextOffer();
        }

        private void SetState(State s, bool force = false)
        {
            if (!force && _state == s)
                return;

            _state = s;
            SyncDialogVisibility();

            if (_state == State.WaitingForNextOffer && _nextOfferTimeLeft <= 0f)
                StartWaitingForNextOffer();
        }

        private void SyncDialogVisibility()
        {
            SetActiveSafe(dealOfferDialog, _state == State.Offer);
            SetActiveSafe(dealAnalysisDialog, _state == State.Analysis);
            SetActiveSafe(dealSuccessDialog, _state == State.Success);
            SetActiveSafe(dealFailDialog, _state == State.Fail);
        }

        private void UpdateButtons()
        {
            if (offerBetButton != null)
                offerBetButton.interactable = _state == State.Offer && !_betPlaced && profile != null && profile.Rubles >= _stakeRubles && _stateTimeLeft > 0f;

            if (successClaimButton != null)
                successClaimButton.interactable = _state == State.Success && !_claimTaken && _betPlaced;
        }

        private void RefreshTimersOnly()
        {
            if (_state == State.Offer)
            {
                if (offerTimerText != null)
                    offerTimerText.text = FormatTimer(_stateTimeLeft);
                UpdateButtons();
            }
            else if (_state == State.Analysis)
            {
                if (analysisTimerText != null)
                    analysisTimerText.text = FormatTimer(_stateTimeLeft);
            }
        }

        private void RefreshAllTexts()
        {
            if (offerChanceText != null)
                offerChanceText.text = string.Format(SafeFormat(chanceFormat, "{0}%"), Mathf.RoundToInt(_chance01 * 100f));

            if (offerStakeText != null)
                offerStakeText.text = string.Format(SafeFormat(stakeFormat, "{0}"), FormatRubles(_stakeRubles));

            if (offerTimerText != null && _state == State.Offer)
                offerTimerText.text = FormatTimer(_stateTimeLeft);

            if (analysisStakeText != null)
                analysisStakeText.text = string.Format(SafeFormat(stakeFormatShort, "{0}"), FormatRubles(_stakeRubles));

            if (analysisTimerText != null && _state == State.Analysis)
                analysisTimerText.text = FormatTimer(_stateTimeLeft);

            if (successStakeText != null)
                successStakeText.text = string.Format(SafeFormat(stakeFormatShort, "{0}"), FormatRubles(_stakeRubles));

            if (successProfitText != null)
                successProfitText.text = string.Format(SafeFormat(profitFormat, "{0}"), FormatRubles(_profitRubles));

            if (failStakeText != null)
                failStakeText.text = string.Format(SafeFormat(stakeFormatShort, "{0}"), FormatRubles(_stakeRubles));
        }

        private long RollStake()
        {
            var min = Math.Min(stakeMinRubles, stakeMaxRubles);
            var max = Math.Max(stakeMinRubles, stakeMaxRubles);
            if (max <= 0)
                return 0;

            if (min == max)
                return min;

            // Unity Random for long range via double.
            var v = UnityEngine.Random.value;
            var rolled = min + (long)Math.Round((max - min) * v);
            return Math.Max(0, rolled);
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

        private static void SetActiveSafe(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active)
                go.SetActive(active);
        }

        private static string SafeFormat(string format, string fallback)
        {
            return string.IsNullOrEmpty(format) ? fallback : format;
        }
    }
}

