using System;
using System.Collections.Generic;
using UnityEngine;
#if RedefinePlayerPrefs_yg
using YG;
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
        private static bool _criticalFlushPending;
        private static bool _pendingDeleteAll;
        private static SaveStorageFlushRunner _flushRunner;
        private static bool _flushWarningLogged;
        private static bool _ygDataEventSeen;

        public static event Action ExternalDataLoaded;

#if RedefinePlayerPrefs_yg
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void BootstrapYandexStorageEvents()
        {
            YG2.onGetSDKData -= OnYandexStorageDataLoaded;
            YG2.onGetSDKData += OnYandexStorageDataLoaded;
            _ygDataEventSeen = false;
        }
#endif

        public static bool HasKey(string key)
        {
            EnsureYandexStorageEventSubscription();

            if (PendingStringWrites.ContainsKey(key))
                return true;

            if (_pendingDeleteAll || PendingDeletedKeys.Contains(key))
                return false;

            return HasStoredKey(key);
        }

        public static void SaveJson<T>(string key, T data) where T : class
        {
            EnsureYandexStorageEventSubscription();

            if (_writesSuspended)
                return;

            if (data == null)
                return;

            var json = JsonUtility.ToJson(data);
            PendingDeletedKeys.Remove(key);
            PendingStringWrites[key] = json;
            SetStoredString(key, json);
        }

        public static bool TryLoadJson<T>(string key, out T data) where T : class, new()
        {
            EnsureYandexStorageEventSubscription();

            data = null!;
            if (PendingStringWrites.TryGetValue(key, out var pendingJson))
                return TryDeserialize(pendingJson, out data);

            if (_pendingDeleteAll || PendingDeletedKeys.Contains(key))
                return false;

            if (!HasStoredKey(key))
                return false;

            var json = GetStoredString(key);
            if (string.IsNullOrEmpty(json))
                return false;

            return TryDeserialize(json, out data);
        }

        public static void DeleteKey(string key)
        {
            EnsureYandexStorageEventSubscription();

            PendingStringWrites.Remove(key);
            if (!_pendingDeleteAll)
                PendingDeletedKeys.Add(key);

            if (HasStoredKey(key))
                DeleteStoredKey(key);
        }

        public static void DeleteAll()
        {
            EnsureYandexStorageEventSubscription();

            PendingStringWrites.Clear();
            PendingDeletedKeys.Clear();
            _pendingDeleteAll = true;
            DeleteAllStored();
        }

        public static void SuspendWrites()
        {
            _writesSuspended = true;
        }

        public static void ResumeWrites()
        {
            _writesSuspended = false;

            if (_flushPending)
                FlushInternal(false);
        }

        public static void Flush()
        {
            FlushInternal(false);
        }

        public static void FlushCritical()
        {
            FlushInternal(true);
        }

        private static void FlushInternal(bool forceCloudImmediate)
        {
            EnsureYandexStorageEventSubscription();

            if (_writesSuspended)
                return;

            _flushPending = true;
            _criticalFlushPending |= forceCloudImmediate;
            if (!TryFlushNow(forceCloudImmediate))
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

        private static bool TryFlushNow(bool forceCloudImmediate = false)
        {
            if (_writesSuspended)
                return false;

            if (!_flushPending)
                return true;

            if (!CanFlushNow())
                return false;

            try
            {
                var shouldForceCloudImmediate = forceCloudImmediate || _criticalFlushPending;
                ApplyPendingChanges();
                SavePlayerPrefs(shouldForceCloudImmediate);
                ClearPendingChanges();
                _flushPending = false;
                _criticalFlushPending = false;
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
                DeleteAllStored();
            }

            foreach (var key in PendingDeletedKeys)
            {
                if (HasStoredKey(key))
                    DeleteStoredKey(key);
            }

            foreach (var pair in PendingStringWrites)
            {
                SetStoredString(pair.Key, pair.Value);
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

        private static void SavePlayerPrefs(bool forceCloudImmediate)
        {
#if RedefinePlayerPrefs_yg && Storage_yg
            if (!forceCloudImmediate)
            {
                SaveStoredPrefs();
                return;
            }

            var previousFlush = YG2.infoYG.Storage.flush;
            try
            {
                YG2.infoYG.Storage.flush = true;
                SaveStoredPrefs();
            }
            finally
            {
                YG2.infoYG.Storage.flush = previousFlush;
            }
#else
            SaveStoredPrefs();
#endif
        }

        private static bool HasStoredKey(string key)
        {
#if RedefinePlayerPrefs_yg
            try
            {
                if (YG2.iPlatform != null)
                    return RedefineYG.PlayerPrefs.HasKey(key);
            }
            catch
            {
                // Local editor checks can run before YG2 initializes its storage platform.
            }
#endif
            return UnityEngine.PlayerPrefs.HasKey(key);
        }

        private static string GetStoredString(string key)
        {
#if RedefinePlayerPrefs_yg
            try
            {
                if (YG2.iPlatform != null)
                    return RedefineYG.PlayerPrefs.GetString(key);
            }
            catch
            {
                // Local editor fallback.
            }
#endif
            return UnityEngine.PlayerPrefs.GetString(key);
        }

        private static void SetStoredString(string key, string value)
        {
#if RedefinePlayerPrefs_yg
            try
            {
                if (YG2.iPlatform != null)
                {
                    RedefineYG.PlayerPrefs.SetString(key, value);
                    return;
                }
            }
            catch
            {
                // Local editor fallback.
            }
#endif
            UnityEngine.PlayerPrefs.SetString(key, value);
        }

        private static void DeleteStoredKey(string key)
        {
#if RedefinePlayerPrefs_yg
            try
            {
                if (YG2.iPlatform != null)
                {
                    RedefineYG.PlayerPrefs.DeleteKey(key);
                    return;
                }
            }
            catch
            {
                // Local editor fallback.
            }
#endif
            UnityEngine.PlayerPrefs.DeleteKey(key);
        }

        private static void DeleteAllStored()
        {
#if RedefinePlayerPrefs_yg
            try
            {
                if (YG2.iPlatform != null)
                {
                    RedefineYG.PlayerPrefs.DeleteAll();
                    return;
                }
            }
            catch
            {
                // Local editor fallback.
            }
#endif
            UnityEngine.PlayerPrefs.DeleteAll();
        }

        private static void SaveStoredPrefs()
        {
#if RedefinePlayerPrefs_yg
            try
            {
                if (YG2.iPlatform != null)
                {
                    RedefineYG.PlayerPrefs.Save();
                    return;
                }
            }
            catch
            {
                // Local editor fallback.
            }
#endif
            UnityEngine.PlayerPrefs.Save();
        }

#if RedefinePlayerPrefs_yg
        private static void OnYandexStorageDataLoaded()
        {
            if (!_ygDataEventSeen)
            {
                _ygDataEventSeen = true;
                ExternalDataLoaded?.Invoke();
                return;
            }

            ClearPendingChanges();
            _flushPending = false;
            _criticalFlushPending = false;

            ExternalDataLoaded?.Invoke();
            FlushCritical();
        }
#endif

        private static void EnsureYandexStorageEventSubscription()
        {
#if RedefinePlayerPrefs_yg
            YG2.onGetSDKData -= OnYandexStorageDataLoaded;
            YG2.onGetSDKData += OnYandexStorageDataLoaded;
#endif
        }
    }

}
