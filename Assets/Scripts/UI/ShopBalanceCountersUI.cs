using System;
using System.Collections;
using System.Globalization;
using TMPro;
using TraidingIDLE.Player;
using UnityEngine;

namespace TraidingIDLE.UI
{
    public sealed class ShopBalanceCountersUI : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private PlayerProfile profile;
        [SerializeField] private TMP_Text rublesText;
        [SerializeField] private TMP_Text gemsText;

        [Header("Formats")]
        [SerializeField] private string rublesFormat = "{0}";
        [SerializeField] private string gemsFormat = "{0}";

        [Header("Number animation")]
        [SerializeField] private bool animateNumbers = true;
        [SerializeField, Min(0.01f)] private float numberAnimationSeconds = 0.35f;
        [SerializeField] private bool useUnscaledTime = true;

        [Header("Punch animation")]
        [SerializeField] private bool animateScale = true;
        [SerializeField, Min(1f)] private float punchScale = 1.12f;
        [SerializeField, Min(0.01f)] private float punchSeconds = 0.18f;

        [Header("Flash animation")]
        [SerializeField] private bool animateColor = true;
        [SerializeField] private Color gainFlashColor = new(0.55f, 1f, 0.55f, 1f);
        [SerializeField] private Color spendFlashColor = new(1f, 0.45f, 0.45f, 1f);

        private long _shownRubles;
        private long _shownGems;

        private Coroutine _rublesRoutine;
        private Coroutine _gemsRoutine;

        private Color _rublesBaseColor = Color.white;
        private Color _gemsBaseColor = Color.white;

        private Vector3 _rublesBaseScale = Vector3.one;
        private Vector3 _gemsBaseScale = Vector3.one;

        private void Awake()
        {
            if (profile == null)
                profile = FindFirstObjectByType<PlayerProfile>();

            if (rublesText != null)
            {
                _rublesBaseColor = rublesText.color;
                _rublesBaseScale = rublesText.transform.localScale;
            }

            if (gemsText != null)
            {
                _gemsBaseColor = gemsText.color;
                _gemsBaseScale = gemsText.transform.localScale;
            }
        }

        private void OnEnable()
        {
            if (profile == null)
                profile = FindFirstObjectByType<PlayerProfile>();

            if (profile != null)
            {
                profile.RublesChanged += OnRublesChanged;
                profile.GemsChanged += OnGemsChanged;
            }

            RefreshImmediate();
        }

        private void OnDisable()
        {
            if (profile != null)
            {
                profile.RublesChanged -= OnRublesChanged;
                profile.GemsChanged -= OnGemsChanged;
            }

            StopRublesAnimation();
            StopGemsAnimation();
        }

        private void OnRublesChanged(long value)
        {
            AnimateRubles(value);
        }

        private void OnGemsChanged(long value)
        {
            AnimateGems(value);
        }

        private void RefreshImmediate()
        {
            var rubles = profile != null ? profile.Rubles : 0;
            var gems = profile != null ? profile.Gems : 0;

            _shownRubles = rubles;
            _shownGems = gems;

            if (rublesText != null)
            {
                rublesText.text = FormatWithTemplate(rublesFormat, rubles);
                rublesText.color = _rublesBaseColor;
                rublesText.transform.localScale = _rublesBaseScale;
            }

            if (gemsText != null)
            {
                gemsText.text = FormatWithTemplate(gemsFormat, gems);
                gemsText.color = _gemsBaseColor;
                gemsText.transform.localScale = _gemsBaseScale;
            }
        }

        private void AnimateRubles(long target)
        {
            if (rublesText == null)
                return;

            StopRublesAnimation();

            if (!animateNumbers || !gameObject.activeInHierarchy)
            {
                _shownRubles = target;
                rublesText.text = FormatWithTemplate(rublesFormat, target);
                return;
            }

            var from = _shownRubles;
            var flashColor = target >= from ? gainFlashColor : spendFlashColor;

            _rublesRoutine = StartCoroutine(AnimateCounterRoutine(
                rublesText,
                rublesFormat,
                from,
                target,
                _rublesBaseColor,
                flashColor,
                _rublesBaseScale,
                value => _shownRubles = value,
                () => _rublesRoutine = null));
        }

