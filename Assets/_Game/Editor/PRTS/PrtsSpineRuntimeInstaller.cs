#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace ArknightsACT.Editor.PRTS
{
    internal static class PrtsSpineRuntimeInstaller
    {
        // Pinned for reproducibility. This fork adds multi-version binary import including Arknights Spine 3.8.
        private const string RuntimeCommit = "f569ce2e8f5cbe5aed2c6023d6569efc5abc44af";
        private const string Repo = "https://github.com/ZeroFlyFly/WaifuSpineRuntime.git";
        private static readonly Queue<string> PendingPackages = new();
        private static AddRequest _request;
        private static bool _busy;

        public static bool IsRuntimeAvailable => FindType("Spine.Unity.SkeletonAnimation") != null;

        [MenuItem("ArknightsACT/Assets/PRTS/1. Install Spine 3.8-Compatible Runtime")]
        public static void Install()
        {
            if (_busy)
            {
                Debug.LogWarning("[ArknightsACT/PRTS] Spine runtime installation is already running.");
                return;
            }

            if (IsRuntimeAvailable)
            {
                EditorUtility.DisplayDialog(
                    "ArknightsACT",
                    "A Spine.Unity.SkeletonAnimation runtime is already loaded. No package changes were made.",
                    "OK");
                return;
            }

            var accepted = EditorUtility.DisplayDialog(
                "Install Spine-compatible runtime?",
                "Arknights PRTS battle models are Spine 3.8 binary assets. The official Spine 3.8 Unity runtime does not support Unity 6, so this prototype can install a pinned third-party multi-version runtime fork.\n\n" +
                "The fork is based on Esoteric Software Spine runtimes. Review Spine runtime licensing before distributing a build. The game code itself does not take a hard dependency on this package.\n\n" +
                "Install the pinned packages now?",
                "Install",
                "Cancel");

            if (!accepted)
                return;

            PendingPackages.Clear();
            PendingPackages.Enqueue(Repo + "?path=Assets/Spine/Runtime/spine-csharp#" + RuntimeCommit);
            PendingPackages.Enqueue(Repo + "?path=Assets/Spine#" + RuntimeCommit);
            _busy = true;
            EditorApplication.update += Tick;
            StartNext();
        }

        [MenuItem("ArknightsACT/Assets/PRTS/1. Install Spine 3.8-Compatible Runtime", true)]
        private static bool ValidateInstall() => !_busy;

        [MenuItem("ArknightsACT/Assets/PRTS/Open Runtime Fork on GitHub")]
        private static void OpenRuntimeFork()
        {
            Application.OpenURL("https://github.com/ZeroFlyFly/WaifuSpineRuntime");
        }

        private static void StartNext()
        {
            if (PendingPackages.Count == 0)
            {
                Finish(true, "Spine runtime packages were added. Unity may recompile/reload the domain now. After compilation, run '3. Build Presentation Prefabs'.");
                return;
            }

            var url = PendingPackages.Dequeue();
            Debug.Log("[ArknightsACT/PRTS] Installing package: " + url);
            _request = Client.Add(url);
        }

        private static void Tick()
        {
            if (!_busy || _request == null || !_request.IsCompleted)
                return;

            if (_request.Status == StatusCode.Failure)
            {
                var error = _request.Error != null ? _request.Error.message : "Unknown Package Manager error.";
                Finish(false, "Spine runtime installation failed: " + error);
                return;
            }

            Debug.Log("[ArknightsACT/PRTS] Installed: " + (_request.Result != null ? _request.Result.packageId : "package"));
            _request = null;
            StartNext();
        }

        private static void Finish(bool success, string message)
        {
            EditorApplication.update -= Tick;
            _request = null;
            PendingPackages.Clear();
            _busy = false;

            if (success)
            {
                Debug.Log("[ArknightsACT/PRTS] " + message);
                EditorUtility.DisplayDialog("ArknightsACT", message, "OK");
            }
            else
            {
                Debug.LogError("[ArknightsACT/PRTS] " + message);
                EditorUtility.DisplayDialog("ArknightsACT - install failed", message, "OK");
            }
        }

        private static System.Type FindType(string fullName)
        {
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName, false);
                if (type != null)
                    return type;
            }
            return null;
        }
    }
}
#endif
