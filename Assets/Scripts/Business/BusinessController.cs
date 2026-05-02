using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using TraidingIDLE.Currencies.Simulation;
using TraidingIDLE.Player;
using TraidingIDLE.Saves;
using UnityEngine;
using UnityEngine.UI;

namespace TraidingIDLE.Business
{
    public sealed class BusinessController : MonoBehaviour
    {
        [Serializable]
        private sealed class BusinessLevelSave
        {
            public string id = "";
            public int level;
        }

        [Serializable]
        private sealed class SaveData
        {
            public BusinessLevelSave[] businessLevels = Array.Empty<BusinessLevelSave>();
            public double accumulatedRubles;
            public int energyCurrent;
            public long lastSimUtcSeconds;
            public long nextEnergyAtUtc;
            public byte tempKind;
            public long tempBuffEndUtc;
            public float tempMultiplierHeld;
            public int selectedBusinessIndex;
        }

        private sealed class RuntimeBusinessEntry
        {
            public string saveId = "";
            public string displayName = "";
            public string category = "";
            public Sprite artwork;
            public BusinessLevelConfig[] levels = Array.Empty<BusinessLevelConfig>();
            public BusinessSkillConfig skill = new();

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

        private const string SaveKey = "save.business.v2";

        [Header("Refs")]
        [SerializeField] private PlayerProfile profile;
        [SerializeField] private MarketSimulation marketSimulation;

        [Header("Business list")]
        [SerializeField] private BusinessListRowUI rowCardPrefab;
        [SerializeField] private RectTransform rowCardsContent;

        [Header("Filters")]
        [SerializeField] private BusinessFilterButtonUI allFilterButton;
        [SerializeField] private BusinessFilterButtonUI[] categoryFilterButtons = Array.Empty<BusinessFilterButtonUI>();

        [Header("Selected business")]
        [SerializeField] private BusinessDetailCardUI detailCard;
        [SerializeField] private BusinessSkillPanelUI skillPanel;

        [Header("Definitions")]
        [SerializeField] private BusinessDefinition[] businesses = Array.Empty<BusinessDefinition>();

        [Header("Energy")]
        [SerializeField, Min(1)] private int energyMax = 3;
        [SerializeField, Min(1f)] private float energyChunkRegenSeconds = 1200f;

        [Header("Top UI")]
        [SerializeField] private TMP_Text energyCountText;
        [SerializeField] private TMP_Text energyTimerText;
        [SerializeField] private TMP_Text totalIncomePerHourText;
        [SerializeField] private TMP_Text accumulatedAbbrevText;
        [SerializeField] private Button claimButton;

        [Header("Animation")]
        [SerializeField, Min(0.1f)] private float accumulatedDisplayLerpPerSecond = 8f;

        [Header("Texts")]
        [SerializeField] private string rowPrimaryBuyCaption = "Купить";
        [SerializeField] private string rowPrimaryUpgradeCaption = "Улучшить";
        [SerializeField] private string maxLevelCaption = "Макс.";
        [SerializeField] private string rowLevelFormat = "Уровень {0}";
        [SerializeField] private string rowNextLevelFormat = "+{0}";
        [SerializeField] private string totalIncomePerHourFormat = "{0} в час";
        [SerializeField] private string rublesCurrencySuffix = "р";

        [Header("Number format")]
        [SerializeField] private string thousandSep = ".";
        [SerializeField] private string thousandSuffix = " тыс";
        [SerializeField] private string millionSuffix = " млн";
        [SerializeField] private string billionSuffix = " млрд";

        private readonly List<RuntimeBusinessEntry> _entries = new();
        private BusinessListRowUI[] _resolvedRows = Array.Empty<BusinessListRowUI>();
        private int[] _levels = Array.Empty<int>();
        private double _accumulatedRubles;
        private double _displayAccumRubles;
        private int _energy;
        private long _lastSimUtcSeconds;
        private long _nextEnergyAtUtc;
        private BusinessTemporaryBuffKind _tempKind;
        private long _tempBuffEndUtc;
        private float _tempMultiplier = 1f;
        private int _selectedIndex;
        private string _activeCategoryFilter = "";
        private float _saveTimer;
        private bool _dirty;

