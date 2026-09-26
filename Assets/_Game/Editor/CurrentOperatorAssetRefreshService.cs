#if UNITY_EDITOR
using System;
using ArknightsACT.Gameplay.Characters;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArknightsACT.Editor
{
    internal static class PrototypeOperatorEditorSelection
    {
        private const string OperatorKey = "ArknightsACT.Editor.CurrentOperatorId";
        private const string SkinKey = "ArknightsACT.Editor.CurrentOperatorSkinId";

        public static void Remember(string operatorId, string skinId)
        {
            if (!string.IsNullOrWhiteSpace(operatorId))
                EditorPrefs.SetString(OperatorKey, operatorId);
            if (!string.IsNullOrWhiteSpace(skinId))
                EditorPrefs.SetString(SkinKey, skinId);
        }

        public static bool TryResolve(
            out PlayableOperatorDefinition definition,
            out PlayableOperatorSkinDefinition skin,
            out PlayableOperatorIdentity sceneIdentity)
        {
            definition = null;
            skin = null;
            sceneIdentity = FindCurrentSceneIdentity();

            var operatorId = sceneIdentity != null
                ? sceneIdentity.OperatorId
                : EditorPrefs.GetString(OperatorKey, string.Empty);
            var skinId = sceneIdentity != null
                ? sceneIdentity.SkinId
                : EditorPrefs.GetString(SkinKey, string.Empty);

            var definitions = PrototypeOperatorRegistry.GetDefinitions();
            if (definitions == null || definitions.Count == 0)
                return false;

            for (var i = 0; i < definitions.Count; i++)
            {
                var candidate = definitions[i];
                if (candidate == null ||
                    !string.Equals(candidate.OperatorId, operatorId, StringComparison.OrdinalIgnoreCase))
                    continue;

                definition = candidate;
                skin = FindSkin(candidate, skinId) ?? FindFirstUsableSkin(candidate);
                break;
            }

            if (definition == null)
            {
                definition = definitions[0];
                skin = FindFirstUsableSkin(definition);
            }

            if (definition == null || skin == null)
                return false;

            Remember(definition.OperatorId, skin.SkinId);
            return true;
        }

        public static PlayableOperatorIdentity FindCurrentSceneIdentity()
        {
            var identities = UnityEngine.Object.FindObjectsByType<PlayableOperatorIdentity>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            if (identities == null || identities.Length == 0)
                return null;

            for (var i = 0; i < identities.Length; i++)
            {
                var identity = identities[i];
                if (identity != null && identity.gameObject.activeInHierarchy)
                    return identity;
            }

            return identities[0];
        }

        private static PlayableOperatorSkinDefinition FindSkin(
            PlayableOperatorDefinition definition,
            string skinId)
        {
            if (definition?.Skins == null)
                return null;

            var skins = definition.Skins;
            for (var i = 0; i < skins.Count; i++)
            {
                var candidate = skins[i];
                if (candidate != null &&
                    string.Equals(candidate.SkinId, skinId, StringComparison.OrdinalIgnoreCase))
                    return candidate;
            }

            return null;
        }

        private static PlayableOperatorSkinDefinition FindFirstUsableSkin(
            PlayableOperatorDefinition definition)
        {
            if (definition?.Skins == null)
                return null;

            var skins = definition.Skins;
            for (var i = 0; i < skins.Count; i++)
            {
                var candidate = skins[i];
                if (candidate != null && !candidate.IsReserved)
                    return candidate;
            }

            return skins.Count > 0 ? skins[0] : null;
        }
    }

    internal static class CurrentOperatorAssetRefreshService
    {
        private static bool _refreshing;

        private static bool _reloadRefreshPending;

        [DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            if (_reloadRefreshPending)
                return;

            _reloadRefreshPending = true;
            EditorApplication.update -= RefreshAfterCompileWhenReady;
            EditorApplication.update += RefreshAfterCompileWhenReady;
        }

        private static void RefreshAfterCompileWhenReady()
        {
            if (EditorApplication.isCompiling ||
                EditorApplication.isUpdating)
                return;

            EditorApplication.update -= RefreshAfterCompileWhenReady;
            _reloadRefreshPending = false;

            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            RefreshCurrent(force: false, reason: "script reload");
        }

        public static void RefreshCurrent(bool force, string reason = "manual")
        {
            if (_refreshing ||
                EditorApplication.isCompiling ||
                EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (!PrototypeOperatorEditorSelection.TryResolve(
                    out var definition,
                    out var skin,
                    out var sceneIdentity))
            {
                Debug.LogWarning("[ArknightsACT/OperatorAssets] No current operator/skin could be resolved.");
                return;
            }

            Refresh(definition, skin, sceneIdentity, force, reason);
        }

        public static void Refresh(
            PlayableOperatorDefinition definition,
            PlayableOperatorSkinDefinition skin,
            PlayableOperatorIdentity sceneIdentity,
            bool force,
            string reason)
        {
            if (_refreshing || definition == null || skin == null)
                return;

            if (!PrototypeOperatorRegistry.TryGetBuilder(definition.OperatorId, out var builder))
            {
                Debug.LogWarning(
                    $"[ArknightsACT/OperatorAssets] No builder for {definition.OperatorId}; refresh skipped.");
                return;
            }

            try
            {
                _refreshing = true;
                PrototypeOperatorEditorSelection.Remember(definition.OperatorId, skin.SkinId);

                builder.RefreshAssets(definition, skin, force);

                if (sceneIdentity != null &&
                    string.Equals(sceneIdentity.OperatorId, definition.OperatorId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(sceneIdentity.SkinId, skin.SkinId, StringComparison.OrdinalIgnoreCase))
                {
                    builder.RefreshExisting(sceneIdentity.gameObject, definition, skin);
                    EditorUtility.SetDirty(sceneIdentity.gameObject);

                    var scene = SceneManager.GetActiveScene();
                    if (scene.IsValid() && scene.isLoaded && !EditorApplication.isPlayingOrWillChangePlaymode)
                        EditorSceneManager.MarkSceneDirty(scene);
                }

                AssetDatabase.SaveAssets();
                Debug.Log(
                    $"[ArknightsACT/OperatorAssets] Refreshed current operator only: " +
                    $"{definition.OperatorId}/{skin.SkinId}; force={force}; reason={reason}.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                _refreshing = false;
            }
        }
    }
}
#endif
