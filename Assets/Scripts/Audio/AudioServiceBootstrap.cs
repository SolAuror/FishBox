using UnityEngine;

namespace Sol.Audio
{
    public static class AudioServiceBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureAudioService()
        {
            if (AudioService.Instance != null)
                return;

            AudioService existing = Object.FindFirstObjectByType<AudioService>(FindObjectsInactive.Include);
            if (existing != null)
                return;

            GameObject root = new("[AudioService]");
            root.AddComponent<AudioService>();
        }
    }
}