        public double GetMiningIncomeMultiplierFromBusinessSkills()
        {
            if (_tempKind != BusinessTemporaryBuffKind.MiningIncome)
                return 1d;

            if (!IsTemporaryBuffActive(out _))
            {
                ClearTemporaryBuffIfExpired();
                return 1d;
            }

            return Math.Max(1d, _tempMultiplier);
        }

        public double GetAccumulatedBusinessPassiveMultiplier()
        {
            if (_tempKind != BusinessTemporaryBuffKind.AllBusinessIncome)
                return 1d;

            if (!IsTemporaryBuffActive(out _))
            {
                ClearTemporaryBuffIfExpired();
                return 1d;
            }

            return Math.Max(1d, _tempMultiplier);
        }

        private void Awake()
        {
            ResolveSceneReferences();
            RebuildRuntimeBusinesses();
            EnsureArrays();
            ResolveRows();
            BindRows();
            BindFilterButtons();
            Load();
            ProcessOfflineCatchUp();

            if (claimButton != null)
            {
                claimButton.onClick.RemoveListener(Claim);
                claimButton.onClick.AddListener(Claim);
            }

            if (skillPanel != null)
                skillPanel.SetLaunchListener(TryLaunchSkillForSelection);

            SelectBusiness(_selectedIndex);
        }

        private void OnEnable()
        {
            if (profile != null)
                profile.RublesChanged += OnProfileRublesChanged;

            ProcessOfflineCatchUp();
            RefreshAllUi();
        }

        private void OnDisable()
        {
            if (profile != null)
                profile.RublesChanged -= OnProfileRublesChanged;

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
            var now = UtcNow();
            RegenEnergy(now);
            ClearTemporaryBuffIfExpired();
            SimulateTick(Time.deltaTime);

            var dt = Time.unscaledDeltaTime;
            _displayAccumRubles += (_accumulatedRubles - _displayAccumRubles)
                * (1d - Math.Exp(-accumulatedDisplayLerpPerSecond * dt));

            RefreshDynamicUi();

            if (!_dirty)
                return;

            _saveTimer -= Time.unscaledDeltaTime;
            if (_saveTimer <= 0f)
                SaveNow();
        }

        private void ResolveSceneReferences()
        {
            if (profile == null)
                profile = FindAnyObjectByType<PlayerProfile>();
            if (marketSimulation == null)
                marketSimulation = FindAnyObjectByType<MarketSimulation>();
            if (skillPanel == null)
                skillPanel = FindAnyObjectByType<BusinessSkillPanelUI>();
            if (detailCard == null)
                detailCard = FindAnyObjectByType<BusinessDetailCardUI>();

            if (detailCard == null)
            {
                var detailRoot = GameObject.Find("Big_card");
                if (detailRoot != null)
                    detailCard = detailRoot.GetComponent<BusinessDetailCardUI>()
                        ?? detailRoot.AddComponent<BusinessDetailCardUI>();
            }
        }

        private void RebuildRuntimeBusinesses()
        {
            _entries.Clear();

            if (businesses == null)
                return;

            for (var i = 0; i < businesses.Length; i++)
            {
                var definition = businesses[i];
                if (definition == null)
                    continue;

                _entries.Add(new RuntimeBusinessEntry
                {
                    saveId = definition.SaveId,
                    displayName = definition.DisplayName,
                    category = definition.Category,
                    artwork = definition.Artwork,
                    levels = CopyLevels(definition),
                    skill = CloneSkill(definition.Skill),
                });
            }
        }

        private static BusinessLevelConfig[] CopyLevels(BusinessDefinition definition)
        {
            var levels = new BusinessLevelConfig[definition.MaxLevel];
            for (var i = 0; i < levels.Length; i++)
            {
                var currentLevel = i + 1;
                levels[i] = new BusinessLevelConfig
                {
                    incomeRublesPerHour = definition.GetIncomePerHour(currentLevel),
                    upgradeRublesCost = definition.GetUpgradeCostFromLevel(i),
                };
            }

            return levels;
        }

