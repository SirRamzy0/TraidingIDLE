using System;
using TraidingIDLE.Currencies;
using TraidingIDLE.Saves;
using UnityEngine;

namespace TraidingIDLE.Player
{
    public sealed class PlayerProfile : MonoBehaviour
    {
        [Serializable]
        private struct CoinHolding
        {
            public CurrencyId id;
            [Min(0)] public int amount;
            [Min(0)] public int cap;
            public long investedRubles;
        }

        [Serializable]
        private sealed class SaveData
        {
            public long rubles;
            public long gems;
            public CoinHolding[] holdings = Array.Empty<CoinHolding>();
        }

        private const string SaveKey = "save.player.v1";

        [Header("Rubles (main currency)")]
        [SerializeField, Min(0)] private long startingRubles = 25_000;

        [Header("Gems (hard currency)")]
        [SerializeField, Min(0)] private long startingGems = 0;

        [Header("Coin holdings")]
        [SerializeField] private CoinHolding[] holdings =
        {
            new() { id = CurrencyId.SHT, amount = 0, cap = 500 },
            new() { id = CurrencyId.ETH, amount = 0, cap = 150 },
            new() { id = CurrencyId.BTC, amount = 0, cap = 50 },
        };

        public event Action<long>? RublesChanged;
        public event Action<long>? GemsChanged;
        public event Action<CurrencyId>? HoldingsChanged;

        private long _rubles;
        private long _gems;

        public long Rubles => _rubles;
        public long Gems => _gems;

        private void Awake()
        {
            EnsureHoldingsConfigured();
            _rubles = Math.Max(0, startingRubles);
            _gems = Math.Max(0, startingGems);
            LoadFromStorage();
        }

        private void OnValidate()
        {
            EnsureHoldingsConfigured();
        }

        public int GetAmount(CurrencyId id)
        {
            var index = FindHolding(id);
            return index < 0 ? 0 : holdings[index].amount;
        }

        public int GetCap(CurrencyId id)
        {
            var index = FindHolding(id);
            return index < 0 ? 0 : holdings[index].cap;
        }

        public long GetInvestedRubles(CurrencyId id)
        {
            var index = FindHolding(id);
            return index < 0 ? 0 : Math.Max(0, holdings[index].investedRubles);
        }

        public int GetAvailableRoom(CurrencyId id)
        {
            var index = FindHolding(id);
            if (index < 0)
                return 0;

            var holding = holdings[index];
            return Math.Max(0, holding.cap - holding.amount);
        }

        public bool TryBuy(CurrencyId id, int count, long unitPrice)
        {
            if (count <= 0 || unitPrice < 0)
                return false;

            var index = FindHolding(id);
            if (index < 0)
                return false;

            var holding = holdings[index];
            if (holding.amount + count > holding.cap)
                return false;

            var cost = unitPrice * count;
            if (_rubles < cost)
                return false;

            _rubles -= cost;
            holding.amount += count;
            holding.investedRubles = Math.Max(0, holding.investedRubles + cost);
            holdings[index] = holding;

            RublesChanged?.Invoke(_rubles);
            HoldingsChanged?.Invoke(id);
            SaveToStorage();
            return true;
        }

        public bool TrySell(CurrencyId id, int count, long unitPrice)
        {
            if (count <= 0 || unitPrice < 0)
                return false;

            var index = FindHolding(id);
            if (index < 0)
                return false;

            var holding = holdings[index];
            if (holding.amount < count)
                return false;

            var proceeds = unitPrice * count;

            // Reduce investedRubles proportionally using weighted average cost.
            if (holding.amount > 0 && holding.investedRubles > 0)
            {
                var avgCost = (double)holding.investedRubles / holding.amount;
                var costRemoved = (long)Math.Round(avgCost * count);
                holding.investedRubles = Math.Max(0, holding.investedRubles - costRemoved);
            }

            holding.amount -= count;
            if (holding.amount <= 0)
            {
                holding.amount = 0;
                holding.investedRubles = 0;
            }
            holdings[index] = holding;
            _rubles += proceeds;

            RublesChanged?.Invoke(_rubles);
            HoldingsChanged?.Invoke(id);
            SaveToStorage();
            return true;
        }

        public bool TryAddCoins(CurrencyId id, int count, out int added)
        {
            added = 0;
            if (count <= 0)
                return false;

            var index = FindHolding(id);
            if (index < 0)
                return false;

            var holding = holdings[index];
            added = Mathf.Clamp(count, 0, Math.Max(0, holding.cap - holding.amount));
            if (added <= 0)
                return false;

            holding.amount += added;
            holdings[index] = holding;

            HoldingsChanged?.Invoke(id);
            SaveToStorage();
            return true;
        }

