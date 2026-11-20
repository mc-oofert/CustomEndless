using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

namespace Celestial.CustomEndless
{
    public static partial class EndlessConfiguration
    {
        [HarmonyPatch(typeof(DEN_DeathFloor), "Start")]
        class DeathFloor_Start
        {
            static void Postfix(DEN_DeathFloor __instance)
            {
                if (CL_GameManager.GetCurrentGamemode() !=
                EndlessConfiguration.EndlessMode)
                    return;
                __instance.SetHeight(-50);
                EndlessConfiguration.ApplyConfiguredBuffs();

                __instance.SetSpeedMultiplier(
                __instance.GetCurrentSpeedMult() *
                EndlessConfiguration.CustomUIManager.GetFloatState("mass_speed_mult"));

                __instance.SetSpeed(__instance.GetCurrentSpeed() *
                EndlessConfiguration.CustomUIManager.GetFloatState("mass_start_speed"));

                __instance.SetSpeedIncreaseMultiplier(
                EndlessConfiguration.CustomUIManager.GetFloatState("mass_accel_mult"));

                __instance.rubberbandMult =
                EndlessConfiguration.CustomUIManager.GetFloatState("mass_rubberband_mult");

                __instance.SetRubberbandActive(
                EndlessConfiguration.CustomUIManager.GetToggleState("mass_rubberband_enabled"));
            }
        }

        [HarmonyPatch(typeof(CH_RoachCollector), "Start")]
        public static class CH_RoachCollector_Start_Patch
        {
            [HarmonyPrefix]
            public static void Prefix(CH_RoachCollector __instance, ref int ___currentRoaches, ref int ___bankTarget)
            {
                ___currentRoaches = CL_GameManager.GetRoaches(false);
                ___bankTarget += cycles * 5;
                collector = __instance;
            }
        }

        [HarmonyPatch(typeof(CL_GameManager), "AddRoaches")]
        private static class AddRoachesPatch
        {
            [HarmonyPostfix]
            public static void Postfix(int i)
            {
                if (CL_GameManager.GetRoaches() == 999 && collector != null)
                {
                    var field = typeof(CH_RoachCollector).GetField("currentRoaches", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    int value = field.GetValue(collector).ConvertTo<int>();
                    field.SetValue(collector, value - i);
                }
                return;
            }
        }



        [HarmonyPatch(typeof(DEN_DeathFloor))]
        public static class DeathFloorHeightPatch
        {
            public static bool haldHeightUpdates = true;
            private static float? pendingTargetHeight;

            public static bool HaltHeightUpdates
            {
                get => haldHeightUpdates;
                set
                {
                    if (haldHeightUpdates == value) return;
                    haldHeightUpdates = value;
                    if (!haldHeightUpdates) ApplyPendingTargetHeight();
                }
            }

            [HarmonyPatch("Update")]
            [HarmonyPrefix]
            private static void UpdatePrefix(DEN_DeathFloor __instance, out float __state)
            {
                __state = __instance.transform.position.y;
            }

            [HarmonyPatch("Update")]
            [HarmonyPostfix]
            private static void UpdatePostfix(DEN_DeathFloor __instance, float __state)
            {
                if (!haldHeightUpdates) return;

                Vector3 position = __instance.transform.position;
                position.y = __state;
                __instance.transform.position = position;

                if (__instance.setCorruptionHeight && FXManager.fxMan != null)
                {
                    FXManager.fxMan.corruptionHeight = __state;
                }
            }

            [HarmonyPatch("LateUpdate")]
            [HarmonyPrefix]
            private static void LateUpdatePrefix(DEN_DeathFloor __instance, out float __state)
            {
                __state = __instance.transform.position.y;
            }

            [HarmonyPatch("LateUpdate")]
            [HarmonyPostfix]
            private static void LateUpdatePostfix(DEN_DeathFloor __instance, float __state)
            {
                if (!haldHeightUpdates) return;

                Vector3 position = __instance.transform.position;
                position.y = __state;
                __instance.transform.position = position;
            }

