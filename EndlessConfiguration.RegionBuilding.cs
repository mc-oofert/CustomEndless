using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

namespace Celestial.CustomEndless
{
    public static partial class EndlessConfiguration
    {
        private static readonly HashSet<M_Region> firstLoopOnlyDiverRegions = new HashSet<M_Region>();

        [HarmonyPatch(typeof(M_Region), nameof(M_Region.GetLevels))]
        private static class FirstLoopDiverEntrancePatch
        {
            private static readonly HashSet<M_Region> generated = new HashSet<M_Region>();
            private static bool Prefix(M_Region __instance, M_Region lastRegion, ref List<M_Level.LevelAssetHolder> __result)
            {
                if (lastRegion == null)
                {
                    generated.Clear();
                    if (firstLoopOnlyDiverRegions.Contains(__instance))
                        generated.Add(__instance);
                    return true;
                }
                if (lastRegion == null || !firstLoopOnlyDiverRegions.Contains(__instance))
                    return true;
                if (generated.Add(__instance))
                    return true;
                __result = new List<M_Level.LevelAssetHolder>();
                return false;
            }
        }
        public static void UpdateRegions()
        {
            RegisterRegions();
            UpdateOrderedRegions();
        }

        private static M_Level CreateUniversalBreakroomLevel(string regionKey, int slotIndex, string breakroomRegion)
        {
            M_Level source = GetRegionBreakroomSource(breakroomRegion);
            if (source == null)
                return null;

            M_Level level = UnityEngine.Object.Instantiate(source);
            level.name = $"Universal_Breakroom_{regionKey}_{universalBreakroomIndex++}";
            level.gameObject.name = level.name;
            level.loadAdjacent = true;
            ConfigureBreakroomDecorations(level, breakroomRegion);
            CustomLevelLoader.PrepareRoom(level);
            RegisterRuntimeObject(level.gameObject, false);
            level.gameObject.SetActive(false);

            UniversalBreakroomSettings settings = level.GetComponent<UniversalBreakroomSettings>();
            if (settings == null)
                settings = level.gameObject.AddComponent<UniversalBreakroomSettings>();

            settings.regionKey = regionKey;
            settings.slotIndex = slotIndex;
            settings.breakroomNumber = ++universalBreakroomVisit;
            settings.configured = false;
            return level;
        }

        private static void ConfigureUniversalBreakroom(M_Level level)
        {
            if (level == null)
                return;

            UniversalBreakroomSettings settings = level.GetComponent<UniversalBreakroomSettings>();
            if (settings == null)
                return;

            if (settings.configured)
                return;

            settings.configured = true;

            RemoveUniversalBreakroomMachines(level.transform);
            bool hasAuthoredMachines = ConfigureAuthoredBreakroomMachines(level, settings);

            int breakroomNumber = settings.breakroomNumber;
            for (int i = 0; i < BreakroomEntities.Length; i++)
            {
                BreakroomEntityDefinition entity = BreakroomEntities[i];
                int every = Mathf.Max(0, GetCountStateForSlot(entity.EveryKey, settings.regionKey, settings.slotIndex));
                int after = Mathf.Max(0, GetCountStateForSlot(entity.AfterKey, settings.regionKey, settings.slotIndex));
                if (!ShouldSpawnBreakroomEntity(breakroomNumber, every, after))
                    continue;

                if (!hasAuthoredMachines)
                    AddUniversalBreakroomEntity(level, GetBreakroomEntitySource(entity), entity, i);
            }
        }
        private static bool ShouldSpawnBreakroomEntity(int breakroomNumber, int every, int after)
        {
            if (every <= 0)
                return false;

            int firstBreakroom = after > 0 ? after : 1;
            return breakroomNumber >= firstBreakroom &&
                   (breakroomNumber - firstBreakroom) % every == 0;
        }

        private static void AddUniversalBreakroomEntity(
            M_Level level,
            GameObject source,
            BreakroomEntityDefinition entity,
            int entityIndex)
        {
            if (source == null)
                return;

            GameObject machine = UnityEngine.Object.Instantiate(source, level.transform);
            Vector3 forward = level.levelEntrance.forward;

            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 center = level.levelEntrance.position + forward * 5f;
            Vector3[] locations =
            {
                right * -9.5f + forward * -4f,
                right * 9.5f + forward * -4f,
                right * -9.5f + forward * -2f,
                right * 9.5f + forward * -2f,
                right * -9.5f,
                right * 9.5f,
                right * -9.5f + forward * 2f,
                right * 9.5f + forward * 2f,
                right * -9.5f + forward * 4f
            };
            Vector3 wallOffset = locations[entityIndex % locations.Length];
            float verticalDrop = entity.SearchName.ContainsInsensitive("ATM") || entity.SearchName.ContainsInsensitive("Hatch") || entity.SearchName.ContainsInsensitive("Rho") ? 8f : 10f;
            Vector3 position = center + wallOffset + Vector3.down * verticalDrop;
            Vector3 wallFacing = Vector3.Dot(wallOffset, right) < 0f ? -right : right;
            if (entity.SearchName.ContainsInsensitive("Vend") || entity.SearchName.ContainsInsensitive("Hatch")) wallFacing = -wallFacing;

            machine.name = "UniversalBreakroom" + entity.Name;
            machine.transform.position = position;
            machine.transform.rotation = entity.UsesForwardVector
                ? Quaternion.LookRotation(wallFacing, Vector3.up)
                : Quaternion.LookRotation(Vector3.up, wallFacing);
            machine.SetActive(true);

            foreach (OS_Computer_Interface computer in machine.GetComponentsInChildren<OS_Computer_Interface>(true))
                computer.computerID = string.Empty;

            if (entity.RemoveSpawnChance)
            {
                foreach (UT_SpawnChance spawnChance in machine.GetComponentsInChildren<UT_SpawnChance>(true))
                    UnityEngine.Object.Destroy(spawnChance);
            }
        }

        private static GameObject GetPerkMachineSource()
        {
            M_Level sourceLevel = GetCachedObject<M_Level>("M1_Silos_SafeArea_Endless_01");
            GameObject source = sourceLevel != null
                ? FindChildContaining(sourceLevel.transform, "Prop_UpgradeConsole_Main_Endless")
                : null;

            if (source != null)
                return source;

            M_Region sourceRegion = GetCachedObject<M_Region>("Region_Silos_Saferoom_Endless");
            if (sourceRegion == null || sourceRegion.subregionGroups == null)
                return null;

            foreach (M_Region.SubregionGroup group in sourceRegion.subregionGroups)
            {
                if (group == null || group.subregions == null)
                    continue;

                foreach (M_Subregion subregion in group.subregions)
                {
                    if (subregion == null || subregion.levelGroups == null)
                        continue;

                    foreach (M_Subregion.LevelGroup levelGroup in subregion.levelGroups)
                    {
                        if (levelGroup == null || levelGroup.levelReferences == null)
                            continue;

                        foreach (M_Level.LevelAssetHolder holder in levelGroup.levelReferences)
                        {
                            M_Level level = holder != null ? holder.GetLevel() : null;
                            source = level != null
                                ? FindChildContaining(level.transform, "Prop_UpgradeConsole_Main_Endless")
                                : null;
                            if (source != null)
                                return source;
                        }
                    }
                }
            }

            return null;
        }

        private static GameObject GetExperimentalMachineSource()
        {
            M_Level sourceLevel = GetCachedObject<M_Level>("M1_Silos_SafeArea_01");
            return sourceLevel != null
                ? FindChildContaining(sourceLevel.transform, "UpgradeConsole")
                : null;
        }

        private static GameObject GetBreakroomEntitySource(BreakroomEntityDefinition entity)
        {
            string sourceLevelName = entity.Name == "ExperimentalMachine" ? "M1_Silos_SafeArea_01" : "M1_Silos_SafeArea_Endless_01";
            M_Level sourceLevel = GetCachedObject<M_Level>(sourceLevelName);
            return sourceLevel != null
                ? FindChildContaining(sourceLevel.transform, entity.SearchName)
                : null;
        }

        private static GameObject FindChildContaining(Transform root, string value)
        {
            if (root == null)
                return null;

            if (root.name.ContainsInsensitive(value))
                return root.gameObject;

            foreach (Transform child in root)
            {
                GameObject result = FindChildContaining(child, value);
                if (result != null)
                    return result;
            }

            return null;
        }

        private static void RemoveUniversalBreakroomMachines(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (child.name.StartsWith("UniversalBreakroom", StringComparison.Ordinal))
                    UnityEngine.Object.Destroy(child.gameObject);
                else
                    RemoveUniversalBreakroomMachines(child);
            }
        }

        private static void RemoveComputersFromAbyssBreakroom(M_Level level)
        {
            if (UseDefaultTransitionsAndBreakrooms || level == null || level.region == null)
                return;

            string regionName = level.region.name + " " + level.region.regionName;
            if (!regionName.ContainsInsensitive("Abyss_Breakroom"))
                return;

            RemoveChildrenContaining(level.transform, "Upgrade");
        }

