using System;
using System.Collections.Generic;
using TMPro;
using TraidingIDLE.Integrations;
using TraidingIDLE.Localization;
using TraidingIDLE.Player;
using TraidingIDLE.Saves;
using TraidingIDLE.UI;
using UnityEngine;
using UnityEngine.UI;

namespace TraidingIDLE.Temki
{
    public sealed class TemkiController : MonoBehaviour
    {
        [Serializable]
        private sealed class TemkaSave
        {
            public string id = "";
            public byte state;
            public long endUtc;
            public bool success;
            public long rewardRubles;
            public long activeStakeRubles;
            public int useCount;
        }

        [Serializable]
        private sealed class SaveData
        {
            public TemkaSave[] items = Array.Empty<TemkaSave>();
        }

        private enum TemkaState : byte
        {
            Ready = 0,
            Running = 1,
            Finished = 2,
        }

        private sealed class RuntimeTemka
        {
            public TemkaDefinition definition;
            public TemkaState state;
            public long endUtc;
            public bool success;
            public long rewardRubles;
            public long activeStakeRubles;
            public int useCount;
        }

        private const string SaveKey = "save.temki.v1";

        [Header("Refs")]
        [SerializeField] private PlayerProfile profile;
        [SerializeField] private TemkaCardUI cardPrefab;
        [SerializeField] private RectTransform cardsContent;
        [SerializeField] private TemkiResultDialogUI resultDialog;

        [Header("Definitions")]
        [SerializeField] private TemkaDefinition[] temki = Array.Empty<TemkaDefinition>();

        [Header("Texts")]
        [SerializeField] private string rubleSuffix = "р";
        [SerializeField] private string thousandSep = ".";

        [Header("Runtime")]
        [SerializeField, Min(0.1f)] private float refreshIntervalSeconds = 0.25f;

        private readonly List<RuntimeTemka> _runtime = new();
        private TemkaCardUI[] _cards = Array.Empty<TemkaCardUI>();
        private int _pendingDialogIndex = -1;
        private float _refreshTimer;
        private float _saveTimer;
        private bool _dirty;

        private void Awake()
        {
            ResolveReferences();
            RebuildRuntime();
            Load();
            SpawnCards();
            RefreshAll();
        }

        private void OnEnable()
        {
            SaveStorage.ExternalDataLoaded += ReloadFromExternalStorage;

            if (profile != null)
                profile.RublesChanged += OnRublesChanged;

            RefreshAll();
        }

        private void OnDisable()
        {
            SaveStorage.ExternalDataLoaded -= ReloadFromExternalStorage;

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
            _refreshTimer -= Time.unscaledDeltaTime;
            if (_refreshTimer <= 0f)
            {
                _refreshTimer = refreshIntervalSeconds;
                RefreshAll();
            }

            if (!_dirty)
                return;

            _saveTimer -= Time.unscaledDeltaTime;
            if (_saveTimer <= 0f)
                SaveNow();
        }

        private void ResolveReferences()
        {
            if (profile == null)
                profile = FindAnyObjectByType<PlayerProfile>();
            if (cardPrefab == null)
                cardPrefab = GetComponentInChildren<TemkaCardUI>(true);
            if (cardsContent == null && cardPrefab != null)
                cardsContent = cardPrefab.transform.parent as RectTransform;
            if (resultDialog == null)
                resultDialog = FindAnyObjectByType<TemkiResultDialogUI>(FindObjectsInactive.Include);
        }

        private void RebuildRuntime()
        {
            _runtime.Clear();
            if (temki == null)
                return;

            for (var i = 0; i < temki.Length; i++)
            {
                var definition = temki[i];
                if (definition == null)
                    continue;

                _runtime.Add(new RuntimeTemka
                {
                    definition = definition,
                    state = TemkaState.Ready,
                });
            }
        }

        private void SpawnCards()
        {
            if (cardsContent == null || cardPrefab == null)
            {
                _cards = Array.Empty<TemkaCardUI>();
                return;
            }

            UiTransformUtility.DestroyChildren(cardsContent);
            _cards = new TemkaCardUI[_runtime.Count];
            for (var i = 0; i < _runtime.Count; i++)
            {
                var card = Instantiate(cardPrefab, cardsContent, false);
                card.gameObject.name = $"{cardPrefab.name}_{i + 1}";
                var captured = i;
                card.Bind(() => OnCardClicked(captured));
                _cards[i] = card;
            }

            UiTransformUtility.RebuildLayout(cardsContent);
        }

        private void OnCardClicked(int index)
        {
            if (!IsValidIndex(index))
                return;

            var item = _runtime[index];
            if (item.state == TemkaState.Ready)
            {
                StartTemka(index);
                return;
            }

            if (item.state == TemkaState.Finished || item.endUtc <= UtcNow())
                ShowResult(index);
        }

