using UnityEngine;
using UnityEngine.UI;

namespace TraidingIDLE.UI.Charts
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UICandlestickChartGraphic : Graphic
    {
        public struct Candle01
        {
            public float open01;
            public float high01;
            public float low01;
            public float close01;
        }

        [Header("Style")]
        [SerializeField, Min(1f)] private float wickThickness = 1.6f;
        [SerializeField, Range(0.1f, 1f)] private float bodyWidth01 = 0.62f; // of slot width
        [SerializeField] private Color upColor = new(0.25f, 0.95f, 0.55f, 1f);
        [SerializeField] private Color downColor = new(0.95f, 0.35f, 0.35f, 1f);

        [Header("Pixel readability")]
        [SerializeField, Min(1f)] private float minBodyHeightPixels = 4f;
        [SerializeField, Min(1f)] private float minWickHeightPixels = 8f;
        [SerializeField, Min(0f)] private float minWickOutsideBodyPixels = 1.5f;

        private Candle01[] _candles = new Candle01[0];
        private int _count;

        public void SetCandles01(Candle01[] candles, int count)
        {
            _candles = candles ?? new Candle01[0];
            _count = Mathf.Clamp(count, 0, _candles.Length);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_count <= 0)
                return;

            var rect = rectTransform.rect;
            var w = rect.width;
            var h = rect.height;
            if (w <= 0f || h <= 0f)
                return;

            var slot = w / _count;
            var bodyW = slot * bodyWidth01;
            var bodyHalf = bodyW * 0.5f;
            var wickHalf = wickThickness * 0.5f;

            for (var i = 0; i < _count; i++)
            {
                var c = _candles[i];
                var xCenter = rect.xMin + slot * (i + 0.5f);

                var openY = rect.yMin + Mathf.Clamp01(c.open01) * h;
                var highY = rect.yMin + Mathf.Clamp01(c.high01) * h;
                var lowY = rect.yMin + Mathf.Clamp01(c.low01) * h;
                var closeY = rect.yMin + Mathf.Clamp01(c.close01) * h;

                var isUp = closeY >= openY;
                var col = (Color32)(isUp ? upColor : downColor);

                var y0 = Mathf.Min(openY, closeY);
                var y1 = Mathf.Max(openY, closeY);

                var bodyHeight = Mathf.Abs(closeY - openY);
                var readableBodyHeight = GetReadableBodyHeight(bodyHeight, h);
                ExpandSegmentToHeight(ref y0, ref y1, readableBodyHeight, rect.yMin, rect.yMax);

                var wickLow = Mathf.Min(lowY, y0 - minWickOutsideBodyPixels);
                var wickHigh = Mathf.Max(highY, y1 + minWickOutsideBodyPixels);
                var wickHeight = Mathf.Abs(highY - lowY);
                var readableWickHeight = GetReadableWickHeight(wickHeight, readableBodyHeight, h);
                ExpandSegmentToHeight(ref wickLow, ref wickHigh, readableWickHeight, rect.yMin, rect.yMax);

                // Wick
                AddQuad(vh,
                    new Vector2(xCenter - wickHalf, wickLow),
                    new Vector2(xCenter + wickHalf, wickHigh),
                    col);

                // Body
                AddQuad(vh,
                    new Vector2(xCenter - bodyHalf, y0),
                    new Vector2(xCenter + bodyHalf, y1),
                    col);
            }
        }

        private static void ExpandSegmentToHeight(ref float y0, ref float y1, float minHeight, float minY, float maxY)
        {
            minHeight = Mathf.Max(1f, Mathf.Min(minHeight, maxY - minY));
            if (y1 - y0 >= minHeight)
            {
                y0 = Mathf.Clamp(y0, minY, maxY);
                y1 = Mathf.Clamp(y1, minY, maxY);
                return;
            }

            var center = (y0 + y1) * 0.5f;
            var half = minHeight * 0.5f;
            y0 = center - half;
            y1 = center + half;

            if (y0 < minY)
            {
                y1 += minY - y0;
                y0 = minY;
            }

            if (y1 > maxY)
            {
                y0 -= y1 - maxY;
                y1 = maxY;
            }

            y0 = Mathf.Clamp(y0, minY, maxY);
            y1 = Mathf.Clamp(y1, minY, maxY);
        }

        private float GetReadableBodyHeight(float actualHeight, float chartHeight)
        {
            var maxReadable = Mathf.Max(1f, chartHeight * 0.16f);
            var minHeight = Mathf.Min(minBodyHeightPixels, maxReadable);
            return Mathf.Clamp(Mathf.Max(actualHeight, minHeight), 1f, maxReadable);
        }

        private float GetReadableWickHeight(float actualHeight, float bodyHeight, float chartHeight)
        {
            var maxReadable = Mathf.Max(bodyHeight + minWickOutsideBodyPixels * 2f, chartHeight * 0.24f);
            var minHeight = Mathf.Min(minWickHeightPixels, maxReadable);
            var target = Mathf.Max(actualHeight, bodyHeight + minWickOutsideBodyPixels * 2f, minHeight);
            return Mathf.Clamp(target, bodyHeight, maxReadable);
        }

        private static void AddQuad(VertexHelper vh, Vector2 min, Vector2 max, Color32 col)
        {
            var idx = vh.currentVertCount;
            vh.AddVert(new Vector2(min.x, min.y), col, Vector2.zero);
            vh.AddVert(new Vector2(min.x, max.y), col, Vector2.zero);
            vh.AddVert(new Vector2(max.x, max.y), col, Vector2.zero);
            vh.AddVert(new Vector2(max.x, min.y), col, Vector2.zero);
            vh.AddTriangle(idx + 0, idx + 1, idx + 2);
            vh.AddTriangle(idx + 0, idx + 2, idx + 3);
        }
    }
}
