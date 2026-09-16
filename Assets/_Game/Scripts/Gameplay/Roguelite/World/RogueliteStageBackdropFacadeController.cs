using System;
using ArknightsACT.Gameplay.Roguelite.Routing;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// Replaces the old giant-box Concept-01 skyline with an assembled modular mobile-city facade.
    /// Everything is visual-only and kept north/east of the playable footprint so the south-west
    /// gameplay camera reads architecture behind the fight instead of through a foreground slab.
    /// </summary>
    [DefaultExecutionOrder(16)]
    [DisallowMultipleComponent]
    public sealed class RogueliteStageBackdropFacadeController : MonoBehaviour
    {
        private const float ChunkWidth = 14f;
        private const float ChunkDepth = 11f;
        private const float WallModuleWidth = 2.4f;
        private const float WallModuleHeight = 1.55f;

        [SerializeField] private RogueliteStageMapController stageMap;
        [SerializeField] private ChernobogEnvironmentKit kit;

        private GameObject _preparedStage;
        private float _nextResolveAt;

        public void Configure(RogueliteStageMapController map, ChernobogEnvironmentKit environmentKit)
        {
            stageMap = map;
            kit = environmentKit;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextResolveAt)
                return;
            _nextResolveAt = Time.unscaledTime + 0.14f;

            stageMap ??= FindFirstObjectByType<RogueliteStageMapController>();
            if (stageMap == null || kit == null || !kit.IsUsable)
                return;

            var stage = GameObject.Find($"[Stage_{stageMap.StageIndex:00}_Runtime]");
            if (stage == null || stage == _preparedStage)
                return;

            var modular = stage.transform.Find("[Chernobog_ModularKit]");
            if (modular == null)
                return;

            HideLegacyEdgeArchitecture(stage.transform);
            BuildBackdrop(stage.transform);
            _preparedStage = stage;

            Debug.Log(
                $"[ArknightsACT/BackdropFacade] Stage {stageMap.StageIndex}: modular north/east mobile-city facade assembled.",
                this);
        }

        private static void HideLegacyEdgeArchitecture(Transform stage)
        {
            var legacy = stage.Find("[Concept01_ChernobogDeckKit]/EdgeArchitecture");
            if (legacy != null)
                legacy.gameObject.SetActive(false);
        }

        private void BuildBackdrop(Transform stage)
        {
            var old = stage.Find("[Chernobog_BackdropFacadeKit]");
            if (old != null)
                Destroy(old.gameObject);

            var root = new GameObject("[Chernobog_BackdropFacadeKit]").transform;
            root.SetParent(stage, false);

            var width = stageMap.Width * ChunkWidth;
            var depth = stageMap.Height * ChunkDepth;
            var stageExtra = Mathf.Clamp(stageMap.StageIndex - 1, 0, 2);

            // One dominant north service block anchors the location. It is built from the exact same
            // wall/equipment kit as the gameplay boundary, which makes the scene feel like one place.
            var mainColumns = Mathf.Clamp(4 + stageMap.Width, 6, 8);
            var mainRows = 3 + stageExtra;
            BuildFacadeGrid(
                root,
                "North_MainFacility",
                new Vector3(-width * 0.14f, 0f, depth * 0.5f + 4.55f),
                Quaternion.LookRotation(Vector3.back, Vector3.up),
                mainColumns,
                mainRows,
                true,
                true);

            // Smaller utility block creates an uneven roofline rather than a symmetric wall of boxes.
            BuildFacadeGrid(
                root,
                "North_UtilityAnnex",
                new Vector3(width * 0.34f, 0f, depth * 0.5f + 3.35f),
                Quaternion.LookRotation(Vector3.back, Vector3.up),
                2,
                2 + stageExtra,
                false,
                false);

            // East-side tower frames the far right edge without entering the camera foreground.
            BuildFacadeGrid(
                root,
                "East_ServiceTower",
                new Vector3(width * 0.5f + 3.45f, 0f, depth * 0.14f),
                Quaternion.LookRotation(Vector3.left, Vector3.up),
                2,
                3 + stageExtra,
                true,
                false);

            BuildRoofEquipment(root, width, depth, mainRows);
            BuildServiceCatwalk(root, width, depth, mainRows);
        }

        private void BuildFacadeGrid(
            Transform parent,
            string name,
            Vector3 position,
            Quaternion rotation,
            int columns,
            int rows,
            bool useVentPattern,
            bool hasServiceDoor)
        {
            var facade = new GameObject(name).transform;
            facade.SetParent(parent, false);
            facade.localPosition = position;
            facade.localRotation = rotation;

            var totalWidth = columns * WallModuleWidth;
            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    var centerDoor = hasServiceDoor && row == 0 && column == columns / 2;
                    var vent = useVentPattern && !centerDoor && PositiveMod(column + row * 2 + stageMap.StageIndex, 4) != 1;
                    var prefab = vent ? kit.wallVent : kit.wallSolid;
                    if (prefab == null)
                        prefab = kit.wallVent;
                    if (prefab == null)
                        continue;

                    var instance = Instantiate(prefab, facade);
                    instance.name = $"Facade_{row:00}_{column:00}";
                    instance.transform.localPosition = new Vector3(
                        -totalWidth * 0.5f + WallModuleWidth * (column + 0.5f),
                        row * WallModuleHeight,
                        0f);
                    instance.transform.localRotation = Quaternion.identity;

                    TuneFacadeBay(instance.transform, row, column, centerDoor);
                }
            }

            // Structural roof band and side pylons use reusable support/corner modules.
            if (kit.supportBeam != null)
            {
                var roofY = rows * WallModuleHeight + 0.10f;
                var beamCount = Mathf.Max(1, Mathf.CeilToInt(totalWidth / 2.6f));
                for (var i = 0; i < beamCount; i++)
                {
                    var beam = Instantiate(kit.supportBeam, facade);
                    beam.name = $"RoofBeam_{i:00}";
                    beam.transform.localPosition = new Vector3(
                        -totalWidth * 0.5f + totalWidth * (i + 0.5f) / beamCount,
                        roofY,
                        -0.03f);
                    beam.transform.localRotation = Quaternion.identity;
                    beam.transform.localScale = new Vector3((totalWidth / beamCount) / 2.6f, 1f, 1f);
                }
            }

            if (kit.wallCorner != null)
            {
                var left = Instantiate(kit.wallCorner, facade);
                left.name = "FacadePylon_L";
                left.transform.localPosition = new Vector3(-totalWidth * 0.5f - 0.05f, 0f, 0f);
                left.transform.localRotation = Quaternion.identity;

                var right = Instantiate(kit.wallCorner, facade);
                right.name = "FacadePylon_R";
                right.transform.localPosition = new Vector3(totalWidth * 0.5f + 0.05f, 0f, 0f);
                right.transform.localRotation = Quaternion.identity;
            }
        }

        private void TuneFacadeBay(Transform module, int row, int column, bool serviceDoor)
        {
            var serviceLight = FindDescendant(module, "ServiceLight");
            if (serviceLight != null)
                serviceLight.gameObject.SetActive(row == 0 && PositiveMod(column + stageMap.StageIndex, 5) == 1);

            var id = FindDescendant(module, "Optional_IDStrip");
            if (id != null)
                id.gameObject.SetActive(row == 0 && PositiveMod(column + stageMap.StageIndex, 6) == 3);

            var conduit = FindDescendant(module, "Optional_Conduit");
            if (conduit != null)
                conduit.gameObject.SetActive(PositiveMod(column + row + stageMap.StageIndex, 5) == 0);

            var serviceBay = FindDescendant(module, "ServiceBay");
            if (serviceBay != null)
                serviceBay.gameObject.SetActive(!serviceDoor && PositiveMod(column + row, 3) != 1);

            if (!serviceDoor)
                return;

            // The solid center bay reads as a service door using existing materials/geometry rather
            // than a painted symbol. Door trim is borrowed from small reusable modules nearby.
            if (kit.electricalCabinet != null)
            {
                var cabinet = Instantiate(kit.electricalCabinet, module);
                cabinet.name = "DoorSideElectrical";
                cabinet.transform.localPosition = new Vector3(0.72f, 0f, -0.28f);
                cabinet.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                cabinet.transform.localScale = new Vector3(0.72f, 0.72f, 0.72f);
            }
        }

        private void BuildRoofEquipment(Transform root, float width, float depth, int mainRows)
        {
            var roofY = mainRows * WallModuleHeight + 0.22f;
            if (kit.hvacLarge != null)
            {
                Spawn(root, kit.hvacLarge,
                    new Vector3(-width * 0.26f, roofY, depth * 0.5f + 4.25f),
                    Quaternion.identity, "North_RoofHVAC_A");
                Spawn(root, kit.hvacLarge,
                    new Vector3(-width * 0.06f, roofY, depth * 0.5f + 4.30f),
                    Quaternion.Euler(0f, 180f, 0f), "North_RoofHVAC_B");
            }

            if (kit.pipeRun != null)
            {
                Spawn(root, kit.pipeRun,
                    new Vector3(-width * 0.18f, roofY + 0.25f, depth * 0.5f + 5.35f),
                    Quaternion.identity, "North_RoofPipe_A");
                Spawn(root, kit.pipeRun,
                    new Vector3(width * 0.02f, roofY + 0.25f, depth * 0.5f + 5.35f),
                    Quaternion.identity, "North_RoofPipe_B");
            }
        }

        private void BuildServiceCatwalk(Transform root, float width, float depth, int mainRows)
        {
            if (kit.catwalk == null)
                return;

            var y = Mathf.Max(3.0f, mainRows * WallModuleHeight * 0.72f);
            var z = depth * 0.5f + 2.35f;
            var segments = Mathf.Clamp(stageMap.Width + 1, 3, 5);
            for (var i = 0; i < segments; i++)
            {
                var catwalk = Instantiate(kit.catwalk, root);
                catwalk.name = $"North_ServiceCatwalk_{i:00}";
                catwalk.transform.localPosition = new Vector3(
                    -width * 0.34f + i * 3.0f,
                    y,
                    z);
                catwalk.transform.localRotation = Quaternion.identity;
            }

            if (kit.supportBeam == null)
                return;

            for (var i = 0; i <= segments; i += 2)
            {
                var support = Instantiate(kit.supportBeam, root);
                support.name = $"CatwalkSupport_{i:00}";
                support.transform.localPosition = new Vector3(
                    -width * 0.34f + Mathf.Min(i, segments - 1) * 3.0f,
                    y * 0.5f,
                    z + 0.18f);
                support.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                support.transform.localScale = new Vector3(y / 2.6f, 1f, 1f);
            }
        }

        private static GameObject Spawn(Transform parent, GameObject prefab, Vector3 localPosition, Quaternion localRotation, string name)
        {
            var instance = Instantiate(prefab, parent);
            instance.name = name;
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = localRotation;
            return instance;
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            if (root == null)
                return null;

            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                var current = transforms[i];
                if (current != null && string.Equals(current.name, name, StringComparison.Ordinal))
                    return current;
            }
            return null;
        }

        private static int PositiveMod(int value, int divisor)
        {
            var result = value % divisor;
            return result < 0 ? result + divisor : result;
        }
    }
}
