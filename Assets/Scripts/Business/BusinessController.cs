using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using TraidingIDLE.Currencies;
using TraidingIDLE.Currencies.Simulation;
using TraidingIDLE.Player;
using TraidingIDLE.Saves;
using UnityEngine;
using UnityEngine.UI;

namespace TraidingIDLE.Business
{
    /// <summary>
    /// Пассивный доход бизнесов, энергия, навыки (отдельный запуск), сбор на рубли и один глобальный временный буст.
    /// </summary>
    public sealed class BusinessController : MonoBehaviour
    {
        [Serializable]
        private sealed class BusinessTierConfig
        {
            [Min(0f)] public float incomeRublesPerHour;
            [Min(0)] public long upgradeRublesCost;
        }

        [Serializable]
        private sealed class BusinessCatalogEntry
        {
            public string displayName = "Бизнес";
            public string category = "Разное";
            [Tooltip("Доход на уровне N = tiers[N-1]; покупка первого уровня = upgradeRublesCost tiers[0].")]
            public BusinessTierConfig[] tiers = { new() };

            [Min(1)] public int skillUnlockLevel = 5;
            [Min(0)] public int skillEnergyCost = 1;
            public BusinessSkillEffectKind skillEffect = BusinessSkillEffectKind.InstantRubles;

            [Tooltip("Только для навыка «рынок» (Market Manipulate): какую монету из симуляции торгов затронуть при следующей смене состояния рынка.")]
            public CurrencyId marketSkillCurrency = CurrencyId.SHT;

            [Min(0)] public long skillInstantRubles;
            [Min(1f)] public float temporaryIncomeMultiplier = 5f;
            [Min(1f)] public float temporaryDurationSeconds = 600f;

            public Sprite? skillIcon;
            public string skillTitle = "Умение";
            [TextArea] public string skillDescription = "";
            public string lockedSkillFormat = "Улучши до {0} уровня, чтобы открыть умение";
            public string launchButtonIdleFormat = "Запустить ⚡{0}";
            public string temporarySkillActiveFormat = "навык активен {0}";
        }

        private sealed class SaveData
        {
            public int[] levels = Array.Empty<int>();
            public double accumulatedRubles;
            public int energyCurrent;
            public long lastSimUtcSeconds;
            public long nextEnergyAtUtc;
            public byte tempKind;
            public long tempBuffEndUtc;
            public float tempMultiplierHeld;
            public int selectedBusinessIndex;
        }

        private const string SaveKey = "save.business.v1";

        [Header("Refs")]
        [SerializeField] private PlayerProfile profile = null!;
        [SerializeField] private MarketSimulation? marketSimulation;

        [Header("Спавн из префаба (если заполнены — главный режим для Scroll View)")]
        [Tooltip("Префаб корня карточки с BusinessListRowUI (кнопки и тексты настроены один раз на префабе).")]
        [SerializeField] private BusinessListRowUI? rowCardPrefab;
        [Tooltip("Объект Content внутри Scroll View — сюда клонируются карточки, столько же сколько элементов Catalog.")]
        [SerializeField] private RectTransform? rowCardsContent;

        [Tooltip("Главный простой вариант: контейнер, у которого прямые дочерние объекты — карточки бизнеса сверху вниз. На каждом дочернем объекте висит BusinessListRowUI.\nНомера в каталоге: первый ребёнок = Catalog[0], второй = Catalog[1]…")]
        [SerializeField] private Transform? businessRowsParent;

        [Tooltip("Если Rows Parent не задан: перетащите сюда карточки в том же порядке, что и Catalog (первая = бизнес 0). Перенумерация сделает контроллер сам.")]
        [SerializeField] private BusinessListRowUI[] listRows = Array.Empty<BusinessListRowUI>();

        [SerializeField] private BusinessSkillPanelUI skillPanel = null!;

        [Header("Энергия")]
        [SerializeField, Min(1)] private int energyMax = 3;
        [SerializeField, Min(1f)] private float energyChunkRegenSeconds = 1200f;

        [Header("Каталог")]
        [SerializeField] private BusinessCatalogEntry[] catalog = { new() };

