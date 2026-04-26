using UnityEngine;

namespace Sol.HUD
{
    public sealed class PauseMenuBridge : MonoBehaviour
    {
        private PauseMenuSystem _pauseMenu;
        private bool _isBound;

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
            PauseMenuSystem resolved = PauseMenuSystem.ResolveInstance(activateIfInactive: false);
            if (resolved == null)
                return;

            if (_isBound && ReferenceEquals(_pauseMenu, resolved))
                return;

            if (_isBound && _pauseMenu != null)
                _pauseMenu.OnQuit -= HandleQuit;

            _pauseMenu = resolved;
            _pauseMenu.OnQuit -= HandleQuit;
            _pauseMenu.OnQuit += HandleQuit;
            _isBound = true;
        }

        private void Unbind()
        {
            if (_pauseMenu == null)
                return;
            _pauseMenu.OnQuit -= HandleQuit;
            _isBound = false;
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
