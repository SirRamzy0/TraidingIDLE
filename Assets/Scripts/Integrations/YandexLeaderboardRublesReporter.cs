using System;
using TraidingIDLE.Player;
using TraidingIDLE.Saves;
using UnityEngine;
using YG;

namespace TraidingIDLE.Integrations
{
    public sealed class YandexLeaderboardRublesReporter : MonoBehaviour
    {
        private const long RublesPerBillion = 1_000_000_000L;
        private static YandexLeaderboardRublesReporter _instance;

        [SerializeField] private PlayerProfile profile;
        [SerializeField] private string leaderboardName = "Billioniers";
        [SerializeField] private string saveKey = "save.leaderboard.billioniers.v1";
        [SerializeField, Min(0)] private int minimumBillionsToSubmit = 1;
        [SerializeField] private bool submitOnEnable = true;

        [Serializable]
        private sealed class SaveData
        {
            public int bestSubmittedBillions;
        }

        private int _bestSubmittedBillions;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInstance()
        {
            if (_instance != null)
                return;

            var existing = FindFirstObjectByType<YandexLeaderboardRublesReporter>();
            if (existing != null)
            {
                _instance = existing;
                return;
            }

            var go = new GameObject(nameof(YandexLeaderboardRublesReporter));
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<YandexLeaderboardRublesReporter>();
        }

        private void OnEnable()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            if (profile == null)
                profile = FindFirstObjectByType<PlayerProfile>();

            _bestSubmittedBillions = LoadBestSubmittedBillions();

            if (profile != null)
            {
                profile.RublesChanged += OnRublesChanged;

                if (submitOnEnable)
                    TrySubmit(profile.Rubles);
            }
        }

        private void OnDisable()
        {
            if (profile != null)
                profile.RublesChanged -= OnRublesChanged;
        }

        private void OnRublesChanged(long rubles)
        {
            TrySubmit(rubles);
        }

        private void TrySubmit(long rubles)
        {
            if (string.IsNullOrWhiteSpace(leaderboardName))
                return;

            var billions = ToBillionsScore(rubles);
            if (billions < minimumBillionsToSubmit || billions <= _bestSubmittedBillions)
                return;

            _bestSubmittedBillions = billions;
            SaveBestSubmittedBillions(billions);
            YG2.SetLeaderboard(leaderboardName.Trim(), billions);
        }

        private static int ToBillionsScore(long rubles)
        {
            if (rubles <= 0)
                return 0;

            return rubles / RublesPerBillion > int.MaxValue
                ? int.MaxValue
                : (int)(rubles / RublesPerBillion);
        }

        private int LoadBestSubmittedBillions()
        {
            if (string.IsNullOrWhiteSpace(saveKey))
                return 0;

            return SaveStorage.TryLoadJson<SaveData>(saveKey, out var data) && data != null
                ? Math.Max(0, data.bestSubmittedBillions)
                : 0;
        }

        private void SaveBestSubmittedBillions(int billions)
        {
            if (string.IsNullOrWhiteSpace(saveKey))
                return;

            SaveStorage.SaveJson(saveKey, new SaveData
            {
                bestSubmittedBillions = Math.Max(0, billions),
            });
            SaveStorage.Flush();
        }
    }
}