        private void StartTemka(int index)
        {
            if (profile == null || !IsValidIndex(index))
                return;

            var item = _runtime[index];
            var stake = CalculateStakeRubles(item);
            if (!profile.TrySpendRubles(stake))
                return;

            item.state = TemkaState.Running;
            item.endUtc = UtcNow() + item.definition.DurationSeconds;
            item.activeStakeRubles = stake;
            item.useCount = Math.Max(0, item.useCount) + 1;
            item.success = UnityEngine.Random.value < item.definition.SuccessChance;
            item.rewardRubles = item.success
                ? ToSaturatedLong(Math.Round(stake * item.definition.RewardMultiplier))
                : 0;

            MarkDirty();
            RefreshAll();
        }

        private void ShowResult(int index)
        {
            if (!IsValidIndex(index))
                return;

            var item = _runtime[index];
            if (item.state == TemkaState.Running && item.endUtc > UtcNow())
                return;

            item.state = TemkaState.Finished;
            _pendingDialogIndex = index;
            EnsureResultDialog();

            if (resultDialog == null)
                return;

            if (item.success)
            {
                resultDialog.ShowSuccess(
                    FormatRubles(item.rewardRubles),
                    ClaimPendingReward,
                    item.rewardRubles > 0 ? RequestDoublePendingReward : null);
            }
            else
            {
                resultDialog.ShowFail(ClosePendingFail);
            }

            MarkDirty();
            RefreshAll();
        }

        private void ClaimPendingReward()
        {
            ClaimPendingReward(multiplier: 1);
        }

        private void RequestDoublePendingReward()
        {
            if (!IsValidIndex(_pendingDialogIndex))
                return;

            YandexRewardedAds.Show(YandexRewardedAds.TemkiDoubleRewardId, ClaimDoublePendingReward);
        }

        private void ClaimDoublePendingReward()
        {
            ClaimPendingReward(multiplier: 2);
        }

        private void ClaimPendingReward(int multiplier)
        {
            if (!IsValidIndex(_pendingDialogIndex))
                return;

            var item = _runtime[_pendingDialogIndex];
            if (profile != null && item.rewardRubles > 0)
                profile.AddRubles(ToSaturatedLong(item.rewardRubles * Math.Max(1d, multiplier)));

            ResetTemka(item);
            _pendingDialogIndex = -1;
            if (resultDialog != null)
                resultDialog.Hide();

            MarkDirty();
            RefreshAll();
        }

        private void ClosePendingFail()
        {
            if (!IsValidIndex(_pendingDialogIndex))
                return;

            ResetTemka(_runtime[_pendingDialogIndex]);
            _pendingDialogIndex = -1;
            if (resultDialog != null)
                resultDialog.Hide();

            MarkDirty();
            RefreshAll();
        }

        private void ResetTemka(RuntimeTemka item)
        {
            item.state = TemkaState.Ready;
            item.endUtc = 0;
            item.success = false;
            item.rewardRubles = 0;
            item.activeStakeRubles = 0;
        }

        private void RefreshAll()
        {
            var now = UtcNow();
            for (var i = 0; i < _runtime.Count; i++)
            {
                var item = _runtime[i];
                if (item.state == TemkaState.Running && item.endUtc <= now)
                {
                    item.state = TemkaState.Finished;
                    MarkDirty();
                }

                if (i >= _cards.Length || _cards[i] == null)
                    continue;

                var definition = item.definition;
                var displayStake = item.state == TemkaState.Ready
                    ? CalculateStakeRubles(item)
                    : Math.Max(0, item.activeStakeRubles);
                _cards[i].ConfigureStatic(
                    definition,
                    FormatNumber(displayStake),
                    GameTextFormatter.CountdownHours(definition.DurationSeconds));

                switch (item.state)
                {
                    case TemkaState.Running:
                        _cards[i].PresentTimer(Math.Max(0, item.endUtc - now));
                        break;
                    case TemkaState.Finished:
                        _cards[i].PresentCheck();
                        break;
                    default:
                        _cards[i].PresentRisk(profile != null && profile.Rubles >= displayStake);
                        break;
                }
            }
        }

        private void EnsureResultDialog()
        {
            if (resultDialog != null)
                return;

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
                canvas = FindAnyObjectByType<Canvas>(FindObjectsInactive.Include);
            if (canvas == null)
                return;

            resultDialog = CreateFallbackDialog(canvas.transform);
        }

        private TemkiResultDialogUI CreateFallbackDialog(Transform parent)
        {
            var root = new GameObject("Temki_Result_Dialog", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(TemkiResultDialogUI));
            root.transform.SetParent(parent, false);
            var rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

            var panel = CreatePanel(root.transform, "Panel");
            var success = CreatePanel(panel.transform, "Success");
            var fail = CreatePanel(panel.transform, "Fail");

            var rewardText = CreateText(success.transform, "RewardText", "0р", 46, TextAlignmentOptions.Center);
            var claim = CreateButton(success.transform, "ClaimButton", LocalizationManager.Tr("common.claim", "Забрать"));
            var doubleAd = CreateButton(success.transform, "DoubleAdButton", LocalizationManager.Tr("common.watch_ad", "Удвоить за рекламу"));
            var close = CreateButton(fail.transform, "CloseButton", LocalizationManager.Tr("common.close", "Закрыть"));
            CreateText(fail.transform, "FailText", LocalizationManager.Tr("temki.fail", "Темка не стрельнула"), 38, TextAlignmentOptions.Center);

            var dialog = root.GetComponent<TemkiResultDialogUI>();
            dialog.Initialize(success, fail, rewardText, claim, doubleAd, close);
            return dialog;
        }

