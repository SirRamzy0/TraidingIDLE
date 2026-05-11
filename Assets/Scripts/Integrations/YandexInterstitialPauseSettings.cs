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
        [Min(1)] public int countdownSeconds = 3;

        public float FirstInterstitialDelaySeconds => Mathf.Max(1f, firstInterstitialDelaySeconds);
        public float RepeatInterstitialDelaySeconds => Mathf.Max(1f, repeatInterstitialDelaySeconds);
        public int CountdownSeconds => Mathf.Max(1, countdownSeconds);

        public static YandexInterstitialPauseSettings Load()
        {
            var settings = Resources.Load<YandexInterstitialPauseSettings>(ResourceName);
            if (settings != null)
                return settings;

            return CreateInstance<YandexInterstitialPauseSettings>();
        }
    }
}
