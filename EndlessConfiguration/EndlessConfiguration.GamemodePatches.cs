using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using Unity.VisualScripting;
using UnityEngine;
using static WorldLoader;

namespace Celestial.CustomEndless
{
    public static partial class EndlessConfiguration
    {
        private sealed class GeneratedLoop
        {
            public int Completed;
        }

        private static System.Runtime.CompilerServices.ConditionalWeakTable<M_Level.LevelAssetHolder, GeneratedLoop> generatedLoops = new System.Runtime.CompilerServices.ConditionalWeakTable<M_Level.LevelAssetHolder, GeneratedLoop>();
        private static readonly MethodInfo cloneLoopHolder = AccessTools.Method(typeof(object), "MemberwiseClone");
        private static int generatedLoop;
        private static int lastGeneratedRegion = -1;

        private static void ResetLoopTracking()
        {
            generatedLoops = new System.Runtime.CompilerServices.ConditionalWeakTable<M_Level.LevelAssetHolder, GeneratedLoop>();
            generatedLoop = 0;
            lastGeneratedRegion = -1;
            cycles = 0;
        }

        private static bool UpdateCompletedLoops(M_Level level)
        {
            EndlessLoopLevel marker = level.GetComponent<EndlessLoopLevel>();
            if (marker == null || marker.CompletedLoops <= cycles) return false;
            cycles = marker.CompletedLoops;
            SkyboxManager.UnloadSkyboxes();
            return true;
        }

        [HarmonyPatch(typeof(M_Region), nameof(M_Region.GetLevels))]
        private static class GeneratedLoopPatch
        {
            [HarmonyPriority(Priority.Last)]
            private static void Postfix(M_Region __instance, M_Subregion __1, ref List<M_Level.LevelAssetHolder> __result)
            {
                if (EndlessMode == null || CL_GameManager.GetCurrentGamemode() != EndlessMode ||
                    __result == null || __result.Count == 0 || EndlessMode.regions == null) return;
                int index = EndlessMode.regions.IndexOf(__instance);
                if (index < 0) return;
                if (lastGeneratedRegion >= 0 && (index < lastGeneratedRegion || index == lastGeneratedRegion && __1 == null))
                    generatedLoop++;
                lastGeneratedRegion = index;
                List<M_Level.LevelAssetHolder> levels = new List<M_Level.LevelAssetHolder>(__result.Count);
                foreach (M_Level.LevelAssetHolder holder in __result)
                {
                    if (holder == null) continue;
                    M_Level.LevelAssetHolder copy = (M_Level.LevelAssetHolder)cloneLoopHolder.Invoke(holder, null);
                    generatedLoops.Add(copy, new GeneratedLoop { Completed = generatedLoop });
                    levels.Add(copy);
                }
                __result = levels;
            }
        }

        [HarmonyPatch(typeof(WorldLoader.LevelInfo), "SetLevel")]
        private static class SpawnedLoopPatch
        {
            private static void Postfix(M_Level l, M_Level.LevelAssetHolder lA)
            {
                if (l == null || lA == null || !generatedLoops.TryGetValue(lA, out GeneratedLoop loop)) return;
                EndlessLoopLevel marker = l.GetComponent<EndlessLoopLevel>() ?? l.gameObject.AddComponent<EndlessLoopLevel>();
                marker.CompletedLoops = loop.Completed;
            }
        }

