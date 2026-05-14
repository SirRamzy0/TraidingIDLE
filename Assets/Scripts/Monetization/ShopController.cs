using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
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

        private readonly Dictionary<string, ProductReward> _rewards = new();

        private readonly List<ButtonBinding> _boundButtons = new();
        private readonly List<TMP_Text> _gemTexts = new();
        private readonly List<Button> _noAdsButtons = new();
        private readonly List<DailyRewardView> _dailyRewardViews = new();

        private PlayerProfile _profile;
        private GameObject _shopDialog;

        [Serializable]
        private sealed class DailyRewardSaveData
        {
            public long lastClaimDay = -1;
            public int nextRewardIndex;
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

                BindPurchaseButton(entry.purchaseButton, entry.productId.Trim());
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

#if Payments_yg
            YG.YG2.BuyPayments(productId);
#else
            ApplyPurchase(productId);
#endif
        }

        private void OnPurchaseSuccess(string productId)
        {
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

            RefreshGems();
            RefreshDailyRewards();
            RefreshNoAdsButtons();
            RefreshPriceTexts();
        }

        private void CloseShop()
        {
            if (_shopDialog != null)
                _shopDialog.SetActive(false);
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
            data.nextRewardIndex = (index + 1) % DailyGemRewards.Length;

            SaveDailyRewardData(data);

            RefreshGems();
            RefreshDailyRewards();
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

            for (var i = 0; i < _dailyRewardViews.Count; i++)
            {
                var view = _dailyRewardViews[i];
                var reward = DailyGemRewards[Mathf.Clamp(view.Index, 0, DailyGemRewards.Length - 1)];

                if (view.RewardText != null)
                    view.RewardText.text = $" {FormatThousands(reward)}";

                if (view.ButtonText != null)
                    view.ButtonText.text = view.Index == activeIndex && canClaimToday
                        ? "Забрать"
                        : "Закрыто";

                if (view.Button != null)
                    view.Button.interactable = view.Index == activeIndex && canClaimToday;
            }
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

            if (data.lastClaimDay < 0)
                return changed;

            var gap = today - data.lastClaimDay;

            if (gap > 1 || gap < 0)
            {
                data.lastClaimDay = -1;
                data.nextRewardIndex = 0;
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