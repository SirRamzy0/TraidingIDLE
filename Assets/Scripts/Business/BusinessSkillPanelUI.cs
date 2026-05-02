using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TraidingIDLE.Business
{
    public sealed class BusinessSkillPanelUI : MonoBehaviour
    {
        [SerializeField] private GameObject skillLockedRoot = null!;
        [SerializeField] private GameObject skillUnlockedRoot = null!;

        [Header("Locked layout")]
        [SerializeField] private TMP_Text lockedHintText = null!;

        [Header("Unlocked layout")]
        [SerializeField] private Image skillAvatar = null!;
        [SerializeField] private TMP_Text skillTitleText = null!;
        [SerializeField] private TMP_Text skillDescriptionText = null!;
        [SerializeField] private Button launchButton = null!;
        [SerializeField] private TMP_Text launchButtonLabel = null!;

        private void Awake()
        {
            if (skillAvatar != null && skillAvatar.sprite == null)
                skillAvatar.enabled = false;
        }

        public void PresentLocked(string lockedText)
        {
            if (skillLockedRoot != null)
                skillLockedRoot.SetActive(true);
            if (skillUnlockedRoot != null)
                skillUnlockedRoot.SetActive(false);

            if (lockedHintText != null)
                lockedHintText.text = lockedText;
        }

        public void PresentUnlocked(
            Sprite? avatar,
            string title,
            string description,
            bool launchInteractable,
            string launchLabel)
        {
            if (skillLockedRoot != null)
                skillLockedRoot.SetActive(false);
            if (skillUnlockedRoot != null)
                skillUnlockedRoot.SetActive(true);

            if (skillAvatar != null)
            {
                skillAvatar.sprite = avatar;
                skillAvatar.enabled = avatar != null;
            }

            if (skillTitleText != null)
                skillTitleText.text = title;
            if (skillDescriptionText != null)
                skillDescriptionText.text = description;

            if (launchButton != null)
                launchButton.interactable = launchInteractable;
            if (launchButtonLabel != null)
                launchButtonLabel.text = launchLabel;
        }

        public void SetLaunchListener(System.Action listener)
        {
            if (launchButton == null)
                return;

            launchButton.onClick.RemoveAllListeners();
            if (listener != null)
                launchButton.onClick.AddListener(listener.Invoke);
        }
    }
}
