using UnityEngine;
#if RedefinePlayerPrefs_yg
using PlayerPrefs = RedefineYG.PlayerPrefs;
#endif

namespace TraidingIDLE.Saves
{
    public static class SaveStorage
    {
        private static bool _writesSuspended;

        public static bool HasKey(string key) => PlayerPrefs.HasKey(key);

        public static void SaveJson<T>(string key, T data) where T : class
        {
            if (_writesSuspended)
                return;

            if (data == null)
                return;

            var json = JsonUtility.ToJson(data);
            PlayerPrefs.SetString(key, json);
        }

        public static bool TryLoadJson<T>(string key, out T data) where T : class, new()
        {
            data = null!;
            if (!PlayerPrefs.HasKey(key))
                return false;

            var json = PlayerPrefs.GetString(key);
            if (string.IsNullOrEmpty(json))
                return false;

            try
            {
                data = JsonUtility.FromJson<T>(json);
                return data != null;
            }
            catch
            {
                return false;
            }
        }

        public static void DeleteKey(string key)
        {
            if (PlayerPrefs.HasKey(key))
                PlayerPrefs.DeleteKey(key);
        }

        public static void DeleteAll()
        {
            PlayerPrefs.DeleteAll();
        }

        public static void SuspendWrites()
        {
            _writesSuspended = true;
        }

        public static void ResumeWrites()
        {
            _writesSuspended = false;
        }

        public static void Flush()
        {
            if (_writesSuspended)
                return;

            PlayerPrefs.Save();
        }
    }
}
