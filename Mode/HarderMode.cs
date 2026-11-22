//using BepInEx;
//using HarmonyLib;
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using UnityEngine;

//public static class HarderMode
//{
//    public static bool Enabled =>
//        CL_GameManager.GetCurrentGamemode().HasActiveSetting("harder_mode");
//}

//[HarmonyPatch(typeof(UI_GamemodeScreen_Panel), nameof(UI_GamemodeScreen_Panel.FillSettings))]
//static class HarderModeButtonPatch
//{
//    static void Prefix(UI_GamemodeScreen_Panel __instance)
//    {
//        var mode = __instance.GetGamemode();

//        if (mode == null)
//            return;

//        if (mode.gamemodeSettings.Any(x => x.id == "harder_mode"))
//            return;

//        var template = mode.gamemodeSettings.FirstOrDefault();

//        if (template == null)
//            return;

//        var harderMode = UnityEngine.Object.Instantiate(template);

//        harderMode.id = "harder_mode";
//        harderMode.title = "Harder Mode";
//        harderMode.description = "LMAO (In Development)";
//        harderMode.color = Color.red;
//        harderMode.nameAppend = "";
//        harderMode.leaderboardAppendID = "";
//        harderMode.allowDownstream = false;
//        harderMode.downstreamId = "hardmode";

//        if (harderMode.exclusiveSettings == null)
//            harderMode.exclusiveSettings = new System.Collections.Generic.List<string>();
//        else
//            harderMode.exclusiveSettings = harderMode.exclusiveSettings.ToList();

//        mode.gamemodeSettings.Add(harderMode);
//    }
//}
//[HarmonyPatch(typeof(UI_GamemodeSetting), nameof(UI_GamemodeSetting.ToggleSetting))]
//static class HardModeDependencyPatch
//{
//    static void Postfix(UI_GamemodeSetting __instance, bool b)
//    {
//        if (__instance.gamemodeSetting.id != "hardmode")
//            return;

//        if (b)
//            return;

//        var mode = UI_GamemodeScreen.instance.GetGamemode();

//        StatManager.saveData.SetSetting(
//            mode.gamemodeName,
//            "harder_mode",
//            false);

//        SettingsManager.RefreshSettings("");
//        UI_GamemodeScreen.instance.RefreshCurrentGamemode();
//    }
//}

//[HarmonyPatch(typeof(UI_GamemodeSetting), "UpdateColor")]
//static class HarderModeLockPatch
//{
//    static void Postfix(UI_GamemodeSetting __instance)
//    {
//        if (__instance.gamemodeSetting.id != "harder_mode")
//            return;

//        var mode = UI_GamemodeScreen.instance.GetGamemode();

//        bool hardModeEnabled = mode.HasActiveSetting("hardmode");

//        if (!hardModeEnabled)
//        {
//            __instance.toggle.interactable = false;
//            __instance.descriptionText.text = "Requires Hard Mode";
//        }
//    }
//}


//[HarmonyPatch(typeof(CL_GameManager), "Start")]
//class GameManagerStartPatch
//{
//    static void Postfix(CL_GameManager __instance)
//    {
//        if (!CL_GameManager.GetCurrentGamemode().HasActiveSetting("harder_mode"))
//            return;

//        Debug.Log("Harder Mode enabled.");
//        GameObject managerObject = new GameObject("AnimatorRandomizerManager");
//        managerObject.AddComponent<AnimatorSpeedRandomizer.AnimatorRandomizer>();
//    }
//}


////Turret Patch

//[HarmonyPatch(typeof(DEN_Turret), nameof(DEN_Turret.Start))]
//static class HarderTurretsPatch
//{
//    static void Postfix(DEN_Turret __instance)
//    {
//        if (!HarderMode.Enabled)
//            return;

//        __instance.maxTurnAngle = 180f;
//        __instance.shootSpeed *= 0.04f;
//        __instance.damage *= 0.1f;
//        __instance.bulletSpread *= 0.4f;
//        __instance.maxRange *= 10f;
//    }
//}

////Bloodbug Patch

//[HarmonyPatch(typeof(DEN_BasicDenizen_Flier), nameof(DEN_BasicDenizen_Flier.Start))]
//static class HarderBloodbugsPatch
//{
//    static void Postfix(DEN_BasicDenizen_Flier __instance)
//    {
//        if (!HarderMode.Enabled)
//            return;
//        __instance.movementModule.SetSpeedMult(__instance.movementModule.GetSpeedMult()*5f);
//    }
//}

////heehhehehh



//namespace AnimatorSpeedRandomizer
//{

//    public class AnimatorRandomizer : MonoBehaviour
//    {
//        private readonly HashSet<int> _processedAnimators = new HashSet<int>();

//        private void Start()
//        {
//            StartCoroutine(ScanForAnimatorsRoutine());
//        }

//        private IEnumerator ScanForAnimatorsRoutine()
//        {
//            while (true)
//            {
//                if (HarderMode.Enabled)
//                {
//                    Animator[] allAnimators = FindObjectsOfType<Animator>();

//                    foreach (Animator anim in allAnimators)
//                    {
//                        if (anim == null) continue;

//                        int id = anim.GetInstanceID();

//                        if (_processedAnimators.Add(id))
//                        {
//                            float randomSpeedMultiplier = UnityEngine.Random.Range(0.1f, 2.0f) * UnityEngine.Random.Range(0.1f, 2.0f) * UnityEngine.Random.Range(0.1f, 2.0f);
//                            anim.speed *= randomSpeedMultiplier;
//                        }
//                    }
//                }

//                yield return new WaitForSeconds(1.0f);
//            }
//        }
//    }
//}
////Don't really know what this does

//[HarmonyPatch(typeof(UT_Platform), "Update")]
//static class HarderPlatformsPatch
//{
//    static void Postfix(UT_Platform __instance)
//    {
//        if (!HarderMode.Enabled)
//            return;
//        __instance.speedMult = 10f;
//    }
//}

//// Fans and stuff
//[HarmonyPatch(typeof(UT_Spin), "Update")]
//static class HarderSpinnersPatch
//{
//    static void Postfix(UT_Spin __instance)
//    {
//        if (!HarderMode.Enabled)
//            return;
//        if (UnityEngine.Random.Range(0.01f,1.01f) > 1.00f)
//        {
//            __instance.SetMultiplier(UnityEngine.Random.Range(-20f, 20f));
//        }

//    }
//}