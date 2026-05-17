using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using TraidingIDLE.Analytics;
using TraidingIDLE.Currencies;
using TraidingIDLE.Integrations;
using TraidingIDLE.Player;
using TraidingIDLE.Saves;
using TraidingIDLE.UI;
using UnityEngine;
using UnityEngine.UI;

namespace TraidingIDLE.Mining
{
    public sealed class MiningController : MonoBehaviour
    {
        private enum BoostCostCurrency
        {
            Rubles = 0,
            SHT = 1,
            ETH = 2,
            BTC = 3,
        }

        [Serializable]
        private sealed class RigLevelSettings
        {
            [Min(0)] public long costToReachLevelRubles;
            [Tooltip("Optional. If empty, this level reuses the previous configured rig sprite.")]
            public Sprite rigSprite;
            [Min(0f)] public float shtPerHour;
            [Min(0f)] public float ethPerHour;
            [Min(0f)] public float btcPerHour;
        }

        [Serializable]
        private sealed class CurrencyMiningSettings
        {
            public CurrencyId id;
            [Min(1f)] public float cycleDurationSeconds = 10f;
        }

        [Serializable]
        private sealed class BoostTextSettings
        {
            public string title = "";
            public string levelFormat = "{0}";
            public string descriptionFormat = "{0}";
            public string inactiveButtonFormat = "{0}";
            public string activeButtonFormat = "Активно {0}";
        }

        [Serializable]
        private sealed class CurrencyUnlockTextSettings
        {
            public string title = "Новая валюта";
            public string levelFormat = "{0} → {1}";
            public string descriptionFormat = "Все риги начнут добывать {0}";
            public string buttonFormat = "Открыть {0}";
            public string maxLevel = "MAX";
            public string maxDescription = "Все валюты открыты";
            public string maxButton = "MAX";
        }

        [Serializable]
        private sealed class SaveData
        {
            public int openedCurrency;
            public double accumulated;
            public int[] rigLevels = Array.Empty<int>();
            public float[] rigProgress = Array.Empty<float>();
            public float adSpeedTimeLeft;
            public float gemSpeedTimeLeft;
            public float coinIncomeTimeLeft;
            public int coinIncomeBoostUseCount;
            public long lastSimulationUtcSeconds;
        }

        private const string SaveKey = "save.mining.v1";

        [Header("Refs")]
        [SerializeField] private PlayerProfile profile = null!;
        [SerializeField] private CurrencyMarket market = null!;

        [Header("Passive links")]
        [SerializeField] private Business.BusinessController? businessPassiveLink;
        [SerializeField] private bool scaleMiningWithBusinessIncome = true;
        [Range(0f, 1f)]
        [SerializeField] private float shtTargetBusinessIncomeShare = 0.06f;
        [Range(0f, 1f)]
        [SerializeField] private float ethTargetBusinessIncomeShare = 0.09f;
        [Range(0f, 1f)]
        [SerializeField] private float btcTargetBusinessIncomeShare = 0.14f;
        [SerializeField, Min(1f)] private float maxBusinessMiningCatchUpMultiplier = 6f;

        [Header("Top UI")]
        [SerializeField] private TMP_Text totalIncomePerHourText = null!;
        [SerializeField] private TMP_Text accumulatedText = null!;
        [SerializeField] private Button claimButton = null!;

        [Header("Stats UI")]
        [SerializeField] private TMP_Text boughtRigsStatText = null!;
        [SerializeField] private TMP_Text maxedRigsStatText = null!;
        [SerializeField] private TMP_Text incomeBonusStatText = null!;
        [SerializeField] private TMP_Text speedBonusStatText = null!;

        [Header("Rig List")]
        [SerializeField] private Transform rigCardsRoot = null!;
        [SerializeField] private MiningRigCardUI rigCardPrefab = null!;
        [SerializeField] private MiningBuyRigCardUI buyRigCardPrefab = null!;
        [SerializeField] private MiningLockedRigCardUI lockedRigCardPrefab = null!;
        [SerializeField, Min(1)] private int maxRigCount = 7;

