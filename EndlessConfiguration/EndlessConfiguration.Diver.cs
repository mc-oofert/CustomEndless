using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;
using static WorldLoader;

//At this point I'm just talking to whoever's reading this. This is uh. This code makes the Diver work. Please keep fixes to the diver's functioning here.


namespace Celestial.CustomEndless
{
    public sealed class EndlessDiverLevel : MonoBehaviour
    {
        public DEN_HadalDiver Diver;
        public GameObject SpawnRoot;
        public UT_Timer SummonTimer;
        public bool Entered;
        public bool UsesEntranceRig;
        public int Slot;

        private void OnDestroy()
        {
            if (Diver != null)
                Destroy(Diver.gameObject);
        }
    }

    public sealed class EndlessDiverInstance : MonoBehaviour
    {
        public EndlessDiverLevel Level;
        public DiverSummonRig Rig;
        public bool Exiting;
    }

    public sealed class DiverSummonRig : MonoBehaviour
    {
        public GameObject SpawnRoot;
        public UT_Timer SummonTimer;
        public DEN_HadalDiver Diver;
        public M_Level Level;
        public bool Configured;
    }

    public static partial class EndlessConfiguration
    {
        private static readonly HashSet<M_Level> pendingDiverSummons = new HashSet<M_Level>();

        private static bool IsDiverRegion(M_Region region)
        {
            return region != null && !string.IsNullOrEmpty(region.name) &&
                region.name.ContainsInsensitive("HadalDiver");
        }

        private static M_Level GetDiverSourceLevel()
        {
            return GetCachedObject<M_Level>("Campaign_Interlude_Habitation_To_Abyss_01");
        }

        private static Transform FindChildByName(Transform root, string name)
        {
            if (root == null)
                return null;

            if (root.name == name)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChildByName(root.GetChild(i), name);
                if (found != null)
                    return found;
            }
            return null;
        }

        private static DiverSummonRig AttachDiverSummonRig(M_Level level)
        {
            if (level == null || level.levelName != "To_Diver")
                return null;

            DiverSummonRig existing = level.GetComponentInChildren<DiverSummonRig>(true);
            if (existing != null)
                return existing;

            M_Level sourceLevel = GetDiverSourceLevel();
            Transform anchor = FindChildByName(level.transform, "Lever");
            Transform sourceLever = FindChildByName(sourceLevel?.transform, "Prop_Lever_01");
            if (sourceLever != null) sourceLever = sourceLever.parent;
            Transform sourceDiver = FindChildByName(sourceLevel?.transform, "HadalDiver");
            if (anchor == null || sourceLever == null || sourceDiver == null)
            {
                Debug.LogWarning("Custom Endless To_Diver lever or source rig is unavailable");
                return null;
            }

            GameObject pivotObject = new GameObject("HadalDiverRig");
            Transform pivot = pivotObject.transform;
            pivot.SetParent(level.transform, false);
            pivotObject.SetActive(false);
            pivot.position = sourceLever.position;
            pivot.rotation = sourceLever.rotation;

            GameObject leverObject = UnityEngine.Object.Instantiate(sourceLever.gameObject, pivot, true);
            GameObject diverObject = UnityEngine.Object.Instantiate(sourceDiver.gameObject, pivot, true);
            foreach (CL_HoldLever lever in leverObject.GetComponentsInChildren<CL_HoldLever>(true))
            {
                lever.active = true;
                lever.canUse = true;
                lever.locked = false;
                lever.hasBeenPressed = false;
                lever.hasFinished = false;
                if (lever.snapPoints != null)
                {
                    foreach (CL_HoldLever.SnapPoint snapPoint in lever.snapPoints)
                        snapPoint.isActive = Mathf.Abs(snapPoint.angle - lever.initialAngle) <= snapPoint.snapRange;
                }
                Collider collider = lever.GetComponent<Collider>();
                if (collider != null)
                    collider.enabled = true;
            }
            foreach (CL_Prop prop in leverObject.GetComponentsInChildren<CL_Prop>(true))
            {
                prop.canInteract = true;
                Collider[] colliders = prop.GetComponentsInChildren<Collider>(true);
                foreach (Collider collider in colliders)
                    collider.enabled = true;
            }
            Transform spawnTransform = FindChildByName(diverObject.transform, "HadalDiver_Root");
            Transform summonTransform = FindChildByName(leverObject.transform, "Hadal Diver Summon");
            GameObject spawnRoot = spawnTransform?.gameObject;
            UT_Timer summonTimer = summonTransform?.GetComponent<UT_Timer>();
            DEN_HadalDiver diver = spawnRoot?.GetComponentInChildren<DEN_HadalDiver>(true);
            if (spawnRoot == null || summonTimer == null || diver == null)
            {
                UnityEngine.Object.Destroy(pivotObject);
                Debug.LogWarning("Custom Endless To_Diver source rig is incomplete");
                return null;
            }

            spawnRoot.SetActive(false);
            diver.exitEvent = new UnityEvent();
            diver.gameObject.AddComponent<EndlessDiverInstance>();
            Animator boardingAnimator = diver.GetComponent<Animator>();
            boardingAnimator?.SetBool("Idle", true);
            summonTimer.runOnAwake = false;
            summonTimer.endEvent = new UnityEvent();

            foreach (UT_TriggerZone trigger in diverObject.GetComponentsInChildren<UT_TriggerZone>(true))
            {
                if (trigger.onEnter == null)
                    continue;
                for (int i = 0; i < trigger.onEnter.GetPersistentEventCount(); i++)
                {
                    if (trigger.onEnter.GetPersistentMethodName(i) == "EnterChamber")
                    {
                        trigger.enabled = false;
                        break;
                    }
                }
            }

            DiverSummonRig rig = pivotObject.AddComponent<DiverSummonRig>();
            rig.SpawnRoot = spawnRoot;
            rig.SummonTimer = summonTimer;
            rig.Diver = diver;
            rig.Level = level;
            diver.GetComponent<EndlessDiverInstance>().Rig = rig;
            summonTimer.endEvent.AddListener(() =>
            {
                TryConfigureDiverRig(rig);
                spawnRoot.SetActive(true);
            });
            pivot.position = anchor.position;
            pivot.rotation = anchor.rotation;
            pivotObject.SetActive(true);
            TryConfigureDiverRig(rig);
            WorldLoader.instance?.StartCoroutine(WatchDiverRig(rig));
            return rig;
        }

