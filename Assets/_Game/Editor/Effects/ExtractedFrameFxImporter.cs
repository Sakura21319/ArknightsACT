#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArknightsACT.Gameplay.Characters.Chen;
using ArknightsACT.Gameplay.Presentation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArknightsACT.Editor.Effects
{
    internal static class ExtractedFrameFxImporter
    {
        internal const string DefaultOutputRoot = "Assets/_Game/Art/FX/Extracted";
        private const string PrefabFolder = "Prefabs";
        private const string AnimationFolder = "Animations";
        private const string ControllerFolder = "Controllers";
        private const string MaterialFolder = "Materials";
        private const float DefaultFps = 30f;
        private const float DefaultPixelsPerUnit = 512f;

        internal sealed class ImportResult
        {
            internal readonly List<string> PrefabPaths = new();
            internal string FramesRoot;
            internal string PackageRoot;
            internal int ImportedEffects;
            internal int ImportedFrames;
        }

        internal static ImportResult Import(string sourceRoot, string outputRoot, string packageName)
        {
            var framesRoot = ResolveFramesRoot(sourceRoot);
            if (string.IsNullOrWhiteSpace(framesRoot))
                throw new InvalidOperationException(
                    "找不到 frames 目录。请选择提取结果根目录、delivery_v2、run 目录，或直接选择 frames 目录。\n" +
                    "目录中应包含 skill_02_start、skill_03_start 或 attack_01_start 等子文件夹。");

            packageName = SanitizeName(packageName, "Imported");
            outputRoot = NormalizeAssetPath(string.IsNullOrWhiteSpace(outputRoot) ? DefaultOutputRoot : outputRoot);
            EnsureAssetFolder(outputRoot);
            var packageRoot = outputRoot + "/" + packageName;
            EnsureAssetFolder(packageRoot);
            foreach (var child in new[] { PrefabFolder, AnimationFolder, ControllerFolder, MaterialFolder })
                EnsureAssetFolder(packageRoot + "/" + child);

            var result = new ImportResult { FramesRoot = framesRoot, PackageRoot = packageRoot };
            var sourceFolders = Directory.GetDirectories(framesRoot)
                .Where(IsEffectFolder)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (sourceFolders.Length == 0)
                throw new InvalidOperationException("frames 目录中没有 skill_* 特效文件夹。");

            var copiedFolders = new List<string>();
            foreach (var sourceFolder in sourceFolders)
            {
                var effectName = SanitizeName(Path.GetFileName(sourceFolder), "Effect");
                var destination = packageRoot + "/Frames/" + effectName;
                EnsureAssetFolder(packageRoot + "/Frames");
                EnsureAssetFolder(destination);
                CopyFrames(sourceFolder, destination, result);
                copiedFolders.Add(destination);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var material = CreateOrLoadMaterial(packageRoot + "/" + MaterialFolder + "/" + packageName + "_Frame.mat");
            foreach (var destination in copiedFolders)
            {
                var effectName = Path.GetFileName(destination);
                var sprites = ImportSprites(destination);
                if (sprites.Length == 0)
                {
                    Debug.LogWarning("[ArknightsACT/FX] 跳过空特效目录: " + destination);
                    continue;
                }

                // Extracted output is a finite captured sequence.  Do not infer an infinite
                // runtime loop from the client asset name "buff"; the runtime controller can
                // explicitly keep an effect alive later if a future skill needs that behavior.
                var loop = ShouldLoopEffect(effectName);
                var clipPath = packageRoot + "/" + AnimationFolder + "/" + effectName + ".anim";
                var controllerPath = packageRoot + "/" + ControllerFolder + "/" + effectName + ".controller";
                var prefabPath = packageRoot + "/" + PrefabFolder + "/" + effectName + ".prefab";
                DeleteAssetIfExists(clipPath);
                DeleteAssetIfExists(controllerPath);
                DeleteAssetIfExists(prefabPath);

                var clip = CreateClip(clipPath, sprites, loop);
                var controller = CreateController(controllerPath, clip);
                CreatePrefab(prefabPath, effectName, sprites[0], material, controller, sprites.Length, loop);
                result.PrefabPaths.Add(prefabPath);
                result.ImportedEffects++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            return result;
        }

        internal static ImportResult ImportSelected(
            IEnumerable<string> sourceFolders,
            string outputRoot,
            string packageName)
        {
            if (sourceFolders == null)
                throw new ArgumentNullException(nameof(sourceFolders));

            var folders = sourceFolders
                .Where(path => !string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (folders.Length == 0)
                throw new InvalidOperationException("没有找到指定的特效帧目录。");

            packageName = SanitizeName(packageName, "Imported");
            outputRoot = NormalizeAssetPath(string.IsNullOrWhiteSpace(outputRoot) ? DefaultOutputRoot : outputRoot);
            EnsureAssetFolder(outputRoot);
            var packageRoot = outputRoot + "/" + packageName;
            EnsureAssetFolder(packageRoot);
            foreach (var child in new[] { PrefabFolder, AnimationFolder, ControllerFolder, MaterialFolder, "Frames" })
                EnsureAssetFolder(packageRoot + "/" + child);

            var result = new ImportResult
            {
                FramesRoot = Path.GetDirectoryName(folders[0]),
                PackageRoot = packageRoot
            };

            var copiedFolders = new List<string>();
            foreach (var sourceFolder in folders)
            {
                var effectName = SanitizeName(Path.GetFileName(sourceFolder), "Effect");
                var destination = packageRoot + "/Frames/" + effectName;
                EnsureAssetFolder(destination);
                CopyFrames(sourceFolder, destination, result);
                copiedFolders.Add(destination);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var material = CreateOrLoadMaterial(packageRoot + "/" + MaterialFolder + "/" + packageName + "_Frame.mat");
            foreach (var destination in copiedFolders)
            {
                var effectName = Path.GetFileName(destination);
                var sprites = ImportSprites(destination);
                if (sprites.Length == 0)
                    continue;

                var loop = ShouldLoopEffect(effectName);
                var clipPath = packageRoot + "/" + AnimationFolder + "/" + effectName + ".anim";
                var controllerPath = packageRoot + "/" + ControllerFolder + "/" + effectName + ".controller";
                var prefabPath = packageRoot + "/" + PrefabFolder + "/" + effectName + ".prefab";
                DeleteAssetIfExists(clipPath);
                DeleteAssetIfExists(controllerPath);
                DeleteAssetIfExists(prefabPath);

                var clip = CreateClip(clipPath, sprites, loop);
                var controller = CreateController(controllerPath, clip);
                CreatePrefab(prefabPath, effectName, sprites[0], material, controller, sprites.Length, loop);
                result.PrefabPaths.Add(prefabPath);
                result.ImportedEffects++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            return result;
        }

        internal static string ResolveFramesRoot(string sourceRoot)
        {
            if (string.IsNullOrWhiteSpace(sourceRoot))
                return null;
            sourceRoot = Path.GetFullPath(sourceRoot.Trim());
            var candidates = new[]
            {
                sourceRoot,
                Path.Combine(sourceRoot, "frames"),
                Path.Combine(sourceRoot, "effects", "frames"),
                Path.Combine(sourceRoot, "delivery_v2", "frames")
            };
            foreach (var candidate in candidates)
            {
                if (!Directory.Exists(candidate))
                    continue;
                if (Directory.GetDirectories(candidate)
                    .Any(IsEffectFolder))
                    return candidate;
            }
            return null;
        }

        private static void CopyFrames(string sourceFolder, string destination, ImportResult result)
        {
            foreach (var source in Directory.GetFiles(sourceFolder, "*.png")
                         .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                var fileName = Path.GetFileName(source);
                var relativeAssetPath = destination + "/" + fileName;
                var absoluteDestination = ToAbsoluteProjectPath(relativeAssetPath);
                File.Copy(source, absoluteDestination, true);
                result.ImportedFrames++;
            }
        }

        private static Sprite[] ImportSprites(string folder)
        {
            var paths = AssetDatabase.FindAssets("t:Texture2D", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            foreach (var path in paths)
            {
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                    continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 2048;
                importer.spritePixelsPerUnit = DefaultPixelsPerUnit;
                importer.SaveAndReimport();
            }

            return paths.Select(AssetDatabase.LoadAssetAtPath<Sprite>)
                .Where(sprite => sprite != null)
                .ToArray();
        }

        private static AnimationClip CreateClip(string path, Sprite[] sprites, bool loop)
        {
            var clip = new AnimationClip
            {
                name = Path.GetFileNameWithoutExtension(path),
                frameRate = DefaultFps,
                wrapMode = loop ? WrapMode.Loop : WrapMode.Once
            };
            var keys = new ObjectReferenceKeyframe[sprites.Length];
            for (var i = 0; i < sprites.Length; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i / DefaultFps, value = sprites[i] };
            var binding = EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite");
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            settings.stopTime = sprites.Length / DefaultFps;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        private static AnimatorController CreateController(string path, AnimationClip clip)
        {
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            var stateMachine = controller.layers[0].stateMachine;
            var state = stateMachine.AddState("Play");
            state.motion = clip;
            stateMachine.defaultState = state;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void CreatePrefab(
            string path,
            string effectName,
            Sprite firstSprite,
            Material material,
            AnimatorController controller,
            int frameCount,
            bool loop)
        {
            var root = new GameObject("FX_" + effectName);
            var renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = firstSprite;
            renderer.sharedMaterial = material;
            renderer.sortingOrder = 90;

            var animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;

            var playback = root.AddComponent<ExtractedFrameFxPlayback>();
            var playbackSpeed = effectName.StartsWith("attack_", StringComparison.OrdinalIgnoreCase)
                ? ExtractedFrameFxPlayback.BasicAttackPlaybackSpeed
                : ExtractedFrameFxPlayback.DefaultPlaybackSpeed;
            playback.Configure(
                animator,
                frameCount / DefaultFps,
                loop,
                playbackSpeed);

            // The active prototype is 2.5D. BillboardPresentation25D is deliberately optional at
            // runtime and falls back to Camera.main, so the generated prefab remains usable in a
            // plain 2D test scene as well.
            root.AddComponent<BillboardPresentation25D>();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static Material CreateOrLoadMaterial(string path)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("ArknightsACT/ImportedClientFX") ??
                             Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ??
                             Shader.Find("Sprites/Default");
                if (shader == null)
                    throw new InvalidOperationException("找不到可用的特效 Shader。");
                material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
            }

            // FX should remain visible over the character/target presentation. Transparent
            // sorting order alone is not enough when the 2.5D presentation writes depth.
            // The exporter writes alpha as the source frame's maximum RGB intensity because
            // the original additive client shader does not write a useful RT alpha channel.
            // Using SrcAlpha here would multiply that intensity a second time and make the
            // imported FX look washed out/faint, especially on soft edges.
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.One);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.One);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            if (material.HasProperty("_ZTest")) material.SetFloat("_ZTest", (float)CompareFunction.Always);
            if (AssetDatabase.GetAssetPath(material) == string.Empty)
                AssetDatabase.CreateAsset(material, path);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureAssetFolder(string assetPath)
        {
            assetPath = NormalizeAssetPath(assetPath).TrimEnd('/');
            if (AssetDatabase.IsValidFolder(assetPath))
                return;
            var parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            var name = Path.GetFileName(assetPath);
            if (string.IsNullOrWhiteSpace(parent) || parent == assetPath)
                return;
            EnsureAssetFolder(parent);
            if (!AssetDatabase.IsValidFolder(assetPath))
                AssetDatabase.CreateFolder(parent, name);
        }

        private static void DeleteAssetIfExists(string assetPath)
        {
            if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
                AssetDatabase.DeleteAsset(assetPath);
        }

        private static string NormalizeAssetPath(string path) => (path ?? string.Empty).Replace('\\', '/').TrimEnd('/');

        private static string ToAbsoluteProjectPath(string assetPath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            return Path.Combine(projectRoot ?? string.Empty, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string SanitizeName(string value, string fallback)
        {
            value = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            foreach (var invalid in Path.GetInvalidFileNameChars())
                value = value.Replace(invalid.ToString(), string.Empty);
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static bool ShouldLoopEffect(string effectName)
        {
            if (string.IsNullOrWhiteSpace(effectName))
                return false;

            return effectName.StartsWith("skill_03_buff_02", StringComparison.OrdinalIgnoreCase) ||
                   effectName.StartsWith("skill_03_buff_03", StringComparison.OrdinalIgnoreCase) ||
                   effectName.Equals("common_064_ignite_attack_red", StringComparison.OrdinalIgnoreCase) ||
                   effectName.Equals("common_combustion_buff_02", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsEffectFolder(string path)
        {
            var name = Path.GetFileName(path);
            return name.StartsWith("skill_", StringComparison.OrdinalIgnoreCase) ||
                   name.StartsWith("attack_", StringComparison.OrdinalIgnoreCase) ||
                   name.StartsWith("buff_", StringComparison.OrdinalIgnoreCase) ||
                   name.IndexOf("_attack_", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    internal sealed class ExtractedFrameFxImporterWindow : EditorWindow
    {
        private const string SourcePref = "ArknightsACT.ExtractedFx.Source";
        private string _sourceFolder;
        private string _outputRoot = ExtractedFrameFxImporter.DefaultOutputRoot;
        private string _packageName = "Chen";
        private string _status = "选择提取工具输出目录，然后导入并生成 Prefab。";

        internal static void Open()
        {
            var window = GetWindow<ExtractedFrameFxImporterWindow>("Extracted FX Importer");
            window.minSize = new Vector2(680f, 300f);
            window.Show();
        }

        private void OnEnable()
        {
            _sourceFolder = EditorPrefs.GetString(SourcePref, string.Empty);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Extracted Frame FX Importer", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "读取特效提取工具生成的 frames 目录，复制 PNG、创建 Sprite 动画和 Prefab。\n" +
                "支持 skill_* 技能特效和 attack_* 普攻特效；Chen 包会自动映射到 Player_Chen 的 CustomFxMountPoint、普攻和技能事件。",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                _sourceFolder = EditorGUILayout.TextField("提取结果目录", _sourceFolder);
                if (GUILayout.Button("浏览...", GUILayout.Width(80f)))
                {
                    var selected = EditorUtility.OpenFolderPanel("选择特效提取结果", _sourceFolder, string.Empty);
                    if (!string.IsNullOrWhiteSpace(selected))
                        _sourceFolder = selected;
                }
            }
            _packageName = EditorGUILayout.TextField("角色包名", _packageName);
            _outputRoot = EditorGUILayout.TextField("Unity 输出目录", _outputRoot);

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("导入并生成 Prefab", GUILayout.Height(32f)))
                    Import(false);
                if (GUILayout.Button("导入并应用到当前陈原型", GUILayout.Height(32f)))
                    Import(true);
            }
            EditorGUILayout.HelpBox(_status, MessageType.None);
        }

        private void Import(bool apply)
        {
            try
            {
                var result = ExtractedFrameFxImporter.Import(_sourceFolder, _outputRoot, _packageName);
                EditorPrefs.SetString(SourcePref, _sourceFolder ?? string.Empty);
                var applied = false;
                if (apply && string.Equals(_packageName.Trim(), "Chen", StringComparison.OrdinalIgnoreCase))
                    applied = ChenExtractedFxSetup.TryApplyToOpenScene();
                _status = $"完成：{result.ImportedEffects} 个特效，{result.ImportedFrames} 张帧。\n" +
                          $"Prefab：{result.PackageRoot}/Prefabs\n" +
                          (applied
                              ? "已应用到当前场景的 Player_Chen。"
                              : "下次执行 ArknightsACT > Build Prototype Scene 时会自动接入；当前没有可修改的 Player_Chen。\n" +
                                "也可以重新点击“导入并应用到当前陈原型”。");
                Debug.Log("[ArknightsACT/FX] " + _status);
            }
            catch (Exception exception)
            {
                _status = exception.Message;
                Debug.LogException(exception);
            }
        }
    }

    internal static class ChenExtractedFxSetup
    {
        private const string Root = "Assets/_Game/Art/FX/Extracted/Chen/Prefabs";

        internal static bool HasImportedEffects()
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/skill_02_start.prefab") != null ||
                   AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/skill_03_start.prefab") != null ||
                   AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/attack_01_start.prefab") != null ||
                   AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/skill_01_start.prefab") != null;
        }

        internal static void Configure(GameObject owner)
        {
            if (owner == null)
                return;
            var controller = owner.GetComponent<ChenExtractedFxController>() ??
                             owner.AddComponent<ChenExtractedFxController>();
            controller.Configure(
                Load("skill_02_start"),
                Load("skill_02_buff"),
                Load("skill_02_hit"),
                Load("skill_03_start"),
                Load("skill_03_start_02"),
                Load("skill_03_start_03"),
                Enumerable.Range(1, 10).Select(index => Load("skill_03_hit_" + index.ToString("00"))).ToArray(),
                Load("attack_01_start") ?? Load("skill_01_start"),
                Load("attack_01_hit") ?? Load("skill_01_hit"),
                Load("skill_01_start"),
                Load("skill_01_hit"));
            EditorUtility.SetDirty(controller);
        }

        internal static bool TryApplyToOpenScene()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!scene.IsValid())
                return false;
            foreach (var root in scene.GetRootGameObjects())
            {
                var player = FindChild(root.transform, "Player_Chen");
                if (player == null)
                    continue;
                Configure(player.gameObject);
                var old = player.GetComponent<ChenCustomFxController>();
                if (old != null)
                    old.enabled = false;
                EditorUtility.SetDirty(player.gameObject);
                return true;
            }
            return false;
        }

        private static GameObject Load(string name) =>
            AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/" + name + ".prefab");

        private static Transform FindChild(Transform root, string name)
        {
            if (root == null)
                return null;
            if (string.Equals(root.name, name, StringComparison.Ordinal))
                return root;
            for (var i = 0; i < root.childCount; i++)
            {
                var result = FindChild(root.GetChild(i), name);
                if (result != null)
                    return result;
            }
            return null;
        }
    }
}
#endif
