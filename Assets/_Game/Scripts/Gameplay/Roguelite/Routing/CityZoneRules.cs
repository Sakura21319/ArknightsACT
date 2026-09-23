using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.Routing
{
    public enum CityZone { Outskirts, Core, Ruins, Industrial }
    public static class CityZoneRules
    {
        public static CityZone Resolve(Vector2Int cell, int width, int height)
        {
            if (cell == Vector2Int.zero) return CityZone.Outskirts;
            if (Mathf.Abs(cell.x - (width - 1) * .5f) <= .55f && Mathf.Abs(cell.y - (height - 1) * .5f) <= .55f) return CityZone.Core;
            if (cell.y == 0) return CityZone.Ruins;
            if (cell.x == width - 1) return CityZone.Industrial;
            return CityZone.Outskirts;
        }
        public static string Label(CityZone zone) => zone switch { CityZone.Core => "核心区", CityZone.Ruins => "南部废墟", CityZone.Industrial => "东部工业区", _ => "外围防线" };
        public static Color Color(CityZone zone) => zone switch
        {
            CityZone.Core => new Color(.60f, .28f, .16f),
            CityZone.Ruins => new Color(.36f, .30f, .27f),
            CityZone.Industrial => new Color(.29f, .38f, .43f),
            _ => new Color(.28f, .40f, .36f)
        };
        public static float HealthMultiplier(CityZone zone) => zone == CityZone.Core ? 1.55f : zone == CityZone.Industrial ? 1.15f : 1f;
        public static int ExtraEnemies(CityZone zone) => zone == CityZone.Core ? 2 : 0;
    }
}
