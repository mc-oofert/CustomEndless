using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace Celestial.CustomEndless
{
    [BepInPlugin("com.severon.customendless", "CustomEndless", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static bool DumbItDown = true;
        internal static ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;

            ModSaveData.Initialize();
            Harmony harmony = new Harmony("com.severon.customendless");
            harmony.PatchAll();
            harmony.PatchAll(typeof(EndlessConfiguration));
            Logger.LogInfo("CustomEndless loaded.");

        }
    }

    [HarmonyPatch(typeof(M_Gamemode), "Initialize")]
    public static class M_Gamemode_Initialize_Patch
    {
        private static void Postfix(M_Gamemode __instance)
        {
            __instance.allowLeaderboardScoring = false;
            __instance.allowCheatedScores = false;
            __instance.allowAchievements = false;
            if (__instance == EndlessConfiguration.EndlessMode)
            {
                EndlessConfiguration.UpdateRegions();
            }
        }
    }

}
