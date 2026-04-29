using System.Collections;
using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace TraidingIDLE.UI
{
    public sealed class MenuSelectionHighlight : MonoBehaviour
    {
        [System.Serializable]
        private struct MenuItem
        {
            public Button button;
            public RectTransform highlightTarget;
            public GameObject tabRoot;
        }

        [Header("Items (in display order)")]
        [SerializeField] private MenuItem[] items = new MenuItem[0];

        [Header("Highlight (the green plate)")]
        [SerializeField] private RectTransform highlight = null!;
        [SerializeField] private bool matchTargetSize = true;
        [SerializeField] private Vector2 sizePadding = Vector2.zero;

        [Header("Animation")]
        [SerializeField, Min(0f)] private float moveDurationSeconds = 0.18f;
        [SerializeField] private AnimationCurve moveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private bool unscaledTime = true;

        [Header("Startup")]
        [SerializeField, Min(0)] private int initialIndex = 0;
        [SerializeField] private bool snapOnEnable = true;
        [Tooltip("Layouts often settle after OnEnable. If true, initial snap happens next frame.")]
        [SerializeField] private bool delayInitialSnapByOneFrame = true;

        private int _currentIndex = -1;
        private Coroutine? _moveRoutine;
        private UnityAction[]? _cachedClickHandlers;
        private Coroutine? _initialSnapRoutine;

        public int CurrentIndex => _currentIndex;
        public event Action<int>? SelectionChanged;

        private void OnEnable()
        {
            _cachedClickHandlers ??= new UnityAction[items?.Length ?? 0];

            for (var i = 0; i < items.Length; i++)
            {
                if (items[i].button != null)
                {
                    var idx = i;
                    _cachedClickHandlers[i] ??= () => Select(idx);
                    items[i].button.onClick.AddListener(_cachedClickHandlers[i]);
                }
            }

            if (_initialSnapRoutine != null)
                StopCoroutine(_initialSnapRoutine);

            if (delayInitialSnapByOneFrame)
                _initialSnapRoutine = StartCoroutine(InitialSnapRoutine());
            else
                Select(initialIndex, animated: !snapOnEnable ? true : false);
        }

        private void OnDisable()
        {
            if (_initialSnapRoutine != null)
            {
                StopCoroutine(_initialSnapRoutine);
                _initialSnapRoutine = null;
            }

            for (var i = 0; i < items.Length; i++)
            {
                if (items[i].button != null)
                {
                    if (_cachedClickHandlers != null && i < _cachedClickHandlers.Length && _cachedClickHandlers[i] != null)
                        items[i].button.onClick.RemoveListener(_cachedClickHandlers[i]);
                }
            }
        }

        public void Select(int index) => Select(index, animated: true);

        public void Select(int index, bool animated)
        {
            if (items == null || items.Length == 0)
                return;

            index = Mathf.Clamp(index, 0, items.Length - 1);
            if (_currentIndex == index && animated)
                return;

            _currentIndex = index;
            ApplyTabs(index);
            MoveHighlightTo(index, animated);
            SelectionChanged?.Invoke(_currentIndex);
        }

        private IEnumerator InitialSnapRoutine()
        {
            yield return null;

            // Force layout rebuild so anchoredPosition/sizeDelta are final.
            Canvas.ForceUpdateCanvases();

            Select(initialIndex, animated: !snapOnEnable);
            _initialSnapRoutine = null;
        }

        private void ApplyTabs(int activeIndex)
        {
            for (var i = 0; i < items.Length; i++)
            {
                if (items[i].tabRoot != null)
                    items[i].tabRoot.SetActive(i == activeIndex);
            }
        }

        private void MoveHighlightTo(int index, bool animated)
        {
            if (highlight == null)
                return;

            var target = items[index].highlightTarget != null ? items[index].highlightTarget : items[index].button?.transform as RectTransform;
            if (target == null)
                return;

            // Ensure highlight uses the same parent space.
            if (highlight.parent != target.parent)
                highlight.SetParent(target.parent, worldPositionStays: false);

            var targetPos = target.anchoredPosition;
            var targetSize = target.sizeDelta;

            if (matchTargetSize)
                targetSize += sizePadding;

            if (!animated || moveDurationSeconds <= 0.0001f)
            {
                highlight.anchoredPosition = targetPos;
                if (matchTargetSize)
                    highlight.sizeDelta = targetSize;
                return;
            }

            if (_moveRoutine != null)
                StopCoroutine(_moveRoutine);

            _moveRoutine = StartCoroutine(AnimateMove(highlight, targetPos, targetSize));
        }

        private IEnumerator AnimateMove(RectTransform rt, Vector2 targetPos, Vector2 targetSize)
        {
            var startPos = rt.anchoredPosition;
            var startSize = rt.sizeDelta;

            var t = 0f;
            while (t < 1f)
            {
                t += (unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime) / Mathf.Max(0.0001f, moveDurationSeconds);
                var k = moveCurve.Evaluate(Mathf.Clamp01(t));

                rt.anchoredPosition = Vector2.LerpUnclamped(startPos, targetPos, k);
                if (matchTargetSize)
                    rt.sizeDelta = Vector2.LerpUnclamped(startSize, targetSize, k);

                yield return null;
            }

            rt.anchoredPosition = targetPos;
            if (matchTargetSize)
                rt.sizeDelta = targetSize;

            _moveRoutine = null;
        }
    }
}