        [Header("Верхняя панель")]
        [SerializeField] private TMP_Text energyCountText = null!;
        [SerializeField] private TMP_Text energyTimerText = null!;
        [SerializeField] private TMP_Text totalIncomePerHourText = null!;
        [SerializeField] private TMP_Text accumulatedAbbrevText = null!;
        [SerializeField] private Button claimButton = null!;
        [SerializeField, Min(1f)] private float accumulatedDisplayLerpPerSecond = 8f;

        [Header("Тексты кнопки строки")]
        [SerializeField] private string rowPrimaryBuyCaption = "Купить";
        [SerializeField] private string rowPrimaryUpgradeCaption = "Улучшить";

        [Header("Формат сумм")]
        [SerializeField] private string millionSuffix = " млн";
        [SerializeField] private string billionSuffix = " млрд";
        [SerializeField] private string thousandSep = ".";

        [SerializeField]
        [Tooltip("Подпись валюты в TMP после суммы. По умолчанию кириллическое «р». Если вижу квадрат — включи символ во Font Atlas TMP.")]
        private string rublesCurrencySuffix = "р";

        private BusinessListRowUI[] _resolvedRows = Array.Empty<BusinessListRowUI>();
        private int[] _levels = Array.Empty<int>();
        private double _accumulatedRubles;
        private int _energy;
        private long _lastSimUtcSeconds;
        private long _nextEnergyAtUtc;
        private BusinessTemporaryBuffKind _tempKind;
        private long _tempBuffEndUtc;
        private float _tempMultiplier = 1f;
        private int _selectedIndex;

        private double _displayAccumRubles;
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

        private static void WarnRowCatalogMismatch(string context, int rowCount, int catalogLength)
        {
            if (catalogLength <= 0 || rowCount == catalogLength)
                return;
            Debug.LogWarning(
                $"[Business]{context}: карточек {rowCount}, записей каталога {catalogLength}. Сделайте число строк и элементов Catalog одинаковым.");
        }

        private bool IsPrefabRowSpawnConfigured() => rowCardPrefab != null && rowCardsContent != null;

        private static void DestroyImmediateChildren(RectTransform parent)
        {
            for (var i = parent.childCount - 1; i >= 0; i--)
                Destroy(parent.GetChild(i).gameObject);
        }

        private void SpawnCardsFromPrefab()
        {
            DestroyImmediateChildren(rowCardsContent!);

            if (catalog.Length == 0)
            {
                _resolvedRows = Array.Empty<BusinessListRowUI>();
                return;
            }

            var buf = new List<BusinessListRowUI>(catalog.Length);
            for (var i = 0; i < catalog.Length; i++)
            {
                var row = Instantiate(rowCardPrefab, rowCardsContent!, worldPositionStays: false);
                row.gameObject.name = $"{rowCardPrefab!.name}_{i}";
                row.AssignCatalogIndex(i);
                row.ResolveButtonReferences();
                buf.Add(row);
            }

            _resolvedRows = buf.ToArray();

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rowCardsContent!);
        }

        private void ResolveEffectiveRows()
        {
            if (IsPrefabRowSpawnConfigured())
            {
                if (businessRowsParent != null || (listRows is { Length: > 0 }))
                    Debug.LogWarning(
                        "[Business] Режим префаба: поля Rows Parent и ручной список строк не используются.",
                        this);

                SpawnCardsFromPrefab();
                WarnRowCatalogMismatch("(префаб)", _resolvedRows.Length, catalog?.Length ?? 0);
                return;
            }

            if (rowCardPrefab != null || rowCardsContent != null)
                Debug.LogWarning("[Business] Для автоспавна задайте и Row Card Prefab и Row Cards Content.", this);

            if (businessRowsParent != null)
            {
                if (listRows is { Length: > 0 })
                    Debug.LogWarning(
                        "[Business] Заполнены и Rows Parent, и ручной список строк — используются только дочерние объекты у Rows Parent.",
                        this);

                var buf = new List<BusinessListRowUI>();
                for (var i = 0; i < businessRowsParent.childCount; i++)
                {
                    var row = businessRowsParent.GetChild(i).GetComponent<BusinessListRowUI>();
                    if (row == null)
                        continue;

                    row.ResolveButtonReferences();
                    row.AssignCatalogIndex(buf.Count);
                    buf.Add(row);
                }

                _resolvedRows = buf.ToArray();
                WarnRowCatalogMismatch(" (Rows Parent)", _resolvedRows.Length, catalog?.Length ?? 0);
                return;
            }

            if (listRows is { Length: > 0 })
            {
                var buf = new List<BusinessListRowUI>();
                foreach (var row in listRows)
                {
                    if (row == null)
                        continue;
                    row.ResolveButtonReferences();
                    row.AssignCatalogIndex(buf.Count);
                    buf.Add(row);
                }

                _resolvedRows = buf.ToArray();
                WarnRowCatalogMismatch(" (ручной список строк)", _resolvedRows.Length, catalog?.Length ?? 0);
                return;
            }

            _resolvedRows = Array.Empty<BusinessListRowUI>();
            if ((catalog?.Length ?? 0) > 0)
                Debug.LogWarning(
                    "[Business] Нет ни Rows Parent, ни карточек в списке — откройте экран только после добавления строк.",
                    this);
        }

