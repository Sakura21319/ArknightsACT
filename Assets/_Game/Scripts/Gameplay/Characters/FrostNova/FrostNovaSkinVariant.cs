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
        // Runtime FrostNova has been consolidated to the Winter (冬痕) presentation. Legacy enum
        // values are intentionally kept so old serialized scenes/assets still deserialize, but every
        // runtime/editor request converges on the single supported skin instead of reviving old variants.
        public static bool UsesWinterSkillSet(this FrostNovaSkinVariant skin) => true;

        public static bool UsesNewSpine(this FrostNovaSkinVariant skin) => false;

        public static string ToSkinId(this FrostNovaSkinVariant skin) => "winter#1";

        public static FrostNovaSkinVariant FromSkinId(string skinId) => FrostNovaSkinVariant.Winter;
    }
}
