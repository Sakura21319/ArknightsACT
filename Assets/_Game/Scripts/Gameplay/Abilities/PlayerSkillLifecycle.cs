using UnityEngine;

namespace ArknightsACT.Gameplay.Abilities
{
    public enum PlayerSkillLifecycleType
    {
        Detached = 0,
        Permanent = 1,
        Duration = 2,
        Ammo = 3
    }

    /// <summary>
    /// Optional runtime lifecycle contract layered on top of IPlayerSkill.
    /// Detached skills do not need to implement this interface.
    /// Permanent / Duration / Ammo skills should expose their active state through it.
    /// </summary>
    public interface IPlayerSkillLifecycleState : IPlayerSkillActiveState
    {
        PlayerSkillLifecycleType LifecycleType { get; }

        /// <summary>
        /// Total authored active duration for Duration skills. Zero for other lifecycle types.
        /// </summary>
        float ActiveDurationSeconds { get; }

        /// <summary>
        /// Runtime remaining duration for Duration skills. Zero for inactive/non-duration skills.
        /// </summary>
        float ActiveSecondsRemaining { get; }

        /// <summary>
        /// Current ammo count for Ammo skills. Zero for other lifecycle types.
        /// </summary>
        int AmmoRemaining { get; }

        /// <summary>
        /// Maximum ammo count granted when an Ammo skill activates.
        /// </summary>
        int AmmoCapacity { get; }
    }

    /// <summary>
    /// Implement on ammo skills that should spend one round when a basic attack starts.
    /// Other ammo skills can keep this false and consume ammunition from their own gameplay event.
    /// </summary>
    public interface IPlayerSkillAmmoConsumer : IPlayerSkillLifecycleState
    {
        bool ConsumeAmmoOnBasicAttackStarted { get; }
        bool TryConsumeAmmo();
    }

    public static class PlayerSkillLifecycleUtility
    {
        public static PlayerSkillLifecycleType GetLifecycleType(IPlayerSkill skill)
        {
            if (skill is IPlayerSkillLifecycleState lifecycle)
                return lifecycle.LifecycleType;

            // Backward-compatible interpretation for old active-state skills that have not yet
            // migrated to the explicit lifecycle contract.
            if (skill is IPlayerSkillActiveState)
                return PlayerSkillLifecycleType.Duration;

            return PlayerSkillLifecycleType.Detached;
        }

        public static bool IsActive(IPlayerSkill skill) =>
            skill is IPlayerSkillActiveState active && active.IsActive;

        public static bool IsReady(IPlayerSkill skill) =>
            skill != null &&
            !skill.IsCasting &&
            !IsActive(skill) &&
            skill.SkillPointRatio >= 0.999f;

        public static bool CanReceiveSkillPoints(IPlayerSkill skill) =>
            skill != null && !IsActive(skill);

        public static string GetActiveHudValue(IPlayerSkill skill)
        {
            if (skill is not IPlayerSkillLifecycleState lifecycle || !lifecycle.IsActive)
                return string.Empty;

            switch (lifecycle.LifecycleType)
            {
                case PlayerSkillLifecycleType.Permanent:
                    return "ACTIVE";
                case PlayerSkillLifecycleType.Duration:
                    if (float.IsPositiveInfinity(lifecycle.ActiveSecondsRemaining))
                        return "ACTIVE";
                    return lifecycle.ActiveSecondsRemaining > 0f
                        ? $"{lifecycle.ActiveSecondsRemaining:0.0}s"
                        : "ACTIVE";
                case PlayerSkillLifecycleType.Ammo:
                    return $"{Mathf.Max(0, lifecycle.AmmoRemaining)} / {Mathf.Max(0, lifecycle.AmmoCapacity)}";
                default:
                    return string.Empty;
            }
        }

        public static string GetLifecycleLabel(IPlayerSkill skill)
        {
            return GetLifecycleType(skill) switch
            {
                PlayerSkillLifecycleType.Permanent => "永久",
                PlayerSkillLifecycleType.Duration => "持续",
                PlayerSkillLifecycleType.Ammo => "弹药",
                _ => string.Empty
            };
        }
    }
}
