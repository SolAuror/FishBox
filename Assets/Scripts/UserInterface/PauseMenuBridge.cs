using UnityEngine;

namespace Sol.HUD
{
    public sealed class PauseMenuBridge : MonoBehaviour
    {
        private PauseMenuSystem _pauseMenu;

        private void Start()
        {
            TryBind();
        }

        private void OnEnable()
        {
            TryBind();
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void TryBind()
        {
            _pauseMenu = PauseMenuSystem.Instance;
            if (_pauseMenu == null)
                _pauseMenu = FindFirstObjectByType<PauseMenuSystem>(FindObjectsInactive.Include);

            if (_pauseMenu == null)
                return;

            _pauseMenu.OnQuit -= HandleQuit;
            _pauseMenu.OnQuit += HandleQuit;
        }

        private void Unbind()
        {
            if (_pauseMenu == null)
                return;
            _pauseMenu.OnQuit -= HandleQuit;
        }

        private void HandleQuit()
        {
            DialoguePromptSystem prompt = DialoguePromptSystem.ResolveInstance(activateIfInactive: true);
            if (prompt != null)
            {
                prompt.Show(
                    "Quit to Desktop?",
                    "Unsaved progress will be lost.",
                    QuitApplication,
                    () => PauseMenuSystem.ResolveInstance(activateIfInactive: true)?.Show(),
                    confirmLabel: "Quit",
                    cancelLabel: "Cancel");
                return;
            }

            QuitApplication();
        }

        private static void QuitApplication()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
