using UnityEngine;
using UnityEngine.UI;

namespace TraidingIDLE.UI
{
    /// <summary>
    /// Replaces Content Size Fitter for horizontal scroll content: sets width/height from
    /// <see cref="HorizontalLayoutGroup"/> (or other ILayoutElement children) without CSF ↔ layout cycles.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class HorizontalScrollContentLayoutSize : MonoBehaviour
    {
        [SerializeField] private bool updateHeightFromLayout = true;

        private RectTransform Rect => (RectTransform)transform;

        private void OnEnable()
        {
            Refresh();
        }

        public void Refresh()
        {
            var rt = Rect;
            var scroll = rt.GetComponentInParent<ScrollRect>();
            Canvas.ForceUpdateCanvases();
            if (updateHeightFromLayout)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            else if (scroll != null && scroll.viewport != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.viewport);
                var vh = scroll.viewport.rect.height;
                if (vh > 0.01f)
                    rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, vh);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            var w = LayoutUtility.GetPreferredWidth(rt);
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, w);
            if (updateHeightFromLayout)
            {
                var h = LayoutUtility.GetPreferredHeight(rt);
                rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h);
            }
        }
    }
}
