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
            public PPtr[] m_Materials;
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
            public List<AssetRecord> RootGameObjects = new();
            public Dictionary<string, int> TypeCounts = new(StringComparer.OrdinalIgnoreCase);
        }

        internal sealed class ImportOptions
        {
            public string SourceRoot;
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
            public readonly List<string> UnresolvedReferences = new();
            public readonly List<string> Warnings = new();

            public string ToSummary()
            {
                return
                    $"Prefabs={PrefabsCreated}, GameObjects={GameObjectsCreated}, ParticleSystems={ParticleSystemsCreated}, " +
                    $"Materials={MaterialsCreated}, Textures={TexturesCreated}, Meshes={MeshesCreated}, " +
                    $"UnknownMonoBehaviours={UnknownMonoBehaviours}, MissingAnimatorPayloads={MissingAnimatorPayloads}, " +
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
            public ImportReport Report = new();
        }

        internal static bool TryScan(string selectedPath, out ScanResult scan, out string error)
        {
            scan = null;
            error = null;

            if (!TryResolveSourceRoot(selectedPath, out var sourceRoot, out error))
                return false;

            if (!TryReadIndex(sourceRoot, out var records, out error))
                return false;

            var context = new ImportContext
            {
                SourceRoot = sourceRoot,
                ThingsRoot = Path.Combine(sourceRoot, "things")
            };
            IndexRecords(context, records);

            scan = new ScanResult
            {
                SourceRoot = sourceRoot,
                PackageName = ToPackageName(new DirectoryInfo(sourceRoot).Name)
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
            if (!TryReadIndex(sourceRoot, out var records, out error))
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
                SourceRoot = sourceRoot,
                ThingsRoot = Path.Combine(sourceRoot, "things"),
                PackageRoot = packageRoot,
                PrefabRoot = prefabRoot,
                MaterialRoot = materialRoot,
                TextureRoot = textureRoot,
                MeshRoot = meshRoot
            };
            IndexRecords(context, records);

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
                    ImportRootPrefab(context, record);
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
                var path = AssetDatabase.GenerateUniqueAssetPath(
                    $"{context.PrefabRoot}/{SanitizeFileName(rootRecord.Name)}.prefab");
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
                var component = go.GetComponent<ParticleSystem>() ?? go.AddComponent<ParticleSystem>();
                ApplyEditorJson(context, record, component);
                context.Report.ParticleSystemsCreated++;
            }

            foreach (var record in components.Where(record => IsType(record, "ParticleSystemRenderer")))
            {
                var renderer = go.GetComponent<ParticleSystemRenderer>();
                if (renderer == null)
                {
                    var system = go.GetComponent<ParticleSystem>() ?? go.AddComponent<ParticleSystem>();
                    renderer = system.GetComponent<ParticleSystemRenderer>();
                }
                ApplyEditorJson(context, record, renderer);
                ApplyRendererMaterials(context, record, renderer, go.name);
            }

            foreach (var record in components.Where(record => IsType(record, "MeshFilter")))
            {
                var filter = go.GetComponent<MeshFilter>() ?? go.AddComponent<MeshFilter>();
                ApplyEditorJson(context, record, filter);
                if (TryReadJson<MeshFilterDump>(context, record, out var filterDump, out _) && filterDump.m_Mesh != null)
                    filter.sharedMesh = ResolveMesh(context, filterDump.m_Mesh, go.name);
            }

            foreach (var record in components.Where(record => IsType(record, "MeshRenderer")))
            {
                var renderer = go.GetComponent<MeshRenderer>() ?? go.AddComponent<MeshRenderer>();
                ApplyEditorJson(context, record, renderer);
                ApplyRendererMaterials(context, record, renderer, go.name);
            }

            foreach (var record in components.Where(record => IsType(record, "TrailRenderer")))
            {
                var renderer = go.GetComponent<TrailRenderer>() ?? go.AddComponent<TrailRenderer>();
                ApplyEditorJson(context, record, renderer);
                ApplyRendererMaterials(context, record, renderer, go.name);
            }

            foreach (var record in components.Where(record => IsType(record, "Animation")))
            {
                var animation = go.GetComponent<Animation>() ?? go.AddComponent<Animation>();
                ApplyEditorJson(context, record, animation);
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
                if (TryReadTextPayload(context, record, out var raw))
                {
                    var scriptId = ExtractLong(raw, "m_PathID", 0L);
                    context.Report.Warnings.Add(
                        $"Skipped original MonoBehaviour on '{go.name}' (asset ID {record.ID}, script/path hint {scriptId}). " +
                        "Standard Unity FX components are imported; game runtime scripts are intentionally not fabricated.");
                }
            }
        }

        private static void ApplyRendererMaterials(
            ImportContext context,
            AssetRecord record,
            Renderer renderer,
            string ownerName)
        {
            if (renderer == null || !TryReadJson<RendererDump>(context, record, out var dump, out _) || dump.m_Materials == null)
                return;

            var materials = new Material[dump.m_Materials.Length];
            for (var i = 0; i < materials.Length; i++)
                materials[i] = ResolveMaterial(context, dump.m_Materials[i], ownerName);
            renderer.sharedMaterials = materials;
        }

        private static Material ResolveMaterial(ImportContext context, PPtr pointer, string ownerName)
        {
            if (pointer == null || pointer.m_PathID == 0)
                return null;
            if (pointer.m_FileID != 0)
            {
                ReportExternal(context, pointer, ownerName, "Material");
                return null;
            }
            if (context.Materials.TryGetValue(pointer.m_PathID, out var cached))
                return cached;

            var record = ResolveInternal(context, pointer, "Material", ownerName);
            if (record == null)
                return null;
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

            var assetPath = AssetDatabase.GenerateUniqueAssetPath(
                $"{context.MaterialRoot}/{SanitizeFileName(material.name)}_{SafePathId(record.PathID)}.mat");
            AssetDatabase.CreateAsset(material, assetPath);
            context.Materials[pointer.m_PathID] = material;
            context.Report.MaterialsCreated++;
            return material;
        }

        private static Texture2D ResolveTexture(ImportContext context, PPtr pointer, string ownerName)
        {
            if (pointer == null || pointer.m_PathID == 0)
                return null;
            if (pointer.m_FileID != 0)
            {
                ReportExternal(context, pointer, ownerName, "Texture");
                return null;
            }
            if (context.Textures.TryGetValue(pointer.m_PathID, out var cached))
                return cached;

            var record = ResolveInternal(context, pointer, "Texture2D", ownerName);
            if (record == null)
                return null;
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
            var assetPath = AssetDatabase.GenerateUniqueAssetPath($"{context.TextureRoot}/{fileName}");
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
                context.Textures[pointer.m_PathID] = texture;
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

            var assetPath = AssetDatabase.GenerateUniqueAssetPath(
                $"{context.MeshRoot}/{SanitizeFileName(record.Name)}_{SafePathId(record.PathID)}.asset");
            AssetDatabase.CreateAsset(mesh, assetPath);
            context.Meshes[pointer.m_PathID] = mesh;
            context.Report.MeshesCreated++;
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

        private static void ApplyEditorJson(ImportContext context, AssetRecord record, UnityEngine.Object target)
        {
            if (target == null || !TryReadTextPayload(context, record, out var raw))
                return;

            try
            {
                EditorJsonUtility.FromJsonOverwrite(raw, target);
            }
            catch (Exception exception)
            {
                context.Report.Warnings.Add(
                    $"EditorJsonUtility could not fully apply {record.Type?.name}:{record.Name} (ID {record.ID}): {exception.Message}");
            }
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
            => Path.Combine(context.ThingsRoot, record.ID + ".ttbin");

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
