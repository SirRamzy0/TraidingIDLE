using System.Globalization;
using TMPro;
using TraidingIDLE.Player;
using UnityEngine;
using UnityEngine.UI;

namespace TraidingIDLE.UI
{
    public sealed class DebugAddRublesButton : MonoBehaviour
    {
        [SerializeField] private PlayerProfile profile;
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text label;
        [SerializeField, Min(0)] private long rublesToAdd = 100_000_000_000;
        [SerializeField] private string labelFormat = "Тест\n+{0}";

        private void Awake()
        {
            ResolveReferences();
            RefreshLabel();

            if (button != null)
                button.onClick.AddListener(AddRubles);
        }

        private void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveListener(AddRubles);
        }

        private void OnValidate()
        {
            ResolveReferences();
            RefreshLabel();
        }

        private void AddRubles()
        {
            if (profile == null)
                profile = FindFirstObjectByType<PlayerProfile>();

            if (profile != null)
                profile.AddRubles(rublesToAdd);
        }

        private void ResolveReferences()
        {
            if (profile == null)
                profile = FindFirstObjectByType<PlayerProfile>();
            if (button == null)
                button = GetComponent<Button>();
            if (label == null)
                label = GetComponentInChildren<TMP_Text>(true);
        }

        private void RefreshLabel()
        {
            if (label == null)
                return;

            label.text = string.Format(
                string.IsNullOrEmpty(labelFormat) ? "+{0}" : labelFormat,
                FormatThousands(rublesToAdd));
        }

        private static string FormatThousands(long value)
        {
            return value
                .ToString("N0", CultureInfo.InvariantCulture)
                .Replace(",", ".");
        }
    }
}
