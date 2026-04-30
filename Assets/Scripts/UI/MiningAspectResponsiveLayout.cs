using UnityEngine;
using UnityEngine.UI;

namespace TraidingIDLE.UI
{
    /// <summary>
    /// Переключает параметры вертикального layout майнинга между «широким» (например 16:9) и узким/высоким viewport,
    /// чтобы карточки бустов не раздувались через Content Size Fitter и не вылезали ниже рамки.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class MiningAspectResponsiveLayout : MonoBehaviour
    {
        [SerializeField, Tooltip("При width/height меньше этого включается узкий режим.")]
        private float wideLayoutMinAspectRatio = 1.2f;

        [Header("Flexible height (VerticalLayoutGroup)")]
        [SerializeField] private LayoutElement topBox = null!;
        [SerializeField] private LayoutElement middleBox = null!;
        [SerializeField] private LayoutElement bottomBox = null!;

        [SerializeField] private float wideTopFlex = 1.08f;
        [SerializeField] private float wideMiddleFlex = 5f;
        [SerializeField] private float wideBottomFlex = 3.3f;

        [SerializeField] private float narrowTopFlex = 1.08f;
        [SerializeField] private float narrowMiddleFlex = 3.5f;
        [SerializeField] private float narrowBottomFlex = 4.8f;

        [Header("Горизонтальный скролл ригов")]
        [SerializeField] private HorizontalScrollContentLayoutSize rigScrollContentLayout = null!;

        [Header("Ряд карточек бустов")]
        [SerializeField] private RectTransform boostCardStrip = null!;
        [SerializeField] private HorizontalLayoutGroup boostCardRowLayout = null!;

        [Header("Нижний ряд: ферма | статистика (flexible width)")]
        [SerializeField] private LayoutElement farmBottomColumn = null!;
        [SerializeField] private LayoutElement statsBottomColumn = null!;
        [SerializeField] private float farmColumnWideFlexibleWidth = 1.8f;
        [SerializeField] private float statsColumnWideFlexibleWidth = 1f;
        [SerializeField] private float farmColumnNarrowFlexibleWidth = 5f;
        [SerializeField] private float statsColumnNarrowFlexibleWidth = 0.85f;

        [SerializeField]
        private RectTransform[] boostCardRoots =
        {
            null!, null!, null!, null!
        };

        private bool _narrow;
        private Vector2 _lastCheckedSize;
        private bool _hasApplied;

        private void OnEnable()
        {
            _hasApplied = false;
            _lastCheckedSize = Vector2.zero;
            ApplyNow();
        }

        private void OnDisable()
        {
            _hasApplied = false;
        }

        private void OnRectTransformDimensionsChange()
        {
            ApplyNow();
        }

        private void LateUpdate()
        {
            var rt = (RectTransform)transform;
            var sz = rt.rect.size;

            bool sizeChanged =
                !_hasApplied ||
                Mathf.Abs(sz.x - _lastCheckedSize.x) >= 1f ||
                Mathf.Abs(sz.y - _lastCheckedSize.y) >= 1f;

            if (!sizeChanged)
                return;

            ApplyNow();
        }

        private void ApplyNow()
        {
            var rt = (RectTransform)transform;
            var sz = rt.rect.size;
            if (sz.y < 2f || sz.x < 2f)
                return;

            float aspect = sz.x / sz.y;
            bool narrow = aspect < wideLayoutMinAspectRatio;

            if (_hasApplied &&
                narrow == _narrow &&
                Mathf.Abs(sz.x - _lastCheckedSize.x) < 1f &&
                Mathf.Abs(sz.y - _lastCheckedSize.y) < 1f)
                return;

            _hasApplied = true;
            _narrow = narrow;
            _lastCheckedSize = sz;

            if (narrow)
                ApplyNarrowWeights();
            else
                ApplyWideWeights();

            if (rigScrollContentLayout != null)
                rigScrollContentLayout.Refresh();

            if (boostCardStrip != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(boostCardStrip);

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }

        private void ApplyWideWeights()
        {
            SetFlex(topBox, wideTopFlex);
            SetFlex(middleBox, wideMiddleFlex);
            SetFlex(bottomBox, wideBottomFlex);
            SetColumnFlex(farmBottomColumn, farmColumnWideFlexibleWidth);
            SetColumnFlex(statsBottomColumn, statsColumnWideFlexibleWidth);
            if (boostCardRowLayout != null)
                boostCardRowLayout.childForceExpandWidth = false;
            foreach (var root in boostCardRoots)
                ApplyWideBoost(root);
        }

        private void ApplyNarrowWeights()
        {
            SetFlex(topBox, narrowTopFlex);
            SetFlex(middleBox, narrowMiddleFlex);
            SetFlex(bottomBox, narrowBottomFlex);
            SetColumnFlex(farmBottomColumn, farmColumnNarrowFlexibleWidth);
            SetColumnFlex(statsBottomColumn, statsColumnNarrowFlexibleWidth);
            if (boostCardRowLayout != null)
                boostCardRowLayout.childForceExpandWidth = true;
            foreach (var root in boostCardRoots)
                ApplyNarrowBoost(root);
        }

        private static void SetFlex(LayoutElement le, float h)
        {
            if (le != null)
                le.flexibleHeight = h;
        }

        private static void SetColumnFlex(LayoutElement le, float w)
        {
            if (le != null)
                le.flexibleWidth = w;
        }

        private static void ApplyNarrowBoost(RectTransform root)
        {
            if (root == null)
                return;

            var csf = root.GetComponent<ContentSizeFitter>();
            if (csf != null)
            {
                csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                csf.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            }

            var le = root.GetComponent<LayoutElement>();
            if (le != null)
            {
                le.minWidth = 64f;
                le.preferredWidth = 0f;
                le.flexibleWidth = 1f;
            }
        }

        private static void ApplyWideBoost(RectTransform root)
        {
            if (root == null)
                return;

            var csf = root.GetComponent<ContentSizeFitter>();
            if (csf != null)
            {
                csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }

            var le = root.GetComponent<LayoutElement>();
            if (le != null)
            {
                le.minWidth = -1f;
                le.preferredWidth = 228f;
                le.flexibleWidth = -1f;
            }
        }
    }
}
