using System;
using TMPro;
using TraidingIDLE.UI;
using UnityEngine;
using UnityEngine.UI;

namespace TraidingIDLE.Temki
{
    public sealed class TemkiResultDialogUI : MonoBehaviour
    {
        [Header("Roots")]
        [SerializeField] private GameObject successRoot;
        [SerializeField] private GameObject failRoot;

        [Header("Success")]
        [SerializeField] private TMP_Text rewardText;
        [SerializeField] private Button claimButton;
        [SerializeField] private Button doubleAdButton;

        [Header("Fail")]
        [SerializeField] private Button failCloseButton;

        private Action _claimClicked;
        private Action _doubleClicked;
        private Action _closeClicked;

        private void Awake()
        {
            BindButtons();
            Hide();
        }

        private void OnDestroy()
        {
            if (claimButton != null)
                claimButton.onClick.RemoveListener(OnClaimClicked);
            if (doubleAdButton != null)
                doubleAdButton.onClick.RemoveListener(OnDoubleClicked);
            if (failCloseButton != null)
                failCloseButton.onClick.RemoveListener(OnCloseClicked);
        }

        public void ShowSuccess(string reward, Action claimClicked, Action doubleClicked)
        {
            BindButtons();
            _claimClicked = claimClicked;
            _doubleClicked = doubleClicked;
            _closeClicked = null;

            if (rewardText != null)
                rewardText.text = reward;
            if (doubleAdButton != null)
                doubleAdButton.interactable = doubleClicked != null;
            SetActive(successRoot, true);
            SetActive(failRoot, false);
            gameObject.SetActive(true);
        }

        public void ShowFail(Action closeClicked)
        {
            BindButtons();
            _claimClicked = null;
            _doubleClicked = null;
            _closeClicked = closeClicked;

            SetActive(successRoot, false);
            SetActive(failRoot, true);
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            SetActive(successRoot, false);
            SetActive(failRoot, false);
            gameObject.SetActive(false);
        }

        public void Initialize(
            GameObject successRoot,
            GameObject failRoot,
            TMP_Text rewardText,
            Button claimButton,
            Button doubleAdButton,
            Button failCloseButton)
        {
            this.successRoot = successRoot;
            this.failRoot = failRoot;
            this.rewardText = rewardText;
            this.claimButton = claimButton;
            this.doubleAdButton = doubleAdButton;
            this.failCloseButton = failCloseButton;
            BindButtons();
            Hide();
        }

        private void BindButtons()
        {
            if (claimButton != null)
            {
                claimButton.onClick.RemoveListener(OnClaimClicked);
                claimButton.onClick.AddListener(OnClaimClicked);
            }

            if (doubleAdButton != null)
            {
                doubleAdButton.onClick.RemoveListener(OnDoubleClicked);
                doubleAdButton.onClick.AddListener(OnDoubleClicked);
            }

            if (failCloseButton != null)
            {
                failCloseButton.onClick.RemoveListener(OnCloseClicked);
                failCloseButton.onClick.AddListener(OnCloseClicked);
            }
        }

        private void OnClaimClicked() => _claimClicked?.Invoke();

        private void OnDoubleClicked() => _doubleClicked?.Invoke();

        private void OnCloseClicked() => _closeClicked?.Invoke();

        private static void SetActive(GameObject root, bool active)
        {
            if (root != null && root.activeSelf != active)
                root.SetActive(active);
        }
    }
}
