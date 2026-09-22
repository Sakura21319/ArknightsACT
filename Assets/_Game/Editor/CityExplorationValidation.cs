#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ArknightsACT.Gameplay.Roguelite;
using ArknightsACT.Gameplay.Roguelite.Routing;
using ArknightsACT.Gameplay.Roguelite.World;
using ArknightsACT.Gameplay.Roguelite.Treasure;
using ArknightsACT.Gameplay.Roguelite.Collectibles;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ArknightsACT.Editor
{
    /// <summary>Play Mode smoke test against the saved production scene, without rebuilding assets.</summary>
    public static class CityExplorationValidation
    {
        private const string ScenePath = "Assets/_Game/Scenes/PrototypeRun.unity";
        private const string LogDir = "Logs/CityExploration";
        private const string SessionKey = "CityValidation";

        private static double _next;
        private static double _timeout;
        private static int _stage = 1;
        private static int _errors;
        private static int _phase;
        private static bool _exitOnFinish;

        /// <summary>Batch entry point: `-executeMethod ArknightsACT.Editor.CityExplorationValidation.Run`.</summary>
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath);
            Start(exitOnFinish: true);
        }

        /// <summary>
        /// Interactive entry point. A batch Unity run cannot start while an Editor already holds the
        /// same project, so this runs the identical checks against the scene that is already open.
        /// </summary>
        private static void RunInteractive()
        {
            var active = EditorSceneManager.GetActiveScene();
            if (active.path != ScenePath)
            {
                Debug.LogError($"[CityExploration] Open {ScenePath} before validating (active scene: {active.path}).");
                return;
            }
            Start(exitOnFinish: false);
        }

        private static bool CanRunInteractive() => !EditorApplication.isPlaying && !EditorApplication.isCompiling;

        private static void Start(bool exitOnFinish)
        {
            Directory.CreateDirectory(LogDir);
            _exitOnFinish = exitOnFinish;
            Application.logMessageReceived -= Log;
            Application.logMessageReceived += Log;
            SessionState.SetBool(SessionKey, true);
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, false);
            // A domain reload normally resets these, but Play Mode Options can disable that.
            _stage = 1;
            _phase = 0;
            _errors = 0;
            _timeout = EditorApplication.timeSinceStartup + 150;
            _next = EditorApplication.timeSinceStartup + 8;
            Application.logMessageReceived -= Log;
            Application.logMessageReceived += Log;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }
        private static void Log(string text, string stack, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error) _errors++;
        }
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > _timeout) { Finish("Timed out", false); return; }
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < _next) return;
            try
            {
                var context = UnityEngine.Object.FindFirstObjectByType<RogueliteStageRuntimeContext>();
                if (context == null || context.StageRoot == null) throw new Exception("Missing stage context/root");
                if (_phase == 1)
                {
                    Capture($"interior-{_stage}", Vector3.zero, -1f);
                    ScreenCapture.CaptureScreenshot($"{LogDir}/hud-{_stage}.png");
                    _phase = 2;
                    _next = EditorApplication.timeSinceStartup + 1;
                    return;
                }
                if (_phase == 2)
                {
                    if (_stage == 3)
                    {
                        ValidateDeath();
                        Finish("Three-stage generation, actor-width doorways, search interruption, pause, duplicate, capacity, settlement and death-loss checks passed", _errors == 0);
                        return;
                    }
                    var runtime = context.GetComponent<RogueliteStageRuntimeController>();
                    typeof(RogueliteStageRuntimeController).GetMethod("AdvanceStage", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(runtime, null);
                    _stage++;
                    _phase = 0;
                    _next = EditorApplication.timeSinceStartup + 4;
                    return;
                }
                var rooms = EnterableBuilding25D.Active.Where(x => x != null && x.gameObject.activeInHierarchy).ToArray();
                var containers = SearchableContainer25D.Active.Where(x => x != null && x.gameObject.activeInHierarchy).ToArray();
                if (rooms.Length < 9 || containers.Length < 6) throw new Exception($"Incomplete generation: rooms={rooms.Length}, containers={containers.Length}");
                Physics.SyncTransforms();
                foreach (var room in rooms)
                {
                    if (room.transform.Find("ShellBack") == null) continue;
                    var depth = room.Interior.size.z - 0.4f;
                    var start = room.transform.TransformPoint(new Vector3(0f, 0.85f, -depth * 0.5f - 0.7f));
                    var end = room.transform.TransformPoint(new Vector3(0f, 0.85f, 0f));
                    if (Physics.SphereCast(start, 0.25f, (end - start).normalized, out var hit, (end - start).magnitude, ~0, QueryTriggerInteraction.Ignore))
                        throw new Exception($"Blocked doorway {room.name}: {hit.collider.name}");
                    var path = new System.Collections.Generic.List<Vector3>();
                    if (!ArknightsACT.Gameplay.Navigation.PrototypeNavigationGraph25D.Instance.TryBuildPath(
                        new Vector3(-18f, 0f, -15f), room.transform.position + Vector3.up * 0.18f, path))
                        throw new Exception($"Disconnected interior navigation: {room.name}");
                }
                Capture($"stage-{_stage}", new Vector3(0f, 0f, 0f), 48f);
                var roomForShot = rooms.First(x => x.name == "ApartmentWing_A");
                var scavenging = UnityEngine.Object.FindFirstObjectByType<ScavengingInventory25D>();
                ValidateLoot(scavenging, containers.First(x => x.transform.parent == roomForShot.transform));
                if (_stage == 1) ValidateCapacity(scavenging, containers);
                Teleport(scavenging.transform, roomForShot.transform.position + new Vector3(0.4f, 0.2f, 0f));
                Debug.Log($"CITY_VALIDATION stage={_stage} rooms={rooms.Length} containers={containers.Length} doors=clear");
                _phase = 1;
                _next = EditorApplication.timeSinceStartup + 1.5f;
            }
            catch (Exception e) { Finish(e.ToString(), false); }
        }
        private static void Teleport(Transform player, Vector3 position)
        {
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.position = position;
            controller.enabled = true;
            Physics.SyncTransforms();
        }
        private static void ValidateLoot(ScavengingInventory25D scavenging, SearchableContainer25D container)
        {
            if (scavenging == null) throw new Exception("Missing scavenging input integration");
            if (scavenging.GetComponent<ScavengingWindowUI>() == null) throw new Exception("Missing scavenging window UI");
            var catalog = ScavengingCatalog.Load();
            if (catalog.Count < 22 || catalog.Select(x => x.Id).Distinct().Count() != catalog.Count)
                throw new Exception("Invalid collectible catalog");
            if (!catalog.Any(x => x.SalvageGridSize.x > 1 || x.SalvageGridSize.y > 1))
                throw new Exception("Scavenging catalog has no multi-cell items");
            // Icons are optional by design: only explicitly mapped real relics currently
            // carry artwork. Ordinary collection goods and unmapped prototypes are color-only.
            var inventory = scavenging.GetComponent<CollectibleInventory>();
            foreach (var item in catalog)
            {
                if (item.Profession != OperatorProfession.Guard && item.Profession != OperatorProfession.Unspecified && inventory.IsApplicable(item))
                    throw new Exception("Other-profession collectible applied to Chen");
                UnityEngine.Object.Destroy(item);
            }
            var position = container.transform.position + container.transform.parent.TransformVector(new Vector3(0f, 0.03f, -0.95f));
            Teleport(scavenging.transform, position);

            // Pausing must freeze the queue without losing the rolled loot list.
            Time.timeScale = 0f;
            if (!scavenging.OpenContainer(container)) throw new Exception("Could not open the container search window");
            scavenging.AdvanceSearch(5f);
            Time.timeScale = 1f;
            if (container.RevealedCount != 0 || container.TakenCount != 0) throw new Exception("Search advanced while paused");
            if (container.SlotCount < 3 || container.SlotCount > 10)
                throw new Exception($"Unexpected slot count for {container.DisplayName}: {container.SlotCount}");
            ValidatePackedGrid(container);

            // Gameplay input is modal while scavenging is open. Even an external teleport
            // must not auto-close the UI or release gameplay input; only an explicit close does.
            scavenging.AdvanceSearch(1f);
            Teleport(scavenging.transform, position + new Vector3(3.5f, 0f, 0f));
            scavenging.AdvanceSearch(0.1f);
            if (scavenging.ActiveContainer == null) throw new Exception("External movement closed the modal scavenging window");
            if (!ArknightsACT.Gameplay.Input.GameplayInputBlocker.IsBlocked)
                throw new Exception("Scavenging window did not lock gameplay input");
            if (container.RevealedCount != 0 || container.TakenCount != 0)
                throw new Exception("Search revealed an entry before its pass completed");
            scavenging.CloseContainerWindow();
            if (ArknightsACT.Gameplay.Input.GameplayInputBlocker.IsBlocked)
                throw new Exception("Manual scavenging close did not release gameplay input");

            // Entries unseal one at a time, never early.
            Teleport(scavenging.transform, position);
            if (!scavenging.OpenContainer(container)) throw new Exception("Could not reopen the container search window");
            scavenging.AdvanceSearch(1f);
            if (container.RevealedCount != 0) throw new Exception("An entry revealed before its search pass finished");
            scavenging.AdvanceSearch(3f);
            if (container.RevealedCount != 1) throw new Exception("The queue did not unseal exactly one entry");

            // Picking up is a separate step, and it settles stack counts exactly once.
            if (!scavenging.TryPickup(0)) throw new Exception("Picking up an identified entry failed");
            if (scavenging.PendingCount != 1 || container.TakenCount != 1)
                throw new Exception("Pickup did not move exactly one entry into the haul");
            if (scavenging.TryPickup(0)) throw new Exception("An already taken entry was picked up twice");

            // Closing and resuming must not reroll or duplicate the loot list.
            var rolledSlots = container.SlotCount;
            scavenging.CloseContainerWindow();
            if (!scavenging.OpenContainer(container)) throw new Exception("Could not resume the container");
            if (container.SlotCount != rolledSlots || container.TakenCount != 1)
                throw new Exception("Resuming the container rerolled or duplicated its loot");
            for (var i = 0; i < 24 && container.HasUnsearched; i++) scavenging.AdvanceSearch(3f);
            if (container.RevealedCount + container.TakenCount != container.SlotCount)
                throw new Exception("The queue did not unseal every entry");
            var revealedBeforeSweep = container.RevealedCount;
            var takenBeforeSweep = container.TakenCount;
            var swept = scavenging.TakeAllRevealed();
            if (swept > revealedBeforeSweep || container.TakenCount != takenBeforeSweep + swept)
                throw new Exception("Take-all consumed an invalid number of identified entries");
            var layout = new List<ScavengingInventory25D.BackpackPlacement>();
            if (!scavenging.BuildBackpackLayout(layout))
                throw new Exception("Take-all produced an invalid backpack layout");
            if (container.Emptied && scavenging.OpenContainer(container))
                throw new Exception("An emptied container reopened for more loot");

            var reward = container.Slots[0].Item;
            var beforeStack = reward.IsSalvageCommodity ? 0 : inventory.GetStackCount(reward);
            var beforeValue = scavenging.SecuredCollectionValue;
            scavenging.SecureHaul();
            if (scavenging.PendingCount != 0)
                throw new Exception("Extraction retained unsecured loot");
            if (!reward.IsSalvageCommodity && inventory.GetStackCount(reward) != beforeStack)
                throw new Exception("Extraction reapplied an already-active collectible");
            if (scavenging.SecuredCollectionValue <= beforeValue)
                throw new Exception("Extraction did not increase secured collection value");
        }
        private static void ValidatePackedGrid(SearchableContainer25D container)
        {
            var occupied = new bool[container.GridWidth, container.GridHeight];
            foreach (var slot in container.Slots)
            {
                if (slot.Width < 1 || slot.Height < 1 || slot.GridX < 0 || slot.GridY < 0 ||
                    slot.GridX + slot.Width > container.GridWidth || slot.GridY + slot.Height > container.GridHeight)
                    throw new Exception($"Loot item outside container grid: {slot.Item?.DisplayName}");

                for (var y = 0; y < slot.Height; y++)
                for (var x = 0; x < slot.Width; x++)
                {
                    var gx = slot.GridX + x;
                    var gy = slot.GridY + y;
                    if (occupied[gx, gy])
                        throw new Exception($"Overlapping loot grid cell at {gx},{gy}");
                    occupied[gx, gy] = true;
                }
            }
        }

        private static void ValidateCapacity(ScavengingInventory25D scavenging, SearchableContainer25D[] containers)
        {
            var placements = new List<ScavengingInventory25D.BackpackPlacement>();
            foreach (var container in containers.Where(x => !x.Emptied && x.transform.parent.Find("ShellBack") != null))
            {
                Teleport(scavenging.transform, container.transform.position + container.transform.parent.TransformVector(new Vector3(0f, 0.03f, -0.95f)));
                if (!scavenging.OpenContainer(container)) continue;
                for (var i = 0; i < 24 && container.HasUnsearched; i++) scavenging.AdvanceSearch(3f);
                scavenging.TakeAllRevealed();
                if (scavenging.UsedBackpackCells > scavenging.BackpackCellCapacity)
                    throw new Exception("Backpack grid exceeded its real cell capacity");
                if (!scavenging.BuildBackpackLayout(placements))
                    throw new Exception("Backpack contains an invalid/overlapping layout");
                if (scavenging.FreeBackpackCells <= 0) break;
            }
            if (scavenging.PendingCount == 0) throw new Exception("Could not place any item in the real backpack grid");
            scavenging.SecureHaul();
        }
        private static void ValidateDeath()
        {
            var scavenging = UnityEngine.Object.FindFirstObjectByType<ScavengingInventory25D>();
            foreach (var container in SearchableContainer25D.Active.Where(x => !x.Emptied && x.transform.parent.Find("ShellBack") != null))
            {
                Teleport(scavenging.transform, container.transform.position + container.transform.parent.TransformVector(new Vector3(0f, 0.03f, -0.95f)));
                if (!scavenging.OpenContainer(container)) continue;
                for (var i = 0; i < 24 && container.HasUnsearched; i++) scavenging.AdvanceSearch(3f);
                scavenging.TakeAllRevealed();
                if (scavenging.PendingCount > 0) break;
            }
            if (scavenging.PendingCount == 0) throw new Exception("No pending item to test death loss");
            var inventory = scavenging.GetComponent<CollectibleInventory>();
            var activeBeforeDeath = inventory.Definitions.Values.Sum(x => inventory.GetStackCount(x));
            var pendingRelics = scavenging.Pending.Count(x => x != null && !x.IsSalvageCommodity);
            var securedValue = scavenging.SecuredCollectionValue;
            scavenging.GetComponent<ArknightsACT.Combat.Health>().TakeDamage(float.MaxValue);
            var activeAfterDeath = inventory.Definitions.Values.Sum(x => inventory.GetStackCount(x));
            if (scavenging.PendingCount != 0 || activeAfterDeath != activeBeforeDeath - pendingRelics ||
                scavenging.SecuredCollectionValue != securedValue)
                throw new Exception("Death did not remove exactly the unsecured relic stacks or retained unsecured loot");
            if (scavenging.ActiveContainer != null) throw new Exception("Death did not close the container window");
        }
        private static void Capture(string name, Vector3 focus, float size)
        {
            var go = new GameObject("ValidationCamera");
            var camera = go.AddComponent<Camera>();
            camera.CopyFrom(Camera.main);
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = size;
            go.transform.position = focus + new Vector3(-25f, 42f, -42f);
            go.transform.LookAt(focus);
            if (size < 0f)
            {
                camera.orthographicSize = Camera.main.orthographicSize;
                go.transform.SetPositionAndRotation(Camera.main.transform.position, Camera.main.transform.rotation);
            }
            var rt = new RenderTexture(1600, 1000, 24);
            camera.targetTexture = rt;
            camera.Render();
            var old = RenderTexture.active;
            RenderTexture.active = rt;
            var image = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0);
            image.Apply();
            File.WriteAllBytes($"{LogDir}/{name}.png", image.EncodeToPNG());
            RenderTexture.active = old;
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(go);
        }
        private static void Finish(string message, bool passed)
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= Log;
            File.WriteAllText($"{LogDir}/result.txt", $"{(passed ? "PASS" : "FAIL")}\n{message}\nErrors: {_errors}");
            var summary = $"CITY_VALIDATION {(passed ? "PASS" : "FAIL")}: {message}";
            // Detach the counter first so a reported failure does not count as its own error.
            if (passed) Debug.Log(summary);
            else Debug.LogError(summary);
            if (_exitOnFinish)
            {
                EditorApplication.Exit(passed ? 0 : 1);
                return;
            }
            EditorApplication.isPlaying = false;
        }
    }
}
#endif