        private void Awake()
        {
            if (profile == null)
                profile = FindAnyObjectByType<PlayerProfile>();

            EnsureArrays();
            ResolveEffectiveRows();
            Load();
            ProcessOfflineCatchUp();
            foreach (var row in _resolvedRows)
            {
                if (row == null)
                    continue;

                var captured = row.BusinessIndex;
                row.BindSelect(() => SelectBusiness(captured));
                row.BindPrimaryAction(() => PrimaryActionOnRow(captured));
            }

            if (claimButton != null)
                claimButton.onClick.AddListener(Claim);

            if (skillPanel != null)
                skillPanel.SetLaunchListener(TryLaunchSkillForSelection);

            SelectBusiness(Mathf.Clamp(_selectedIndex, 0, Mathf.Max(0, catalog.Length - 1)));
        }

        private void OnEnable()
        {
            ProcessOfflineCatchUp();
            RefreshAllUi();
        }

        private void OnDisable()
        {
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
            var dt = Time.unscaledDeltaTime;
            SimulateTick(Time.deltaTime);

            _displayAccumRubles += (_accumulatedRubles - _displayAccumRubles)
                * (1d - Math.Exp(-accumulatedDisplayLerpPerSecond * dt));

            RefreshDynamicUi();

            if (!_dirty)
                return;

            _saveTimer -= Time.unscaledDeltaTime;
            if (_saveTimer <= 0f)
                SaveNow();
        }

        private void SimulateTick(float dt)
        {
            if (dt <= 0f)
                return;

            var perSecond = GetTotalBaseIncomePerHour() / 3600d * GetAccumulatedBusinessPassiveMultiplier();
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
            _lastSimUtcSeconds = now;
            if (elapsed <= 0)
            {
                RegenEnergyOffline(now);
                ClearTemporaryBuffIfExpired();
                return;
            }

            SimulateTick((float)Math.Min(elapsed, 86400d));
            RegenEnergyOffline(now);
            ClearTemporaryBuffIfExpired();
        }

        private void RegenEnergyOffline(long nowUtc)
        {
            if (energyMax <= 0)
                return;

            if (_energy >= energyMax)
            {
                _nextEnergyAtUtc = 0;
                return;
            }

            if (_nextEnergyAtUtc <= 0)
            {
                _nextEnergyAtUtc = nowUtc + (long)Mathf.Max(1f, energyChunkRegenSeconds);
                return;
            }

            var interval = (long)Mathf.Max(1f, energyChunkRegenSeconds);
            while (_energy < energyMax && nowUtc >= _nextEnergyAtUtc)
            {
                _energy++;
                if (_energy >= energyMax)
                {
                    _nextEnergyAtUtc = 0;
                    break;
                }

                _nextEnergyAtUtc += interval;
            }
        }

