using UnityEngine;
using UnityEngine.SceneManagement;
using Sol.Locomotion;

namespace Sol.Fishing
{
    public static class FishingRodBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            AttachToPlayerControllers();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            AttachToPlayerControllers();
        }

        private static void AttachToPlayerControllers()
        {
            LocomotionController[] controllers = Object.FindObjectsByType<LocomotionController>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            for (int i = 0; i < controllers.Length; i++)
            {
                LocomotionController controller = controllers[i];
                if (controller == null || !controller.IsPlayerControlled())
                    continue;

                if (controller.GetComponent<FishingState>() == null)
                    controller.gameObject.AddComponent<FishingState>();
            }
        }
    }
}