        private static BusinessSkillConfig CloneSkill(BusinessSkillConfig source)
        {
            if (source == null)
                return new BusinessSkillConfig();

            return new BusinessSkillConfig
            {
                unlockLevel = Mathf.Max(1, source.unlockLevel),
                energyCost = Mathf.Max(0, source.energyCost),
                icon = source.icon,
                title = source.title,
                description = source.description,
                effect = source.effect,
                instantRubles = Math.Max(0, source.instantRubles),
                temporaryIncomeMultiplier = Mathf.Max(1f, source.temporaryIncomeMultiplier),
                temporaryDurationSeconds = Mathf.Max(1f, source.temporaryDurationSeconds),
                marketCurrency = source.marketCurrency,
                marketGrowDurationSeconds = NormalizeRange(source.marketGrowDurationSeconds, 1f),
                marketDumpDurationSeconds = NormalizeRange(source.marketDumpDurationSeconds, 0.25f),
                marketGrowthPercent = NormalizeRange(source.marketGrowthPercent, 0.01f),
                marketReturnTolerancePercent = Mathf.Clamp(source.marketReturnTolerancePercent, 0f, 0.3f),
                lockedSkillFormat = source.lockedSkillFormat,
            };
        }

        private static Vector2 NormalizeRange(Vector2 range, float min)
        {
            var x = Mathf.Max(min, range.x);
            var y = Mathf.Max(min, range.y);
            return new Vector2(Mathf.Min(x, y), Mathf.Max(x, y));
        }

        private void ResolveRows()
        {
            if (rowCardPrefab != null && rowCardsContent != null)
            {
                DestroyChildren(rowCardsContent);
                var rows = new List<BusinessListRowUI>(_entries.Count);

                for (var i = 0; i < _entries.Count; i++)
                {
                    var row = Instantiate(rowCardPrefab, rowCardsContent, false);
                    row.gameObject.name = $"{rowCardPrefab.name}_{i + 1}";
                    row.AssignBusinessIndex(i);
                    row.ResolveButtonReferences();
                    rows.Add(row);
                }

                _resolvedRows = rows.ToArray();
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(rowCardsContent);
                return;
            }

            _resolvedRows = Array.Empty<BusinessListRowUI>();
        }

        private static void DestroyChildren(RectTransform parent)
        {
            for (var i = parent.childCount - 1; i >= 0; i--)
                Destroy(parent.GetChild(i).gameObject);
        }

        private void BindRows()
        {
            for (var i = 0; i < _resolvedRows.Length; i++)
            {
                var row = _resolvedRows[i];
                if (row == null)
                    continue;

                var captured = row.BusinessIndex;
                row.BindSelect(() => SelectBusiness(captured));
                row.BindPrimaryAction(() => BuyOrUpgrade(captured));
            }
        }

        private void BindFilterButtons()
        {
            if (allFilterButton != null)
                allFilterButton.Bind(_ => SelectCategoryFilter(""));

            if (categoryFilterButtons == null)
                return;

            for (var i = 0; i < categoryFilterButtons.Length; i++)
            {
                var filterButton = categoryFilterButtons[i];
                if (filterButton == null)
                    continue;

                filterButton.Bind(SelectCategoryFilter);
            }
        }

        private void SimulateTick(float dt)
        {
            if (dt <= 0f)
                return;

            var perSecond = GetTotalBaseIncomePerHour() / 3600d * GetAccumulatedBusinessPassiveMultiplier();
            if (perSecond <= 0d)
                return;

            _accumulatedRubles += perSecond * dt;
            MarkDirty();
        }

        private void ProcessOfflineCatchUp()
        {
            var now = UtcNow();
            if (_lastSimUtcSeconds <= 0)
            {
                _lastSimUtcSeconds = now;
                return;
            }

            var elapsed = now - _lastSimUtcSeconds;
            if (elapsed > 0)
            {
                var simulatedEnd = _lastSimUtcSeconds + Math.Min(elapsed, 86400);
                AccumulatePassiveIncome(_lastSimUtcSeconds, simulatedEnd);
                _lastSimUtcSeconds = now;
            }

            RegenEnergy(now);
            ClearTemporaryBuffIfExpired();
        }