        private void ClearTemporaryBuffIfExpired()
        {
            if (_tempKind == BusinessTemporaryBuffKind.None)
                return;

            if (UtcNow() >= _tempBuffEndUtc)
            {
                _tempKind = BusinessTemporaryBuffKind.None;
                _tempBuffEndUtc = 0;
                _tempMultiplier = 1f;
                MarkDirty();
            }
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
            var wasFull = _energy >= energyMax;
            _energy = Math.Max(0, _energy - amount);
            if (_energy < energyMax && (wasFull || _nextEnergyAtUtc <= 0))
                _nextEnergyAtUtc = UtcNow() + (long)Mathf.Max(1f, energyChunkRegenSeconds);

            MarkDirty();
        }

        private void PrimaryActionOnRow(int index)
        {
            EnsureArrays();
            if (index < 0 || index >= catalog.Length)
                return;

            var lvl = _levels[index];
            if (lvl <= 0)
                TryPurchase(index);
            else
                TryUpgrade(index);
        }

        private void TryPurchase(int index)
        {
            if (profile == null)
                return;

            var cost = GetTierCostForUpgradeFrom(index, 0);
            if (cost <= 0 || !profile.TrySpendRubles(cost))
                return;

            _levels[index] = 1;
            MarkDirty();
            RefreshAllUi();
        }

        private void TryUpgrade(int index)
        {
            if (profile == null)
                return;

            var lvl = _levels[index];
            var tiers = catalog[index].tiers;
            if (lvl <= 0 || tiers == null || lvl >= tiers.Length)
                return;

            var cost = GetTierCostForUpgradeFrom(index, lvl);
            if (cost <= 0 || !profile.TrySpendRubles(cost))
                return;

            _levels[index] = lvl + 1;
            MarkDirty();
            RefreshAllUi();
        }

        private static long GetTierCostForUpgradeFrom(BusinessCatalogEntry entry, int currentLevel)
        {
            var tiers = entry.tiers;
            if (tiers == null || currentLevel >= tiers.Length)
                return 0;

            return Math.Max(0, tiers[currentLevel].upgradeRublesCost);
        }

        private long GetTierCostForUpgradeFrom(int bizIndex, int currentLevel)
        {
            return GetTierCostForUpgradeFrom(catalog[bizIndex], currentLevel);
        }

        private void TryLaunchSkillForSelection()
        {
            TryLaunchSkill(_selectedIndex);
        }

        /// <summary>Запуск навыка у выбранного бизнеса (кнопка «Запустить»).</summary>
        private void TryLaunchSkill(int bizIndex)
        {
            EnsureArrays();
            if (bizIndex < 0 || bizIndex >= catalog.Length)
                return;

            var entry = catalog[bizIndex];
            var lvl = _levels[bizIndex];
            if (lvl < entry.skillUnlockLevel)
                return;

            if (_energy < entry.skillEnergyCost)
                return;

            switch (entry.skillEffect)
            {
                case BusinessSkillEffectKind.InstantRubles:
                    if (profile == null)
                        return;
                    profile.AddRubles(Math.Max(0, entry.skillInstantRubles));
                    SpendEnergy(entry.skillEnergyCost);
                    MarkDirty();
                    RefreshSkillPanel();
                    return;

                case BusinessSkillEffectKind.TemporaryBoostAllBusinessIncome:
                    if (!TryStartTemporaryBoost(BusinessTemporaryBuffKind.AllBusinessIncome, entry.skillEnergyCost, entry.temporaryIncomeMultiplier, entry.temporaryDurationSeconds))
                        return;

                    RefreshSkillPanel();
                    return;

                case BusinessSkillEffectKind.TemporaryBoostMiningIncome:
                    if (!TryStartTemporaryBoost(BusinessTemporaryBuffKind.MiningIncome, entry.skillEnergyCost, entry.temporaryIncomeMultiplier, entry.temporaryDurationSeconds))
                        return;

                    RefreshSkillPanel();
                    return;

                case BusinessSkillEffectKind.MarketManipulate:
                    SpendEnergy(entry.skillEnergyCost);
                    marketSimulation?.EnqueueBusinessSkillOverrideNextState(entry.marketSkillCurrency);
                    MarkDirty();
                    RefreshSkillPanel();
                    return;

                default:
                    return;
            }
        }

