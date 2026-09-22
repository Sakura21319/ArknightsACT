using System.Collections.Generic;

namespace ArknightsACT.Gameplay.Input
{
    /// <summary>
    /// Lightweight gameplay-only input gate. UI owners can keep receiving their own explicit
    /// controls while movement, attacks, skills and other gameplay actions are suppressed.
    /// </summary>
    public static class GameplayInputBlocker
    {
        private static readonly HashSet<object> Owners = new();

        public static bool IsBlocked => Owners.Count > 0;

        public static void SetBlocked(object owner, bool blocked)
        {
            if (owner == null) return;
            if (blocked) Owners.Add(owner);
            else Owners.Remove(owner);
        }

        public static void Release(object owner)
        {
            if (owner != null) Owners.Remove(owner);
        }
    }
}
