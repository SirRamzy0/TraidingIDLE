using System;
using UnityEngine;
using System.Globalization;
using UnityEngine.UI;
#if TMP_YG2
using TMPro;
#endif

namespace YG
{
    public class LBPlayerDataYG : MonoBehaviour
    {
        public ImageLoadYG imageLoad;

        [Serializable]
        public struct TextLegasy
        {
            public Text rank, name, score;
        }
        public TextLegasy textLegasy;

#if TMP_YG2
        [Serializable]
        public struct TextMP
        {
            public TextMeshProUGUI rank, name, score;
        }
        public TextMP textMP;
#endif
        [Space(10)]
        public MonoBehaviour[] topPlayerActivityComponents = new MonoBehaviour[0];
        public MonoBehaviour[] currentPlayerActivityComponents = new MonoBehaviour[0];

        [Header("Score Formatting")]
        public bool abbreviateScore = false;
        public long scoreMultiplier = 1;
        public string thousandSuffix = "тыс";
        public string millionSuffix = "млн";
        public string billionSuffix = "млрд";
        public string trillionSuffix = "трлн";

        public class Data
        {
            public string rank;
            public string name;
            public string score;
            public string photoUrl;
            public bool inTop;
            public bool currentPlayer;
            public Sprite photoSprite;
        }

        [HideInInspector]
        public Data data = new Data();

        public void UpdateEntries()
        {
            string scoreText = GetScoreText();

            if (textLegasy.rank && data.rank != null) textLegasy.rank.text = data.rank.ToString();
            if (textLegasy.name && data.name != null) textLegasy.name.text = data.name;
            if (textLegasy.score && data.score != null) textLegasy.score.text = scoreText;

#if TMP_YG2
            if (textMP.rank && data.rank != null) textMP.rank.text = data.rank.ToString();
            if (textMP.name && data.name != null) textMP.name.text = data.name;
            if (textMP.score && data.score != null) textMP.score.text = scoreText;
#endif
            if (imageLoad)
            {
                if (data.photoSprite)
                {
                    imageLoad.SetTexture(data.photoSprite.texture);
                }
                else if (data.photoUrl == null)
                {
                    imageLoad.ClearTexture();
                }
                else
                {
                    imageLoad.Load(data.photoUrl);
                }
            }

            if (topPlayerActivityComponents.Length > 0)
            {
                if (data.inTop)
                {
                    ActivityMomoObjects(topPlayerActivityComponents, true);
                }
                else
                {
                    ActivityMomoObjects(topPlayerActivityComponents, false);
                }
            }

            if (currentPlayerActivityComponents.Length > 0)
            {
                if (data.currentPlayer)
                {
                    ActivityMomoObjects(currentPlayerActivityComponents, true);
                }
                else
                {
                    ActivityMomoObjects(currentPlayerActivityComponents, false);
                }
            }

            void ActivityMomoObjects(MonoBehaviour[] objects, bool activity)
            {
                for (int i = 0; i < objects.Length; i++)
                {
                    objects[i].enabled = activity;
                }
            }
        }

        private string GetScoreText()
        {
            if (!abbreviateScore || data.score == null)
                return data.score?.ToString();

            if (!decimal.TryParse(data.score, NumberStyles.Integer, CultureInfo.InvariantCulture, out var score))
                return data.score.ToString();

            var multiplier = Math.Max(1, scoreMultiplier);
            return FormatAbbreviated(score * multiplier);
        }

        private string FormatAbbreviated(decimal value)
        {
            var abs = Math.Abs(value);

            if (abs >= 1_000_000_000_000m)
                return FormatScaled(value / 1_000_000_000_000m, trillionSuffix);

            if (abs >= 1_000_000_000m)
                return FormatScaled(value / 1_000_000_000m, billionSuffix);

            if (abs >= 1_000_000m)
                return FormatScaled(value / 1_000_000m, millionSuffix);

            if (abs >= 1_000m)
                return FormatScaled(value / 1_000m, thousandSuffix);

            return decimal.Round(value, 0, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);
        }

        private static string FormatScaled(decimal value, string suffix)
        {
            var abs = Math.Abs(value);
            string format = abs >= 100m || value == decimal.Truncate(value) ? "0" : abs >= 10m ? "0.#" : "0.##";
            var number = value.ToString(format, CultureInfo.InvariantCulture);
            return string.IsNullOrWhiteSpace(suffix) ? number : $"{number} {suffix}";
        }
    }
}
