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

            _pauseMenu.OnSettings -= HandleSettings;
            _pauseMenu.OnSettings += HandleSettings;

            _pauseMenu.OnSaveGame -= HandleSaveGame;
            _pauseMenu.OnSaveGame += HandleSaveGame;

            _pauseMenu.OnLoadGame -= HandleLoadGame;
            _pauseMenu.OnLoadGame += HandleLoadGame;

            _pauseMenu.OnQuit -= HandleQuit;
            _pauseMenu.OnQuit += HandleQuit;
        }

        private void Unbind()
        {
            if (_pauseMenu == null)
                return;

            _pauseMenu.OnSettings -= HandleSettings;
            _pauseMenu.OnSaveGame -= HandleSaveGame;
            _pauseMenu.OnLoadGame -= HandleLoadGame;
            _pauseMenu.OnQuit -= HandleQuit;
        }

        private void HandleSettings()
        {
            PauseMenuSystem.Instance?.Hide();

            SettingsMenuSystem settingsMenu = SettingsMenuSystem.ResolveInstance(activateIfInactive: true);
            settingsMenu?.Open(returnToPause: true);
        }

        private void HandleSaveGame()
        {
            PauseMenuSystem.Instance?.Hide();

            SaveMenuSystem saveMenu = SaveMenuSystem.ResolveInstance(activateIfInactive: true);
            saveMenu?.Open(returnToPause: true);
        }

        private void HandleLoadGame()
        {
            PauseMenuSystem.Instance?.Hide();

            LoadMenuSystem loadMenu = LoadMenuSystem.ResolveInstance(activateIfInactive: true);
            loadMenu?.Open(returnToPause: true);
        }

        private void HandleQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
