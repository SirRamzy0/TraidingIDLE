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
        [SerializeField, Min(1f)] private float wickThickness = 2f;
        [SerializeField, Range(0.1f, 1f)] private float bodyWidth01 = 0.65f; // of slot width
        [SerializeField] private Color upColor = new(0.25f, 0.95f, 0.55f, 1f);
        [SerializeField] private Color downColor = new(0.95f, 0.35f, 0.35f, 1f);

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

                // Wick
                AddQuad(vh,
                    new Vector2(xCenter - wickHalf, lowY),
                    new Vector2(xCenter + wickHalf, highY),
                    col);

                // Body
                var y0 = Mathf.Min(openY, closeY);
                var y1 = Mathf.Max(openY, closeY);

                // If open==close make a small body so it's visible.
                if (Mathf.Abs(y1 - y0) < 1f)
                {
                    y0 -= 0.5f;
                    y1 += 0.5f;
                }

                AddQuad(vh,
                    new Vector2(xCenter - bodyHalf, y0),
                    new Vector2(xCenter + bodyHalf, y1),
                    col);
            }
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