        [HarmonyPatch(typeof(CL_GameManager), "Win")]
        public static class WinPatch
        {
            public static bool Prefix()
            {
                if (EndlessMode != null && CL_GameManager.GetCurrentGamemode() == EndlessMode && !EndlessConfiguration.CanWin)
                {
                    string[] args = { };
                    EndlessConfiguration.TeleportPlayerToNextLevel(args);
                    UI_ObjectiveViewer.ClearAllObjectives();
                    return false;
                }
                return true;
            }
        }
        [HarmonyPatch(typeof(M_Level), "OnEnter")]
        public static class M_Level_OnLevelActivate_Patch
        {
            [HarmonyPrefix]
            public static void Prefix(M_Level __instance)
            {
                if (__instance == null || EndlessMode == null || EndlessMode.regions == null || EndlessMode.regions.Count == 0 || CL_GameManager.gMan == null || ENT_Player.GetPlayer() == null) return;
                if (!(last_entered == null) && __instance.GetInstanceID() == last_entered.GetInstanceID()) return;
                last_entered = __instance;
                if (CL_GameManager.GetCurrentGamemode() != EndlessMode)
                    return;
                RemainsPopulationManager.GetOrCreate().SetCurrentLevel(__instance);
                bool customMassDisabled = IsCustomMassDisabled(__instance);
                if (customMassDisabled)
                {
                    DeathFloorHeightPatch.HaltHeightUpdates = false;
                    EndlessConfiguration.DestroyMassInstance();
                }
                if (__instance.levelName == "To_Diver" || __instance.levelName == "CustomEndless_HadalDiver")
                    EndlessConfiguration.DestroyMassInstance();
                BalancedMode.OnLevelEnter(__instance);
                if (canWin) canWin = false;
                Debug.Log($"CYCLES:::::::::::::{cycles}");
                bool completedLoop = UpdateCompletedLoops(__instance);
                EndlessEnemyScaling.ApplyToExisting();
                RemainsPopulationManager.GetOrCreate().SetCurrentLevel(__instance);
                if (CustomUIManager.GetToggleState("height_win_enabled") && CustomUIManager.GetFloatState("height_win_amount") <= CL_GameManager.gMan.GetPlayerCorrectedHeight())
                {
                    canWin = true;
                    CL_GameManager.gMan.Win();
                }
                if (CustomUIManager.GetToggleState("cycles_win_enabled") && CustomUIManager.GetCountState("cycles_win_amount") <= cycles)
                {
                    if (completedLoop)
                    {
                        canWin = true;
                        CL_GameManager.gMan.Win();
                    }
                }
                if (lastRegion == null)
                    lastRegion = __instance.region;

                string itemRegion = GetLogicalRegionName(__instance);
                if (!gaveFlashlight &&
                    (__instance.levelName.ContainsInsensitive("Habitation") ||
                     __instance.levelName.ContainsInsensitive("Abyss") ||
                     __instance.levelName.ContainsInsensitive("Ladder") ||
                     itemRegion == "Body Cavity" ||
                     itemRegion == "Duct") &&
                    !__instance.levelName.ContainsInsensitive("interlude"))
                {
                    gaveFlashlight = true;
                    GiveItem("Item_Flashlight", 1);
                    GiveItem("Item_Flaregun", 1);
                    GiveItem("Item_Flaregun_Ammo", 3);
                }

                if (__instance.levelName.ContainsInsensitive("Nest") && !gaveGun)
                {
                    gaveGun = true;
                    GiveItem("Item_Handgun", 1);
                    GiveItem("Item_10mm_Ammo", 3);
                }

                DEN_Face facePrefab = GetCachedObject<DEN_Face>("Denizen_Face");
                if (facePrefab == null) return;

                int targetCount = Mathf.Max(0, GetFaceCountForLevel(__instance));
                spawnedFaces.RemoveAll(face => face == null);

                while (spawnedFaces.Count > targetCount)
                {
                    int lastIndex = spawnedFaces.Count - 1;
                    DEN_Face theFace = spawnedFaces[lastIndex];

                    spawnedFaces.RemoveAt(lastIndex);
                    DEN_Face.RemoveFaceInstance(theFace);

                    if (theFace != null) UnityEngine.Object.Destroy(theFace.gameObject);
                }

                while (spawnedFaces.Count < targetCount)
                {
                    Vector3 spawnPos = ENT_Player.GetPlayer().transform.position + new Vector3(0, -20, 0);
                    DEN_Face theFace = UnityEngine.Object.Instantiate(facePrefab, spawnPos, Quaternion.identity);

                    spawnedFaces.Add(theFace);
                    DEN_Face.AddFaceInstance(theFace);
                }

                if (customMassDisabled || __instance.levelName.ContainsInsensitive("Abyss") || __instance.levelName.ContainsInsensitive("Extraction") || __instance.levelName.ContainsInsensitive("Comms") || __instance.levelName.ContainsInsensitive("Boost") || __instance.levelName == "To_Diver" || __instance.levelName == "CustomEndless_HadalDiver" || itemRegion == "Body Cavity")
                {
                    DeathFloorHeightPatch.HaltHeightUpdates = false;
                    EndlessConfiguration.DestroyMassInstance();
                }
                else
                    DEN_DeathFloor.instance?.SetActive(true);

                if (EndlessConfiguration.BEGONE_FOUL_SPIRIT)
                {
                    EndlessConfiguration.DestroyMassInstance();
                    EndlessConfiguration.BEGONE_FOUL_SPIRIT = false;
                }

                if ((__instance.levelName.ContainsInsensitive("Entryway") || __instance.levelName.ContainsInsensitive("Campaign_Interlude_Sink_To_Pipeworks_01")) &&
                    !IsCampaignGenerationLevel(__instance))
                {
                    string[] args = { "" };
                    TeleportPlayerToNextLevel(args);
                }

                DeathFloorHeightPatch.HaltHeightUpdates = BalancedMode.IsAbyssMode(GetLogicalRegionName(__instance)) || ((__instance.levelName.ContainsInsensitive("To_") || __instance.levelName.ContainsInsensitive("Universal") || __instance.levelName.ContainsInsensitive("To_Comms Array") || __instance.levelName.ContainsInsensitive("From")) && !__instance.levelName.ContainsInsensitive("interlude"));

                void RemoveLODs(Transform parent)
                {
                    for (int i = parent.childCount - 1; i >= 0; i--)
                    {
                        Transform child = parent.GetChild(i);
                        RemoveLODs(child);

                        if (child.name.ContainsInsensitive("Props"))
                        {
                            Debug.Log(child.name);
                            UnityEngine.Object.Destroy(child.gameObject);
                        }
                    }
                }

                Debug.Log(__instance.name);
                if (__instance.levelName.ContainsInsensitive("Habitation_Shaft_Intro"))
                {
                    Debug.Log("HIII");
                    //RemoveLODs(__instance.gameObject.transform);
                }

                if (CustomUIManager.GetToggleState("fun_consumah"))
                {
                    if (!IsDuct(__instance)) _ = UnloadPreviousLevels(__instance);
                    DEN_DeathFloor.instance?.SetVisualActive(false);
                    if (UnityEngine.Random.Range(0.01f, 1f) > 0.9f) SpawnEntity("Denizen_Hunter_BindingVariant", __instance);
                }

                bool isChimney = __instance.levelName.ContainsInsensitive("chimney") || CustomUIManager.GetToggleState("fun_always_snowing");

                Event_Cold coldInstance = GetInstanceInternal();
                if (coldInstance == null)
                {
                    coldInstance = UnityEngine.Object.FindObjectOfType<Event_Cold>(true);
                }

                if (cutOff && !IsCampaignGenerationLevel(__instance) && !__instance.name.ContainsInsensitive("To_") && !__instance.name.ContainsInsensitive("From_") && !__instance.name.ContainsInsensitive("Universal"))
                {
                    cutOff = false;
                    _ = UnloadPreviousLevels(__instance);
                }

                //if (__instance.name.ContainsInsensitive("Prototype"))
                //{
                //teleportCTS?.Cancel();
                //teleportCTS?.Dispose();
                //teleportCTS = new CancellationTokenSource();
                //_ = TeleportAtEndOfLevel(__instance, teleportCTS.Token);
                //}

                if (__instance.name.ContainsInsensitive("From_Anywhere"))
                {
                    WorldLoader.instance.UnloadPreviousLevels(0);
                }

                if (__instance.name.ContainsInsensitive("To_"))
                {
                    cutOff = true;
                }

                if (isChimney)
                {
                    if (coldInstance == null)
                    {
                        coldInstance = GetOrSpawnEventCold();
                    }

                    if (coldInstance != null)
                    {
                        if (!coldInstance.gameObject.activeSelf)
                        {
                            coldInstance.gameObject.SetActive(true);
                        }

                        Event_Cold.SetEventStatus(true);
                    }
                }
                else
                {
                    Event_Cold.SetEventStatus(false);

                    if (coldInstance != null && coldInstance.gameObject.activeSelf)
                    {
                        DisableEventCold(coldInstance);
                        RemoveColdDebuff();
                    }
                }
            }
        }
        [HarmonyPatch(typeof(WorldLoader), "Initialize")]
        public static class WorldLoaderInitializePatch
        {
            [HarmonyPrefix]
            public static void Prefix()
            {
                if (EndlessMode != null && CL_GameManager.GetCurrentGamemode() == EndlessMode) UpdateRegions();
            }
        }
    }