        private static GameObject CreatePanel(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(620f, 360f);
            var image = go.GetComponent<Image>();
            image.color = new Color(0.06f, 0.08f, 0.14f, 0.98f);
            var layout = go.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 20f;
            layout.padding = new RectOffset(32, 32, 32, 32);
            var fitter = go.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return go;
        }

        private static TMP_Text CreateText(Transform parent, string name, string text, int size, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = alignment;
            tmp.color = Color.white;
            var layout = go.GetComponent<LayoutElement>();
            layout.minHeight = 72f;
            layout.preferredHeight = 72f;
            return tmp;
        }

        private static Button CreateButton(Transform parent, string name, string label)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.16f, 0.45f, 0.32f, 1f);
            var layout = go.GetComponent<LayoutElement>();
            layout.minWidth = 420f;
            layout.minHeight = 72f;
            layout.preferredWidth = 420f;
            layout.preferredHeight = 72f;
            CreateText(go.transform, "Text", label, 32, TextAlignmentOptions.Center);
            return go.GetComponent<Button>();
        }

        private bool Load()
        {
            if (!SaveStorage.TryLoadJson(SaveKey, out SaveData data) || data.items == null)
                return false;

            var map = new Dictionary<string, TemkaSave>(StringComparer.Ordinal);
            for (var i = 0; i < data.items.Length; i++)
            {
                var save = data.items[i];
                if (save != null && !string.IsNullOrWhiteSpace(save.id))
                    map[save.id] = save;
            }

            for (var i = 0; i < _runtime.Count; i++)
            {
                var item = _runtime[i];
                if (!map.TryGetValue(item.definition.SaveId, out var save))
                    continue;

                item.state = Enum.IsDefined(typeof(TemkaState), save.state) ? (TemkaState)save.state : TemkaState.Ready;
                item.endUtc = Math.Max(0, save.endUtc);
                item.success = save.success;
                item.rewardRubles = Math.Max(0, save.rewardRubles);
                item.activeStakeRubles = Math.Max(0, save.activeStakeRubles);
                item.useCount = Math.Max(0, save.useCount);
            }

            return true;
        }

        private void ReloadFromExternalStorage()
        {
            RebuildRuntime();
            Load();
            _dirty = false;
            _saveTimer = 0f;
            RefreshAll();
        }

        private void SaveNow()
        {
            var items = new TemkaSave[_runtime.Count];
            for (var i = 0; i < _runtime.Count; i++)
            {
                var item = _runtime[i];
                items[i] = new TemkaSave
                {
                    id = item.definition.SaveId,
                    state = (byte)item.state,
                    endUtc = item.endUtc,
                    success = item.success,
                    rewardRubles = item.rewardRubles,
                    activeStakeRubles = item.activeStakeRubles,
                    useCount = item.useCount,
                };
            }

            SaveStorage.SaveJson(SaveKey, new SaveData { items = items });
            SaveStorage.Flush();
            _dirty = false;
            _saveTimer = 0f;
        }

        private void MarkDirty()
        {
            _dirty = true;
            if (_saveTimer <= 0f)
                _saveTimer = 2f;
        }

        private void OnRublesChanged(long _) => RefreshAll();

        private bool IsValidIndex(int index) => index >= 0 && index < _runtime.Count;

        private string FormatRubles(long value) => $"{FormatNumber(value)}{rubleSuffix}";

        private string FormatNumber(long value) => GameTextFormatter.WholeNumber(value, thousandSep);

        private static long CalculateStakeRubles(RuntimeTemka item)
        {
            if (item?.definition == null)
                return 0;

            var baseStake = item.definition.StakeRubles;
            if (baseStake <= 0)
                return 0;

            var multiplier = Math.Pow(item.definition.StakeGrowthPerUse, Math.Max(0, item.useCount));
            return ToSaturatedLong(RoundToReadableMoney(baseStake * multiplier));
        }

        private static double RoundToReadableMoney(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0d)
                return 0d;

            var step = value switch
            {
                < 100_000d => 5_000d,
                < 1_000_000d => 25_000d,
                < 10_000_000d => 100_000d,
                < 100_000_000d => 500_000d,
                < 1_000_000_000d => 2_500_000d,
                _ => 10_000_000d,
            };

            return Math.Max(step, Math.Round(value / step, MidpointRounding.AwayFromZero) * step);
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
    }
}
