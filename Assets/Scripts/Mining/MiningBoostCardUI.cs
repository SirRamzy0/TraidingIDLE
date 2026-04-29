using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TraidingIDLE.Mining
{
    public sealed class MiningBoostCardUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText = null!;
        [SerializeField] private TMP_Text levelText = null!;
        [SerializeField] private TMP_Text descriptionText = null!;
        [SerializeField] private TMP_Text buttonText = null!;
        [SerializeField] private Button button = null!;

        private Action? _clicked;

        private void Awake()
        {
            if (button != null)
                button.onClick.AddListener(OnClicked);
        }

        private void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveListener(OnClicked);
        }

        public void Configure(
            string title,
            string level,
            string description,
            string buttonLabel,
            bool interactable,
            Action clicked)
        {
            _clicked = clicked;

            if (titleText != null)
                titleText.text = title;
            if (levelText != null)
                levelText.text = level;
            if (descriptionText != null)
                descriptionText.text = description;
            if (buttonText != null)
                buttonText.text = buttonLabel;
            if (button != null)
                button.interactable = interactable;
        }

        private void OnClicked()
        {
            _clicked?.Invoke();
        }
    }
}