        [Header("Rig Levels (same for every rig)")]
        [SerializeField] private RigLevelSettings[] rigLevels =
        {
            new() { costToReachLevelRubles = 0, shtPerHour = 6f, ethPerHour = 0.5f, btcPerHour = 0.05f },
            new() { costToReachLevelRubles = 50_000, shtPerHour = 12f, ethPerHour = 1f, btcPerHour = 0.08f },
            new() { costToReachLevelRubles = 180_000, shtPerHour = 22f, ethPerHour = 1.8f, btcPerHour = 0.13f },
            new() { costToReachLevelRubles = 650_000, shtPerHour = 38f, ethPerHour = 2.8f, btcPerHour = 0.20f },
            new() { costToReachLevelRubles = 2_500_000, shtPerHour = 60f, ethPerHour = 4f, btcPerHour = 0.30f },
            new() { costToReachLevelRubles = 6_000_000, shtPerHour = 85f, ethPerHour = 5.6f, btcPerHour = 0.42f },
            new() { costToReachLevelRubles = 12_000_000, shtPerHour = 115f, ethPerHour = 7.5f, btcPerHour = 0.58f },
            new() { costToReachLevelRubles = 23_000_000, shtPerHour = 150f, ethPerHour = 9.8f, btcPerHour = 0.78f },
            new() { costToReachLevelRubles = 40_000_000, shtPerHour = 190f, ethPerHour = 12.5f, btcPerHour = 1.05f },
            new() { costToReachLevelRubles = 70_000_000, shtPerHour = 235f, ethPerHour = 16f, btcPerHour = 1.35f },
            new() { costToReachLevelRubles = 115_000_000, shtPerHour = 285f, ethPerHour = 20f, btcPerHour = 1.75f },
            new() { costToReachLevelRubles = 185_000_000, shtPerHour = 340f, ethPerHour = 25f, btcPerHour = 2.20f },
            new() { costToReachLevelRubles = 290_000_000, shtPerHour = 400f, ethPerHour = 31f, btcPerHour = 2.75f },
            new() { costToReachLevelRubles = 450_000_000, shtPerHour = 465f, ethPerHour = 38f, btcPerHour = 3.40f },
            new() { costToReachLevelRubles = 680_000_000, shtPerHour = 535f, ethPerHour = 46f, btcPerHour = 4.15f },
            new() { costToReachLevelRubles = 1_000_000_000, shtPerHour = 610f, ethPerHour = 55f, btcPerHour = 5.00f },
            new() { costToReachLevelRubles = 1_450_000_000, shtPerHour = 690f, ethPerHour = 65f, btcPerHour = 6.00f },
            new() { costToReachLevelRubles = 2_050_000_000, shtPerHour = 775f, ethPerHour = 76f, btcPerHour = 7.10f },
            new() { costToReachLevelRubles = 2_850_000_000, shtPerHour = 865f, ethPerHour = 88f, btcPerHour = 8.35f },
            new() { costToReachLevelRubles = 3_900_000_000, shtPerHour = 960f, ethPerHour = 102f, btcPerHour = 9.80f },
        };

        [Header("Rig Purchase")]
        [SerializeField] private long[] rigPurchaseCostsRubles =
        {
            50_000,
            150_000,
            450_000,
            1_200_000,
            3_000_000,
            7_500_000,
            18_000_000,
        };
        [SerializeField, Min(1f)] private float extraRigCostMultiplier = 2.4f;

        [Header("Mining Currency")]
        [SerializeField] private CurrencyMiningSettings[] currencySettings =
        {
            new() { id = CurrencyId.SHT, cycleDurationSeconds = 8f },
            new() { id = CurrencyId.ETH, cycleDurationSeconds = 30f },
            new() { id = CurrencyId.BTC, cycleDurationSeconds = 90f },
        };

        [Header("Boost Cards")]
        [SerializeField] private MiningBoostCardUI adSpeedBoostCard = null!;
        [SerializeField] private MiningBoostCardUI gemSpeedBoostCard = null!;
        [SerializeField] private MiningBoostCardUI coinIncomeBoostCard = null!;
        [SerializeField] private MiningBoostCardUI currencyUnlockBoostCard = null!;

        [Header("Boost 1: Ad speed")]
        [SerializeField, Min(1f)] private float adSpeedMultiplier = 2f;
        [SerializeField, Min(1f)] private float adSpeedDurationSeconds = 1800f;

        [Header("Boost 2: Gems speed")]
        [SerializeField, Min(1f)] private float gemSpeedMultiplier = 3f;
        [SerializeField, Min(1f)] private float gemSpeedDurationSeconds = 7200f;
        [SerializeField, Min(0)] private long gemSpeedCost = 100;

        [Header("Boost 3: Coin income")]
        [SerializeField, Min(1f)] private float coinIncomeMultiplier = 2f;
        [SerializeField, Min(1f)] private float coinIncomeDurationSeconds = 1800f;
        [SerializeField] private BoostCostCurrency coinIncomeCostCurrency = BoostCostCurrency.SHT;
        [SerializeField, Min(1)] private int coinIncomeBaseCost = 1_000_000;
        [SerializeField, Min(1f)] private float coinIncomeCostMultiplier = 2.2f;

        [Header("Boost 4: Currency unlock")]
        [SerializeField] private long[] currencyUnlockCostsRubles = { 12_000_000, 150_000_000 };

        [Header("Formats")]
        [SerializeField] private string totalIncomePerHourFormat = "{0} {1}";
        [SerializeField] private string accumulatedFormat = "{0} {1}";
        [SerializeField] private string boughtRigsStatFormat = "{0}/{1}";
        [SerializeField] private string maxedRigsStatFormat = "{0}/{1}";
        [SerializeField] private string bonusPercentFormat = "+{0}%";

        [Header("Boost Texts")]
        [SerializeField] private BoostTextSettings adSpeedBoostText = new()
        {
            title = "Реклама",
            levelFormat = "x{0} скорости",
            descriptionFormat = "На {0}",
            inactiveButtonFormat = "Активировать",
            activeButtonFormat = "Активно {0}",
        };
        [SerializeField] private BoostTextSettings gemSpeedBoostText = new()
        {
            title = "Гемы",
            levelFormat = "x{0} скорости",
            descriptionFormat = "На {0}",
            inactiveButtonFormat = "За {0} гемов",
            activeButtonFormat = "Активно {0}",
        };
        [SerializeField] private BoostTextSettings coinIncomeBoostText = new()
        {
            title = "Доход",
            levelFormat = "x{0} дохода",
            descriptionFormat = "На {0}",
            inactiveButtonFormat = "За {0} {1}",
            activeButtonFormat = "Активно {0}",
        };
        [SerializeField] private CurrencyUnlockTextSettings currencyUnlockBoostText = new();

