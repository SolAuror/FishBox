using UnityEngine;

namespace Sol.HUD
{
    public sealed class PauseMenuBridge : MonoBehaviour
    {
        private void Awake()
        {
            if (UIInputManager.Instance == null)
                gameObject.AddComponent<UIInputManager>();
        }
    }
}
