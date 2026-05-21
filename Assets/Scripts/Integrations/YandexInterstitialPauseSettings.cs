using UnityEngine;

namespace TraidingIDLE.Integrations
{
    [CreateAssetMenu(
        fileName = "YandexInterstitialPauseSettings",
        menuName = "TraidingIDLE/Yandex Interstitial Pause Settings")]
    public sealed class YandexInterstitialPauseSettings : ScriptableObject
    {
        private const string ResourceName = "YandexInterstitialPauseSettings";

        [Header("Session timing")]
        [Min(1f)] public float firstInterstitialDelaySeconds = 600f;
        [Min(1f)] public float repeatInterstitialDelaySeconds = 300f;
        [Min(2)] public int countdownSeconds = 2;
        [Min(0.5f)] public float blockedRetrySeconds = 5f;

        [Header("Pause overlay")]
        public GameObject pauseOverlayPrefab;

        [Header("Interstitial blockers")]
        public string[] blockedActiveRootNames =
        {
            "Shop_dialog",
            "Big_Leaderboard",
            "Reward_dialogs",
            "Messenger_Dialog",
            "CapUpgradeDialog",
            "Stavka_profit_dialog",
            "Temki_Result_Dialog",
        };

        public float FirstInterstitialDelaySeconds => Mathf.Max(1f, firstInterstitialDelaySeconds);
        public float RepeatInterstitialDelaySeconds => Mathf.Max(1f, repeatInterstitialDelaySeconds);
        public int CountdownSeconds => 2;
        public float BlockedRetrySeconds => Mathf.Max(0.5f, blockedRetrySeconds);

        public static YandexInterstitialPauseSettings Load()
        {
            var settings = Resources.Load<YandexInterstitialPauseSettings>(ResourceName);
            if (settings != null)
                return settings;

            return CreateInstance<YandexInterstitialPauseSettings>();
        }
    }
}