    public sealed class EndlessLoopLevel : MonoBehaviour
    {
        internal int CompletedLoops;
    }

    public sealed class RemainsPopulationManager : MonoBehaviour
    {
        private static RemainsPopulationManager instance;
        private readonly HashSet<DEN_Remains_Active> ductRemains = new HashSet<DEN_Remains_Active>();
        private readonly HashSet<DEN_Remains_Active> presentAtDuctEntry = new HashSet<DEN_Remains_Active>();
        private M_Level currentLevel;
        private float nextPopulationCheck;

        public static RemainsPopulationManager GetOrCreate()
        {
            if (instance != null) return instance;
            GameObject managerObject = new GameObject("RemainsPopulationManager");
            DontDestroyOnLoad(managerObject);
            instance = managerObject.AddComponent<RemainsPopulationManager>();
            return instance;
        }

        public void SetCurrentLevel(M_Level level)
        {
            bool wasDuct = IsDuctLevel(currentLevel);
            bool isDuct = IsDuctLevel(level);
            if (wasDuct && !isDuct)
            {
                TrackDuctRemains();
                foreach (DEN_Remains_Active remains in ductRemains)
                    if (remains != null) Destroy(remains.gameObject);
                ductRemains.Clear();
                presentAtDuctEntry.Clear();
            }
            else if (!wasDuct && isDuct)
            {
                presentAtDuctEntry.Clear();
                if (GameEntity.entities != null)
                    foreach (GameEntity entity in GameEntity.entities)
                        if (entity is DEN_Remains_Active remains && remains != null)
                            presentAtDuctEntry.Add(remains);
            }

            currentLevel = level;
            RefreshPopulation();
        }