        private static void RemoveChildrenContaining(Transform parent, string value)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (child.name.ContainsInsensitive(value))
                    UnityEngine.Object.Destroy(child.gameObject);
                else
                    RemoveChildrenContaining(child, value);
            }
        }

        private static M_Region AppendRegionToList(M_Region reggie, int slot = -1)
        {
            M_Region Reggie = GetAndFilterRegion(CleanRegionName(reggie.name), slot);
            if (Reggie != null && Reggie.name.ContainsInsensitive("pier"))
            {
                AddRegionIfValid(BuildRegionFromLevelName("Shaft To Pier"));
                AddRegionIfValid(BuildRegionFromLevelName("Pier Intro"));
            }
            if (Reggie != null && Reggie.name.ContainsInsensitive("chute"))
            {
                AddRegionIfValid(BuildRegionFromLevelName("From_Spawn"));
                AddRegionIfValid(BuildRegionFromLevelName("To_Abyss"));
            }

            AddRegionIfValid(Reggie);

            if (Reggie != null && Reggie.name.ContainsInsensitive("chute"))
            {
                AddRegionIfValid(BuildRegionFromLevelName("From_Silos"));
                AddRegionIfValid(BuildRegionFromLevelName("To_Anywhere"));
            }
            if (Reggie != null && Reggie.name.ContainsInsensitive("interlude"))
            {
                AddRegionIfValid(GetAndFilterRegion("Habitation Intro"));
            }
            if (Reggie != null && Reggie.name.ContainsInsensitive("sink"))
            {
                AddRegionIfValid(BuildRegionFromLevelName("Campaign_Interlude_Sink_To_Pipeworks_01"));
            }
            if (Reggie != null && Reggie.name.ContainsInsensitive("pier"))
            {
                AddRegionIfValid(GetAndFilterRegion("Lobby"));
                AddRegionIfValid(BuildRegionFromLevelName("From_Habitation"));
                AddRegionIfValid(BuildRegionFromLevelName("To_Anywhere"));
            }
            return Reggie;
        }

        private static void AddRegionIfValid(M_Region region)
        {
            if (region != null)
                EndlessMode.regions.Add(region);
        }

        private static M_Gamemode GetCampaignGamemode()
        {
            var gamemodeAssets = CL_AssetManager.GetBaseAssetDatabase()?.gamemodeAssets;
            if (gamemodeAssets == null)
                return null;

            return gamemodeAssets.FirstOrDefault(gamemode =>
                gamemode != null &&
                ((gamemode.name != null && gamemode.name.ContainsInsensitive("campaign")) ||
                 (gamemode.gamemodeName != null && gamemode.gamemodeName.ContainsInsensitive("campaign"))));
        }

        private static string GetCampaignRegionAssetName()
        {
            M_Gamemode campaignGamemode = GetCampaignGamemode();
            return campaignGamemode?.regions?.FirstOrDefault(region => region != null)?.name;
        }

        private static void AppendCampaignRegions(int slot, int loops)
        {
            M_Gamemode campaignGamemode = GetCampaignGamemode();
            if (campaignGamemode?.regions == null)
                return;

            bool forceStartLevel = true;
            for (int loop = 0; loop < loops; loop++)
            {
                foreach (M_Region campaignRegion in campaignGamemode.regions)
                {
                    if (campaignRegion == null)
                        continue;

                    M_Region runtimeRegion = FilterCampaign(campaignRegion.name, slot);
                    if (runtimeRegion == null)
                        continue;

                    if (forceStartLevel)
                    {
                        runtimeRegion.forceStartLevel = true;
                        forceStartLevel = false;
                    }

                    EndlessMode.regions.Add(runtimeRegion);
                }
            }
        }

        public static void UpdateOrderedRegions()
        {
            ResetLoopTracking();
            RegisterRegions();

            if (EndlessMode == null || EndlessMode.regions == null)
                return;
            EndlessMode.regions.Clear();
            firstLoopOnlyDiverRegions.Clear();
            CampaignGenerationRegions.Clear();
            campaignBranchRegions.Clear();
            universalBreakroomVisit = 0;
            universalBreakroomIndex = 0;

            List<(int slotIndex, string selection, string logicalName, string assetName, int loops, bool breakRoom)> activeSlots = new List<(int, string, string, string, int, bool)>();
            BalancedMode.BeginBuild();
            giveStartingItems = true;
            for (int i = 0; i < 50; i++)
            {
                string selection = CustomUIManager.GetStringState($"slot_{i}_contents");
                int loops = CustomUIManager.GetCountState($"slot_{i}_loops");

                if (string.IsNullOrEmpty(selection) || selection == "None" || loops <= 0)
                    continue;

                List<SubTabContainer.ResolvedSlot> resolvedSlots = SubTabContainer.ExpandSlot(i, selection);
                for (int resolvedIndex = 0; resolvedIndex < resolvedSlots.Count; resolvedIndex++)
                {
                    SubTabContainer.ResolvedSlot resolved = resolvedSlots[resolvedIndex];
                    string resolvedSelection = resolved.selection;
                    string logicalName = resolvedSelection;
                    string assetName = "";
                    bool generatesBreakRoom = true;

                    if (resolvedSelection == "Silos Breakroom")
                    {
                        logicalName = "Silos";
                        assetName = RegionDefinitions["Silos"].BreakroomAsset;
                        generatesBreakRoom = false;
                    }
                    else if (resolvedSelection == "Silos Campaign Breakroom")
                    {
                        logicalName = "Silos";
                        assetName = "Region_Silos_Saferoom";
                        generatesBreakRoom = false;
                    }
                    else if (RegionDefinitions.ContainsKey(resolvedSelection))
                    {
                        logicalName = resolvedSelection;
                        assetName = RegionDefinitions[resolvedSelection].RegionAsset;
                    }
                    else if (resolvedSelection == "Campaign")
                    {
                        logicalName = "Campaign";
                        generatesBreakRoom = false;
                    }
                    else
                    {
                        continue;
                    }

                    if (!BalancedMode.CanUse(logicalName)) continue;
                    if (resolved.hasBreakroomOverride) generatesBreakRoom = resolved.generateBreakroom;
                    BalancedMode.NotifyResolved(logicalName, selection);
                    BalancedMode.ApplySlot(logicalName, i);
                    activeSlots.Add((i, resolvedSelection, logicalName, assetName, loops, generatesBreakRoom));
                }
            }

            if (activeSlots.Count == 0)
            {
                var fallback = GetAndFilterRegion(RegionDefinitions["Silos"].BreakroomAsset, -1);
                if (fallback != null)
                    EndlessMode.regions.Add(fallback);
                return;
            }

            BEGONE_FOUL_SPIRIT = activeSlots[0].logicalName == "Abyss";
            giveStartingItems = true;

            if (CustomRegions.IsCustom(activeSlots[0].logicalName))
            {
                string entrance = GetCustomTransitionIdentity(activeSlots[0].logicalName, true);
                string suffix = entrance == "Body Cavity" ? "Cavity" : entrance == "Campaign" ? "Comms Array" : entrance;
                string entranceLevel = "To_" + suffix;
                if (GetTransitionLevel(entranceLevel) == null) entranceLevel = "To_Anywhere";
                M_Region spawnRegion = BuildRegionFromLevelName("From_Spawn");
                M_Region entranceRegion = BuildRegionFromLevelName(entranceLevel);
                if (spawnRegion != null && entranceRegion != null)
                {
                    firstLoopOnlyDiverRegions.Add(spawnRegion);
                    firstLoopOnlyDiverRegions.Add(entranceRegion);
                    AddRegionIfValid(spawnRegion);
                    AddRegionIfValid(entranceRegion);
                }
            }
            if (activeSlots[0].logicalName == "Interlude")
            {
                AddRegionIfValid(BuildRegionFromLevelName("From_Spawn"));
                AddRegionIfValid(BuildRegionFromLevelName("To_Pipeworks"));
            }
            if (activeSlots[0].logicalName == "Body Cavity")
            {
                M_Region spawnRegion = BuildRegionFromLevelName("From_Spawn");
                M_Region entranceRegion = BuildRegionFromLevelName("To_Cavity");
                if (spawnRegion != null)
                {
                    firstLoopOnlyDiverRegions.Add(spawnRegion);
                    AddRegionIfValid(spawnRegion);
                }
                if (entranceRegion != null)
                {
                    firstLoopOnlyDiverRegions.Add(entranceRegion);
                    AddRegionIfValid(entranceRegion);
                }
            }
            if (activeSlots[0].logicalName == "Diver")
            {
                M_Region spawnRegion = BuildRegionFromLevelName("From_Spawn");
                M_Region entranceRegion = BuildRegionFromLevelName("To_Diver");
                if (spawnRegion != null)
                {
                    firstLoopOnlyDiverRegions.Add(spawnRegion);
                    AddRegionIfValid(spawnRegion);
                }
                if (entranceRegion != null)
                {
                    firstLoopOnlyDiverRegions.Add(entranceRegion);
                    AddRegionIfValid(entranceRegion);
                }
            }
            for (int i = 0; i < activeSlots.Count; i++)
            {

                var currentSlot = activeSlots[i];
                string key = currentSlot.logicalName.ToLower();

                var mainReg = currentSlot.logicalName == "Campaign" || currentSlot.logicalName == "Diver"
                    ? null
                    : GetAndFilterRegion(currentSlot.assetName, currentSlot.slotIndex);
                int breakCount = Mathf.Max(0, GetCountStateForSlot(key + "_breakroom_amount", key, currentSlot.slotIndex));
                int transitionBreakCount = currentSlot.breakRoom || currentSlot.logicalName == "Campaign"
                    ? breakCount
                    : 0;

                if (currentSlot.logicalName == "Campaign")
                    AppendCampaignRegions(currentSlot.slotIndex, currentSlot.loops);

                for (int loop = 0; loop < currentSlot.loops && currentSlot.logicalName != "Campaign"; loop++)
                {
                    if (CustomRegions.IsCustom(currentSlot.logicalName) && loop > 0)
                    {
                        List<(string levelId, string targetRegion)> transitions = new List<(string, string)>();
                        foreach (var transition in ResolveTransitions(currentSlot.logicalName, currentSlot.logicalName))
                            transitions.Add((transition.levelId, currentSlot.logicalName));
                        InjectTransitionRegion(transitions, currentSlot.logicalName, currentSlot.logicalName, 0, key, currentSlot.slotIndex);
                    }
                    if (currentSlot.logicalName == "Body Cavity" && loop > 0)
                    {
                        List<(string levelId, string targetRegion)> transitions = new List<(string, string)>();
                        foreach (var transition in ResolveTransitions("Body Cavity", "Body Cavity"))
                            transitions.Add(transition);
                        InjectTransitionRegion(transitions, "Body Cavity", "Body Cavity", 0, key, currentSlot.slotIndex);
                    }
                    if (currentSlot.logicalName == "Diver")
                    {
                        if (loop > 0)
                        {
                            List<(string levelId, string targetRegion)> transitions = new List<(string, string)>();
                            foreach (var transition in ResolveTransitions("Diver", "Diver"))
                                transitions.Add(transition);
                            InjectTransitionRegion(transitions, "Diver", "Diver", 0, key, currentSlot.slotIndex);
                        }
                        AddRegionIfValid(BuildDiverRegion(currentSlot.slotIndex));
                    }
                    else if (mainReg != null)
                        AppendRegionToList(mainReg, currentSlot.slotIndex);

                    if (UseDefaultTransitionsAndBreakrooms && currentSlot.breakRoom &&
                        RegionDefinitions.TryGetValue(currentSlot.logicalName, out RegionDefinition definition))
                    {
                        for (int j = 0; j < breakCount; j++)
                        {
                            M_Region breakroom = GetAndFilterRegion(definition.BreakroomAsset, currentSlot.slotIndex);
                            if (breakroom != null)
                                EndlessMode.regions.Add(breakroom);
                        }
                    }
                }

                if (!UseDefaultTransitionsAndBreakrooms && currentSlot.logicalName == "Abyss")
                {
                    M_Region abyssBreakroom = GetAndFilterRegion(
                    RegionDefinitions["Abyss"].BreakroomAsset,
                    currentSlot.slotIndex);
                    if (abyssBreakroom != null)
                        EndlessMode.regions.Add(abyssBreakroom);
                }

                if (activeSlots.Count > 1)
                {
                    var nextSlot = activeSlots[(i + 1) % activeSlots.Count];

                    string from = currentSlot.logicalName;
                    string to = nextSlot.logicalName;

                    if (from == "Breakroom" || from == "Checkpoint") from = "Silos";
                    if (to == "Breakroom" || to == "Checkpoint") to = "Silos";
                    if (to == "Interlude") to = (i + 1) % activeSlots.Count == 0 ? "Anywhere" : "Pipeworks";
                    if (!UseDefaultTransitionsAndBreakrooms || currentSlot.logicalName == "Campaign" || nextSlot.logicalName == "Campaign" || from != to || from == "Body Cavity" || from.ContainsInsensitive("Diver") || from.ContainsInsensitive("Omicron") || from.ContainsInsensitive("Parasite") || from.ContainsInsensitive("Interlude") || from.ContainsInsensitive("Pier") || from.ContainsInsensitive("Advanced") || from.ContainsInsensitive("Shattered") || from.ContainsInsensitive("Boost") || from.ContainsInsensitive("Roach") || from.ContainsInsensitive("Comms") || from.ContainsInsensitive("Sink") || from.ContainsInsensitive("Chute"))
                    {
                        List<(string levelId, string targetRegion)> transitions = new List<(string, string)>();

                        string transitionFrom = currentSlot.logicalName == "Campaign" ? "Campaign" : from;
                        string transitionTo = nextSlot.logicalName == "Campaign" ? "Comms Array" : to;

                        foreach (var transition in ResolveTransitions(transitionFrom, transitionTo))
                        {
                            transitions.Add((transition.levelId, to));
                        }

                        InjectTransitionRegion(transitions, from, to, transitionBreakCount, key, currentSlot.slotIndex);

                    }
                }
                else
                {
                    string from = currentSlot.logicalName;
                    if (from == "Breakroom" || from == "Checkpoint") from = "Silos";

                    if (CustomRegions.IsCustom(from) || !UseDefaultTransitionsAndBreakrooms || (from != "Campaign" && (from == "Body Cavity" || from.ContainsInsensitive("Diver") || from.ContainsInsensitive("Parasite") || from.ContainsInsensitive("Omicron") || from.ContainsInsensitive("Interlude") || from.ContainsInsensitive("Pier") || from.ContainsInsensitive("Advanced") || from.ContainsInsensitive("Shattered") || from.ContainsInsensitive("Boost") || from.ContainsInsensitive("Roach") || from.ContainsInsensitive("Comms") || from.ContainsInsensitive("Sink") || from.ContainsInsensitive("Chute"))))
                    {
                        List<(string levelId, string targetRegion)> transitions = new List<(string, string)>();

                        string transitionFrom = currentSlot.logicalName == "Campaign" ? "Campaign" : from;
                        string transitionTo = currentSlot.logicalName == "Campaign" ? "Comms Array" : from;

                        foreach (var transition in ResolveTransitions(transitionFrom, transitionTo))
                        {
                            transitions.Add((transition.levelId, from));
                        }
                        InjectTransitionRegion(transitions, from, from, transitionBreakCount, key, currentSlot.slotIndex);
                    }
                }
            }

            for (int i = 0; i < EndlessMode.regions.Count; i++)
            {
                var region = EndlessMode.regions[i];
                if (region != null)
                    region.nextRegion = EndlessMode.regions[(i + 1) % EndlessMode.regions.Count];
            }
        }

        public static void GiveStartingItems()
        {
            if (EndlessMode == null)
                return;

            int timepiece = CustomUIManager.GetCountState("item_timepiece");
            if (timepiece > 0)
                GiveItem("Item_Artifact_Timepiece", timepiece);

            int translocator = CustomUIManager.GetCountState("item_translocator");
            if (translocator > 0)
                GiveItem("Item_Artifact_Translocator", translocator);

            int evaglove = CustomUIManager.GetCountState("item_evaglove");
            if (evaglove > 0)
                GiveItem("Item_Artifact_EVAGlove", evaglove);

            int rapier = CustomUIManager.GetCountState("item_rapier");
            if (rapier > 0)
                GiveItem("Item_Artifact_Rapier", rapier);

            int rebar = CustomUIManager.GetCountState("item_rebar_return");
            if (rebar > 0)
                GiveItem("Item_Artifact_Rebar_Return", rebar);

            int remote = CustomUIManager.GetCountState("item_remote");
            if (remote > 0)
                GiveItem("Item_Artifact_Remote", remote);

            int handgun = CustomUIManager.GetCountState("item_handgun_kit");
            if (handgun > 0)
            {
                GiveItem("Item_Handgun", handgun);
                GiveItem("Item_10mm_Ammo", handgun * 2);
            }

            int flaregun = CustomUIManager.GetCountState("item_flaregun_kit");
            if (flaregun > 0)
            {
                GiveItem("Item_Flaregun", flaregun);
                GiveItem("Item_Flaregun_Ammo", flaregun * 2);
            }

            int flashlight = CustomUIManager.GetCountState("item_flashlight");
            if (flashlight > 0)
                GiveItem("Item_Flashlight", flashlight);

            int craftcube = CustomUIManager.GetCountState("item_artifact_craftcube");
            if (craftcube > 0)
                GiveItem("Item_Artifact_Craftcube", craftcube);

            int cryogun = CustomUIManager.GetCountState("item_cryogun");
            if (cryogun > 0)
                GiveItem("Item_Cryogun", cryogun);
        }

        public static void GiveStartingPerks()
        {
            if (EndlessMode == null)
                return;

            var player = ENT_Player.GetPlayer();
            if (player == null)
                return;

            foreach (var kvp in StartingPerkTitles)
            {
                int count = CustomUIManager.GetCountState(kvp.Key);
                if (count > 0)
                {
                    var perkAsset = CL_AssetManager.GetPerkAsset(kvp.Key);
                    if (perkAsset != null)
                    {
                        for (int i = 0; i < count; i++)
                        {
                            player.AddPerk(perkAsset);
                        }
                    }
                }
            }
        }

        private static void GiveItem(string prefabName, int count)
        {
            if (Inventory.instance == null) return;
            for (int i = 0; i < count; i++)
            {
                var itemObj = CL_AssetManager.GetItemObjectPrefab(prefabName);
                if (itemObj != null && itemObj.itemData != null)
                {
                    Inventory.instance.AddItemToInventoryCenter(
                        itemObj.itemData.GetClone(null, false));
                }
            }
        }

        public static bool PendingPierOutro = false;

        private static readonly Dictionary<string, M_Region> campaignBranchRegions = new Dictionary<string, M_Region>();

        public static M_Region GetCampaignRegionForLevel(M_Level level)
        {
            if (level == null) return null;
            if (IsCampaignRegion(level.region)) return level.region;
            WorldLoader loader = WorldLoader.instance;
            if (loader == null || CL_GameManager.GetCurrentGamemode() != EndlessMode) return null;
            WorldLoader.BranchInfo branch = null;
            if (loader.currentBranch?.levelTracker != null && loader.currentBranch.levelTracker.Any(info => info != null && info.GetLevel() == level))
                branch = loader.currentBranch;
            else if (loader.branchTracker != null)
                branch = loader.branchTracker.Find(candidate => candidate?.levelTracker != null && candidate.levelTracker.Any(info => info != null && info.GetLevel() == level));
            return GetCampaignRegionForBranch(branch);
        }

        public static M_Region GetCampaignRegionForBranch(WorldLoader.BranchInfo branch)
        {
            HashSet<WorldLoader.BranchInfo> visited = new HashSet<WorldLoader.BranchInfo>();
            while (branch != null && visited.Add(branch))
            {
                if (!string.IsNullOrEmpty(branch.id) && campaignBranchRegions.TryGetValue(branch.id, out M_Region campaign) && campaign != null) return campaign;
                M_Level parent = branch.parentLevel?.GetLevel();
                if (parent != null && IsCampaignRegion(parent.region)) return parent.region;
                branch = branch.parent;
            }
            return null;
        }

        [HarmonyPatch(typeof(WorldLoader), nameof(WorldLoader.GenerateBranchFromLevel))]
        public static class CampaignBranchContextPatch
        {
            private static void Prefix(M_Level level, out M_Region __state)
            {
                __state = CL_GameManager.GetCurrentGamemode() == EndlessMode ? GetCampaignRegionForLevel(level) : null;
            }

            private static void Postfix(M_GenerationBranch branch, M_Region __state)
            {
                if (__state != null && branch != null && !string.IsNullOrEmpty(branch.id))
                    campaignBranchRegions[branch.id] = __state;
            }
        }

        private static bool IsCampaignRegion(M_Region region)
        {
            return region != null && (CampaignGenerationRegions.Contains(region) || region.name.Contains("_CampaignRuntimeCopy"));
        }

        private static bool IsCampaignGenerationLevel(M_Level level)
        {
            return GetCampaignRegionForLevel(level) != null;
        }

        private static M_Region FilterCampaign(string name, int slot = -1)
        {
            M_Region original = GetCachedObject<M_Region>(name);

            if (original == null)
            {
                original = CL_AssetManager.GetBaseAssetDatabase()
                    .regionAssets
                    .Find(r => r != null && r.name == name);
            }

            if (original == null)
                return null;

            M_Region dynamicRegion = UnityEngine.Object.Instantiate(original);
            RegisterGeneratedRegion(dynamicRegion);
            CampaignGenerationRegions.Add(dynamicRegion);
            dynamicRegion.name = original.name + "_slot_" + slot + "_CampaignRuntimeCopy";
            dynamicRegion.subregionGroups = new List<M_Region.SubregionGroup>();
            Dictionary<M_Subregion, M_Subregion> clonedSubregions = new Dictionary<M_Subregion, M_Subregion>();

            if (original.subregionGroups == null)
            {
                dynamicRegion.startLevelReferences = CloneCampaignHolders(original.startLevelReferences, dynamicRegion, null, clonedSubregions, slot);
                dynamicRegion.transitionLevels = CloneCampaignTransitions(original.transitionLevels, dynamicRegion, clonedSubregions, slot);
                return dynamicRegion;
            }

            foreach (M_Region.SubregionGroup originalGroup in original.subregionGroups)
            {
                if (originalGroup == null || originalGroup.subregions == null)
                    continue;

                M_Region.SubregionGroup newGroup = new M_Region.SubregionGroup
                {
                    name = originalGroup.name,
                    subregions = new List<M_Subregion>()
                };

                foreach (M_Subregion originalSub in originalGroup.subregions)
                {
                    if (originalSub == null)
                        continue;

                    if (IsCampaignSubregionDisabled(originalSub, slot))
                        continue;

                    newGroup.subregions.Add(CloneCampaignSubregion(originalSub, dynamicRegion, slot, clonedSubregions));
                }

                if (newGroup.subregions.Count > 0)
                    dynamicRegion.subregionGroups.Add(newGroup);
            }

            dynamicRegion.startLevelReferences = CloneCampaignHolders(original.startLevelReferences, dynamicRegion, null, clonedSubregions, slot);
            dynamicRegion.transitionLevels = CloneCampaignTransitions(original.transitionLevels, dynamicRegion, clonedSubregions, slot);

            var db = CL_AssetManager.GetBaseAssetDatabase();
            if (dynamicRegion.subregionGroups.Count == 0)
                return null;

            if (!db.regionAssets.Contains(dynamicRegion))
                db.regionAssets.Add(dynamicRegion);

            return dynamicRegion;
        }

        private static M_Subregion CloneCampaignSubregion(
            M_Subregion original,
            M_Region region,
            int slot,
            Dictionary<M_Subregion, M_Subregion> clonedSubregions)
        {
            if (original == null)
                return null;

            if (clonedSubregions.TryGetValue(original, out M_Subregion existing))
                return existing;

            M_Subregion clone = UnityEngine.Object.Instantiate(original);
            RegisterGeneratedSubregion(clone);
            clone.name = original.name;
            clonedSubregions.Add(original, clone);

            string subKey = GetCampaignSubregionKey(original);
            clone.difficulty *= GetFloatStateForSlot($"subregion_{subKey}_difficulty_mult", "campaign", slot);
            clone.massSpeedMult *= GetFloatStateForSlot($"subregion_{subKey}_mass_mult", "campaign", slot);
            clone.spaceCompression *= GetFloatStateForSlot($"subregion_{subKey}_space_mult", "campaign", slot);
            clone.levelReferences = CloneCampaignHolders(original.levelReferences, region, clone);
            clone.startLevelReferences = CloneCampaignHolders(original.startLevelReferences, region, clone);
            clone.endLevelReferences = CloneCampaignHolders(original.endLevelReferences, region, clone);
            clone.levelGroups = new List<M_Subregion.LevelGroup>();

            if (original.levelGroups != null)
            {
                foreach (M_Subregion.LevelGroup group in original.levelGroups)
                {
                    if (group == null)
                        continue;

                    clone.levelGroups.Add(new M_Subregion.LevelGroup
                    {
                        name = group.name,
                        length = group.length,
                        endSubregion = group.endSubregion,
                        levelReferences = CloneCampaignHolders(group.levelReferences, region, clone)
                    });
                }
            }

            return clone;
        }

        private static List<M_Region.TransitionLevels> CloneCampaignTransitions(
            List<M_Region.TransitionLevels> transitions,
            M_Region region,
            Dictionary<M_Subregion, M_Subregion> clonedSubregions,
            int slot)
        {
            List<M_Region.TransitionLevels> clonedTransitions = new List<M_Region.TransitionLevels>();
            if (transitions == null)
                return clonedTransitions;

            foreach (M_Region.TransitionLevels transition in transitions)
            {
                if (transition == null)
                    continue;

                M_Region.TransitionLevels clonedTransition = new M_Region.TransitionLevels
                {
                    name = transition.name,
                    fromRegion = transition.fromRegion,
                    levels = transition.levels,
                    transitionRegion = transition.transitionRegion,
                    transitionSubRegion = CloneCampaignSubregion(transition.transitionSubRegion, region, slot, clonedSubregions),
                    useAllLevels = transition.useAllLevels,
                    levelReferences = CloneCampaignHolders(transition.levelReferences, region, null, clonedSubregions, slot)
                };
                clonedTransitions.Add(clonedTransition);
            }

            return clonedTransitions;
        }

        private static bool IsCampaignSubregionDisabled(M_Subregion subregion, int slot)
        {
            List<string> keys = GetCampaignSubregionKeys(subregion);

            foreach (string key in keys.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (GetToggleStateForSlot($"subregion_{key}_disabled", "campaign", slot))
                    return true;
            }

            return false;
        }

        public static string GetCampaignSubregionKey(M_Subregion subregion)
        {
            return "campaign_" + subregion.name;
        }

        public static List<(string Region, M_Subregion Subregion)> GetCampaignSubregions()
        {
            List<(string Region, M_Subregion Subregion)> result = new List<(string Region, M_Subregion Subregion)>();
            HashSet<M_Region> visited = new HashSet<M_Region>();
            HashSet<M_Subregion> added = new HashSet<M_Subregion>();
            void Add(M_Subregion subregion, string group)
            {
                if (subregion != null && added.Add(subregion)) result.Add((group, subregion));
            }
            void Visit(M_Region region, string group)
            {
                if (region == null || !visited.Add(region)) return;
                if (region.subregionGroups != null)
                    foreach (M_Region.SubregionGroup entry in region.subregionGroups)
                        if (entry?.subregions != null) foreach (M_Subregion subregion in entry.subregions) Add(subregion, group);
                if (region.transitionLevels != null)
                    foreach (M_Region.TransitionLevels transition in region.transitionLevels)
                    {
                        if (transition == null) continue;
                        Add(transition.transitionSubRegion, group);
                        Visit(transition.transitionRegion, group);
                    }
                if (region.startLevelReferences != null)
                    foreach (M_Level.LevelAssetHolder holder in region.startLevelReferences) Add(holder?.subregion, group);
            }
            M_Gamemode campaign = GetCampaignGamemode();
            if (campaign?.regions != null)
                foreach (M_Region region in campaign.regions)
                    if (region != null) Visit(region, string.IsNullOrWhiteSpace(region.introText) ? region.regionName : region.introText);
            return result;
        }

        private static List<string> GetCampaignSubregionKeys(M_Subregion subregion)
        {
            return new List<string> { GetCampaignSubregionKey(subregion) };
        }

        private static List<M_Level.LevelAssetHolder> CloneCampaignHolders(
            List<M_Level.LevelAssetHolder> holders,
            M_Region region,
            M_Subregion subregion,
            Dictionary<M_Subregion, M_Subregion> clonedSubregions = null,
            int slot = -1)
        {
            List<M_Level.LevelAssetHolder> clonedHolders = new List<M_Level.LevelAssetHolder>();
            if (holders == null)
                return clonedHolders;

            foreach (M_Level.LevelAssetHolder holder in holders)
            {
                if (holder == null)
                    continue;

                M_Level.LevelAssetHolder clonedHolder = holder.CloneViaSerialization<M_Level.LevelAssetHolder>();
                clonedHolder.region = region;
                clonedHolder.subregion = subregion ?? (clonedSubregions != null
                    ? CloneCampaignSubregion(holder.subregion, region, slot, clonedSubregions)
                    : null);
                clonedHolder.pauseGeneration = holder.pauseGeneration;

                clonedHolders.Add(clonedHolder);
            }

            return clonedHolders;
        }

        private static readonly Dictionary<string, float> generatedSubregionLengthCache = new Dictionary<string, float>();

        private static float GetGeneratedSubregionLength(M_Subregion subregion)
        {
            if (subregion == null) return 0f;
            string cacheKey = subregion.name + "|" + subregion.subregionName + "|" + subregion.useLevelCount + "|" + subregion.subregionMinLength + "|" + subregion.subregionMaxLength + "|" + subregion.subregionHeight;
            if (generatedSubregionLengthCache.TryGetValue(cacheKey, out float cachedLength)) return cachedLength;
            UnityEngine.Random.State randomState = UnityEngine.Random.state;
            try
            {
                List<M_Level.LevelAssetHolder> levels = subregion.GetGenerationLevels().fullList;
                if (levels == null) return 0f;
                float length = 0f;
                foreach (M_Level.LevelAssetHolder level in levels) if (level != null) length += level.length;
                generatedSubregionLengthCache[cacheKey] = length;
                return length;
            }
            finally
            {
                UnityEngine.Random.state = randomState;
            }
        }

        private static bool IsHabitationBoundaryGroup(string groupName)
        {
            if (string.IsNullOrEmpty(groupName)) return false;
            return groupName.ContainsInsensitive("Entry") || groupName.ContainsInsensitive("Ending") || groupName.ContainsInsensitive("Intro") || groupName.ContainsInsensitive("Outro") || groupName.ContainsInsensitive("_To_") || groupName.ContainsInsensitive(" To ");
        }

        private static float GetLargestGeneratedLevelLength(M_Subregion subregion)
        {
            if (subregion == null) return 0f;
            float largest = 0f;
            if (subregion.levelGroups != null)
            {
                foreach (M_Subregion.LevelGroup group in subregion.levelGroups)
                {
                    if (group == null || group.levelReferences == null) continue;
                    foreach (M_Level.LevelAssetHolder holder in group.levelReferences) if (holder != null) largest = Mathf.Max(largest, holder.length);
                }
            }
            if (subregion.startLevelReferences != null) foreach (M_Level.LevelAssetHolder holder in subregion.startLevelReferences) if (holder != null) largest = Mathf.Max(largest, holder.length);
            if (subregion.endLevelReferences != null) foreach (M_Level.LevelAssetHolder holder in subregion.endLevelReferences) if (holder != null) largest = Mathf.Max(largest, holder.length);
            return largest;
        }

        private static M_Region.SubregionGroup CloneBalancedHabitationGroup(M_Region.SubregionGroup source, float generatedLength, float largestLevelLength, bool shorten)
        {
            M_Region.SubregionGroup clone = new M_Region.SubregionGroup();
            clone.name = source.name;
            clone.subregions = new List<M_Subregion>();
            foreach (M_Subregion sourceSubregion in source.subregions)
            {
                if (sourceSubregion == null) continue;
                if (!shorten)
                {
                    clone.subregions.Add(sourceSubregion);
                    continue;
                }
                M_Subregion cloneSubregion = UnityEngine.Object.Instantiate(sourceSubregion);
                RegisterGeneratedSubregion(cloneSubregion);
                cloneSubregion.name = sourceSubregion.name;
                if (shorten)
                {
                    cloneSubregion.useLevelCount = false;
                    cloneSubregion.subregionHeight = Mathf.Max(0f, generatedLength - largestLevelLength);
                }
                clone.subregions.Add(cloneSubregion);
                var db = CL_AssetManager.GetBaseAssetDatabase();
                if (!db.subRegionAssets.Contains(cloneSubregion)) db.subRegionAssets.Add(cloneSubregion);
            }
            return clone;
        }

        private static void BalanceHabitationSubregions(M_Region region, float requestedLength)
        {
            if (region == null || region.subregionGroups == null || region.subregionGroups.Count == 0) return;
            List<M_Region.SubregionGroup> boundaries = region.subregionGroups.Where(group => group != null && IsHabitationBoundaryGroup(group.name)).ToList();
            List<M_Region.SubregionGroup> mains = region.subregionGroups.Where(group => group != null && !IsHabitationBoundaryGroup(group.name) && group.subregions != null && group.subregions.Count > 0).ToList();
            float fixedLength = 0f;
            foreach (M_Region.SubregionGroup boundary in boundaries) foreach (M_Subregion subregion in boundary.subregions) fixedLength += GetGeneratedSubregionLength(subregion);
            float remainingLength = Mathf.Max(0f, requestedLength - fixedLength);
            float share = mains.Count > 0 ? remainingLength / mains.Count : 0f;
            float generatedMainLength = 0f;
            List<M_Region.SubregionGroup> balanced = new List<M_Region.SubregionGroup>();
            foreach (M_Region.SubregionGroup group in region.subregionGroups)
            {
                if (group == null) continue;
                if (IsHabitationBoundaryGroup(group.name))
                {
                    balanced.Add(group);
                    continue;
                }
                if (group.subregions == null || group.subregions.Count == 0) continue;
                float naturalLength = 0f;
                float largestLevelLength = 0f;
                foreach (M_Subregion subregion in group.subregions)
                {
                    naturalLength += GetGeneratedSubregionLength(subregion);
                    largestLevelLength = Mathf.Max(largestLevelLength, GetLargestGeneratedLevelLength(subregion));
                }
                if (naturalLength <= 0f || largestLevelLength <= 0f) continue;
                float quantizedShare = Mathf.Floor(share / largestLevelLength) * largestLevelLength;
                float groupRemaining = quantizedShare;
                int copyCount = 0;
                while (groupRemaining >= largestLevelLength)
                {
                    float copyLength = Mathf.Min(naturalLength, groupRemaining);
                    copyLength = Mathf.Floor(copyLength / largestLevelLength) * largestLevelLength;
                    if (copyLength < largestLevelLength) break;
                    balanced.Add(CloneBalancedHabitationGroup(group, copyLength, largestLevelLength, copyLength < naturalLength));
                    generatedMainLength += copyLength;
                    groupRemaining -= copyLength;
                    copyCount++;
                }
                Debug.Log("[Endless] Balanced Habitation " + group.name + " copies: " + copyCount + " allocated length: " + quantizedShare);
            }
            region.subregionGroups = balanced;
            region.regionHeight = fixedLength + generatedMainLength;
            region.useRegionHeight = false;
            Debug.Log("[Endless] Balanced Habitation length: " + region.regionHeight + " requested: " + requestedLength + " remainder: " + Mathf.Max(0f, requestedLength - region.regionHeight));
        }

        private static bool IsDuctInterlude(M_Level.LevelAssetHolder holder, bool filterDuctInterludes)
        {
            if (!filterDuctInterludes || holder == null)
                return false;

            M_Level level = holder.GetLevel();
            string levelName = level != null ? level.levelName : string.Empty;
            return !string.IsNullOrEmpty(levelName) && levelName.ContainsInsensitive("inter");
        }

        private static M_Region GetAndFilterRegion(string name, int slot = -1)
        {
            if (CustomRegions.TryGet(name, out CustomRegions.RegionData custom)) return BuildPlayerCustomRegion(custom, slot);
            M_Region original;
            if (!TryGetRegisteredCustomRegion(name, out original)) original = GetCachedObject<M_Region>(name);

            if (original == null)
            {
                original = CL_AssetManager.GetBaseAssetDatabase()
                    .regionAssets
                    .Find(r => r != null && r.name == name);
            }

            if (original == null)
                return null;

            bool filterDuctInterludes = original.name.ContainsInsensitive("Duct");
            M_Region dynamicRegion = UnityEngine.Object.Instantiate(original);
            RegisterGeneratedRegion(dynamicRegion);
            dynamicRegion.name = original.name + "_slot_" + slot + "_RuntimeCopy";
            dynamicRegion.subregionGroups = new List<M_Region.SubregionGroup>();
            if (original.name == "Region_Habitation_BodyCavity")
                dynamicRegion.transitionLevels = new List<M_Region.TransitionLevels>();
            Dictionary<string, float> disabledGeneratedLengths = new Dictionary<string, float>();
            if (original.subregionGroups == null)
                return dynamicRegion;

            string regionKey = null;

            if (original.name == "Region_Habitation_BodyCavity")
                regionKey = "cavity";
            else if (original.name.Contains("Silos"))
                regionKey = "silos";
            else if (original.name.Contains("Ladder"))
                regionKey = "ladder";
            else if (original.name.Contains("Pipeworks"))
                regionKey = "pipeworks";
            else if (original.name.Contains("Habitation"))
                regionKey = "habitation";
            else if (original.name.Contains("Abyss"))
                regionKey = "abyss";
            else if (original.name.Contains("Nest"))
                regionKey = "nest";
            else if (original.name.Contains("Parasite"))
                regionKey = "parasite";
            else if (original.name.Contains("Chimney"))
                regionKey = "chimney";
            else if (original.name.Contains("Pier"))
                regionKey = "pier";
            else if (original.name.Contains("Ascent"))
                regionKey = "ascent";
            else if (original.name.Contains("HadalDiver"))
                regionKey = "diver";

            float mult_value = 0f;
            if (regionKey == "abyss")
            {
                if (GetToggleStateForSlot("subregion_Scaffolds_disabled", regionKey, slot)) mult_value += 1f;
                if (GetToggleStateForSlot("subregion_Handle_disabled", regionKey, slot)) mult_value += 1f;
                if (GetToggleStateForSlot("subregion_Gardens_disabled", regionKey, slot)) mult_value += 1f;
            }

            int groupAmount = Math.Max(
                1,
                (int)Math.Round(
                    GetFloatStateForSlot($"{regionKey}_length_mult", regionKey, slot)));

            if (regionKey == "habitation") groupAmount = 1;

            groupAmount += (int)mult_value;
            List<M_Region.SubregionGroup> groupsToProcess = new List<M_Region.SubregionGroup>();

            int groupCount = original.subregionGroups.Count;

            if (groupCount > 0)
            {
                groupsToProcess.Add(original.subregionGroups[0]);

                if (groupCount > 2)
                {
                    for (int i = 0; i < groupAmount; i++)
                    {
                        for (int j = 1; j < groupCount - 1; j++)
                        {
                            groupsToProcess.Add(original.subregionGroups[j]);
                        }
                    }
                }

                if (groupCount > 1)
                {
                    groupsToProcess.Add(original.subregionGroups[groupCount - 1]);
                }
            }

            var db = CL_AssetManager.GetBaseAssetDatabase();

            if (!db.regionAssets.Contains(dynamicRegion))
                db.regionAssets.Add(dynamicRegion);

            if (regionKey == "chimney")
            {
                if (GetToggleStateForSlot("subregion_Icebox_disabled", regionKey, slot) && GetToggleStateForSlot("subregion_Shelf_disabled", regionKey, slot) && (!GetToggleStateForSlot("subregion_Chimney_Transition_Interlude_disabled", regionKey, slot)) && GetToggleStateForSlot("subregion_Spire_disabled", regionKey, slot))
                {
                    string chimKey = (slot >= 0 && HasSlotSetting($"slot_{slot}_subregion_Chimney_Transition_Interlude_disabled")) ? $"slot_{slot}_subregion_Chimney_Transition_Interlude_disabled" : "subregion_Chimney_Transition_Interlude_disabled";
                    if (!CustomUIManager.SettingToggles.ContainsKey(chimKey))
                        CustomUIManager.SettingToggles.Add(chimKey, true);
                    else
                        CustomUIManager.SettingToggles[chimKey] = true;
                }
            }

            foreach (var originalGroup in groupsToProcess)
            {
                if (originalGroup == null || originalGroup.subregions == null)
                    continue;

                M_Region.SubregionGroup newGroup = new M_Region.SubregionGroup();
                newGroup.name = originalGroup.name;
                newGroup.subregions = new List<M_Subregion>();

                foreach (var originalSub in originalGroup.subregions)
                {
                    if (originalSub == null)
                        continue;

                    string subKey = null;

                    foreach (var pair in SubregionDefinitions)
                    {
                        bool nameMatch =
                            !string.IsNullOrEmpty(originalSub.name) &&
                            originalSub.name.Contains(pair.Key);

                        bool subregionNameMatch =
                            !string.IsNullOrEmpty(originalSub.subregionName) &&
                            originalSub.subregionName.Contains(pair.Key);

                        if (nameMatch || subregionNameMatch)
                        {
                            subKey = pair.Key;
                            break;
                        }
                    }

                    if (subKey == null)
                        subKey = GetRegionSubregionSettingKey(regionKey, slot);

                    bool disabled = subKey != null && GetToggleStateForSlot($"subregion_{subKey}_disabled", regionKey, slot);

                    M_Level replacementLevel = null;
                    string replacementAsset = null;

                    if (disabled)
                    {
                        if (regionKey == "habitation" && (subKey == "Labs" || subKey == "LostPier" || subKey == "Shaft"))
                        {
                            float generatedLength = GetGeneratedSubregionLength(originalSub);
                            if (disabledGeneratedLengths.ContainsKey(subKey)) disabledGeneratedLengths[subKey] += generatedLength;
                            else disabledGeneratedLengths.Add(subKey, generatedLength);
                        }
                        bool removedAnyLevels = true;

                        if (removedAnyLevels &&
                            subKey != null &&
                            SubregionDefinitions.ContainsKey(subKey) &&
                            !string.IsNullOrEmpty(SubregionDefinitions[subKey].ReplacementLevelAsset))
                        {
                            replacementAsset = SubregionDefinitions[subKey].ReplacementLevelAsset;
                            replacementLevel = GetCachedObject<M_Level>(replacementAsset);

                            if (replacementLevel == null)
                            {
                                replacementLevel = CustomLevelRegistry.Load(replacementAsset);
                            }
                        }

                        if (replacementLevel == null)
                        {
                            continue;
                        }
                    }

                    M_Subregion clonedSub = UnityEngine.Object.Instantiate(originalSub);
                    RegisterGeneratedSubregion(clonedSub);
                    clonedSub.name = originalSub.name;
                    clonedSub.levelGroups = new List<M_Subregion.LevelGroup>();

                    if (subKey != null)
                    {
                        clonedSub.difficulty *= GetFloatStateForSlot($"subregion_{subKey}_difficulty_mult", regionKey, slot);
                        clonedSub.massSpeedMult *= GetFloatStateForSlot($"subregion_{subKey}_mass_mult", regionKey, slot);
                        clonedSub.spaceCompression *= GetFloatStateForSlot($"subregion_{subKey}_space_mult", regionKey, slot);
                    }

                    if (!disabled)
                    {
                        if (originalSub.startLevelReferences != null)
                        {
                            clonedSub.startLevelReferences = new List<M_Level.LevelAssetHolder>();
                            foreach (var holder in originalSub.startLevelReferences)
                            {
                                if (holder == null || IsDuctInterlude(holder, filterDuctInterludes))
                                    continue;

                                var newHolder = holder.CloneViaSerialization<M_Level.LevelAssetHolder>();
                                newHolder.region = dynamicRegion;
                                newHolder.subregion = clonedSub;

                                M_Level level = newHolder.GetLevel();
                                if (level != null)
                                {
                                    level.pauseGeneration = false;
                                    level.region = dynamicRegion;
                                    level.lastLevel = false;
                                    level.isLODGap = false;
                                }
                                clonedSub.startLevelReferences.Add(newHolder);
                            }
                        }

                        if (originalSub.endLevelReferences != null)
                        {
                            clonedSub.endLevelReferences = new List<M_Level.LevelAssetHolder>();
                            foreach (var holder in originalSub.endLevelReferences)
                            {
                                if (holder == null || IsDuctInterlude(holder, filterDuctInterludes))
                                    continue;

                                var newHolder = holder.CloneViaSerialization<M_Level.LevelAssetHolder>();
                                newHolder.region = dynamicRegion;
                                newHolder.subregion = clonedSub;

                                M_Level level = newHolder.GetLevel();
                                if (level != null)
                                {
                                    level.pauseGeneration = false;
                                    level.region = dynamicRegion;
                                    level.lastLevel = false;
                                    level.isLODGap = false;
                                }
                                clonedSub.endLevelReferences.Add(newHolder);
                            }
                        }

                        if (originalSub.levelGroups != null)
                        {
                            foreach (var originalLevelGroup in originalSub.levelGroups)
                            {
                                if (originalLevelGroup == null)
                                    continue;

                                M_Subregion.LevelGroup clonedGroup = new M_Subregion.LevelGroup();
                                clonedGroup.name = originalLevelGroup.name;
                                clonedGroup.length = originalLevelGroup.length;

                                clonedGroup.endSubregion = originalLevelGroup.endSubregion;

                                clonedGroup.levelReferences = new List<M_Level.LevelAssetHolder>();

                                if (originalLevelGroup.levelReferences != null)
                                {
                                    foreach (var holder in originalLevelGroup.levelReferences)
                                    {
                                        if (holder == null || IsDuctInterlude(holder, filterDuctInterludes))
                                            continue;

                                        var newHolder = holder.CloneViaSerialization<M_Level.LevelAssetHolder>();
                                        newHolder.region = dynamicRegion;
                                        newHolder.subregion = clonedSub;

                                        M_Level level = newHolder.GetLevel();
                                        if (original.name == "Region_Habitation_BodyCavity" && holder.id == "S3_BodyCavity_Exit" && level != null)
                                        {
                                            level = UnityEngine.Object.Instantiate(level);
                                            RegisterRuntimeObject(level.gameObject, false);
                                            newHolder.level = level;
                                            Transform drop = FindChildByName(level.transform, "Trigger - Shortcut Generator");
                                            if (drop != null)
                                            {
                                                foreach (MonoBehaviour behaviour in drop.GetComponents<MonoBehaviour>())
                                                {
                                                    behaviour.enabled = false;
                                                    UnityEngine.Object.Destroy(behaviour);
                                                }
                                                if (drop.GetComponent<TeleportTrigger>() == null)
                                                    drop.gameObject.AddComponent<TeleportTrigger>();
                                                drop.gameObject.SetActive(true);
                                            }
                                            level.gameObject.SetActive(false);
                                        }
                                        if (level != null)
                                        {
                                            level.pauseGeneration = false;
                                            level.region = dynamicRegion;
                                            level.lastLevel = false;
                                        }

                                        clonedGroup.levelReferences.Add(newHolder);
                                    }
                                }
                                void RemoveLODs(Transform parent)
                                {
                                    for (int i = parent.childCount - 1; i >= 0; i--)
                                    {
                                        Transform child = parent.GetChild(i);
                                        RemoveLODs(child);

                                        if (child.name.ContainsInsensitive("Abyss_Structures"))
                                        {
                                            Debug.Log(child.name);
                                            UnityEngine.Object.Destroy(child.gameObject);
                                        }
                                    }
                                }

                                if (clonedGroup.levelReferences.Count > 0)
                                    clonedSub.levelGroups.Add(clonedGroup);
                            }
                        }
                    }
                    else if (!originalSub.name.ContainsInsensitive("_To_") && !(regionKey == "chimney" && GetToggleStateForSlot("subregion_Chimney_Transition_Interlude_disabled", regionKey, slot)))
                    {
                        replacementLevel = UnityEngine.Object.Instantiate(replacementLevel);
                        clonedSub.startLevelReferences = new List<M_Level.LevelAssetHolder>();
                        clonedSub.endLevelReferences = new List<M_Level.LevelAssetHolder>();
                        clonedSub.subregionOrder = M_Subregion.SubregionOrder.single;

                        replacementLevel.pauseGeneration = false;
                        replacementLevel.region = dynamicRegion;
                        replacementLevel.lastLevel = false;

                        M_Level.LevelAssetHolder holder = M_Level.LevelAssetHolder.GetNewHolderFromLevel(replacementLevel);
                        holder.region = dynamicRegion;
                        holder.subregion = clonedSub;

                        M_Subregion.LevelGroup replacementGroup = new M_Subregion.LevelGroup();
                        replacementGroup.name = replacementAsset;
                        replacementGroup.length = 1;
                        replacementGroup.endSubregion = false;
                        replacementGroup.levelReferences = new List<M_Level.LevelAssetHolder> { holder };

                        clonedSub.levelGroups.Add(replacementGroup);
                    }
                    else continue;

                    newGroup.subregions.Add(clonedSub);

                    if (!db.subRegionAssets.Contains(clonedSub))
                        db.subRegionAssets.Add(clonedSub);
                }

                if (newGroup.subregions.Count > 0)
                {
                    dynamicRegion.subregionGroups.Add(newGroup);
                }
            }

            if (regionKey == "habitation" && disabledGeneratedLengths.Count > 0)
            {
                float total = 0f;
                foreach (KeyValuePair<string, float> pair in disabledGeneratedLengths)
                {
                    total += pair.Value;
                    Debug.Log("[Endless] Disabled Habitation " + pair.Key + " generated length: " + pair.Value);
                }
                Debug.Log("[Endless] Disabled Habitation generated length total: " + total);
            }

            if (regionKey != null)
            {
                float mult = GetFloatStateForSlot(regionKey + "_length_mult", regionKey, slot);
                mult += mult_value;
                if (regionKey == "habitation")
                {
                    float removedLength = disabledGeneratedLengths.Values.Sum();
                    float requestedLength = Mathf.Max(0f, (original.regionHeight - removedLength) * mult);
                    BalanceHabitationSubregions(dynamicRegion, requestedLength);
                }
                else dynamicRegion.regionHeight *= mult;
            }

            return dynamicRegion;
        }

        private static bool GetToggleStateForSlot(string fullKey, string regionKey, int slot)
        {
            if (slot >= 0)
            {
                string stripRegion = (!string.IsNullOrEmpty(regionKey) && fullKey.StartsWith(regionKey + "_"))
                    ? fullKey.Substring(regionKey.Length + 1)
                    : fullKey;
                string slotKey = $"slot_{slot}_{stripRegion}";
                if (HasSlotSetting(slotKey))
                {
                    bool slotValue = CustomUIManager.GetToggleState(slotKey);
                    bool regionValue = CustomUIManager.GetToggleState(fullKey);
                    if (MenuSettingsManager.IsDefaultBool(fullKey, slotValue) && !MenuSettingsManager.IsDefaultBool(fullKey, regionValue)) return regionValue;
                    return slotValue;
                }
            }
            return CustomUIManager.GetToggleState(fullKey);
        }

        private static float GetFloatStateForSlot(string fullKey, string regionKey, int slot)
        {
            if (slot >= 0)
            {
                string stripRegion = (!string.IsNullOrEmpty(regionKey) && fullKey.StartsWith(regionKey + "_"))
                    ? fullKey.Substring(regionKey.Length + 1)
                    : fullKey;
                string slotKey = $"slot_{slot}_{stripRegion}";
                if (HasSlotSetting(slotKey))
                {
                    float slotValue = CustomUIManager.GetFloatState(slotKey);
                    float regionValue = CustomUIManager.GetFloatState(fullKey);
                    if (MenuSettingsManager.IsDefaultFloat(fullKey, slotValue) && !MenuSettingsManager.IsDefaultFloat(fullKey, regionValue)) return regionValue;
                    return slotValue;
                }
            }
            return CustomUIManager.GetFloatState(fullKey);
        }

        private static int GetCountStateForSlot(string fullKey, string regionKey, int slot)
        {
            if (slot >= 0)
            {
                string stripRegion = (!string.IsNullOrEmpty(regionKey) && fullKey.StartsWith(regionKey + "_"))
                    ? fullKey.Substring(regionKey.Length + 1)
                    : fullKey;
                string slotKey = $"slot_{slot}_{stripRegion}";
                if (HasSlotSetting(slotKey))
                {
                    int slotValue = CustomUIManager.GetCountState(slotKey);
                    int regionValue = CustomUIManager.GetCountState(fullKey);
                    if (MenuSettingsManager.IsDefaultInt(fullKey, slotValue) && !MenuSettingsManager.IsDefaultInt(fullKey, regionValue)) return regionValue;
                    return slotValue;
                }
            }
            return CustomUIManager.GetCountState(fullKey);
        }

        private static bool HasSlotSetting(string key)
        {
            try
            {
                if (CustomUIManager.SettingToggles != null && CustomUIManager.SettingToggles.ContainsKey(key)) return true;
            }
            catch { }
            try
            {
                if (CustomUIManager.SettingFloats != null && CustomUIManager.SettingFloats.ContainsKey(key)) return true;
            }
            catch { }
            try
            {
                if (CustomUIManager.SettingCounts != null && CustomUIManager.SettingCounts.ContainsKey(key)) return true;
            }
            catch { }
            return false;
        }

        private static string GetRegionSubregionSettingKey(string regionKey, int slot)
        {
            if (string.IsNullOrEmpty(regionKey))
                return null;

            string titleCaseRegionKey = char.ToUpperInvariant(regionKey[0]) + regionKey.Substring(1);
            string[] candidates = { regionKey, titleCaseRegionKey };
            foreach (string candidate in candidates.Distinct(StringComparer.Ordinal))
            {
                if (HasSlotSetting($"subregion_{candidate}_disabled") ||
                    HasSlotSetting($"subregion_{candidate}_difficulty_mult") ||
                    HasSlotSetting($"subregion_{candidate}_mass_mult") ||
                    HasSlotSetting($"subregion_{candidate}_space_mult"))
                    return candidate;
            }

            return null;
        }


        private static string GetTransitionAssetName(string from, string to)
        {
            RegisterRegions();

            if (TransitionDefinitions.TryGetValue((from, to), out var transition))
                return transition;

            return "";
        }

        private static string GetTransitionTargetAssetName(string regionName)
        {
            if (RegionDefinitions.TryGetValue(regionName, out var definition))
                return definition.RegionAsset;

            if (regionName == "Campaign")
                return GetCampaignRegionAssetName();

            return null;
        }

        private static IEnumerable<(string levelId, string targetRegion)> ResolveTransitions(string from, string to)
        {
            RegisterRegions();
            from = GetCustomTransitionIdentity(from, false);
            to = GetCustomTransitionIdentity(to, true);

            string fromLevelSuffix = CustomRegions.IsCustom(from) ? "Anywhere" : from == "Body Cavity" ? "Duct" : from;
            string toLevelSuffix = CustomRegions.IsCustom(to) ? "Anywhere" : to == "Body Cavity" ? "Cavity" : to;

            Debug.Log($"ResolveTransitions: {from} -> {to}");

            if (UseDefaultTransitionsAndBreakrooms &&
                TransitionDefinitions.TryGetValue((from, to), out var direct))
            {
                Debug.Log($"Direct transition: {direct}");
                yield return (direct, to);
                yield break;
            }
            else
            {
                if ((CustomLevelRegistry.Load($"From_{fromLevelSuffix}") != null) && (CustomLevelRegistry.Load($"To_{toLevelSuffix}") != null))
                {
                    Debug.Log($"Step {from}->{to}: From_{fromLevelSuffix}, To_{toLevelSuffix}");
                    yield return ($"From_{fromLevelSuffix}", to);
                    yield return ($"To_{toLevelSuffix}", to);

                    yield break;
                }
                else if ((CustomLevelRegistry.Load($"From_{fromLevelSuffix}") != null))
                {
                    Debug.Log($"Step {from}->{to}: From_{fromLevelSuffix}, To_Anywhere");
                    yield return ($"From_{fromLevelSuffix}", to);
                    yield return ($"To_Anywhere", to);

                    yield break;
                }
                else if ((CustomLevelRegistry.Load($"To_{toLevelSuffix}") != null))
                {
                    Debug.Log($"Step {from}->{to}: From_Anywhere, To_{toLevelSuffix}");
                    yield return ($"From_Anywhere", to);
                    yield return ($"To_{toLevelSuffix}", to);

                    yield break;
                }
                else
                {
                    Debug.Log($"Step {from}->{to}: From_Anywhere, To_Anywhere");
                    yield return ($"From_Anywhere", to);
                    yield return ($"To_Anywhere", to);

                    yield break;
                }
            }

            //int current = RegionOrder.IndexOf(from);
            //int target = RegionOrder.IndexOf(to);

            //while (current != target)
            //{
            //int next = (current + 1) % RegionOrder.Count;

            //string a = RegionOrder[current];
            //string b = RegionOrder[next];

            //if (TransitionDefinitions.TryGetValue((a, b), out var transition))
            //{
            //Debug.Log($"Step {a}->{b}: {transition}");
            //yield return (transition, b);
            //if (b == to) yield break;
            //}
            //else
            //{
            //Debug.Log($"Step {a}->{b}: From_{a}, To_{b}");
            //yield return ($"From_{a}", b);
            //yield return ($"To_{b}", b);
            //}

            //current = next;
            //}
        }
        private static M_Level GetTransitionLevel(string id)
        {
            Debug.Log($"Transition lookup: {id}");

            var level = CustomLevelRegistry.Load(id);
            if (level != null)
            {
                Debug.Log($"Registry lookup found {id}");
                return level;
            }

            level = GetCachedObject<M_Level>(id);
            if (level != null)
            {
                Debug.Log($"Resources lookup found {id}, registering now.");

                CustomLevelRegistry.Register(id, level);

                return level;
            }

            Debug.Log($"Transition lookup FAILED for {id}");
            return null;
        }
        private static M_Region BuildRegionFromLevelName(string levelId, string targetRegionKey = null)
        {
            M_Level level = GetTransitionLevel(levelId);
            if (level == null)
                return null;

            level.loadAdjacent = true;

            M_Region template = GetCachedObject<M_Region>("Region_Silos_Saferoom_Endless");
            if (template == null)
                return null;

            M_Region region = UnityEngine.Object.Instantiate(template);
            RegisterGeneratedRegion(region);
            region.name = $"Region_{levelId}";
            region.useRegionHeight = false;
            region.regionHeight = 0;
            region.regionOrder = M_Region.RegionOrder.playlist;
            region.introText = "Omicron";
            region.forceStartLevel = false;
            region.transitionLevels = new List<M_Region.TransitionLevels>();

            if (!string.IsNullOrEmpty(targetRegionKey) && RegionDefinitions.TryGetValue(targetRegionKey, out var targetDef))
            {
                region.nextRegion = GetAndFilterRegion(targetDef.RegionAsset);
            }

            var db = CL_AssetManager.GetBaseAssetDatabase();
            if (!db.regionAssets.Contains(region))
            {
                db.regionAssets.Add(region);
            }

            M_Subregion subregion = UnityEngine.Object.Instantiate(region.subregionGroups[0].subregions[0]);
            RegisterGeneratedSubregion(subregion);
            subregion.name = $"Subregion_{levelId}";
            subregion.subregionName = subregion.name;
            subregion.introText = "Transition";
            subregion.useLevelCount = false;
            subregion.subregionMinLength = 0;
            subregion.subregionMaxLength = 0;
            subregion.subregionOrder = M_Subregion.SubregionOrder.single;

            var holder = M_Level.LevelAssetHolder.GetNewHolderFromLevel(level);
            holder.region = region.nextRegion;
            holder.subregion = subregion;

            subregion.levelGroups = new List<M_Subregion.LevelGroup>()
{
new M_Subregion.LevelGroup()
{
name = "Transition_1",
length = 0,
levelReferences = new List<M_Level.LevelAssetHolder>() { holder }
}
};

            if (levelId.StartsWith("To_", StringComparison.OrdinalIgnoreCase) && HasConnectorDoor(level, true))
            {
                M_Level Adaptor = GetTransitionLevel("Adaptor_SquareToDoor");
                if (Adaptor != null)
                {
                    Adaptor.loadAdjacent = true;
                    M_Level.LevelAssetHolder AdaptorHolder = M_Level.LevelAssetHolder.GetNewHolderFromLevel(Adaptor);
                    AdaptorHolder.region = region;
                    AdaptorHolder.subregion = subregion;
                    subregion.useLevelCount = true;
                    subregion.subregionMinLength = 2;
                    subregion.subregionMaxLength = 2;
                    subregion.subregionOrder = M_Subregion.SubregionOrder.playlist;
                    subregion.levelGroups[0].length = 1;
                    subregion.levelGroups[0].endSubregion = true;
                    subregion.levelGroups.Insert(0, new M_Subregion.LevelGroup
                    {
                        name = "ConnectorAdaptor",
                        length = 1,
                        levelReferences = new List<M_Level.LevelAssetHolder> { AdaptorHolder }
                    });
                }
            }

            if (!db.subRegionAssets.Contains(subregion))
            {
                db.subRegionAssets.Add(subregion);
            }

            region.subregionGroups = new List<M_Region.SubregionGroup>()
{
new M_Region.SubregionGroup()
{
name = "Transition",
subregions = new List<M_Subregion>() { subregion }
}
};

            return region;
        }
        private static void InjectTransitionRegion(
         List<(string levelId, string targetRegion)> transitionData,
         string transFrom,
         string transTo,
         int breakCount,
         string regionKey,
         int slotIndex)
        {
            List<(M_Level.LevelAssetHolder holder, string targetRegion, bool universalBreakroom)> holders =
                new List<(M_Level.LevelAssetHolder, string, bool)>();
            int transitionLevelCount = 0;

            foreach (var data in transitionData)
            {
                M_Level level = GetTransitionLevel(data.levelId);

                if (level == null)
                    continue;

                level.loadAdjacent = true;
                holders.Add((M_Level.LevelAssetHolder.GetNewHolderFromLevel(level), data.targetRegion, false));
                transitionLevelCount++;

                if (data.levelId.StartsWith("From_", StringComparison.OrdinalIgnoreCase))
                {
                    for (int i = 0; i < breakCount; i++)
                    {
                        M_Level breakroom = CreateUniversalBreakroomLevel(regionKey, slotIndex, transFrom);
                        if (breakroom == null)
                            continue;

                        holders.Add((M_Level.LevelAssetHolder.GetNewHolderFromLevel(breakroom), data.targetRegion, true));
                        transitionLevelCount++;
                    }
                }
            }

            InsertConnectorAdaptors(holders);
            transitionLevelCount = holders.Count;

            if (holders.Count == 0)
                return;

            M_Region template =
            GetCachedObject<M_Region>("Region_Silos_Saferoom_Endless");

            if (template == null)
                return;

            M_Region bridgeRegion = UnityEngine.Object.Instantiate(template);
            RegisterGeneratedRegion(bridgeRegion);

            bridgeRegion.name = $"CustomTransition_{transFrom}_To_{transTo}";
            bridgeRegion.regionName = bridgeRegion.name;
            bridgeRegion.useRegionHeight = false;
            bridgeRegion.regionHeight = 0f;
            bridgeRegion.regionOrder = M_Region.RegionOrder.playlist;
            bridgeRegion.introText = "Omicron";
            bridgeRegion.forceStartLevel = false;
            bridgeRegion.transitionLevels = new List<M_Region.TransitionLevels>();

            string transitionTargetAssetName = GetTransitionTargetAssetName(transTo);
            bridgeRegion.nextRegion = string.IsNullOrEmpty(transitionTargetAssetName)
                ? null
                : GetAndFilterRegion(transitionTargetAssetName);


            var db = CL_AssetManager.GetBaseAssetDatabase();

            if (!db.regionAssets.Contains(bridgeRegion))
            {
                db.regionAssets.Add(bridgeRegion);
            }


            M_Subregion subregion = UnityEngine.Object.Instantiate(
            bridgeRegion.subregionGroups[0].subregions[0]);
            RegisterGeneratedSubregion(subregion);

            subregion.name = $"Subregion_{transFrom}_To_{transTo}";
            subregion.subregionName = subregion.name;
            subregion.introText = "Transition";
            subregion.useLevelCount = true;
            subregion.subregionMinLength = transitionLevelCount;
            subregion.subregionMaxLength = transitionLevelCount;
            subregion.subregionOrder = M_Subregion.SubregionOrder.playlist;

            foreach (var item in holders)
            {
                string targetAssetName = GetTransitionTargetAssetName(item.targetRegion);
                item.holder.region = item.universalBreakroom
                    ? bridgeRegion
                    : string.IsNullOrEmpty(targetAssetName)
                        ? null
                        : GetAndFilterRegion(targetAssetName);
                item.holder.subregion = subregion;

            }

            var levelGroups = new List<M_Subregion.LevelGroup>();

            for (int i = 0; i < holders.Count; i++)
            {
                levelGroups.Add(new M_Subregion.LevelGroup()
                {
                    name = $"Transition_{i + 1}",
                    length = 1,
                    endSubregion = i == holders.Count - 1,
                    levelReferences = new List<M_Level.LevelAssetHolder>()
{
holders[i].holder
}
                });
            }

            subregion.levelGroups = levelGroups;
            if (!db.subRegionAssets.Contains(subregion))
            {
                db.subRegionAssets.Add(subregion);
            }

            bridgeRegion.subregionGroups = new List<M_Region.SubregionGroup>()
{
new M_Region.SubregionGroup()
{
name = "Transition",
subregions = new List<M_Subregion>()
{
subregion
}
}
};
            EndlessMode.regions.Add(bridgeRegion);
        }
        public static M_Region BuildRegionFromRegistry(
        string regionName,
        string[] customLevelAssetNames,
        KeyValuePair<string, string>[] subregionPairs,
        bool random)
        {
            M_Region newRegion = ScriptableObject.CreateInstance<M_Region>();
            newRegion.name = regionName;
            newRegion.regionName = regionName;
            newRegion.introText = regionName;
            newRegion.subregionGroups = new List<M_Region.SubregionGroup>();
            newRegion.useRegionHeight = true;
            newRegion.regionHeight = 0.1f;
            if (random)
            {
                newRegion.regionHeight = -1;
            }

            newRegion.regionOrder = M_Region.RegionOrder.shuffle;
            if (random)
            {
                newRegion.regionOrder = M_Region.RegionOrder.playlist;
            }
            var db = CL_AssetManager.GetBaseAssetDatabase();

            foreach (var pair in subregionPairs)
            {
                M_Region.SubregionGroup mainGroup = new M_Region.SubregionGroup();
                mainGroup.name = regionName + "_" + pair.Key + "_Group";
                mainGroup.subregions = new List<M_Subregion>();
                M_Subregion subregion = ScriptableObject.CreateInstance<M_Subregion>();
                subregion.name = regionName + "_" + pair.Key;
                subregion.subregionName = pair.Value;
                subregion.introText = pair.Value;
                subregion.levelGroups = new List<M_Subregion.LevelGroup>();
                subregion.subregionOrder = M_Subregion.SubregionOrder.standard;
                subregion.subregionHeight = 0;
                subregion.useLevelCount = true;


                foreach (string assetName in customLevelAssetNames)
                {
                    if (assetName.Contains(pair.Key))
                    {
                        M_Level level = CustomLevelRegistry.Load(assetName);

                        if (level != null)
                        {
                            level.pauseGeneration = false;
                            level.region = newRegion;
                            level.lastLevel = false;

                            subregion.subregionHeight += 1;
                            if (random)
                            {
                                newRegion.regionHeight += 1;
                            }
                            M_Level.LevelAssetHolder holder = M_Level.LevelAssetHolder.GetNewHolderFromLevel(level);
                            holder.region = newRegion;
                            holder.subregion = subregion;
                            holder.length = 1;

                            M_Subregion.LevelGroup group = new M_Subregion.LevelGroup();
                            group.name = assetName;
                            group.length = 1;
                            group.levelReferences = new List<M_Level.LevelAssetHolder> { holder };

                            subregion.levelGroups.Add(group);
                        }
                    }
                }

                if (subregion.levelGroups.Count > 0)
                {
                    if (!db.subRegionAssets.Contains(subregion)) db.subRegionAssets.Add(subregion);
                    mainGroup.subregions.Add(subregion);
                    newRegion.subregionGroups.Add(mainGroup);
                }
                else UnityEngine.Object.Destroy(subregion);
            }

            if (newRegion.subregionGroups.Count == 0)
            {
                UnityEngine.Object.Destroy(newRegion);
                return null;
            }
            if (!db.regionAssets.Contains(newRegion)) db.regionAssets.Add(newRegion);
            RegisterCustomRegion(regionName, newRegion);
            return newRegion;
        }
    }

}
