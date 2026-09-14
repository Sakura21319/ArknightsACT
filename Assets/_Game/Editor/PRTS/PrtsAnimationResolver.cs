#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;

namespace ArknightsACT.Editor.PRTS
{
    internal static class PrtsAnimationResolver
    {
        public readonly struct ResolvedAnimations
        {
            public readonly string Idle;
            public readonly string Move;
            public readonly string[] Attacks;
            public readonly string Skill;
            public readonly string Hit;
            public readonly string Die;

            public ResolvedAnimations(string idle, string move, string[] attacks, string skill, string hit, string die)
            {
                Idle = idle;
                Move = move;
                Attacks = attacks;
                Skill = skill;
                Hit = hit;
                Die = die;
            }
        }

        public static ResolvedAnimations Resolve(IEnumerable<string> animationNames)
        {
            var names = animationNames?
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray() ?? Array.Empty<string>();

            var idle = ResolveIdle(names) ?? names.FirstOrDefault() ?? string.Empty;
            var move = ResolvePersistentMove(names) ?? idle;
            var attacks = ResolveBasicAttacks(names, idle);
            var skill = Exact(names, "Skill")
                        ?? FirstByPrefixes(names, "skill", "ability", "special")
                        ?? attacks[0];
            var hit = Exact(names, "Hit")
                      ?? FirstByPrefixes(names, "hit", "hurt", "stun", "damage")
                      ?? string.Empty;
            var die = Exact(names, "Die")
                      ?? FirstByPrefixes(names, "die", "death", "dead")
                      ?? idle;

            return new ResolvedAnimations(idle, move, attacks, skill, hit, die);
        }

        private static string ResolveIdle(IReadOnlyList<string> names)
        {
            return Exact(names, "Idle")
                   ?? names.FirstOrDefault(name => StartsWithAny(name, "idle") && IsLoopLike(name))
                   ?? FirstByPrefixes(names, "idle", "relax")
                   ?? Exact(names, "Default")
                   ?? FirstByPrefixes(names, "default", "stand");
        }

        private static string ResolvePersistentMove(IReadOnlyList<string> names)
        {
            // A persistent locomotion state must never resolve to Move_Begin / Move_End /
            // Move_Up / Move_Down. Those are transition or directional clips in Arknights data.
            return Exact(names, "Move")
                   ?? Exact(names, "Move_Loop")
                   ?? Exact(names, "Run_Loop")
                   ?? Exact(names, "Walk_Loop")
                   ?? names.FirstOrDefault(name =>
                       StartsWithAny(name, "move", "run", "walk") && IsLoopLike(name))
                   ?? names.FirstOrDefault(name =>
                       StartsWithAny(name, "move", "run", "walk") && !IsTransitionClip(name));
        }

        private static string[] ResolveBasicAttacks(IReadOnlyList<string> names, string idle)
        {
            // Operators such as Texas expose Attack_Start / Attack_Loop / Attack_End.
            // These are phases of ONE attack state, not a three-hit combo. For an ACT press,
            // use the repeatable core clip and keep gameplay combo counting separate.
            var attackLoop = Exact(names, "Attack_Loop");
            if (!string.IsNullOrWhiteSpace(attackLoop))
                return new[] { attackLoop };

            var exactAttack = Exact(names, "Attack") ?? Exact(names, "Combat");
            if (!string.IsNullOrWhiteSpace(exactAttack))
                return new[] { exactAttack };

            var candidates = names
                .Where(name => StartsWithAny(name, "attack", "combat", "atk"))
                .Where(name => !IsTransitionClip(name))
                .OrderByDescending(IsLoopLike)
                .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
                .Take(4)
                .ToArray();

            return candidates.Length > 0 ? candidates : new[] { idle };
        }

        private static bool IsTransitionClip(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            var normalized = name.Replace('-', '_').ToLowerInvariant();
            return normalized.Contains("_start") || normalized.Contains("_begin") ||
                   normalized.Contains("_end") || normalized.Contains("_finish") ||
                   normalized.Contains("_up") || normalized.Contains("_down");
        }

        private static bool IsLoopLike(string name)
        {
            return !string.IsNullOrWhiteSpace(name) &&
                   name.IndexOf("loop", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string Exact(IEnumerable<string> names, string value)
        {
            return names.FirstOrDefault(name => string.Equals(name, value, StringComparison.OrdinalIgnoreCase));
        }

        private static string FirstByPrefixes(IEnumerable<string> names, params string[] prefixes)
        {
            foreach (var name in names)
            {
                if (StartsWithAny(name, prefixes))
                    return name;
            }
            return null;
        }

        private static bool StartsWithAny(string name, params string[] prefixes)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            var normalized = name.Trim().Replace('-', '_');
            for (var i = 0; i < prefixes.Length; i++)
            {
                if (normalized.StartsWith(prefixes[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
#endif