        private void Update()
        {
            if (Time.time < nextPopulationCheck) return;
            nextPopulationCheck = Time.time + 0.5f;
            if (CL_GameManager.GetCurrentGamemode() != EndlessConfiguration.EndlessMode)
            {
                currentLevel = null;
                ductRemains.Clear();
                presentAtDuctEntry.Clear();
                return;
            }

            WorldLoader loader = WorldLoader.instance;
            WorldLoader.LevelInfo currentInfo = loader?.GetCurrentLevel();
            M_Level level = currentInfo?.GetLevel();
            if (level != null && level != currentLevel)
                SetCurrentLevel(level);
            else
                RefreshPopulation();
        }

        private void RefreshPopulation()
        {
            if (currentLevel == null || CL_GameManager.GetCurrentGamemode() != EndlessConfiguration.EndlessMode)
                return;
            if (IsDuctLevel(currentLevel))
            {
                TrackDuctRemains();
                return;
            }

            ENT_Player player = ENT_Player.GetPlayer();
            if (player == null) return;

            int target = EndlessConfiguration.GetRemainsPopulationForLevel(currentLevel);
            List<DEN_Remains_Active> remains = new List<DEN_Remains_Active>();
            if (GameEntity.entities != null)
                foreach (GameEntity entity in GameEntity.entities)
                    if (entity is DEN_Remains_Active active && active != null &&
                        !ductRemains.Contains(active) && !IsDuctOrigin(active))
                        remains.Add(active);

            remains.Sort((left, right) =>
                Vector3.SqrMagnitude(right.transform.position - player.transform.position)
                    .CompareTo(Vector3.SqrMagnitude(left.transform.position - player.transform.position)));
            for (int i = 0; i < remains.Count - target; i++)
                Destroy(remains[i].gameObject);

            if (!currentLevel.gameObject.activeInHierarchy) return;
            int missing = Mathf.Min(3, target - remains.Count);
            if (missing <= 0) return;
            GameObject prefab = CL_AssetManager.GetAssetGameObject("Denizen_Remains_Active");
            if (prefab == null || prefab.GetComponent<DEN_Remains_Active>() == null) return;

            for (int i = 0; i < missing; i++)
            {
                if (!TryFindSpawnPosition(currentLevel, player.transform.position, out Vector3 position))
                    break;
                Instantiate(prefab, position, Quaternion.identity, currentLevel.transform);
            }
        }