        private readonly List<MiningRigCardUI> _rigCards = new();
        private int[] _rigStateLevels = Array.Empty<int>();
        private float[] _rigProgress = Array.Empty<float>();
        private CurrencyId _openedCurrency = CurrencyId.SHT;
        private double _accumulated;
        private float _adSpeedTimeLeft;
        private float _gemSpeedTimeLeft;
        private float _coinIncomeTimeLeft;
        private int _coinIncomeBoostUseCount;
        private long _lastSimulationUtcSeconds;
        private float _saveTimer;
        private bool _dirty;

        private int MaxRigLevel => Math.Max(1, rigLevels?.Length ?? 0);
        private CurrencyId ActiveCurrency => _openedCurrency;

        private void Awake()
        {
            if (profile == null)
                profile = FindAnyObjectByType<PlayerProfile>(FindObjectsInactive.Include);
            if (market == null)
                market = FindAnyObjectByType<CurrencyMarket>(FindObjectsInactive.Include);
            if (businessPassiveLink == null)
                businessPassiveLink = FindAnyObjectByType<Business.BusinessController>(FindObjectsInactive.Include);

            EnsureStateArrays();
            LoadFromStorage();
            EnsureStateArrays();
            ProcessElapsedSinceLastTimestamp();
        }

        private void OnEnable()
        {
            ProcessElapsedSinceLastTimestamp();

            if (claimButton != null)
                claimButton.onClick.AddListener(ClaimAccumulated);

            if (profile != null)
            {
                profile.RublesChanged += OnProfileChanged;
                profile.GemsChanged += OnProfileChanged;
                profile.HoldingsChanged += OnHoldingChanged;
            }

            RebuildRigCards();
            RefreshAll();
        }

        private void OnDisable()
        {
            if (claimButton != null)
                claimButton.onClick.RemoveListener(ClaimAccumulated);

            if (profile != null)
            {
                profile.RublesChanged -= OnProfileChanged;
                profile.GemsChanged -= OnProfileChanged;
                profile.HoldingsChanged -= OnHoldingChanged;
            }

            SaveToStorage();
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause)
                SaveToStorage();
        }

        private void OnApplicationQuit()
        {
            SaveToStorage();
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            if (dt > 0f)
            {
                ProcessSimulation(dt);
                RefreshDynamicUi();
                MarkDirty();
            }

            if (!_dirty)
                return;

            _saveTimer -= Time.unscaledDeltaTime;
            if (_saveTimer <= 0f)
                SaveToStorage();
        }

        private void ProcessElapsedSinceLastTimestamp()
        {
            var now = GetUtcSeconds();
            if (Time.timeScale <= 0f)
            {
                _lastSimulationUtcSeconds = now;
                return;
            }

            if (_lastSimulationUtcSeconds <= 0)
            {
                _lastSimulationUtcSeconds = now;
                return;
            }

            var elapsed = now - _lastSimulationUtcSeconds;
            _lastSimulationUtcSeconds = now;
            if (elapsed <= 0)
                return;

            ProcessSimulation(elapsed);
            MarkDirty();
        }

        private void ProcessSimulation(double seconds)
        {
            var remaining = Math.Max(0d, seconds);
            while (remaining > 0.0001d)
            {
                var segment = remaining;
                segment = MinPositive(segment, _adSpeedTimeLeft);
                segment = MinPositive(segment, _gemSpeedTimeLeft);
                segment = MinPositive(segment, _coinIncomeTimeLeft);

                var dt = (float)Math.Min(segment, float.MaxValue);
                TickRigs(dt);
                TickBoostTimers(dt);

                remaining -= dt;

                // Safety against precision stalls.
                if (dt <= 0.0001f)
                    break;
            }
        }

        private static double MinPositive(double current, float candidate)
        {
            return candidate > 0f ? Math.Min(current, candidate) : current;
        }

        private void TickBoostTimers(float dt)
        {
            _adSpeedTimeLeft = Mathf.Max(0f, _adSpeedTimeLeft - dt);
            _gemSpeedTimeLeft = Mathf.Max(0f, _gemSpeedTimeLeft - dt);
            _coinIncomeTimeLeft = Mathf.Max(0f, _coinIncomeTimeLeft - dt);
        }

        private void TickRigs(float dt)
        {
            var speedMultiplier = GetSpeedMultiplier();
            var incomeMultiplier = GetIncomeMultiplier();

            for (var i = 0; i < _rigStateLevels.Length; i++)
            {
                var level = _rigStateLevels[i];
                if (level <= 0)
                    continue;

                var baseIncomePerHour = GetRigIncomePerHour(level, ActiveCurrency);
                var earnedCoins = baseIncomePerHour * incomeMultiplier * speedMultiplier / 3600d * dt;
                if (earnedCoins <= 0d)
                    continue;

                _rigProgress[i] += (float)Math.Min(earnedCoins, float.MaxValue);
                if (_rigProgress[i] < 1f)
                    continue;

                var completedCoins = Mathf.FloorToInt(_rigProgress[i]);
                _rigProgress[i] -= completedCoins;
                _accumulated += completedCoins;
            }
        }

        private void ClaimAccumulated()
        {
            if (profile == null)
                return;

            var available = Mathf.FloorToInt((float)Math.Min(_accumulated, int.MaxValue));
            if (available <= 0)
                return;

            if (profile.TryAddCoins(ActiveCurrency, available, out var added))
            {
                _accumulated = Math.Max(0d, _accumulated - added);
                MarkDirty();
                RefreshAll();
            }
        }