        private bool TryStartTemporaryBoost(BusinessTemporaryBuffKind kind, int energyCost, float multiplier, float durationSeconds)
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
            _selectedIndex = Mathf.Clamp(index, 0, catalog.Length - 1);
            MarkDirty();

            foreach (var row in _resolvedRows)
            {
                if (row == null)
                    continue;

                var i = Mathf.Clamp(row.BusinessIndex, 0, catalog.Length - 1);
                var lvl = _levels.Length > i ? _levels[i] : 0;
                row.RefreshAppearance(i == _selectedIndex, lvl > 0);
            }

            RefreshDetailAndSkill();
        }

        private double GetTotalBaseIncomePerHour()
        {
            var sum = 0d;
            for (var i = 0; i < catalog.Length; i++)
            {
                var lvl = _levels.Length > i ? _levels[i] : 0;
                sum += GetIncomePerHour(i, lvl);
            }

            return sum;
        }

        private double GetIncomePerHour(int bizIndex, int level)
        {
            if (level <= 0)
                return 0;

            var tiers = catalog[bizIndex].tiers;
            if (tiers == null || tiers.Length == 0)
                return 0;

            var idx = Mathf.Clamp(level - 1, 0, tiers.Length - 1);
            return Math.Max(0d, tiers[idx].incomeRublesPerHour);
        }

        private void RefreshAllUi()
        {
            EnsureArrays();

            foreach (var row in _resolvedRows)
            {
                if (row == null)
                    continue;

                var i = Mathf.Clamp(row.BusinessIndex, 0, catalog.Length - 1);
                RefreshRowUi(row, i);
            }

            RefreshDetailAndSkill();

            if (totalIncomePerHourText != null)
            {
                var total = GetTotalBaseIncomePerHour() * GetAccumulatedBusinessPassiveMultiplier();
                totalIncomePerHourText.text = $"{FormatNumberCompact(total)}{rublesCurrencySuffix} в час";
            }

            RefreshSkillPanel();
        }

        private void RefreshRowUi(BusinessListRowUI row, int index)
        {
            var entry = catalog[index];
            var lvl = _levels[index];

            var incomeHr = lvl > 0 ? GetIncomePerHour(index, lvl) : 0d;
            string incomeLine = lvl > 0
                ? $"{FormatNumberMoney(incomeHr)}{rublesCurrencySuffix} в час"
                : "—";

            string levelLine;
            string priceLine;
            bool showPrimary;
            bool primaryInteractable;
            string primaryVerb = "";
            if (lvl <= 0)
            {
                levelLine = "Не куплено";
                var c = Math.Max(0, entry.tiers[0].upgradeRublesCost);
                priceLine = c > 0 ? FormatNumberMoney(c) : "";
                showPrimary = true;
                primaryInteractable = c > 0;
                primaryVerb = rowPrimaryBuyCaption;
            }
            else if (lvl >= entry.tiers.Length)
            {
                levelLine = $"Уровень {lvl} (макс.)";
                priceLine = "";
                showPrimary = false;
                primaryInteractable = false;
            }
            else
            {
                levelLine = $"Уровень {lvl}";
                var c = GetTierCostForUpgradeFrom(index, lvl);
                priceLine = c > 0 ? FormatNumberMoney(c) : "";
                showPrimary = c > 0;
                primaryInteractable = c > 0;
                primaryVerb = rowPrimaryUpgradeCaption;
            }

            row.RefreshRow(
                entry.displayName,
                entry.category,
                incomeLine,
                levelLine,
                priceLine,
                primaryVerb,
                showPrimary,
                primaryInteractable);
            row.RefreshAppearance(index == _selectedIndex, lvl > 0);
        }

        private void RefreshDetailAndSkill()
        {
            RefreshSkillPanel();
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
                {
                    var left = Math.Max(0, _nextEnergyAtUtc - UtcNow());
                    energyTimerText.text = FormatCountdown(left);
                }
            }

            if (accumulatedAbbrevText != null)
                accumulatedAbbrevText.text = AbbreviateRubles(_displayAccumRubles);

            RefreshSkillPanel();
        }

        private void RefreshSkillPanel()
        {
            if (skillPanel == null)
                return;

            EnsureArrays();
            var entry = catalog[_selectedIndex];
            var lvl = _levels[_selectedIndex];

            if (lvl < entry.skillUnlockLevel)
            {
                skillPanel.PresentLocked(entry.skillUnlockLevel, entry.lockedSkillFormat);
                skillPanel.SetLaunchListener(null);
                return;
            }

            ClearTemporaryBuffIfExpired();

            var energyOk = _energy >= entry.skillEnergyCost;
            var skillIsTemporaryBoost = entry.skillEffect is BusinessSkillEffectKind.TemporaryBoostAllBusinessIncome
                or BusinessSkillEffectKind.TemporaryBoostMiningIncome;

            var blockingGlobalTemporary = skillIsTemporaryBoost && IsGlobalTemporaryBuffBlocking();

            string launchLabel;
            bool interactable;
            if (skillIsTemporaryBoost && blockingGlobalTemporary && IsTemporaryBuffActive(out var secLeft))
            {
                launchLabel = string.Format(entry.temporarySkillActiveFormat, FormatCountdown(secLeft));
                interactable = false;
            }
            else
            {
                launchLabel = string.Format(entry.launchButtonIdleFormat, entry.skillEnergyCost);
                interactable = energyOk && !blockingGlobalTemporary;
            }

            skillPanel.PresentUnlocked(entry.skillIcon, entry.skillTitle, entry.skillDescription, interactable, launchLabel);
            skillPanel.SetLaunchListener(TryLaunchSkillForSelection);
        }

        private string FormatCountdown(double secondsTotal)
        {
            secondsTotal = Math.Max(0, secondsTotal);
            var totalMinutes = (int)(secondsTotal / 60d);
            var secs = (int)(secondsTotal % 60d);
            return $"{totalMinutes:00}:{secs:00}";
        }

        private string FormatNumberMoney(double v)
        {
            return Math.Max(0, v)
                .ToString("N0", CultureInfo.InvariantCulture)
                .Replace(",", thousandSep);
        }

        private string FormatNumberCompact(double v)
        {
            return Math.Max(0, v)
                .ToString("N0", CultureInfo.InvariantCulture)
                .Replace(",", thousandSep);
        }

        private string AbbreviateRubles(double value)
        {
            value = Math.Max(0, value);
            if (value >= 1_000_000_000d)
                return (value / 1_000_000_000d).ToString("0.0", CultureInfo.InvariantCulture) + billionSuffix;
            if (value >= 1_000_000d)
                return (value / 1_000_000d).ToString("0.0", CultureInfo.InvariantCulture) + millionSuffix;
            if (value >= 1000d)
                return (value / 1000d).ToString("0.0", CultureInfo.InvariantCulture) + " тыс";

            return FormatNumberMoney(value);
        }

        private void EnsureArrays()
        {
            var n = Mathf.Max(1, catalog?.Length ?? 0);
            if (_levels == null || _levels.Length != n)
            {
                var next = new int[n];
                if (_levels != null)
                    Array.Copy(_levels, next, Math.Min(_levels.Length, n));

                _levels = next;
            }

            _selectedIndex = Mathf.Clamp(_selectedIndex, 0, n - 1);
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
            var data = new SaveData
            {
                levels = (int[])_levels.Clone(),
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

            if (_energy < energyMax && _nextEnergyAtUtc <= 0)
                _nextEnergyAtUtc = UtcNow() + (long)Mathf.Max(1f, energyChunkRegenSeconds);

            _tempKind = Enum.IsDefined(typeof(BusinessTemporaryBuffKind), data.tempKind)
                ? (BusinessTemporaryBuffKind)data.tempKind
                : BusinessTemporaryBuffKind.None;

            _tempBuffEndUtc = Math.Max(0, data.tempBuffEndUtc);
            _tempMultiplier = Mathf.Max(1f, data.tempMultiplierHeld);

            if (data.levels != null && data.levels.Length == _levels.Length)
                Array.Copy(data.levels, _levels, _levels.Length);

            _selectedIndex = Mathf.Clamp(data.selectedBusinessIndex, 0, catalog.Length - 1);
            _displayAccumRubles = _accumulatedRubles;
            ClearTemporaryBuffIfExpired();
        }

        private static long UtcNow() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}
