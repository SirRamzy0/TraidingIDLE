using System;
using System.Collections.Generic;
using TMPro;
using TraidingIDLE.Business;
using TraidingIDLE.Player;
using TraidingIDLE.Saves;
using TraidingIDLE.UI;
using UnityEngine;
using UnityEngine.UI;

namespace TraidingIDLE.Collections
{
    public sealed class CollectionController : MonoBehaviour
    {
        [Serializable]
        private sealed class CollectionSave
        {
            public string id = "";
            public string[] ownedCardIds = Array.Empty<string>();
            public bool finalItemOwned;
        }

        [Serializable]
        private sealed class SaveData
        {
            public CollectionSave[] collections = Array.Empty<CollectionSave>();
            public int activeCollectionIndex;
        }

        private sealed class RuntimeCollection
        {
            public CollectionDefinition definition;
            public bool[] ownedCards = Array.Empty<bool>();
            public bool finalItemOwned;

            public int CardCount => definition != null ? definition.CardCount : 0;

            public int OwnedCount
            {
                get
                {
                    var count = 0;
                    for (var i = 0; i < ownedCards.Length; i++)
                    {
                        if (ownedCards[i])
                            count++;
                    }

                    return count;
                }
            }

            public string GetCardSaveId(int index)
            {
                if (definition == null || index < 0 || index >= definition.Cards.Length)
                    return "";

                var card = definition.Cards[index];
                if (card == null)
                    return "";

                return string.IsNullOrWhiteSpace(card.saveId) ? $"{index}.{card.title}" : card.saveId;
            }
        }

        private const string SaveKey = "save.collections.v1";

        [Header("Refs")]
        [SerializeField] private PlayerProfile profile;
        [SerializeField] private BusinessController businessController;

        [Header("Definitions")]
        [SerializeField] private CollectionDefinition[] collections = Array.Empty<CollectionDefinition>();

        [Header("Filters")]
        [SerializeField] private CollectionFilterButtonUI[] filterButtons = Array.Empty<CollectionFilterButtonUI>();

        [Header("Progress")]
        [SerializeField] private TMP_Text collectionTitleText;
        [SerializeField] private TMP_Text collectionDescriptionText;
        [SerializeField] private Slider progressSlider;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private TMP_Text totalBonusText;

        [Header("Cards")]
        [SerializeField] private CollectionCardUI cardPrefab;
        [SerializeField] private RectTransform cardsContent;

        [Header("Bonus rows")]
        [SerializeField] private CollectionBonusMilestoneRowUI bonusRowPrefab;
        [SerializeField] private RectTransform bonusRowsContent;
        [SerializeField] private CollectionBonusMilestoneRowUI[] bonusRows = Array.Empty<CollectionBonusMilestoneRowUI>();

        [Header("Final item")]
        [SerializeField] private CollectionFinalItemUI finalItemUi;

        [Header("Texts")]
        [SerializeField] private string progressFormat = "Собрано {0}/{1}";
        [SerializeField] private string bonusConditionFormat = "{0}/{1} куплено";
        [SerializeField] private string bonusValueFormat = "+ {0}% к доходу";
        [SerializeField] private string totalBonusFormat = "Общий бонус: {0}%";

        [Header("Number format")]
        [SerializeField] private string thousandSep = ".";

        private readonly List<RuntimeCollection> _runtime = new();
        private CollectionCardUI[] _cardRows = Array.Empty<CollectionCardUI>();
        private CollectionBonusMilestoneRowUI[] _resolvedBonusRows = Array.Empty<CollectionBonusMilestoneRowUI>();
        private int _spawnedCardCount = -1;
        private int _spawnedBonusRowCount = -1;
        private int _activeIndex;
        private float _saveTimer;
        private bool _dirty;
        private bool _initialized;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            EnsureInitialized();

            if (profile != null)
                profile.RublesChanged += OnRublesChanged;

            RefreshAllUi();
        }

