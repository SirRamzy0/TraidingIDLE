using System;
using TraidingIDLE.Currencies;
using UnityEngine;

namespace TraidingIDLE.Business
{
    [Serializable]
    public sealed class BusinessProgressionConfig
    {
        [Header("Unlock")]
        public string requiredBusinessSaveId = "";
        [Min(0)] public int requiredBusinessLevel;

        [Header("Starting values")]
        [Min(0)] public long startRublesCost = 50_000;
        [Min(0)] public long startUpgradeRublesCost = 90_000;

        [Header("Formula")]
        [Min(1.01f)] public float upgradeCostGrowthPerLevel = 1.24f;
        [Min(1.01f)] public float incomeGrowthPerLevel = 1.16f;
        [Min(0.1f)] public float startingPaybackHours = 1.8f;
        [Min(0)] public int lateCostAccelerationStartLevel = 24;
        [Min(1f)] public float lateCostAccelerationPerLevel = 1.025f;

        public long GetUpgradeCostFromLevel(int currentLevel)
        {
            if (currentLevel <= 0)
                return ToSaturatedLong(RoundToReadableMoney(Math.Max(0, startRublesCost)));

            var baseCost = Math.Max(0d, startUpgradeRublesCost);
            if (baseCost <= 0d)
                return 0;

            var growthSteps = Math.Max(0, currentLevel - 1);
            var acceleratedSteps = Math.Max(0, currentLevel - Math.Max(0, lateCostAccelerationStartLevel));
            var value = baseCost
                * Math.Pow(Math.Max(1.01f, upgradeCostGrowthPerLevel), growthSteps)
                * Math.Pow(Math.Max(1f, lateCostAccelerationPerLevel), acceleratedSteps);

            return ToSaturatedLong(RoundToReadableMoney(value));
        }

        public double GetIncomePerHour(int level)
        {
            if (level <= 0)
                return 0d;

            var paybackHours = Math.Max(0.1d, startingPaybackHours);
            var incomeGrowth = Math.Max(1.01d, incomeGrowthPerLevel);
            var total = Math.Max(0d, startRublesCost) / paybackHours;

            if (level <= 1)
                return RoundToReadableMoney(total);

            var upgradeBaseIncome = Math.Max(0d, startUpgradeRublesCost) / paybackHours;
            var upgradeLevels = level - 1;
            var growthSum = (Math.Pow(incomeGrowth, upgradeLevels) - 1d) / (incomeGrowth - 1d);
            total += upgradeBaseIncome * growthSum;
            return RoundToReadableMoney(Math.Max(0d, total));
        }

        public BusinessProgressionConfig Clone()
        {
            return new BusinessProgressionConfig
            {
                requiredBusinessSaveId = requiredBusinessSaveId,
                requiredBusinessLevel = Math.Max(0, requiredBusinessLevel),
                startRublesCost = Math.Max(0, startRublesCost),
                startUpgradeRublesCost = Math.Max(0, startUpgradeRublesCost),
                upgradeCostGrowthPerLevel = Math.Max(1.01f, upgradeCostGrowthPerLevel),
                incomeGrowthPerLevel = Math.Max(1.01f, incomeGrowthPerLevel),
                startingPaybackHours = Math.Max(0.1f, startingPaybackHours),
                lateCostAccelerationStartLevel = Math.Max(0, lateCostAccelerationStartLevel),
                lateCostAccelerationPerLevel = Math.Max(1f, lateCostAccelerationPerLevel),
            };
        }

        private static long ToSaturatedLong(double value)
        {
            if (double.IsNaN(value) || value <= 0d)
                return 0;
            if (double.IsInfinity(value) || value >= long.MaxValue)
                return long.MaxValue;

            return (long)Math.Ceiling(value);
        }

        private static double RoundToReadableMoney(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0d)
                return 0d;

            var step = value switch
            {
                < 1_000d => 1d,
                < 10_000d => 1_000d,
                < 250_000d => 5_000d,
                < 1_000_000d => 10_000d,
                < 10_000_000d => 50_000d,
                < 100_000_000d => 100_000d,
                < 1_000_000_000d => 500_000d,
                _ => 5_000_000d,
            };

            return Math.Max(step, Math.Round(value / step, MidpointRounding.AwayFromZero) * step);
        }
    }

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
        [SerializeField] private BusinessProgressionConfig progression = new();

        [SerializeField, HideInInspector] private BusinessLevelConfig[] levels =
        {
            new() { incomeRublesPerHour = 10000d, upgradeRublesCost = 1000 },
        };

        [Header("Skill")]
        [SerializeField] private BusinessSkillConfig skill = new();

        public string SaveId => string.IsNullOrWhiteSpace(saveId) ? name : saveId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Category => category;
        public Sprite Artwork => artwork;
        public BusinessProgressionConfig Progression => progression ?? new BusinessProgressionConfig();
        public BusinessSkillConfig Skill => skill;

        public double GetIncomePerHour(int level)
        {
            return Progression.GetIncomePerHour(level);
        }

        public long GetUpgradeCostFromLevel(int currentLevel)
        {
            return Progression.GetUpgradeCostFromLevel(currentLevel);
        }
    }
}
