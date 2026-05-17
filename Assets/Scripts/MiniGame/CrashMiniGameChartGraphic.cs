using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TraidingIDLE.MiniGame
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CrashMiniGameChartGraphic : Graphic
    {
        [System.Serializable]
        public sealed class ThresholdLine
        {
            [Min(1f)] public float multiplier = 2f;
            public Color color = new(0.1f, 1f, 0.45f, 1f);
            public TMP_Text labelText;
        }

        public struct Sample
        {
            public float time01;
            public float multiplier;
        }

        [SerializeField, Min(1f)] private float lineThickness = 3f;
        [SerializeField] private Color upLineColor = new(0.1f, 1f, 0.45f, 1f);
        [SerializeField] private Color downLineColor = new(1f, 0.18f, 0.18f, 1f);
        [SerializeField] private Color fillColor = new(0.05f, 0.8f, 0.25f, 0.18f);
        [SerializeField, Min(1f)] private float thresholdThickness = 2f;
        [SerializeField, Min(1f)] private float dashLength = 8f;
        [SerializeField, Min(0f)] private float dashGap = 5f;
        [SerializeField] private bool autoCreateThresholdLabels = true;
        [SerializeField, Min(1f)] private float thresholdLabelFontSize = 22f;
        [SerializeField] private Vector2 thresholdLabelOffset = new(8f, 0f);
        [SerializeField] private ThresholdLine[] thresholds =
        {
            new() { multiplier = 2f, color = new Color(0.2f, 1f, 0.35f, 1f) },
            new() { multiplier = 5f, color = new Color(0.05f, 0.65f, 1f, 1f) },
            new() { multiplier = 10f, color = new Color(1f, 0.7f, 0.05f, 1f) },
            new() { multiplier = 15f, color = new Color(0.9f, 0.15f, 1f, 1f) },
        };

        [Header("Current value")]
        [SerializeField] private bool autoCreateCurrentValueLabel = true;
        [SerializeField] private TMP_Text currentValueText;
        [SerializeField] private string currentValueFormat = "x{0:0.00}";
        [SerializeField, Min(1f)] private float currentValueFontSize = 18f;
        [SerializeField] private Color currentValueColor = Color.white;
        [SerializeField] private Vector2 currentValueOffset = new(28f, 16f);
        [SerializeField] private Vector2 currentValueSize = new(78f, 28f);

        private Sample[] _samples = System.Array.Empty<Sample>();
        private int _count;
        private float _viewMinMultiplier = 1f;
        private float _viewMaxMultiplier = 2.2f;
        private bool _showCurrentValue;
        private bool _showThresholds = true;
        private float _currentValueTime01;
        private float _currentValueMultiplier;

        protected override void Awake()
        {
            base.Awake();
            EnsureThresholdLabels();
            EnsureCurrentValueLabel();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            EnsureThresholdLabels();
            EnsureCurrentValueLabel();
            UpdateThresholdLabels();
            UpdateCurrentValueLabel();
        }

        public void Clear()
        {
            _samples = System.Array.Empty<Sample>();
            _count = 0;
            _showCurrentValue = false;
            UpdateThresholdLabels();
            UpdateCurrentValueLabel();
            SetVerticesDirty();
        }

        public void SetSamples(Sample[] samples, int count, float viewMinMultiplier, float viewMaxMultiplier)
        {
            _samples = samples ?? System.Array.Empty<Sample>();
            _count = Mathf.Clamp(count, 0, _samples.Length);
            _viewMinMultiplier = Mathf.Max(0f, viewMinMultiplier);
            _viewMaxMultiplier = Mathf.Max(_viewMinMultiplier + 0.01f, viewMaxMultiplier);
            UpdateThresholdLabels();
            UpdateCurrentValueLabel();
            SetVerticesDirty();
        }

        public void SetCurrentValue(float time01, float multiplier, bool show)
        {
            _currentValueTime01 = Mathf.Clamp01(time01);
            _currentValueMultiplier = Mathf.Max(0f, multiplier);
            _showCurrentValue = show;
            EnsureCurrentValueLabel();
            UpdateCurrentValueLabel();
        }

        public void SetThresholdMultipliers(float[] values, int count)
        {
            if (values == null || count <= 0)
                return;

            count = Mathf.Min(count, values.Length);
            EnsureThresholdCount(count);
            for (var i = 0; i < count; i++)
            {
                if (thresholds[i] == null)
                    thresholds[i] = new ThresholdLine();

                thresholds[i].multiplier = Mathf.Max(1f, values[i]);
            }

            EnsureThresholdLabels();
            UpdateThresholdLabels();
            UpdateCurrentValueLabel();
            SetVerticesDirty();
        }

        public void SetThresholdsVisible(bool visible)
        {
            if (_showThresholds == visible)
                return;

            _showThresholds = visible;
            UpdateThresholdLabels();
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f)
                return;

            DrawThresholds(vh, rect);
            if (_count < 2)
                return;

            DrawFill(vh, rect);

            for (var i = 1; i < _count; i++)
            {
                var a = ToRectPoint(rect, _samples[i - 1]);
                var b = ToRectPoint(rect, _samples[i]);
                var color32 = (Color32)(_samples[i].multiplier >= _samples[i - 1].multiplier
                    ? upLineColor
                    : downLineColor);
                AddSegment(vh, a, b, lineThickness, color32);
            }
        }

        private void DrawThresholds(VertexHelper vh, Rect rect)
        {
            if (!_showThresholds || thresholds == null)
                return;

            for (var i = 0; i < thresholds.Length; i++)
            {
                var threshold = thresholds[i];
                if (threshold == null || !TryGetThresholdY(rect, threshold.multiplier, out var y))
                    continue;

                AddDashedLine(vh, rect.xMin, rect.xMax, y, thresholdThickness, dashLength, dashGap, threshold.color);
            }
        }

        private void DrawFill(VertexHelper vh, Rect rect)
        {
            if (_count < 2)
                return;

            var color32 = (Color32)fillColor;
            var bottom = rect.yMin;
            for (var i = 1; i < _count; i++)
            {
                var a = ToRectPoint(rect, _samples[i - 1]);
                var b = ToRectPoint(rect, _samples[i]);
                var index = vh.currentVertCount;
                vh.AddVert(new Vector2(a.x, bottom), color32, Vector2.zero);
                vh.AddVert(a, color32, Vector2.zero);
                vh.AddVert(b, color32, Vector2.zero);
                vh.AddVert(new Vector2(b.x, bottom), color32, Vector2.zero);
                vh.AddTriangle(index, index + 1, index + 2);
                vh.AddTriangle(index, index + 2, index + 3);
            }
        }

        private Vector2 ToRectPoint(Rect rect, Sample sample)
        {
            return new Vector2(
                Mathf.Lerp(rect.xMin, rect.xMax, Mathf.Clamp01(sample.time01)),
                MultiplierToY(rect, sample.multiplier));
        }

        private bool TryGetThresholdY(Rect rect, float multiplier, out float y)
        {
            y = MultiplierToY(rect, multiplier);
            return multiplier >= _viewMinMultiplier && multiplier <= _viewMaxMultiplier;
        }

        private float MultiplierToY(Rect rect, float multiplier)
        {
            var normalized = Mathf.InverseLerp(_viewMinMultiplier, _viewMaxMultiplier, Mathf.Max(0f, multiplier));
            return Mathf.Lerp(rect.yMin, rect.yMax, Mathf.Clamp01(normalized));
        }

        private void UpdateThresholdLabels()
        {
            if (thresholds == null)
                return;

            EnsureThresholdLabels();
            var rect = rectTransform.rect;
            for (var i = 0; i < thresholds.Length; i++)
            {
                var threshold = thresholds[i];
                if (threshold?.labelText == null)
                    continue;

                var y = 0f;
                var visible = _showThresholds && TryGetThresholdY(rect, threshold.multiplier, out y);
                threshold.labelText.gameObject.SetActive(visible);
                if (!visible)
                    continue;

                threshold.labelText.text = $"x{threshold.multiplier:0.#}";
                threshold.labelText.color = threshold.color;

                if (threshold.labelText.rectTransform.parent == rectTransform)
                    threshold.labelText.rectTransform.anchoredPosition = new Vector2(
                        thresholdLabelOffset.x,
                        y - rect.yMin + thresholdLabelOffset.y);
            }
        }

        private void EnsureThresholdLabels()
        {
            if (!autoCreateThresholdLabels || thresholds == null)
                return;

            for (var i = 0; i < thresholds.Length; i++)
            {
                var threshold = thresholds[i];
                if (threshold == null || threshold.labelText != null)
                    continue;

                var go = new GameObject($"ThresholdLabel_x{threshold.multiplier:0.#}", typeof(RectTransform));
                go.transform.SetParent(rectTransform, false);

                var label = go.AddComponent<TextMeshProUGUI>();
                label.raycastTarget = false;
                label.fontSize = thresholdLabelFontSize;
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.text = $"x{threshold.multiplier:0.#}";
                label.color = threshold.color;

                var rt = label.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.zero;
                rt.pivot = new Vector2(0f, 0.5f);
                rt.sizeDelta = new Vector2(80f, thresholdLabelFontSize * 1.5f);

                threshold.labelText = label;
            }
        }

        private void EnsureCurrentValueLabel()
        {
            if (currentValueText != null || !autoCreateCurrentValueLabel)
                return;

            var go = new GameObject("CurrentValueLabel", typeof(RectTransform));
            go.transform.SetParent(rectTransform, false);

            var label = go.AddComponent<TextMeshProUGUI>();
            label.raycastTarget = false;
            label.fontSize = currentValueFontSize;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.MidlineGeoAligned;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.color = currentValueColor;

            var rt = label.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = currentValueSize;

            currentValueText = label;
        }

        private void UpdateCurrentValueLabel()
        {
            if (currentValueText == null)
                return;

            var visible = _showCurrentValue && _currentValueMultiplier > 0f;
            currentValueText.gameObject.SetActive(visible);
            if (!visible)
                return;

            var rect = rectTransform.rect;
            currentValueText.text = FormatCurrentValue(_currentValueMultiplier);
            currentValueText.color = currentValueColor;
            currentValueText.fontSize = currentValueFontSize;

            var rt = currentValueText.rectTransform;
            rt.sizeDelta = currentValueSize;
            if (rt.parent != rectTransform)
                return;

            var point = ToRectPoint(rect, new Sample
            {
                time01 = _currentValueTime01,
                multiplier = _currentValueMultiplier,
            });

            var x = point.x - rect.xMin + currentValueOffset.x;
            var y = point.y - rect.yMin + currentValueOffset.y;
            var halfWidth = currentValueSize.x * 0.5f;
            var halfHeight = currentValueSize.y * 0.5f;
            x = Mathf.Clamp(x, halfWidth, Mathf.Max(halfWidth, rect.width - halfWidth));
            y = Mathf.Clamp(y, halfHeight, Mathf.Max(halfHeight, rect.height - halfHeight));
            rt.anchoredPosition = new Vector2(x, y);
        }

        private string FormatCurrentValue(float value)
        {
            if (!string.IsNullOrEmpty(currentValueFormat))
            {
                try
                {
                    return string.Format(currentValueFormat, value);
                }
                catch (System.FormatException)
                {
                }
            }

            return $"x{value:0.00}";
        }

        private void EnsureThresholdCount(int count)
        {
            if (thresholds != null && thresholds.Length == count)
                return;

            var oldThresholds = thresholds;
            thresholds = new ThresholdLine[count];
            for (var i = 0; i < count; i++)
            {
                if (oldThresholds != null && i < oldThresholds.Length && oldThresholds[i] != null)
                {
                    thresholds[i] = oldThresholds[i];
                    continue;
                }

                thresholds[i] = new ThresholdLine
                {
                    color = GetDefaultThresholdColor(i),
                };
            }
        }

        private static Color GetDefaultThresholdColor(int index)
        {
            switch (index)
            {
                case 0:
                    return new Color(0.2f, 1f, 0.35f, 1f);
                case 1:
                    return new Color(0.05f, 0.65f, 1f, 1f);
                case 2:
                    return new Color(1f, 0.7f, 0.05f, 1f);
                default:
                    return new Color(0.9f, 0.15f, 1f, 1f);
            }
        }

        private static void AddDashedLine(
            VertexHelper vh,
            float xMin,
            float xMax,
            float y,
            float thickness,
            float dash,
            float gap,
            Color32 color32)
        {
            dash = Mathf.Max(1f, dash);
            gap = Mathf.Max(0f, gap);

            var x = xMin;
            while (x < xMax)
            {
                var end = Mathf.Min(x + dash, xMax);
                AddSegment(vh, new Vector2(x, y), new Vector2(end, y), thickness, color32);
                x = end + gap;
            }
        }

        private static void AddSegment(VertexHelper vh, Vector2 a, Vector2 b, float thickness, Color32 color32)
        {
            var delta = b - a;
            if (delta.sqrMagnitude <= 0.001f)
                return;

            var normal = new Vector2(-delta.y, delta.x).normalized * (thickness * 0.5f);
            var index = vh.currentVertCount;
            vh.AddVert(a - normal, color32, Vector2.zero);
            vh.AddVert(a + normal, color32, Vector2.zero);
            vh.AddVert(b + normal, color32, Vector2.zero);
            vh.AddVert(b - normal, color32, Vector2.zero);
            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
