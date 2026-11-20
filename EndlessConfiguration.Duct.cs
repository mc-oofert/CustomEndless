using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using static WorldLoader;

//I'm going to stop writing these comments now, this isn't really how you're supposed to do it anyways. Just message me on discord if you need help finding where the logic for something is or need help with a bug. I wish I could like. Download my knowledge and give it to you; the code related stuff, that is. The other stuff isn't fit for mortal grasp.

namespace Celestial.CustomEndless
{
    public static partial class EndlessConfiguration
    {
        private sealed class DuctState
        {
            public M_Level Level;
            public Transform Button;
            public bool Pressed;
            public bool HandholdReady;
        }

        private static readonly List<DuctState> ductStates = new List<DuctState>();
        private static readonly FieldInfo cachedLevelBounds = AccessTools.Field(typeof(M_Level), "currentBoundsLocal");
        private static bool ductUnloadInProgress;

        private static bool IsDuct(M_Level level)
        {
            return level != null && (level.levelName == "To_Duct" || level.name == "To_Duct" || level.name.StartsWith("To_Duct(") || level.levelName == "To_Cavity" || level.name == "To_Cavity" || level.name.StartsWith("To_Cavity("));
        }

        private static DuctState EnsureDuct(M_Level level)
        {
            if (!IsDuct(level))
                return null;

            ductStates.RemoveAll(state => state.Level == null);
            DuctState duct = ductStates.Find(state => state.Level == level);
            if (duct == null)
            {
                duct = new DuctState { Level = level };
                ductStates.Add(duct);
                if (level.customBounds && cachedLevelBounds != null)
                    cachedLevelBounds.SetValue(level, level.levelBounds);
            }

            if (duct.Button == null)
            {
                foreach (Transform child in level.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name == "ButtonOfDoomAndDespair")
                    {
                        duct.Button = child;
                        break;
                    }
                }
            }

            if (duct.Button == null)
                return duct;

            GameObject button = duct.Button.gameObject;
            HandholdManager manager = WorldLoader.instance != null ? WorldLoader.instance.GetComponent<HandholdManager>() : null;
            if (manager != null && manager.handholdTypes != null && manager.handholdTypes.Count > 0 && manager.handholdTypes[0].prefab != null)
                button.layer = manager.handholdTypes[0].prefab.layer;
            if (duct.HandholdReady)
                return duct;

            if (button.GetComponent<Collider>() == null)
            {
                BoxCollider box = button.AddComponent<BoxCollider>();
                MeshFilter filter = button.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                {
                    box.center = filter.sharedMesh.bounds.center;
                    box.size = filter.sharedMesh.bounds.size;
                }
            }

            CL_Handhold handhold = button.GetComponent<CL_Handhold>();
            if (handhold == null)
                handhold = button.AddComponent<CL_Handhold>();
            ObjectTagger tagger = button.GetComponent<ObjectTagger>();
            if (tagger == null)
                tagger = button.AddComponent<ObjectTagger>();
            tagger.AddTag("Handhold");
            if (handhold.activeEvent == null)
                handhold.activeEvent = new UnityEvent();
            if (handhold.stopEvent == null)
                handhold.stopEvent = new UnityEvent();
            duct.HandholdReady = true;
            Debug.Log("To_Duct button handhold ready");
            return duct;
        }

        private static DuctState GetGate(BranchInfo branch, out int ductIndex)
        {
            ductIndex = -1;
            if (branch == null || branch.levelTracker == null)
                return null;

            for (int i = 0; i < branch.levelTracker.Count; i++)
            {
                LevelInfo info = branch.levelTracker[i];
                DuctState duct = EnsureDuct(info != null ? info.GetLevel() : null);
                if (duct != null && !duct.Pressed)
                {
                    ductIndex = i;
                    return duct;
                }
            }
            return null;
        }

        private static void PressDuct(DuctState duct)
        {
            if (duct == null || duct.Pressed || duct.Level == null || duct.Button == null)
                return;

            duct.Pressed = true;
            Debug.Log("To_Duct button grabbed");
            foreach (Light light in duct.Button.GetComponentsInChildren<Light>(true))
            {
                if (light.transform != duct.Button && light.type == LightType.Point)
                {
                    CL_Lamp lamp = light.GetComponent<CL_Lamp>();
                    if (lamp != null)
                    {
                        lamp.lampActive = false;
                        lamp.InstantFade(0f);
                    }
                    light.enabled = false;
                    break;
                }
            }

            Transform otherSide = duct.Button.Find("OtherSide");
            if (otherSide != null)
                otherSide.gameObject.SetActive(true);

            WorldLoader loader = WorldLoader.instance;
            BranchInfo branch = loader != null ? loader.GetCurrentBranch() : null;
            if (branch == null || branch.levelTracker == null)
                return;

            ductUnloadInProgress = true;
            try
            {
                loader.UnloadPreviousLevels(0);
            }
            finally
            {
                ductUnloadInProgress = false;
            }

            int ductIndex = branch.levelTracker.FindIndex(info => info != null && info.GetLevel() == duct.Level);
            if (ductIndex >= 0 && ductIndex + 1 < branch.levelTracker.Count)
            {
                M_Level following = branch.levelTracker[ductIndex + 1].GetLevel();
                if (following != null)
                    following.gameObject.SetActive(true);
            }
        }

