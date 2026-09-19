#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArknightsACT.Editor.OHMS
{
    /// <summary>
    /// Generic importer for AssetStudio-Arknights/OHMS "Structured JSON" exports.
    ///
    /// Expected source layout:
    ///   assets.json
    ///   things/<asset ID>.ttbin
    ///
    /// OHMS .ttbin files are type-dependent payloads: built-in Unity objects are generally JSON,
    /// Texture2D payloads are PNG bytes and Mesh payloads are OHMS text meshes. This importer rebuilds
    /// the standard Unity-side pieces needed by battle effects without depending on game-specific names.
    /// Unknown MonoBehaviours and missing Animator/AnimationClip payloads are reported instead of guessed.
    /// </summary>
    internal static class OhmsStructuredFxImporter
    {
        internal const string DefaultOutputRoot = "Assets/_Game/Art/FX/OriginalClient";
        private const string ShaderName = "ArknightsACT/ImportedClientFX";

        [Serializable]
        private sealed class IndexDocument
        {
            public AssetRecord[] Assets;
        }

        [Serializable]
        internal sealed class AssetRecord
        {
            public int ID;
            public string Name;
            public AssetType Type;
            public long PathID;
        }

        [Serializable]
        internal sealed class AssetType
        {
            public int id;
            public string name;
        }

        [Serializable]
        private sealed class PPtr
        {
            public int m_FileID;
            public long m_PathID;
        }

        [Serializable]
        private sealed class ComponentRef
        {
            public PPtr component;
        }

        [Serializable]
        private sealed class GameObjectDump
        {
            public ComponentRef[] m_Component;
            public int m_Layer;
            public string m_Name;
            public int m_Tag;
            public bool m_IsActive;
        }

        [Serializable]
        private sealed class TransformDump
        {
            public PPtr m_GameObject;
            public SerializedQuaternion m_LocalRotation;
            public SerializedVector3 m_LocalPosition;
            public SerializedVector3 m_LocalScale;
            public PPtr[] m_Children;
            public PPtr m_Father;
        }

        [Serializable]
        private sealed class SerializedVector2
        {
            public float x;
            public float y;
        }

        [Serializable]
        private sealed class SerializedVector3
        {
            public float x;
            public float y;
            public float z;

            public Vector3 ToVector3() => new(x, y, z);
        }

        [Serializable]
        private sealed class SerializedQuaternion
        {
            public float x;
            public float y;
            public float z;
            public float w;

            public Quaternion ToQuaternion() => new(x, y, z, w);
        }

        [Serializable]
        private sealed class RendererDump
        {
            public bool m_Enabled;
            public PPtr[] m_Materials;
            public int m_SortingLayerID;
            public int m_SortingOrder;
            public int m_RenderMode;
            public int m_SortMode;
            public float m_MinParticleSize;
            public float m_MaxParticleSize;
            public float m_CameraVelocityScale;
            public float m_VelocityScale;
            public float m_LengthScale;
            public float m_SortingFudge;
            public float m_NormalDirection;
            public int m_RenderAlignment;
            public SerializedVector3 m_Pivot;
            public SerializedVector3 m_Flip;
            public bool m_EnableGPUInstancing;
            public PPtr m_Mesh;
            public PPtr m_Mesh1;
            public PPtr m_Mesh2;
            public PPtr m_Mesh3;
        }

        [Serializable]
        private sealed class ParticleSystemDump
        {
            public float lengthInSec;
            public float simulationSpeed;
            public int cullingMode;
            public bool looping;
            public bool prewarm;
            public bool playOnAwake;
            public bool useUnscaledTime;
            public ScalarCurveDump startDelay;
            public int moveWithTransform;
            public int scalingMode;
            public InitialModuleDump InitialModule;
            public EmissionModuleDump EmissionModule;
            public ShapeModuleDump ShapeModule;
            public SizeModuleDump SizeModule;
            public RotationModuleDump RotationModule;
            public ColorModuleDump ColorModule;
            public UvModuleDump UVModule;
        }

        // OHMS writes the values exposed by ParticleSystem.main below the
        // "InitialModule" object.  Keeping this nesting is important: reading
        // startLifetime/startSpeed from the root silently leaves Unity's default
        // 0.0001s lifetime, which makes every imported effect invisible.
        [Serializable]
        private sealed class InitialModuleDump
        {
            public ScalarCurveDump startLifetime;
            public ScalarCurveDump startSpeed;
            public ScalarCurveDump startSize;
            public ScalarCurveDump startSizeY;
            public ScalarCurveDump startSizeZ;
            public bool size3D;
            public ScalarCurveDump startRotationX;
            public ScalarCurveDump startRotationY;
            public ScalarCurveDump startRotation;
            public bool rotation3D;
            public float randomizeRotationDirection;
            public ScalarCurveDump gravityModifier;
            public int maxNumParticles;
            public ParticleColorDump startColor;
        }

        [Serializable]
        private sealed class ShapeModuleDump
        {
            public bool enabled;
            public int type;
            public float angle;
            public float length;
            public SerializedVector3 boxThickness;
            public float radiusThickness;
            public float donutRadius;
            public SerializedVector3 m_Position;
            public SerializedVector3 m_Rotation;
            public SerializedVector3 m_Scale;
            public bool alignToDirection;
            public ShapeValueDump radius;
            public ShapeValueDump arc;
        }

        [Serializable]
        private sealed class ShapeValueDump
        {
            public float value;
        }

        [Serializable]
        private sealed class SizeModuleDump
        {
            public bool enabled;
            public ScalarCurveDump curve;
            public ScalarCurveDump y;
            public ScalarCurveDump z;
            public bool separateAxes;
        }

        [Serializable]
        private sealed class RotationModuleDump
        {
            public bool enabled;
            public ScalarCurveDump x;
            public ScalarCurveDump y;
            public ScalarCurveDump curve;
            public bool separateAxes;
        }

        [Serializable]
        private sealed class ColorModuleDump
        {
            public bool enabled;
            public ParticleColorDump gradient;
        }

        [Serializable]
        private sealed class UvModuleDump
        {
            public bool enabled;
            public int mode;
            public int timeMode;
            public float fps;
            public ScalarCurveDump frameOverTime;
            public ScalarCurveDump startFrame;
            public int tilesX;
            public int tilesY;
            public int animationType;
            public int rowIndex;
            public float cycles;
            public int rowMode;
            public float flipU;
            public float flipV;
        }

        [Serializable]
        private sealed class EmissionModuleDump
        {
            public bool enabled;
            public ScalarCurveDump rateOverTime;
            public ScalarCurveDump rateOverDistance;
            public int m_BurstCount;
            public BurstDump[] m_Bursts;
        }

        [Serializable]
        private sealed class BurstDump
        {
            public float time;
            public ScalarCurveDump countCurve;
            public int cycleCount;
            public float repeatInterval;
            public float probability;
        }

        [Serializable]
        private sealed class ScalarCurveDump
        {
            public float scalar;

            // Unity serializes MinMaxCurve mode as minMaxState.  OHMS keeps both
            // curve payloads even when a constant mode is selected, so retaining
            // all fields lets us faithfully rebuild animated size/rotation/UV data.
            public int minMaxState;
            public float minScalar;
            public CurveDump maxCurve;
            public CurveDump minCurve;
        }

        [Serializable]
        private sealed class CurveDump
        {
            public CurveKeyDump[] m_Curve;
        }

        [Serializable]
        private sealed class CurveKeyDump
        {
            public float time;
            public float value;
            public float inSlope;
            public float outSlope;
            public int weightedMode;
            public float inWeight;
            public float outWeight;
        }

        [Serializable]
        private sealed class ParticleColorDump
        {
            public int minMaxState;
            public SerializedColor minColor;
            public SerializedColor maxColor;
            public SerializedGradientDump minGradient;
            public SerializedGradientDump maxGradient;
        }

        [Serializable]
        private sealed class SerializedGradientDump
        {
            public SerializedColor key0;
            public SerializedColor key1;
            public SerializedColor key2;
            public SerializedColor key3;
            public SerializedColor key4;
            public SerializedColor key5;
            public SerializedColor key6;
            public SerializedColor key7;
            public int ctime0;
            public int ctime1;
            public int ctime2;
            public int ctime3;
            public int ctime4;
            public int ctime5;
            public int ctime6;
            public int ctime7;
            public int atime0;
            public int atime1;
            public int atime2;
            public int atime3;
            public int atime4;
            public int atime5;
            public int atime6;
            public int atime7;
            public int m_Mode;
            public int m_NumColorKeys;
            public int m_NumAlphaKeys;
        }

        [Serializable]
        private sealed class MeshFilterDump
        {
            public PPtr m_Mesh;
        }

        [Serializable]
        private sealed class MaterialDump
        {
            public string m_Name;
            public PPtr m_Shader;
            public int m_CustomRenderQueue = -1;
            public SavedProperties m_SavedProperties;
        }

        [Serializable]
        private sealed class SavedProperties
        {
            public TextureEnvEntry[] m_TexEnvs;
            public FloatEntry[] m_Floats;
            public ColorEntry[] m_Colors;
        }

        [Serializable]
        private sealed class TextureEnvEntry
        {
            public string Key;
            public TextureEnvValue Value;
        }

        [Serializable]
        private sealed class TextureEnvValue
        {
            public PPtr m_Texture;
            public SerializedVector2 m_Scale;
            public SerializedVector2 m_Offset;
        }

        [Serializable]
        private sealed class FloatEntry
        {
            public string Key;
            public float Value;
        }

        [Serializable]
        private sealed class ColorEntry
        {
            public string Key;
            public SerializedColor Value;
        }

        [Serializable]
        private sealed class SerializedColor
        {
            public float r;
            public float g;
            public float b;
            public float a;

            public Color ToColor() => new(r, g, b, a);
        }

        internal sealed class ScanResult
        {
            public string SourceRoot;
            public string PackageName;
            public int StagedJsonPayloads;
            public int NormalizedPointers;
            public List<AssetRecord> RootGameObjects = new();
            public Dictionary<string, int> TypeCounts = new(StringComparer.OrdinalIgnoreCase);
        }

        internal sealed class ImportOptions
        {
            public string SourceRoot;
            public string ExternalSourceRoot;
            public string OutputRoot = DefaultOutputRoot;
            public string PackageName = "Imported";
            public string IncludeTokens = string.Empty;
            public string ExcludeTokens = string.Empty;
            public bool PreserveLayers;
        }

        internal sealed class ImportReport
        {
            public int PrefabsCreated;
            public int GameObjectsCreated;
            public int ParticleSystemsCreated;
            public int MaterialsCreated;
            public int TexturesCreated;
            public int MeshesCreated;
            public int UnknownMonoBehaviours;
            public int MissingAnimatorPayloads;
            public int StagedJsonPayloads;
            public int NormalizedPointers;
            public readonly List<string> UnresolvedReferences = new();
            public readonly List<string> Warnings = new();

            public string ToSummary()
            {
                return
                    $"Prefabs={PrefabsCreated}, GameObjects={GameObjectsCreated}, ParticleSystems={ParticleSystemsCreated}, " +
                    $"Materials={MaterialsCreated}, Textures={TexturesCreated}, Meshes={MeshesCreated}, " +
                    $"UnknownMonoBehaviours={UnknownMonoBehaviours}, MissingAnimatorPayloads={MissingAnimatorPayloads}, " +
                    $"StagedJson={StagedJsonPayloads}, NormalizedPointers={NormalizedPointers}, " +
                    $"UnresolvedRefs={UnresolvedReferences.Count}";
            }
        }

        private sealed class ImportContext
        {
            public ImportOptions Options;
            public string SourceRoot;
            public string ThingsRoot;
            public string PackageRoot;
            public string PrefabRoot;
            public string MaterialRoot;
            public string TextureRoot;
            public string MeshRoot;
            public readonly Dictionary<long, AssetRecord> ByPathId = new();
            public readonly Dictionary<int, AssetRecord> ById = new();
            public readonly Dictionary<long, Material> Materials = new();
            public readonly Dictionary<long, Texture2D> Textures = new();
            public readonly Dictionary<long, Mesh> Meshes = new();
            public readonly Dictionary<long, AssetRecord> ExternalByPathId = new();
            public readonly Dictionary<AssetRecord, string> ExternalRecordRoots = new();
            public readonly Dictionary<long, Material> ExternalMaterials = new();
            public readonly Dictionary<long, Texture2D> ExternalTextures = new();
            public Material MissingMaterialFallback;
            public Texture2D MissingTextureFallback;
            public ImportReport Report = new();
        }

        internal static bool TryScan(string selectedPath, out ScanResult scan, out string error)
        {
            scan = null;
            error = null;

            if (!TryResolveSourceRoot(selectedPath, out var sourceRoot, out error))
                return false;

            if (!TryPrepareSource(sourceRoot, out var staged, out error))
                return false;

            if (!TryReadIndex(staged.StagingRoot, out var records, out error))
                return false;

            var context = new ImportContext
            {
                SourceRoot = staged.StagingRoot,
                ThingsRoot = Path.Combine(staged.StagingRoot, "things")
            };
            IndexRecords(context, records);

            scan = new ScanResult
            {
                SourceRoot = sourceRoot,
                PackageName = ToPackageName(new DirectoryInfo(sourceRoot).Name),
                StagedJsonPayloads = staged.JsonPayloads,
                NormalizedPointers = staged.RewrittenPointers
            };

            foreach (var group in records.GroupBy(record => record.Type?.name ?? "Unknown"))
                scan.TypeCounts[group.Key] = group.Count();

            foreach (var record in records)
            {
                if (!IsType(record, "GameObject"))
                    continue;

                if (IsRootGameObject(context, record))
                    scan.RootGameObjects.Add(record);
            }

            scan.RootGameObjects = scan.RootGameObjects
                .OrderBy(record => record.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(record => record.PathID)
                .ToList();
            return true;
        }

        internal static ImportReport Import(ImportOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            if (!TryResolveSourceRoot(options.SourceRoot, out var sourceRoot, out var error))
                throw new InvalidOperationException(error);
            if (!TryPrepareSource(sourceRoot, out var staged, out error))
                throw new InvalidOperationException(error);
            if (!TryReadIndex(staged.StagingRoot, out var records, out error))
                throw new InvalidOperationException(error);

            var packageName = string.IsNullOrWhiteSpace(options.PackageName)
                ? ToPackageName(new DirectoryInfo(sourceRoot).Name)
                : SanitizeFileName(options.PackageName.Trim());
            var outputRoot = string.IsNullOrWhiteSpace(options.OutputRoot)
                ? DefaultOutputRoot
                : NormalizeAssetPath(options.OutputRoot.TrimEnd('/', '\\'));

            EnsureAssetFolder(outputRoot);
            var packageRoot = $"{outputRoot}/{packageName}";
            EnsureAssetFolder(packageRoot);
            var prefabRoot = $"{packageRoot}/Prefabs";
            var materialRoot = $"{packageRoot}/Materials";
            var textureRoot = $"{packageRoot}/Textures";
            var meshRoot = $"{packageRoot}/Meshes";
            EnsureAssetFolder(prefabRoot);
            EnsureAssetFolder(materialRoot);
            EnsureAssetFolder(textureRoot);
            EnsureAssetFolder(meshRoot);

            var context = new ImportContext
            {
                Options = options,
                SourceRoot = staged.StagingRoot,
                ThingsRoot = Path.Combine(staged.StagingRoot, "things"),
                PackageRoot = packageRoot,
                PrefabRoot = prefabRoot,
                MaterialRoot = materialRoot,
                TextureRoot = textureRoot,
                MeshRoot = meshRoot
            };
            context.Report.StagedJsonPayloads = staged.JsonPayloads;
            context.Report.NormalizedPointers = staged.RewrittenPointers;
            IndexRecords(context, records);
            LoadExternalRecords(context, options.ExternalSourceRoot);

            var candidates = records
                .Where(record => IsType(record, "GameObject") && IsRootGameObject(context, record))
                .Where(record => MatchesTokens(record.Name, options.IncludeTokens, requireAny: true))
                .Where(record => !MatchesTokens(record.Name, options.ExcludeTokens, requireAny: false))
                .OrderBy(record => record.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(record => record.PathID)
                .ToArray();

            if (candidates.Length == 0)
            {
                context.Report.Warnings.Add("No root GameObjects matched the current include/exclude filters.");
                WriteReport(context);
                return context.Report;
            }

            try
            {
                // Do not wrap the import in StartAssetEditing: Texture2D payloads must be imported
                // synchronously so materials can bind them during the same pass.
                for (var i = 0; i < candidates.Length; i++)
                {
                    var record = candidates[i];
                    EditorUtility.DisplayProgressBar(
                        "OHMS Effect Importer",
                        $"Rebuilding {record.Name} ({i + 1}/{candidates.Length})",
                        candidates.Length <= 1 ? 1f : (float)i / candidates.Length);
                    try
                    {
                        ImportRootPrefab(context, record);
                    }
                    catch (Exception exception)
                    {
                        context.Report.Warnings.Add(
                            $"Failed to rebuild root GameObject '{record.Name}' (PathID {record.PathID}): " +
                            exception.Message);
                        Debug.LogException(exception);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            WriteReport(context);
            return context.Report;
        }

        internal static IReadOnlyList<AssetRecord> FilterRoots(
            ScanResult scan,
            string includeTokens,
            string excludeTokens)
        {
            if (scan == null)
                return Array.Empty<AssetRecord>();

            return scan.RootGameObjects
                .Where(record => MatchesTokens(record.Name, includeTokens, requireAny: true))
                .Where(record => !MatchesTokens(record.Name, excludeTokens, requireAny: false))
                .ToArray();
        }

        private static void ImportRootPrefab(ImportContext context, AssetRecord rootRecord)
        {
            var root = BuildGameObject(context, rootRecord, null);
            if (root == null)
            {
                context.Report.Warnings.Add($"Failed to rebuild root GameObject '{rootRecord.Name}'.");
                return;
            }

            try
            {
                // Use a stable path so an importer refresh updates the existing prefab in place and
                // keeps its .meta GUID.  The old GenerateUniqueAssetPath behavior forced the batch
                // command to delete the whole package, invalidating serialized gameplay references.
                var path = $"{context.PrefabRoot}/{SanitizeFileName(rootRecord.Name)}.prefab";
                PrefabUtility.SaveAsPrefabAsset(root, path);
                context.Report.PrefabsCreated++;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject BuildGameObject(ImportContext context, AssetRecord record, Transform parent)
        {
            if (!TryReadJson<GameObjectDump>(context, record, out var dump, out _))
                return null;

            var go = new GameObject(string.IsNullOrWhiteSpace(dump.m_Name) ? record.Name : dump.m_Name);
            context.Report.GameObjectsCreated++;
            if (parent != null)
                go.transform.SetParent(parent, false);

            if (context.Options.PreserveLayers && dump.m_Layer >= 0 && dump.m_Layer <= 31)
                go.layer = dump.m_Layer;
            else
                go.layer = 0;

            var transformRecord = ResolveComponent(context, dump, "Transform");
            TransformDump transformDump = null;
            if (transformRecord != null)
            {
                TryReadJson(context, transformRecord, out transformDump, out _);
                if (transformDump != null)
                {
                    go.transform.localPosition = transformDump.m_LocalPosition?.ToVector3() ?? Vector3.zero;
                    go.transform.localRotation = transformDump.m_LocalRotation?.ToQuaternion() ?? Quaternion.identity;
                    go.transform.localScale = transformDump.m_LocalScale?.ToVector3() ?? Vector3.one;
                }
            }

            ApplyComponents(context, go, dump);

            if (transformDump?.m_Children != null)
            {
                foreach (var childTransformPtr in transformDump.m_Children)
                {
                    var childTransformRecord = ResolveInternal(context, childTransformPtr, "Transform", record.Name);
                    if (childTransformRecord == null ||
                        !TryReadJson<TransformDump>(context, childTransformRecord, out var childTransformDump, out _))
                        continue;

                    var childRecord = ResolveInternal(context, childTransformDump.m_GameObject, "GameObject", record.Name);
                    if (childRecord != null)
                        BuildGameObject(context, childRecord, go.transform);
                }
            }

            go.SetActive(dump.m_IsActive);
            return go;
        }

        private static void ApplyComponents(ImportContext context, GameObject go, GameObjectDump dump)
        {
            if (dump.m_Component == null)
                return;

            var components = dump.m_Component
                .Select(reference => reference?.component == null
                    ? null
                    : ResolveInternal(context, reference.component, null, go.name))
                .Where(record => record != null)
                .ToArray();

            foreach (var record in components.Where(record => IsType(record, "ParticleSystem")))
            {
                var component = GetOrAddComponent<ParticleSystem>(go);
                if (component == null)
                {
                    context.Report.Warnings.Add($"Could not attach ParticleSystem to '{go.name}'.");
                    continue;
                }

                if (ApplyParticleSystemSettings(context, record, component))
                    context.Report.ParticleSystemsCreated++;
            }

            foreach (var record in components.Where(record => IsType(record, "ParticleSystemRenderer")))
            {
                var renderer = go.GetComponent<ParticleSystemRenderer>();
                if (renderer == null)
                {
                    var system = GetOrAddComponent<ParticleSystem>(go);
                    if (system == null)
                    {
                        context.Report.Warnings.Add(
                            $"Could not attach ParticleSystem required by renderer on '{go.name}'.");
                        continue;
                    }

                    renderer = go.GetComponent<ParticleSystemRenderer>();
                    if (renderer == null)
                    {
                        context.Report.Warnings.Add(
                            $"ParticleSystemRenderer was not created with ParticleSystem on '{go.name}'.");
                        continue;
                    }
                }
                ApplyParticleRendererSettings(context, record, renderer, go.name);
            }

            foreach (var record in components.Where(record => IsType(record, "MeshFilter")))
            {
                var filter = GetOrAddComponent<MeshFilter>(go);
                if (filter == null)
                {
                    context.Report.Warnings.Add($"Could not attach MeshFilter to '{go.name}'.");
                    continue;
                }

                // MeshFilter only carries m_GameObject and m_Mesh in OHMS. Applying its raw JSON would
                // reapply source-object PPtrs to a newly created component, so bind the rebuilt Mesh only.
                if (TryReadJson<MeshFilterDump>(context, record, out var filterDump, out _) && filterDump.m_Mesh != null)
                    filter.sharedMesh = ResolveMesh(context, filterDump.m_Mesh, go.name);
            }

            foreach (var record in components.Where(record => IsType(record, "MeshRenderer")))
            {
                var renderer = GetOrAddComponent<MeshRenderer>(go);
                if (renderer == null)
                    continue;
                renderer.enabled = true;
                ApplyRendererMaterials(context, record, renderer, go.name);
            }

            foreach (var record in components.Where(record => IsType(record, "TrailRenderer")))
            {
                var renderer = GetOrAddComponent<TrailRenderer>(go);
                if (renderer == null)
                    continue;
                renderer.enabled = true;
                ApplyRendererMaterials(context, record, renderer, go.name);
            }

            foreach (var record in components.Where(record => IsType(record, "Animation")))
            {
                var animation = GetOrAddComponent<Animation>(go);
                if (animation != null)
                    animation.enabled = true;
                context.Report.Warnings.Add(
                    $"Legacy Animation component on '{go.name}' was rebuilt, but OHMS does not export AnimationClip payloads in this package; clips may be missing.");
            }

            foreach (var record in components.Where(record => IsType(record, "Animator")))
            {
                if (go.GetComponent<Animator>() == null)
                    go.AddComponent<Animator>();
                context.Report.MissingAnimatorPayloads++;
            }

            foreach (var record in components.Where(record => IsType(record, "MonoBehaviour")))
            {
                context.Report.UnknownMonoBehaviours++;
                // Keep serialized controller payloads. The prefab contains visual pieces, while
                // MonoBehaviour data contains the missing FX assembly/timing references.
                ExportMonoBehaviourTimelineProbe(context, go, record);

                if (TryReadTextPayload(context, record, out var raw))
                {
                    var scriptId = ExtractLong(raw, "m_PathID", 0L);
                    context.Report.Warnings.Add(
                        $"Skipped original MonoBehaviour on '{go.name}' (asset ID {record.ID}, script/path hint {scriptId}). " +
                        "Serialized FX controller data exported for timeline reconstruction.");
                }
            }
        }

        private static void ExportMonoBehaviourTimelineProbe(
            ImportContext context,
            GameObject owner,
            AssetRecord record)
        {
            if (record == null || owner == null || !TryReadTextPayload(context, record, out var raw))
                return;

            // Preserve serialized data from original client components. These are not executed;
            // they are the missing FX assembly information (references, delays and curves).
            var folder = "Assets/_Game/Art/FX/OriginalClient/Chen/TimelineDump";
            if (!AssetDatabase.IsValidFolder(folder) && AssetDatabase.IsValidFolder("Assets/_Game/Art/FX/OriginalClient/Chen"))
                AssetDatabase.CreateFolder("Assets/_Game/Art/FX/OriginalClient/Chen", "TimelineDump");

            var path = folder + "/" + SanitizeFileName(owner.name) + "_" + SafePathId(record.PathID) + ".json";
            File.WriteAllText(ToAbsoluteAssetPath(path), raw);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        private static void ApplyParticleRendererSettings(
            ImportContext context,
            AssetRecord record,
            ParticleSystemRenderer renderer,
            string ownerName)
        {
            if (renderer == null || !TryReadJson<RendererDump>(context, record, out var dump, out _))
                return;

            ApplyRendererMaterials(context, record, renderer, ownerName);

            renderer.enabled = dump.m_Enabled;
            renderer.sortingOrder = dump.m_SortingOrder;
            renderer.minParticleSize = Mathf.Max(0f, dump.m_MinParticleSize);
            renderer.maxParticleSize = Mathf.Max(renderer.minParticleSize, dump.m_MaxParticleSize);
            renderer.cameraVelocityScale = dump.m_CameraVelocityScale;
            renderer.velocityScale = dump.m_VelocityScale;
            renderer.lengthScale = dump.m_LengthScale;
            renderer.sortingFudge = dump.m_SortingFudge;
            renderer.normalDirection = dump.m_NormalDirection;
            if (dump.m_Pivot != null)
                renderer.pivot = dump.m_Pivot.ToVector3();
            if (dump.m_Flip != null)
                renderer.flip = dump.m_Flip.ToVector3();

            if (Enum.IsDefined(typeof(ParticleSystemRenderMode), dump.m_RenderMode))
                renderer.renderMode = (ParticleSystemRenderMode)dump.m_RenderMode;
            if (Enum.IsDefined(typeof(ParticleSystemRenderSpace), dump.m_RenderAlignment))
                renderer.alignment = (ParticleSystemRenderSpace)dump.m_RenderAlignment;
            if (Enum.IsDefined(typeof(ParticleSystemSortMode), dump.m_SortMode))
                renderer.sortMode = (ParticleSystemSortMode)dump.m_SortMode;

            // The fallback shader deliberately has no instancing variant.  Preserve the source
            // preference only when the reconstructed material actually supports instancing.
            renderer.enableGPUInstancing = dump.m_EnableGPUInstancing &&
                                            renderer.sharedMaterial != null &&
                                            renderer.sharedMaterial.enableInstancing;

            if (renderer.renderMode == ParticleSystemRenderMode.Mesh)
            {
                var meshPointer = FirstNonEmptyPointer(dump.m_Mesh, dump.m_Mesh1, dump.m_Mesh2, dump.m_Mesh3);
                if (meshPointer != null)
                {
                    var mesh = ResolveMesh(context, meshPointer, ownerName + ".ParticleRenderer");
                    if (mesh != null)
                        renderer.mesh = mesh;
                }
            }
        }

        private static PPtr FirstNonEmptyPointer(params PPtr[] pointers)
        {
            if (pointers == null)
                return null;
            for (var i = 0; i < pointers.Length; i++)
            {
                if (pointers[i] != null && pointers[i].m_PathID != 0)
                    return pointers[i];
            }
            return null;
        }

        private static void ApplyRendererMaterials(
            ImportContext context,
            AssetRecord record,
            Renderer renderer,
            string ownerName)
        {
            if (renderer == null || !TryReadJson<RendererDump>(context, record, out var dump, out _))
                return;

            if (dump.m_Materials == null || dump.m_Materials.Length == 0)
            {
                renderer.sharedMaterials = new[] { GetMissingMaterialFallback(context) };
                return;
            }

            var materials = new Material[dump.m_Materials.Length];
            for (var i = 0; i < materials.Length; i++)
            {
                var pointer = dump.m_Materials[i];
                materials[i] = ResolveMaterial(context, pointer, ownerName);
                // Null material slots fall back to Unity's built-in particle material. That shader is
                // incompatible with URP and renders magenta, so make every unresolved/empty slot explicit.
                if (materials[i] == null)
                    materials[i] = GetMissingMaterialFallback(context);
            }
            renderer.sharedMaterials = materials;
        }

        private static Material GetMissingMaterialFallback(ImportContext context)
        {
            if (context.MissingMaterialFallback != null)
                return context.MissingMaterialFallback;

            var shader = Shader.Find(ShaderName) ??
                         Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Unlit/Color") ??
                         Shader.Find("Sprites/Default");
            if (shader == null)
                return null;

            var material = new Material(shader)
            {
                name = "OHMS_MissingExternalMaterial",
                renderQueue = (int)RenderQueue.Transparent
            };
            var fallbackTexture = GetMissingTextureFallback(context);
            if (material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", fallbackTexture);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", new Color(1f, 0.22f, 0.025f, 0.92f));
            if (material.HasProperty("_TintColor"))
                material.SetColor("_TintColor", Color.white);
            if (material.HasProperty("_SrcBlend"))
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend"))
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite"))
                material.SetFloat("_ZWrite", 0f);

            var assetPath = $"{context.MaterialRoot}/OHMS_MissingExternalMaterial.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(material);
                if (existing.HasProperty("_MainTex"))
                    existing.SetTexture("_MainTex", fallbackTexture);
                if (existing.HasProperty("_Color"))
                    existing.SetColor("_Color", new Color(1f, 0.22f, 0.025f, 0.92f));
                context.MissingMaterialFallback = existing;
                return existing;
            }

            AssetDatabase.CreateAsset(material, assetPath);
            context.MissingMaterialFallback = material;
            context.Report.MaterialsCreated++;
            context.Report.Warnings.Add(
                "One or more external materials were absent from the structured export; " +
                "affected renderer slots use a visible URP fallback instead of Unity's magenta default material.");
            return material;
        }

        private static Texture2D GetMissingTextureFallback(ImportContext context)
        {
            if (context.MissingTextureFallback != null)
                return context.MissingTextureFallback;

            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "OHMS_MissingExternalTexture",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = (x + 0.5f) / size * 2f - 1f;
                    var dy = (y + 0.5f) / size * 2f - 1f;
                    var radius = Mathf.Sqrt(dx * dx + dy * dy);
                    var alpha = Mathf.Clamp01((1f - radius) * 2.2f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);

            var assetPath = $"{context.TextureRoot}/OHMS_MissingExternalTexture.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(texture);
                context.MissingTextureFallback = existing;
                return existing;
            }

            AssetDatabase.CreateAsset(texture, assetPath);
            context.MissingTextureFallback = texture;
            return texture;
        }

        private static bool ApplyParticleSystemSettings(
            ImportContext context,
            AssetRecord record,
            ParticleSystem system)
        {
            if (system == null || !TryReadJson<ParticleSystemDump>(context, record, out var dump, out _))
                return false;

            var main = system.main;
            if (dump.lengthInSec > 0f)
                main.duration = dump.lengthInSec;
            if (dump.simulationSpeed > 0f)
                main.simulationSpeed = dump.simulationSpeed;
            main.loop = dump.looping;
            main.prewarm = dump.prewarm && dump.looping;
            main.playOnAwake = dump.playOnAwake;
            main.useUnscaledTime = dump.useUnscaledTime;
            // moveWithTransform is Unity's serialized ParticleSystemSimulationSpace value:
            // Local=0, World=1, Custom=2.  Custom needs a reconstructed Transform reference, which
            // the structured payload currently does not expose here, so only apply values we can
            // reproduce safely.
            if (dump.moveWithTransform == (int)ParticleSystemSimulationSpace.Local ||
                dump.moveWithTransform == (int)ParticleSystemSimulationSpace.World)
            {
                main.simulationSpace = (ParticleSystemSimulationSpace)dump.moveWithTransform;
            }
            if (Enum.IsDefined(typeof(ParticleSystemScalingMode), dump.scalingMode))
                main.scalingMode = (ParticleSystemScalingMode)dump.scalingMode;
            if (Enum.IsDefined(typeof(ParticleSystemCullingMode), dump.cullingMode))
                main.cullingMode = (ParticleSystemCullingMode)dump.cullingMode;
            if (dump.startDelay != null)
                main.startDelay = ToMinMaxCurve(dump.startDelay);

            // Preserve the full MinMaxCurve/MinMaxGradient contracts.  The previous importer kept
            // only each module's maximum scalar, which turned Ch'en's short, shaped slash particles
            // into dense generic dots and made randomized ranges deterministic.
            var initial = dump.InitialModule;
            if (initial != null)
            {
                if (initial.startLifetime != null)
                    main.startLifetime = ToMinMaxCurve(initial.startLifetime);
                if (initial.startSpeed != null)
                    main.startSpeed = ToMinMaxCurve(initial.startSpeed);

                main.startSize3D = initial.size3D;
                if (initial.startSize != null)
                    main.startSizeX = ToMinMaxCurve(initial.startSize);
                if (initial.size3D && initial.startSizeY != null)
                    main.startSizeY = ToMinMaxCurve(initial.startSizeY);
                if (initial.size3D && initial.startSizeZ != null)
                    main.startSizeZ = ToMinMaxCurve(initial.startSizeZ);
                if (!initial.size3D && initial.startSize != null)
                    main.startSize = ToMinMaxCurve(initial.startSize);

                main.startRotation3D = initial.rotation3D;
                if (initial.rotation3D)
                {
                    if (initial.startRotationX != null)
                        main.startRotationX = ToMinMaxCurve(initial.startRotationX);
                    if (initial.startRotationY != null)
                        main.startRotationY = ToMinMaxCurve(initial.startRotationY);
                    if (initial.startRotation != null)
                        main.startRotationZ = ToMinMaxCurve(initial.startRotation);
                }
                else if (initial.startRotation != null)
                {
                    main.startRotation = ToMinMaxCurve(initial.startRotation);
                }
                main.flipRotation = Mathf.Clamp01(initial.randomizeRotationDirection);

                if (initial.gravityModifier != null)
                    main.gravityModifier = ToMinMaxCurve(initial.gravityModifier);
                if (initial.maxNumParticles > 0)
                    main.maxParticles = initial.maxNumParticles;
                if (initial.startColor != null)
                    main.startColor = ToMinMaxGradient(initial.startColor);
            }

            var emissionDump = dump.EmissionModule;
            if (emissionDump != null)
            {
                var emission = system.emission;
                emission.enabled = emissionDump.enabled;
                if (emissionDump.rateOverTime != null)
                    emission.rateOverTime = ToMinMaxCurve(emissionDump.rateOverTime);
                if (emissionDump.rateOverDistance != null)
                    emission.rateOverDistance = ToMinMaxCurve(emissionDump.rateOverDistance);

                if (emissionDump.m_BurstCount > 0 && emissionDump.m_Bursts != null)
                {
                    var bursts = emissionDump.m_Bursts
                        .Take(emissionDump.m_BurstCount)
                        .Where(item => item != null)
                        .Select(CreateBurst)
                        .ToArray();
                    emission.SetBursts(bursts);
                }
                else
                {
                    emission.SetBursts(Array.Empty<ParticleSystem.Burst>());
                }
            }

            ApplyShapeModule(system, dump.ShapeModule);
            ApplySizeModule(system, dump.SizeModule);
            ApplyRotationModule(system, dump.RotationModule);
            ApplyColorModule(system, dump.ColorModule);
            ApplyUvModule(system, dump.UVModule);
            return true;
        }

        private static ParticleSystem.Burst CreateBurst(BurstDump dump)
        {
            // A serialized burst slot with count=0 is legal and is used by the client as an
            // inactive placeholder.  Forcing it to one produced a visible extra dot on every play.
            var count = (short)Mathf.Clamp(
                Mathf.RoundToInt(dump?.countCurve?.scalar ?? 0f),
                0,
                short.MaxValue);
            var burst = new ParticleSystem.Burst(Mathf.Max(0f, dump?.time ?? 0f), count);
            burst.cycleCount = dump == null || dump.cycleCount <= 0 ? 1 : dump.cycleCount;
            burst.repeatInterval = Mathf.Max(0.01f, dump?.repeatInterval ?? 0.01f);
            burst.probability = Mathf.Clamp01(dump == null ? 1f : dump.probability);
            return burst;
        }

        private static void ApplyShapeModule(ParticleSystem system, ShapeModuleDump dump)
        {
            if (dump == null)
                return;

            var shape = system.shape;
            shape.enabled = dump.enabled;
            if (!dump.enabled)
                return;

            if (Enum.IsDefined(typeof(ParticleSystemShapeType), dump.type))
                shape.shapeType = (ParticleSystemShapeType)dump.type;
            shape.angle = dump.angle;
            shape.length = dump.length;
            shape.radiusThickness = Mathf.Clamp01(dump.radiusThickness);
            shape.donutRadius = Mathf.Max(0f, dump.donutRadius);
            shape.alignToDirection = dump.alignToDirection;
            if (dump.m_Position != null)
                shape.position = dump.m_Position.ToVector3();
            if (dump.m_Rotation != null)
                shape.rotation = dump.m_Rotation.ToVector3();
            if (dump.m_Scale != null)
                shape.scale = dump.m_Scale.ToVector3();
            if (dump.boxThickness != null)
                shape.boxThickness = dump.boxThickness.ToVector3();
            if (dump.radius != null)
                shape.radius = Mathf.Max(0f, dump.radius.value);
            if (dump.arc != null)
                shape.arc = dump.arc.value;
        }

        private static void ApplySizeModule(ParticleSystem system, SizeModuleDump dump)
        {
            if (dump == null)
                return;

            var module = system.sizeOverLifetime;
            module.enabled = dump.enabled;
            if (!dump.enabled)
                return;

            module.separateAxes = dump.separateAxes;
            if (dump.curve != null)
                module.x = ToMinMaxCurve(dump.curve);
            if (dump.separateAxes && dump.y != null)
                module.y = ToMinMaxCurve(dump.y);
            if (dump.separateAxes && dump.z != null)
                module.z = ToMinMaxCurve(dump.z);
            if (!dump.separateAxes && dump.curve != null)
                module.size = ToMinMaxCurve(dump.curve);
        }

        private static void ApplyRotationModule(ParticleSystem system, RotationModuleDump dump)
        {
            if (dump == null)
                return;

            var module = system.rotationOverLifetime;
            module.enabled = dump.enabled;
            if (!dump.enabled)
                return;

            module.separateAxes = dump.separateAxes;
            if (dump.separateAxes)
            {
                if (dump.x != null)
                    module.x = ToMinMaxCurve(dump.x);
                if (dump.y != null)
                    module.y = ToMinMaxCurve(dump.y);
            }
            if (dump.curve != null)
                module.z = ToMinMaxCurve(dump.curve);
        }

        private static void ApplyColorModule(ParticleSystem system, ColorModuleDump dump)
        {
            if (dump == null)
                return;

            var module = system.colorOverLifetime;
            module.enabled = dump.enabled;
            if (dump.enabled && dump.gradient != null)
                module.color = ToMinMaxGradient(dump.gradient);
        }

        private static void ApplyUvModule(ParticleSystem system, UvModuleDump dump)
        {
            if (dump == null)
                return;

            var module = system.textureSheetAnimation;
            module.enabled = dump.enabled;
            if (!dump.enabled)
                return;

            if (Enum.IsDefined(typeof(ParticleSystemAnimationMode), dump.mode))
                module.mode = (ParticleSystemAnimationMode)dump.mode;
            if (Enum.IsDefined(typeof(ParticleSystemAnimationTimeMode), dump.timeMode))
                module.timeMode = (ParticleSystemAnimationTimeMode)dump.timeMode;
            module.fps = Mathf.Max(0f, dump.fps);
            module.numTilesX = Mathf.Max(1, dump.tilesX);
            module.numTilesY = Mathf.Max(1, dump.tilesY);
            if (Enum.IsDefined(typeof(ParticleSystemAnimationType), dump.animationType))
                module.animation = (ParticleSystemAnimationType)dump.animationType;
            if (Enum.IsDefined(typeof(ParticleSystemAnimationRowMode), dump.rowMode))
                module.rowMode = (ParticleSystemAnimationRowMode)dump.rowMode;
            module.rowIndex = Mathf.Max(0, dump.rowIndex);
            module.cycleCount = Mathf.Max(1, Mathf.RoundToInt(dump.cycles));
            if (dump.frameOverTime != null)
                module.frameOverTime = ToMinMaxCurve(dump.frameOverTime);
            if (dump.startFrame != null)
                module.startFrame = ToMinMaxCurve(dump.startFrame);
        }

        private static ParticleSystem.MinMaxCurve ToMinMaxCurve(ScalarCurveDump dump)
        {
            if (dump == null)
                return new ParticleSystem.MinMaxCurve(0f);

            return dump.minMaxState switch
            {
                1 => new ParticleSystem.MinMaxCurve(dump.scalar, ToAnimationCurve(dump.maxCurve)),
                2 => new ParticleSystem.MinMaxCurve(dump.scalar, ToAnimationCurve(dump.minCurve), ToAnimationCurve(dump.maxCurve)),
                3 => new ParticleSystem.MinMaxCurve(dump.minScalar, dump.scalar),
                _ => new ParticleSystem.MinMaxCurve(dump.scalar)
            };
        }

        private static AnimationCurve ToAnimationCurve(CurveDump dump)
        {
            if (dump?.m_Curve == null || dump.m_Curve.Length == 0)
                return AnimationCurve.Linear(0f, 1f, 1f, 1f);

            var keys = new Keyframe[dump.m_Curve.Length];
            for (var i = 0; i < keys.Length; i++)
            {
                var source = dump.m_Curve[i];
                keys[i] = source == null
                    ? new Keyframe(0f, 0f)
                    : new Keyframe(source.time, source.value, source.inSlope, source.outSlope);
            }
            return new AnimationCurve(keys);
        }

        private static ParticleSystem.MinMaxGradient ToMinMaxGradient(ParticleColorDump dump)
        {
            if (dump == null)
                return new ParticleSystem.MinMaxGradient(Color.white);

            var minColor = dump.minColor?.ToColor() ?? Color.white;
            var maxColor = dump.maxColor?.ToColor() ?? minColor;
            return dump.minMaxState switch
            {
                1 => new ParticleSystem.MinMaxGradient(ToGradient(dump.maxGradient, maxColor)),
                2 => new ParticleSystem.MinMaxGradient(minColor, maxColor),
                3 => new ParticleSystem.MinMaxGradient(
                    ToGradient(dump.minGradient, minColor),
                    ToGradient(dump.maxGradient, maxColor)),
                4 => new ParticleSystem.MinMaxGradient(ToGradient(dump.maxGradient, maxColor)),
                _ => new ParticleSystem.MinMaxGradient(maxColor)
            };
        }

        private static Gradient ToGradient(SerializedGradientDump dump, Color fallback)
        {
            var gradient = new Gradient();
            if (dump == null)
            {
                gradient.SetKeys(
                    new[] { new GradientColorKey(fallback, 0f), new GradientColorKey(fallback, 1f) },
                    new[] { new GradientAlphaKey(fallback.a, 0f), new GradientAlphaKey(fallback.a, 1f) });
                return gradient;
            }

            var colorCount = Mathf.Clamp(dump.m_NumColorKeys, 1, 8);
            var alphaCount = Mathf.Clamp(dump.m_NumAlphaKeys, 1, 8);
            var colorKeys = new GradientColorKey[colorCount];
            var alphaKeys = new GradientAlphaKey[alphaCount];
            for (var i = 0; i < colorCount; i++)
            {
                var color = GetGradientColor(dump, i)?.ToColor() ?? fallback;
                colorKeys[i] = new GradientColorKey(color, GetGradientTime(dump, i, false));
            }
            for (var i = 0; i < alphaCount; i++)
            {
                var color = GetGradientColor(dump, i)?.ToColor() ?? fallback;
                alphaKeys[i] = new GradientAlphaKey(color.a, GetGradientTime(dump, i, true));
            }
            gradient.SetKeys(colorKeys, alphaKeys);
            if (Enum.IsDefined(typeof(GradientMode), dump.m_Mode))
                gradient.mode = (GradientMode)dump.m_Mode;
            return gradient;
        }

        private static SerializedColor GetGradientColor(SerializedGradientDump dump, int index)
        {
            return index switch
            {
                0 => dump.key0,
                1 => dump.key1,
                2 => dump.key2,
                3 => dump.key3,
                4 => dump.key4,
                5 => dump.key5,
                6 => dump.key6,
                7 => dump.key7,
                _ => null
            };
        }

        private static float GetGradientTime(SerializedGradientDump dump, int index, bool alpha)
        {
            var raw = alpha
                ? index switch
                {
                    0 => dump.atime0,
                    1 => dump.atime1,
                    2 => dump.atime2,
                    3 => dump.atime3,
                    4 => dump.atime4,
                    5 => dump.atime5,
                    6 => dump.atime6,
                    7 => dump.atime7,
                    _ => 0
                }
                : index switch
                {
                    0 => dump.ctime0,
                    1 => dump.ctime1,
                    2 => dump.ctime2,
                    3 => dump.ctime3,
                    4 => dump.ctime4,
                    5 => dump.ctime5,
                    6 => dump.ctime6,
                    7 => dump.ctime7,
                    _ => 0
                };
            return Mathf.Clamp01(raw / 65535f);
        }

        private static Material ResolveMaterial(ImportContext context, PPtr pointer, string ownerName)
        {
            if (pointer == null || pointer.m_PathID == 0)
                return null;

            Dictionary<long, Material> cache;
            AssetRecord record;
            if (pointer.m_FileID != 0)
            {
                cache = context.ExternalMaterials;
                if (cache.TryGetValue(pointer.m_PathID, out var externalCached))
                    return externalCached;
                if (!context.ExternalByPathId.TryGetValue(pointer.m_PathID, out record) ||
                    !IsType(record, "Material"))
                {
                    ReportExternal(context, pointer, ownerName, "Material");
                    return null;
                }
            }
            else
            {
                cache = context.Materials;
                if (cache.TryGetValue(pointer.m_PathID, out var cached))
                    return cached;
                record = ResolveInternal(context, pointer, "Material", ownerName);
                if (record == null)
                    return null;
            }
            if (!TryReadJson<MaterialDump>(context, record, out var dump, out _))
                return null;

            var shader = Shader.Find(ShaderName) ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            if (shader == null)
            {
                context.Report.Warnings.Add($"Could not find fallback FX shader while importing material '{record.Name}'.");
                return null;
            }

            var material = new Material(shader)
            {
                name = string.IsNullOrWhiteSpace(dump.m_Name) ? record.Name : dump.m_Name
            };

            if (dump.m_SavedProperties?.m_Floats != null)
            {
                foreach (var entry in dump.m_SavedProperties.m_Floats)
                {
                    if (!string.IsNullOrWhiteSpace(entry.Key) && material.HasProperty(entry.Key))
                        material.SetFloat(entry.Key, entry.Value);
                }
            }

            if (dump.m_SavedProperties?.m_Colors != null)
            {
                foreach (var entry in dump.m_SavedProperties.m_Colors)
                {
                    if (!string.IsNullOrWhiteSpace(entry.Key) && entry.Value != null && material.HasProperty(entry.Key))
                        material.SetColor(entry.Key, entry.Value.ToColor());
                }
            }

            if (dump.m_SavedProperties?.m_TexEnvs != null)
            {
                foreach (var entry in dump.m_SavedProperties.m_TexEnvs)
                {
                    if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value == null || !material.HasProperty(entry.Key))
                        continue;

                    var texture = ResolveTexture(context, entry.Value.m_Texture, material.name + "." + entry.Key);
                    if (texture == null && entry.Value.m_Texture != null && entry.Value.m_Texture.m_PathID != 0)
                        texture = GetMissingTextureFallback(context);
                    if (texture != null)
                        material.SetTexture(entry.Key, texture);
                    if (entry.Value.m_Scale != null)
                        material.SetTextureScale(entry.Key, new Vector2(entry.Value.m_Scale.x, entry.Value.m_Scale.y));
                    if (entry.Value.m_Offset != null)
                        material.SetTextureOffset(entry.Key, new Vector2(entry.Value.m_Offset.x, entry.Value.m_Offset.y));
                }
            }

            if (dump.m_CustomRenderQueue >= 0)
                material.renderQueue = dump.m_CustomRenderQueue;

            var assetPath = $"{context.MaterialRoot}/{SanitizeFileName(material.name)}_{SafePathId(record.PathID)}.mat";
            var existingMaterial = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (existingMaterial != null)
            {
                EditorUtility.CopySerialized(material, existingMaterial);
                EditorUtility.SetDirty(existingMaterial);
                UnityEngine.Object.DestroyImmediate(material);
                material = existingMaterial;
            }
            else
            {
                AssetDatabase.CreateAsset(material, assetPath);
                context.Report.MaterialsCreated++;
            }
            cache[pointer.m_PathID] = material;
            return material;
        }

        private static Texture2D ResolveTexture(ImportContext context, PPtr pointer, string ownerName)
        {
            if (pointer == null || pointer.m_PathID == 0)
                return null;

            Dictionary<long, Texture2D> cache;
            AssetRecord record;
            if (pointer.m_FileID != 0)
            {
                cache = context.ExternalTextures;
                if (cache.TryGetValue(pointer.m_PathID, out var externalCached))
                    return externalCached;
                if (!context.ExternalByPathId.TryGetValue(pointer.m_PathID, out record) ||
                    !IsType(record, "Texture2D"))
                {
                    ReportExternal(context, pointer, ownerName, "Texture");
                    return null;
                }
            }
            else
            {
                cache = context.Textures;
                if (cache.TryGetValue(pointer.m_PathID, out var cached))
                    return cached;
                record = ResolveInternal(context, pointer, "Texture2D", ownerName);
                if (record == null)
                    return null;
            }
            var payloadPath = GetPayloadPath(context, record);
            if (!File.Exists(payloadPath))
            {
                context.Report.Warnings.Add($"Texture payload missing: {record.Name} (ID {record.ID}).");
                return null;
            }

            var bytes = File.ReadAllBytes(payloadPath);
            if (!LooksLikePng(bytes))
            {
                context.Report.Warnings.Add($"Texture payload is not PNG: {record.Name} (ID {record.ID}).");
                return null;
            }

            var fileName = $"{SanitizeFileName(record.Name)}_{SafePathId(record.PathID)}.png";
            var assetPath = $"{context.TextureRoot}/{fileName}";
            File.WriteAllBytes(ToAbsoluteAssetPath(assetPath), bytes);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(assetPath) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.sRGBTexture = true;
                importer.SaveAndReimport();
            }

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (texture != null)
            {
                cache[pointer.m_PathID] = texture;
                context.Report.TexturesCreated++;
            }
            return texture;
        }

        private static Mesh ResolveMesh(ImportContext context, PPtr pointer, string ownerName)
        {
            if (pointer == null || pointer.m_PathID == 0)
                return null;
            if (pointer.m_FileID != 0)
            {
                ReportExternal(context, pointer, ownerName, "Mesh");
                return null;
            }
            if (context.Meshes.TryGetValue(pointer.m_PathID, out var cached))
                return cached;

            var record = ResolveInternal(context, pointer, "Mesh", ownerName);
            if (record == null)
                return null;
            if (!TryReadTextPayload(context, record, out var text))
                return null;

            var mesh = ParseOhmsMesh(text, record.Name, context.Report);
            if (mesh == null)
                return null;

            var assetPath = $"{context.MeshRoot}/{SanitizeFileName(record.Name)}_{SafePathId(record.PathID)}.asset";
            var existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (existingMesh != null)
            {
                EditorUtility.CopySerialized(mesh, existingMesh);
                EditorUtility.SetDirty(existingMesh);
                UnityEngine.Object.DestroyImmediate(mesh);
                mesh = existingMesh;
            }
            else
            {
                AssetDatabase.CreateAsset(mesh, assetPath);
                context.Report.MeshesCreated++;
            }
            context.Meshes[pointer.m_PathID] = mesh;
            return mesh;
        }

        private static Mesh ParseOhmsMesh(string text, string name, ImportReport report)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var posCount = 3;
            var uv0Count = 0;
            var uv1Count = 0;
            var normalCount = 0;
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();

            var lines = text.Replace("\r", string.Empty).Split('\n');
            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (line.StartsWith("# Pos count of a vertex:", StringComparison.Ordinal))
                    int.TryParse(line.Substring(line.LastIndexOf(':') + 1).Trim(), out posCount);
                else if (line.StartsWith("# UV0 count of a vertex:", StringComparison.Ordinal))
                    int.TryParse(line.Substring(line.LastIndexOf(':') + 1).Trim(), out uv0Count);
                else if (line.StartsWith("# UV1 count of a vertex:", StringComparison.Ordinal))
                    int.TryParse(line.Substring(line.LastIndexOf(':') + 1).Trim(), out uv1Count);
                else if (line.StartsWith("# Nor count of a vertex:", StringComparison.Ordinal))
                    int.TryParse(line.Substring(line.LastIndexOf(':') + 1).Trim(), out normalCount);
                else if (line.StartsWith("v ", StringComparison.Ordinal))
                {
                    var parts = SplitNumbers(line.Substring(2));
                    var required = Math.Max(3, posCount) + uv0Count + uv1Count + normalCount;
                    if (parts.Length < required)
                        continue;

                    vertices.Add(new Vector3(parts[0], parts[1], parts[2]));
                    var cursor = posCount;
                    if (uv0Count >= 2)
                        uv.Add(new Vector2(parts[cursor], parts[cursor + 1]));
                    cursor += uv0Count + uv1Count;
                    if (normalCount >= 3)
                        normals.Add(new Vector3(parts[cursor], parts[cursor + 1], parts[cursor + 2]));
                }
                else if (line.StartsWith("f ", StringComparison.Ordinal))
                {
                    var indices = line.Substring(2)
                        .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(token => int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : -1)
                        .Where(value => value >= 0)
                        .ToArray();
                    if (indices.Length >= 3)
                    {
                        for (var i = 1; i + 1 < indices.Length; i++)
                        {
                            triangles.Add(indices[0]);
                            triangles.Add(indices[i]);
                            triangles.Add(indices[i + 1]);
                        }
                    }
                }
            }

            if (vertices.Count == 0 || triangles.Count == 0)
            {
                report.Warnings.Add($"Could not parse OHMS mesh '{name}'.");
                return null;
            }

            var mesh = new Mesh { name = string.IsNullOrWhiteSpace(name) ? "OHMS_Mesh" : name };
            if (vertices.Count > 65535)
                mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            if (uv.Count == vertices.Count)
                mesh.SetUVs(0, uv);
            if (normals.Count == vertices.Count)
                mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0, true);
            if (normals.Count != vertices.Count)
                mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static float[] SplitNumbers(string line)
        {
            var tokens = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            var values = new float[tokens.Length];
            for (var i = 0; i < tokens.Length; i++)
            {
                if (!float.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]))
                    values[i] = 0f;
            }
            return values;
        }

        private static T GetOrAddComponent<T>(GameObject go) where T : Component
        {
            if (go == null)
                return null;
            var component = go.GetComponent<T>();
            if (component == null)
                component = go.AddComponent<T>();
            return component;
        }

        private static AssetRecord ResolveComponent(ImportContext context, GameObjectDump dump, string type)
        {
            if (dump?.m_Component == null)
                return null;
            foreach (var reference in dump.m_Component)
            {
                if (reference?.component == null || reference.component.m_FileID != 0)
                    continue;
                if (context.ByPathId.TryGetValue(reference.component.m_PathID, out var record) && IsType(record, type))
                    return record;
            }
            return null;
        }

        private static AssetRecord ResolveInternal(
            ImportContext context,
            PPtr pointer,
            string expectedType,
            string ownerName)
        {
            if (pointer == null || pointer.m_PathID == 0)
                return null;
            if (pointer.m_FileID != 0)
            {
                ReportExternal(context, pointer, ownerName, expectedType ?? "Object");
                return null;
            }
            if (!context.ByPathId.TryGetValue(pointer.m_PathID, out var record))
            {
                context.Report.UnresolvedReferences.Add(
                    $"{ownerName}: internal PathID {pointer.m_PathID} ({expectedType ?? "Object"}) not present in assets.json");
                return null;
            }
            if (!string.IsNullOrWhiteSpace(expectedType) && !IsType(record, expectedType))
            {
                context.Report.Warnings.Add(
                    $"{ownerName}: PathID {pointer.m_PathID} resolved as {record.Type?.name}, expected {expectedType}.");
            }
            return record;
        }

        private static void ReportExternal(ImportContext context, PPtr pointer, string ownerName, string kind)
        {
            var entry = $"{ownerName}: unresolved external {kind} FileID={pointer.m_FileID}, PathID={pointer.m_PathID}";
            if (!context.Report.UnresolvedReferences.Contains(entry))
                context.Report.UnresolvedReferences.Add(entry);
        }

        private static bool IsRootGameObject(ImportContext context, AssetRecord record)
        {
            if (!TryReadJson<GameObjectDump>(context, record, out var dump, out _))
                return false;
            var transformRecord = ResolveComponent(context, dump, "Transform");
            if (transformRecord == null || !TryReadJson<TransformDump>(context, transformRecord, out var transform, out _))
                return false;
            return transform.m_Father == null || transform.m_Father.m_PathID == 0;
        }

        private static void IndexRecords(ImportContext context, IEnumerable<AssetRecord> records)
        {
            foreach (var record in records)
            {
                context.ById[record.ID] = record;
                if (!context.ByPathId.ContainsKey(record.PathID))
                    context.ByPathId.Add(record.PathID, record);
            }
        }

        private static bool TryReadIndex(string sourceRoot, out AssetRecord[] records, out string error)
        {
            records = null;
            error = null;
            var path = Path.Combine(sourceRoot, "assets.json");
            if (!File.Exists(path))
            {
                error = $"assets.json not found under '{sourceRoot}'.";
                return false;
            }

            try
            {
                var document = JsonUtility.FromJson<IndexDocument>(File.ReadAllText(path));
                records = document?.Assets ?? Array.Empty<AssetRecord>();
                if (records.Length == 0)
                {
                    error = "assets.json contains no Assets records.";
                    return false;
                }
                return true;
            }
            catch (Exception exception)
            {
                error = $"Could not parse assets.json: {exception.Message}";
                return false;
            }
        }

        private static void LoadExternalRecords(ImportContext context, string configuredRoot)
        {
            var root = configuredRoot;
            if (string.IsNullOrWhiteSpace(root))
                root = Environment.GetEnvironmentVariable("OHMS_EXTERNAL_ROOT");
            if (string.IsNullOrWhiteSpace(root))
                root = @"D:\AK_Extract\Unpacked\20260917-171222";
            root = Path.GetFullPath(root);

            if (!File.Exists(Path.Combine(root, "assets.json")) ||
                !Directory.Exists(Path.Combine(root, "things")))
            {
                context.Report.Warnings.Add(
                    $"External OHMS dependency export not found at '{root}'; external materials/textures remain unresolved.");
                return;
            }

            if (!TryReadIndex(root, out var records, out var error))
            {
                context.Report.Warnings.Add("Could not read external OHMS dependency index: " + error);
                return;
            }

            LoadExternalIndex(context, root, records);

            // The material/sharedbattle export references the shared FX texture bundles by
            // external FileID.  Load the texture-only index as a second source so those
            // references resolve to the original PNG payloads as well.
            var textureRoot = @"D:\AK_Extract\Unpacked\20260917-171120";
            if (!string.Equals(Path.GetFullPath(textureRoot), root, StringComparison.OrdinalIgnoreCase) &&
                File.Exists(Path.Combine(textureRoot, "assets.json")) &&
                Directory.Exists(Path.Combine(textureRoot, "things")) &&
                TryReadIndex(textureRoot, out var textureRecords, out _))
            {
                LoadExternalIndex(context, textureRoot, textureRecords);
            }
        }

        private static void LoadExternalIndex(
            ImportContext context,
            string root,
            IEnumerable<AssetRecord> records)
        {
            var thingsRoot = Path.Combine(root, "things");
            foreach (var record in records)
            {
                if (record == null || record.PathID == 0 || context.ExternalByPathId.ContainsKey(record.PathID))
                    continue;
                context.ExternalByPathId.Add(record.PathID, record);
                context.ExternalRecordRoots.Add(record, thingsRoot);
            }
        }

        private static bool TryPrepareSource(
            string sourceRoot,
            out OhmsStructuredExportStager.StageResult staged,
            out string error)
        {
            staged = null;
            error = null;
            try
            {
                staged = OhmsStructuredExportStager.Prepare(sourceRoot);
                return true;
            }
            catch (Exception exception)
            {
                error = $"Could not prepare OHMS structured export: {exception.Message}";
                return false;
            }
        }

        private static bool TryResolveSourceRoot(string selectedPath, out string sourceRoot, out string error)
        {
            sourceRoot = null;
            error = null;
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                error = "Choose an OHMS structured export folder first.";
                return false;
            }

            var path = selectedPath.Trim();
            if (File.Exists(path) && string.Equals(Path.GetFileName(path), "assets.json", StringComparison.OrdinalIgnoreCase))
                path = Path.GetDirectoryName(path);

            if (!Directory.Exists(path))
            {
                error = $"OHMS source folder does not exist: {path}";
                return false;
            }

            if (File.Exists(Path.Combine(path, "assets.json")) && Directory.Exists(Path.Combine(path, "things")))
            {
                sourceRoot = Path.GetFullPath(path);
                return true;
            }

            var assetFiles = Directory.GetFiles(path, "assets.json", SearchOption.AllDirectories);
            var candidate = assetFiles.FirstOrDefault(file => Directory.Exists(Path.Combine(Path.GetDirectoryName(file) ?? string.Empty, "things")));
            if (candidate == null)
            {
                error = $"Could not find an OHMS structured export (assets.json + things/) under '{path}'.";
                return false;
            }

            sourceRoot = Path.GetDirectoryName(candidate);
            return true;
        }

        private static bool TryReadJson<T>(ImportContext context, AssetRecord record, out T value, out string raw)
            where T : class
        {
            value = null;
            raw = null;
            if (!TryReadTextPayload(context, record, out raw))
                return false;

            try
            {
                value = JsonUtility.FromJson<T>(raw);
                return value != null;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryReadTextPayload(ImportContext context, AssetRecord record, out string raw)
        {
            raw = null;
            var path = GetPayloadPath(context, record);
            if (!File.Exists(path))
            {
                context.Report?.Warnings.Add(
                    $"OHMS payload missing for {record.Type?.name}:{record.Name} (asset ID {record.ID}).");
                return false;
            }

            try
            {
                raw = File.ReadAllText(path);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string GetPayloadPath(ImportContext context, AssetRecord record)
            => Path.Combine(
                context.ExternalRecordRoots.TryGetValue(record, out var externalThingsRoot)
                    ? externalThingsRoot
                    : context.ThingsRoot,
                record.ID + ".ttbin");

        private static bool IsType(AssetRecord record, string type)
            => record?.Type != null && string.Equals(record.Type.name, type, StringComparison.OrdinalIgnoreCase);

        private static bool MatchesTokens(string text, string tokenText, bool requireAny)
        {
            var tokens = SplitTokens(tokenText);
            if (tokens.Length == 0)
                return requireAny;
            return tokens.Any(token => (text ?? string.Empty).IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string[] SplitTokens(string tokens)
        {
            if (string.IsNullOrWhiteSpace(tokens))
                return Array.Empty<string>();
            return tokens
                .Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(token => token.Trim())
                .Where(token => token.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static string ToPackageName(string sourceName)
        {
            if (string.IsNullOrWhiteSpace(sourceName))
                return "Imported";
            var cleaned = SanitizeFileName(sourceName.Trim());
            return cleaned.Length == 0
                ? "Imported"
                : char.ToUpperInvariant(cleaned[0]) + cleaned.Substring(1);
        }

        private static void WriteReport(ImportContext context)
        {
            var lines = new List<string>
            {
                "OHMS Structured FX Import Report",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                string.Empty,
                context.Report.ToSummary(),
                string.Empty,
                "Unresolved references:"
            };
            lines.AddRange(context.Report.UnresolvedReferences.Count > 0
                ? context.Report.UnresolvedReferences
                : new[] { "(none)" });
            lines.Add(string.Empty);
            lines.Add("Warnings:");
            lines.AddRange(context.Report.Warnings.Count > 0
                ? context.Report.Warnings.Distinct().Take(500)
                : new[] { "(none)" });

            File.WriteAllLines(ToAbsoluteAssetPath($"{context.PackageRoot}/OHMS_IMPORT_REPORT.txt"), lines);
            AssetDatabase.Refresh();
        }

        private static bool LooksLikePng(byte[] bytes)
            => bytes != null && bytes.Length >= 8 &&
               bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
               bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A;

        private static long ExtractLong(string text, string marker, long fallback)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(marker))
                return fallback;
            var index = text.IndexOf(marker, StringComparison.Ordinal);
            if (index < 0)
                return fallback;
            index = text.IndexOf(':', index);
            if (index < 0)
                return fallback;
            index++;
            while (index < text.Length && char.IsWhiteSpace(text[index])) index++;
            var end = index;
            while (end < text.Length && (char.IsDigit(text[end]) || text[end] == '-')) end++;
            return long.TryParse(text.Substring(index, end - index), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : fallback;
        }

        private static string SafePathId(long value)
        {
            if (value == long.MinValue)
                return "min";
            return Math.Abs(value).ToString(CultureInfo.InvariantCulture);
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "Unnamed";
            var invalid = Path.GetInvalidFileNameChars();
            var chars = name.Select(ch => invalid.Contains(ch) || ch == '/' || ch == '\\' ? '_' : ch).ToArray();
            return new string(chars).Trim();
        }

        private static void EnsureAssetFolder(string assetPath)
        {
            assetPath = NormalizeAssetPath(assetPath);
            if (AssetDatabase.IsValidFolder(assetPath))
                return;
            if (!assetPath.StartsWith("Assets", StringComparison.Ordinal))
                throw new InvalidOperationException($"Output must be inside Assets/: {assetPath}");

            var parts = assetPath.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static string NormalizeAssetPath(string path) => path.Replace('\\', '/').TrimEnd('/');

        private static string ToAbsoluteAssetPath(string assetPath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.Combine(projectRoot, NormalizeAssetPath(assetPath).Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
#endif
