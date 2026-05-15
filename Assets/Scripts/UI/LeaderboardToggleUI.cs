using UnityEngine;
using UnityEngine.UI;

namespace TraidingIDLE.UI
{
    public sealed class LeaderboardToggleUI : MonoBehaviour
    {
        [Header("Roots")]
        [SerializeField] private GameObject smallLeaderboardRoot;
        [SerializeField] private GameObject bigLeaderboardRoot;

        [Header("Buttons")]
        [SerializeField] private Button openBigButton;
        [SerializeField] private Button closeBigButton;

        [Header("Startup")]
        [SerializeField] private bool showSmallOnEnable = true;

        private void Awake()
        {
            if (openBigButton != null)
            {
                openBigButton.onClick.RemoveListener(ShowBig);
                openBigButton.onClick.AddListener(ShowBig);
            }

            if (closeBigButton != null)
            {
                closeBigButton.onClick.RemoveListener(ShowSmall);
                closeBigButton.onClick.AddListener(ShowSmall);
            }
        }

        private void OnEnable()
        {
            if (showSmallOnEnable)
                ShowSmall();
            else
                ApplyState();
        }

        private void OnDestroy()
        {
            if (openBigButton != null)
                openBigButton.onClick.RemoveListener(ShowBig);

            if (closeBigButton != null)
                closeBigButton.onClick.RemoveListener(ShowSmall);
        }

        public void ShowBig()
        {
            if (smallLeaderboardRoot != null)
                smallLeaderboardRoot.SetActive(false);

            if (bigLeaderboardRoot != null)
                bigLeaderboardRoot.SetActive(true);
        }

        public void ShowSmall()
        {
            if (bigLeaderboardRoot != null)
                bigLeaderboardRoot.SetActive(false);

            if (smallLeaderboardRoot != null)
                smallLeaderboardRoot.SetActive(true);
        }

        private void ApplyState()
        {
            var bigActive = bigLeaderboardRoot != null && bigLeaderboardRoot.activeSelf;

            if (smallLeaderboardRoot != null)
                smallLeaderboardRoot.SetActive(!bigActive);

            if (bigLeaderboardRoot != null)
                bigLeaderboardRoot.SetActive(bigActive);
        }
    }
}