            [HarmonyPatch(nameof(DEN_DeathFloor.SetHeight), new[] { typeof(float) })]
            [HarmonyPrefix]
            private static bool SetHeightFloatPrefix(float h)
            {
                if (!haldHeightUpdates) return true;
                pendingTargetHeight = h;
                return false;
            }

            [HarmonyPatch(nameof(DEN_DeathFloor.SetHeight), new[] { typeof(string[]) })]
            [HarmonyPrefix]
            private static bool SetHeightArgsPrefix(string[] args)
            {
                if (!haldHeightUpdates) return true;
                if (ENT_Player.playerObject != null)
                {
                    float offset = -10f;
                    if (args != null && args.Length > 0 && (!float.TryParse(args[0], out offset) || float.IsNaN(offset) || float.IsInfinity(offset))) return false;
                    pendingTargetHeight = ENT_Player.playerObject.transform.position.y + offset;
                }
                return false;
            }

            [HarmonyPatch(nameof(DEN_DeathFloor.SetHeightRelativeToTransform))]
            [HarmonyPrefix]
            private static bool SetHeightRelativeToTransformPrefix(float h, Transform t)
            {
                if (!haldHeightUpdates) return true;
                if (t != null) pendingTargetHeight = t.position.y + h;
                return false;
            }

            private static void ApplyPendingTargetHeight()
            {
                if (!pendingTargetHeight.HasValue || DEN_DeathFloor.instance == null) return;

                Vector3 position = DEN_DeathFloor.instance.transform.position;
                position.y = pendingTargetHeight.Value;
                DEN_DeathFloor.instance.transform.position = position;
                pendingTargetHeight = null;

                if (DEN_DeathFloor.instance.setCorruptionHeight && FXManager.fxMan != null)
                {
                    FXManager.fxMan.corruptionHeight = position.y;
                }
            }
        }

        [HarmonyPatch(typeof(M_Level), "OnEnter")]
        public static class M_Level_OnLevelEnter_Patch
        {
            [HarmonyPostfix]
            public static void Postfix(M_Level __instance)
            {
                if (__instance == null) return;
                if (CL_UIManager.instance != null) CL_UIManager.instance.SetVignette("fade", 0f);
                ENT_Player player = ENT_Player.GetPlayer();
                if (player != null)
                {
                    player.SetCutsceneMode(false);
                    player.SetSpeedMult(1f);
                }
                if (!(last_entered2 == null) && __instance.GetInstanceID() == last_entered2.GetInstanceID()) return;
                last_entered2 = __instance;
                if (__instance.name.ContainsInsensitive("Prototype"))
                {
                    //var generator = new BoostProcGen();
                    Debug.Log(__instance.name);
                    ProcGen.Generate(__instance);
                    Item_Object wishbone = WishboneStuff.SpawnWishbone(__instance.transform.position);
                    if (wishbone != null) wishbone.transform.SetParent(__instance.transform);
                }
            }
        }

        [HarmonyPatch(typeof(M_Level), "OnLevelActivate")]
        public static class M_Level_OnLevelActivate_Items_Patch
        {
            [HarmonyPostfix]
            public static void Postfix(M_Level __instance)
            {
                if (__instance == null) return;

                int instanceId = __instance.GetInstanceID();
                if (last_entered3.Contains(instanceId)) return;

                last_entered3.Add(instanceId);
                if (last_entered3.Count > 5)
                {
                    last_entered3.RemoveAt(0);
                }
                ConfigureUniversalBreakroom(__instance);
                RemoveComputersFromAbyssBreakroom(__instance);
                RemoveUpgradeConsoleComputerIds(__instance.gameObject);

                if (EndlessConfiguration.giveStartingItems)
                {
                    cycles = 0;
                    gaveFlashlight = false;
                    gaveGun = false;
                    EndlessConfiguration.GiveStartingItems();
                    EndlessConfiguration.GiveStartingPerks();
                    EndlessConfiguration.giveStartingItems = false;
                }
                if (__instance.region != null && __instance.region.regionName != null && __instance.region.regionName.ContainsInsensitive("Checkpoint"))
                {
                    ReplaceUpgradeConsoles(__instance.gameObject);
                }


            }