        private void TrackDuctRemains()
        {
            if (GameEntity.entities == null) return;
            foreach (GameEntity entity in GameEntity.entities)
                if (entity is DEN_Remains_Active remains && remains != null &&
                    (IsDuctOrigin(remains) || !presentAtDuctEntry.Contains(remains)))
                    ductRemains.Add(remains);
        }

        private static bool IsDuctLevel(M_Level level)
        {
            return level != null && EndlessConfiguration.GetLogicalRegionName(level) == "Duct";
        }

        private static bool IsDuctOrigin(DEN_Remains_Active remains)
        {
            M_Level owner = remains.GetComponentInParent<M_Level>();
            if (owner != null) return IsDuctLevel(owner);

            WorldLoader loader = WorldLoader.instance;
            WorldLoader.BranchInfo branch = loader?.GetCurrentBranch();
            if (branch == null || branch.levelTracker == null) return false;
            foreach (WorldLoader.LevelInfo info in branch.levelTracker)
            {
                M_Level level = info?.GetLevel();
                if (IsDuctLevel(level) && level.GetLocalBounds().Contains(level.transform.InverseTransformPoint(remains.transform.position)))
                    return true;
            }
            return false;
        }

        private static bool TryFindSpawnPosition(M_Level level, Vector3 playerPosition, out Vector3 position)
        {
            Bounds bounds = level.GetLocalBounds();
            for (int attempt = 0; attempt < 12; attempt++)
            {
                Vector3 point = playerPosition + Vector3.up * 1.5f;
                Vector3 direction = UnityEngine.Random.onUnitSphere;
                for (int bounce = 0; bounce < 6; bounce++)
                {
                    if (Physics.Raycast(point, direction, out RaycastHit hit, 24f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    {
                        if (hit.collider.GetComponentInParent<ENT_Player>() != null)
                        {
                            point += direction * 2f;
                            direction = UnityEngine.Random.onUnitSphere;
                            continue;
                        }
                        point = hit.point + hit.normal * 1.5f;
                        direction = Vector3.Reflect(direction, hit.normal) + UnityEngine.Random.onUnitSphere * 0.8f;
                    }
                    else
                    {
                        point += direction * 12f;
                        direction += UnityEngine.Random.onUnitSphere;
                    }

                    if (direction.sqrMagnitude < 0.0001f)
                        direction = UnityEngine.Random.onUnitSphere;
                    direction.Normalize();
                    if (bounce < 2 || (point - playerPosition).sqrMagnitude < 144f ||
                        !bounds.Contains(level.transform.InverseTransformPoint(point)) ||
                        Physics.CheckSphere(point, 0.75f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                        continue;
                    position = point;
                    return true;
                }
            }
            position = Vector3.zero;
            return false;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }
    }
}
