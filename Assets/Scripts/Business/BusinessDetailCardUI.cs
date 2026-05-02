using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TraidingIDLE.Business
{
    public sealed class BusinessDetailCardUI : MonoBehaviour
    {
        [Header("Content")]
        [SerializeField] private Image artworkImage;
        [SerializeField] private TMP_Text nameText;

        [Header("Level")]
        [SerializeField] private TMP_Text currentLevelText;
        [SerializeField] private TMP_Text nextLevelText;
        [SerializeField] private string currentLevelPreviewFormat = "{0} -";
        [SerializeField] private string nextLevelPreviewFormat = " {0}";

        [Header("Income")]
        [SerializeField] private TMP_Text currentIncomeText;
        [SerializeField] private TMP_Text nextIncomeText;
        [SerializeField] private string currentIncomePreviewFormat = "{0} -";
        [SerializeField] private string nextIncomePreviewFormat = " {0}";

        [Header("Action")]
        [SerializeField] private Button actionButton;
        [SerializeField] private TMP_Text actionLabelText;
        [SerializeField] private string actionLabelFormat = "{0}\n{1}";
        [SerializeField] private Graphic actionButtonGraphic;
        [SerializeField] private Color actionButtonEnabledColor = new(0.08f, 0.45f, 0.28f, 1f);
        [SerializeField] private Color actionButtonDisabledColor = new(0.35f, 0.35f, 0.35f, 0.75f);

        private Action _action;

        private void Awake()
        {
            AutoResolveMissingReferences();
            if (actionButton != null)
                actionButton.onClick.AddListener(OnActionClicked);
        }

        private void OnDestroy()
        {
            if (actionButton != null)
                actionButton.onClick.RemoveListener(OnActionClicked);
        }

        public void Configure(
            Sprite artwork,
            string businessName,
            string currentLevelLine,
            string nextLevelLine,
            string currentIncomeLine,
            string nextIncomeLine,
            bool showUpgradePreview,
            string actionVerb,
            string actionPrice,
            bool actionVisible,
            bool actionInteractable,
            Action action)
        {
            AutoResolveMissingReferences();

            if (artworkImage != null)
            {
                artworkImage.sprite = artwork;
                artworkImage.enabled = artwork != null;
            }

            if (nameText != null)
                nameText.text = businessName;

            if (currentLevelText != null)
                currentLevelText.text = showUpgradePreview
                    ? FormatOne(currentLevelPreviewFormat, "{0} -", currentLevelLine)
                    : currentLevelLine;
            if (nextLevelText != null)
                nextLevelText.text = showUpgradePreview
                    ? FormatOne(nextLevelPreviewFormat, " {0}", nextLevelLine)
                    : "";

            if (currentIncomeText != null)
                currentIncomeText.text = showUpgradePreview
                    ? FormatOne(currentIncomePreviewFormat, "{0} -", currentIncomeLine)
                    : currentIncomeLine;
            if (nextIncomeText != null)
                nextIncomeText.text = showUpgradePreview
                    ? FormatOne(nextIncomePreviewFormat, " {0}", nextIncomeLine)
                    : "";

            _action = action;

            if (actionButton != null)
            {
                actionButton.gameObject.SetActive(actionVisible);
                actionButton.interactable = actionVisible && actionInteractable;
            }

            if (actionLabelText != null)
            {
                actionLabelText.text = actionVisible
                    ? FormatTwo(actionLabelFormat, "{0}\n{1}", actionVerb, actionPrice)
                    : "";
            }

            if (actionButtonGraphic != null)
                actionButtonGraphic.color = actionVisible && actionInteractable
                    ? actionButtonEnabledColor
                    : actionButtonDisabledColor;
        }

        private void OnActionClicked()
        {
            _action?.Invoke();
        }

        private void AutoResolveMissingReferences()
        {
            if (actionButton == null)
                actionButton = GetComponentInChildren<Button>(true);

            if (actionButtonGraphic == null && actionButton != null)
                actionButtonGraphic = actionButton.targetGraphic;

            var texts = GetComponentsInChildren<TMP_Text>(true);
            var contentTexts = new List<TMP_Text>(texts.Length);
            for (var i = 0; i < texts.Length; i++)
            {
                var t = texts[i];
                if (actionButton != null && t.transform.IsChildOf(actionButton.transform))
                    continue;

                contentTexts.Add(t);
                var lowerName = t.gameObject.name.ToLowerInvariant();

                var isCurrent = lowerName.Contains("current") || lowerName.Contains("cur");
                var isNext = lowerName.Contains("next") || lowerName.Contains("upgrade") || lowerName.Contains("after");
                var isLevel = lowerName.Contains("level");
                var isIncome = lowerName.Contains("income") || lowerName.Contains("profit") || lowerName.Contains("hour");

                if (nameText == null && (lowerName.Contains("name") || lowerName.Contains("title")))
                    nameText = t;
                else if (currentLevelText == null && isLevel && isCurrent)
                    currentLevelText = t;
                else if (nextLevelText == null && isLevel && isNext)
                    nextLevelText = t;
                else if (currentIncomeText == null && isIncome && isCurrent)
                    currentIncomeText = t;
                else if (nextIncomeText == null && isIncome && isNext)
                    nextIncomeText = t;
            }

            if (nameText == null && contentTexts.Count > 0)
                nameText = contentTexts[0];

            if (actionButton != null)
            {
                var buttonTexts = actionButton.GetComponentsInChildren<TMP_Text>(true);
                if (actionLabelText == null && buttonTexts.Length > 0)
                    actionLabelText = buttonTexts[0];
            }

            if (artworkImage == null)
            {
                var images = GetComponentsInChildren<Image>(true);
                for (var i = 0; i < images.Length; i++)
                {
                    var img = images[i];
                    if (actionButtonGraphic != null && ReferenceEquals(img, actionButtonGraphic))
                        continue;
                    if (img.sprite == null)
                        continue;

                    artworkImage = img;
                    break;
                }
            }
        }

        private static string SafeFormat(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static string FormatTwo(string format, string fallback, object arg0, object arg1)
        {
            var safe = SafeFormat(format, fallback);
            try
            {
                return string.Format(safe, arg0, arg1);
            }
            catch (FormatException)
            {
                return string.Format(fallback, arg0, arg1);
            }
        }

        private static string FormatOne(string format, string fallback, object arg)
        {
            var safe = SafeFormat(format, fallback);
            try
            {
                return string.Format(safe, arg);
            }
            catch (FormatException)
            {
                return string.Format(fallback, arg);
            }
        }
    }
}
