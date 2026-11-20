using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Celestial.CustomEndless
{
    public static partial class EndlessConfiguration
    {
        private static readonly HashSet<GameObject> runtimeObjects = new HashSet<GameObject>();
        private static readonly HashSet<M_Region> generatedRegions = new HashSet<M_Region>();
        private static readonly HashSet<M_Subregion> generatedSubregions = new HashSet<M_Subregion>();

        public static void RegisterGeneratedRegion(M_Region region)
        {
            if (region != null)
                generatedRegions.Add(region);
        }

        public static void RegisterGeneratedSubregion(M_Subregion subregion)
        {
            if (subregion != null)
                generatedSubregions.Add(subregion);
        }
        [HarmonyPatch(typeof(CL_AssetManager), nameof(CL_AssetManager.GetRegionAsset))]
        public static class GeneratedRegionLookupPatch
        {
            private static void Postfix(string name, string database, ref M_Region __result)
            {
                if (string.IsNullOrEmpty(name) || !string.IsNullOrEmpty(database) ||
                    EndlessMode == null || CL_GameManager.GetCurrentGamemode() != EndlessMode) return;
                M_Region active = EndlessMode.regions?.FirstOrDefault(region => region != null && region.name == name);
                if (active != null)
                {
                    __result = active;
                    return;
                }
                if (__result == null)
                    __result = generatedRegions.FirstOrDefault(region => region != null && region.name == name);
            }
        }

        public static void RegisterRuntimeObject(GameObject instance, bool cleanup = true)
        {
            if (cleanup && instance != null)
                runtimeObjects.Add(instance);
        }

    }
}
