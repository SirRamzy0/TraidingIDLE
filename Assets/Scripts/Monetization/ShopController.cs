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

        private readonly Dictionary<string, ProductReward> _rewards = new()
        {
            { StarterPackId, new ProductReward(gems: 20_000, rubles: 50_900_000_000L, consumable: true) },
            { "gems_500", new ProductReward(gems: 500, rubles: 0, consumable: true) },
            { "gems_2000", new ProductReward(gems: 2_000, rubles: 0, consumable: true) },
            { "gems_6500", new ProductReward(gems: 6_500, rubles: 0, consumable: true) },
            { "gems_12000", new ProductReward(gems: 12_000, rubles: 0, consumable: true) },
            { "rubles_pack_1", new ProductReward(gems: 0, rubles: 50_000_000L, consumable: true) },
            { "rubles_pack_2", new ProductReward(gems: 0, rubles: 250_000_000L, consumable: true) },
            { "rubles_pack_3", new ProductReward(gems: 0, rubles: 1_000_000_000L, consumable: true) },
            { "rubles_pack_4", new ProductReward(gems: 0, rubles: 5_000_000_000L, consumable: true) },
            { NoAdsId, new ProductReward(gems: 0, rubles: 0, consumable: false) },
        };

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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null)
                return;

            var go = new GameObject(nameof(ShopController));
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ShopController>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
#if Payments_yg
            YG.YG2.onPurchaseSuccess += OnPurchaseSuccess;
            YG.YG2.onPurchaseFailed += OnPurchaseFailed;
#endif
            StartCoroutine(BindAfterFrame());
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
#if Payments_yg
            YG.YG2.onPurchaseSuccess -= OnPurchaseSuccess;
            YG.YG2.onPurchaseFailed -= OnPurchaseFailed;
#endif
            UnbindButtons();
            UnbindProfile();
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
            if (_shopDialog == null)
                return;

            var cashButtons = new List<Button>();
            var gemButtons = new List<Button>();

            foreach (var button in _shopDialog.GetComponentsInChildren<Button>(true))
            {
                var path = GetPath(button.transform);
                if (path.Contains("/Daily_widget/", StringComparison.OrdinalIgnoreCase)
                    || path.Contains("/Close_button", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (path.Contains("/Offer/", StringComparison.OrdinalIgnoreCase))
                {
                    BindPurchaseButton(button, StarterPackId);
                    continue;
                }

                if (path.Contains("/Cash_bundels/Image (1)/", StringComparison.OrdinalIgnoreCase))
                {
                    BindPurchaseButton(button, NoAdsId);
                    continue;
                }

                if (path.Contains("/Gems_bundles/", StringComparison.OrdinalIgnoreCase))
                    gemButtons.Add(button);
                else if (path.Contains("/Cash_bundels/Image/Gems_bundels", StringComparison.OrdinalIgnoreCase))
                    cashButtons.Add(button);
            }

            BindOrderedGemButtons(gemButtons);
            BindOrderedCashButtons(cashButtons);
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

        private void BindOrderedGemButtons(List<Button> buttons)
        {
            foreach (var button in buttons.OrderBy(GetCardSiblingIndex))
            {
                var amount = FindCardAmount(button.transform);
                var productId = amount switch
                {
                    500 => "gems_500",
                    2000 => "gems_2000",
                    6500 => "gems_6500",
                    12000 => "gems_12000",
                    _ => null,
                };

                if (!string.IsNullOrEmpty(productId))
                    BindPurchaseButton(button, productId);
            }
        }

        private void BindOrderedCashButtons(List<Button> buttons)
        {
            var ids = new[] { "rubles_pack_1", "rubles_pack_2", "rubles_pack_3", "rubles_pack_4" };
            var ordered = buttons.OrderBy(GetCardSiblingIndex).ToArray();
            for (var i = 0; i < ordered.Length && i < ids.Length; i++)
                BindPurchaseButton(ordered[i], ids[i]);
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

        private void OpenShop()
        {
            if (_shopDialog != null)
                _shopDialog.SetActive(true);
            RefreshGems();
            RefreshDailyRewards();
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
                    view.ButtonText.text = view.Index == activeIndex && canClaimToday ? "Забрать" : "Закрыто";

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

        private static int GetCardSiblingIndex(Button button)
        {
            var card = FindCardRoot(button.transform);
            return card != null ? card.GetSiblingIndex() : button.transform.GetSiblingIndex();
        }

        private static int FindCardAmount(Transform button)
        {
            var card = FindCardRoot(button);
            if (card == null)
                return 0;

            foreach (var text in card.GetComponentsInChildren<TMP_Text>(true))
            {
                if (!text.transform.parent || !text.transform.parent.name.Contains("Name", StringComparison.OrdinalIgnoreCase))
                    continue;

                var raw = (text.text ?? string.Empty).Replace(".", string.Empty).Trim();
                if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount))
                    return amount;
            }

            return 0;
        }

        private static Transform FindCardRoot(Transform source)
        {
            var current = source;
            while (current != null)
            {
                if (current.name.StartsWith("Gem_card", StringComparison.OrdinalIgnoreCase))
                    return current;
                current = current.parent;
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

        private static string GetButtonText(Button button)
        {
            return button.GetComponentInChildren<TMP_Text>(true)?.text?.Trim() ?? string.Empty;
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
    }
}
