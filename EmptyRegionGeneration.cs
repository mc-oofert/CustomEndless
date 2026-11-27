using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Celestial.CustomEndless
{
    public static class EmptyRegionGeneration
    {
        private static readonly HashSet<string> warned = new HashSet<string>();

        private static bool Valid(M_Level.LevelAssetHolder holder)
        {
            return holder != null && (holder.level != null || holder.levelAssetReference != null && holder.levelAssetReference.RuntimeKeyIsValid());
        }

        private static bool HasLevels(M_Subregion subregion)
        {
            return subregion != null && subregion.CanSpawn() &&
                ((subregion.startLevelReferences?.Any(Valid) ?? false) ||
                 (subregion.endLevelReferences?.Any(Valid) ?? false) ||
                 (subregion.levelGroups?.Any(group => group?.levelReferences?.Any(Valid) ?? false) ?? false));
        }

        private static List<M_Level.LevelAssetHolder> Fallback()
        {
            M_Level source = CustomLevelRegistry.Load("From_Spawn") ?? EndlessConfiguration.GetCachedObject<M_Level>("From_Spawn");
            M_Level level = source != null ? UnityEngine.Object.Instantiate(source) : null;
            if (level == null) return new List<M_Level.LevelAssetHolder>();
            level.pauseGeneration = true;
            level.gameObject.SetActive(false);
            EndlessConfiguration.RegisterRuntimeObject(level.gameObject);
            M_Level.LevelAssetHolder holder = M_Level.LevelAssetHolder.GetNewHolderFromLevel(level);
            holder.region = EndlessConfiguration.EndlessMode.regions?.FirstOrDefault(region => region != null);
            holder.pauseGeneration = true;
            holder.isPausingGeneration = true;
            Debug.LogWarning("Custom Endless has No valid regions to generate.");
            return new List<M_Level.LevelAssetHolder> { holder };
        }

        [HarmonyPatch(typeof(M_Region), nameof(M_Region.GetLevels))]
        private static class RegionPatch
        {
            private sealed class RegionState
            {
                public List<M_Region.SubregionGroup> Groups;
            }

            [HarmonyPriority(Priority.First)]
            private static bool Prefix(M_Region __instance, ref PcgRandom rng, M_Region lastRegion,
                ref List<M_Level.LevelAssetHolder> __result, out RegionState __state)
            {
                __state = null;
                if (CL_GameManager.GetCurrentGamemode() != EndlessConfiguration.EndlessMode) return true;
                __state = new RegionState { Groups = __instance.subregionGroups };
                __instance.subregionGroups = (__state.Groups ?? new List<M_Region.SubregionGroup>())
                    .Where(group => group != null).Select(group => new M_Region.SubregionGroup
                    {
                        name = group.name,
                        subregions = (group.subregions ?? new List<M_Subregion>()).Where(HasLevels).ToList()
                    }).Where(group => group.subregions.Count > 0).ToList();
                if (__instance.subregionGroups.Count > 0) return true;
                __result = new List<M_Level.LevelAssetHolder>();
                if ((lastRegion == null || __instance.forceStartLevel) && __instance.startLevelReferences != null)
                    __result.AddRange(__instance.startLevelReferences.Where(Valid));
                else if (lastRegion != null && __instance.transitionLevels != null)
                    __result.AddRange(__instance.GetTransitionLevels(ref rng, lastRegion) ?? new List<M_Level.LevelAssetHolder>());
                foreach (M_Level.LevelAssetHolder holder in __result) holder.region = __instance;
                return false;
            }

            private static Exception Finalizer(M_Region __instance, RegionState __state,
                Exception __exception, ref List<M_Level.LevelAssetHolder> __result)
            {
                if (CL_GameManager.GetCurrentGamemode() != EndlessConfiguration.EndlessMode) return __exception;
                if (__state != null) __instance.subregionGroups = __state.Groups;
                if (__exception != null)
                {
                    if (warned.Add(__instance.name)) Debug.LogWarning("Skipping region " + __instance.name + ": " + __exception.Message);
                    __result = new List<M_Level.LevelAssetHolder>();
                }
                if (__result == null) __result = new List<M_Level.LevelAssetHolder>();
                __result.RemoveAll(holder => !Valid(holder));
                return null;
            }
        }

        [HarmonyPatch(typeof(M_Gamemode), nameof(M_Gamemode.GetGenerationList))]
        private static class GamemodePatch
        {
            private static bool Prefix(M_Gamemode __instance, ref PcgRandom rng, M_Region lastRegion, M_Region regionToGenerate,
                M_Subregion startAfterSubregion, ref List<M_Level.LevelAssetHolder> __result)
            {
                if (__instance != EndlessConfiguration.EndlessMode) return true;
                List<M_Level.LevelAssetHolder> result = new List<M_Level.LevelAssetHolder>();
                List<M_Region> regions = (__instance.regions ?? new List<M_Region>()).Where(region => region != null).ToList();
                if (regionToGenerate != null)
                {
                    result.AddRange(regionToGenerate.GetLevels(ref rng, lastRegion, startAfterSubregion));
                    lastRegion = regionToGenerate;
                }
                int start = regions.IndexOf(lastRegion) + 1;
                if (start >= regions.Count) start = 0;
                int empty = 0;
                int limit = Mathf.Max(1, __instance.levelsToGenerate);
                for (int i = 0; regions.Count > 0 && result.Count < limit && empty < regions.Count && i < limit + regions.Count; i++)
                {
                    M_Region region = regions[(start + i) % regions.Count];
                    List<M_Level.LevelAssetHolder> levels = region.GetLevels(ref rng, lastRegion);
                    if (levels.Count == 0)
                    {
                        empty++;
                        continue;
                    }
                    empty = 0;
                    result.AddRange(levels);
                    lastRegion = region;
                }
                if (result.Count == 0) result = Fallback();
                if (result.Count > 0 && !result[result.Count - 1].pauseGeneration)
                    result[result.Count - 1].isPausingGeneration = false;
                __result = result;
                return false;
            }
        }

        [HarmonyPatch(typeof(M_GenerationBranch), nameof(M_GenerationBranch.GetGenerationList))]
        private static class BranchPatch
        {
            private static bool Prefix(M_GenerationBranch __instance, ref PcgRandom rng, M_Region lastRegion, M_Region regionToGenerate,
                M_Subregion startAfterSubregion, ref List<M_Level.LevelAssetHolder> __result)
            {
                if (CL_GameManager.GetCurrentGamemode() != EndlessConfiguration.EndlessMode) return true;
                List<M_Level.LevelAssetHolder> result = new List<M_Level.LevelAssetHolder>();
                M_Region current = null;
                if (__instance.generationType == M_GenerationBranch.GenerationType.levelPlaylist ||
                    __instance.generationType == M_GenerationBranch.GenerationType.singleLevel)
                {
                    result.AddRange((__instance.playlistLevelAssets ?? new List<M_Level.LevelAssetHolder>()).Where(Valid));
                    if (__instance.generationType == M_GenerationBranch.GenerationType.singleLevel && result.Count > 1)
                        result.RemoveRange(1, result.Count - 1);
                }
                else
                {
                    List<M_Region> regions = (__instance.regions ?? new List<M_Region>()).Where(region => region != null).ToList();
                    if (regionToGenerate != null)
                    {
                        result.AddRange(regionToGenerate.GetLevels(ref rng, lastRegion, startAfterSubregion));
                        current = regionToGenerate;
                        lastRegion = regionToGenerate;
                    }
                    int start = regions.IndexOf(lastRegion) + 1;
                    if (start >= regions.Count) start = 0;
                    int empty = 0;
                    int limit = Mathf.Max(1, __instance.levelsToGenerate);
                    for (int i = 0; regions.Count > 0 && result.Count < limit && empty < regions.Count && i < limit + regions.Count; i++)
                    {
                        M_Region region = regions[(start + i) % regions.Count];
                        List<M_Level.LevelAssetHolder> levels = region.GetLevels(ref rng, lastRegion);
                        if (levels.Count == 0)
                        {
                            empty++;
                            continue;
                        }
                        empty = 0;
                        result.AddRange(levels);
                        current = region;
                        if (__instance.generationType == M_GenerationBranch.GenerationType.singleRegion) break;
                        lastRegion = region;
                    }
                }
                if (result.Count == 0)
                {
                    __result = Fallback();
                    return false;
                }
                __instance.FinalizeLevelList(ref rng, ref result, lastRegion, current);
                __result = result;
                return false;
            }
        }
    }
}