using System;
using UnityEngine;

namespace TraidingIDLE.Integrations
{
    public static class YandexRewardedAds
    {
        public const string TemkiDoubleRewardId = "temki_double_reward";
        public const string MiningAdSpeedBoostId = "mining_ad_speed_boost";
        public const string EnergyRefillId = "energy_refill";

        public static void Show(string id, Action rewarded)
        {
            if (rewarded == null)
                return;

#if RewardedAdv_yg
            YG.YG2.RewardedAdvShow(string.IsNullOrEmpty(id) ? "default" : id, rewarded);
#elif UNITY_EDITOR
            rewarded.Invoke();
#else
            Debug.LogWarning($"Rewarded ad was requested, but RewardedAdv_yg is not enabled. Reward id: {id}");
#endif
        }
    }
}
