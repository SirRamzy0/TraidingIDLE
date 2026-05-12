using TMPro;
using TraidingIDLE.Saves;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TraidingIDLE.UI
{
    public sealed class DebugResetProgressButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text label;
        [SerializeField] private string labelText = "Сброс\nпрогресса";

        private void Awake()
        {
            ResolveReferences();
            RefreshLabel();

            if (button != null)
                button.onClick.AddListener(ResetProgress);
        }

        private void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveListener(ResetProgress);
        }

        private void OnValidate()
        {
            ResolveReferences();
            RefreshLabel();
        }

        private void ResetProgress()
        {
            SaveStorage.SuspendWrites();
            SaveStorage.DeleteAll();
            SaveStorage.ResumeWrites();
            SaveStorage.Flush();

            SaveStorage.SuspendWrites();
            var activeScene = SceneManager.GetActiveScene();
            SceneManager.sceneLoaded += OnSceneLoadedAfterReset;
            SceneManager.LoadScene(activeScene.name);
        }

        private static void OnSceneLoadedAfterReset(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= OnSceneLoadedAfterReset;
            SaveStorage.ResumeWrites();
        }

        private void ResolveReferences()
        {
            if (button == null)
                button = GetComponent<Button>();
            if (label == null)
                label = GetComponentInChildren<TMP_Text>(true);
        }

        private void RefreshLabel()
        {
            if (label != null)
                label.text = labelText;
        }
    }
}