        public bool TrySpendCoins(CurrencyId id, int count)
        {
            if (count < 0)
                return false;
            if (count == 0)
                return true;

            var index = FindHolding(id);
            if (index < 0)
                return false;

            var holding = holdings[index];
            if (holding.amount < count)
                return false;

            if (holding.amount > 0 && holding.investedRubles > 0)
            {
                var avgCost = (double)holding.investedRubles / holding.amount;
                var costRemoved = (long)Math.Round(avgCost * count);
                holding.investedRubles = Math.Max(0, holding.investedRubles - costRemoved);
            }

            holding.amount -= count;
            if (holding.amount <= 0)
            {
                holding.amount = 0;
                holding.investedRubles = 0;
            }

            holdings[index] = holding;
            HoldingsChanged?.Invoke(id);
            SaveToStorage();
            return true;
        }

        public void SetCap(CurrencyId id, int cap)
        {
            var index = FindHolding(id);
            if (index < 0)
                return;

            cap = Mathf.Max(0, cap);
            var holding = holdings[index];
            if (holding.cap == cap)
                return;

            holding.cap = cap;
            if (holding.amount > cap)
                holding.amount = cap;

            holdings[index] = holding;
            HoldingsChanged?.Invoke(id);
            SaveToStorage();
        }

        public void AddRubles(long delta)
        {
            if (delta == 0)
                return;

            _rubles = Math.Max(0, _rubles + delta);
            RublesChanged?.Invoke(_rubles);
            SaveToStorage();
        }

        public void AddGems(long delta)
        {
            if (delta == 0)
                return;

            _gems = Math.Max(0, _gems + delta);
            GemsChanged?.Invoke(_gems);
            SaveToStorage();
        }

        public bool TrySpendRubles(long amount)
        {
            if (amount < 0 || _rubles < amount)
                return false;
            if (amount == 0)
                return true;

            _rubles -= amount;
            RublesChanged?.Invoke(_rubles);
            SaveToStorage();
            return true;
        }

        public bool TrySpendGems(long amount)
        {
            if (amount < 0 || _gems < amount)
                return false;
            if (amount == 0)
                return true;

            _gems -= amount;
            GemsChanged?.Invoke(_gems);
            SaveToStorage();
            return true;
        }

        public void SaveToStorage()
        {
            var data = new SaveData
            {
                rubles = _rubles,
                gems = _gems,
                holdings = (CoinHolding[])holdings.Clone(),
            };
            SaveStorage.SaveJson(SaveKey, data);
            SaveStorage.Flush();
        }

        private void LoadFromStorage()
        {
            if (!SaveStorage.TryLoadJson<SaveData>(SaveKey, out var data))
                return;

            _rubles = Math.Max(0, data.rubles);
            _gems = Math.Max(0, data.gems);

            if (data.holdings == null)
                return;

            for (var i = 0; i < data.holdings.Length; i++)
            {
                var saved = data.holdings[i];
                var index = FindHolding(saved.id);
                if (index < 0)
                    continue;

                var current = holdings[index];
                current.amount = Mathf.Max(0, saved.amount);
                current.cap = Mathf.Max(0, current.cap);
                current.investedRubles = Math.Max(0, saved.investedRubles);

                if (current.amount > current.cap)
                    current.amount = current.cap;

                if (current.amount <= 0)
                    current.investedRubles = 0;

                holdings[index] = current;
            }

            RublesChanged?.Invoke(_rubles);
            GemsChanged?.Invoke(_gems);
            for (var i = 0; i < holdings.Length; i++)
                HoldingsChanged?.Invoke(holdings[i].id);
        }

        [ContextMenu("Debug/Reset save")]
        private void Debug_ResetSave()
        {
            SaveStorage.DeleteKey(SaveKey);
            SaveStorage.Flush();
        }

        [ContextMenu("Debug/Add 100k rubles")]
        private void Debug_Add100k() => AddRubles(100_000);

        [ContextMenu("Debug/Add 100 gems")]
        private void Debug_Add100Gems() => AddGems(100);

        [ContextMenu("Debug/Reset to starting rubles")]
        private void Debug_ResetRubles()
        {
            _rubles = Math.Max(0, startingRubles);
            RublesChanged?.Invoke(_rubles);
        }

        private int FindHolding(CurrencyId id)
        {
            for (var i = 0; i < holdings.Length; i++)
            {
                if (holdings[i].id == id)
                    return i;
            }

            return -1;
        }

        private void EnsureHoldingsConfigured()
        {
            holdings ??= Array.Empty<CoinHolding>();

            EnsureHolding(CurrencyId.SHT, 500);
            EnsureHolding(CurrencyId.ETH, 150);
            EnsureHolding(CurrencyId.BTC, 50);

            for (var i = 0; i < holdings.Length; i++)
            {
                var h = holdings[i];
                h.amount = Mathf.Max(0, h.amount);
                h.cap = Mathf.Max(0, h.cap);
                h.investedRubles = Math.Max(0, h.investedRubles);
                if (h.amount > h.cap)
                    h.amount = h.cap;
                if (h.amount <= 0)
                    h.investedRubles = 0;
                holdings[i] = h;
            }
        }

        private void EnsureHolding(CurrencyId id, int defaultCap)
        {
            if (FindHolding(id) >= 0)
                return;

            Array.Resize(ref holdings, holdings.Length + 1);
            holdings[^1] = new CoinHolding { id = id, amount = 0, cap = Mathf.Max(0, defaultCap), investedRubles = 0 };
        }
    }
}