        private static IEnumerator WatchDiverRig(DiverSummonRig rig)
        {
            while (rig != null && rig.Diver != null)
            {
                TryConfigureDiverRig(rig);
                if (rig.Configured && rig.SpawnRoot.activeSelf && rig.Diver.segments != null && rig.Diver.segments.Count > 0)
                {
                    yield return WatchDiverExit(rig.Diver);
                    yield break;
                }
                yield return null;
            }
        }

        private static float ConfigureDiverLength(DEN_HadalDiver diver, int slot)
        {
            bool hasLengthSetting = CustomUIManager.SettingFloats.ContainsKey("diver_length_mult") ||
                slot >= 0 && HasSlotSetting($"slot_{slot}_length_mult");
            float multiplier = hasLengthSetting
                ? GetFloatStateForSlot("diver_length_mult", "diver", slot)
                : 1f;
            float length = 0f;

            foreach (DEN_HadalDiver.SegmentGroup group in diver.segmentGroups)
            {
                if (group == null || group.segmentPrefabs == null || group.segmentPrefabs.Count == 0)
                    continue;

                group.length = group.id == "Head" || group.id == "Tail"
                    ? 1
                    : multiplier <= 0f || float.IsNaN(multiplier) ? 0 : (int)Math.Min(int.MaxValue, Math.Round((double)group.length * multiplier));

                for (int i = 0; i < group.length; i++)
                {
                    DEN_HadalDiver_Segment segment = group.segmentPrefabs[i % group.segmentPrefabs.Count];
                    if (segment == null || segment.endLocator == null)
                        continue;
                    length += Mathf.Max(1f, Vector3.Distance(segment.transform.position, segment.endLocator.position));
                }
            }

            return Mathf.Max(40f, length);
        }

