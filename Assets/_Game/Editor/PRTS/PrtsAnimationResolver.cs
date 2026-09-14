#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ArknightsACT.Editor.PRTS
{
    internal static class PrtsAnimationResolver
    {
        private static readonly Regex Idle = new("^((Id.?le)|(Relax)|(Default)).{0,2}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex Move = new("^Move.{0,2}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex Attack = new("^((Attack)|(Combat)).{0,2}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex Skill = new("^Skill.{0,2}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex Hit = new("^((Hit)|(Hurt)|(Stun)).*", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex Die = new("^Die.{0,2}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

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

            var idle = First(names, Idle) ?? names.FirstOrDefault() ?? string.Empty;
            var move = First(names, Move) ?? idle;
            var attacks = names.Where(name => Attack.IsMatch(name)).Take(6).ToArray();
            if (attacks.Length == 0)
                attacks = new[] { idle };

            var skill = First(names, Skill) ?? attacks[0];
            var hit = First(names, Hit) ?? string.Empty;
            var die = First(names, Die) ?? idle;
            return new ResolvedAnimations(idle, move, attacks, skill, hit, die);
        }

        private static string First(IEnumerable<string> names, Regex regex)
        {
            foreach (var name in names)
            {
                if (regex.IsMatch(name))
                    return name;
            }
            return null;
        }
    }
}
#endif