        private void AccumulatePassiveIncome(long startUtc, long endUtc)
        {
            var seconds = Math.Max(0, endUtc - startUtc);
            if (seconds <= 0)
                return;

            var basePerSecond = GetTotalBaseIncomePerHour() / 3600d;
            if (basePerSecond <= 0d)
                return;

            var boostedSeconds = 0d;
            if (_tempKind == BusinessTemporaryBuffKind.AllBusinessIncome
                && _tempMultiplier > 1f
                && _tempBuffEndUtc > startUtc)
            {
                var boostedEnd = Math.Min(endUtc, _tempBuffEndUtc);
                boostedSeconds = Math.Max(0, boostedEnd - startUtc);
            }

            var normalSeconds = seconds - boostedSeconds;
            _accumulatedRubles += basePerSecond * normalSeconds;
            _accumulatedRubles += basePerSecond * boostedSeconds * Math.Max(1d, _tempMultiplier);
            MarkDirty();
        }

        private void RegenEnergy(long nowUtc)
        {
            if (energyMax <= 0)
                return;

            if (_energy >= energyMax)
            {
                if (_nextEnergyAtUtc != 0)
                {
                    _nextEnergyAtUtc = 0;
                    MarkDirty();
                }

                return;
            }

            var interval = (long)Mathf.Max(1f, energyChunkRegenSeconds);
            if (_nextEnergyAtUtc <= 0)
            {
                _nextEnergyAtUtc = nowUtc + interval;
                MarkDirty();
                return;
            }

            var changed = false;
            while (_energy < energyMax && nowUtc >= _nextEnergyAtUtc)
            {
                _energy++;
                changed = true;
                _nextEnergyAtUtc = _energy >= energyMax ? 0 : _nextEnergyAtUtc + interval;
            }

            if (changed)
                MarkDirty();
        }

        private void ClearTemporaryBuffIfExpired()
        {
            if (_tempKind == BusinessTemporaryBuffKind.None)
                return;

            if (UtcNow() < _tempBuffEndUtc)
                return;

            _tempKind = BusinessTemporaryBuffKind.None;
            _tempBuffEndUtc = 0;
            _tempMultiplier = 1f;
            MarkDirty();
        }

        private bool IsTemporaryBuffActive(out double secondsLeft)
        {
            secondsLeft = 0;
            if (_tempKind == BusinessTemporaryBuffKind.None)
                return false;

            var now = UtcNow();
            if (now >= _tempBuffEndUtc)
                return false;

            secondsLeft = _tempBuffEndUtc - now;
            return true;
        }

        private bool IsGlobalTemporaryBuffBlocking()
        {
            return IsTemporaryBuffActive(out _);
        }

        private void SpendEnergy(int amount)
        {
            if (amount <= 0)
                return;

            var wasFull = _energy >= energyMax;
            _energy = Math.Max(0, _energy - amount);

            if (_energy < energyMax && (wasFull || _nextEnergyAtUtc <= 0))
                _nextEnergyAtUtc = UtcNow() + (long)Mathf.Max(1f, energyChunkRegenSeconds);

            MarkDirty();
        }

        private void BuyOrUpgrade(int index)
        {
            EnsureArrays();
            if (!IsValidIndex(index) || profile == null)
                return;

            SelectBusiness(index);

            var entry = _entries[index];
            var currentLevel = _levels[index];
            if (currentLevel >= entry.MaxLevel)
                return;

            var cost = entry.GetUpgradeCostFromLevel(currentLevel);
            if (cost <= 0 || !profile.TrySpendRubles(cost))
                return;

            _levels[index] = Mathf.Clamp(currentLevel + 1, 0, entry.MaxLevel);
            MarkDirty();
            RefreshAllUi();
        }

        private void BuyOrUpgradeSelected()
        {
            BuyOrUpgrade(_selectedIndex);
        }

        private void TryLaunchSkillForSelection()
        {
            TryLaunchSkill(_selectedIndex);
        }

