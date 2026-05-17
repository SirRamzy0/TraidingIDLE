using UnityEngine;
using UnityEngine.UI;
using YG;

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

        [Header("Refresh")]
        [SerializeField] private bool autoFindLeaderboards = true;
        [SerializeField] private LeaderboardYG[] leaderboardsToRefresh = System.Array.Empty<LeaderboardYG>();
        [SerializeField] private bool refreshOnToggle = true;
        [SerializeField] private bool refreshOnMenuSelectionChanged = true;
        [SerializeField, Min(0f)] private float refreshDelaySeconds = 0.05f;

        private MenuSelectionHighlight[] _menuSelections = System.Array.Empty<MenuSelectionHighlight>();
        private void Awake()
        {
            ResolveLeaderboards();
            SubscribeMenuSelections();

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
            SubscribeMenuSelections();

            if (showSmallOnEnable)
                ShowSmall();
            else
                ApplyState();

            ScheduleRefreshVisibleLeaderboards();
        }

        private void OnDisable()
        {
            CancelInvoke(nameof(RefreshVisibleLeaderboards));
            UnsubscribeMenuSelections();
        }

        private void OnDestroy()
        {
            UnsubscribeMenuSelections();

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

            if (refreshOnToggle)
                ScheduleRefreshVisibleLeaderboards();
        }

        public void ShowSmall()
        {
            if (bigLeaderboardRoot != null)
                bigLeaderboardRoot.SetActive(false);

            if (smallLeaderboardRoot != null)
                smallLeaderboardRoot.SetActive(true);

            if (refreshOnToggle)
                ScheduleRefreshVisibleLeaderboards();
        }

        private void ApplyState()
        {
            var bigActive = bigLeaderboardRoot != null && bigLeaderboardRoot.activeSelf;

            if (smallLeaderboardRoot != null)
                smallLeaderboardRoot.SetActive(!bigActive);

            if (bigLeaderboardRoot != null)
                bigLeaderboardRoot.SetActive(bigActive);
        }

        private void ResolveLeaderboards()
        {
            if (!autoFindLeaderboards && leaderboardsToRefresh != null && leaderboardsToRefresh.Length > 0)
                return;

            var resolved = new System.Collections.Generic.List<LeaderboardYG>();
            AddLeaderboardsFromRoot(smallLeaderboardRoot, resolved);
            AddLeaderboardsFromRoot(bigLeaderboardRoot, resolved);

            if (leaderboardsToRefresh != null)
            {
                for (var i = 0; i < leaderboardsToRefresh.Length; i++)
                {
                    var leaderboard = leaderboardsToRefresh[i];
                    if (leaderboard != null && !resolved.Contains(leaderboard))
                        resolved.Add(leaderboard);
                }
            }

            leaderboardsToRefresh = resolved.ToArray();
        }

        private static void AddLeaderboardsFromRoot(GameObject root, System.Collections.Generic.List<LeaderboardYG> result)
        {
            if (root == null)
                return;

            var leaderboards = root.GetComponentsInChildren<LeaderboardYG>(true);
            for (var i = 0; i < leaderboards.Length; i++)
            {
                if (leaderboards[i] != null && !result.Contains(leaderboards[i]))
                    result.Add(leaderboards[i]);
            }
        }

        private void SubscribeMenuSelections()
        {
            if (!refreshOnMenuSelectionChanged)
                return;

            UnsubscribeMenuSelections();

            _menuSelections = FindObjectsByType<MenuSelectionHighlight>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < _menuSelections.Length; i++)
            {
                if (_menuSelections[i] != null)
                    _menuSelections[i].SelectionChanged += OnMenuSelectionChanged;
            }
        }

        private void UnsubscribeMenuSelections()
        {
            for (var i = 0; i < _menuSelections.Length; i++)
            {
                if (_menuSelections[i] != null)
                    _menuSelections[i].SelectionChanged -= OnMenuSelectionChanged;
            }

            _menuSelections = System.Array.Empty<MenuSelectionHighlight>();
        }

        private void OnMenuSelectionChanged(int _)
        {
            ScheduleRefreshVisibleLeaderboards();
        }

        private void ScheduleRefreshVisibleLeaderboards()
        {
            if (!isActiveAndEnabled)
                return;

            CancelInvoke(nameof(RefreshVisibleLeaderboards));
            Invoke(nameof(RefreshVisibleLeaderboards), refreshDelaySeconds);
        }

        private void RefreshVisibleLeaderboards()
        {
            ResolveLeaderboards();

            for (var i = 0; i < leaderboardsToRefresh.Length; i++)
            {
                var leaderboard = leaderboardsToRefresh[i];
                if (leaderboard != null && leaderboard.isActiveAndEnabled)
                    leaderboard.UpdateLB();
            }
        }
    }
}