        private void BuyRig(int rigIndex)
        {
            if (profile == null || rigIndex < 0 || rigIndex >= _rigStateLevels.Length)
                return;

            if (_rigStateLevels[rigIndex] > 0)
                return;

            if (rigIndex != GetBoughtRigCount())
                return;

            var cost = GetRigPurchaseCost(rigIndex);
            if (!profile.TrySpendRubles(cost))
                return;

            _rigStateLevels[rigIndex] = 1;
            _rigProgress[rigIndex] = 0f;
            AnalyticsTracker.ReportRigBuy(rigIndex, ActiveCurrency, cost);
            MarkDirty();
            RebuildRigCards();
            RefreshAll();
        }

        private void UpgradeRig(int rigIndex)
        {
            if (profile == null || rigIndex < 0 || rigIndex >= _rigStateLevels.Length)
                return;

            var level = _rigStateLevels[rigIndex];
            if (level <= 0 || level >= MaxRigLevel)
                return;

            var cost = GetUpgradeCost(level);
            if (!profile.TrySpendRubles(cost))
                return;

            _rigStateLevels[rigIndex] = level + 1;
            MarkDirty();
            RebuildRigCards();
            RefreshAll();
        }

        private void RequestAdSpeedBoostRewarded()
        {
            if (_adSpeedTimeLeft > 0f)
                return;

            YandexRewardedAds.Show(YandexRewardedAds.MiningAdSpeedBoostId, ActivateAdSpeedBoost);
        }

        private void ActivateAdSpeedBoost()
        {
            if (_adSpeedTimeLeft > 0f)
                return;

            _adSpeedTimeLeft = adSpeedDurationSeconds;
            MarkDirty();
            RefreshAll();
        }

        private void ActivateGemSpeedBoost()
        {
            if (_gemSpeedTimeLeft > 0f)
                return;

            if (profile == null)
                return;

            if (!profile.TrySpendGems(gemSpeedCost))
                return;

            _gemSpeedTimeLeft = gemSpeedDurationSeconds;
            MarkDirty();
            RefreshAll();
        }

        private void ActivateCoinIncomeBoost()
        {
            if (_coinIncomeTimeLeft > 0f)
                return;

            if (profile == null)
                return;

            var cost = GetCoinIncomeBoostCost();
            if (!TrySpendBoostCost(coinIncomeCostCurrency, cost))
                return;

            _coinIncomeTimeLeft = coinIncomeDurationSeconds;
            _coinIncomeBoostUseCount++;
            MarkDirty();
            RefreshAll();
        }

        private void UnlockNextCurrency()
        {
            if (profile == null || market == null)
                return;

            if (!TryGetNextCurrency(ActiveCurrency, out var next))
                return;

            var cost = GetCurrencyUnlockCost(ActiveCurrency);
            if (!profile.TrySpendRubles(cost))
                return;

            ConvertAccumulated(ActiveCurrency, next);
            _openedCurrency = next;
            for (var i = 0; i < _rigProgress.Length; i++)
                _rigProgress[i] = 0f;

            MarkDirty();
            RebuildRigCards();
            RefreshAll();
        }

        private void ConvertAccumulated(CurrencyId from, CurrencyId to)
        {
            if (market == null || _accumulated <= 0d)
                return;

            var fromPrice = Math.Max(0.000001d, market.GetPrice(from));
            var toPrice = Math.Max(0.000001d, market.GetPrice(to));
            _accumulated = Math.Ceiling(_accumulated * fromPrice / toPrice);
        }

        private void RebuildRigCards()
        {
            _rigCards.Clear();
            if (rigCardsRoot == null)
                return;

            for (var i = rigCardsRoot.childCount - 1; i >= 0; i--)
                Destroy(rigCardsRoot.GetChild(i).gameObject);

            var bought = GetBoughtRigCount();
            for (var i = 0; i < bought; i++)
            {
                if (rigCardPrefab == null)
                    continue;

                var rigCard = Instantiate(rigCardPrefab, rigCardsRoot);
                _rigCards.Add(rigCard);
                ConfigureRigCard(rigCard, i);
            }

            if (bought < maxRigCount)
            {
                if (buyRigCardPrefab != null)
                {
                    var buyCard = Instantiate(buyRigCardPrefab, rigCardsRoot);
                    buyCard.Configure(bought, GetRigPurchaseCost(bought), CanSpendRubles(GetRigPurchaseCost(bought)), BuyRig);
                }

                if (bought < maxRigCount - 1 && lockedRigCardPrefab != null)
                {
                    var lockedIndex = bought + 1;
                    var locked = Instantiate(lockedRigCardPrefab, rigCardsRoot);
                    locked.Configure(lockedIndex);
                }
            }

            StartCoroutine(RefreshRigStripLayoutDeferred());
        }

        private IEnumerator RefreshRigStripLayoutDeferred()
        {
            yield return null;
            if (rigCardsRoot == null)
                yield break;

            var sizer = rigCardsRoot.GetComponent<HorizontalScrollContentLayoutSize>();
            if (sizer != null)
                sizer.Refresh();
        }

        private void ConfigureRigCard(MiningRigCardUI card, int rigIndex)
        {
            var level = _rigStateLevels[rigIndex];
            var upgradeCost = GetUpgradeCost(level);
            var canAfford = CanSpendRubles(upgradeCost);
            card.Configure(
                rigIndex,
                level,
                MaxRigLevel,
                GetRigIncomePerHour(level, ActiveCurrency) * GetIncomeMultiplier() * GetSpeedMultiplier(),
                ActiveCurrency,
                GetRigSprite(level),
                upgradeCost,
                canAfford,
                UpgradeRig);
            card.SetProgress(_rigProgress[rigIndex]);
        }

