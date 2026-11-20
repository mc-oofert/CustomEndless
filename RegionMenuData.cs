using Celestial.CustomEndless;
using HarmonyLib;
using System;
using System.Collections.Generic;

public static class RegionFlavorText
{
    public static readonly Dictionary<string, string> ByRegion = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public static void Register(string region, string text)
    {
        if (string.IsNullOrWhiteSpace(region)) return;
        ByRegion[region] = text ?? string.Empty;
    }

    public static void RegisterDefaults()
    {
        Register("Silos", "");
        Register("Pipeworks", "");
        Register("Habitation", "");
        Register("Abyss", "");
        Register("Nest", "");
        Register("Duct", "");
        Register("Body Cavity", "");
        Register("Diver", "");
        Register("Ladder", "");
        Register("Chute", "");
        Register("Sink", "");
        Register("Parasite", "");
        Register("Chimney", "");
        Register("Omicron", "");
        Register("Campaign", "");
        Register("Checkpoint", "");
        Register("Pier", "");
        Register("Interlude", "");
        Register("Advanced Course", "");
        Register("Shattered Territory", "");
        Register("Roach Run", "");
        Register("Comms Array", "");
        Register("Boost Course", "");
        Register("Random Any", "");
        Register("Random Region", "");
        Register("Random Shorts", "");
        Register("Random Subregion", "");
        Register("Random Random", "");
        Register("Custom Region", "");
    }


    public static void Show(MenuSettingsManager menu, string region)
    {
        if (region.EndsWith("-BALANCED", StringComparison.OrdinalIgnoreCase)) region = region.Substring(0, region.Length - 9);
        if (ByRegion.TryGetValue(region, out string text) && !string.IsNullOrWhiteSpace(text)) CustomRegionEditor.AddInfo(menu, "Region Description", text);
    }
}

public static class RegionRunScores
{
    private static readonly HashSet<string> visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static string run;
    private static bool saved;
    private static bool medals;

    [HarmonyPatch(typeof(WorldLoader), nameof(WorldLoader.Initialize))]
    private static class StartPatch
    {
        private static void Postfix()
        {
            visited.Clear();
            run = Guid.NewGuid().ToString("N");
            saved = false;
            medals = !BalancedMode.FullCustom;
        }
    }

    [HarmonyPatch(typeof(M_Level), nameof(M_Level.OnEnter))]
    private static class EnterPatch
    {
        private static void Postfix(M_Level __instance)
        {
            if (CL_GameManager.GetCurrentGamemode() != EndlessConfiguration.EndlessMode) return;
            string region = EndlessConfiguration.GetLogicalRegionName(__instance);
            if (!string.IsNullOrEmpty(region)) visited.Add(region);
            if (BalancedMode.FullCustom && !Challenges.IsActive) medals = false;
        }
    }

    [HarmonyPatch(typeof(CL_GameManager), "EndGameStatUpdate")]
    private static class FinishPatch
    {
        private static void Postfix()
        {
            if (saved || run == null || CL_GameManager.GetCurrentGamemode() != EndlessConfiguration.EndlessMode) return;
            BalancedMode.UpdateHeight();
            double points = Convert.ToDouble(StatManager.sessionStats.GetStatistic("score").GetValue());
            string challenge = Challenges.RunName;
            bool eligible = medals && !BalancedMode.FullCustom;
            foreach (string region in visited)
            {
                List<string> modifiers = eligible ? BalancedMode.GetRunChallenges(region) : new List<string>();
                int medal = eligible ? BalancedMode.GetRunMedal(region) : 0;
                if (!string.IsNullOrEmpty(challenge))
                {
                    modifiers.Add(challenge);
                    if (ChallengeGoals.ByName.TryGetValue(challenge, out ChallengeGoals.HeightGoals goals))
                    {
                        float height = CL_GameManager.gMan.GetPlayerBestTravelDistance();
                        medal = height >= goals.Platinum ? 4 : height >= goals.Gold ? 3 : height >= goals.Silver ? 2 : height >= goals.Bronze ? 1 : 0;
                    }
                }
                ModSaveData.RecordScore(new ModSaveData.RunScore
                {
                    run = run,
                    region = region,
                    points = points,
                    completedUtc = DateTime.UtcNow.ToString("o"),
                    medalsEnabled = eligible || !string.IsNullOrEmpty(challenge),
                    medal = medal,
                    challenges = modifiers
                });
            }
            saved = true;
        }
    }
}