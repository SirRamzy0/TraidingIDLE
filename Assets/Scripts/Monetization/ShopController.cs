using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using TraidingIDLE.Analytics;
using TraidingIDLE.Player;
using TraidingIDLE.Saves;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TraidingIDLE.Monetization
{
    public sealed class ShopController : MonoBehaviour
    {
        private const string StarterPackId = "starter_pack_big";
        private const string NoAdsId = "no_ads";
        private const string DailyRewardSaveKey = "save.shop.daily_rewards.v1";

        private static ShopController _instance;
        private static readonly long[] DailyGemRewards = { 25, 35, 50, 75, 80, 95, 150 };

        [Serializable]
        public sealed class ShopRewardEntry
        {
            [Header("Yandex Product")]
            public string productId;

            [Header("Reward")]
            public int gems;
            public string rubles;
            public bool consumable = true;

            [Header("Purchase Button")]
            public Button purchaseButton;
            public Button cardButton;
            public bool wholeCardClickable = true;

            [Header("Price UI")]
            public TMP_Text priceText;
            public bool useYandexPrice = true;
            public string fallbackPriceText;
        }

        [Header("Shop Rewards")]
        [SerializeField]
        private List<ShopRewardEntry> shopRewards = new()
        {
            new ShopRewardEntry { productId = StarterPackId, gems = 20000, rubles = "50900000000", consumable = true },
            new ShopRewardEntry { productId = "gems_500", gems = 500, rubles = "0", consumable = true },
            new ShopRewardEntry { productId = "gems_2000", gems = 2000, rubles = "0", consumable = true },
            new ShopRewardEntry { productId = "gems_6500", gems = 6500, rubles = "0", consumable = true },
            new ShopRewardEntry { productId = "gems_12000", gems = 12000, rubles = "0", consumable = true },
            new ShopRewardEntry { productId = "rubles_pack_1", gems = 0, rubles = "50000000", consumable = true },
            new ShopRewardEntry { productId = "rubles_pack_2", gems = 0, rubles = "250000000", consumable = true },
            new ShopRewardEntry { productId = "rubles_pack_3", gems = 0, rubles = "1000000000", consumable = true },
            new ShopRewardEntry { productId = "rubles_pack_4", gems = 0, rubles = "5000000000", consumable = true },
            new ShopRewardEntry { productId = NoAdsId, gems = 0, rubles = "0", consumable = false },
        };

        [Header("Daily Rewards UI")]
        [SerializeField] private string dailyClaimAvailableText = "Получить";
        [SerializeField] private string dailyClaimedText = "Получено";
        [SerializeField] private string dailyLockedText = "Скоро";
        [SerializeField] private string dailyNextRewardCountdownFormat = "{0}";
        [SerializeField] private Color dailyAvailableButtonColor = new Color(0.23f, 0.78f, 0.34f, 1f);
        [SerializeField] private Color dailyClaimedButtonColor = new Color(0.43f, 0.48f, 0.52f, 1f);
        [SerializeField] private Color dailyNextRewardButtonColor = new Color(0.95f, 0.66f, 0.20f, 1f);
        [SerializeField] private Color dailyLockedButtonColor = new Color(0.30f, 0.32f, 0.35f, 1f);

        private readonly Dictionary<string, ProductReward> _rewards = new();

        private readonly List<ButtonBinding> _boundButtons = new();
        private readonly List<TMP_Text> _gemTexts = new();
        private readonly List<Button> _noAdsButtons = new();
        private readonly List<DailyRewardView> _dailyRewardViews = new();

        private PlayerProfile _profile;
        private GameObject _shopDialog;
        private Button _shopOpenButton;
        private Coroutine _dailyRewardRefreshRoutine;

        public event Action ShopOpened;
        public event Action ShopClosed;
        public event Action DailyRewardsChanged;
        public event Action DailyRewardClaimed;

        public bool IsShopOpen => _shopDialog != null && _shopDialog.activeInHierarchy;
        public Button ShopOpenButton => _shopOpenButton;

        [Serializable]
        private sealed class DailyRewardSaveData
        {
            public long lastClaimDay = -1;
            public int nextRewardIndex;
            public int claimedInCycle;
        }

        private readonly struct ProductReward
        {
            public ProductReward(long gems, long rubles, bool consumable)
            {
                Gems = Math.Max(0, gems);
                Rubles = Math.Max(0, rubles);
                Consumable = consumable;
            }

            public long Gems { get; }
            public long Rubles { get; }
            public bool Consumable { get; }
        }

        private readonly struct ButtonBinding
        {
            public ButtonBinding(Button button, UnityEngine.Events.UnityAction action)
            {
                Button = button;
                Action = action;
            }

            public Button Button { get; }
            public UnityEngine.Events.UnityAction Action { get; }
        }

        private sealed class DailyRewardView
        {
            public int Index;
            public Button Button;
            public TMP_Text RewardText;
            public TMP_Text ButtonText;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            RebuildRewards();
        }

        private void OnValidate()
        {
            if (Application.isPlaying)
                RebuildRewards();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;

#if Payments_yg
            YG.YG2.onPurchaseSuccess += OnPurchaseSuccess;
            YG.YG2.onPurchaseFailed += OnPurchaseFailed;
            YG.YG2.onGetPayments += RefreshPriceTexts;
            StartCoroutine(ConsumeUnprocessedPurchasesAfterStart());
#endif

            StartCoroutine(BindAfterFrame());

            if (_dailyRewardRefreshRoutine == null)
                _dailyRewardRefreshRoutine = StartCoroutine(DailyRewardRefreshRoutine());
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;

#if Payments_yg
            YG.YG2.onPurchaseSuccess -= OnPurchaseSuccess;
            YG.YG2.onPurchaseFailed -= OnPurchaseFailed;
            YG.YG2.onGetPayments -= RefreshPriceTexts;
#endif

            UnbindButtons();
            UnbindProfile();

            if (_dailyRewardRefreshRoutine != null)
            {
                StopCoroutine(_dailyRewardRefreshRoutine);
                _dailyRewardRefreshRoutine = null;
            }
        }

#if Payments_yg
        private IEnumerator ConsumeUnprocessedPurchasesAfterStart()
        {
            yield return null;
            yield return null;

            RefreshPriceTexts();

            // Обрабатывает зависшие покупки и вызывает onPurchaseSuccess.
            YG.YG2.ConsumePurchases(true);
        }
#endif

        private void RebuildRewards()
        {
            _rewards.Clear();

            foreach (var entry in shopRewards)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.productId))
                    continue;

                var productId = entry.productId.Trim();
                var rubles = ParseLong(entry.rubles);

                _rewards[productId] = new ProductReward(entry.gems, rubles, entry.consumable);
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            StartCoroutine(BindAfterFrame());
        }

        private IEnumerator BindAfterFrame()
        {
            yield return null;
            BindCurrentScene();
        }

        private void BindCurrentScene()
        {
            UnbindButtons();
            UnbindProfile();
            _shopOpenButton = null;

            _profile = FindFirstObjectByType<PlayerProfile>();

            if (_profile != null)
                _profile.GemsChanged += OnGemsChanged;

            _shopDialog = FindSceneObject("Shop_dialog");

            if (_shopDialog != null)
                _shopDialog.SetActive(false);

            BindOpenButton();
            BindCloseButton();
            BindPurchaseButtons();
            BindDailyRewards();
            BindGemTexts();

            RefreshGems();
            RefreshDailyRewards();
            RefreshNoAdsButtons();
            RefreshPriceTexts();
        }

        private void BindOpenButton()
        {
            var buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var button in buttons)
            {
                if (!button.gameObject.scene.IsValid())
                    continue;

                var path = GetPath(button.transform);

                if (!path.Contains("Top_conteiner/Profile_conteiner/Profile_container", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (_shopOpenButton == null)
                    _shopOpenButton = button;

                AddButtonListener(button, OpenShop);
            }
        }

        private void BindCloseButton()
        {
            if (_shopDialog == null)
                return;

            foreach (var button in _shopDialog.GetComponentsInChildren<Button>(true))
            {
                if (!button.name.Contains("Close", StringComparison.OrdinalIgnoreCase))
                    continue;

                AddButtonListener(button, CloseShop);
            }
        }

        private void BindPurchaseButtons()
        {
            foreach (var entry in shopRewards)
            {
                if (entry == null
                    || entry.purchaseButton == null
                    || string.IsNullOrWhiteSpace(entry.productId))
                    continue;

                var productId = entry.productId.Trim();

                BindPurchaseButton(entry.purchaseButton, productId);

                if (entry.wholeCardClickable)
                    BindPurchaseCard(entry, productId);
            }
        }

        private void BindDailyRewards()
        {
            _dailyRewardViews.Clear();

            if (_shopDialog == null)
                return;

            var dailyRoot = _shopDialog.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name == "Daily_widget");

            if (dailyRoot == null)
                return;

            foreach (var button in dailyRoot.GetComponentsInChildren<Button>(true))
            {
                var card = FindDailyCardRoot(button.transform);
                var dayIndex = FindDailyCardDayIndex(card);

                if (dayIndex < 0 || dayIndex >= DailyGemRewards.Length)
                    continue;

                var view = new DailyRewardView
                {
                    Index = dayIndex,
                    Button = button,
                    ButtonText = button.GetComponentInChildren<TMP_Text>(true),
                    RewardText = FindChildText(card, "Gem_count"),
                };

                var capturedIndex = dayIndex;

                AddButtonListener(button, () => ClaimDailyReward(capturedIndex));

                _dailyRewardViews.Add(view);
            }

            _dailyRewardViews.Sort((a, b) => a.Index.CompareTo(b.Index));
        }

        private void BindPurchaseButton(Button button, string productId)
        {
            if (button == null || string.IsNullOrEmpty(productId))
                return;

            AddButtonListener(button, () => Buy(productId));

            if (productId == NoAdsId)
            {
                _noAdsButtons.Add(button);
                RefreshNoAdsButtons();
            }
        }

        private void BindPurchaseCard(ShopRewardEntry entry, string productId)
        {
            if (entry == null || entry.purchaseButton == null || string.IsNullOrEmpty(productId))
                return;

            var cardButton = entry.cardButton != null
                ? entry.cardButton
                : FindOrCreateCardButton(entry.purchaseButton);

            if (cardButton == null || ReferenceEquals(cardButton, entry.purchaseButton))
                return;

            cardButton.transition = Selectable.Transition.None;
            AddButtonListener(cardButton, () => Buy(productId));

            if (productId == NoAdsId)
                _noAdsButtons.Add(cardButton);
        }

        private Button FindOrCreateCardButton(Button purchaseButton)
        {
            var cardRoot = FindPurchaseCardRoot(purchaseButton != null ? purchaseButton.transform : null);
            if (cardRoot == null)
                return null;

            var cardButton = cardRoot.GetComponent<Button>();
            if (cardButton == null)
                cardButton = cardRoot.gameObject.AddComponent<Button>();

            if (cardRoot.GetComponent<Graphic>() == null)
                cardRoot.gameObject.AddComponent<Image>().color = Color.clear;

            return cardButton;
        }

        private void AddButtonListener(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null || action == null)
                return;

            button.onClick.AddListener(action);

            _boundButtons.Add(new ButtonBinding(button, action));
        }

        private void Buy(string productId)
        {
            if (productId == NoAdsId && MonetizationState.NoAdsPurchased)
                return;

            AnalyticsTracker.ReportShopPurchaseClick(productId);

#if Payments_yg
            YG.YG2.BuyPayments(productId);
#else
            AnalyticsTracker.ReportPurchaseSuccess(productId);
            ApplyPurchase(productId);
#endif
        }

        private void OnPurchaseSuccess(string productId)
        {
            AnalyticsTracker.ReportPurchaseSuccess(productId);
            ApplyPurchase(productId);
        }

        private void OnPurchaseFailed(string productId)
        {
            Debug.LogWarning($"Purchase failed: {productId}", this);
        }

        private void ApplyPurchase(string productId)
        {
            RebuildRewards();

            if (!_rewards.TryGetValue(productId, out var reward))
            {
                Debug.LogWarning($"Unknown shop product id: {productId}", this);
                return;
            }

            if (productId == NoAdsId)
            {
                MonetizationState.SetNoAdsPurchased();
                RefreshNoAdsButtons();
            }

            if (_profile != null)
            {
                if (reward.Gems > 0)
                    _profile.AddGems(reward.Gems);

                if (reward.Rubles > 0)
                    _profile.AddRubles(reward.Rubles);
            }

#if Payments_yg
            if (reward.Consumable)
                YG.YG2.ConsumePurchaseByID(productId, false);
#endif

            AnalyticsTracker.ReportPurchaseRewardGranted(
                productId,
                reward.Gems,
                reward.Rubles,
                productId == NoAdsId);
        }

        private void RefreshPriceTexts()
        {
            foreach (var entry in shopRewards)
            {
                if (entry == null || entry.priceText == null)
                    continue;

                var price = GetPriceText(entry);

                if (!string.IsNullOrWhiteSpace(price))
                    entry.priceText.text = price;
            }
        }

        private static string GetPriceText(ShopRewardEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.productId))
                return string.Empty;

