using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using TraidingIDLE.Analytics;
using TraidingIDLE.Collections;
using TraidingIDLE.Currencies.Simulation;
using TraidingIDLE.Localization;
using TraidingIDLE.Player;
using TraidingIDLE.Saves;
using TraidingIDLE.UI;
using UnityEngine;
using UnityEngine.UI;
using TraidingIDLE.Integrations;
using TraidingIDLE.Monetization;

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
            public string tempCategory = "";
            public int selectedBusinessIndex;
            public bool hasActiveCategoryFilter;
            public string activeCategoryFilter = "";
            public long nextEnergyAdAtUtc;
        }

        private sealed class RuntimeBusinessEntry
        {
            public string saveId = "";
            public string displayName = "";
            public string category = "";
            public Sprite listArtwork;
            public Sprite detailArtwork;
            public BusinessProgressionConfig progression = new();
            public BusinessSkillConfig skill = new();

            public bool HasUnlockRequirement =>
                progression != null
                && !string.IsNullOrWhiteSpace(progression.requiredBusinessSaveId)
                && progression.requiredBusinessLevel > 0;

            public double GetIncomePerHour(int level)
            {
                return progression?.GetIncomePerHour(level) ?? 0d;
            }

            public long GetUpgradeCostFromLevel(int currentLevel)
            {
                return progression?.GetUpgradeCostFromLevel(currentLevel) ?? 0;
            }
        }

        private const string SaveKey = "save.business.v2";
        private const long MaxOfflineIncomeSeconds = 21600;

        [Header("Refs")]
        [SerializeField] private PlayerProfile profile;
        [SerializeField] private MarketSimulation marketSimulation;
        [SerializeField] private CollectionController collectionController;

        [Header("Business list")]
        [SerializeField] private BusinessListRowUI rowCardPrefab;
        [SerializeField] private RectTransform rowCardsContent;

        [Header("Filters")]
        [SerializeField] private BusinessFilterButtonUI allFilterButton;
        [SerializeField] private BusinessFilterButtonUI[] categoryFilterButtons = Array.Empty<BusinessFilterButtonUI>();
        [SerializeField] private string defaultCategoryFilter = "Товарный бизнес";

        [Header("Selected business")]
        [SerializeField] private BusinessDetailCardUI detailCard;
        [SerializeField] private BusinessSkillPanelUI skillPanel;

        [Header("Definitions")]
        [SerializeField] private BusinessDefinition[] businesses = Array.Empty<BusinessDefinition>();

        [Header("Energy")]
        [SerializeField, Min(1)] private int energyMax = 5;
        [SerializeField, Min(1f)] private float energyChunkRegenSeconds = 900f;

        [Header("Energy Refill")]
        [SerializeField] private Button energyRefillAdButton;
        [SerializeField] private TMP_Text energyRefillAdLabelText;
        [SerializeField] private Button energyRefillGemButton;
        [SerializeField] private TMP_Text energyRefillGemCostText;
        [SerializeField, Min(0)] private int energyRefillGemCost = 5;
        [SerializeField, Min(1f)] private float energyRefillAdCooldownSeconds = 3600f;
        [SerializeField] private Graphic energyRefillAdFlashGraphic;
        [SerializeField] private Graphic energyRefillGemFlashGraphic;
        [SerializeField] private string energyRefillAdCaption = "Энергия за рекламу";
        [SerializeField] private string energyRefillGemCostFormat = "{0}";
        [SerializeField] private Color energyRefillFlashColor = new(1f, 0.35f, 0.35f, 1f);

        [Header("x5 income boost")]
        [SerializeField, Min(0)] private int x5BoostGemCost = 10;
        [SerializeField, Min(1f)] private float x5BoostDurationSeconds = 3600f;
        [SerializeField, Min(1f)] private float x5BoostMultiplier = 5f;

        [Header("Top UI")]
        [SerializeField] private TMP_Text energyCountText;
        [SerializeField] private TMP_Text energyTimerText;
        [SerializeField] private TMP_Text totalIncomePerHourText;
        [SerializeField] private TMP_Text accumulatedAbbrevText;
        [SerializeField] private Button claimButton;

        [Header("Display timing")]
        [SerializeField, Min(0.1f)] private float accumulatedDisplayUpdateIntervalSeconds = 0.75f;
        [SerializeField, Min(0f)] private float accumulatedDisplayAnimationSeconds = 0.35f;

        [Header("Texts")]
        [SerializeField] private string rowPrimaryBuyCaption = "Купить";
        [SerializeField] private string rowPrimaryUpgradeCaption = "Улучшить";
        [SerializeField] private string lockedCaption = "Закрыто";
        [SerializeField] private string unlockRequirementFormat = "{0}: уровень {1}";
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
        private long _nextEnergyAdAtUtc;
        private BusinessTemporaryBuffKind _tempKind;
        private long _tempBuffEndUtc;
        private float _tempMultiplier = 1f;
        private string _tempCategory = "";
        private int _selectedIndex;
        private string _activeCategoryFilter = "";
        private double _displayAccumStartRubles;
        private double _displayAccumTargetRubles;
        private float _accumulatedDisplayUpdateTimer;
        private float _accumulatedDisplayAnimationTimer;
        private float _saveTimer;
        private float _lastSimSaveTimer;
        private bool _runtimeStateLoaded;
        private bool _dirty;
        private Coroutine _energyRefillFlashRoutine;

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

        public void RefreshIncomeFromExternalBonuses()
        {
            NotifyMarketEconomyChanged();
            RefreshAllUi();
        }

        private void Awake()
        {
            ResolveSceneReferences();
            RebuildRuntimeBusinesses();
            EnsureArrays();
            ResolveRows();
            BindRows();
            BindFilterButtons();
            LoadRuntimeStateIfNeeded();
            ProcessOfflineCatchUp();

            if (claimButton != null)
            {
                claimButton.onClick.RemoveListener(Claim);
                claimButton.onClick.AddListener(Claim);
            }

            if (energyRefillAdButton != null)
            {
                energyRefillAdButton.onClick.RemoveListener(TryRefillEnergyByAd);
                energyRefillAdButton.onClick.AddListener(TryRefillEnergyByAd);
            }

            if (energyRefillGemButton != null)
            {
                energyRefillGemButton.onClick.RemoveListener(TryRefillEnergyByGems);
                energyRefillGemButton.onClick.AddListener(TryRefillEnergyByGems);
            }

            if (skillPanel != null)
                skillPanel.SetLaunchListener(TryLaunchSkillForSelection);

            SelectBusiness(_selectedIndex);
        }

        private void OnEnable()
        {
            LocalizationManager.LanguageChanged += RefreshAllUi;
            SaveStorage.ExternalDataLoaded += ReloadFromExternalStorage;

            if (profile != null)
                profile.RublesChanged += OnProfileRublesChanged;

            ProcessOfflineCatchUp();
            RefreshAllUi();
        }

        private void OnDisable()
        {
            LocalizationManager.LanguageChanged -= RefreshAllUi;
            SaveStorage.ExternalDataLoaded -= ReloadFromExternalStorage;

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

            _lastSimSaveTimer -= Time.unscaledDeltaTime;
            if (_lastSimSaveTimer <= 0f)
            {
                _lastSimSaveTimer = 5f;
                _lastSimUtcSeconds = now;
                MarkDirty();
            }

            RefreshDynamicUi(UpdateAccumulatedDisplay(Time.unscaledDeltaTime));

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
            if (collectionController == null)
                collectionController = FindAnyObjectByType<CollectionController>(FindObjectsInactive.Include);
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
                    listArtwork = definition.ListArtwork,
                    detailArtwork = definition.DetailArtwork,
                    progression = definition.Progression.Clone(),
                    skill = CloneSkill(definition.Skill),
                });
            }
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
                instantRublesGrowthPer10Levels = Mathf.Max(0f, source.instantRublesGrowthPer10Levels),
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
                UiTransformUtility.DestroyChildren(rowCardsContent);
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
                UiTransformUtility.RebuildLayout(rowCardsContent);
                return;
            }

            _resolvedRows = Array.Empty<BusinessListRowUI>();
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

            var perSecond = GetTotalEffectiveIncomePerHour() / 3600d;
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
                var simulatedEnd = _lastSimUtcSeconds + Math.Min(elapsed, MaxOfflineIncomeSeconds);
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
            var businessIncomeBoostActive = IsBusinessIncomeTemporaryBoostKind(_tempKind)
                && _tempMultiplier > 1f
                && _tempBuffEndUtc > startUtc;
            if (businessIncomeBoostActive)
            {
                var boostedEnd = Math.Min(endUtc, _tempBuffEndUtc);
                boostedSeconds = Math.Max(0, boostedEnd - startUtc);
            }

            var normalSeconds = seconds - boostedSeconds;
            _accumulatedRubles += basePerSecond * normalSeconds;
            _accumulatedRubles += GetTotalEffectiveIncomePerHour() / 3600d * boostedSeconds;
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

            var changedBusinessIncome = IsBusinessIncomeTemporaryBoostKind(_tempKind);
            _tempKind = BusinessTemporaryBuffKind.None;
            _tempBuffEndUtc = 0;
            _tempMultiplier = 1f;
            _tempCategory = "";
            MarkDirty();

            if (changedBusinessIncome)
                NotifyMarketEconomyChanged();
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
            if (!IsBusinessUnlocked(index) || currentLevel >= int.MaxValue)
                return;

            var cost = entry.GetUpgradeCostFromLevel(currentLevel);
            if (cost <= 0 || !profile.TrySpendRubles(cost))
                return;

            _levels[index] = currentLevel + 1;
            if (currentLevel <= 0)
                AnalyticsTracker.ReportBusinessBuy(
                    entry.saveId,
                    entry.category,
                    cost,
                    GetBusinessIncomePerHourWithBonuses(index, _levels[index]));

            MarkDirty();
            NotifyMarketEconomyChanged();
            RefreshAllUi();
        }

        private void BuyOrUpgradeSelected()
        {
            BuyOrUpgrade(_selectedIndex);
        }

        private void TryBuyX5AllBusinessIncome()
        {
            if (profile == null)
                return;

            if (IsGlobalTemporaryBuffBlocking())
                return;

            if (!profile.TrySpendGems(x5BoostGemCost))
                return;

            _tempKind = BusinessTemporaryBuffKind.AllBusinessIncome;
            _tempMultiplier = Mathf.Max(1f, x5BoostMultiplier);
            _tempCategory = "";
            _tempBuffEndUtc = UtcNow() + (long)Mathf.Max(1f, x5BoostDurationSeconds);

            MarkDirty();
            NotifyMarketEconomyChanged();
            RefreshAllUi();
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
            if (!IsBusinessUnlocked(index) || _levels[index] < skill.unlockLevel || _energy < skill.energyCost)
                return;

            switch (skill.effect)
            {
                case BusinessSkillEffectKind.InstantRubles:
                    if (profile == null)
                        return;

                    profile.AddRubles(CalculateInstantSkillRubles(skill, _levels[index]));
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

                case BusinessSkillEffectKind.TemporaryBoostCategoryBusinessIncome:
                    if (!TryStartTemporaryBoost(
                            BusinessTemporaryBuffKind.CategoryBusinessIncome,
                            skill.energyCost,
                            skill.temporaryIncomeMultiplier,
                            skill.temporaryDurationSeconds,
                            entry.category))
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
            float durationSeconds,
            string category = "")
        {
            if (IsGlobalTemporaryBuffBlocking())
                return false;

            SpendEnergy(energyCost);
            _tempKind = kind;
            _tempMultiplier = Mathf.Max(1f, multiplier);
            _tempCategory = kind == BusinessTemporaryBuffKind.CategoryBusinessIncome
                ? NormalizeCategory(category)
                : "";
            _tempBuffEndUtc = UtcNow() + (long)Mathf.Max(1f, durationSeconds);
            MarkDirty();

            if (IsBusinessIncomeTemporaryBoostKind(kind))
                NotifyMarketEconomyChanged();

            return true;
        }

        private void NotifyMarketEconomyChanged()
        {
            if (marketSimulation == null)
                marketSimulation = FindAnyObjectByType<MarketSimulation>(FindObjectsInactive.Include);

            if (marketSimulation != null)
                marketSimulation.RefreshEconomyScaleNow();
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
            MarkDirty();
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
                if (level <= 0 || !IsBusinessUnlocked(i))
                    continue;

                sum += _entries[i].GetIncomePerHour(level) * GetCollectionIncomeMultiplier(_entries[i].category);
            }

            return sum;
        }

        public double GetTotalEffectiveIncomePerHour()
        {
            EnsureRuntimeStateForExternalQuery();

            var sum = 0d;
            for (var i = 0; i < _entries.Count; i++)
            {
                var level = _levels.Length > i ? _levels[i] : 0;
                if (level <= 0 || !IsBusinessUnlocked(i))
                    continue;

                sum += GetBusinessIncomePerHourWithBonuses(i, level);
            }

            return sum;
        }

        private void EnsureRuntimeStateForExternalQuery()
        {
            if (_runtimeStateLoaded && _entries.Count > 0 && _levels != null && _levels.Length == _entries.Count)
                return;

            if (_entries.Count == 0 && businesses != null && businesses.Length > 0)
                RebuildRuntimeBusinesses();

            EnsureArrays();
            LoadRuntimeStateIfNeeded();
        }

        private double GetBusinessIncomePerHourWithBonuses(int index, int level)
        {
            if (!IsValidIndex(index) || level <= 0)
                return 0d;

            var entry = _entries[index];
            return entry.GetIncomePerHour(level)
                * GetCollectionIncomeMultiplier(entry.category)
                * GetBusinessTemporaryMultiplierForCategory(entry.category);
        }

        private double GetBusinessTemporaryMultiplierForCategory(string category)
        {
            if (_tempKind == BusinessTemporaryBuffKind.AllBusinessIncome)
                return Math.Max(1d, _tempMultiplier);

            if (_tempKind == BusinessTemporaryBuffKind.CategoryBusinessIncome
                && string.Equals(NormalizeCategory(category), _tempCategory, StringComparison.OrdinalIgnoreCase))
                return Math.Max(1d, _tempMultiplier);

            return 1d;
        }

        private static bool IsBusinessIncomeTemporaryBoostKind(BusinessTemporaryBuffKind kind)
        {
            return kind is BusinessTemporaryBuffKind.AllBusinessIncome
                or BusinessTemporaryBuffKind.CategoryBusinessIncome;
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

            ApplyVisibleRowOrder();
            RefreshDetailCard();
            RefreshTopStaticUi();
            RefreshDynamicUi(true);
            RefreshFilterButtons();

            UiTransformUtility.RebuildLayout(rowCardsContent);
        }

        private void ApplyVisibleRowOrder()
        {
            if (rowCardsContent == null || _resolvedRows == null || _resolvedRows.Length <= 1)
                return;

            var ordered = new List<BusinessListRowUI>(_resolvedRows.Length);
            for (var i = 0; i < _resolvedRows.Length; i++)
            {
                var row = _resolvedRows[i];
                if (row != null && row.gameObject.activeSelf)
                    ordered.Add(row);
            }

            if (ordered.Count <= 1)
                return;

            if (string.IsNullOrEmpty(_activeCategoryFilter))
            {
                ordered.Sort((a, b) =>
                {
                    var incomeCompare = GetBusinessSortIncome(b.BusinessIndex)
                        .CompareTo(GetBusinessSortIncome(a.BusinessIndex));
                    return incomeCompare != 0
                        ? incomeCompare
                        : a.BusinessIndex.CompareTo(b.BusinessIndex);
                });
            }
            else
            {
                ordered.Sort((a, b) => a.BusinessIndex.CompareTo(b.BusinessIndex));
            }

            for (var i = 0; i < ordered.Count; i++)
                ordered[i].transform.SetSiblingIndex(i);
        }

        private double GetBusinessSortIncome(int index)
        {
            if (!IsValidIndex(index) || !IsBusinessUnlocked(index))
                return -1d;

            var level = _levels[index];
            return level > 0
                ? GetBusinessIncomePerHourWithBonuses(index, level)
                : _entries[index].GetIncomePerHour(GetNextLevel(index)) * GetCollectionIncomeMultiplier(_entries[index].category);
        }

        private void RefreshRowUi(BusinessListRowUI row, int index)
        {
            var entry = _entries[index];
            var level = _levels[index];
            var nextLevel = GetNextLevel(index);
            var unlocked = IsBusinessUnlocked(index);
            var displayIncome = unlocked && level > 0
                ? GetBusinessIncomePerHourWithBonuses(index, level)
                : entry.GetIncomePerHour(nextLevel) * GetCollectionIncomeMultiplier(entry.category);
            var cost = entry.GetUpgradeCostFromLevel(level);
            var canUpgrade = unlocked && cost > 0;

            row.RefreshRow(
                entry.listArtwork,
                KnownLocalization.TranslateBusinessName(entry.saveId, entry.displayName),
                KnownLocalization.TranslateCategory(entry.category),
                unlocked
                    ? GameTextFormatter.Format(
                        LocalizationManager.Tr("business.income_per_hour_format", totalIncomePerHourFormat),
                        "{0} в час",
                        FormatRublesAmount(displayIncome))
                    : "-",
                !unlocked
                    ? GameTextFormatter.Format(LocalizationManager.Tr("common.level_format", rowLevelFormat), "Уровень {0}", 0)
                    : level > 0
                    ? GameTextFormatter.Format(LocalizationManager.Tr("common.level_format", rowLevelFormat), "Уровень {0}", level)
                    : GameTextFormatter.Format(LocalizationManager.Tr("common.level_format", rowLevelFormat), "Уровень {0}", nextLevel),
                !unlocked ? LocalizationManager.Tr("common.locked", lockedCaption) : canUpgrade ? FormatReadableMoney(cost) : "",
                !unlocked || level <= 0
                    ? LocalizationManager.Tr("common.buy", rowPrimaryBuyCaption)
                    : LocalizationManager.Tr("common.upgrade", rowPrimaryUpgradeCaption),
                canUpgrade || !unlocked,
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
            var unlocked = IsBusinessUnlocked(_selectedIndex);
            var currentIncome = unlocked ? GetBusinessIncomePerHourWithBonuses(_selectedIndex, level) : 0d;
            var nextIncome = entry.GetIncomePerHour(nextLevel)
                * GetCollectionIncomeMultiplier(entry.category)
                * GetBusinessTemporaryMultiplierForCategory(entry.category);
            var cost = entry.GetUpgradeCostFromLevel(level);
            var canUpgrade = unlocked && cost > 0;

            detailCard.Configure(
                entry.detailArtwork,
                KnownLocalization.TranslateBusinessName(entry.saveId, entry.displayName),
                FormatNumberMoney(level),
                FormatNumberMoney(nextLevel),
                FormatDetailIncomeMoney(currentIncome),
                FormatDetailIncomeMoney(nextIncome),
                canUpgrade,
                !unlocked
                    ? LocalizationManager.Tr("common.locked", lockedCaption)
                    : level <= 0
                        ? LocalizationManager.Tr("common.buy", rowPrimaryBuyCaption)
                        : LocalizationManager.Tr("common.upgrade", rowPrimaryUpgradeCaption),
                !unlocked ? GetUnlockRequirementText(_selectedIndex) : canUpgrade ? FormatReadableMoney(cost) : "",
                canUpgrade || !unlocked,
                canUpgrade && CanSpendRubles(cost),
                BuyOrUpgradeSelected);

            double boostSecondsLeft = 0d;

            var boostActive =
                _tempKind == BusinessTemporaryBuffKind.AllBusinessIncome
                && IsTemporaryBuffActive(out boostSecondsLeft);

            detailCard.ConfigureX5Boost(
                boostActive,
                boostActive ? FormatBoostCountdown(boostSecondsLeft) : "",
                !boostActive && profile != null && profile.Gems >= x5BoostGemCost,
                TryBuyX5AllBusinessIncome);
        }

        private void RefreshTopStaticUi()
        {
            if (totalIncomePerHourText != null)
            {
                var total = GetTotalEffectiveIncomePerHour();
                totalIncomePerHourText.text = GameTextFormatter.Format(
                    LocalizationManager.Tr("business.income_per_hour_format", totalIncomePerHourFormat),
                    "{0} в час",
                    FormatRublesAmount(total));
            }
        }

        private bool UpdateAccumulatedDisplay(float deltaTime)
        {
            var safeDelta = Mathf.Max(0f, deltaTime);
            var changed = false;

            _accumulatedDisplayUpdateTimer -= safeDelta;
            if (_accumulatedDisplayUpdateTimer <= 0f)
            {
                _accumulatedDisplayUpdateTimer = Mathf.Max(0.1f, accumulatedDisplayUpdateIntervalSeconds);
                changed |= StartAccumulatedDisplayAnimation(_accumulatedRubles);
            }

            if (_accumulatedDisplayAnimationTimer <= 0f)
                return changed;

            _accumulatedDisplayAnimationTimer -= safeDelta;
            var duration = Mathf.Max(0.01f, accumulatedDisplayAnimationSeconds);
            var progress = 1f - Mathf.Clamp01(_accumulatedDisplayAnimationTimer / duration);
            var eased = progress * progress * (3f - 2f * progress);
            _displayAccumRubles = _displayAccumStartRubles
                + (_displayAccumTargetRubles - _displayAccumStartRubles) * eased;

            if (_accumulatedDisplayAnimationTimer <= 0f)
                _displayAccumRubles = _displayAccumTargetRubles;

            return true;
        }

        private bool StartAccumulatedDisplayAnimation(double targetRubles)
        {
            if (Math.Abs(targetRubles - _displayAccumTargetRubles) < 0.5d
                && _accumulatedDisplayAnimationTimer <= 0f)
                return false;

            _displayAccumStartRubles = _displayAccumRubles;
            _displayAccumTargetRubles = Math.Max(0d, targetRubles);
            _accumulatedDisplayAnimationTimer = Mathf.Max(0f, accumulatedDisplayAnimationSeconds);

            if (_accumulatedDisplayAnimationTimer > 0f)
                return true;

            _displayAccumRubles = _displayAccumTargetRubles;
            return true;
        }

        private void RefreshDynamicUi(bool updateAccumulatedText = false)
        {
            if (energyCountText != null)
                energyCountText.text = $"{_energy}/{energyMax}";

            if (energyTimerText != null)
            {
                if (_energy >= energyMax || _nextEnergyAtUtc <= 0)
                    energyTimerText.text = "";
                else
                    energyTimerText.text = GameTextFormatter.CountdownMinutes(Math.Max(0, _nextEnergyAtUtc - UtcNow()));
            }

            if (updateAccumulatedText)
            {
                _accumulatedDisplayUpdateTimer = Mathf.Max(0.1f, accumulatedDisplayUpdateIntervalSeconds);
                _accumulatedDisplayAnimationTimer = 0f;
                _displayAccumRubles = _accumulatedRubles;
                _displayAccumStartRubles = _displayAccumRubles;
                _displayAccumTargetRubles = _displayAccumRubles;
            }

            if (updateAccumulatedText && accumulatedAbbrevText != null)
                accumulatedAbbrevText.text = AbbreviateRubles(_displayAccumRubles);

            if (claimButton != null)
                claimButton.interactable = profile != null && Math.Floor(_accumulatedRubles) > 0d;

            RefreshSkillPanel();
            RefreshX5BoostDynamicUi();
            RefreshEnergyRefillButtons();
        }

        private void RefreshX5BoostDynamicUi()
        {
            if (detailCard == null)
                return;

            double boostSecondsLeft = 0d;

            var boostActive =
                _tempKind == BusinessTemporaryBuffKind.AllBusinessIncome
                && IsTemporaryBuffActive(out boostSecondsLeft);

            detailCard.ConfigureX5Boost(
                boostActive,
                boostActive ? FormatBoostCountdown(boostSecondsLeft) : "",
                !boostActive && profile != null && profile.Gems >= x5BoostGemCost,
                TryBuyX5AllBusinessIncome);
        }

        private void RefreshEnergyRefillButtons()
        {
            var adReady = UtcNow() >= _nextEnergyAdAtUtc;

            if (energyRefillAdButton != null)
            {
                if (energyRefillAdButton.gameObject.activeSelf != adReady)
                    energyRefillAdButton.gameObject.SetActive(adReady);

                energyRefillAdButton.interactable = true;

                if (energyRefillAdLabelText != null)
                    energyRefillAdLabelText.text = energyRefillAdCaption;
            }

            if (energyRefillGemButton != null)
            {
                var showGem = !adReady;
                if (energyRefillGemButton.gameObject.activeSelf != showGem)
                    energyRefillGemButton.gameObject.SetActive(showGem);

                energyRefillGemButton.interactable =
                    profile != null && profile.Gems >= energyRefillGemCost;
            }

            if (energyRefillGemCostText != null)
            {
                energyRefillGemCostText.text = GameTextFormatter.Format(
                    energyRefillGemCostFormat,
                    "{0}",
                    energyRefillGemCost);
            }
        }

        private void TryRefillEnergyByAd()
        {
            if (_energy >= energyMax)
            {
                FlashEnergyRefillButton(energyRefillAdFlashGraphic);
                return;
            }

            if (UtcNow() < _nextEnergyAdAtUtc)
            {
                RefreshAllUi();
                return;
            }

            YandexRewardedAds.Show(YandexRewardedAds.EnergyRefillId, OnEnergyAdRewarded);
        }

        private void OnEnergyAdRewarded()
        {
            _nextEnergyAdAtUtc = UtcNow() + (long)Mathf.Max(1f, energyRefillAdCooldownSeconds);

            if (_energy < energyMax)
            {
                _energy = energyMax;
                _nextEnergyAtUtc = 0;
            }

            MarkDirty();
            RefreshAllUi();
        }

        private void TryRefillEnergyByGems()
        {
            if (_energy >= energyMax)
            {
                FlashEnergyRefillButton(energyRefillGemFlashGraphic);
                return;
            }

            if (profile == null || !profile.TrySpendGems(energyRefillGemCost))
                return;

            _energy = energyMax;
            _nextEnergyAtUtc = 0;

            MarkDirty();
            RefreshAllUi();
        }

        private void FlashEnergyRefillButton(Graphic graphic)
        {
            if (graphic == null)
                return;

            if (_energyRefillFlashRoutine != null)
                StopCoroutine(_energyRefillFlashRoutine);

            _energyRefillFlashRoutine = StartCoroutine(FlashEnergyRefillButtonRoutine(graphic));
        }

        private IEnumerator FlashEnergyRefillButtonRoutine(Graphic graphic)
        {
            var originalColor = graphic.color;
            graphic.color = energyRefillFlashColor;

            yield return new WaitForSecondsRealtime(0.15f);

            if (graphic != null)
                graphic.color = originalColor;

            _energyRefillFlashRoutine = null;
        }

        private void RefreshSkillPanel()
        {
            if (skillPanel == null || !IsValidIndex(_selectedIndex))
                return;

            var entry = _entries[_selectedIndex];
            var skill = entry.skill;
            var level = _levels[_selectedIndex];

            if (!IsBusinessUnlocked(_selectedIndex))
            {
                skillPanel.PresentLocked(GameTextFormatter.Format(
                    LocalizationManager.Tr("business.locked_skill_format", "Улучши до {0} уровня чтобы открыть"),
                    skill.lockedSkillFormat,
                    skill.unlockLevel));
                skillPanel.SetLaunchListener(null);
                return;
            }

            if (level < skill.unlockLevel)
            {
                skillPanel.PresentLocked(GameTextFormatter.Format(
                    LocalizationManager.Tr("business.locked_skill_format", "Улучши до {0} уровня чтобы открыть"),
                    skill.lockedSkillFormat,
                    skill.unlockLevel));
                skillPanel.SetLaunchListener(null);
                return;
            }

            ClearTemporaryBuffIfExpired();

            var energyOk = _energy >= skill.energyCost;
            var isTemporary = skill.effect is BusinessSkillEffectKind.TemporaryBoostAllBusinessIncome
                or BusinessSkillEffectKind.TemporaryBoostMiningIncome
                or BusinessSkillEffectKind.TemporaryBoostCategoryBusinessIncome;

            var temporaryBlocking = isTemporary && IsGlobalTemporaryBuffBlocking();
            var launchLabel = FormatNumberMoney(skill.energyCost);
            var interactable = energyOk && !temporaryBlocking;

            skillPanel.PresentUnlocked(
                skill.icon,
                KnownLocalization.TranslateBusinessSkillTitle(skill.title),
                FormatSkillDescription(skill, level),
                interactable,
                launchLabel);
            skillPanel.SetLaunchListener(TryLaunchSkillForSelection);
        }

        private string FormatSkillDescription(BusinessSkillConfig skill, int level)
        {
            if (skill.effect == BusinessSkillEffectKind.InstantRubles)
            {
                return GameTextFormatter.Format(
                    LocalizationManager.Tr("business.skill_instant_rubles_format", "Мгновенно приносит {0}."),
                    skill.description,
                    FormatRublesAmount(CalculateInstantSkillRubles(skill, level)));
            }

            if (skill.effect == BusinessSkillEffectKind.TemporaryBoostCategoryBusinessIncome)
            {
                return GameTextFormatter.Format(
                    LocalizationManager.Tr("business.skill_category_income_format", "Доход бизнесов этого типа +{0}"),
                    skill.description,
                    FormatIncomeBonusPercent(skill.temporaryIncomeMultiplier));
            }

            return KnownLocalization.TranslateBusinessSkillTitle(skill.description);
        }

        private static string FormatIncomeBonusPercent(float multiplier)
        {
            var bonusPercent = Math.Max(0d, multiplier - 1d) * 100d;
            return bonusPercent.ToString("0.#", CultureInfo.InvariantCulture) + "%";
        }

        private int GetNextLevel(int index)
        {
            if (!IsValidIndex(index))
                return 0;

            var entry = _entries[index];
            var current = _levels[index];
            return current >= int.MaxValue ? int.MaxValue : current + 1;
        }

        private long CalculateInstantSkillRubles(BusinessSkillConfig skill, int businessLevel)
        {
            if (skill == null || skill.instantRubles <= 0)
                return 0;

            var steps = Math.Max(0, businessLevel / 10);
            var multiplier = Math.Pow(1d + Math.Max(0d, skill.instantRublesGrowthPer10Levels), steps);
            return ToSaturatedLong(RoundToReadableMoney(skill.instantRubles * multiplier));
        }

        private bool CanSpendRubles(long amount)
        {
            return profile != null && amount >= 0 && profile.Rubles >= amount;
        }

        private bool IsBusinessUnlocked(int index)
        {
            if (!IsValidIndex(index))
                return false;

            var entry = _entries[index];
            if (!entry.HasUnlockRequirement)
                return true;

            var requiredIndex = FindBusinessIndexBySaveId(entry.progression.requiredBusinessSaveId);
            return requiredIndex >= 0
                && requiredIndex < _levels.Length
                && _levels[requiredIndex] >= entry.progression.requiredBusinessLevel;
        }

        private int FindBusinessIndexBySaveId(string saveId)
        {
            if (string.IsNullOrWhiteSpace(saveId))
                return -1;

            for (var i = 0; i < _entries.Count; i++)
            {
                if (string.Equals(_entries[i].saveId, saveId, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        private string GetUnlockRequirementText(int index)
        {
            if (!IsValidIndex(index))
                return "";

            var entry = _entries[index];
            if (!entry.HasUnlockRequirement)
                return "";

            var requiredIndex = FindBusinessIndexBySaveId(entry.progression.requiredBusinessSaveId);
            var requiredName = requiredIndex >= 0
                ? KnownLocalization.TranslateBusinessName(_entries[requiredIndex].saveId, _entries[requiredIndex].displayName)
                : entry.progression.requiredBusinessSaveId;

            return GameTextFormatter.Format(
                LocalizationManager.Tr("business.unlock_requirement_format", unlockRequirementFormat),
                "{0}: уровень {1}",
                requiredName,
                entry.progression.requiredBusinessLevel);
        }

        private double GetCollectionIncomeMultiplier(string businessCategory)
        {
            if (collectionController == null)
                collectionController = FindAnyObjectByType<CollectionController>(FindObjectsInactive.Include);

            return collectionController != null
                ? collectionController.GetBusinessIncomeMultiplier(businessCategory)
                : 1d;
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
                _levels[i] = Math.Max(0, _levels[i]);

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
                tempCategory = _tempCategory,
                selectedBusinessIndex = _selectedIndex,
                hasActiveCategoryFilter = true,
                activeCategoryFilter = _activeCategoryFilter,
                nextEnergyAdAtUtc = _nextEnergyAdAtUtc,
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
                _activeCategoryFilter = NormalizeCategory(defaultCategoryFilter);
                EnsureSelectedBusinessVisible();
                _lastSimUtcSeconds = UtcNow();
                _displayAccumRubles = _accumulatedRubles;
                _nextEnergyAdAtUtc = 0;
                return;
            }

            _accumulatedRubles = Math.Max(0d, data.accumulatedRubles);
            _energy = Mathf.Clamp(data.energyCurrent, 0, energyMax);
            _lastSimUtcSeconds = data.lastSimUtcSeconds > 0 ? data.lastSimUtcSeconds : UtcNow();
            _nextEnergyAtUtc = data.nextEnergyAtUtc;
            _nextEnergyAdAtUtc = Math.Max(0, data.nextEnergyAdAtUtc);

            if (data.businessLevels != null && data.businessLevels.Length > 0)
                LoadLevelsById(data.businessLevels);

            _tempKind = Enum.IsDefined(typeof(BusinessTemporaryBuffKind), data.tempKind)
                ? (BusinessTemporaryBuffKind)data.tempKind
                : BusinessTemporaryBuffKind.None;
            _tempBuffEndUtc = Math.Max(0, data.tempBuffEndUtc);
            _tempMultiplier = Mathf.Max(1f, data.tempMultiplierHeld);
            _tempCategory = NormalizeCategory(data.tempCategory);
            _selectedIndex = _entries.Count <= 0 ? 0 : Mathf.Clamp(data.selectedBusinessIndex, 0, _entries.Count - 1);
            _activeCategoryFilter = data.hasActiveCategoryFilter
                ? NormalizeCategory(data.activeCategoryFilter)
                : NormalizeCategory(defaultCategoryFilter);
            EnsureSelectedBusinessVisible();
            _displayAccumRubles = _accumulatedRubles;

            if (_energy < energyMax && _nextEnergyAtUtc <= 0)
                _nextEnergyAtUtc = UtcNow() + (long)Mathf.Max(1f, energyChunkRegenSeconds);

            EnsureArrays();
        }

        private void LoadRuntimeStateIfNeeded()
        {
            if (_runtimeStateLoaded)
                return;

            Load();
            _runtimeStateLoaded = true;
        }

        private void ReloadFromExternalStorage()
        {
            Load();
            _runtimeStateLoaded = true;
            _dirty = false;
            _saveTimer = 0f;
            ProcessOfflineCatchUp();
            NotifyMarketEconomyChanged();
            RefreshAllUi();
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

        private string FormatNumberMoney(double value)
        {
            return GameTextFormatter.WholeNumber(value, thousandSep);
        }

        private string FormatReadableMoney(double value)
        {
            return GameTextFormatter.WholeNumber(RoundToReadableMoney(value), thousandSep);
        }

        private string FormatRublesAmount(double value)
        {
            var suffix = LocalizationManager.Tr("currency.rubles_short", rublesCurrencySuffix);
            var separator = suffix != null && suffix.Length > 1 ? " " : "";
            return $"{FormatReadableMoney(value)}{separator}{suffix}";
        }

        private string FormatDetailIncomeMoney(double value)
        {
            value = Math.Max(0d, RoundToReadableMoney(value));
            if (value < 1_000_000d)
                return FormatNumberMoney(value);

            return AbbreviateRubles(value);
        }

        private string AbbreviateRubles(double value)
        {
            value = Math.Max(0, value);
            if (value >= 1_000_000_000d)
                return (value / 1_000_000_000d).ToString("0.0", CultureInfo.InvariantCulture)
                    + LocalizationManager.Tr("number.billion_suffix", billionSuffix);
            if (value >= 1_000_000d)
                return (value / 1_000_000d).ToString("0.0", CultureInfo.InvariantCulture)
                    + LocalizationManager.Tr("number.million_suffix", millionSuffix);
            if (value >= 1000d)
                return (value / 1000d).ToString("0.0", CultureInfo.InvariantCulture)
                    + LocalizationManager.Tr("number.thousand_suffix", thousandSuffix);

            return FormatNumberMoney(value);
        }

        private static string FormatBoostCountdown(double seconds)
        {
            seconds = Math.Max(0, seconds);
            var ts = TimeSpan.FromSeconds(seconds);

            return ts.TotalHours >= 1d
                ? $"{ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}"
                : $"{ts.Minutes:00}:{ts.Seconds:00}";
        }

        private static long UtcNow() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

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
}