        private void AnimateGems(long target)
        {
            if (gemsText == null)
                return;

            StopGemsAnimation();

            if (!animateNumbers || !gameObject.activeInHierarchy)
            {
                _shownGems = target;
                gemsText.text = FormatWithTemplate(gemsFormat, target);
                return;
            }

            var from = _shownGems;
            var flashColor = target >= from ? gainFlashColor : spendFlashColor;

            _gemsRoutine = StartCoroutine(AnimateCounterRoutine(
                gemsText,
                gemsFormat,
                from,
                target,
                _gemsBaseColor,
                flashColor,
                _gemsBaseScale,
                value => _shownGems = value,
                () => _gemsRoutine = null));
        }

        private IEnumerator AnimateCounterRoutine(
            TMP_Text text,
            string format,
            long from,
            long to,
            Color baseColor,
            Color flashColor,
            Vector3 baseScale,
            Action<long> assignShownValue,
            Action onComplete)
        {
            var duration = Mathf.Max(0.01f, numberAnimationSeconds);
            var punchDuration = Mathf.Max(0.01f, punchSeconds);
            var elapsed = 0f;

            while (elapsed < duration && text != null)
            {
                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

                var t = Mathf.Clamp01(elapsed / duration);
                var eased = EaseOutCubic(t);

                var value = LerpLong(from, to, eased);
                assignShownValue?.Invoke(value);

                text.text = FormatWithTemplate(format, value);

                if (animateColor)
                {
                    var colorStrength = 1f - t;
                    text.color = Color.Lerp(baseColor, flashColor, colorStrength);
                }

                if (animateScale)
                {
                    var scaleT = Mathf.Clamp01(elapsed / punchDuration);
                    var wave = Mathf.Sin(scaleT * Mathf.PI);
                    var scale = Mathf.Lerp(1f, punchScale, wave);
                    text.transform.localScale = baseScale * scale;
                }

                yield return null;
            }

            if (text != null)
            {
                assignShownValue?.Invoke(to);
                text.text = FormatWithTemplate(format, to);
                text.color = baseColor;
                text.transform.localScale = baseScale;
            }

            onComplete?.Invoke();
        }

        private void StopRublesAnimation()
        {
            if (_rublesRoutine != null)
            {
                StopCoroutine(_rublesRoutine);
                _rublesRoutine = null;
            }

            if (rublesText != null)
            {
                rublesText.color = _rublesBaseColor;
                rublesText.transform.localScale = _rublesBaseScale;
            }
        }

        private void StopGemsAnimation()
        {
            if (_gemsRoutine != null)
            {
                StopCoroutine(_gemsRoutine);
                _gemsRoutine = null;
            }

            if (gemsText != null)
            {
                gemsText.color = _gemsBaseColor;
                gemsText.transform.localScale = _gemsBaseScale;
            }
        }

        private static float EaseOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - Mathf.Pow(1f - t, 3f);
        }

        private static long LerpLong(long from, long to, float t)
        {
            if (from == to)
                return to;

            var value = from + (to - from) * (double)Mathf.Clamp01(t);

            if (to >= from)
                return (long)Math.Floor(value);

            return (long)Math.Ceiling(value);
        }

        private static string FormatWithTemplate(string template, long value)
        {
            template = string.IsNullOrEmpty(template) ? "{0}" : template;
            return string.Format(template, FormatThousands(value));
        }

        private static string FormatThousands(long value)
        {
            return value
                .ToString("N0", CultureInfo.InvariantCulture)
                .Replace(",", ".");
        }
    }
}