        private void TryLaunchSkill(int index)
        {
            EnsureArrays();
            if (!IsValidIndex(index))
                return;

            var entry = _entries[index];
            var skill = entry.skill;
            if (_levels[index] < skill.unlockLevel || _energy < skill.energyCost)
                return;

            switch (skill.effect)
            {
                case BusinessSkillEffectKind.InstantRubles:
                    if (profile == null)
                        return;

                    profile.AddRubles(Math.Max(0, skill.instantRubles));
                    SpendEnergy(skill.energyCost);
                    MarkDirty();
                    RefreshAllUi();
                    return;

                case BusinessSkillEffectKind.TemporaryBoostAllBusinessIncome:
                    if (!TryStartTemporaryBoost(
                            BusinessTemporaryBuffKind.AllBusinessIncome,
                            skill.energyCost,
                            skill.temporaryIncomeMultiplier,
                            skill.temporaryDurationSeconds))
                        return;

                    RefreshAllUi();
                    return;

                case BusinessSkillEffectKind.TemporaryBoostMiningIncome:
                    if (!TryStartTemporaryBoost(
                            BusinessTemporaryBuffKind.MiningIncome,
                            skill.energyCost,
                            skill.temporaryIncomeMultiplier,
                            skill.temporaryDurationSeconds))
                        return;

                    RefreshAllUi();
                    return;

                case BusinessSkillEffectKind.MarketManipulate:
                    if (marketSimulation == null)
                        return;

                    SpendEnergy(skill.energyCost);
                    marketSimulation.EnqueueBusinessSkillOverrideNextState(
                        skill.marketCurrency,
                        skill.marketGrowDurationSeconds,
                        skill.marketDumpDurationSeconds,
                        skill.marketGrowthPercent,
                        skill.marketReturnTolerancePercent);

                    MarkDirty();
                    RefreshAllUi();
                    return;
            }
        }

        private bool TryStartTemporaryBoost(
            BusinessTemporaryBuffKind kind,
            int energyCost,
            float multiplier,
            float durationSeconds)
        {
            if (IsGlobalTemporaryBuffBlocking())
                return false;

            SpendEnergy(energyCost);
            _tempKind = kind;
            _tempMultiplier = Mathf.Max(1f, multiplier);
            _tempBuffEndUtc = UtcNow() + (long)Mathf.Max(1f, durationSeconds);
            MarkDirty();
            return true;
        }

        private void Claim()
        {
            if (profile == null)
                return;

            var payout = Math.Floor(_accumulatedRubles);
            if (payout <= 0d)
                return;

            var asLong = (long)Math.Min(payout, long.MaxValue);
            profile.AddRubles(asLong);
            _accumulatedRubles = Math.Max(0d, _accumulatedRubles - payout);
            MarkDirty();
            RefreshAllUi();
        }

        private void SelectBusiness(int index)
        {
            EnsureArrays();
            if (_entries.Count == 0)
            {
                RefreshAllUi();
                return;
            }

            _selectedIndex = Mathf.Clamp(index, 0, _entries.Count - 1);
            MarkDirty();
            RefreshAllUi();
        }

        private void SelectCategoryFilter(string category)
        {
            _activeCategoryFilter = NormalizeCategory(category);
            EnsureSelectedBusinessVisible();
            RefreshAllUi();
        }

        private void EnsureSelectedBusinessVisible()
        {
            if (IsValidIndex(_selectedIndex) && MatchesActiveCategoryFilter(_selectedIndex))
                return;

            for (var i = 0; i < _entries.Count; i++)
            {
                if (!MatchesActiveCategoryFilter(i))
                    continue;

                _selectedIndex = i;
                MarkDirty();
                return;
            }
        }

        private double GetTotalBaseIncomePerHour()
        {
            var sum = 0d;
            for (var i = 0; i < _entries.Count; i++)
            {
                var level = _levels.Length > i ? _levels[i] : 0;
                sum += _entries[i].GetIncomePerHour(level);
            }

            return sum;
        }