            private static void RemoveUpgradeConsoleComputerIds(GameObject root)
            {
                if (root == null)
                    return;

                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                {
                    if (!transform.name.ContainsInsensitive("UpgradeConsole") &&
                        !transform.name.ContainsInsensitive("Prop_Upgrade"))
                        continue;

                    foreach (OS_Computer_Interface computer in transform.GetComponentsInChildren<OS_Computer_Interface>(true))
                        computer.computerID = string.Empty;
                }
            }

            public static void ReplaceUpgradeConsoles(GameObject root)
            {
                if (root == null) return;
                if (CustomUIManager.GetToggleState("checkpoint_save"))
                {

                    List<GameObject> targets = new List<GameObject>();
                    FindTargetsRecursively(root.transform, targets, "Prop_VendingMachine.01");
                    Debug.Log(targets);

                    if (targets.Count == 0) return;

                    M_Level prefabLevel = GetCachedObject<M_Level>("Campaign_Interlude_Habitation_To_Abyss_01");
                    GameObject prefab = prefabLevel != null ? prefabLevel.gameObject : null;
                    Debug.Log(prefab);
                    if (prefab == null) return;

                    GameObject replacementSource = FindChildByName(prefab.transform, "Prop_UpgradeConsole_ScreenAndDisk Variant");
                    Debug.Log(replacementSource);
                    if (replacementSource == null) return;

                    foreach (GameObject target in targets)
                    {
                        Transform targetTransform = target.transform;
                        Transform parent = targetTransform.parent;
                        GameObject newObject = UnityEngine.Object.Instantiate(replacementSource, parent);

                        Vector3 localPosition = targetTransform.localPosition;
                        Quaternion localRotation = targetTransform.rotation;
                        Vector3 localScale = targetTransform.localScale;
                        int siblingIndex = targetTransform.GetSiblingIndex();

                        localRotation.y = targetTransform.rotation.y;

                        newObject.name = replacementSource.name;
                        newObject.transform.localPosition = localPosition;
                        newObject.transform.rotation = localRotation;
                        newObject.transform.Rotate(new Vector3(1, 0, 0), -90);
                        newObject.transform.localScale = localScale;
                        newObject.transform.SetSiblingIndex(siblingIndex);

                        UnityEngine.Object.Destroy(target);
                    }
                }
                if (CustomUIManager.GetToggleState("checkpoint_experimental"))
                {

                    List<GameObject> targets = new List<GameObject>();
                    FindTargetsRecursively(root.transform, targets, "Prop_VendingMachine_T2");
                    Debug.Log(targets);

                    if (targets.Count == 0) return;

                    M_Level prefabLevel = GetCachedObject<M_Level>("M1_Silos_SafeArea_01");
                    GameObject prefab = prefabLevel != null ? prefabLevel.gameObject : null;
                    Debug.Log(prefab);
                    if (prefab == null) return;

                    GameObject replacementSource = FindChildByName(prefab.transform, "UpgradeConsole");
                    Debug.Log(replacementSource);
                    if (replacementSource == null) return;

                    foreach (GameObject target in targets)
                    {
                        Transform targetTransform = target.transform;
                        Transform parent = targetTransform.parent;
                        GameObject newObject = UnityEngine.Object.Instantiate(replacementSource, parent);

                        Vector3 localPosition = targetTransform.position;
                        Quaternion localRotation = targetTransform.rotation;
                        Vector3 localScale = targetTransform.localScale;
                        int siblingIndex = targetTransform.GetSiblingIndex();

                        localRotation.y = targetTransform.rotation.y;

                        newObject.name = replacementSource.name;
                        newObject.transform.position = localPosition;
                        newObject.transform.rotation = localRotation;
                        newObject.transform.Rotate(new Vector3(1, 0, 0), -90);
                        newObject.transform.localScale = localScale;
                        newObject.transform.SetSiblingIndex(siblingIndex);
                        newObject.SetActive(true);
                        OS_Computer_Interface computer = newObject.GetComponentInChildren<OS_Computer_Interface>();
                        if (computer != null) computer.computerID = "";

                        UnityEngine.Object.Destroy(target);
                    }
                }
            }

