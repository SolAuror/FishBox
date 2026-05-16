using UnityEngine;

namespace Sol.Rpg
{
    [AddComponentMenu("Sol/RPG/Gameplay Tick Driver")]
    [DisallowMultipleComponent]
    public sealed class GameplayTickDriver : MonoBehaviour
    {
        private void Update()
        {
            GameplayEvents.RaiseGameplayTick(Time.deltaTime);
        }
    }
}
