using System;
using System.Collections.Generic;
using TraidingIDLE.Saves;
using UnityEngine;

namespace TraidingIDLE.Currencies.Simulation
{
    /// <summary>
    /// Persists per-coin recent price history (last N visible prices) so that the
    /// chart board can resume across sessions and seed visually with continuity.
    /// </summary>
    public sealed class PriceHistoryStore : MonoBehaviour
    {
        [Serializable]
        private struct CoinHistory
        {
            public CurrencyId id;
            public float[] prices;
        }

        [Serializable]
        private sealed class SaveData
        {
            public int historyPresetVersion;
            public CoinHistory[] histories = Array.Empty<CoinHistory>();
        }

        private const string SaveKey = "save.history.v1";
        private const int CurrentHistoryPresetVersion = 2;

        [Tooltip("How many recent prices to keep per coin.")]
        [SerializeField, Min(2)] private int historySize = 50;

        [Tooltip("Throttle save flushing in seconds (writes once after a burst of pushes).")]
        [SerializeField, Min(0f)] private float saveCooldownSeconds = 5f;

        private readonly Dictionary<CurrencyId, List<float>> _histories = new();
        private bool _dirty;
        private float _saveCooldown;

        public int HistorySize => historySize;

        public event Action<CurrencyId>? HistoryReset;

        private void Awake()
        {
            LoadFromStorage();
        }

        private void OnEnable()
        {
            SaveStorage.ExternalDataLoaded += ReloadFromExternalStorage;
        }

        private void Update()
        {
            if (!_dirty)
                return;

            _saveCooldown -= Time.unscaledDeltaTime;
            if (_saveCooldown > 0f)
                return;

            FlushSave();
        }

        private void OnDisable()
        {
            SaveStorage.ExternalDataLoaded -= ReloadFromExternalStorage;

            if (_dirty)
                FlushSave();
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause && _dirty)
                FlushSave();
        }

        private void OnApplicationQuit()
        {
            if (_dirty)
                FlushSave();
        }

        public bool TryGet(CurrencyId id, out IReadOnlyList<float> prices)
        {
            if (_histories.TryGetValue(id, out var list) && list.Count > 0)
            {
                prices = list;
                return true;
            }

            prices = Array.Empty<float>();
            return false;
        }

        public void Push(CurrencyId id, float price)
        {
            if (!TryNormalizePrice(price, out price))
                return;

            var list = GetOrCreate(id);
            list.Add(price);
            TrimToCapacity(list);
            MarkDirty();
        }

        public void SetAll(CurrencyId id, IReadOnlyList<float> prices)
        {
            var list = GetOrCreate(id);
            list.Clear();
            if (prices != null)
            {
                for (var i = 0; i < prices.Count; i++)
                {
                    var p = prices[i];
                    if (!TryNormalizePrice(p, out p))
                        continue;
                    list.Add(p);
                }
            }

            TrimToCapacity(list);
            MarkDirty();
            HistoryReset?.Invoke(id);
        }

        public void Clear(CurrencyId id)
        {
            if (_histories.TryGetValue(id, out var list))
            {
                list.Clear();
                MarkDirty();
                HistoryReset?.Invoke(id);
            }
        }

        public void ClearAll()
        {
            foreach (var kv in _histories)
                kv.Value.Clear();
            MarkDirty();
        }

        [ContextMenu("Debug/Reset save")]
        private void Debug_ResetSave()
        {
            SaveStorage.DeleteKey(SaveKey);
            SaveStorage.Flush();
            ClearAll();
        }

        private List<float> GetOrCreate(CurrencyId id)
        {
            if (_histories.TryGetValue(id, out var list))
                return list;

            list = new List<float>(historySize + 1);
            _histories[id] = list;
            return list;
        }

        private void TrimToCapacity(List<float> list)
        {
            var max = Mathf.Max(2, historySize);
            if (list.Count <= max)
                return;

            var excess = list.Count - max;
            list.RemoveRange(0, excess);
        }

        private void MarkDirty()
        {
            _dirty = true;
            if (_saveCooldown <= 0f)
                _saveCooldown = Mathf.Max(0.1f, saveCooldownSeconds);
        }

        private void FlushSave()
        {
            _dirty = false;
            _saveCooldown = 0f;
            SaveToStorage();
        }

        private void SaveToStorage()
        {
            var entries = new List<CoinHistory>(_histories.Count);
            foreach (var kv in _histories)
            {
                if (kv.Value.Count == 0)
                    continue;
                entries.Add(new CoinHistory
                {
                    id = kv.Key,
                    prices = kv.Value.ToArray(),
                });
            }

            var data = new SaveData
            {
                historyPresetVersion = CurrentHistoryPresetVersion,
                histories = entries.ToArray(),
            };

            SaveStorage.SaveJson(SaveKey, data);
            SaveStorage.Flush();
        }

        private void LoadFromStorage()
        {
            if (!SaveStorage.TryLoadJson<SaveData>(SaveKey, out var data))
                return;

            if (data.histories == null)
                return;

            var migratedLegacyHistory = data.historyPresetVersion < CurrentHistoryPresetVersion;

            for (var i = 0; i < data.histories.Length; i++)
            {
                var saved = data.histories[i];
                var list = GetOrCreate(saved.id);
                list.Clear();
                if (saved.prices == null)
                    continue;

                for (var j = 0; j < saved.prices.Length; j++)
                {
                    var p = migratedLegacyHistory
                        ? MigrateLegacyHistoryPrice(saved.id, saved.prices[j])
                        : saved.prices[j];
                    if (!TryNormalizePrice(p, out p))
                        continue;
                    list.Add(p);
                }

                TrimToCapacity(list);
            }

            if (migratedLegacyHistory)
                MarkDirty();
        }

        private void ReloadFromExternalStorage()
        {
            _dirty = false;
            _saveCooldown = 0f;
            _histories.Clear();
            LoadFromStorage();
            HistoryReset?.Invoke(CurrencyId.SHT);
            HistoryReset?.Invoke(CurrencyId.ETH);
            HistoryReset?.Invoke(CurrencyId.BTC);
        }

        private static bool TryNormalizePrice(float rawPrice, out float price)
        {
            price = 0f;
            if (float.IsNaN(rawPrice) || float.IsInfinity(rawPrice) || rawPrice < CurrencyMarket.MinimumTradablePrice)
                return false;

            price = CurrencyMarket.SanitizePrice(rawPrice);
            return true;
        }

        private static float MigrateLegacyHistoryPrice(CurrencyId id, float price)
        {
            return id switch
            {
                CurrencyId.ETH when price < 450000f => price * 9f,
                CurrencyId.BTC when price < 5000000f => price * 4f,
                _ => price,
            };
        }
    }
}
