using System.Collections;
using TMPro;
using TraidingIDLE.Monetization;
using UnityEngine;
using UnityEngine.UI;

#if InterstitialAdv_yg
namespace TraidingIDLE.Integrations
{
    public sealed class YandexInterstitialPauseController : MonoBehaviour
    {
        private const string DefaultOverlayResourceName = "YandexInterstitialPauseOverlay";

        private YandexInterstitialPauseSettings _settings;
        private GameObject _overlayInstance;
        private TMP_Text _messageText;
        private float _nextInterstitialTime;
        private bool _waitingForAd;
        private bool _adOpened;
        private bool _pausedByController;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var existing = FindAnyObjectByType<YandexInterstitialPauseController>();
            if (existing != null)
                return;

            var go = new GameObject(nameof(YandexInterstitialPauseController));
            DontDestroyOnLoad(go);
            go.AddComponent<YandexInterstitialPauseController>();
        }

        private void Awake()
        {
            _settings = YandexInterstitialPauseSettings.Load();
            ApplyPluginInterstitialInterval();
            ScheduleNextInterstitial(_settings.FirstInterstitialDelaySeconds);
            CreateOverlay();
            HideOverlay();
        }

        private void OnEnable()
        {
            YG.YG2.onOpenInterAdv += OnInterstitialOpen;
            YG.YG2.onCloseInterAdv += ResumeAfterAd;
            YG.YG2.onErrorInterAdv += ResumeAfterAd;
        }

        private void OnDisable()
        {
            YG.YG2.onOpenInterAdv -= OnInterstitialOpen;
            YG.YG2.onCloseInterAdv -= ResumeAfterAd;
            YG.YG2.onErrorInterAdv -= ResumeAfterAd;
            if (_pausedByController)
                ResumeAfterAd();
        }

        private void Update()
        {
            if (MonetizationState.NoAdsPurchased)
                return;

            if (_waitingForAd || YG.YG2.nowAdsShow || YG.YG2.isPauseGame)
                return;

            if (Time.unscaledTime >= _nextInterstitialTime)
                StartCoroutine(ShowInterstitialWithCountdown());
        }

        private IEnumerator ShowInterstitialWithCountdown()
        {
            if (MonetizationState.NoAdsPurchased)
                yield break;

            _waitingForAd = true;
            _pausedByController = true;
            YG.YG2.PauseGame(true);
            ShowOverlay();

            for (var secondsLeft = _settings.CountdownSeconds; secondsLeft > 0; secondsLeft--)
            {
                SetMessage(secondsLeft);
                yield return new WaitForSecondsRealtime(1f);
            }

            HideOverlay();
            _adOpened = false;
            YG.YG2.InterstitialAdvShow();
            StartCoroutine(ResumeIfInterstitialDidNotOpen());
        }

        private IEnumerator ResumeIfInterstitialDidNotOpen()
        {
            yield return new WaitForSecondsRealtime(8f);

            if (_waitingForAd && !_adOpened && !YG.YG2.nowInterAdv)
                ResumeAfterAd();
        }

        private void OnInterstitialOpen()
        {
            _adOpened = true;
        }

        private void ResumeAfterAd()
        {
            HideOverlay();
            ScheduleNextInterstitial(_settings.RepeatInterstitialDelaySeconds);
            _waitingForAd = false;
            _adOpened = false;

            if (!_pausedByController)
                return;

            _pausedByController = false;
            YG.YG2.PauseGame(false);
        }

        private void ScheduleNextInterstitial(float delaySeconds)
        {
            _nextInterstitialTime = Time.unscaledTime + Mathf.Max(1f, delaySeconds);
        }

        private void ApplyPluginInterstitialInterval()
        {
            YG.YG2.infoYG.InterstitialAdv.interAdvInterval = Mathf.Max(1, Mathf.FloorToInt(_settings.RepeatInterstitialDelaySeconds));
        }

        private void CreateOverlay()
        {
            var prefab = _settings != null && _settings.pauseOverlayPrefab != null
                ? _settings.pauseOverlayPrefab
                : Resources.Load<GameObject>(DefaultOverlayResourceName);

            if (prefab != null)
            {
                _overlayInstance = Instantiate(prefab, transform, false);
                _overlayInstance.name = prefab.name;

                var canvas = _overlayInstance.GetComponentInChildren<Canvas>(true);
                if (canvas != null)
                    canvas.sortingOrder = short.MaxValue;

                _messageText = FindMessageText(_overlayInstance.transform);
                return;
            }

            CreateFallbackOverlay();
        }

        private static TMP_Text FindMessageText(Transform root)
        {
            if (root == null)
                return null;

            var children = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < children.Length; i++)
            {
                var child = children[i];
                if (child == null || child.name != "Message")
                    continue;

                var text = child.GetComponent<TMP_Text>();
                if (text != null)
                    return text;
            }

            return root.GetComponentInChildren<TMP_Text>(true);
        }

        private void CreateFallbackOverlay()
        {
            var canvasGo = new GameObject("InterstitialPauseOverlay", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            _overlayInstance = canvasGo;

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            var shade = new GameObject("Shade", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            shade.transform.SetParent(canvasGo.transform, false);
            var shadeRect = (RectTransform)shade.transform;
            shadeRect.anchorMin = Vector2.zero;
            shadeRect.anchorMax = Vector2.one;
            shadeRect.offsetMin = Vector2.zero;
            shadeRect.offsetMax = Vector2.zero;
            shade.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);

            var textGo = new GameObject("Message", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(shade.transform, false);
            var textRect = (RectTransform)textGo.transform;
            textRect.anchorMin = new Vector2(0.08f, 0.45f);
            textRect.anchorMax = new Vector2(0.92f, 0.55f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            _messageText = textGo.GetComponent<TextMeshProUGUI>();
            _messageText.alignment = TextAlignmentOptions.Center;
            _messageText.enableWordWrapping = true;
            _messageText.fontSize = 42f;
            _messageText.color = Color.white;
        }

        private void ShowOverlay()
        {
            if (_overlayInstance != null)
                _overlayInstance.SetActive(true);
        }

        private void HideOverlay()
        {
            if (_overlayInstance != null)
                _overlayInstance.SetActive(false);
        }

        private void SetMessage(int secondsLeft)
        {
            if (_messageText == null)
                return;

            _messageText.text = $"Игра приостановлена. Реклама через {secondsLeft} сек";
        }
    }
}
#endif