        private void RefreshAll()
        {
            RefreshDynamicUi();
            RefreshBoostCards();
            RefreshStats();
            RefreshRigCardsStatic();
        }

        private void RefreshDynamicUi()
        {
            if (totalIncomePerHourText != null)
            {
                totalIncomePerHourText.text = string.Format(
                    SafeFormat(totalIncomePerHourFormat, "{0} {1}"),
                    FormatAmount(GetTotalIncomePerHour(), ActiveCurrency),
                    ActiveCurrency);
            }

            if (accumulatedText != null)
            {
                accumulatedText.text = string.Format(
                    SafeFormat(accumulatedFormat, "{0} {1}"),
                    FormatAmount(Math.Floor(_accumulated), ActiveCurrency),
                    ActiveCurrency);
            }

            if (claimButton != null)
            {
                var canClaim = profile != null
                    && Math.Floor(_accumulated) >= 1d
                    && profile.GetAvailableRoom(ActiveCurrency) > 0;
                claimButton.interactable = canClaim;
            }

            RefreshRigProgress();
        }

        private void RefreshRigProgress()
        {
            var cardIndex = 0;
            for (var i = 0; i < _rigStateLevels.Length && cardIndex < _rigCards.Count; i++)
            {
                if (_rigStateLevels[i] <= 0)
                    continue;

                _rigCards[cardIndex].SetProgress(_rigProgress[i]);
                cardIndex++;
            }
        }

        private void RefreshRigCardsStatic()
        {
            var cardIndex = 0;
            for (var i = 0; i < _rigStateLevels.Length && cardIndex < _rigCards.Count; i++)
            {
                if (_rigStateLevels[i] <= 0)
                    continue;

                ConfigureRigCard(_rigCards[cardIndex], i);
                cardIndex++;
            }
        }

        private void RefreshBoostCards()
        {
            if (adSpeedBoostCard != null)
            {
                var isActive = _adSpeedTimeLeft > 0f;
                var activeText = _adSpeedTimeLeft > 0f
                    ? FormatOne(adSpeedBoostText.activeButtonFormat, "Активно {0}", FormatTimer(_adSpeedTimeLeft))
                    : SafeFormat(adSpeedBoostText.inactiveButtonFormat, "Активировать");
                adSpeedBoostCard.Configure(
                    SafeFormat(adSpeedBoostText.title, "Реклама"),
                    FormatOne(adSpeedBoostText.levelFormat, "x{0} скорости", FormatMultiplier(adSpeedMultiplier)),
                    FormatOne(adSpeedBoostText.descriptionFormat, "На {0}", FormatDuration(adSpeedDurationSeconds)),
                    activeText,
                    !isActive,
                    RequestAdSpeedBoostRewarded);
            }

            if (gemSpeedBoostCard != null)
            {
                var isActive = _gemSpeedTimeLeft > 0f;
                var activeText = isActive
                    ? FormatOne(gemSpeedBoostText.activeButtonFormat, "Активно {0}", FormatTimer(_gemSpeedTimeLeft))
                    : FormatOne(gemSpeedBoostText.inactiveButtonFormat, "За {0} гемов", FormatRubles(gemSpeedCost));
                gemSpeedBoostCard.Configure(
                    SafeFormat(gemSpeedBoostText.title, "Гемы"),
                    FormatOne(gemSpeedBoostText.levelFormat, "x{0} скорости", FormatMultiplier(gemSpeedMultiplier)),
                    FormatOne(gemSpeedBoostText.descriptionFormat, "На {0}", FormatDuration(gemSpeedDurationSeconds)),
                    activeText,
                    !isActive && profile != null && profile.Gems >= gemSpeedCost,
                    ActivateGemSpeedBoost);
            }

            if (coinIncomeBoostCard != null)
            {
                var cost = GetCoinIncomeBoostCost();
                var isActive = _coinIncomeTimeLeft > 0f;
                var activeText = isActive
                    ? FormatOne(coinIncomeBoostText.activeButtonFormat, "Активно {0}", FormatTimer(_coinIncomeTimeLeft))
                    : FormatTwo(coinIncomeBoostText.inactiveButtonFormat, "За {0} {1}", FormatRubles(cost), FormatBoostCostCurrency(coinIncomeCostCurrency));
                coinIncomeBoostCard.Configure(
                    SafeFormat(coinIncomeBoostText.title, "Доход"),
                    FormatOne(coinIncomeBoostText.levelFormat, "x{0} дохода", FormatMultiplier(coinIncomeMultiplier)),
                    FormatOne(coinIncomeBoostText.descriptionFormat, "На {0}", FormatDuration(coinIncomeDurationSeconds)),
                    activeText,
                    !isActive && CanSpendBoostCost(coinIncomeCostCurrency, cost),
                    ActivateCoinIncomeBoost);
            }

            if (currencyUnlockBoostCard != null)
            {
                if (!TryGetNextCurrency(ActiveCurrency, out var next))
                {
                    currencyUnlockBoostCard.Configure(
                        SafeFormat(currencyUnlockBoostText.title, "Новая валюта"),
                        SafeFormat(currencyUnlockBoostText.maxLevel, "MAX"),
                        SafeFormat(currencyUnlockBoostText.maxDescription, "Все валюты открыты"),
                        SafeFormat(currencyUnlockBoostText.maxButton, "MAX"),
                        false,
                        UnlockNextCurrency);
                }
                else
                {
                    var cost = GetCurrencyUnlockCost(ActiveCurrency);
                    currencyUnlockBoostCard.Configure(
                        SafeFormat(currencyUnlockBoostText.title, "Новая валюта"),
                        FormatTwo(currencyUnlockBoostText.levelFormat, "{0} → {1}", ActiveCurrency.ToString(), next.ToString()),
                        FormatOne(currencyUnlockBoostText.descriptionFormat, "Все риги начнут добывать {0}", next.ToString()),
                        FormatOne(currencyUnlockBoostText.buttonFormat, "Открыть {0}", FormatRubles(cost)),
                        CanSpendRubles(cost),
                        UnlockNextCurrency);
                }
            }
        }