        private void OnDisable()
        {
            if (profile != null)
                profile.RublesChanged -= OnRublesChanged;

            SaveNow();
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause)
                SaveNow();
        }

        private void OnApplicationQuit()
        {
            SaveNow();
        }

        private void Update()
        {
            if (!_dirty)
                return;

            _saveTimer -= Time.unscaledDeltaTime;
            if (_saveTimer <= 0f)
                SaveNow();
        }

        public double GetBusinessIncomeMultiplier(string businessCategory)
        {
            EnsureInitialized();

            var normalizedBusinessCategory = NormalizeKey(businessCategory);
            if (string.IsNullOrEmpty(normalizedBusinessCategory))
                return 1d;

            var bonusPercent = 0d;
            for (var i = 0; i < _runtime.Count; i++)
            {
                var item = _runtime[i];
                if (item?.definition == null)
                    continue;

                if (!string.Equals(
                        NormalizeKey(item.definition.TargetBusinessCategory),
                        normalizedBusinessCategory,
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                bonusPercent += GetUnlockedBonusPercent(item);
            }

            return Math.Max(1d, 1d + bonusPercent / 100d);
        }

        private void EnsureInitialized()
        {
            if (_initialized)
                return;

            ResolveSceneReferences();
            RebuildRuntimeCollections();
            BindFilterButtons();
            Load();
            _activeIndex = _runtime.Count <= 0 ? 0 : Mathf.Clamp(_activeIndex, 0, _runtime.Count - 1);
            _initialized = true;
            RefreshAllUi();
        }

        private void ResolveSceneReferences()
        {
            if (profile == null)
                profile = FindAnyObjectByType<PlayerProfile>();

            if (businessController == null)
                businessController = FindAnyObjectByType<BusinessController>(FindObjectsInactive.Include);
        }

        private void RebuildRuntimeCollections()
        {
            _runtime.Clear();

            if (collections == null)
                return;

            for (var i = 0; i < collections.Length; i++)
            {
                var definition = collections[i];
                if (definition == null)
                    continue;

                _runtime.Add(new RuntimeCollection
                {
                    definition = definition,
                    ownedCards = new bool[definition.CardCount],
                });
            }
        }

        private void BindFilterButtons()
        {
            if (filterButtons == null)
                return;

            for (var i = 0; i < filterButtons.Length; i++)
            {
                var button = filterButtons[i];
                if (button == null)
                    continue;

                button.Bind(SelectCollectionCategory);
            }
        }

        private void SelectCollectionCategory(string category)
        {
            var normalized = NormalizeKey(category);
            for (var i = 0; i < _runtime.Count; i++)
            {
                var definition = _runtime[i].definition;
                if (definition == null)
                    continue;

                if (!string.Equals(
                        NormalizeKey(definition.CollectionCategory),
                        normalized,
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                _activeIndex = i;
                MarkDirty();
                RefreshAllUi();
                return;
            }
        }

        private void BuyCard(int cardIndex)
        {
            EnsureInitialized();
            if (!IsValidActiveCollection() || profile == null)
                return;

            var active = _runtime[_activeIndex];
            if (cardIndex < 0 || cardIndex >= active.CardCount || active.ownedCards[cardIndex])
                return;

            var card = active.definition.Cards[cardIndex];
            if (card == null)
                return;

            var cost = Math.Max(0, card.priceRubles);
            if (!profile.TrySpendRubles(cost))
                return;

            active.ownedCards[cardIndex] = true;
            MarkDirty();
            NotifyBusinessBonusesChanged();
            RefreshAllUi();
        }

        private void BuyFinalItem()
        {
            EnsureInitialized();
            if (!IsValidActiveCollection() || profile == null)
                return;

            var active = _runtime[_activeIndex];
            if (active.finalItemOwned || !IsFinalItemUnlocked(active))
                return;

            var finalItem = active.definition.FinalItem;
            var cost = Math.Max(0, finalItem.priceRubles);
            if (!profile.TrySpendRubles(cost))
                return;

            active.finalItemOwned = true;
            MarkDirty();
            RefreshAllUi();
        }

        private void RefreshAllUi()
        {
            if (!IsValidActiveCollection())
            {
                RefreshFilterButtons();
                return;
            }

            var active = _runtime[_activeIndex];
            var definition = active.definition;
            var owned = active.OwnedCount;
            var total = active.CardCount;

            if (collectionTitleText != null)
                collectionTitleText.text = definition.DisplayName;

            if (collectionDescriptionText != null)
                collectionDescriptionText.text = definition.Description;

            if (progressSlider != null)
            {
                progressSlider.minValue = 0f;
                progressSlider.maxValue = Mathf.Max(1, total);
                progressSlider.value = Mathf.Clamp(owned, 0, Mathf.Max(1, total));
            }

            if (progressText != null)
                progressText.text = GameTextFormatter.Format(progressFormat, "Собрано {0}/{1}", owned, total);

            if (totalBonusText != null)
                totalBonusText.text = GameTextFormatter.Format(
                    totalBonusFormat,
                    "Общий бонус: {0}%",
                    GameTextFormatter.Percent(GetUnlockedBonusPercent(active)));

            RefreshCards(active);
            RefreshBonusRows(active);
            RefreshFinalItem(active);
            RefreshFilterButtons();
        }

        private void RefreshCards(RuntimeCollection active)
        {
            EnsureCardRows(active);

            for (var i = 0; i < _cardRows.Length; i++)
            {
                var row = _cardRows[i];
                if (row == null)
                    continue;

                if (i < 0 || i >= active.CardCount)
                {
                    row.gameObject.SetActive(false);
                    continue;
                }

                var card = active.definition.Cards[i];
                if (card == null)
                {
                    row.gameObject.SetActive(false);
                    continue;
                }

                var owned = active.ownedCards[i];
                var cost = Math.Max(0, card.priceRubles);
                var captured = i;

                row.gameObject.SetActive(true);
                row.Configure(
                    card.background,
                    card.artwork,
                    card.title,
                    FormatNumberMoney(cost),
                    owned,
                    profile != null && profile.Rubles >= cost,
                    () => BuyCard(captured));
            }

            UiTransformUtility.RebuildLayout(cardsContent);
        }

        private void EnsureCardRows(RuntimeCollection active)
        {
            if (_spawnedCardCount == active.CardCount && _cardRows.Length == active.CardCount)
                return;

            if (cardsContent == null || cardPrefab == null)
            {
                _spawnedCardCount = -1;
                _cardRows = Array.Empty<CollectionCardUI>();
                return;
            }

            _spawnedCardCount = active.CardCount;
            UiTransformUtility.DestroyChildren(cardsContent);
            var rows = new CollectionCardUI[active.CardCount];
            for (var i = 0; i < rows.Length; i++)
            {
                var row = Instantiate(cardPrefab, cardsContent, false);
                row.gameObject.name = $"{cardPrefab.name}_{i + 1}";
                rows[i] = row;
            }

            _cardRows = rows;
        }

        private void RefreshBonusRows(RuntimeCollection active)
        {
            var milestones = active.definition.BonusMilestones;
            EnsureBonusRows(milestones.Length);

            for (var i = 0; i < _resolvedBonusRows.Length; i++)
            {
                var row = _resolvedBonusRows[i];
                if (row == null)
                    continue;

                if (i >= milestones.Length)
                {
                    row.gameObject.SetActive(false);
                    continue;
                }

                var milestone = milestones[i];
                if (milestone == null)
                {
                    row.gameObject.SetActive(false);
                    continue;
                }

                var reached = active.OwnedCount >= milestone.requiredOwnedCount;
                row.Configure(
                    GameTextFormatter.Format(
                        bonusConditionFormat,
                        "{0}/{1} куплено",
                        milestone.requiredOwnedCount,
                        active.CardCount),
                    GameTextFormatter.Format(
                        bonusValueFormat,
                        "+ {0}% к доходу",
                        GameTextFormatter.Percent(milestone.bonusPercent)),
                    reached);
            }

            UiTransformUtility.RebuildLayout(bonusRowsContent);
        }

        private void EnsureBonusRows(int count)
        {
            if (bonusRowPrefab != null && bonusRowsContent != null)
            {
                if (_spawnedBonusRowCount == count && _resolvedBonusRows.Length == count)
                    return;

                _spawnedBonusRowCount = count;
                UiTransformUtility.DestroyChildren(bonusRowsContent);
                var rows = new CollectionBonusMilestoneRowUI[count];
                for (var i = 0; i < count; i++)
                {
                    var row = Instantiate(bonusRowPrefab, bonusRowsContent, false);
                    row.gameObject.name = $"{bonusRowPrefab.name}_{i + 1}";
                    rows[i] = row;
                }

                _resolvedBonusRows = rows;
                return;
            }

            _resolvedBonusRows = bonusRows ?? Array.Empty<CollectionBonusMilestoneRowUI>();
        }

        private void RefreshFinalItem(RuntimeCollection active)
        {
            if (finalItemUi == null)
                return;

            var finalItem = active.definition.FinalItem;
            var unlocked = IsFinalItemUnlocked(active);
            var cost = Math.Max(0, finalItem.priceRubles);

            finalItemUi.Configure(
                finalItem.background,
                finalItem.artwork,
                finalItem.title,
                FormatNumberMoney(cost),
                unlocked,
                active.finalItemOwned,
                profile != null && profile.Rubles >= cost,
                BuyFinalItem);
        }

        private void RefreshFilterButtons()
        {
            if (filterButtons == null)
                return;

            var activeCategory = IsValidActiveCollection()
                ? NormalizeKey(_runtime[_activeIndex].definition.CollectionCategory)
                : "";

            for (var i = 0; i < filterButtons.Length; i++)
            {
                var button = filterButtons[i];
                if (button == null)
                    continue;

                button.SetSelected(string.Equals(
                    NormalizeKey(button.CategoryKey),
                    activeCategory,
                    StringComparison.OrdinalIgnoreCase));
            }
        }

        private double GetUnlockedBonusPercent(RuntimeCollection active)
        {
            if (active?.definition == null)
                return 0d;

            var bonus = 0d;
            var owned = active.OwnedCount;
            var milestones = active.definition.BonusMilestones;
            for (var i = 0; i < milestones.Length; i++)
            {
                var milestone = milestones[i];
                if (milestone != null && owned >= milestone.requiredOwnedCount)
                    bonus += Math.Max(0f, milestone.bonusPercent);
            }

            return bonus;
        }

        private bool IsFinalItemUnlocked(RuntimeCollection active)
        {
            return active != null && active.CardCount > 0 && active.OwnedCount >= active.CardCount;
        }

        private bool IsValidActiveCollection()
        {
            return _activeIndex >= 0 && _activeIndex < _runtime.Count && _runtime[_activeIndex]?.definition != null;
        }

        private void NotifyBusinessBonusesChanged()
        {
            if (businessController == null)
                businessController = FindAnyObjectByType<BusinessController>(FindObjectsInactive.Include);

            if (businessController != null)
                businessController.RefreshIncomeFromExternalBonuses();
        }

        private void OnRublesChanged(long _)
        {
            RefreshAllUi();
        }

        private void MarkDirty()
        {
            _dirty = true;
            if (_saveTimer <= 0f)
                _saveTimer = 2f;
        }

        private void SaveNow()
        {
            if (!_initialized)
                return;

            var collectionSaves = new CollectionSave[_runtime.Count];
            for (var i = 0; i < _runtime.Count; i++)
            {
                var item = _runtime[i];
                collectionSaves[i] = new CollectionSave
                {
                    id = item.definition.SaveId,
                    ownedCardIds = GetOwnedCardIds(item),
                    finalItemOwned = item.finalItemOwned,
                };
            }

            SaveStorage.SaveJson(SaveKey, new SaveData
            {
                collections = collectionSaves,
                activeCollectionIndex = _activeIndex,
            });
            SaveStorage.Flush();
            _dirty = false;
            _saveTimer = 0f;
        }

        private string[] GetOwnedCardIds(RuntimeCollection item)
        {
            var result = new List<string>();
            for (var i = 0; i < item.ownedCards.Length; i++)
            {
                if (item.ownedCards[i])
                    result.Add(item.GetCardSaveId(i));
            }

            return result.ToArray();
        }

        private void Load()
        {
            if (!SaveStorage.TryLoadJson(SaveKey, out SaveData data))
                return;

            _activeIndex = _runtime.Count <= 0 ? 0 : Mathf.Clamp(data.activeCollectionIndex, 0, _runtime.Count - 1);
            if (data.collections == null)
                return;

            var map = new Dictionary<string, CollectionSave>(StringComparer.Ordinal);
            for (var i = 0; i < data.collections.Length; i++)
            {
                var item = data.collections[i];
                if (item == null || string.IsNullOrEmpty(item.id))
                    continue;

                map[item.id] = item;
            }

            for (var i = 0; i < _runtime.Count; i++)
            {
                var item = _runtime[i];
                if (!map.TryGetValue(item.definition.SaveId, out var save))
                    continue;

                LoadCollectionState(item, save);
            }
        }

        private void LoadCollectionState(RuntimeCollection item, CollectionSave save)
        {
            item.finalItemOwned = save.finalItemOwned;

            if (save.ownedCardIds == null || save.ownedCardIds.Length == 0)
                return;

            var owned = new HashSet<string>(save.ownedCardIds, StringComparer.Ordinal);
            for (var i = 0; i < item.ownedCards.Length; i++)
                item.ownedCards[i] = owned.Contains(item.GetCardSaveId(i));
        }

        private string FormatNumberMoney(double value)
        {
            return GameTextFormatter.WholeNumber(value, thousandSep);
        }

        private static string NormalizeKey(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "" : value.Trim();
        }
    }
}