        private static M_Level CreateDiverLevel(int slot, M_Region region, M_Subregion subregion)
        {
            M_Level template = CustomLevelRegistry.Load("To_Anywhere");
            M_Level sourceLevel = GetDiverSourceLevel();
            Transform sourceRoot = FindChildByName(sourceLevel?.transform, "HadalDiver_Root");
            UT_Timer sourceSummon = null;
            if (sourceLevel != null)
            {
                foreach (UT_Timer timer in sourceLevel.GetComponentsInChildren<UT_Timer>(true))
                {
                    if (timer != null && timer.gameObject.name == "Hadal Diver Summon")
                    {
                        sourceSummon = timer;
                        break;
                    }
                }
            }

            DEN_HadalDiver source = sourceRoot?.GetComponentInChildren<DEN_HadalDiver>(true);
            if (template == null || sourceRoot == null || sourceSummon == null || source == null)
            {
                Debug.LogWarning("Custom Endless Hadal Diver summon assets are unavailable");
                return null;
            }

            M_Level level = UnityEngine.Object.Instantiate(template);
            level.gameObject.SetActive(false);
            level.name = "CustomEndless_HadalDiver";
            level.gameObject.name = level.name;
            foreach (Transform child in level.transform)
                child.gameObject.SetActive(false);

            GameObject spawnRoot = UnityEngine.Object.Instantiate(sourceRoot.gameObject, level.transform, false);
            spawnRoot.name = "CustomEndless_HadalDiver_SpawnRoot";
            spawnRoot.transform.localPosition = new Vector3(0f, 0f, 0f);
            spawnRoot.SetActive(false);
            DEN_HadalDiver diver = spawnRoot.GetComponentInChildren<DEN_HadalDiver>(true);
            GameObject summonObject = UnityEngine.Object.Instantiate(sourceSummon.gameObject, level.transform, false);
            summonObject.name = "Hadal Diver Summon";
            summonObject.transform.localPosition = Vector3.zero;
            summonObject.transform.localRotation = Quaternion.identity;
            summonObject.SetActive(true);
            UT_Timer summonTimer = summonObject.GetComponent<UT_Timer>();
            summonTimer.runOnAwake = false;
            summonTimer.endEvent = new UnityEvent();
            summonTimer.endEvent.AddListener(() => spawnRoot.SetActive(true));

            if (diver == null)
            {
                UnityEngine.Object.Destroy(level.gameObject);
                return null;
            }
            diver.exitEvent = new UnityEvent();
            Animator boardingAnimator = diver.GetComponent<Animator>();
            boardingAnimator?.SetBool("Idle", true);

            foreach (UT_TriggerZone trigger in spawnRoot.GetComponentsInChildren<UT_TriggerZone>(true))
            {
                if (trigger.onEnter == null)
                    continue;
                for (int i = 0; i < trigger.onEnter.GetPersistentEventCount(); i++)
                {
                    if (trigger.onEnter.GetPersistentMethodName(i) == "EnterChamber")
                    {
                        trigger.enabled = false;
                        break;
                    }
                }
            }

            float length = ConfigureDiverLength(diver, slot);
            spawnRoot.transform.localPosition = new Vector3(0f, length, 0f);
            diver.noiseBlend = 1f;

            Transform entrance = new GameObject("HadalDiverEntrance").transform;
            entrance.SetParent(level.transform, false);
            entrance.localPosition = Vector3.zero;
            entrance.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);

            Transform exit = new GameObject("HadalDiverExit").transform;
            exit.SetParent(level.transform, false);
            exit.localPosition = new Vector3(0f, length, 0f);
            exit.localRotation = entrance.localRotation;

            level.levelName = "CustomEndless_HadalDiver";
            level.levelEntrance = entrance;
            level.levelExit = exit;
            level.spawnPosition = entrance;
            level.region = region;
            level.subRegion = subregion;
            level.loadAdjacent = true;
            level.pauseGeneration = false;
            level.isLODGap = false;
            level.lastLevel = false;
            level.canFlip = false;
            level.lodAsset = null;
            level.customBounds = true;
            level.levelBounds = new Bounds(new Vector3(0f, length * 0.5f, 0f), new Vector3(16f, length, 16f));
            cachedLevelBounds?.SetValue(level, level.levelBounds);