        private void RefreshStats()
        {
            var bought = GetBoughtRigCount();
            var maxed = GetMaxedRigCount();

            if (boughtRigsStatText != null)
                boughtRigsStatText.text = string.Format(SafeFormat(boughtRigsStatFormat, "{0}/{1}"), bought, maxRigCount);

            if (maxedRigsStatText != null)
                maxedRigsStatText.text = string.Format(SafeFormat(maxedRigsStatFormat, "{0}/{1}"), maxed, maxRigCount);

            if (incomeBonusStatText != null)
                incomeBonusStatText.text = string.Format(SafeFormat(bonusPercentFormat, "+{0}%"), Mathf.RoundToInt((float)((GetDisplayedIncomeBonusMultiplier() - 1d) * 100d)));

            if (speedBonusStatText != null)
                speedBonusStatText.text = string.Format(SafeFormat(bonusPercentFormat, "+{0}%"), Mathf.RoundToInt((float)((GetSpeedMultiplier() - 1d) * 100d)));
        }

        private double GetTotalIncomePerHour()
        {
            var total = 0d;
            for (var i = 0; i < _rigStateLevels.Length; i++)
            {
                if (_rigStateLevels[i] > 0)
                    total += GetRigIncomePerHour(_rigStateLevels[i], ActiveCurrency);
            }

            return total * GetIncomeMultiplier() * GetSpeedMultiplier();
        }

        private double GetRigIncomePerHour(int level, CurrencyId currency)
        {
            if (rigLevels == null || rigLevels.Length == 0)
                return 0d;

            var index = Mathf.Clamp(level - 1, 0, rigLevels.Length - 1);
            var cfg = rigLevels[index];
            return currency switch
            {
                CurrencyId.SHT => Math.Max(0f, cfg.shtPerHour),
                CurrencyId.ETH => Math.Max(0f, cfg.ethPerHour),
                CurrencyId.BTC => Math.Max(0f, cfg.btcPerHour),
                _ => 0d,
            };
        }

        private Sprite GetRigSprite(int level)
        {
            if (rigLevels == null || rigLevels.Length == 0)
                return null;

            var index = Mathf.Clamp(level - 1, 0, rigLevels.Length - 1);
            for (var i = index; i >= 0; i--)
            {
                if (rigLevels[i] != null && rigLevels[i].rigSprite != null)
                    return rigLevels[i].rigSprite;
            }

            return null;
        }

        // В статистике «Бонус к доходу» только явные бафы; catch-up от бизнеса зависит от ригов и туда не входит.
        private double GetDisplayedIncomeBonusMultiplier()
        {
            var fromBoost = _coinIncomeTimeLeft > 0f ? Math.Max(1f, coinIncomeMultiplier) : 1d;
            var business = businessPassiveLink != null ? businessPassiveLink.GetMiningIncomeMultiplierFromBusinessSkills() : 1d;
            return fromBoost * business;
        }

        private double GetIncomeMultiplier()
        {
            var fromBoost = _coinIncomeTimeLeft > 0f ? Math.Max(1f, coinIncomeMultiplier) : 1d;
            var business = businessPassiveLink != null ? businessPassiveLink.GetMiningIncomeMultiplierFromBusinessSkills() : 1d;
            var economy = GetBusinessEconomyMiningMultiplier(ActiveCurrency);
            return fromBoost * business * economy;
        }

        private double GetBusinessEconomyMiningMultiplier(CurrencyId currency)
        {
            if (!scaleMiningWithBusinessIncome || businessPassiveLink == null || market == null)
                return 1d;

            var businessIncomePerHour = businessPassiveLink.GetTotalEffectiveIncomePerHour();
            if (businessIncomePerHour <= 0d)
                return 1d;

            var baseCoinIncomePerHour = 0d;
            for (var i = 0; i < _rigStateLevels.Length; i++)
            {
                if (_rigStateLevels[i] > 0)
                    baseCoinIncomePerHour += GetRigIncomePerHour(_rigStateLevels[i], currency);
            }

            if (baseCoinIncomePerHour <= 0d)
                return 1d;

            var coinPrice = Math.Max(1d, market.GetPrice(currency));
            var baseRublesPerHour = baseCoinIncomePerHour * coinPrice;
            var targetRublesPerHour = businessIncomePerHour * GetTargetBusinessIncomeShare(currency);
            if (targetRublesPerHour <= baseRublesPerHour)
                return 1d;

            var catchUp = Math.Sqrt(targetRublesPerHour / Math.Max(1d, baseRublesPerHour));
            return ClampDouble(catchUp, 1d, Math.Max(1f, maxBusinessMiningCatchUpMultiplier));
        }

