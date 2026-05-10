using UnityEngine;

namespace TraidingIDLE.Temki
{
    [CreateAssetMenu(menuName = "Traiding IDLE/Temki/Temka Definition", fileName = "Temka_Definition")]
    public sealed class TemkaDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string saveId = "";
        [SerializeField] private string displayName = "Темка";
        [SerializeField, TextArea] private string description = "";

        [Header("Visual")]
        [SerializeField] private Sprite artwork;

        [Header("Balance")]
        [SerializeField, Range(0f, 1f)] private float successChance = 0.35f;
        [SerializeField, Range(0f, 1f)] private float displayedSuccessChance = 0.38f;
        [SerializeField, Min(1f)] private float rewardMultiplier = 2f;
        [SerializeField, Min(1)] private int durationSeconds = 300;
        [SerializeField, Min(0)] private long stakeRubles = 100_000;
        [SerializeField, Min(1f)] private float stakeGrowthPerUse = 1.55f;

        public string SaveId => string.IsNullOrWhiteSpace(saveId) ? name : saveId.Trim();
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Artwork => artwork;
        public float SuccessChance => Mathf.Clamp01(successChance);
        public float DisplayedSuccessChance => Mathf.Clamp01(displayedSuccessChance);
        public float RewardMultiplier => Mathf.Max(1f, rewardMultiplier);
        public int DurationSeconds => Mathf.Max(1, durationSeconds);
        public long StakeRubles => System.Math.Max(0, stakeRubles);
        public float StakeGrowthPerUse => Mathf.Max(1f, stakeGrowthPerUse);
    }
}