            public static void FindTargetsRecursively(Transform current, List<GameObject> targets, String targeter = "Prop_UpgradeConsole_Main_Endless")
            {
                foreach (Transform child in current)
                {
                    if (child.name.Contains(targeter))
                    {
                        targets.Add(child.gameObject);
                    }
                    FindTargetsRecursively(child, targets, targeter);
                }
            }

            public static GameObject FindChildByName(Transform parent, string searchName, bool strict = true)
            {
                if (strict)
                {
                    if (parent.name == searchName) return parent.gameObject;
                }
                else if (parent.name.ContainsInsensitive(searchName))
                {
                    return parent.gameObject;
                }

                foreach (Transform child in parent)
                {
                    GameObject result = FindChildByName(child, searchName);
                    if (result != null)
                    {
                        return result;
                    }
                }
                foreach (Transform child in parent)
                {
                    GameObject result = FindChildByName(child, searchName, false);
                    if (result != null)
                    {
                        return result;
                    }
                }

                return null;
            }
        }
        [HarmonyPatch(typeof(CL_Initializer), "Awake")]
        [HarmonyPrefix]
        public static void InitializerAwake(ref CL_Initializer __instance)
        {
            if (!added)
            {
                if (__instance.baseDatabase == null || __instance.baseDatabase.gamemodeAssets == null) return;
                M_Gamemode vanillaTemplate =
                __instance.baseDatabase.gamemodeAssets.FirstOrDefault(
                x => x != null && x.name == "GM_Endless_Silos");
                if (vanillaTemplate == null)
                {
                    Debug.LogWarning("Custom Endless could not find its base gamemode.");
                    return;
                }

                foreach (M_Gamemode gm in __instance.baseDatabase.gamemodeAssets)
                {
                    if (gm != null && gm.regions != null)
                    {
                        foreach (var reg in gm.regions)
                        {
                            if (reg != null && !regionCache.ContainsKey(reg.name))
                            {
                                regionCache[reg.name] = reg;
                            }
                        }
                    }
                }

                EndlessMode = ScriptableObject.CreateInstance<M_Gamemode>();

                EndlessMode.gamemodeName = "Custom Endless";
                EndlessMode.name = "GM_Custom_Endless";
                EndlessMode.introText = "ASCEND";
                EndlessMode.newGameText = "Start";
                EndlessMode.modeDescription = "Customize your Endless";

                EndlessMode.allowAchievements = false;
                EndlessMode.allowCheatedScores = false;
                EndlessMode.allowCheats = true;
                EndlessMode.allowLeaderboardScoring = false;
                EndlessMode.steamLeaderboardName = "";
                EndlessMode.allowHeightAchievements = true;
                EndlessMode.baseGamemode = false;
                EndlessMode.useGamemodeSettings = true;
                EndlessMode.isEndless = true;
                EndlessMode.hasPerks = vanillaTemplate.hasPerks;
                EndlessMode.hasRevives = vanillaTemplate.hasRevives;
                EndlessMode.modeType = vanillaTemplate.modeType;

                EndlessMode.gamemodeScene = vanillaTemplate.gamemodeScene;
                EndlessMode.capsuleName = "Custom Endless";
                EndlessMode.capsuleArt = LoadSprite("customEndlessCapsule.png");
                EndlessMode.screenArt = vanillaTemplate.screenArt;
                EndlessMode.roachEndSprite = vanillaTemplate.roachEndSprite;
                EndlessMode.gamemodePanel = vanillaTemplate.gamemodePanel;
                EndlessMode.roachBankID = vanillaTemplate.roachBankID;
                EndlessMode.loseScreen = vanillaTemplate.loseScreen;
                EndlessMode.winScreen = vanillaTemplate.winScreen;
                EndlessMode.gamemodeColor = vanillaTemplate.gamemodeColor;
                EndlessMode.modeTags = vanillaTemplate.modeTags;
                EndlessMode.unlockAchievement = vanillaTemplate.unlockAchievement;
                EndlessMode.gamemodeModule = vanillaTemplate.gamemodeModule;
                EndlessMode.levelsToGenerate = vanillaTemplate.levelsToGenerate;
                EndlessMode.availableTrinkets = vanillaTemplate.availableTrinkets;
                EndlessMode.playlistLevelAssets = new List<M_Level.LevelAssetHolder>();
                EndlessMode.playlistLevels = new List<M_Level>();
                EndlessMode.gamemodeObjects = vanillaTemplate.gamemodeObjects;
                EndlessMode.unlockHint = vanillaTemplate.unlockHint;
                EndlessMode.startItems = vanillaTemplate.startItems;
                EndlessMode.regions = new List<M_Region>();

                EndlessMode.regions.Clear();
                EndlessMode.gamemodeSettings = vanillaTemplate.gamemodeSettings;
                EndlessMode.winScreen.tipText.text = "YOU BEAT YOUR ARBITRARY, LIKELY SELF-IMPOSED, GOAL";

                __instance.baseDatabase.gamemodeAssets.Add(EndlessMode);
                added = true;
            }
        }
        [HarmonyPatch(typeof(UI_PlayPane), "Start")]
        [HarmonyPrefix]
        public static void PlayPaneStart(ref UI_PlayPane __instance)
        {
            if (EndlessMode == null) return;
            GameObject gameObject =
            UnityEngine.Object.FindObjectsOfType<GameObject>(true).FirstOrDefault(
            x => (x.name == "Mode Selection Button - Endless Superstructure" ||
            x.name == "Other Endless 01") &&
             x.transform.parent != null && x.transform.parent.parent != null && x.transform.parent.parent.parent != null && x.transform.parent.parent.parent.name ==
             "Play Pane - Scroll View Tab - Endless Variant");
            if (gameObject != null)
            {
                if (gameObject.transform.parent.Find("Custom Endless Tab") != null)
                    return;

                GameObject gameObject2 =
                UnityEngine.Object.Instantiate<GameObject>(gameObject);
                gameObject2.name = "Custom Endless Tab";
                gameObject2.transform.SetParent(gameObject.transform.parent, false);
                gameObject2.transform.SetSiblingIndex(1);
                Transform child = gameObject2.transform.GetComponentInChildren<UI_Gamemode_Button>().gameObject.transform;

                for (int i = 0; i < gameObject2.transform.childCount; i++)
                {
                    if (gameObject2.transform.GetChild(i) != gameObject2.transform.GetComponentInChildren<TMP_Text>().transform)
                    {
                        UnityEngine.Object.Destroy(
                        gameObject2.transform.GetChild(i).gameObject);
                    }
                }

                GameObject gameObject3 = child.gameObject;
                gameObject3.name = "Mode Selection Button - Custom Endless";
                gameObject3.GetComponent<Button>().onClick.RemoveAllListeners();

                gameObject3.GetComponent<UI_CapsuleButton>().unlockAchievement =
                EndlessMode.unlockAchievement;
                gameObject3.GetComponent<UI_Gamemode_Button>().gamemode = EndlessMode;
                gameObject3.GetComponent<UI_Gamemode_Button>().Initialize();
                gameObject2.transform.localScale = Vector3.one;

                ContentSizeFitter csf =
                gameObject2.transform.parent.GetComponent<ContentSizeFitter>();
                UI_TabGroup component =
                gameObject2.transform.parent.parent.parent.parent.parent
                .Find("Tabs")
                .GetComponent<UI_TabGroup>();
                component.EventOnChangeTab.AddListener(
                delegate () { csf.Invoke("SetLayoutHorizontal", 0.05f); });
            }
        }
    }
}
