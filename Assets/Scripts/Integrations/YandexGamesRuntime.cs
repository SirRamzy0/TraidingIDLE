using UnityEngine;

namespace TraidingIDLE.Integrations
{
    public static class YandexGamesRuntime
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
#if StickyAdv_yg
            if (YG.YG2.isSDKEnabled)
            {
                EnableStickyAd();
            }
            else
            {
                YG.YG2.onGetSDKData -= EnableStickyAd;
                YG.YG2.onGetSDKData += EnableStickyAd;
            }
#endif
        }

#if StickyAdv_yg
        private static void EnableStickyAd()
        {
            YG.YG2.onGetSDKData -= EnableStickyAd;
            YG.YG2.StickyAdActivity(true);
        }
#endif
    }
}