        private float GetTargetBusinessIncomeShare(CurrencyId currency)
        {
            return currency switch
            {
                CurrencyId.SHT => shtTargetBusinessIncomeShare,
                CurrencyId.ETH => ethTargetBusinessIncomeShare,
                CurrencyId.BTC => btcTargetBusinessIncomeShare,
                _ => ethTargetBusinessIncomeShare,
            };
        }

        private static double ClampDouble(double value, double min, double max)
        {
            if (max < min)
                (min, max) = (max, min);
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }

        private double GetSpeedMultiplier()
        {
            var speed = 1d;
            if (_adSpeedTimeLeft > 0f)
                speed *= Math.Max(1f, adSpeedMultiplier);
            if (_gemSpeedTimeLeft > 0f)
                speed *= Math.Max(1f, gemSpeedMultiplier);
            return speed;
        }

        private long GetUpgradeCost(int currentLevel)
        {
            if (rigLevels == null || rigLevels.Length == 0 || currentLevel >= MaxRigLevel)
                return 0;

            var nextLevelIndex = Mathf.Clamp(currentLevel, 0, rigLevels.Length - 1);
            return Math.Max(0, rigLevels[nextLevelIndex].costToReachLevelRubles);
        }

        private long GetRigPurchaseCost(int rigIndex)
        {
            if (rigIndex < 0)
                return 0;

            if (rigPurchaseCostsRubles != null && rigIndex < rigPurchaseCostsRubles.Length)
                return Math.Max(0, rigPurchaseCostsRubles[rigIndex]);

            var baseCost = rigPurchaseCostsRubles != null && rigPurchaseCostsRubles.Length > 0
                ? Math.Max(1, rigPurchaseCostsRubles[^1])
                : 50_000;
            var extraSteps = rigIndex - (rigPurchaseCostsRubles?.Length ?? 0) + 1;
            return (long)Math.Round(baseCost * Math.Pow(Math.Max(1f, extraRigCostMultiplier), Math.Max(0, extraSteps)));
        }

        private int GetCoinIncomeBoostCost()
        {
            var cost = coinIncomeBaseCost * Math.Pow(Math.Max(1f, coinIncomeCostMultiplier), _coinIncomeBoostUseCount);
            return Math.Max(1, (int)Math.Ceiling(cost));
        }

        private long GetCurrencyUnlockCost(CurrencyId current)
        {
            var index = current == CurrencyId.SHT ? 0 : 1;
            if (currencyUnlockCostsRubles != null && index < currencyUnlockCostsRubles.Length)
                return Math.Max(0, currencyUnlockCostsRubles[index]);
            return index == 0 ? 1_000_000 : 10_000_000;
        }

        private float GetCycleDuration(CurrencyId currency)
        {
            if (currencySettings != null)
            {
                for (var i = 0; i < currencySettings.Length; i++)
                {
                    if (currencySettings[i] != null && currencySettings[i].id == currency)
                        return Mathf.Max(0.1f, currencySettings[i].cycleDurationSeconds);
                }
            }

            return currency switch
            {
                CurrencyId.SHT => 8f,
                CurrencyId.ETH => 30f,
                CurrencyId.BTC => 90f,
                _ => 10f,
            };
        }

        private int GetBoughtRigCount()
        {
            var count = 0;
            for (var i = 0; i < _rigStateLevels.Length; i++)
            {
                if (_rigStateLevels[i] > 0)
                    count++;
            }

            return count;
        }

        private int GetMaxedRigCount()
        {
            var count = 0;
            for (var i = 0; i < _rigStateLevels.Length; i++)
            {
                if (_rigStateLevels[i] >= MaxRigLevel)
                    count++;
            }

            return count;
        }

        private bool CanSpendRubles(long amount)
        {
            return profile != null && amount >= 0 && profile.Rubles >= amount;
        }

        private bool CanSpendBoostCost(BoostCostCurrency currency, int amount)
        {
            if (profile == null || amount < 0)
                return false;

            if (currency == BoostCostCurrency.Rubles)
                return profile.Rubles >= amount;

            if (TryConvertBoostCostCurrency(currency, out var coin))
                return profile.GetAmount(coin) >= amount;

            return false;
        }

        private bool TrySpendBoostCost(BoostCostCurrency currency, int amount)
        {
            if (profile == null)
                return false;

            if (currency == BoostCostCurrency.Rubles)
                return profile.TrySpendRubles(amount);

            return TryConvertBoostCostCurrency(currency, out var coin)
                && profile.TrySpendCoins(coin, amount);
        }

        private static bool TryConvertBoostCostCurrency(BoostCostCurrency currency, out CurrencyId coin)
        {
            switch (currency)
            {
                case BoostCostCurrency.SHT:
                    coin = CurrencyId.SHT;
                    return true;
                case BoostCostCurrency.ETH:
                    coin = CurrencyId.ETH;
                    return true;
                case BoostCostCurrency.BTC:
                    coin = CurrencyId.BTC;
                    return true;
                default:
                    coin = CurrencyId.SHT;
                    return false;
            }
        }

        private static bool TryGetNextCurrency(CurrencyId current, out CurrencyId next)
        {
            next = current switch
            {
                CurrencyId.SHT => CurrencyId.ETH,
                CurrencyId.ETH => CurrencyId.BTC,
                _ => current,
            };
            return next != current;
        }

