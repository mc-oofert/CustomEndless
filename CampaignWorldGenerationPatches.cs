using HarmonyLib;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

// Makes campaign work as a region. If campaign breaks look here and keep patches for it here.

namespace Celestial.CustomEndless
{
    [HarmonyPatch(typeof(WorldLoader), "GenerateLevels")]
    public static class CampaignShortcutReturnPatch
    {
        private static void Prefix(WorldLoader.BranchInfo branch, ref WorldLoader.GenerationParameters genParams)
        {
            if (branch == null || branch.levelTracker == null || branch.levelTracker.Count == 0 ||
                branch.parentLevel == null ||
                CL_GameManager.GetCurrentGamemode() != EndlessConfiguration.EndlessMode)
                return;

            M_Level source = branch.parentLevel.GetLevel();
            M_Level lastLevel = branch.levelTracker[branch.levelTracker.Count - 1]?.GetLevel();
            M_Region shortcutRegion = lastLevel?.region;
            M_Region campaignRegion = EndlessConfiguration.GetCampaignRegionForLevel(source) ?? EndlessConfiguration.GetCampaignRegionForBranch(branch);
            if (campaignRegion == null || shortcutRegion == null)
                return;

            string destinationName = shortcutRegion.regionName == "rotten-duct" ? "abyss" :
                shortcutRegion.regionName == "sink" ? "pipeworks" :
                shortcutRegion.regionName == "chute" ? "habitation" :
                shortcutRegion.regionName == "hadal-diver" ? "nest" : null;
            if (destinationName == null || EndlessConfiguration.EndlessMode.regions == null)
                return;

            int slotIndex = campaignRegion.name.IndexOf("_slot_", System.StringComparison.Ordinal);
            string campaignSuffix = slotIndex >= 0 ? campaignRegion.name.Substring(slotIndex) : "_CampaignRuntimeCopy";
            M_Region destinationRegion = EndlessConfiguration.EndlessMode.regions.Find(region =>
                region != null && region.regionName == destinationName &&
                region.name.EndsWith(campaignSuffix, System.StringComparison.Ordinal));
            if (destinationRegion == null)
                return;

            if (genParams == null)
                genParams = new WorldLoader.GenerationParameters();
            genParams.generator = EndlessConfiguration.EndlessMode;
            genParams.lastRegion = shortcutRegion;
            genParams.startRegion = destinationRegion;
            genParams.forceResetGeneratorToGamemode = false;
        }
    }

