using System;
using UnityEngine;
using UnityEngine.UI;

namespace TraidingIDLE.UI.Charts
{
    public sealed class PriceChartAxisLabels : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] private PriceChartBoard board = null!;

        [Header("UI")]
        [SerializeField] private RectTransform labelsRoot = null!;
        [SerializeField, Min(2)] private int labelsCount = 5;
        [SerializeField] private Color color = new(1f, 1f, 1f, 0.85f);
        [SerializeField, Min(8)] private int fontSize = 18;
        [SerializeField] private string numberFormat = "N0";

        [Header("Ticks")]
        [SerializeField, Min(1f)] private float tickStep = 5f;

        private Text[] _labels = Array.Empty<Text>();

        private void Awake()
        {
            if (board == null)
                board = GetComponentInParent<PriceChartBoard>();
            if (labelsRoot == null)
                labelsRoot = transform as RectTransform;
        }

        private void OnEnable()
        {
            if (board != null)
                board.ViewportChanged += OnViewportChanged;

            Rebuild();
            if (board != null && board.HasViewport)
                Apply(board.ViewMin, board.ViewMax);
        }

        private void OnDisable()
        {
            if (board != null)
                board.ViewportChanged -= OnViewportChanged;
        }

        private void OnValidate()
        {
            labelsCount = Mathf.Max(2, labelsCount);
            tickStep = Mathf.Max(1f, tickStep);
        }

        private void OnViewportChanged(float min, float max) => Apply(min, max);

        private void Rebuild()
        {
            if (labelsRoot == null)
                return;

            labelsCount = Mathf.Max(2, labelsCount);

            // Ensure pool
            if (_labels.Length != labelsCount)
            {
                // destroy old
                for (var i = 0; i < _labels.Length; i++)
                {
                    if (_labels[i] != null)
                        Destroy(_labels[i].gameObject);
                }

                _labels = new Text[labelsCount];

                Font font;
                try
                {
                    // Unity 6+: Arial.ttf is not a valid built-in font anymore.
                    font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
                catch
                {
                    font = null;
                }

                for (var i = 0; i < labelsCount; i++)
                {
                    var go = new GameObject($"PriceLabel_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                    go.transform.SetParent(labelsRoot, false);
                    var rt = (RectTransform)go.transform;
                    rt.anchorMin = new Vector2(0f, 0f);
                    rt.anchorMax = new Vector2(0f, 0f);
                    rt.pivot = new Vector2(0f, 0.5f);

                    var t = go.GetComponent<Text>();
                    t.font = font;
                    t.fontSize = fontSize;
                    t.color = color;
                    t.alignment = TextAnchor.MiddleLeft;
                    t.raycastTarget = false;
                    _labels[i] = t;
                }
            }
        }

        private void Apply(float min, float max)
        {
            if (_labels.Length == 0 || labelsRoot == null)
                return;

            if (max <= min + 0.000001f)
                return;

            var h = labelsRoot.rect.height;
            if (h <= 0f)
                return;

            tickStep = Mathf.Max(1f, tickStep);

            var niceMin = FloorToStep(min, tickStep);
            var niceMax = CeilToStep(max, tickStep);
            if (niceMax <= niceMin + tickStep * 0.5f)
                niceMax = niceMin + tickStep;

            var span = Mathf.Max(tickStep, niceMax - niceMin);
            var step = ComputeNiceStep(span, _labels.Length - 1, tickStep);

            var start = FloorToStep(niceMin, step);
            var end = CeilToStep(niceMax, step);

            var requiredSpan = step * (_labels.Length - 1);
            if (end - start < requiredSpan - 0.0001f)
                end = start + requiredSpan;

            for (var i = 0; i < _labels.Length; i++)
            {
                var t01 = (float)i / (_labels.Length - 1);
                var v = Mathf.Lerp(start, end, t01);
                v = RoundToStep(v, tickStep);

                var label = _labels[i];
                if (label == null)
                    continue;

                label.fontSize = fontSize;
                label.color = color;
                label.text = v.ToString(numberFormat);

                var rt = (RectTransform)label.transform;
                rt.anchoredPosition = new Vector2(0f, t01 * h);
                rt.sizeDelta = new Vector2(220f, 28f);
            }
        }

        private static float ComputeNiceStep(float span, int divisions, float baseStep)
        {
            divisions = Mathf.Max(1, divisions);
            var raw = span / divisions;

            var k = Mathf.CeilToInt(raw / baseStep);
            k = Mathf.Max(1, k);
            return k * baseStep;
        }

        private static float FloorToStep(float v, float step)
        {
            return Mathf.Floor(v / step) * step;
        }

        private static float CeilToStep(float v, float step)
        {
            return Mathf.Ceil(v / step) * step;
        }

        private static float RoundToStep(float v, float step)
        {
            return Mathf.Round(v / step) * step;
        }
    }
}

