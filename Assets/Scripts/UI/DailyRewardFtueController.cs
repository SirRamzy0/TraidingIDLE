using System;
using System.Collections;
using TraidingIDLE.Monetization;
using TraidingIDLE.Saves;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TraidingIDLE.UI
{
    public sealed class DailyRewardFtueController : MonoBehaviour
    {
        private const string SaveKey = "save.ftue.daily_reward.v1";

        private static bool _sessionRegisteredThisLaunch;

        [Header("Rules")]
        [SerializeField] private bool showOnlyUntilFirstDailyClaim = true;
        [SerializeField, Min(0f)] private float firstSessionDelaySeconds = 600f;
        [SerializeField, Min(0.05f)] private float refreshIntervalSeconds = 0.25f;

        [Header("References")]
        [SerializeField] private ShopController shopController;
        [SerializeField] private Button shopButtonOverride;
        [SerializeField] private RectTransform shopButtonAnchorOverride;
        [SerializeField] private Canvas handCanvasOverride;
        [SerializeField] private Sprite handSprite;

        [Header("Placement")]
        [SerializeField] private Vector2 handSize = new(96f, 96f);
        [SerializeField] private Vector2 shopButtonOffset = new(48f, -44f);
        [SerializeField] private Vector2 dailyRewardOffset = new(44f, -34f);
        [SerializeField] private Vector2 pressStartOffset = new(18f, -18f);

        [Header("Animation")]
        [SerializeField, Min(0.01f)] private float fadeInSeconds = 0.12f;
        [SerializeField, Min(0.01f)] private float pressSeconds = 0.42f;
        [SerializeField, Min(0.01f)] private float fadeOutSeconds = 0.16f;
        [SerializeField, Min(0f)] private float pauseSeconds = 0.34f;
        [SerializeField, Range(0.5f, 1f)] private float pressedScale = 0.86f;
        [SerializeField] private AnimationCurve pressCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Debug")]
        [SerializeField] private bool resetFtueSaveOnStart;

        private SaveData _save = new();
        private float _sessionStartedAt;
        private Coroutine _bindRoutine;
        private Coroutine _refreshRoutine;
        private Coroutine _animationRoutine;
        private RectTransform _currentTarget;
        private Vector2 _currentOffset;
        private Canvas _currentCanvas;
        private RectTransform _handRect;
        private CanvasGroup _handCanvasGroup;
        private bool _shopSubscribed;

        [Serializable]
        private sealed class SaveData
        {
            public int sessionsStarted;
            public bool completed;
            public bool shopPromptCompleted;
        }

        private void Awake()
        {
            _sessionStartedAt = Time.unscaledTime;

            if (resetFtueSaveOnStart)
            {
                SaveStorage.DeleteKey(SaveKey);
                SaveStorage.Flush();
            }

            Load();
            RegisterSessionOnce();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            _bindRoutine = StartCoroutine(BindAfterFrame());
            _refreshRoutine = StartCoroutine(RefreshRoutine());
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            UnsubscribeShop();

            if (_bindRoutine != null)
            {
                StopCoroutine(_bindRoutine);
                _bindRoutine = null;
            }

            if (_refreshRoutine != null)
            {
                StopCoroutine(_refreshRoutine);
                _refreshRoutine = null;
            }

            HideHand();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_bindRoutine != null)
                StopCoroutine(_bindRoutine);

            _bindRoutine = StartCoroutine(BindAfterFrame());
        }

        private IEnumerator BindAfterFrame()
        {
            yield return null;
            BindShopController();
            EvaluateNow();
            _bindRoutine = null;
        }

        private IEnumerator RefreshRoutine()
        {
            var wait = new WaitForSecondsRealtime(Mathf.Max(0.05f, refreshIntervalSeconds));

            while (true)
            {
                EvaluateNow();
                yield return wait;
            }
        }

        private void BindShopController()
        {
            var found = shopController != null
                ? shopController
                : FindFirstObjectByType<ShopController>(FindObjectsInactive.Include);

            if (found == shopController)
            {
                SubscribeShop();
                return;
            }

            UnsubscribeShop();
            shopController = found;
            SubscribeShop();
        }

        private void SubscribeShop()
        {
            if (shopController == null || _shopSubscribed)
                return;

            shopController.ShopOpened += OnShopOpened;
            shopController.ShopClosed += EvaluateNow;
            shopController.DailyRewardsChanged += EvaluateNow;
            shopController.DailyRewardClaimed += OnDailyRewardClaimed;
            _shopSubscribed = true;
        }

        private void UnsubscribeShop()
        {
            if (shopController == null || !_shopSubscribed)
                return;

            shopController.ShopOpened -= OnShopOpened;
            shopController.ShopClosed -= EvaluateNow;
            shopController.DailyRewardsChanged -= EvaluateNow;
            shopController.DailyRewardClaimed -= OnDailyRewardClaimed;
            _shopSubscribed = false;
        }

        private void EvaluateNow()
        {
            if (!CanShowFtue())
            {
                HideHand();
                return;
            }

            if (shopController.IsShopOpen)
            {
                CompleteShopPrompt();

                var dailyButton = shopController.GetClaimableDailyRewardButton();
                ShowAt(dailyButton != null ? dailyButton.transform as RectTransform : null, dailyRewardOffset);
                return;
            }

            if (_save.shopPromptCompleted)
            {
                HideHand();
                return;
            }

            var shopTarget = shopButtonAnchorOverride != null
                ? shopButtonAnchorOverride
                : shopButtonOverride != null
                    ? shopButtonOverride.transform as RectTransform
                    : shopController.ShopOpenButton != null
                        ? shopController.ShopOpenButton.transform as RectTransform
                        : null;

            ShowAt(shopTarget, shopButtonOffset);
        }

        private void OnShopOpened()
        {
            if (CanShowFtue())
                CompleteShopPrompt();

            EvaluateNow();
        }

        private void CompleteShopPrompt()
        {
            if (_save.shopPromptCompleted)
                return;

            _save.shopPromptCompleted = true;
            Save();
        }

        private bool CanShowFtue()
        {
            if (showOnlyUntilFirstDailyClaim && _save.completed)
                return false;

            if (shopController == null)
            {
                BindShopController();
                if (shopController == null)
                    return false;
            }

            if (!IsUnlockedBySessionRule())
                return false;

            return shopController.IsDailyRewardClaimAvailable();
        }

        private bool IsUnlockedBySessionRule()
        {
            if (_save.sessionsStarted >= 2)
                return true;

            return Time.unscaledTime - _sessionStartedAt >= Mathf.Max(0f, firstSessionDelaySeconds);
        }

        private void ShowAt(RectTransform target, Vector2 offset)
        {
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                HideHand();
                return;
            }

            _currentTarget = target;
            _currentOffset = offset;

            EnsureHandCreated(target);

            if (_handRect == null)
                return;

            _handRect.gameObject.SetActive(true);
            _handRect.SetAsLastSibling();

            if (_animationRoutine == null)
                _animationRoutine = StartCoroutine(AnimateHand());
        }

        private void EnsureHandCreated(RectTransform target)
        {
            var canvas = handCanvasOverride != null
                ? handCanvasOverride
                : target.GetComponentInParent<Canvas>();

            if (canvas == null)
                canvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Exclude);

            if (canvas == null || handSprite == null)
                return;

            if (_handRect != null && _currentCanvas == canvas)
                return;

            if (_handRect != null)
                Destroy(_handRect.gameObject);

            _currentCanvas = canvas;

            var go = new GameObject("Daily Reward FTUE Hand", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            go.transform.SetParent(canvas.transform, false);
            go.SetActive(false);

            _handRect = go.GetComponent<RectTransform>();
            _handRect.anchorMin = new Vector2(0.5f, 0.5f);
            _handRect.anchorMax = new Vector2(0.5f, 0.5f);
            _handRect.pivot = new Vector2(0.5f, 0.5f);
            _handRect.sizeDelta = handSize;

            _handCanvasGroup = go.GetComponent<CanvasGroup>();
            _handCanvasGroup.blocksRaycasts = false;
            _handCanvasGroup.interactable = false;

            var image = go.GetComponent<Image>();
            image.sprite = handSprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        private IEnumerator AnimateHand()
        {
            var totalDuration = Mathf.Max(0.01f, fadeInSeconds + pressSeconds + fadeOutSeconds + pauseSeconds);
            var cycleTime = 0f;

            while (_handRect != null && _currentTarget != null)
            {
                if (_save.completed
                    || shopController == null
                    || !_currentTarget.gameObject.activeInHierarchy)
                {
                    HideHand();
                    yield break;
                }

                cycleTime += Time.unscaledDeltaTime;
                while (cycleTime > totalDuration)
                    cycleTime -= totalDuration;

                ApplyAnimation(cycleTime);
                yield return null;
            }

            HideHand();
        }

        private void ApplyAnimation(float cycleTime)
        {
            var alpha = 1f;
            var press01 = 0f;
            var scale = 1f;

            if (cycleTime < fadeInSeconds)
            {
                alpha = Mathf.InverseLerp(0f, Mathf.Max(0.01f, fadeInSeconds), cycleTime);
            }
            else if (cycleTime < fadeInSeconds + pressSeconds)
            {
                var t = Mathf.InverseLerp(
                    fadeInSeconds,
                    fadeInSeconds + Mathf.Max(0.01f, pressSeconds),
                    cycleTime);

                press01 = pressCurve != null ? pressCurve.Evaluate(t) : t;
                scale = Mathf.Lerp(1f, pressedScale, press01);
            }
            else if (cycleTime < fadeInSeconds + pressSeconds + fadeOutSeconds)
            {
                var t = Mathf.InverseLerp(
                    fadeInSeconds + pressSeconds,
                    fadeInSeconds + pressSeconds + Mathf.Max(0.01f, fadeOutSeconds),
                    cycleTime);

                alpha = 1f - t;
                press01 = 1f;
                scale = pressedScale;
            }
            else
            {
                alpha = 0f;
                press01 = 1f;
                scale = pressedScale;
            }

            UpdateHandPosition(Vector2.LerpUnclamped(pressStartOffset, Vector2.zero, press01));

            if (_handCanvasGroup != null)
                _handCanvasGroup.alpha = alpha;

            if (_handRect != null)
                _handRect.localScale = Vector3.one * scale;
        }

        private void UpdateHandPosition(Vector2 animationOffset)
        {
            if (_handRect == null || _currentTarget == null || _currentCanvas == null)
                return;

            var canvasRect = _currentCanvas.transform as RectTransform;
            if (canvasRect == null)
                return;

            var camera = _currentCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : _currentCanvas.worldCamera;

            var targetWorld = _currentTarget.TransformPoint(_currentTarget.rect.center);
            var screenPoint = RectTransformUtility.WorldToScreenPoint(camera, targetWorld);

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, camera, out var localPoint))
                return;

            _handRect.anchoredPosition = localPoint + _currentOffset + animationOffset;
        }

        private void HideHand()
        {
            _currentTarget = null;

            if (_animationRoutine != null)
            {
                StopCoroutine(_animationRoutine);
                _animationRoutine = null;
            }

            if (_handCanvasGroup != null)
                _handCanvasGroup.alpha = 0f;

            if (_handRect != null)
                _handRect.gameObject.SetActive(false);
        }

        private void OnDailyRewardClaimed()
        {
            if (showOnlyUntilFirstDailyClaim)
            {
                _save.completed = true;
                Save();
            }

            HideHand();
        }

        private void RegisterSessionOnce()
        {
            if (_sessionRegisteredThisLaunch)
                return;

            _sessionRegisteredThisLaunch = true;
            _save.sessionsStarted = Mathf.Max(0, _save.sessionsStarted) + 1;
            Save();
        }

        private void Load()
        {
            _save = SaveStorage.TryLoadJson<SaveData>(SaveKey, out var data) && data != null
                ? data
                : new SaveData();
        }

        private void Save()
        {
            SaveStorage.SaveJson(SaveKey, _save);
            SaveStorage.Flush();
        }
    }
}
