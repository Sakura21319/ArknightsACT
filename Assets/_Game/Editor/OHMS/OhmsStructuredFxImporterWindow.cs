#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor.OHMS
{
    internal sealed class OhmsStructuredFxImporterWindow : EditorWindow
    {
        private string _sourceFolder = string.Empty;
        private string _outputRoot = OhmsStructuredFxImporter.DefaultOutputRoot;
        private string _packageName = "Imported";
        private string _include = string.Empty;
        private string _exclude = string.Empty;
        private bool _preserveLayers;
        private Vector2 _scroll;
        private OhmsStructuredFxImporter.ScanResult _scan;
        private string _status = "Choose an OHMS structured export folder (assets.json + things/).";

        internal static void Open()
        {
            var window = GetWindow<OhmsStructuredFxImporterWindow>("OHMS FX Importer");
            window.minSize = new Vector2(620f, 520f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("OHMS Structured Effect Importer", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Generic importer for AssetStudio-Arknights/OHMS Structured JSON exports. " +
                "It rebuilds GameObject hierarchies, ParticleSystems, renderers, meshes, materials and internal textures. " +
                "Original game MonoBehaviours and missing Animator/AnimationClip payloads are reported instead of guessed.",
                MessageType.Info);

            DrawPathRow("Source export", ref _sourceFolder, BrowseSource);
            DrawPathRow("Output root", ref _outputRoot, null);
            _packageName = EditorGUILayout.TextField("Package name", _packageName);
            _include = EditorGUILayout.TextField("Include tokens", _include);
            _exclude = EditorGUILayout.TextField("Exclude tokens", _exclude);
            _preserveLayers = EditorGUILayout.ToggleLeft("Preserve original Unity layers (normally leave OFF)", _preserveLayers);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Scan", GUILayout.Height(28f)))
                    Scan();
                if (GUILayout.Button("Preset: Ch'en combat", GUILayout.Height(28f)))
                {
                    _packageName = "Chen";
                    _include = "chen_skill_02_,chen_skill_03_,chen_attack_01_";
                    _exclude = "_sale#10,_nian#2";
                    Repaint();
                }
                GUI.enabled = _scan != null;
                if (GUILayout.Button("Import filtered roots", GUILayout.Height(28f)))
                    Import();
                GUI.enabled = true;
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox(_status, MessageType.None);

            if (_scan == null)
                return;

            var filtered = OhmsStructuredFxImporter.FilterRoots(_scan, _include, _exclude);
            EditorGUILayout.LabelField(
                $"Detected {_scan.RootGameObjects.Count} root GameObjects; current filter matches {filtered.Count}.",
                EditorStyles.boldLabel);

            if (_scan.TypeCounts.Count > 0)
            {
                var counts = string.Join(", ", _scan.TypeCounts
                    .OrderByDescending(pair => pair.Value)
                    .Take(12)
                    .Select(pair => $"{pair.Key}={pair.Value}"));
                EditorGUILayout.LabelField(counts, EditorStyles.wordWrappedMiniLabel);
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var record in filtered.Take(250))
                EditorGUILayout.LabelField($"• {record.Name}  [{record.PathID}]", EditorStyles.miniLabel);
            if (filtered.Count > 250)
                EditorGUILayout.LabelField($"… {filtered.Count - 250} more", EditorStyles.miniLabel);
            EditorGUILayout.EndScrollView();
        }

        private void BrowseSource()
        {
            var path = EditorUtility.OpenFolderPanel("Select OHMS structured export", _sourceFolder, string.Empty);
            if (string.IsNullOrWhiteSpace(path))
                return;
            _sourceFolder = path;
            Scan();
        }

        private void Scan()
        {
            if (!OhmsStructuredFxImporter.TryScan(_sourceFolder, out _scan, out var error))
            {
                _status = error;
                return;
            }

            _sourceFolder = _scan.SourceRoot;
            if (string.IsNullOrWhiteSpace(_packageName) || string.Equals(_packageName, "Imported", StringComparison.OrdinalIgnoreCase))
                _packageName = _scan.PackageName;
            _status = $"Loaded {_scan.RootGameObjects.Count} root GameObjects from {_scan.SourceRoot}.";
        }

        private void Import()
        {
            try
            {
                var report = OhmsStructuredFxImporter.Import(new OhmsStructuredFxImporter.ImportOptions
                {
                    SourceRoot = _sourceFolder,
                    OutputRoot = _outputRoot,
                    PackageName = _packageName,
                    IncludeTokens = _include,
                    ExcludeTokens = _exclude,
                    PreserveLayers = _preserveLayers
                });
                _status = report.ToSummary() + "\nSee OHMS_IMPORT_REPORT.txt inside the imported package for unresolved external references.";
                Debug.Log($"[ArknightsACT/OHMS] Import finished. {report.ToSummary()}");
            }
            catch (Exception exception)
            {
                _status = exception.Message;
                Debug.LogException(exception);
            }
        }

        private static void DrawPathRow(string label, ref string value, Action browse)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                value = EditorGUILayout.TextField(label, value);
                if (browse != null && GUILayout.Button("Browse…", GUILayout.Width(90f)))
                    browse();
            }
        }
    }
}
#endif
