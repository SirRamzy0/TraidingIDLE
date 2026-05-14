using System;
using TraidingIDLE.Saves;

namespace TraidingIDLE.Monetization
{
    public static class MonetizationState
    {
        [Serializable]
        private sealed class SaveData
        {
            public bool noAdsPurchased;
        }

        private const string SaveKey = "save.monetization.v1";

        private static bool _loaded;
        private static bool _noAdsPurchased;

        public static bool NoAdsPurchased
        {
            get
            {
                EnsureLoaded();
                return _noAdsPurchased;
            }
        }

        public static void SetNoAdsPurchased()
        {
            EnsureLoaded();
            if (_noAdsPurchased)
                return;

            _noAdsPurchased = true;
            Save();
        }

        private static void EnsureLoaded()
        {
            if (_loaded)
                return;

            _loaded = true;
            if (SaveStorage.TryLoadJson<SaveData>(SaveKey, out var data))
                _noAdsPurchased = data.noAdsPurchased;
        }

        private static void Save()
        {
            SaveStorage.SaveJson(SaveKey, new SaveData { noAdsPurchased = _noAdsPurchased });
            SaveStorage.Flush();
        }
    }
}
