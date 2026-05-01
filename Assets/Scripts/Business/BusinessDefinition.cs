using System;
using TraidingIDLE.Currencies;
using UnityEngine;

namespace TraidingIDLE.Business
{
    [Serializable]
    public sealed class BusinessLevelConfig
    {
        [Min(0f)] public double incomeRublesPerHour;
        [Min(0)] public long upgradeRublesCost;
    }

    [Serializable]
    public sealed class BusinessSkillConfig
    {
        [Header("Unlock")]
        [Min(1)] public int unlockLevel = 5;
        [Min(0)] public int energyCost = 1;

        [Header("View")]
        public Sprite icon;
        public string title = "Skill";
        [TextArea] public string description = "";

        [Header("Effect")]
        public BusinessSkillEffectKind effect = BusinessSkillEffectKind.InstantRubles;
        [Min(0)] public long instantRubles;
        [Min(1f)] public float temporaryIncomeMultiplier = 5f;
        [Min(1f)] public float temporaryDurationSeconds = 600f;

        [Header("Market skill")]
        public CurrencyId marketCurrency = CurrencyId.SHT;
        public Vector2 marketGrowDurationSeconds = new(10f, 20f);
        public Vector2 marketDumpDurationSeconds = new(5f, 10f);
        public Vector2 marketGrowthPercent = new(0.25f, 0.45f);
        [Range(0f, 0.3f)] public float marketReturnTolerancePercent = 0.04f;

        [Header("Texts")]
        public string lockedSkillFormat = "Улучши до {0} уровня чтобы открыть";
        public string launchButtonIdleFormat = "Запустить ⚡{0}";
        public string temporarySkillActiveFormat = "навык активен {0}";
    }

    [CreateAssetMenu(menuName = "TraidingIDLE/Business Definition", fileName = "BusinessDefinition")]
    public sealed class BusinessDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id for saves. Do not change after release.")]
        [SerializeField] private string saveId = "";
        [SerializeField] private string displayName = "Business";
        [SerializeField] private string category = "Other";
        [SerializeField] private Sprite artwork;

        [Header("Progression")]
        [SerializeField] private BusinessLevelConfig[] levels =
        {
            new() { incomeRublesPerHour = 10000d, upgradeRublesCost = 1000 },
        };

        [Header("Skill")]
        [SerializeField] private BusinessSkillConfig skill = new();

        public string SaveId => string.IsNullOrWhiteSpace(saveId) ? name : saveId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Category => category;
        public Sprite Artwork => artwork;
        public BusinessSkillConfig Skill => skill;
        public int MaxLevel => Mathf.Max(0, levels?.Length ?? 0);

        public double GetIncomePerHour(int level)
        {
            if (levels == null || levels.Length == 0 || level <= 0)
                return 0d;

            var index = Mathf.Clamp(level - 1, 0, levels.Length - 1);
            return Math.Max(0d, levels[index].incomeRublesPerHour);
        }

        public long GetUpgradeCostFromLevel(int currentLevel)
        {
            if (levels == null || levels.Length == 0 || currentLevel < 0 || currentLevel >= levels.Length)
                return 0;

            return Math.Max(0, levels[currentLevel].upgradeRublesCost);
        }
    }
}