    [HarmonyPatch]
    public static class CampaignGenerationPauseIndexPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.EnumeratorMoveNext(AccessTools.Method(typeof(WorldLoader), "GenerateLevels"));
        }

        private static int PlayerStartIndex(int startingLevel)
        {
            return CL_GameManager.GetCurrentGamemode() == EndlessConfiguration.EndlessMode ? 0 : startingLevel;
        }

        private static int BranchLevelIndex(int index, int startingLevel)
        {
            return CL_GameManager.GetCurrentGamemode() == EndlessConfiguration.EndlessMode ? index + startingLevel : index;
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> code = new List<CodeInstruction>(instructions);
            FieldInfo startingLevel = null;
            foreach (CodeInstruction instruction in code)
                if (instruction.operand is FieldInfo field && field.Name.StartsWith("<startingLevel>", System.StringComparison.Ordinal))
                    startingLevel = field;
            if (startingLevel == null) throw new System.InvalidOperationException("WorldLoader starting level field was not found.");
            MethodInfo normalize = AccessTools.Method(typeof(CampaignGenerationPauseIndexPatch), nameof(PlayerStartIndex));
            MethodInfo offset = AccessTools.Method(typeof(CampaignGenerationPauseIndexPatch), nameof(BranchLevelIndex));
            MethodInfo getLevel = AccessTools.PropertyGetter(typeof(List<WorldLoader.LevelInfo>), "Item");
            int normalized = 0;
            int corrected = 0;
            for (int i = 0; i < code.Count; i++)
            {
                CodeInstruction instruction = code[i];
                if (instruction.opcode == OpCodes.Stfld && instruction.operand is FieldInfo destination &&
                    destination.Name.StartsWith("<playerStartLevel>", System.StringComparison.Ordinal) &&
                    i > 0 && code[i - 1].opcode == OpCodes.Ldfld && Equals(code[i - 1].operand, startingLevel))
                {
                    CodeInstruction call = new CodeInstruction(OpCodes.Call, normalize);
                    call.labels.AddRange(instruction.labels);
                    instruction.labels.Clear();
                    yield return call;
                    normalized++;
                }
                if (instruction.Calls(getLevel) && i > 0 && code[i - 1].opcode == OpCodes.Ldfld &&
                    code[i - 1].operand is FieldInfo index &&
                    (index.Name.StartsWith("<n>", System.StringComparison.Ordinal) || index.Name.StartsWith("<i>", System.StringComparison.Ordinal)))
                {
                    CodeInstruction load = new CodeInstruction(OpCodes.Ldarg_0);
                    load.labels.AddRange(instruction.labels);
                    instruction.labels.Clear();
                    yield return load;
                    yield return new CodeInstruction(OpCodes.Ldfld, startingLevel);
                    yield return new CodeInstruction(OpCodes.Call, offset);
                    corrected++;
                }
                yield return instruction;
            }
            if (normalized != 1 || corrected == 0)
                throw new System.InvalidOperationException("WorldLoader generation pause indices were not found.");
        }
    }

    [HarmonyPatch(typeof(M_Level), nameof(M_Level.OnSpawn))]
    public static class CampaignNestInterludePausePatch
    {
        private static void Prefix(M_Level __instance, M_Region r)
        {
            if (CL_GameManager.GetCurrentGamemode() != EndlessConfiguration.EndlessMode ||
                __instance == null || r == null || !r.name.Contains("_CampaignRuntimeCopy") ||
                string.IsNullOrEmpty(__instance.levelName) ||
                !__instance.levelName.StartsWith("Campaign_Interlude_Abyss_To_Nest_01_Prison", System.StringComparison.OrdinalIgnoreCase))
                return;

            __instance.pauseGeneration = true;
        }
    }

    [HarmonyPatch(typeof(M_Level), nameof(M_Level.OnSpawn))]
    public static class CampaignEndingSpawnPatch
    {
        private static void Postfix(M_Level __instance, M_Region r)
        {
            if (CL_GameManager.GetCurrentGamemode() != EndlessConfiguration.EndlessMode ||
                __instance == null || __instance.levelName != "M5_Nest_Trough_Ending_01" ||
                r == null || r.regionName != "nest" || !r.name.Contains("_CampaignRuntimeCopy"))
                return;

            __instance.lastLevel = false;
            __instance.pauseGeneration = false;
        }
    }

    [HarmonyPatch(typeof(UT_GameStateController), nameof(UT_GameStateController.Win))]
    public static class CampaignEndingWinPatch
    {
        private static M_Level pendingEnding;

        private static bool Prefix(UT_GameStateController __instance)
        {
            if (CL_GameManager.GetCurrentGamemode() != EndlessConfiguration.EndlessMode || __instance == null)
                return true;

            M_Level ending = __instance.GetComponentInParent<M_Level>();
            if (ending == null || ending.levelName != "M5_Nest_Trough_Ending_01" ||
                ending.region == null || ending.region.regionName != "nest" ||
                !ending.region.name.Contains("_CampaignRuntimeCopy"))
                return true;

            WorldLoader loader = WorldLoader.instance;
            if (loader != null && pendingEnding != ending)
            {
                pendingEnding = ending;
                loader.StartCoroutine(ExitCampaignEnding(loader, ending));
            }

            return false;
        }

        private static IEnumerator ExitCampaignEnding(WorldLoader loader, M_Level ending)
        {
            WorldLoader.BranchInfo branch = loader.GetBranchOfLevel(ending);
            if (branch == null || branch.levelTracker == null)
            {
                pendingEnding = null;
                yield break;
            }

            ending.ContinueGeneration();
            float deadline = Time.realtimeSinceStartup + 30f;
            while (loader == WorldLoader.instance &&
                   CL_GameManager.GetCurrentGamemode() == EndlessConfiguration.EndlessMode &&
                   Time.realtimeSinceStartup < deadline)
            {
                int endingIndex = branch.levelTracker.FindIndex(info => info != null && info.GetLevel() == ending);
                for (int i = endingIndex + 1; endingIndex >= 0 && i < branch.levelTracker.Count; i++)
                {
                    M_Level exit = branch.levelTracker[i]?.GetLevel();
                    if (exit == null || exit.levelName != "From_Anywhere")
                        continue;

                    ENT_Player player = ENT_Player.GetPlayer();
                    if (player != null)
                    {
                        exit.gameObject.SetActive(true);
                        Transform spawn = exit.GetSpawnPositionTransform();
                        player.Teleport(exit.GetSpawnPosition(), spawn.rotation);
                        player.SetPlayerRotation(new Vector3(0f, spawn.rotation.eulerAngles.y, 0f));
                        exit.SetAsCurrentLevel();
                    }
                    yield break;
                }

                yield return null;
            }

            Debug.LogWarning("Custom Endless campaign ending could not find From_Anywhere anywhere");
            pendingEnding = null;
        }
    }

    [HarmonyPatch(typeof(UT_GameStateController), nameof(UT_GameStateController.CutToBlack))]
    public static class CampaignEndingBlackoutPatch
    {
        private static bool Prefix(UT_GameStateController __instance)
        {
            if (CL_GameManager.GetCurrentGamemode() != EndlessConfiguration.EndlessMode || __instance == null)
                return true;

            M_Level ending = __instance.GetComponentInParent<M_Level>();
            return ending == null || ending.levelName != "M5_Nest_Trough_Ending_01" ||
                   ending.region == null || !ending.region.name.Contains("_CampaignRuntimeCopy");
        }
    }

    [HarmonyPatch(typeof(WorldLoader), "GenerateLevels")]
    public static class CampaignWorldGenerationPatch
    {
        private static void Prefix(
            WorldLoader.BranchInfo branch,
            ref WorldLoader.GenerationParameters genParams)
        {
            if (genParams == null || branch == null || branch.levelTracker == null ||
                branch.levelTracker.Count == 0 ||
                CL_GameManager.GetCurrentGamemode() != EndlessConfiguration.EndlessMode)
                return;

            M_Level continuedFrom = branch.levelTracker[branch.levelTracker.Count - 1]?.GetLevel();
            if (continuedFrom == null || continuedFrom.levelName != "M5_Nest_Trough_Ending_01" ||
                continuedFrom.region == null ||
                !continuedFrom.region.name.Contains("_CampaignRuntimeCopy"))
                return;

            M_Region endedRegion = continuedFrom.region;
            if (EndlessConfiguration.EndlessMode == null ||
                EndlessConfiguration.EndlessMode.regions == null ||
                EndlessConfiguration.EndlessMode.regions.Count == 0)
                return;

            if (!EndlessConfiguration.EndlessMode.regions.Contains(endedRegion))
                return;

            WorldLoader.instance.DeleteOtherBranches(branch.id);
            genParams.generator = EndlessConfiguration.EndlessMode;
            genParams.lastRegion = endedRegion;
            genParams.startRegion = null;
            genParams.forceResetGeneratorToGamemode = false;
        }

    }

    [HarmonyPatch(typeof(M_Level), "OnSpawn")]
    public static class LevelSpawnOrientationPatch
    {
        internal static bool IsTransitionRoom(M_Level level)
        {
            string name = level?.levelName;
            if (string.IsNullOrEmpty(name) && level != null) name = level.name;
            return !string.IsNullOrEmpty(name) &&
                (name.StartsWith("Adaptor_", System.StringComparison.OrdinalIgnoreCase) ||
                 name.StartsWith("From_", System.StringComparison.OrdinalIgnoreCase) ||
                 name.StartsWith("Breakroom_", System.StringComparison.OrdinalIgnoreCase) ||
                 name.StartsWith("Universal_Breakroom", System.StringComparison.OrdinalIgnoreCase) ||
                 name.StartsWith("To_", System.StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsAlignmentExcluded(M_Level level)
        {
            return level != null && string.Equals(level.levelName, "To_Duct", System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool ShouldCorrect(M_Level previous, M_Level level)
        {
            return previous != null && previous != level && !IsAlignmentExcluded(level) &&
                (IsTransitionRoom(previous) || IsTransitionRoom(level));
        }

        [HarmonyPatch(typeof(HandholdManager), nameof(HandholdManager.LoadHandholds))]
        private static class FoldbackPatch
        {
            private static void Prefix(GameObject g)
            {
                WorldLoader loader = WorldLoader.instance;
                M_Level level = g?.GetComponent<M_Level>();
                if (loader == null || level == null || level.transform.parent != loader.transform ||
                    CL_GameManager.GetCurrentGamemode() != EndlessConfiguration.EndlessMode)
                    return;

                if (TryCorrectFoldback(loader.currentBranch, level))
                    return;

                if (loader.branchTracker == null)
                    return;

                for (int i = loader.branchTracker.Count - 1; i >= 0; i--)
                {
                    if (TryCorrectFoldback(loader.branchTracker[i], level))
                        return;
                }
            }
        }

        private static bool TryCorrectFoldback(WorldLoader.BranchInfo branch, M_Level level)
        {
            if (branch == null)
                return false;

            WorldLoader.LevelInfo previousInfo = branch.levelTracker != null && branch.levelTracker.Count > 0
                ? branch.levelTracker[branch.levelTracker.Count - 1]
                : branch.parentLevel;
            M_Level previousLevel = previousInfo?.GetLevel();
            if (!ShouldCorrect(previousLevel, level))
                return false;

            Transform previousExit = previousLevel.GetLevelExit();
            Transform entrance = level.GetLevelEntrance();
            Transform exit = level.GetLevelExit();
            if (previousExit == null || entrance == null || exit == null ||
                (previousExit.position - entrance.position).sqrMagnitude > 0.25f)
                return false;

            Vector3 travel = exit.position - entrance.position;
            if (travel.sqrMagnitude < 0.0001f)
                return true;

            float progress = Vector3.Dot(travel, previousExit.forward);
            if (progress >= -0.01f)
                return true;

            Quaternion turn = Quaternion.AngleAxis(180f, Vector3.up);
            if (Vector3.Dot(turn * travel, previousExit.forward) <= 0.01f)
                return true;

            level.transform.RotateAround(entrance.position, Vector3.up, 180f);
            entrance.rotation = previousExit.rotation;
            return true;
        }

        private static void Prefix(M_Level __instance)
        {
            if (__instance == null || IsAlignmentExcluded(__instance) ||
                CL_GameManager.GetCurrentGamemode() != EndlessConfiguration.EndlessMode)
                return;

            if (IsTransitionRoom(__instance))
                __instance.canFlip = false;

            WorldLoader loader = WorldLoader.instance;
            if (loader == null)
                return;

            if (TryOrientFromBranch(loader.currentBranch, __instance))
                return;

            if (loader.branchTracker == null)
                return;

            for (int i = loader.branchTracker.Count - 1; i >= 0; i--)
            {
                if (TryOrientFromBranch(loader.branchTracker[i], __instance))
                    return;
            }

        }

        private static bool TryOrientFromBranch(WorldLoader.BranchInfo branch, M_Level level)
        {
            if (branch == null)
                return false;

            WorldLoader.LevelInfo previousInfo = branch.levelTracker != null && branch.levelTracker.Count > 0
                ? branch.levelTracker[branch.levelTracker.Count - 1]
                : branch.parentLevel;
            M_Level previousLevel = previousInfo?.GetLevel();
            if (!ShouldCorrect(previousLevel, level))
                return false;

            Transform previousExit = previousLevel.GetLevelExit();
            Transform entrance = level.GetLevelEntrance();
            if (previousExit == null || entrance == null)
                return false;

            if (level.levelName == "From_Anywhere")
            {
                entrance.rotation = level.transform.rotation * previousExit.rotation;
                return true;
            }

            Quaternion alignedRotation = previousExit.rotation * Quaternion.Inverse(entrance.rotation) * level.transform.rotation;
            Vector3 alignedUp = alignedRotation * Vector3.up;
            if (Vector3.Dot(alignedUp, Vector3.up) >= 0.9999f)
                return true;

            Vector3 forward = Vector3.ProjectOnPlane(alignedRotation * Vector3.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
            {
                Vector3 right = Vector3.ProjectOnPlane(alignedRotation * Vector3.right, Vector3.up);
                forward = Vector3.Cross(right, Vector3.up);
            }
            if (forward.sqrMagnitude < 0.0001f)
                return true;

            Quaternion uprightRotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
            entrance.rotation = level.transform.rotation * Quaternion.Inverse(uprightRotation) * previousExit.rotation;
            return true;
        }
    }
}