        [HarmonyPatch(typeof(M_Level), nameof(M_Level.OnSpawn))]
        private static class DuctSpawnPatch
        {
            private static void Postfix(M_Level __instance)
            {
                EnsureDuct(__instance);
            }
        }

        [HarmonyPatch(typeof(HandholdManager), nameof(HandholdManager.LoadHandholds))]
        private static class DuctHandholdPatch
        {
            private static void Postfix(GameObject g)
            {
                if (g != null)
                    EnsureDuct(g.GetComponent<M_Level>());
            }
        }

        [HarmonyPatch(typeof(CL_Handhold), nameof(CL_Handhold.Interact))]
        private static class DuctGrabPatch
        {
            private static void Postfix(CL_Handhold __instance)
            {
                foreach (DuctState duct in ductStates)
                {
                    if (duct.Level != null && duct.Button == __instance.transform)
                    {
                        PressDuct(duct);
                        return;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(WorldLoader), nameof(WorldLoader.GetClosestLevelToPosition))]
        private static class DuctClosestLevelPatch
        {
            private static void Postfix(Vector3 position, ref LevelInfo __result)
            {
                WorldLoader loader = WorldLoader.instance;
                BranchInfo branch = loader != null ? loader.GetCurrentBranch() : null;
                DuctState duct = GetGate(branch, out int ductIndex);
                if (duct == null || duct.Level.levelEntrance == null || __result == null)
                    return;

                int resultIndex = branch.levelTracker.IndexOf(__result);
                bool pastEntrance = Vector3.Dot(position - duct.Level.levelEntrance.position, duct.Level.levelEntrance.forward) >= 0f;
                if (pastEntrance && duct.Level.GetLocalBounds().Contains(duct.Level.transform.InverseTransformPoint(position)))
                {
                    __result = branch.levelTracker[ductIndex];
                    return;
                }

                if (resultIndex <= ductIndex)
                    return;

                float closestDistance = float.PositiveInfinity;
                for (int i = 0; i <= ductIndex; i++)
                {
                    LevelInfo info = branch.levelTracker[i];
                    M_Level level = info != null ? info.GetLevel() : null;
                    if (level == null)
                        continue;
                    Vector3 localPosition = level.transform.InverseTransformPoint(position);
                    Bounds bounds = level.GetLocalBounds();
                    if (bounds.Contains(localPosition))
                    {
                        __result = info;
                        return;
                    }
                    float distance = bounds.SqrDistance(localPosition);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        __result = info;
                    }
                }
            }
        }

        [HarmonyPatch]
        private static class DuctLevelActivationPatch
        {
            private static MethodBase TargetMethod()
            {
                foreach (MethodInfo method in typeof(WorldLoader).GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic))
                {
                    if (method.Name.Contains("g__CheckLevelLoad"))
                        return method;
                }
                return null;
            }

            private static bool Prefix(LevelInfo info, ref bool __result)
            {
                BranchInfo branch = info != null ? info.branch : null;
                DuctState duct = GetGate(branch, out int ductIndex);
                if (duct == null || branch.levelTracker.IndexOf(info) <= ductIndex)
                    return true;

                M_Level level = info.GetLevel();
                if (level != null)
                    level.gameObject.SetActive(false);
                info.loaded = false;
                info.adjacent = false;
                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(WorldLoader), "UnloadLevelRange")]
        private static class DuctUnloadRangePatch
        {
            private static bool Prefix(WorldLoader __instance, int start, ref int end)
            {
                if (ductUnloadInProgress)
                    return true;

                BranchInfo branch = __instance.GetCurrentBranch();
                if (branch == null || branch.levelTracker == null)
                    return true;

                DuctState duct = GetGate(branch, out int ductIndex);
                if (duct != null && start <= ductIndex && end > start)
                    return false;

                ENT_Player player = ENT_Player.GetPlayer();
                if (player == null)
                    return true;

                Vector3 position = player.transform.position;
                for (int i = start; i < end && i < branch.levelTracker.Count; i++)
                {
                    LevelInfo info = branch.levelTracker[i];
                    M_Level level = info != null ? info.GetLevel() : null;
                    if (level == null || !EndlessConfiguration.IsLastLevelBeforeRegion(info))
                        continue;
                    if (EndlessConfiguration.IsPlayerWithinLevel(level, position))
                    {
                        end = i;
                        break;
                    }
                }
                return true;
            }
        }
    }
}
