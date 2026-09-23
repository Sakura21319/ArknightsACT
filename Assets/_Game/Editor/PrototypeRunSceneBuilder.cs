#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArknightsACT.Editor
{
    /// <summary>
    /// Single PrototypeRun composition pipeline for every operator/skin.
    /// Character-specific builders only construct players; scene/world wiring stays here.
    /// </summary>
    internal static class PrototypeRunSceneBuilder
    {
        private const string ScenePath = "Assets/_Game/Scenes/PrototypeRun.unity";

        public static void Build(string activeOperatorId, string activeSkinId = null)
        {
            PrototypeOperatorEditorSelection.Remember(activeOperatorId, activeSkinId);

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(
                    "ArknightsACT",
                    "不能在 Play Mode 中构建场景。请先停止运行。",
                    "确定");
                return;
            }

            PrototypePlayerSettings.Apply();

            // Reuse the validated 2.5D shell for environment/camera setup, then replace its
            // fixed demo actors with the data-driven operator catalog and roguelite runtime.
            Prototype25DSceneBuilder.Build();
            RemoveDemoActor("Player_Chen_25D");
            RemoveDemoActor("Enemy_Soldier");
            RemoveDemoActor("Enemy_Hound");
            RemoveDemoActor("Enemy_Crossbowman");
            RemoveDemoActor("[3D Map]");

            var camera = GameObject.Find("Main Camera")?.GetComponent<Camera>();
            if (camera == null)
            {
                Debug.LogError(
                    "[ArknightsACT/PrototypeRun] Main Camera was not created by the 2.5D world builder.");
                return;
            }

            PrototypeFactory.CreateServices();

            var roster = PrototypeOperatorRosterFactory.Build(
                camera,
                activeOperatorId,
                activeSkinId);
            var player = roster.ActivePlayer;
            Prototype25DProductionFactory.ConfigureCamera(camera, player.transform);

            var enemyTemplates = Prototype25DProductionFactory.CreateEnemyTemplates(camera);
            var treasureTemplates = PrototypeTreasureFactory.CreateTemplates(camera);
            var rogueliteRoot = PrototypeRogueliteFactory.CreateExploration(player.transform);
            PrototypeOperatorRosterFactory.RegisterReserves(rogueliteRoot, roster);

            PrototypeStageRuntimeFactory.Create(
                player.transform,
                enemyTemplates,
                treasureTemplates,
                rogueliteRoot);

            var scene = SceneManager.GetActiveScene();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = player;
            EditorGUIUtility.PingObject(player);

            var identity = player.GetComponent<ArknightsACT.Gameplay.Characters.PlayableOperatorIdentity>();
            if (identity != null)
                PrototypeOperatorEditorSelection.Remember(identity.OperatorId, identity.SkinId);

            Debug.Log(
                identity != null
                    ? $"[ArknightsACT/PrototypeRun] Generated {ScenePath}; active=" +
                      $"{identity.OperatorId}/{identity.SkinId}; " +
                      $"switchTargets={roster.AllPlayers.Count}. Tab opens operator/skin selector."
                    : $"[ArknightsACT/PrototypeRun] Generated {ScenePath}.");
        }

        private static void RemoveDemoActor(string objectName)
        {
            var go = GameObject.Find(objectName);
            if (go != null)
                Object.DestroyImmediate(go);
        }
    }
}
#endif