            EndlessDiverLevel levelMarker = level.gameObject.AddComponent<EndlessDiverLevel>();
            levelMarker.Diver = diver;
            levelMarker.SpawnRoot = spawnRoot;
            levelMarker.SummonTimer = summonTimer;
            levelMarker.Slot = slot;
            EndlessDiverInstance diverMarker = diver.gameObject.AddComponent<EndlessDiverInstance>();
            diverMarker.Level = levelMarker;
            RegisterRuntimeObject(level.gameObject, false);
            return level;
        }

        private static M_Region BuildDiverRegion(int slot)
        {
            M_Region region = GetAndFilterRegion("Region_Shortcut_HadalDiver", slot);
            if (region == null || region.subregionGroups == null || region.subregionGroups.Count == 0 ||
                region.subregionGroups[0].subregions == null || region.subregionGroups[0].subregions.Count == 0)
                return null;

            M_Subregion subregion = region.subregionGroups[0].subregions[0];
            M_Level level = CreateDiverLevel(slot, region, subregion);
            if (level == null)
                return null;

            M_Level.LevelAssetHolder holder = M_Level.LevelAssetHolder.GetNewHolderFromLevel(level);
            holder.region = region;
            holder.subregion = subregion;
            subregion.subregionOrder = M_Subregion.SubregionOrder.single;
            subregion.useLevelCount = true;
            subregion.subregionMinLength = 1;
            subregion.subregionMaxLength = 1;
            subregion.levelReferences = new List<M_Level.LevelAssetHolder>();
            subregion.startLevelReferences = new List<M_Level.LevelAssetHolder>();
            subregion.endLevelReferences = new List<M_Level.LevelAssetHolder>();
            subregion.levelGroups = new List<M_Subregion.LevelGroup>
            {
                new M_Subregion.LevelGroup
                {
                    name = "HadalDiver",
                    length = 1,
                    endSubregion = true,
                    levelReferences = new List<M_Level.LevelAssetHolder> { holder }
                }
            };
            region.regionHeight = level.GetLength();
            return region;
        }

        private static M_Level FindFollowingLevel(BranchInfo branch, M_Level source, string name)
        {
            if (branch == null || branch.levelTracker == null)
                return null;

            int sourceIndex = branch.levelTracker.FindIndex(info => info != null && info.GetLevel() == source);
            if (sourceIndex < 0)
                return null;

            for (int i = sourceIndex + 1; i < branch.levelTracker.Count; i++)
            {
                M_Level level = branch.levelTracker[i]?.GetLevel();
                if (level != null && level.levelName == name)
                    return level;
            }
            return null;
        }

        private static void TryConfigureDiverRig(DiverSummonRig rig)
        {
            if (rig == null || rig.Configured || rig.Diver == null || rig.Level == null)
                return;

            WorldLoader loader = WorldLoader.instance;
            BranchInfo branch = loader?.GetCurrentBranch();
            M_Level following = FindFollowingLevel(branch, rig.Level, "CustomEndless_HadalDiver");
            EndlessDiverLevel marker = following?.GetComponent<EndlessDiverLevel>();
            if (marker == null)
                return;

            ConfigureDiverLength(rig.Diver, marker.Slot);
            GameObject fallbackRoot = marker.SpawnRoot;
            marker.SpawnRoot = rig.SpawnRoot;
            marker.SummonTimer = rig.SummonTimer;
            marker.Diver = rig.Diver;
            marker.UsesEntranceRig = true;
            EndlessDiverInstance instance = rig.Diver.GetComponent<EndlessDiverInstance>();
            if (instance == null)
                instance = rig.Diver.gameObject.AddComponent<EndlessDiverInstance>();
            instance.Level = marker;
            if (fallbackRoot != null && fallbackRoot != rig.SpawnRoot)
                UnityEngine.Object.Destroy(fallbackRoot);
            rig.Configured = true;
        }

        private static void PlayDiverMusic()
        {
            M_Level source = GetCachedObject<M_Level>("Campaign_Interlude_Habitation_To_Abyss_01");
            if (source == null)
                return;

            foreach (UT_TriggerZone trigger in source.GetComponentsInChildren<UT_TriggerZone>(true))
            {
                if (trigger.onEnter == null)
                    continue;

                for (int i = 0; i < trigger.onEnter.GetPersistentEventCount(); i++)
                {
                    if (trigger.onEnter.GetPersistentMethodName(i) != "PlaySongAsset")
                        continue;

                    UT_SoundtrackController controller = trigger.onEnter.GetPersistentTarget(i) as UT_SoundtrackController;
                    FieldInfo groupField = AccessTools.Field(typeof(UnityEventBase), "m_PersistentCalls");
                    object group = groupField?.GetValue(trigger.onEnter);
                    FieldInfo callsField = group != null ? AccessTools.Field(group.GetType(), "m_Calls") : null;
                    IList calls = callsField != null ? callsField.GetValue(group) as IList : null;
                    object call = calls != null && i < calls.Count ? calls[i] : null;
                    FieldInfo argumentsField = call != null ? AccessTools.Field(call.GetType(), "m_Arguments") : null;
                    object arguments = argumentsField?.GetValue(call);
                    FieldInfo clipField = arguments != null ? AccessTools.Field(arguments.GetType(), "m_ObjectArgument") : null;
                    AudioClip clip = clipField != null ? clipField.GetValue(arguments) as AudioClip : null;
                    if (controller != null && clip != null)
                        controller.PlaySongAsset(clip);
                    return;
                }
            }
        }

        public static bool IsDiverTransition(M_Level level)
        {
            return level != null && (level.levelName == "To_Anywhere" || level.levelName == "To_Diver") &&
                (IsDiverRegion(level.region?.nextRegion) ||
                 level.region != null && level.region.name.ContainsInsensitive("Diver"));

        }

        private static void StartDiverSummon(M_Level source, bool direct)
        {
            WorldLoader loader = WorldLoader.instance;
            BranchInfo branch = loader?.GetCurrentBranch();
            if (source == null || branch == null || !pendingDiverSummons.Add(source))
                return;

            loader.StartCoroutine(EnterDiver(source, branch, direct));
        }

        private static IEnumerator EnterDiver(M_Level source, BranchInfo branch, bool direct)
        {
            try
            {
                if (source == null || ENT_Player.playerObject == null)
                    yield break;

                if (!direct)
                    source.ContinueGeneration();

                float deadline = Time.realtimeSinceStartup + 30f;
                M_Level level = direct ? source : FindFollowingLevel(branch, source, "CustomEndless_HadalDiver");
                while (level == null && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                    level = FindFollowingLevel(branch, source, "CustomEndless_HadalDiver");
                }

                if (level == null)
                {
                    Debug.LogWarning("Custom Endless Hadal Diver did not generate after its entrance");
                    yield break;
                }

                level.gameObject.SetActive(true);
                EndlessDiverLevel marker = level.GetComponent<EndlessDiverLevel>();
                DiverSummonRig entranceRig = !direct ? AttachDiverSummonRig(source) : null;
                if (marker != null && entranceRig != null)
                {
                    TryConfigureDiverRig(entranceRig);
                    GameObject fallbackRoot = marker.SpawnRoot;
                    marker.SpawnRoot = entranceRig.SpawnRoot;
                    marker.SummonTimer = entranceRig.SummonTimer;
                    marker.Diver = entranceRig.Diver;
                    marker.UsesEntranceRig = true;
                    EndlessDiverInstance entranceInstance = entranceRig.Diver.GetComponent<EndlessDiverInstance>();
                    if (entranceInstance == null)
                        entranceInstance = entranceRig.Diver.gameObject.AddComponent<EndlessDiverInstance>();
                    entranceInstance.Level = marker;
                    if (fallbackRoot != null && fallbackRoot != marker.SpawnRoot)
                        UnityEngine.Object.Destroy(fallbackRoot);
                }
                DEN_HadalDiver diver = marker?.Diver;
                if (marker == null || marker.SpawnRoot == null || marker.SummonTimer == null || diver == null)
                {
                    yield break;
                }

                yield return null;
                if (!marker.SpawnRoot.activeSelf)
                    marker.SummonTimer.Play(true);
                deadline = Time.realtimeSinceStartup + 15f;
                while (!marker.SpawnRoot.activeSelf && Time.realtimeSinceStartup < deadline)
                    yield return null;

                while ((diver.segments == null || diver.segments.Count == 0) && Time.realtimeSinceStartup < deadline)
                    yield return null;

                if (!marker.SpawnRoot.activeSelf || diver.segments == null || diver.segments.Count == 0 || ENT_Player.playerObject == null)
                {
                    yield break;
                }

                yield return null;
                yield return null;
                UT_SimpleAnimatorController animator = diver.GetComponent<UT_SimpleAnimatorController>();
                animator?.SetAnimatorBool("Idle:true");

                UT_ChamberLoader chamber = diver.GetComponent<UT_ChamberLoader>();
                if (chamber == null)
                {
                    yield break;
                }

                chamber.EnterChamber();
                marker.Entered = true;
                level.SetAsCurrentLevel();
                diver.StartCoroutine(FinishDiverEntrance(diver));
            }
            finally
            {
                pendingDiverSummons.Remove(source);
            }
        }

        private static IEnumerator FinishDiverEntrance(DEN_HadalDiver diver)
        {
            EndlessDiverInstance instance = diver.GetComponent<EndlessDiverInstance>();
            if (instance != null && instance.Level != null && !instance.Level.UsesEntranceRig)
                diver.transform.position = instance.Level.GetComponent<M_Level>().levelExit.position;
            diver.SnapSegmentsToFinalPosition();
            yield return new WaitForEndOfFrame();
            yield return null;

            UT_Locator tail = null;
            foreach (UT_Locator locator in diver.GetComponentsInChildren<UT_Locator>(true))
            {
                if (locator.locatorID == "hadaldiver_entrymover")
                {
                    tail = locator;
                    break;
                }
            }

            if (tail == null || ENT_Player.playerObject == null)
            {
                UT_ChamberLoader chamber = diver.GetComponent<UT_ChamberLoader>();
                chamber?.LeaveChamber();
                yield break;
            }

            ENT_Player.playerObject.Teleport(tail.transform.position, tail.transform.rotation);
            diver.PullPlayerToTail();
            try
            {
                PlayDiverMusic();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(exception);
            }
            diver.BeginDive();
            WorldLoader.instance?.StartCoroutine(WatchDiverExit(diver));
        }

        private static IEnumerator WatchDiverExit(DEN_HadalDiver diver)
        {
            List<UT_PlayerForceMover> pullMovers = new List<UT_PlayerForceMover>();
            foreach (DEN_HadalDiver_Segment segment in diver.segments)
            {
                if (segment == null)
                    continue;
                foreach (UT_PlayerForceMover mover in segment.GetComponentsInChildren<UT_PlayerForceMover>(true))
                {
                    if (mover.GetComponent<UT_HadalDiverController>() != null)
                        pullMovers.Add(mover);
                }
            }

            while (diver != null)
            {
                EndlessDiverInstance marker = diver.GetComponent<EndlessDiverInstance>();
                if (marker == null || marker.Exiting)
                    yield break;
                foreach (UT_PlayerForceMover mover in pullMovers)
                {
                    if (mover == null || !mover.active)
                        continue;
                    yield return new WaitForSeconds(1.5f);
                    BeginCustomDiverExit(diver, marker);
                    yield break;
                }
                yield return null;
            }
        }

        private static IEnumerator ExitDiver(DEN_HadalDiver diver, EndlessDiverInstance marker)
        {
            M_Level source = marker.Level != null ? marker.Level.GetComponent<M_Level>() :
                marker.Rig?.Level;
            WorldLoader loader = WorldLoader.instance;
            BranchInfo branch = loader?.GetCurrentBranch();
            if (source == null || branch == null)
            {
                marker.Exiting = false;
                yield break;
            }

            UT_ChamberLoader chamber = diver.GetComponent<UT_ChamberLoader>();
            foreach (DEN_HadalDiver_Segment segment in diver.segments)
            {
                if (segment == null)
                    continue;
                foreach (UT_PlayerForceMover mover in segment.GetComponentsInChildren<UT_PlayerForceMover>(true))
                {
                    if (mover.active)
                        mover.SetActive(false);
                }
                foreach (FX_Zone zone in segment.GetComponentsInChildren<FX_Zone>(true))
                    zone.FadeTo(0f);
            }
            chamber?.LeaveChamber();

            source.ContinueGeneration();
            float deadline = Time.realtimeSinceStartup + 30f;
            M_Level exit = FindFollowingLevel(branch, source, "From_Anywhere");
            while (exit == null && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                exit = FindFollowingLevel(branch, source, "From_Anywhere");
            }

            if (exit == null || ENT_Player.playerObject == null)
            {
                marker.Exiting = false;
                yield break;
            }

            exit.gameObject.SetActive(true);
            Quaternion spawnRotation = exit.GetSpawnPositionTransform().rotation;
            ENT_Player.playerObject.Teleport(exit.GetSpawnPosition(), spawnRotation);
            ENT_Player.playerObject.SetPlayerRotation(new Vector3(0f, spawnRotation.eulerAngles.y, 0f));
            exit.SetAsCurrentLevel();
            UnityEngine.Object.Destroy(diver.gameObject);
        }

        [HarmonyPatch(typeof(M_Level), nameof(M_Level.OnLevelActivate))]
        private static class DiverRoomActivationPatch
        {
            private static void Postfix(M_Level __instance)
            {
                AttachDiverSummonRig(__instance);
                if (__instance != null && (__instance.levelName == "To_Diver" || __instance.levelName == "CustomEndless_HadalDiver") &&
                    CL_GameManager.GetCurrentGamemode() == EndlessMode)
                    DestroyMassInstance();
            }
        }

        [HarmonyPatch(typeof(M_Level), nameof(M_Level.OnEnter))]
        private static class DiverEntrancePatch
        {
            private static void Postfix(M_Level __instance)
            {
                if (__instance == null)
                    return;

                if (IsDiverTransition(__instance))
                {
                    AttachDiverSummonRig(__instance);
                    __instance.ContinueGeneration();
                    return;
                }

                EndlessDiverLevel marker = __instance.GetComponent<EndlessDiverLevel>();
                if (marker != null && !marker.Entered)
                    StartDiverSummon(__instance, true);
            }
        }

        private static void StartDiverFromTrigger(UT_TriggerZone trigger, Collider other)
        {
            if (trigger == null || other == null || other.GetComponentInParent<ENT_Player>() == null)
                return;

            M_Level level = trigger.GetComponentInParent<M_Level>();
            if (!IsDiverTransition(level) || level.levelExit == null ||
                Vector3.SqrMagnitude(trigger.transform.position - level.levelExit.position) > 144f)
                return;

            StartDiverSummon(level, false);
        }

        private static void StartDiverExitFromTrigger(UT_TriggerZone trigger, Collider other)
        {
            if (trigger == null || other == null || other.GetComponentInParent<ENT_Player>() == null ||
                trigger.GetComponent<UT_PlayerForceMover>() == null)
                return;

            DEN_HadalDiver_Segment segment = trigger.GetComponentInParent<DEN_HadalDiver_Segment>();
            DEN_HadalDiver diver = DEN_HadalDiver.instance;
            EndlessDiverInstance marker = diver?.GetComponent<EndlessDiverInstance>();
            if (segment == null || diver == null || marker == null || marker.Level == null ||
                diver.segments == null || !diver.segments.Contains(segment) || marker.Exiting)
                return;

            diver.StartCoroutine(CompleteDiverExitAfterPull(diver, marker));
        }

        private static IEnumerator CompleteDiverExitAfterPull(DEN_HadalDiver diver, EndlessDiverInstance marker)
        {
            yield return new WaitForSeconds(1.5f);
            BeginCustomDiverExit(diver, marker);
        }

        [HarmonyPatch]
        private static class DiverTeleportTriggerPatch
        {
            private static MethodBase TargetMethod()
            {
                return AccessTools.Method(typeof(UT_TriggerZone), "OnTriggerEnter");
            }

            private static void Postfix(UT_TriggerZone __instance, Collider other)
            {
                StartDiverFromTrigger(__instance, other);
                StartDiverExitFromTrigger(__instance, other);
            }
        }

        [HarmonyPatch]
        private static class DiverCustomTeleportTriggerPatch
        {
            private static MethodBase TargetMethod()
            {
                return AccessTools.Method(typeof(TeleportTrigger), "OnTriggerEnter");
            }

            private static void Postfix(TeleportTrigger __instance, Collider other)
            {
                if (__instance == null || other == null || other.GetComponentInParent<ENT_Player>() == null)
                    return;

                M_Level level = __instance.GetComponentInParent<M_Level>();
                if (IsDiverTransition(level))
                    StartDiverSummon(level, false);
            }
        }

        [HarmonyPatch(typeof(WorldLoader), nameof(WorldLoader.GetClosestLevelToPosition))]
        private static class DiverClosestLevelPatch
        {
            private static void Postfix(ref LevelInfo __result)
            {
                M_Level level = __result?.GetLevel();
                EndlessDiverLevel marker = level?.GetComponent<EndlessDiverLevel>();
                if (marker == null || marker.Entered)
                    return;

                WorldLoader loader = WorldLoader.instance;
                BranchInfo branch = loader?.GetCurrentBranch();
                if (branch == null || branch.levelTracker == null)
                    return;

                int index = branch.levelTracker.IndexOf(__result);
                if (index > 0)
                    __result = branch.levelTracker[index - 1];
            }
        }

        [HarmonyPatch(typeof(DEN_HadalDiver), nameof(DEN_HadalDiver.ExitDiver))]
        private static class DiverExitPatch
        {
            private static bool Prefix(DEN_HadalDiver __instance)
            {
                EndlessDiverInstance marker = __instance?.GetComponent<EndlessDiverInstance>();
                if (marker == null)
                    return true;

                BeginCustomDiverExit(__instance, marker);
                return false;
            }
        }

        [HarmonyPatch(typeof(UT_EventController), nameof(UT_EventController.PlayEvent))]
        private static class DiverBoardingEventPatch
        {
            private static bool Prefix(UT_EventController __instance, string eventName)
            {
                if (eventName != "noboard" && eventName != "removepulltrigger")
                    return true;
                DEN_HadalDiver diver = __instance?.GetComponent<DEN_HadalDiver>();
                return diver == null || diver.GetComponent<EndlessDiverInstance>() == null;
            }
        }

        [HarmonyPatch(typeof(DEN_HadalDiver), nameof(DEN_HadalDiver.DestroyDiver))]
        private static class DiverBoardingTimeoutPatch
        {
            private static bool Prefix(DEN_HadalDiver __instance)
            {
                return __instance == null || __instance.GetComponent<EndlessDiverInstance>() == null;
            }
        }

        private static void BeginCustomDiverExit(DEN_HadalDiver diver, EndlessDiverInstance marker)
        {
            if (diver == null || marker == null || marker.Exiting || WorldLoader.instance == null)
                return;
            if (marker.Level == null && marker.Rig != null)
                TryConfigureDiverRig(marker.Rig);
            if (marker.Level == null && (marker.Rig == null || marker.Rig.Level == null))
            {
                Debug.LogWarning("Custom Endless Hadal Diver exit has no entrance level");
                return;
            }
            marker.Exiting = true;
            diver.StartCoroutine(ExitDiver(diver, marker));
        }

        [HarmonyPatch(typeof(UT_PlayerForceMover), nameof(UT_PlayerForceMover.SetActive))]
        private static class DiverPullExitPatch
        {
            private static void Postfix(UT_PlayerForceMover __instance, bool b)
            {
                if (!b || __instance == null || __instance.GetComponent<UT_HadalDiverController>() == null)
                    return;
                DEN_HadalDiver diver = DEN_HadalDiver.instance;
                EndlessDiverInstance marker = diver?.GetComponent<EndlessDiverInstance>();
                if (marker != null && !marker.Exiting && WorldLoader.instance != null)
                    WorldLoader.instance.StartCoroutine(CompleteDiverExitAfterPull(diver, marker));
            }
        }

        [HarmonyPatch(typeof(UT_HadalDiverController), nameof(UT_HadalDiverController.ExitDiver))]
        private static class DiverControllerExitPatch
        {
            private static bool Prefix()
            {
                DEN_HadalDiver diver = DEN_HadalDiver.instance;
                EndlessDiverInstance marker = diver?.GetComponent<EndlessDiverInstance>();
                if (marker == null)
                    return true;
                BeginCustomDiverExit(diver, marker);
                return false;
            }
        }
    }
}
