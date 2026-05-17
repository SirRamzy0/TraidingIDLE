using System;
using System.Collections.Generic;
using TraidingIDLE.Currencies;
using TraidingIDLE.Saves;
using UnityEngine;

namespace TraidingIDLE.Analytics
{
    public sealed class AnalyticsTracker : MonoBehaviour
    {
        public const string GameStart = "game_start";
        public const string Session5Minutes = "session_5m";
        public const string Session40Minutes = "session_40m";
        public const string FirstCoinBuy = "first_coin_buy";
        public const string FirstCoinSell = "first_coin_sell";
        public const string FirstProfitableSell = "first_profitable_sell";
        public const string FirstBusinessBuy = "first_business_buy";
        public const string FirstRigBuy = "first_rig_buy";
        public const string ShopOpen = "shop_open";
        public const string ShopPurchaseClick = "shop_purchase_click";
        public const string PurchaseSuccess = "purchase_success";
        public const string PurchaseRewardGranted = "purchase_reward_granted";

        private const string SaveKey = "save.analytics.v1";
        private const float Session5Seconds = 5f * 60f;
        private const float Session40Seconds = 40f * 60f;

        private static AnalyticsTracker _instance;

        private SaveData _save = new();
        private float _sessionTime;
        private bool _session5Sent;
        private bool _session40Sent;

        [Serializable]
        private sealed class SaveData
        {
            public bool firstCoinBuy;
            public bool firstCoinSell;
            public bool firstProfitableSell;
            public bool firstBusinessBuy;
            public bool firstRigBuy;
        }

        public static AnalyticsTracker Instance => GetOrCreate();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            GetOrCreate();
        }

        public static void ReportCoinBuy(CurrencyId coin, int amount, long price, long rublesAfter)
        {
            Instance.SendOnce(
                FirstCoinBuy,
                save => save.firstCoinBuy,
                save => save.firstCoinBuy = true,
                EventData(
                    ("coin", coin.ToString()),
                    ("amount", amount),
                    ("price", price),
                    ("rubles_after", rublesAfter)));
        }

        public static void ReportCoinSell(
            CurrencyId coin,
            int amount,
            long price,
            long rublesAfter,
            long profitRubles)
        {
            var data = EventData(
                ("coin", coin.ToString()),
                ("amount", amount),
                ("price", price),
                ("rubles_after", rublesAfter),
                ("profit_rubles", profitRubles));

            Instance.SendOnce(
                FirstCoinSell,
                save => save.firstCoinSell,
                save => save.firstCoinSell = true,
                data);

            if (profitRubles > 0)
            {
                Instance.SendOnce(
                    FirstProfitableSell,
                    save => save.firstProfitableSell,
                    save => save.firstProfitableSell = true,
                    data);
            }
        }

        public static void ReportBusinessBuy(string businessId, string category, long cost, double incomePerHour)
        {
            Instance.SendOnce(
                FirstBusinessBuy,
                save => save.firstBusinessBuy,
                save => save.firstBusinessBuy = true,
                EventData(
                    ("business_id", businessId),
                    ("category", category),
                    ("cost", cost),
                    ("income_per_hour", Math.Round(Math.Max(0d, incomePerHour)))));
        }

        public static void ReportRigBuy(int rigIndex, CurrencyId coin, long cost)
        {
            Instance.SendOnce(
                FirstRigBuy,
                save => save.firstRigBuy,
                save => save.firstRigBuy = true,
                EventData(
                    ("rig_index", rigIndex),
                    ("coin", coin.ToString()),
                    ("cost", cost)));
        }

        public static void ReportShopOpen()
        {
            Instance.Send(ShopOpen);
        }

        public static void ReportShopPurchaseClick(string productId)
        {
            Instance.Send(ShopPurchaseClick, EventData(("product_id", productId)));
        }

        public static void ReportPurchaseSuccess(string productId)
        {
            Instance.Send(PurchaseSuccess, EventData(("product_id", productId)));
        }

        public static void ReportPurchaseRewardGranted(
            string productId,
            long gems,
            long rubles,
            bool noAds)
        {
            Instance.Send(
                PurchaseRewardGranted,
                EventData(
                    ("product_id", productId),
                    ("gems", gems),
                    ("rubles", rubles),
                    ("no_ads", noAds)));
        }

        private static AnalyticsTracker GetOrCreate()
        {
            if (_instance != null)
                return _instance;

            _instance = FindAnyObjectByType<AnalyticsTracker>(FindObjectsInactive.Include);
            if (_instance != null)
                return _instance;

            var go = new GameObject(nameof(AnalyticsTracker));
            _instance = go.AddComponent<AnalyticsTracker>();
            DontDestroyOnLoad(go);
            return _instance;
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
            Load();
            Send(GameStart);
        }

        private void Update()
        {
            _sessionTime += Time.unscaledDeltaTime;

            if (!_session5Sent && _sessionTime >= Session5Seconds)
            {
                _session5Sent = true;
                Send(Session5Minutes);
            }

            if (!_session40Sent && _sessionTime >= Session40Seconds)
            {
                _session40Sent = true;
                Send(Session40Minutes);
            }
        }

        private void SendOnce(
            string eventName,
            Func<SaveData, bool> getSent,
            Action<SaveData> setSent,
            Dictionary<string, object> data)
        {
            if (getSent(_save))
                return;

            if (!Send(eventName, data))
                return;

            setSent(_save);
            Save();
        }

        private bool Send(string eventName, Dictionary<string, object> data = null)
        {
            if (string.IsNullOrWhiteSpace(eventName))
                return false;

            try
            {
                if (data == null || data.Count == 0)
                    YG.YG2.MetricaSend(eventName);
                else
                    YG.YG2.MetricaSend(eventName, data);

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Metrica event failed: {eventName}. {ex.Message}", this);
                return false;
            }
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

        private static Dictionary<string, object> EventData(params (string Key, object Value)[] pairs)
        {
            var data = new Dictionary<string, object>();
            for (var i = 0; i < pairs.Length; i++)
            {
                var pair = pairs[i];
                if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value == null)
                    continue;

                data[pair.Key] = pair.Value;
            }

            return data;
        }
    }
}
