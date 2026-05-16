using UnityEngine;

namespace Sol.Rpg
{
    public interface IGameplayTagProvider
    {
        GameplayTagSet Tags { get; }
    }

    public static class GameplayTagProviderExtensions
    {
        public static GameplayTagSet GetGameplayTags(this Component component)
        {
            if (component == null)
                return GameplayTagSet.Empty;

            if (component is IGameplayTagProvider provider)
                return provider.Tags ?? GameplayTagSet.Empty;

            return component.GetComponent<IGameplayTagProvider>()?.Tags ?? GameplayTagSet.Empty;
        }

        public static GameplayTagSet GetGameplayTags(this GameObject gameObject)
        {
            if (gameObject == null)
                return GameplayTagSet.Empty;

            return gameObject.GetComponent<IGameplayTagProvider>()?.Tags ?? GameplayTagSet.Empty;
        }

        public static bool HasGameplayTag(this Component component, string tagIdOrPath)
        {
            return component.GetGameplayTags().HasExact(tagIdOrPath);
        }

        public static bool HasGameplayTag(this GameObject gameObject, string tagIdOrPath)
        {
            return gameObject.GetGameplayTags().HasExact(tagIdOrPath);
        }

        public static bool HasGameplayTagOrChild(this Component component, string tagPath)
        {
            return component.GetGameplayTags().HasTagOrChild(tagPath);
        }

        public static bool HasGameplayTagOrChild(this GameObject gameObject, string tagPath)
        {
            return gameObject.GetGameplayTags().HasTagOrChild(tagPath);
        }
    }
}
