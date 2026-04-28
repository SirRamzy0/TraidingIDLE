using System;
using TraidingIDLE.Currencies;
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
        }

        [Header("Rubles (main currency)")]
        [SerializeField, Min(0)] private long startingRubles = 25_000;

        [Header("Coin holdings")]
        [SerializeField] private CoinHolding[] holdings =
        {
            new() { id = CurrencyId.SHT, amount = 0, cap = 1000 },
            new() { id = CurrencyId.ETH, amount = 0, cap = 100 },
            new() { id = CurrencyId.BTC, amount = 0, cap = 0 },
        };

        public event Action<long>? RublesChanged;
        public event Action<CurrencyId>? HoldingsChanged;

        private long _rubles;

        public long Rubles => _rubles;

        private void Awake()
        {
            EnsureHoldingsConfigured();
            _rubles = Math.Max(0, startingRubles);
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
            holdings[index] = holding;

            RublesChanged?.Invoke(_rubles);
            HoldingsChanged?.Invoke(id);
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
            holding.amount -= count;
            holdings[index] = holding;
            _rubles += proceeds;

            RublesChanged?.Invoke(_rubles);
            HoldingsChanged?.Invoke(id);
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
        }

        public void AddRubles(long delta)
        {
            if (delta == 0)
                return;

            _rubles = Math.Max(0, _rubles + delta);
            RublesChanged?.Invoke(_rubles);
        }

        [ContextMenu("Debug/Add 100k rubles")]
        private void Debug_Add100k() => AddRubles(100_000);

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

            EnsureHolding(CurrencyId.SHT, 1000);
            EnsureHolding(CurrencyId.ETH, 100);
            EnsureHolding(CurrencyId.BTC, 0);

            for (var i = 0; i < holdings.Length; i++)
            {
                var h = holdings[i];
                h.amount = Mathf.Max(0, h.amount);
                h.cap = Mathf.Max(0, h.cap);
                if (h.amount > h.cap)
                    h.amount = h.cap;
                holdings[i] = h;
            }
        }

        private void EnsureHolding(CurrencyId id, int defaultCap)
        {
            if (FindHolding(id) >= 0)
                return;

            Array.Resize(ref holdings, holdings.Length + 1);
            holdings[^1] = new CoinHolding { id = id, amount = 0, cap = Mathf.Max(0, defaultCap) };
        }
    }
}
