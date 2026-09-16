#if UNITY_EDITOR
using ArknightsACT.Editor.PRTS;
using ArknightsACT.Gameplay.Roguelite.Rewards;
using ArknightsACT.Gameplay.Roguelite.Routing;
using ArknightsACT.Gameplay.Roguelite.World;
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor
{
    internal static class PrototypeStageRuntimeFactory
    {
        private const string MaterialRoot = "Assets/_Game/Data/Prototype25D/Materials";

        public static GameObject Create(
            Transform player,
            GameObject[] enemyTemplates,
            GameObject[] treasureTemplates,
            GameObject rogueliteRoot)
        {
            if (player == null || rogueliteRoot == null)
                return null;

            var runState = rogueliteRoot.GetComponent<RogueliteRunState>();
            var stageMap = rogueliteRoot.GetComponent<RogueliteStageMapController>();
            var rewards = rogueliteRoot.GetComponent<RogueliteRewardController>();
            if (runState == null || stageMap == null || rewards == null)
            {
                Debug.LogError("[ArknightsACT/StageRuntime] Roguelite root is missing run/map/reward components.", rogueliteRoot);
                return null;
            }

            // The selected visual direction now has a persistent modular asset kit. Ensure it exists
            // before the generated scene serializes references to its meshes/materials/prefabs, then
            // enrich the most visible modules with the current production-detail pass.
            var environmentKit = ChernobogEnvironmentKitBuilder.EnsureBuilt();
            ChernobogProductionDetailPass.EnsureApplied(environmentKit);

            var go = new GameObject("[StageRuntime]");
            var controller = go.AddComponent<RogueliteStageRuntimeController>();
            controller.Configure(
                player,
                runState,
                stageMap,
                rewards,
                enemyTemplates,
                treasureTemplates,
                Load("Ground_Tactical"),
                Load("Road_Tactical"),
                Load("Sidewalk_Tactical"),
                Load("Facility_Wall"),
                Load("Facility_Floor"),
                Load("CombatCover"),
                Load("TacticalAccent"),
                Load("HazardBand"));

            // Keep route/encounter code stable. Presentation is layered deliberately:
            // physical floor/layout -> palette -> legacy base -> Concept-01 placement hints ->
            // persistent modular assets -> bevel fallback -> art-direction rhythm -> PBR/lighting ->
            // real pit binding -> iconic terrain -> optional PRTS backdrop.
            go.AddComponent<RogueliteStageLayoutController>().Configure(stageMap);
            go.AddComponent<RogueliteStagePaletteController>().Configure(stageMap);
            go.AddComponent<RogueliteStageAuthenticityController>().Configure(stageMap);
            go.AddComponent<RogueliteStageConceptOneController>().Configure(stageMap);
            go.AddComponent<RogueliteStageModularKitController>().Configure(stageMap, environmentKit);
            go.AddComponent<RogueliteStageMeshUpgradeController>().Configure(stageMap);
            go.AddComponent<RogueliteStageArtDirectionController>().Configure(stageMap);
            go.AddComponent<RogueliteStageQualityPassController>().Configure(stageMap);
            go.AddComponent<RoguelitePitFloorSyncController>().Configure(stageMap);
            go.AddComponent<RogueliteStageTerrainPresentationController>().Configure(stageMap);
            go.AddComponent<RogueliteStageBackdropReferenceController>().Configure(stageMap, LoadEnvironmentBackdrops());
            return go;
        }

        private static Texture2D[] LoadEnvironmentBackdrops()
        {
            var references = PrtsEnvironmentReferenceCatalog.RuntimeBackdrops;
            var textures = new Texture2D[references.Length];
            for (var i = 0; i < references.Length; i++)
                textures[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(references[i].LocalPath);
            return textures;
        }

        private static Material Load(string name)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialRoot}/{name}.mat");
            if (material == null)
                Debug.LogWarning($"[ArknightsACT/StageRuntime] Material missing: {name}. Rebuild the 2.5D scene materials.");
            return material;
        }
    }
}
#endif