        private void RefreshAllUi()
        {
            EnsureArrays();
            EnsureSelectedBusinessVisible();

            for (var i = 0; i < _resolvedRows.Length; i++)
            {
                var row = _resolvedRows[i];
                if (row == null)
                    continue;

                var index = row.BusinessIndex;
                var visible = IsValidIndex(index) && MatchesActiveCategoryFilter(index);
                row.gameObject.SetActive(visible);

                if (visible)
                    RefreshRowUi(row, index);
            }

            RefreshDetailCard();
            RefreshTopStaticUi();
            RefreshSkillPanel();
            RefreshDynamicUi();
            RefreshFilterButtons();

            if (rowCardsContent != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rowCardsContent);
        }

        private void RefreshRowUi(BusinessListRowUI row, int index)
        {
            var entry = _entries[index];
            var level = _levels[index];
            var nextLevel = GetNextLevel(index);
            var displayIncome = level > 0 ? entry.GetIncomePerHour(level) : entry.GetIncomePerHour(nextLevel);
            var cost = entry.GetUpgradeCostFromLevel(level);
            var canUpgrade = level < entry.MaxLevel && cost > 0;
            var isMaxLevel = entry.MaxLevel > 0 && level >= entry.MaxLevel;

            row.RefreshRow(
                entry.artwork,
                entry.displayName,
                entry.category,
                $"{FormatRublesAmount(displayIncome)} в час",
                level > 0
                    ? FormatOne(rowLevelFormat, "Уровень {0}", level)
                    : FormatOne(rowNextLevelFormat, "+{0}", nextLevel),
                canUpgrade ? FormatNumberMoney(cost) : "",
                isMaxLevel ? maxLevelCaption : level <= 0 ? rowPrimaryBuyCaption : rowPrimaryUpgradeCaption,
                canUpgrade || isMaxLevel,
                canUpgrade && CanSpendRubles(cost));

            row.RefreshAppearance(index == _selectedIndex, level > 0);
        }

        private void RefreshDetailCard()
        {
            if (detailCard == null || !IsValidIndex(_selectedIndex))
                return;

            var entry = _entries[_selectedIndex];
            var level = _levels[_selectedIndex];
            var nextLevel = GetNextLevel(_selectedIndex);
            var currentIncome = entry.GetIncomePerHour(level);
            var nextIncome = entry.GetIncomePerHour(nextLevel);
            var cost = entry.GetUpgradeCostFromLevel(level);
            var canUpgrade = level < entry.MaxLevel && cost > 0;
            var isMaxLevel = entry.MaxLevel > 0 && level >= entry.MaxLevel;

            detailCard.Configure(
                entry.artwork,
                entry.displayName,
                FormatNumberMoney(level),
                FormatNumberMoney(nextLevel),
                FormatNumberMoney(currentIncome),
                FormatNumberMoney(nextIncome),
                canUpgrade,
                isMaxLevel ? maxLevelCaption : level <= 0 ? rowPrimaryBuyCaption : rowPrimaryUpgradeCaption,
                canUpgrade ? FormatNumberMoney(cost) : "",
                canUpgrade || isMaxLevel,
                canUpgrade && CanSpendRubles(cost),
                BuyOrUpgradeSelected);
        }

        private void RefreshTopStaticUi()
        {
            if (totalIncomePerHourText != null)
            {
                var total = GetTotalBaseIncomePerHour() * GetAccumulatedBusinessPassiveMultiplier();
                totalIncomePerHourText.text = FormatOne(
                    totalIncomePerHourFormat,
                    "{0} в час",
                    FormatRublesAmount(total));
            }
        }

        private void RefreshDynamicUi()
        {
            if (energyCountText != null)
                energyCountText.text = $"{_energy}/{energyMax}";

            if (energyTimerText != null)
            {
                if (_energy >= energyMax || _nextEnergyAtUtc <= 0)
                    energyTimerText.text = "";
                else
                    energyTimerText.text = FormatCountdown(Math.Max(0, _nextEnergyAtUtc - UtcNow()));
            }

            if (accumulatedAbbrevText != null)
                accumulatedAbbrevText.text = AbbreviateRubles(_displayAccumRubles);

            if (claimButton != null)
                claimButton.interactable = profile != null && Math.Floor(_accumulatedRubles) > 0d;

            RefreshSkillPanel();
        }

