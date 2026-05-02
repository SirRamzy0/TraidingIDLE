using UnityEngine;
using UnityEngine.UI;

namespace TraidingIDLE.UI
{
    public static class UiTransformUtility
    {
        public static void DestroyChildren(Transform parent)
        {
            if (parent == null)
                return;

            for (var i = parent.childCount - 1; i >= 0; i--)
                Object.Destroy(parent.GetChild(i).gameObject);
        }

        public static void RebuildLayout(RectTransform transform)
        {
            if (transform == null)
                return;

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(transform);
        }
    }
}
