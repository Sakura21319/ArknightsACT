using System;
using System.Collections.Generic;
using ArknightsACT.Gameplay.Roguelite.Routing;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// Reuses the generated procedural textures but retunes their material response toward the
    /// colder, darker Chernobog combat-stage palette. Material names are intentionally preserved so
    /// later environment passes can continue resolving them without coupling to this controller.
    ///
    /// This component is present in already-generated PrototypeRun scenes, so it also bootstraps the
    /// selected Concept-01, mesh-upgrade and quality passes. That means pulling code is enough to get
    /// the current presentation; rebuilding the scene is still recommended, but no longer required
    /// merely to add the new visual components.
    /// </summary>
    [DefaultExecutionOrder(5)]
    [DisallowMultipleComponent]
    public sealed class RogueliteStagePaletteController : MonoBehaviour
    {
        [SerializeField] private RogueliteStageMapController stageMap;

        private RogueliteStageRuntimeContext _context;
        private readonly Dictionary<string, Material> _runtimePalette = new(StringComparer.OrdinalIgnoreCase);
        private GameObject _preparedStage;
        private float _nextResolveAt;

        public void Configure(RogueliteStageMapController map)
        {
            _context ??= GetComponent<RogueliteStageRuntimeContext>();
            stageMap = map;
        }

        private void Awake()
        {
            _context = GetComponent<RogueliteStageRuntimeContext>();
            if (_context != null)
                stageMap ??= _context.StageMap;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextResolveAt)
                return;
            _nextResolveAt = Time.unscaledTime + 0.10f;

            if (_context == null)
                return;
            stageMap ??= _context.StageMap;
            if (stageMap == null)
                return;

            EnsureCurrentVisualPasses();

            var stage = _context.StageRoot != null ? _context.StageRoot.gameObject : null;
            if (stage == null || stage == _preparedStage)
                return;

            ApplyPalette(stage);
            _preparedStage = stage;
        }

        private void EnsureCurrentVisualPasses()
        {
            var concept = GetComponent<RogueliteStageConceptOneController>();
            if (concept == null)
                concept = gameObject.AddComponent<RogueliteStageConceptOneController>();
            concept.Configure(stageMap);

            var meshUpgrade = GetComponent<RogueliteStageMeshUpgradeController>();
            if (meshUpgrade == null)
                meshUpgrade = gameObject.AddComponent<RogueliteStageMeshUpgradeController>();
            meshUpgrade.Configure(stageMap);

            var quality = GetComponent<RogueliteStageQualityPassController>();
            if (quality == null)
                quality = gameObject.AddComponent<RogueliteStageQualityPassController>();
            quality.Configure(stageMap);
        }

        private void OnDestroy()
        {
            foreach (var pair in _runtimePalette)
            {
                if (pair.Value != null)
                    Destroy(pair.Value);
            }
            _runtimePalette.Clear();
        }

        private void ApplyPalette(GameObject stage)
        {
            var renderers = stage.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || renderer.sharedMaterial == null)
                    continue;

                var source = renderer.sharedMaterial;
                var replacement = ResolveReplacement(source);
                if (replacement != null)
                    renderer.sharedMaterial = replacement;
            }
        }

        private Material ResolveReplacement(Material source)
        {
            if (source == null)
                return null;
            if (_runtimePalette.TryGetValue(source.name, out var existing))
                return existing;

            Material replacement = source.name switch
            {
                "Ground_Tactical" => Clone(source, new Color(0.225f, 0.245f, 0.278f), 0.08f, 0.12f),
                "Road_Tactical" => Clone(source, new Color(0.115f, 0.128f, 0.150f), 0.04f, 0.09f),
                "Sidewalk_Tactical" => Clone(source, new Color(0.305f, 0.330f, 0.360f), 0.14f, 0.16f),
                "Facility_Wall" => Clone(source, new Color(0.245f, 0.285f, 0.335f), 0.34f, 0.18f),
                "Facility_Floor" => Clone(source, new Color(0.285f, 0.315f, 0.350f), 0.34f, 0.19f),
                "CombatCover" => Clone(source, new Color(0.235f, 0.265f, 0.305f), 0.36f, 0.17f),
                "TacticalAccent" => Clone(source, new Color(0.78f, 0.365f, 0.075f), 0.16f, 0.20f),
                "HazardBand" => Clone(source, new Color(0.88f, 0.88f, 0.84f), 0.12f, 0.16f),
                _ => null
            };

            if (replacement != null)
                _runtimePalette[source.name] = replacement;
            return replacement;
        }

        private static Material Clone(Material source, Color color, float metallic, float smoothness)
        {
            var clone = new Material(source)
            {
                // Preserve the lookup name for StageEnvironment/Authenticity material discovery.
                name = source.name
            };
            if (clone.HasProperty("_BaseColor")) clone.SetColor("_BaseColor", color);
            if (clone.HasProperty("_Color")) clone.SetColor("_Color", color);
            if (clone.HasProperty("_Metallic")) clone.SetFloat("_Metallic", metallic);
            if (clone.HasProperty("_Smoothness")) clone.SetFloat("_Smoothness", smoothness);
            if (clone.HasProperty("_Glossiness")) clone.SetFloat("_Glossiness", smoothness);
            return clone;
        }
    }
}
