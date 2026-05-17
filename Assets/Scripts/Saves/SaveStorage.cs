using System;
using System.Collections.Generic;
using UnityEngine;
#if RedefinePlayerPrefs_yg
using YG;
using PlayerPrefs = RedefineYG.PlayerPrefs;
#endif

namespace TraidingIDLE.Saves
{
    public static class SaveStorage
    {
        internal const float FlushRetryIntervalSeconds = 1f;

        private static readonly Dictionary<string, string> PendingStringWrites = new();
        private static readonly HashSet<string> PendingDeletedKeys = new();

        private static bool _writesSuspended;
        private static bool _flushPending;
        private static bool _pendingDeleteAll;
        private static SaveStorageFlushRunner _flushRunner;
        private static bool _flushWarningLogged;

        public static bool HasKey(string key)
        {
            if (PendingStringWrites.ContainsKey(key))
                return true;

            if (_pendingDeleteAll || PendingDeletedKeys.Contains(key))
                return false;

            return PlayerPrefs.HasKey(key);
        }

        public static void SaveJson<T>(string key, T data) where T : class
        {
            if (_writesSuspended)
                return;

            if (data == null)
                return;

            var json = JsonUtility.ToJson(data);
            PendingDeletedKeys.Remove(key);
            PendingStringWrites[key] = json;
            PlayerPrefs.SetString(key, json);
        }

        public static bool TryLoadJson<T>(string key, out T data) where T : class, new()
        {
            data = null!;
            if (PendingStringWrites.TryGetValue(key, out var pendingJson))
                return TryDeserialize(pendingJson, out data);

            if (_pendingDeleteAll || PendingDeletedKeys.Contains(key))
                return false;

            if (!PlayerPrefs.HasKey(key))
                return false;

            var json = PlayerPrefs.GetString(key);
            if (string.IsNullOrEmpty(json))
                return false;

            return TryDeserialize(json, out data);
        }

        public static void DeleteKey(string key)
        {
            PendingStringWrites.Remove(key);
            if (!_pendingDeleteAll)
                PendingDeletedKeys.Add(key);

            if (PlayerPrefs.HasKey(key))
                PlayerPrefs.DeleteKey(key);
        }

        public static void DeleteAll()
        {
            PendingStringWrites.Clear();
            PendingDeletedKeys.Clear();
            _pendingDeleteAll = true;
            PlayerPrefs.DeleteAll();
        }

        public static void SuspendWrites()
        {
            _writesSuspended = true;
        }

        public static void ResumeWrites()
        {
            _writesSuspended = false;

            if (_flushPending)
                Flush();
        }

        public static void Flush()
        {
            if (_writesSuspended)
                return;

            _flushPending = true;
            if (!TryFlushNow())
                EnsureFlushRunner();
        }

        private static bool TryDeserialize<T>(string json, out T data) where T : class, new()
        {
            data = null!;
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

        private static bool TryFlushNow()
        {
            if (_writesSuspended)
                return false;

            if (!_flushPending)
                return true;

            if (!CanFlushNow())
                return false;

            try
            {
                ApplyPendingChanges();
                PlayerPrefs.Save();
                ClearPendingChanges();
                _flushPending = false;
                _flushWarningLogged = false;
                StopFlushRunnerIfIdle();
                return true;
            }
            catch (Exception e)
            {
                if (!_flushWarningLogged)
                {
                    _flushWarningLogged = true;
                    Debug.LogWarning($"Save flush failed. It will be retried automatically. {e.Message}");
                }

                return false;
            }
        }

        private static void ApplyPendingChanges()
        {
            if (_pendingDeleteAll)
            {
                PlayerPrefs.DeleteAll();
            }

            foreach (var key in PendingDeletedKeys)
            {
                if (PlayerPrefs.HasKey(key))
                    PlayerPrefs.DeleteKey(key);
            }

            foreach (var pair in PendingStringWrites)
            {
                PlayerPrefs.SetString(pair.Key, pair.Value);
            }
        }

        private static void ClearPendingChanges()
        {
            _pendingDeleteAll = false;
            PendingDeletedKeys.Clear();
            PendingStringWrites.Clear();
        }

        private static bool CanFlushNow()
        {
#if RedefinePlayerPrefs_yg
            return YG2.isSDKEnabled;
#else
            return true;
#endif
        }

        private static void EnsureFlushRunner()
        {
            if (_flushRunner != null)
                return;

            var go = new GameObject("Save Storage Flush Runner");
            go.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(go);
            _flushRunner = go.AddComponent<SaveStorageFlushRunner>();
        }

        private static void StopFlushRunnerIfIdle()
        {
            if (_flushPending || _flushRunner == null)
                return;

            UnityEngine.Object.Destroy(_flushRunner.gameObject);
            _flushRunner = null;
        }

        internal static bool RunPendingFlushForRunner()
        {
            return TryFlushNow();
        }
    }

}
