using UnityEngine;
using UnityEngine.UI;

namespace TraidingIDLE.UI.Charts
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UIChartGridGraphic : Graphic
    {
        [Header("Source")]
        [SerializeField] private PriceChartBoard board = null!;

        [Header("Grid")]
        [SerializeField, Min(0)] private int horizontalLines = 5;
        [SerializeField, Min(0.5f)] private float gridThickness = 1f;
        [SerializeField] private Color gridColor = new(1f, 1f, 1f, 0.10f);

        [Header("Current price line")]
        [SerializeField] private bool showCurrentPriceLine = true;
        [SerializeField, Min(0.5f)] private float currentPriceLineThickness = 1.5f;
        [SerializeField] private Color currentPriceLineColor = new(0.20f, 0.95f, 0.55f, 0.85f);
        [SerializeField] private bool dashedCurrentPriceLine = true;
        [SerializeField, Min(1f)] private float dashLength = 10f;
        [SerializeField, Min(1f)] private float dashGap = 6f;

        [Header("Pixel stability")]
        [SerializeField] private bool snapLinesToPixels = true;
        [SerializeField, Min(1f)] private float minVisibleThickness = 1.25f;

        private float _viewMin;
        private float _viewMax;
        private float _currentPrice;
        private bool _hasViewport;

        protected override void Awake()
        {
            base.Awake();

            if (board == null)
                board = GetComponentInParent<PriceChartBoard>();
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            if (board != null)
            {
                board.ViewportChanged += OnViewportChanged;
                board.CurrentPriceChanged += OnCurrentPriceChanged;

                if (board.HasViewport)
                    OnViewportChanged(board.ViewMin, board.ViewMax);

                OnCurrentPriceChanged(board.CurrentPrice);
            }
        }

        protected override void OnDisable()
        {
            if (board != null)
            {
                board.ViewportChanged -= OnViewportChanged;
                board.CurrentPriceChanged -= OnCurrentPriceChanged;
            }

            base.OnDisable();
        }

        private void OnViewportChanged(float min, float max)
        {
            _viewMin = min;
            _viewMax = max;
            _hasViewport = true;
            SetVerticesDirty();
        }

        private void OnCurrentPriceChanged(float price)
        {
            _currentPrice = price;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            var rect = rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f)
                return;

            DrawGrid(vh, rect);
            DrawCurrentPrice(vh, rect);
        }

        private void DrawGrid(VertexHelper vh, Rect rect)
        {
            if (horizontalLines <= 0)
                return;

            var col = (Color32)gridColor;
            var count = Mathf.Max(1, horizontalLines);
            for (var i = 0; i < count; i++)
            {
                var t = count == 1 ? 0.5f : (float)i / (count - 1);
                var y = Mathf.Lerp(rect.yMin, rect.yMax, t);
                AddLine(vh, rect.xMin, rect.xMax, y, gridThickness, col);
            }
        }

        private void DrawCurrentPrice(VertexHelper vh, Rect rect)
        {
            if (!showCurrentPriceLine || !_hasViewport || _viewMax <= _viewMin + 0.000001f)
                return;

            var y01 = Mathf.InverseLerp(_viewMin, _viewMax, _currentPrice);
            y01 = Mathf.Clamp01(y01);

            var y = Mathf.Lerp(rect.yMin, rect.yMax, y01);
            var col = (Color32)currentPriceLineColor;

            if (!dashedCurrentPriceLine)
            {
                AddLine(vh, rect.xMin, rect.xMax, y, currentPriceLineThickness, col);
                return;
            }

            var x = rect.xMin;
            while (x < rect.xMax)
            {
                var x2 = Mathf.Min(rect.xMax, x + dashLength);
                AddLine(vh, x, x2, y, currentPriceLineThickness, col);
                x += dashLength + dashGap;
            }
        }

        private void AddLine(VertexHelper vh, float x0, float x1, float y, float thickness, Color32 col)
        {
            if (snapLinesToPixels)
            {
                y = SnapToPixelCenter(y);
                x0 = SnapToPixelEdge(x0);
                x1 = SnapToPixelEdge(x1);
            }

            thickness = Mathf.Max(minVisibleThickness, thickness);
            var half = thickness * 0.5f;
            var idx = vh.currentVertCount;

            vh.AddVert(new Vector2(x0, y - half), col, Vector2.zero);
            vh.AddVert(new Vector2(x0, y + half), col, Vector2.zero);
            vh.AddVert(new Vector2(x1, y + half), col, Vector2.zero);
            vh.AddVert(new Vector2(x1, y - half), col, Vector2.zero);

            vh.AddTriangle(idx + 0, idx + 1, idx + 2);
            vh.AddTriangle(idx + 0, idx + 2, idx + 3);
        }

        private float SnapToPixelCenter(float value)
        {
            var ppu = GetPixelScale();
            return (Mathf.Round(value * ppu) + 0.5f) / ppu;
        }

        private float SnapToPixelEdge(float value)
        {
            var ppu = GetPixelScale();
            return Mathf.Round(value * ppu) / ppu;
        }

        private float GetPixelScale()
        {
            return canvas != null ? Mathf.Max(1f, canvas.scaleFactor) : 1f;
        }
    }
}

