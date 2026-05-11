using System;

namespace TraidingIDLE.Integrations
{
    public static class YandexRewardedAds
    {
        public const string TemkiDoubleRewardId = "temki_double_reward";
        public const string MiningAdSpeedBoostId = "mining_ad_speed_boost";

        public static void Show(string id, Action rewarded)
        {
            if (rewarded == null)
                return;

#if RewardedAdv_yg
            YG.YG2.RewardedAdvShow(string.IsNullOrEmpty(id) ? "default" : id, rewarded);
#else
            rewarded.Invoke();
#endif
        }
    }
}
