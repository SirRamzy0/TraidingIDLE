using System;
using System.Collections.Generic;
using TMPro;
using TraidingIDLE.Localization;
using TraidingIDLE.UI;
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
        [SerializeField] private Color actionButtonEnabledColor = new(0.25f, 0.95f, 0.52f, 1f);
        [SerializeField] private Color actionButtonDisabledColor = new(0.55f, 0.57f, 0.62f, 1f);

        [Header("x5 boost")]
        [SerializeField] private Button x5BoostButton;
        [SerializeField] private GameObject x5BoostDisabledState;
        [SerializeField] private TMP_Text x5BoostStatusText;
        [SerializeField] private TMP_Text x5BoostTimerText;

        private Action _action;
        private Action _x5BoostAction;

        private void Awake()
        {
            AutoResolveMissingReferences();

            if (actionButton != null)
                actionButton.onClick.AddListener(OnActionClicked);

            if (x5BoostButton != null)
                x5BoostButton.onClick.AddListener(OnX5BoostClicked);
        }

        private void OnDestroy()
        {
            if (actionButton != null)
                actionButton.onClick.RemoveListener(OnActionClicked);

            if (x5BoostButton != null)
                x5BoostButton.onClick.RemoveListener(OnX5BoostClicked);
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
                    ? GameTextFormatter.Format(currentLevelPreviewFormat, "{0} -", currentLevelLine)
                    : currentLevelLine;
            if (nextLevelText != null)
                nextLevelText.text = showUpgradePreview
                    ? GameTextFormatter.Format(nextLevelPreviewFormat, " {0}", nextLevelLine)
                    : "";

            if (currentIncomeText != null)
                currentIncomeText.text = showUpgradePreview
                    ? GameTextFormatter.Format(currentIncomePreviewFormat, "{0} -", currentIncomeLine)
                    : currentIncomeLine;
            if (nextIncomeText != null)
                nextIncomeText.text = showUpgradePreview
                    ? GameTextFormatter.Format(nextIncomePreviewFormat, " {0}", nextIncomeLine)
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
                    ? GameTextFormatter.Format(actionLabelFormat, "{0}\n{1}", actionVerb, actionPrice)
                    : "";
            }

            if (actionButtonGraphic != null)
                actionButtonGraphic.color = actionVisible && actionInteractable
                    ? actionButtonEnabledColor
                    : actionButtonDisabledColor;
        }

        public void ConfigureX5Boost(
            bool active,
            string timer,
            bool interactable,
            Action action)
        {
            _x5BoostAction = action;

            if (x5BoostButton != null)
            {
                x5BoostButton.gameObject.SetActive(!active);
                x5BoostButton.interactable = interactable;
            }

            if (x5BoostDisabledState != null)
                x5BoostDisabledState.SetActive(active);

            if (x5BoostStatusText != null)
                x5BoostStatusText.text = active
                    ? LocalizationManager.Tr("business.x5_active", "Доход увеличен")
                    : LocalizationManager.Tr("business.x5_prompt", "Увеличь доход на час!");

            if (x5BoostTimerText != null)
                x5BoostTimerText.text = active ? timer : "";
        }

        private void OnActionClicked()
        {
            _action?.Invoke();
        }

        private void OnX5BoostClicked()
        {
            _x5BoostAction?.Invoke();
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
    }
}