        private void EnsureStateArrays()
        {
            maxRigCount = Mathf.Max(1, maxRigCount);

            if (_rigStateLevels == null || _rigStateLevels.Length != maxRigCount)
                Array.Resize(ref _rigStateLevels, maxRigCount);
            if (_rigProgress == null || _rigProgress.Length != maxRigCount)
                Array.Resize(ref _rigProgress, maxRigCount);

            for (var i = 0; i < _rigStateLevels.Length; i++)
            {
                _rigStateLevels[i] = Mathf.Clamp(_rigStateLevels[i], 0, MaxRigLevel);
                _rigProgress[i] = Mathf.Clamp01(_rigProgress[i]);
            }
        }

        private void OnProfileChanged(long _)
        {
            RefreshAll();
        }

        private void OnHoldingChanged(CurrencyId _)
        {
            RefreshAll();
        }

        private void MarkDirty()
        {
            _dirty = true;
            if (_saveTimer <= 0f)
                _saveTimer = 2f;
        }

        private void SaveToStorage()
        {
            EnsureStateArrays();
            var data = new SaveData
            {
                openedCurrency = (int)_openedCurrency,
                accumulated = _accumulated,
                rigLevels = (int[])_rigStateLevels.Clone(),
                rigProgress = (float[])_rigProgress.Clone(),
                adSpeedTimeLeft = _adSpeedTimeLeft,
                gemSpeedTimeLeft = _gemSpeedTimeLeft,
                coinIncomeTimeLeft = _coinIncomeTimeLeft,
                coinIncomeBoostUseCount = _coinIncomeBoostUseCount,
                lastSimulationUtcSeconds = GetUtcSeconds(),
            };
            _lastSimulationUtcSeconds = data.lastSimulationUtcSeconds;

            SaveStorage.SaveJson(SaveKey, data);
            SaveStorage.Flush();
            _dirty = false;
            _saveTimer = 0f;
        }

        private void LoadFromStorage()
        {
            if (!SaveStorage.TryLoadJson<SaveData>(SaveKey, out var data))
                return;

            _openedCurrency = Enum.IsDefined(typeof(CurrencyId), data.openedCurrency)
                ? (CurrencyId)data.openedCurrency
                : CurrencyId.SHT;
            _accumulated = Math.Max(0d, data.accumulated);

            if (data.rigLevels != null && data.rigLevels.Length > 0)
            {
                _rigStateLevels = new int[maxRigCount];
                Array.Copy(data.rigLevels, _rigStateLevels, Math.Min(maxRigCount, data.rigLevels.Length));
            }

            if (data.rigProgress != null && data.rigProgress.Length > 0)
            {
                _rigProgress = new float[maxRigCount];
                Array.Copy(data.rigProgress, _rigProgress, Math.Min(maxRigCount, data.rigProgress.Length));
            }

            _adSpeedTimeLeft = Mathf.Max(0f, data.adSpeedTimeLeft);
            _gemSpeedTimeLeft = Mathf.Max(0f, data.gemSpeedTimeLeft);
            _coinIncomeTimeLeft = Mathf.Max(0f, data.coinIncomeTimeLeft);
            _coinIncomeBoostUseCount = Mathf.Max(0, data.coinIncomeBoostUseCount);
            _lastSimulationUtcSeconds = data.lastSimulationUtcSeconds > 0
                ? data.lastSimulationUtcSeconds
                : GetUtcSeconds();
        }

        [ContextMenu("Debug/Reset mining save")]
        private void Debug_ResetSave()
        {
            SaveStorage.DeleteKey(SaveKey);
            SaveStorage.Flush();
        }

        private static string SafeFormat(string format, string fallback)
        {
            return string.IsNullOrEmpty(format) ? fallback : format;
        }

        private static string FormatOne(string format, string fallback, string value)
        {
            return string.Format(SafeFormat(format, fallback), value);
        }

        private static string FormatTwo(string format, string fallback, string a, string b)
        {
            return string.Format(SafeFormat(format, fallback), a, b);
        }

        private static long GetUtcSeconds()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        private static string FormatAmount(double value, CurrencyId currency)
        {
            value = Math.Max(0d, value);
            return currency == CurrencyId.BTC
                ? value.ToString(value < 1d ? "0.##" : "N0", CultureInfo.InvariantCulture).Replace(",", ".")
                : value.ToString("N0", CultureInfo.InvariantCulture).Replace(",", ".");
        }

        private static string FormatRubles(long value)
        {
            return Math.Max(0, value)
                .ToString("N0", CultureInfo.InvariantCulture)
                .Replace(",", ".");
        }

        private static string FormatBoostCostCurrency(BoostCostCurrency currency)
        {
            return currency == BoostCostCurrency.Rubles ? "Р" : currency.ToString();
        }

        private static string FormatMultiplier(float value)
        {
            return Math.Max(1f, value).ToString("0.#", CultureInfo.InvariantCulture);
        }

        private static string FormatTimer(float seconds)
        {
            var total = Mathf.CeilToInt(Mathf.Max(0f, seconds));
            var hours = total / 3600;
            var minutes = total % 3600 / 60;
            var secs = total % 60;
            return hours > 0 ? $"{hours:00}:{minutes:00}:{secs:00}" : $"{minutes:00}:{secs:00}";
        }

        private static string FormatDuration(float seconds)
        {
            if (seconds >= 3600f)
                return $"{Mathf.RoundToInt(seconds / 3600f)}ч";
            if (seconds >= 60f)
                return $"{Mathf.RoundToInt(seconds / 60f)}м";
            return $"{Mathf.RoundToInt(seconds)}с";
        }
    }
}
