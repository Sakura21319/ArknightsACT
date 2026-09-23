using UnityEngine;

namespace ArknightsACT.Combat
{
    public static class CombatActionUtility
    {
        public static bool IsBlocked(CombatEntity entity, CombatActionMask action)
        {
            if (entity == null || action == CombatActionMask.None)
                return false;

            var behaviours = entity.GetComponents<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is ICombatActionBlockSource source &&
                    (source.BlockedActions & action) != 0)
                    return true;
            }

            return false;
        }

        public static void Interrupt(CombatEntity entity, CombatActionMask actions)
        {
            if (entity == null || actions == CombatActionMask.None)
                return;

            var behaviours = entity.GetComponents<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is ICombatActionInterruptHandler handler)
                    handler.InterruptCombatActions(actions);
            }
        }
    }
}
