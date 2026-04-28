using System.Globalization;
using TMPro;
using TraidingIDLE.Player;
using UnityEngine;

namespace TraidingIDLE.UI
{
    public sealed class PlayerProfileUI : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private PlayerProfile profile = null!;

        [Header("Labels")]
        [SerializeField] private TMP_Text rublesText = null!;

        [Header("Formats")]
        [SerializeField] private string rublesFormat = "{0}";

        private void Awake()
        {
            if (profile == null)
                profile = FindFirstObjectByType<PlayerProfile>();
        }

        private void OnEnable()
        {
            if (profile != null)
                profile.RublesChanged += OnRublesChanged;

            RefreshRubles();
        }

        private void Start()
        {
            RefreshRubles();
        }

        private void OnDisable()
        {
            if (profile != null)
                profile.RublesChanged -= OnRublesChanged;
        }

        private void OnRublesChanged(long _) => RefreshRubles();

        private void RefreshRubles()
        {
            if (rublesText == null || profile == null)
                return;

            rublesText.text = string.Format(
                SafeFormat(rublesFormat, "{0}"),
                FormatThousands(profile.Rubles));
        }

        private static string FormatThousands(long value)
        {
            return value
                .ToString("N0", CultureInfo.InvariantCulture)
                .Replace(",", ".");
        }

        private static string SafeFormat(string format, string fallback)
        {
            return string.IsNullOrEmpty(format) ? fallback : format;
        }
    }
}
