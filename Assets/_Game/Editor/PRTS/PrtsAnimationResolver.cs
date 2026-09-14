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

            var idle = FirstByPrefixes(names, "idle", "relax", "default", "stand")
                       ?? names.FirstOrDefault()
                       ?? string.Empty;

            var move = FirstByPrefixes(names, "move", "run", "walk") ?? idle;

            var attacks = names
                .Where(name => StartsWithAny(name, "attack", "combat", "atk"))
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .Take(8)
                .ToArray();
            if (attacks.Length == 0)
                attacks = new[] { idle };

            var skill = FirstByPrefixes(names, "skill", "ability", "special") ?? attacks[0];
            var hit = FirstByPrefixes(names, "hit", "hurt", "stun", "damage") ?? string.Empty;
            var die = FirstByPrefixes(names, "die", "death", "dead") ?? idle;
            return new ResolvedAnimations(idle, move, attacks, skill, hit, die);
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
