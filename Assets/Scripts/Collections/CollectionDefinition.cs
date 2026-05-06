using System;
using UnityEngine;

namespace TraidingIDLE.Collections
{
    [Serializable]
    public sealed class CollectionCardConfig
    {
        public string saveId = "";
        public string title = "Item";
        public Sprite background;
        public Sprite artwork;
        [Min(0)] public long priceRubles;
    }

    [Serializable]
    public sealed class CollectionBonusMilestoneConfig
    {
        [Min(1)] public int requiredOwnedCount = 3;
        [Min(0f)] public float bonusPercent = 5f;
    }

    [Serializable]
    public sealed class CollectionFinalItemConfig
    {
        public string saveId = "final";
        public string title = "Final item";
        public Sprite background;
        public Sprite artwork;
        [Min(0)] public long priceRubles;
    }

    [CreateAssetMenu(menuName = "TraidingIDLE/Collection Definition", fileName = "CollectionDefinition")]
    public sealed class CollectionDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id for saves. Do not change after release.")]
        [SerializeField] private string saveId = "";
        [SerializeField] private string displayName = "Collection";

        [Header("Categories")]
        [SerializeField] private string collectionCategory = "Auto";
        [SerializeField] private string targetBusinessCategory = "Auto";

        [Header("Content")]
        [TextArea] [SerializeField] private string description = "";
        [SerializeField] private CollectionCardConfig[] cards = Array.Empty<CollectionCardConfig>();
        [SerializeField] private CollectionBonusMilestoneConfig[] bonusMilestones =
        {
            new() { requiredOwnedCount = 3, bonusPercent = 5f },
            new() { requiredOwnedCount = 5, bonusPercent = 10f },
            new() { requiredOwnedCount = 10, bonusPercent = 15f },
        };
        [SerializeField] private CollectionFinalItemConfig finalItem = new();

        public string SaveId => string.IsNullOrWhiteSpace(saveId) ? name : saveId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string CollectionCategory => collectionCategory;
        public string TargetBusinessCategory => targetBusinessCategory;
        public string Description => description;
        public CollectionCardConfig[] Cards => cards ?? Array.Empty<CollectionCardConfig>();
        public CollectionBonusMilestoneConfig[] BonusMilestones => bonusMilestones ?? Array.Empty<CollectionBonusMilestoneConfig>();
        public CollectionFinalItemConfig FinalItem => finalItem ?? new CollectionFinalItemConfig();
        public int CardCount => Cards.Length;
    }
}
