using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if Authorization_yg
using YG;
#endif

namespace TraidingIDLE.UI
{
    public sealed class YandexPlayerNameTextUI : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private Button authButton;

        [Header("Text")]
        [SerializeField] private string textFormat = "{0}";
        [SerializeField] private string fallbackName = "Игрок";
        [SerializeField] private string unauthorizedName = "Гость";
        [SerializeField] private string anonymousName = "Аноним";

        [Header("Auth")]
        [SerializeField] private bool showAuthButtonIfUnauthorized = true;

        private void Awake()
        {
            if (nameText == null)
                nameText = GetComponent<TMP_Text>();

            if (authButton != null)
            {
                authButton.onClick.RemoveListener(OpenAuthDialog);
                authButton.onClick.AddListener(OpenAuthDialog);
            }
        }

        private void OnEnable()
        {
#if Authorization_yg
            YG2.onGetSDKData += Refresh;
#endif
            Refresh();
        }

        private void OnDisable()
        {
#if Authorization_yg
            YG2.onGetSDKData -= Refresh;
#endif
        }

        private void OnDestroy()
        {
            if (authButton != null)
                authButton.onClick.RemoveListener(OpenAuthDialog);
        }

        private void Refresh()
        {
            var playerName = GetPlayerName();
            SetText(playerName);
            RefreshAuthButton();
        }

        private string GetPlayerName()
        {
#if Authorization_yg
            var rawName = YG2.player.name;

            if (string.IsNullOrWhiteSpace(rawName))
                return fallbackName;

            if (rawName == "unauthorized")
                return unauthorizedName;

            if (rawName == "anonymous")
                return anonymousName;

            return rawName;
#else
            return fallbackName;
#endif
        }

        private void SetText(string playerName)
        {
            if (nameText == null)
                return;

            var format = string.IsNullOrWhiteSpace(textFormat) ? "{0}" : textFormat;
            nameText.text = string.Format(format, playerName);
        }

        private void RefreshAuthButton()
        {
            if (authButton == null)
                return;

#if Authorization_yg
            var shouldShow =
                showAuthButtonIfUnauthorized &&
                !YG2.player.auth;

            authButton.gameObject.SetActive(shouldShow);
#else
            authButton.gameObject.SetActive(false);
#endif
        }

        private void OpenAuthDialog()
        {
#if Authorization_yg
            YG2.OpenAuthDialog();
#endif
        }
    }
}