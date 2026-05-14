using System;
using TMPro;
using TraidingIDLE.UI;
using UnityEngine;
using UnityEngine.UI;

namespace TraidingIDLE.Temki
{
    public sealed class TemkaCardUI : MonoBehaviour
    {
        [Header("Content")]
        [SerializeField] private Image artworkImage;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text chanceText;
        [SerializeField] private TMP_Text multiplierText;
        [SerializeField] private TMP_Text durationText;
        [SerializeField] private TMP_Text stakeText;

        [Header("Action")]
        [SerializeField] private Button actionButton;
        [SerializeField] private Image actionButtonImage;
        [SerializeField] private TMP_Text actionButtonText;
        [SerializeField] private Sprite riskButtonSprite;
        [SerializeField] private Sprite timerButtonSprite;
        [SerializeField] private Sprite checkButtonSprite;

        [Header("Chance color")]
        [SerializeField] private Color lowChanceColor = new(0.95f, 0.24f, 0.34f, 1f);
        [SerializeField] private Color mediumChanceColor = new(1f, 0.82f, 0.24f, 1f);
        [SerializeField] private Color highChanceColor = new(0.25f, 0.95f, 0.52f, 1f);

        [Header("Texts")]
        [SerializeField] private string chanceFormat = "{0}%";
        [SerializeField] private string multiplierFormat = "x{0}";
        [SerializeField] private string durationFormat = "{0}";
        [SerializeField] private string stakeFormat = "{0}р";
        [SerializeField] private string riskLabel = "Рискнуть";
        [SerializeField] private string checkLabel = "Проверить";

        private Action _clicked;

        private void Awake()
        {
            ResolveReferences();
            if (actionButton != null)
                actionButton.onClick.AddListener(OnClicked);
        }

        private void OnDestroy()
        {
            if (actionButton != null)
                actionButton.onClick.RemoveListener(OnClicked);
        }

        public void Bind(Action clicked)
        {
            _clicked = clicked;
        }

        public void ConfigureStatic(TemkaDefinition definition, string formattedStake, string formattedDuration)
        {
            ResolveReferences();
            if (definition == null)
                return;

            if (artworkImage != null)
            {
                artworkImage.sprite = definition.Artwork;
                artworkImage.enabled = definition.Artwork != null;
            }

            if (titleText != null)
                titleText.text = definition.DisplayName;
            if (descriptionText != null)
                descriptionText.text = definition.Description;
            if (chanceText != null)
            {
                var displayedChancePercent = RoundChanceToFive(definition.DisplayedSuccessChance * 100f);
                chanceText.text = GameTextFormatter.Format(chanceFormat, "{0}%", displayedChancePercent);
                chanceText.color = GetChanceColor(displayedChancePercent);
            }
            if (multiplierText != null)
                multiplierText.text = GameTextFormatter.Format(multiplierFormat, "x{0}", FormatMultiplier(definition.RewardMultiplier));
            if (durationText != null)
                durationText.text = GameTextFormatter.Format(durationFormat, "{0}", formattedDuration);
            if (stakeText != null)
                stakeText.text = GameTextFormatter.Format(stakeFormat, "{0}р", formattedStake);
        }

        public void PresentRisk(bool canAfford)
        {
            ResolveReferences();
            SetButton(riskButtonSprite, riskLabel, canAfford);
        }

        public void PresentTimer(long secondsLeft)
        {
            ResolveReferences();
            SetButton(timerButtonSprite, GameTextFormatter.CountdownHours(secondsLeft), false);
        }

        public void PresentCheck()
        {
            ResolveReferences();
            SetButton(checkButtonSprite, checkLabel, true);
        }

        private void SetButton(Sprite sprite, string label, bool interactable)
        {
            if (actionButton != null)
                actionButton.interactable = interactable;
            if (actionButtonText != null)
                actionButtonText.text = label;
            if (actionButtonImage != null && sprite != null)
                actionButtonImage.sprite = sprite;
        }

        private void OnClicked()
        {
            _clicked?.Invoke();
        }

        private void ResolveReferences()
        {
            if (actionButton == null)
                actionButton = GetComponentInChildren<Button>(true);
            if (actionButtonImage == null && actionButton != null)
                actionButtonImage = actionButton.GetComponent<Image>();
            if (actionButtonText == null && actionButton != null)
                actionButtonText = actionButton.GetComponentInChildren<TMP_Text>(true);

            var texts = GetComponentsInChildren<TMP_Text>(true);
            for (var i = 0; i < texts.Length; i++)
            {
                var text = texts[i];
                if (text == null)
                    continue;

                switch (text.name)
                {
                    case "Name_text":
                        titleText ??= text;
                        break;
                    case "Description_text":
                        descriptionText ??= text;
                        break;
                    case "chance_text":
                        chanceText ??= text;
                        break;
                    case "Multiple_box":
                        multiplierText ??= text;
                        break;
                    case "Timer_text":
                        durationText ??= text;
                        break;
                    case "Profit_text":
                        stakeText ??= text;
                        break;
                }
            }

            if (artworkImage == null)
            {
                var icon = transform.Find("Logo/Icon");
                if (icon != null)
                    artworkImage = icon.GetComponent<Image>();
            }

            if (riskButtonSprite == null && actionButtonImage != null)
                riskButtonSprite = actionButtonImage.sprite;
            if (timerButtonSprite == null)
                timerButtonSprite = riskButtonSprite;
            if (checkButtonSprite == null)
                checkButtonSprite = riskButtonSprite;
        }

        private static string FormatMultiplier(float value)
        {
            return value.ToString(value % 1f < 0.01f ? "0" : "0.#", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static int RoundChanceToFive(float percent)
        {
            return Mathf.Clamp(Mathf.RoundToInt(percent / 5f) * 5, 0, 100);
        }

        private Color GetChanceColor(int percent)
        {
            if (percent < 35)
                return lowChanceColor;
            if (percent < 55)
                return mediumChanceColor;

            return highChanceColor;
        }
    }
}
