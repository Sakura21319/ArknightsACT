using System;
using ArknightsACT.Gameplay.Roguelite.Routing;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// Shared runtime references for stage presentation controllers.
    /// Keeps visual controllers from repeatedly searching scene objects.
    /// </summary>
    public sealed class RogueliteStageRuntimeContext : MonoBehaviour
    {
        [SerializeField] private RogueliteStageMapController stageMap;
        [SerializeField] private RogueliteRunState runState;
        [SerializeField] private ChernobogEnvironmentKit environmentKit;

        public RogueliteStageMapController StageMap => stageMap;
        public RogueliteRunState RunState => runState;
        public ChernobogEnvironmentKit EnvironmentKit => environmentKit;
        public Transform StageRoot { get; private set; }

        public event Action<Transform> StageRootChanged;

        public void Configure(
            RogueliteStageMapController stageMap,
            ChernobogEnvironmentKit environmentKit,
            Transform stageRoot = null,
            RogueliteRunState runState = null)
        {
            this.stageMap = stageMap;
            this.runState = runState;
            this.environmentKit = environmentKit;
            SetStageRoot(stageRoot);
        }

        public void SetStageRoot(Transform stageRoot)
        {
            if (StageRoot == stageRoot)
                return;

            StageRoot = stageRoot;
            StageRootChanged?.Invoke(stageRoot);
        }
    }
}