        private void RefreshSkillPanel()
        {
            if (skillPanel == null || !IsValidIndex(_selectedIndex))
                return;

            var entry = _entries[_selectedIndex];
            var skill = entry.skill;
            var level = _levels[_selectedIndex];

            if (level < skill.unlockLevel)
            {
                skillPanel.PresentLocked(FormatOne(
                    skill.lockedSkillFormat,
                    "Улучши до {0} уровня чтобы открыть",
                    skill.unlockLevel));
                skillPanel.SetLaunchListener(null);
                return;
            }

            ClearTemporaryBuffIfExpired();

            var energyOk = _energy >= skill.energyCost;
            var isTemporary = skill.effect is BusinessSkillEffectKind.TemporaryBoostAllBusinessIncome
                or BusinessSkillEffectKind.TemporaryBoostMiningIncome;

            var temporaryBlocking = isTemporary && IsGlobalTemporaryBuffBlocking();
            var launchLabel = FormatNumberMoney(skill.energyCost);
            var interactable = energyOk && !temporaryBlocking;

            skillPanel.PresentUnlocked(skill.icon, skill.title, skill.description, interactable, launchLabel);
            skillPanel.SetLaunchListener(TryLaunchSkillForSelection);
        }

        private int GetNextLevel(int index)
        {
            if (!IsValidIndex(index))
                return 0;

            var entry = _entries[index];
            var current = _levels[index];
            return Mathf.Clamp(current + 1, 0, entry.MaxLevel);
        }

        private bool CanSpendRubles(long amount)
        {
            return profile != null && amount >= 0 && profile.Rubles >= amount;
        }

        private bool IsValidIndex(int index)
        {
            return index >= 0 && index < _entries.Count && index < _levels.Length;
        }

        private bool MatchesActiveCategoryFilter(int index)
        {
            if (string.IsNullOrEmpty(_activeCategoryFilter))
                return true;

            if (!IsValidIndex(index))
                return false;

            return string.Equals(
                NormalizeCategory(_entries[index].category),
                _activeCategoryFilter,
                StringComparison.OrdinalIgnoreCase);
        }

        private void RefreshFilterButtons()
        {
            if (allFilterButton != null)
                allFilterButton.SetSelected(string.IsNullOrEmpty(_activeCategoryFilter));

            if (categoryFilterButtons == null)
                return;

            for (var i = 0; i < categoryFilterButtons.Length; i++)
            {
                var filterButton = categoryFilterButtons[i];
                if (filterButton == null)
                    continue;

                filterButton.SetSelected(string.Equals(
                    NormalizeCategory(filterButton.CategoryKey),
                    _activeCategoryFilter,
                    StringComparison.OrdinalIgnoreCase));
            }
        }

        private static string NormalizeCategory(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "" : value.Trim();
        }

        private void EnsureArrays()
        {
            var count = _entries.Count;
            if (_levels == null || _levels.Length != count)
            {
                var next = new int[count];
                if (_levels != null)
                    Array.Copy(_levels, next, Math.Min(_levels.Length, count));

                _levels = next;
            }

            for (var i = 0; i < _levels.Length; i++)
                _levels[i] = Mathf.Clamp(_levels[i], 0, _entries[i].MaxLevel);

            _selectedIndex = count <= 0 ? 0 : Mathf.Clamp(_selectedIndex, 0, count - 1);
            _energy = Mathf.Clamp(_energy, 0, Mathf.Max(1, energyMax));
        }

        private void MarkDirty()
        {
            _dirty = true;
            if (_saveTimer <= 0f)
                _saveTimer = 2f;
        }

        private void SaveNow()
        {
            EnsureArrays();

            var levelSaves = new BusinessLevelSave[_entries.Count];
            for (var i = 0; i < _entries.Count; i++)
            {
                levelSaves[i] = new BusinessLevelSave
                {
                    id = _entries[i].saveId,
                    level = _levels[i],
                };
            }

            var data = new SaveData
            {
                businessLevels = levelSaves,
                accumulatedRubles = _accumulatedRubles,
                energyCurrent = _energy,
                lastSimUtcSeconds = UtcNow(),
                nextEnergyAtUtc = _nextEnergyAtUtc,
                tempKind = (byte)_tempKind,
                tempBuffEndUtc = _tempBuffEndUtc,
                tempMultiplierHeld = _tempMultiplier,
                selectedBusinessIndex = _selectedIndex,
            };

            SaveStorage.SaveJson(SaveKey, data);
            SaveStorage.Flush();
            _dirty = false;
            _saveTimer = 0f;
        }

