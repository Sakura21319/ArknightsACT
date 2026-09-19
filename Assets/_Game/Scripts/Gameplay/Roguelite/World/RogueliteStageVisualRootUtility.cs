using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// Shared helper for runtime visual passes.
    /// Keeps stage visual roots consistent and avoids every controller duplicating
    /// Find/Create/replace logic.
    /// </summary>
    internal static class RogueliteStageVisualRootUtility
    {
        public static Transform GetOrCreate(Transform stage, string rootName, bool replaceExisting = false)
        {
            if (stage == null)
                return null;

            var existing = stage.Find(rootName);
            if (existing != null)
            {
                if (!replaceExisting)
                    return existing;

                Object.Destroy(existing.gameObject);
            }

            var root = new GameObject(rootName).transform;
            root.SetParent(stage, false);
            return root;
        }
    }
}
