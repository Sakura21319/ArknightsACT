using System;

namespace ArknightsACT.Gameplay.Characters.FrostNova
{
    public enum FrostNovaSkinVariant
    {
        Default,
        Winter,
        DefaultNew,
        WinterNew
    }

    public static class FrostNovaSkinVariantExtensions
    {
        public static bool UsesWinterSkillSet(this FrostNovaSkinVariant skin) =>
            skin == FrostNovaSkinVariant.Winter ||
            skin == FrostNovaSkinVariant.WinterNew;

        public static bool UsesNewSpine(this FrostNovaSkinVariant skin) =>
            skin == FrostNovaSkinVariant.DefaultNew ||
            skin == FrostNovaSkinVariant.WinterNew;

        public static string ToSkinId(this FrostNovaSkinVariant skin) =>
            skin switch
            {
                FrostNovaSkinVariant.Winter => "winter#1",
                FrostNovaSkinVariant.DefaultNew => "new#1",
                FrostNovaSkinVariant.WinterNew => "winter_new#1",
                _ => "default"
            };

        public static FrostNovaSkinVariant FromSkinId(string skinId)
        {
            if (string.Equals(skinId, "winter#1", StringComparison.OrdinalIgnoreCase))
                return FrostNovaSkinVariant.Winter;
            if (string.Equals(skinId, "new#1", StringComparison.OrdinalIgnoreCase))
                return FrostNovaSkinVariant.DefaultNew;
            if (string.Equals(skinId, "winter_new#1", StringComparison.OrdinalIgnoreCase))
                return FrostNovaSkinVariant.WinterNew;
            return FrostNovaSkinVariant.Default;
        }
    }
}