#if Payments_yg
            if (entry.useYandexPrice)
            {
                try
                {
                    var purchase = YG.YG2.PurchaseByID(entry.productId.Trim());

                    if (purchase != null && !string.IsNullOrWhiteSpace(purchase.price))
                        return purchase.price;
                }
                catch
                {
                    // SDK может быть ещё не готов.
                }
            }
#endif

            return entry.fallbackPriceText ?? string.Empty;
        }

        private void OpenShop()
        {
            if (_shopDialog != null)
                _shopDialog.SetActive(true);

            AnalyticsTracker.ReportShopOpen();
            ShopOpened?.Invoke();

            RefreshGems();
            RefreshDailyRewards();
            RefreshNoAdsButtons();
            RefreshPriceTexts();
        }

        private void CloseShop()
        {
            if (_shopDialog != null)
                _shopDialog.SetActive(false);

            ShopClosed?.Invoke();
        }

        private void BindGemTexts()
        {
            _gemTexts.Clear();

            foreach (var text in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!text.gameObject.scene.IsValid())
                    continue;

                var path = GetPath(text.transform);

                if (path.EndsWith("/Hard_counter", StringComparison.OrdinalIgnoreCase)
                    || path.Contains("/Shop_dialog/", StringComparison.OrdinalIgnoreCase)
                    && path.Contains("/Top/", StringComparison.OrdinalIgnoreCase)
                    && (text.text ?? string.Empty).Contains("10.000", StringComparison.Ordinal))
                {
                    _gemTexts.Add(text);
                }
            }
        }

        private void OnGemsChanged(long _) => RefreshGems();

        private void RefreshGems()
        {
            var value = _profile != null ? _profile.Gems : 0;
            var formatted = FormatThousands(value);

            for (var i = 0; i < _gemTexts.Count; i++)
            {
                if (_gemTexts[i] != null)
                    _gemTexts[i].text = formatted;
            }
        }

        private void RefreshNoAdsButtons()
        {
            var purchased = MonetizationState.NoAdsPurchased;

            for (var i = 0; i < _noAdsButtons.Count; i++)
            {
                var button = _noAdsButtons[i];

                if (button == null)
                    continue;

                button.interactable = !purchased;

                if (!purchased)
                    continue;

                var text = button.GetComponentInChildren<TMP_Text>(true);

                if (text != null)
                    text.text = "Куплено";
            }
        }

        private void ClaimDailyReward(int index)
        {
            if (_profile == null || index < 0 || index >= DailyGemRewards.Length)
                return;

            var today = GetCurrentLocalDay();
            var data = LoadDailyRewardData();

            NormalizeDailyRewardData(data, today);

            if (data.lastClaimDay == today || data.nextRewardIndex != index)
            {
                RefreshDailyRewards();
                return;
            }

            _profile.AddGems(DailyGemRewards[index]);

            data.lastClaimDay = today;
            data.claimedInCycle = Mathf.Clamp(data.claimedInCycle + 1, 0, DailyGemRewards.Length);
            data.nextRewardIndex = (index + 1) % DailyGemRewards.Length;

            SaveDailyRewardData(data);

            RefreshGems();
            RefreshDailyRewards();
            DailyRewardClaimed?.Invoke();
        }

        public bool IsDailyRewardClaimAvailable()
        {
            var today = GetCurrentLocalDay();
            var data = LoadDailyRewardData();
            var changed = NormalizeDailyRewardData(data, today);

            if (changed)
                SaveDailyRewardData(data);

            return data.lastClaimDay != today
                && data.nextRewardIndex >= 0
                && data.nextRewardIndex < DailyGemRewards.Length;
        }

        public Button GetClaimableDailyRewardButton()
        {
            if (!IsDailyRewardClaimAvailable())
                return null;

            var today = GetCurrentLocalDay();
            var data = LoadDailyRewardData();
            NormalizeDailyRewardData(data, today);

            var activeIndex = Mathf.Clamp(data.nextRewardIndex, 0, DailyGemRewards.Length - 1);

            for (var i = 0; i < _dailyRewardViews.Count; i++)
            {
                var view = _dailyRewardViews[i];
                if (view != null && view.Index == activeIndex)
                    return view.Button;
            }

            return null;
        }

        private void RefreshDailyRewards()
        {
            if (_dailyRewardViews.Count == 0)
                return;

            var today = GetCurrentLocalDay();
            var data = LoadDailyRewardData();

            var changed = NormalizeDailyRewardData(data, today);

            if (changed)
                SaveDailyRewardData(data);

            var canClaimToday = data.lastClaimDay != today;
            var activeIndex = Mathf.Clamp(data.nextRewardIndex, 0, DailyGemRewards.Length - 1);
            var claimedInCycle = Mathf.Clamp(data.claimedInCycle, 0, DailyGemRewards.Length);
            var nextRewardCountdownText = FormatDailyNextRewardCountdown();

            for (var i = 0; i < _dailyRewardViews.Count; i++)
            {
                var view = _dailyRewardViews[i];
                var reward = DailyGemRewards[Mathf.Clamp(view.Index, 0, DailyGemRewards.Length - 1)];

                if (view.RewardText != null)
                    view.RewardText.text = $" {FormatThousands(reward)}";

                var isActive = view.Index == activeIndex;
                var isClaimed = view.Index < claimedInCycle;
                var isAvailable = isActive && canClaimToday;
                var isWaitingNextDay = isActive && !canClaimToday;

                ApplyDailyRewardState(view, isAvailable, isWaitingNextDay, isClaimed, nextRewardCountdownText);
            }

            DailyRewardsChanged?.Invoke();
        }

        private IEnumerator DailyRewardRefreshRoutine()
        {
            var delay = new WaitForSecondsRealtime(30f);

            while (true)
            {
                RefreshDailyRewards();
                yield return delay;
            }
        }

        private void ApplyDailyRewardState(
            DailyRewardView view,
            bool isAvailable,
            bool isWaitingNextDay,
            bool isClaimed,
            string nextRewardCountdownText)
        {
            if (view.ButtonText != null)
            {
                if (isAvailable)
                {
                    view.ButtonText.text = dailyClaimAvailableText;
                }
                else if (isWaitingNextDay)
                {
                    view.ButtonText.text = string.Format(
                        CultureInfo.InvariantCulture,
                        string.IsNullOrWhiteSpace(dailyNextRewardCountdownFormat) ? "{0}" : dailyNextRewardCountdownFormat,
                        nextRewardCountdownText);
                }
                else if (isClaimed)
                {
                    view.ButtonText.text = dailyClaimedText;
                }
                else
                {
                    view.ButtonText.text = dailyLockedText;
                }
            }

            if (view.Button != null)
            {
                view.Button.interactable = isAvailable;
                ApplyDailyButtonColor(view.Button, GetDailyButtonColor(isAvailable, isWaitingNextDay, isClaimed));
            }
        }

        private Color GetDailyButtonColor(bool isAvailable, bool isWaitingNextDay, bool isClaimed)
        {
            if (isAvailable)
                return dailyAvailableButtonColor;

            if (isWaitingNextDay)
                return dailyNextRewardButtonColor;

            return isClaimed ? dailyClaimedButtonColor : dailyLockedButtonColor;
        }

        private static void ApplyDailyButtonColor(Button button, Color color)
        {
            if (button == null)
                return;

            var colors = button.colors;
            colors.normalColor = color;
            colors.selectedColor = color;
            colors.disabledColor = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.12f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.10f);
            button.colors = colors;

            if (button.targetGraphic != null)
                button.targetGraphic.color = color;
        }

        private static string FormatDailyNextRewardCountdown()
        {
            var now = DateTime.Now;
            var remaining = now.Date.AddDays(1d) - now;
            var totalMinutes = Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes));
            var hours = totalMinutes / 60;
            var minutes = totalMinutes % 60;

            return $"{hours}ч {minutes:00}м";
        }

        private static DailyRewardSaveData LoadDailyRewardData()
        {
            return SaveStorage.TryLoadJson<DailyRewardSaveData>(DailyRewardSaveKey, out var data)
                ? data
                : new DailyRewardSaveData();
        }

        private static void SaveDailyRewardData(DailyRewardSaveData data)
        {
            SaveStorage.SaveJson(DailyRewardSaveKey, data);
            SaveStorage.Flush();
        }

        private static bool NormalizeDailyRewardData(DailyRewardSaveData data, long today)
        {
            var changed = false;

            if (data.nextRewardIndex < 0 || data.nextRewardIndex >= DailyGemRewards.Length)
            {
                data.nextRewardIndex = 0;
                changed = true;
            }

            if (data.claimedInCycle < 0 || data.claimedInCycle > DailyGemRewards.Length)
            {
                data.claimedInCycle = Mathf.Clamp(data.claimedInCycle, 0, DailyGemRewards.Length);
                changed = true;
            }

            if (data.claimedInCycle == 0 && data.nextRewardIndex > 0)
            {
                data.claimedInCycle = data.nextRewardIndex;
                changed = true;
            }

            if (data.lastClaimDay < 0)
            {
                if (data.claimedInCycle != 0)
                {
                    data.claimedInCycle = 0;
                    changed = true;
                }

                return changed;
            }

            var gap = today - data.lastClaimDay;

            if (gap > 1 || gap < 0)
            {
                data.lastClaimDay = -1;
                data.nextRewardIndex = 0;
                data.claimedInCycle = 0;
                changed = true;
            }
            else if (gap >= 1 && data.claimedInCycle >= DailyGemRewards.Length)
            {
                data.nextRewardIndex = 0;
                data.claimedInCycle = 0;
                changed = true;
            }

            return changed;
        }

        private static long GetCurrentLocalDay()
        {
            return DateTime.Now.Date.Ticks / TimeSpan.TicksPerDay;
        }

        private void UnbindButtons()
        {
            for (var i = 0; i < _boundButtons.Count; i++)
            {
                var binding = _boundButtons[i];

                if (binding.Button != null)
                    binding.Button.onClick.RemoveListener(binding.Action);
            }

            _boundButtons.Clear();
            _noAdsButtons.Clear();
            _dailyRewardViews.Clear();
        }

        private void UnbindProfile()
        {
            if (_profile != null)
                _profile.GemsChanged -= OnGemsChanged;

            _profile = null;
            _gemTexts.Clear();
        }

        private static GameObject FindSceneObject(string objectName)
        {
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (!go.scene.IsValid() || go.name != objectName)
                    continue;

                return go;
            }

            return null;
        }

        private static Transform FindDailyCardRoot(Transform source)
        {
            var current = source;

            while (current != null)
            {
                if (current.name.StartsWith("Daily_card", StringComparison.OrdinalIgnoreCase))
                    return current;

                current = current.parent;
            }

            return null;
        }

        private Transform FindPurchaseCardRoot(Transform source)
        {
            var shopRoot = _shopDialog != null ? _shopDialog.transform : null;
            var current = source != null ? source.parent : null;

            while (current != null && current != shopRoot)
            {
                if (IsPurchaseCardRootName(current.name))
                    return current;

                current = current.parent;
            }

            return FindFallbackPurchaseCardRoot(source, shopRoot);
        }

        private static Transform FindFallbackPurchaseCardRoot(Transform source, Transform shopRoot)
        {
            var sourceRect = source as RectTransform;
            var sourceSize = sourceRect != null ? sourceRect.rect.size : Vector2.zero;
            var current = source != null ? source.parent : null;

            while (current != null && current != shopRoot)
            {
                if (IsLikelyPurchaseCardRoot(current, sourceSize))
                    return current;

                current = current.parent;
            }

            return null;
        }

        private static bool IsLikelyPurchaseCardRoot(Transform transform, Vector2 sourceSize)
        {
            if (transform == null)
                return false;

            if (transform.name.Contains("button", StringComparison.OrdinalIgnoreCase)
                || transform.name.Contains("buy", StringComparison.OrdinalIgnoreCase))
                return false;

            var rect = transform as RectTransform;
            if (rect == null)
                return false;

            var size = rect.rect.size;
            if (size.x < 180f || size.y < 120f)
                return false;

            if (sourceSize != Vector2.zero
                && size.x < sourceSize.x * 1.25f
                && size.y < sourceSize.y * 1.25f)
                return false;

            return transform.GetComponent<Graphic>() != null;
        }

        private static bool IsPurchaseCardRootName(string name)
        {
            return !string.IsNullOrEmpty(name)
                && (name.Contains("card", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("offer", StringComparison.OrdinalIgnoreCase));
        }

        private static int FindDailyCardDayIndex(Transform card)
        {
            var text = FindChildText(card, "Day_number");

            if (text == null)
                return -1;

            var dayNumber = ParseFirstPositiveInt(text.text);

            return dayNumber <= 0 ? -1 : dayNumber - 1;
        }

        private static TMP_Text FindChildText(Transform root, string parentName)
        {
            if (root == null)
                return null;

            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                var parent = text.transform.parent;

                while (parent != null && parent != root.parent)
                {
                    if (parent.name.Equals(parentName, StringComparison.OrdinalIgnoreCase))
                        return text;

                    parent = parent.parent;
                }
            }

            return null;
        }

        private static int ParseFirstPositiveInt(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            var value = 0;
            var found = false;

            for (var i = 0; i < text.Length; i++)
            {
                if (!char.IsDigit(text[i]))
                {
                    if (found)
                        break;

                    continue;
                }

                found = true;
                value = value * 10 + text[i] - '0';
            }

            return found ? value : 0;
        }

        private static string GetPath(Transform t)
        {
            var path = t.name;

            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }

            return path;
        }

        private static string FormatThousands(long value)
        {
            return value
                .ToString("N0", CultureInfo.InvariantCulture)
                .Replace(",", ".");
        }

        private static long ParseLong(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0;

            value = value
                .Replace(".", string.Empty)
                .Replace(",", string.Empty)
                .Replace(" ", string.Empty)
                .Trim();

            return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
                ? Math.Max(0, result)
                : 0;
        }
    }
}
