using System;
using TraidingIDLE.Saves;

namespace TraidingIDLE.Monetization
{
    public static class EnergyRefillState
    {
        [Serializable]
        private sealed class SaveData
        {
            public long nextAdAvailableUtc;
        }

        private const string SaveKey = "save.energy_refill.v1";
        private const long AdCooldownSeconds = 3600L;

        private static bool _loaded;
        private static long _nextAdAvailableUtc;

        public static bool IsAdAvailable
        {
            get
            {
                EnsureLoaded();
                return UtcNow() >= _nextAdAvailableUtc;
            }
        }

        public static long SecondsUntilAvailable
        {
            get
            {
                EnsureLoaded();
                return Math.Max(0L, _nextAdAvailableUtc - UtcNow());
            }
        }

        public static void MarkAdConsumed()
        {
            EnsureLoaded();
            _nextAdAvailableUtc = UtcNow() + AdCooldownSeconds;
            Save();
        }

        private static void EnsureLoaded()
        {
            if (_loaded)
                return;

            _loaded = true;
            if (SaveStorage.TryLoadJson<SaveData>(SaveKey, out var data))
                _nextAdAvailableUtc = Math.Max(0L, data.nextAdAvailableUtc);
        }

        private static void Save()
        {
            SaveStorage.SaveJson(SaveKey, new SaveData { nextAdAvailableUtc = _nextAdAvailableUtc });
            SaveStorage.Flush();
        }

        private static long UtcNow() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}