        private void Load()
        {
            EnsureArrays();
            _energy = energyMax;

            if (!SaveStorage.TryLoadJson(SaveKey, out SaveData data))
            {
                _lastSimUtcSeconds = UtcNow();
                _displayAccumRubles = _accumulatedRubles;
                return;
            }

            _accumulatedRubles = Math.Max(0d, data.accumulatedRubles);
            _energy = Mathf.Clamp(data.energyCurrent, 0, energyMax);
            _lastSimUtcSeconds = data.lastSimUtcSeconds > 0 ? data.lastSimUtcSeconds : UtcNow();
            _nextEnergyAtUtc = data.nextEnergyAtUtc;

            if (data.businessLevels != null && data.businessLevels.Length > 0)
                LoadLevelsById(data.businessLevels);

            _tempKind = Enum.IsDefined(typeof(BusinessTemporaryBuffKind), data.tempKind)
                ? (BusinessTemporaryBuffKind)data.tempKind
                : BusinessTemporaryBuffKind.None;
            _tempBuffEndUtc = Math.Max(0, data.tempBuffEndUtc);
            _tempMultiplier = Mathf.Max(1f, data.tempMultiplierHeld);
            _selectedIndex = _entries.Count <= 0 ? 0 : Mathf.Clamp(data.selectedBusinessIndex, 0, _entries.Count - 1);
            _displayAccumRubles = _accumulatedRubles;

            if (_energy < energyMax && _nextEnergyAtUtc <= 0)
                _nextEnergyAtUtc = UtcNow() + (long)Mathf.Max(1f, energyChunkRegenSeconds);

            EnsureArrays();
            ClearTemporaryBuffIfExpired();
        }

        private void LoadLevelsById(BusinessLevelSave[] saved)
        {
            var map = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < saved.Length; i++)
            {
                var item = saved[i];
                if (item == null || string.IsNullOrEmpty(item.id))
                    continue;

                map[item.id] = item.level;
            }

            for (var i = 0; i < _entries.Count; i++)
            {
                if (map.TryGetValue(_entries[i].saveId, out var level))
                    _levels[i] = level;
            }
        }

        private void OnProfileRublesChanged(long _)
        {
            RefreshAllUi();
        }

        private string FormatCountdown(double secondsTotal)
        {
            secondsTotal = Math.Max(0, secondsTotal);
            var totalMinutes = (int)(secondsTotal / 60d);
            var seconds = (int)(secondsTotal % 60d);
            return $"{totalMinutes:00}:{seconds:00}";
        }

        private string FormatNumberMoney(double value)
        {
            return Math.Max(0, value)
                .ToString("N0", CultureInfo.InvariantCulture)
                .Replace(",", thousandSep);
        }

        private string FormatRublesAmount(double value)
        {
            var separator = rublesCurrencySuffix != null && rublesCurrencySuffix.Length > 1 ? " " : "";
            return $"{FormatNumberMoney(value)}{separator}{rublesCurrencySuffix}";
        }

        private string AbbreviateRubles(double value)
        {
            value = Math.Max(0, value);
            if (value >= 1_000_000_000d)
                return (value / 1_000_000_000d).ToString("0.0", CultureInfo.InvariantCulture) + billionSuffix;
            if (value >= 1_000_000d)
                return (value / 1_000_000d).ToString("0.0", CultureInfo.InvariantCulture) + millionSuffix;
            if (value >= 1000d)
                return (value / 1000d).ToString("0.0", CultureInfo.InvariantCulture) + thousandSuffix;

            return FormatNumberMoney(value);
        }

        private static string SafeFormat(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static string FormatOne(string format, string fallback, object arg)
        {
            var safe = SafeFormat(format, fallback);
            try
            {
                return string.Format(safe, arg);
            }
            catch (FormatException)
            {
                return string.Format(fallback, arg);
            }
        }

        private static long UtcNow() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}
