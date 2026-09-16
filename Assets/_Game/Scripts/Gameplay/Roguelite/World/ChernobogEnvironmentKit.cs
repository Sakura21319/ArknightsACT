using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// Persistent modular environment library generated in the Editor.
    /// Runtime stage generation consumes these reusable modules instead of assembling every visual
    /// detail from raw Unity primitives.
    /// </summary>
    [CreateAssetMenu(menuName = "ArknightsACT/Environment/Chernobog Modular Kit", fileName = "ChernobogEnvironmentKit")]
    public sealed class ChernobogEnvironmentKit : ScriptableObject
    {
        [Header("Floor")]
        public Mesh floorPlateMesh;
        public Mesh floorPlateHeavyMesh;
        public GameObject floorGrate;
        public GameObject floorServiceHatch;

        [Header("Walls")]
        public GameObject wallVent;
        public GameObject wallSolid;
        public GameObject wallCorner;
        public GameObject wallLow;

        [Header("Gameplay cover")]
        public GameObject hvacSmall;
        public GameObject hvacMedium;
        public GameObject hvacLarge;
        public GameObject electricalCabinet;

        [Header("Infrastructure")]
        public GameObject pipeRun;
        public GameObject catwalk;
        public GameObject supportBeam;
        public GameObject pitFrame;

        [Header("Materials")]
        public Material deckMaterial;
        public Material deckHeavyMaterial;
        public Material wallMaterial;
        public Material insetMaterial;
        public Material steelMaterial;
        public Material grateMaterial;
        public Material accentMaterial;
        public Material emissiveMaterial;

        public bool IsUsable =>
            floorPlateMesh != null &&
            wallVent != null &&
            hvacSmall != null &&
            hvacMedium != null &&
            wallMaterial != null &&
            deckMaterial != null;
    }
}
