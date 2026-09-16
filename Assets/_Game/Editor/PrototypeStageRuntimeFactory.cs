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

            var environmentKit = ChernobogEnvironmentKitBuilder.EnsureBuilt();
            ChernobogFloorProductionPass.EnsureApplied(environmentKit);
            ChernobogMaterialProductionPass.EnsureApplied(environmentKit);
            ChernobogMaterialFineDetailPass.EnsureApplied(environmentKit);
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

            // physical floor/layout -> district assignment -> modular presentation -> industrial shell ->
            // playable rooms/decks -> district-specific streets/facades -> legacy density cleanup -> dressing
            // collision -> lighting/chassis/deep base -> distant city/horizon -> containment -> x-ray visibility.
            go.AddComponent<RogueliteStageLayoutController>().Configure(stageMap);
            go.AddComponent<RogueliteStageExpansionController>().Configure(stageMap);
            go.AddComponent<RogueliteStageDistrictTemplateController>().Configure(stageMap);
            go.AddComponent<RogueliteStageHazardPolicyController>().Configure(stageMap);
            go.AddComponent<RogueliteStagePaletteController>().Configure(stageMap);
            go.AddComponent<RogueliteStageAuthenticityController>().Configure(stageMap);
            go.AddComponent<RogueliteStageConceptOneController>().Configure(stageMap);
            go.AddComponent<RogueliteStageModularKitController>().Configure(stageMap, environmentKit);
            go.AddComponent<RogueliteStageFloorCompositionController>().Configure(stageMap, environmentKit);
            go.AddComponent<RogueliteStageMeshUpgradeController>().Configure(stageMap);
            go.AddComponent<RogueliteStageBackdropFacadeController>().Configure(stageMap, environmentKit);
            go.AddComponent<RogueliteStageArtDirectionController>().Configure(stageMap);
            go.AddComponent<RogueliteStageQualityPassController>().Configure(stageMap);
            go.AddComponent<RogueliteStageMaterialVariationController>().Configure(stageMap);
            go.AddComponent<RogueliteStageSetDressingController>().Configure(stageMap, environmentKit);
            go.AddComponent<RogueliteStageUrbanCompositionController>().Configure(stageMap, environmentKit);
            go.AddComponent<RogueliteStagePlayableArchitectureController>().Configure(stageMap, environmentKit);
            go.AddComponent<RogueliteStageCityStreetsController>().Configure(stageMap, environmentKit);
            go.AddComponent<RogueliteStageUrbanDensityController>().Configure(stageMap);
            go.AddComponent<RogueliteStageCompositionCleanupController>().Configure(stageMap);
            go.AddComponent<RogueliteStageDressingCollisionController>().Configure(stageMap);
            go.AddComponent<RogueliteStageLightingController>().Configure(stageMap);
            go.AddComponent<RogueliteMobileCityChassisController>().Configure(stageMap, environmentKit);
            go.AddComponent<RogueliteMobileCityDeepBaseController>().Configure(stageMap, environmentKit);
            go.AddComponent<RogueliteStageDistantDistrictController>().Configure(stageMap, environmentKit);
            go.AddComponent<RogueliteStageHorizonCityController>().Configure(stageMap, environmentKit);
            go.AddComponent<RogueliteStageContainmentController>().Configure(stageMap);
            go.AddComponent<RogueliteStageActorOcclusionController>().Configure(stageMap);
            go.AddComponent<RogueliteStageTerrainPresentationController>().Configure(stageMap);
            go.AddComponent<RogueliteActiveOriginiumPresentationController>().Configure(stageMap);
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