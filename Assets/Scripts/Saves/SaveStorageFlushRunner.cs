using UnityEngine;

namespace TraidingIDLE.Saves
{
    public sealed class SaveStorageFlushRunner : MonoBehaviour
    {
        private float _retryTimer;

        private void Update()
        {
            _retryTimer -= Time.unscaledDeltaTime;
            if (_retryTimer > 0f)
                return;

            _retryTimer = SaveStorage.FlushRetryIntervalSeconds;
            SaveStorage.RunPendingFlushForRunner();
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause)
                SaveStorage.Flush();
        }

        private void OnApplicationQuit()
        {
            SaveStorage.Flush();
            SaveStorage.RunPendingFlushForRunner();
        }
    }
}
