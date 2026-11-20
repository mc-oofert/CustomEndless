using HarmonyLib;
using MortarLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;
using static WorldLoader;

// Are not you glad that I didn't keep this all as one file?

//This was beautiful once
//Reaching up like antenni to heaven
//I reached out to code
//And I fell

namespace Celestial.CustomEndless
{

    public static partial class EndlessConfiguration
    {
        public static M_Gamemode EndlessMode;
        private static bool added;
        private static Dictionary<string, UnityEngine.Object> regionCache =
        new Dictionary<string, UnityEngine.Object>();
        public static bool BEGONE_FOUL_SPIRIT;
        public static bool giveStartingItems;

        public static Dictionary<string, bool> SettingToggles =
        new Dictionary<string, bool>();
        public static Dictionary<string, int> SettingCounts =
        new Dictionary<string, int>();
        public static Dictionary<string, float> SettingFloats =
        new Dictionary<string, float>();
        public static bool canWin = true;
        public static bool CanWin => canWin || CustomUIManager.GetToggleState("campaign_or_challenge_win");

        public static string CleanRegionName(string _regionName)
        {
            var regionName = _regionName.Replace("_RuntimeCopy", "");
            return Regex.Replace(regionName, @"_slot_[0-9]+", "");
        }

        public static void ApplyConfiguredBuffs()
        {
            ENT_Player player = ENT_Player.GetPlayer();
            if (player == null)
                return;

            foreach (var buff in EndlessSettings.buffSettings)
            {
                float amount = CustomUIManager.GetFloatState(buff.Key);

                if (Mathf.Approximately(amount, 0f))
                    continue;

                var perk =
                new PerkBuilder($"endless_{buff.Key}", buff.Name)
                .WithBuff($"endless_{buff.Key}_buff", buff.BuffId, amount)
                .Build();

                player.AddPerk(perk);
            }

            if (CustomUIManager.GetToggleState("buff_inf_charge"))
            {
                player.AddPerk(
                new PerkBuilder("endless_infcharge", "Infinite Charge")
                .WithBuff("endless_infcharge_buff", "infiniteCharge", 1f)
                .Build());
            }

            if (CustomUIManager.GetToggleState("buff_hang_grounded"))
            {
                player.AddPerk(
                new PerkBuilder("endless_grounded", "Grounded While Hanging")
                .WithBuff("endless_grounded_buff",
                GameUtils.Buffs.HangingGrounded, 1f)
                .Build());
            }
        }
        private static void DestroyMassInstance()
        {
            if (DEN_DeathFloor.instance != null)
            {
                DEN_DeathFloor.instance.SetActive(false);
                DEN_DeathFloor.instance.SetHeight(-1000);
            }
        }
        private static void RemoveColdDebuff()
        {
            ENT_Player player = ENT_Player.GetPlayer();

            if (player == null || player.curBuffs == null)
                return;

            if (player.curBuffs.HasBuffContainer("freezing"))
            {
                player.curBuffs.RemoveBuffContainer("freezing");
            }
        }

        public static CH_RoachCollector collector;
        public static string GetLogicalRegionName(M_Level level)
        {
            if (level == null)
                return null;

            string regionName = level.region != null ? level.region.name : string.Empty;
            string levelName = level.levelName ?? string.Empty;

            regionName = CleanRegionName(regionName);

            foreach (var pair in RegionDefinitions)
            {
                string logicalName = pair.Key;
                RegionDefinition definition = pair.Value;

                if (!string.IsNullOrEmpty(definition.RegionAsset) &&
                regionName.Equals(definition.RegionAsset, StringComparison.OrdinalIgnoreCase))
                {
                    return logicalName;
                }

                if (!string.IsNullOrEmpty(definition.BreakroomAsset) &&
                regionName.Equals(definition.BreakroomAsset, StringComparison.OrdinalIgnoreCase))
                {
                    return logicalName;
                }

                if (regionName.Equals(logicalName, StringComparison.OrdinalIgnoreCase))
                {
                    return logicalName;
                }
            }

            if (regionName.StartsWith("CustomTransition_", StringComparison.OrdinalIgnoreCase))
            {
                int toIndex = regionName.LastIndexOf("_To_", StringComparison.OrdinalIgnoreCase);

                if (toIndex >= 0)
                {
                    string destination = regionName.Substring(toIndex + 4);

                    foreach (var pair in RegionDefinitions)
                    {
                        if (destination.Equals(pair.Key, StringComparison.OrdinalIgnoreCase))
                        {
                            return pair.Key;
                        }
                    }
                }
            }

            foreach (var pair in RegionDefinitions)
            {
                string logicalName = pair.Key;
                RegionDefinition definition = pair.Value;

                if (!string.IsNullOrEmpty(definition.RegionAsset) &&
                levelName.ContainsInsensitive(definition.RegionAsset))
                {
                    return logicalName;
                }

                if (levelName.ContainsInsensitive(logicalName))
                {
                    return logicalName;
                }
            }

            return null;
        }

        public static void GetUnseenPopulationForLevel(M_Level level, out int hostile, out int helpful)
        {
            hostile = 0;
            helpful = 0;
            AddUnseenAmount(CustomUIManager.GetCountState("fun_unseen_amount"), ref hostile, ref helpful);
            if (level == null) return;
            string logicalRegion = GetLogicalRegionName(level);
            if (string.IsNullOrEmpty(logicalRegion)) return;
            int slotNum = GetSlotNumber(level);
            int regional = GetCountStateForSlot(logicalRegion.ToLowerInvariant() + "_unseen", logicalRegion.ToLowerInvariant(), slotNum);
            AddUnseenAmount(regional, ref hostile, ref helpful);
        }

        private static void AddUnseenAmount(int value, ref int hostile, ref int helpful)
        {
            if (value == int.MinValue) return;
            if (value < 0) helpful += Mathf.Abs(value);
            else hostile += value;
        }

        public static int GetCimimPopulationForLevel(M_Level level)
        {
            int total = CustomUIManager.GetCountState("fun_cimim_amount");
            if (level == null) return total;
            string logicalRegion = GetLogicalRegionName(level);
            if (string.IsNullOrEmpty(logicalRegion)) return total;
            int slotNum = GetSlotNumber(level);
            int regional = GetCountStateForSlot(logicalRegion.ToLowerInvariant() + "_cimim", logicalRegion.ToLowerInvariant(), slotNum);
            if (regional != int.MinValue) total += regional;
            return total;
        }

        public static int GetRemainsPopulationForLevel(M_Level level)
        {
            if (level == null) return 0;
            M_Region campaignRegion = GetCampaignRegionForLevel(level);
            string logicalRegion = campaignRegion != null ? "Campaign" : GetLogicalRegionName(level);
            if (string.IsNullOrEmpty(logicalRegion) || logicalRegion == "Duct") return 0;
            int slotNum = GetSlotNumber(campaignRegion != null ? campaignRegion : level.region);
            string key = logicalRegion.ToLowerInvariant();
            return Mathf.Max(0, GetCountStateForSlot(key + "_remains", key, slotNum));
        }

        private static int GetSlotNumber(M_Level level)
        {
            return GetSlotNumber(level != null ? level.region : null);
        }

        private static int GetSlotNumber(M_Region region)
        {
            string name = region != null ? region.name : string.Empty;
            const string marker = "_slot_";
            int start = name.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0) return -1;
            start += marker.Length;
            int end = start;
            while (end < name.Length && char.IsDigit(name[end])) end++;
            return int.TryParse(name.Substring(start, end - start), out int slot) ? slot : -1;
        }

        private static int GetFaceCountForLevel(M_Level level)
        {
            if (level == null || level.region == null) return 0;
            string logicalRegion = GetLogicalRegionName(level);

            int slotNum = GetSlotNumber(level);
            if (CustomUIManager.GetCountState("slot_" + slotNum + "_faces", -1) != -1)
                return CustomUIManager.GetCountState("slot_" + slotNum + "_faces");

            if (string.IsNullOrEmpty(logicalRegion))
                return 0;

            return CustomUIManager.GetCountState(logicalRegion.ToLowerInvariant() + "_faces");
        }

        private static bool gaveFlashlight = false;
        private static bool gaveGun = false;
        public static int cycles = 0;
        private static M_Region lastRegion = null;
        public static bool restoreMass = false;
        private static bool stageTwo = false;
        public static List<DEN_Face> spawnedFaces = new List<DEN_Face>();
        public static bool cutOff = false;
        private static CancellationTokenSource teleportCTS;
        public static M_Level last_entered = null;
        private static M_Level last_entered2 = null;
        public static List<int> last_entered3 = new List<int>();

        public static GameObject SpawnEntity(string name, M_Level level)
        {
            if (level == null || level.levelEntrance == null || string.IsNullOrEmpty(name)) return null;
            Transform enemyPrefab = GetCachedObject<Transform>(name);
            if (enemyPrefab != null)
            {
                GameObject entity = UnityEngine.Object.Instantiate(enemyPrefab.gameObject, level.levelEntrance.position, Quaternion.identity);
                entity.transform.SetParent(level.transform);
                return entity;
            }
            return null;
        }

        static async Task UnloadPreviousLevels(M_Level currentLevel)
        {
            WorldLoader loader = WorldLoader.instance;
            ENT_Player player = ENT_Player.GetPlayer();
            while (loader != null && loader == WorldLoader.instance && player != null && player == ENT_Player.GetPlayer() && currentLevel != null && currentLevel == last_entered && currentLevel.levelEntrance != null && CL_GameManager.GetCurrentGamemode() == EndlessMode)
            {
                Transform entrance = currentLevel.levelEntrance;
                if (player.transform.position.y >= entrance.position.y + 5 && Vector3.Dot(player.transform.position - entrance.position, entrance.forward) > 0)
                {
                    loader.UnloadPreviousLevels(0);
                    if (currentLevel != null && entrance != null && CustomUIManager.GetToggleState("fun_consumah") && DEN_DeathFloor.instance != null && DEN_DeathFloor.instance.IsActiveAndMoving()) DEN_DeathFloor.instance.MoveToHeightOfTransform(entrance);
                    return;
                }
                await Task.Delay(1000);
            }
        }

        static async Task TeleportAtEndOfLevel(M_Level currentLevel, CancellationToken token)
        {
            try
            {
                while (ENT_Player.GetPlayer().transform.position.y < currentLevel.levelExit.position.y - 200) await Task.Delay(100, token);

                string[] args = { };
                TeleportPlayerToNextLevel(args);
            }
            catch (TaskCanceledException)
            {

            }
        }
        private static bool TeleportToNext(M_Level level)
        {
            if (level == null || ENT_Player.playerObject == null) return false;
            ENT_Player.playerObject.Teleport(level.GetSpawnPosition());
            CommandConsole.Log("Teleported to Next Level");
            return true;
        }
        private static M_Level pendingTeleportLevel;

        public static void TeleportPlayerToNextLevel(string[] args)
        {
            TeleportPlayerToNextLevel(args, null);
        }

        public static void TeleportPlayerToNextLevel(string[] args, Action<bool> completed)
        {
            WorldLoader loader = WorldLoader.instance;
            ENT_Player player = ENT_Player.GetPlayer();
            if (loader == null || loader.currentBranch == null || loader.currentBranch.currentLevel == null || player == null) { completed?.Invoke(false); return; }
            BranchInfo branch = loader.currentBranch;
            M_Level source = branch.currentLevel.GetLevel();
            if (source == null || pendingTeleportLevel == source || branch.GetLevelIds() == null || branch.levelTracker == null) { completed?.Invoke(false); return; }
            M_Level next = FindNextLevel();
            if (next != null)
            {
                bool teleported = TeleportToNext(next);
                completed?.Invoke(teleported);
                return;
            }
            pendingTeleportLevel = source;
            loader.StartCoroutine(WaitThenTeleportToNext());

            M_Level FindNextLevel()
            {
                List<BranchInfo.LevelId> idsAfter = branch.GetLevelIdsAfterLevel(source);
                if (idsAfter.Count == 0) return null;
                var property = typeof(LevelInfo).GetProperty("levelID", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    ?? typeof(LevelInfo).GetProperty("LevelID", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var field = typeof(LevelInfo).GetField("levelID", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    ?? typeof(LevelInfo).GetField("LevelID", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (LevelInfo info in branch.levelTracker)
                {
                    if (info == null) continue;
                    object id = property != null ? property.GetValue(info) : field?.GetValue(info);
                    if (Equals(id, idsAfter[0])) return info.GetLevel();
                }
                return null;
            }

            IEnumerator WaitThenTeleportToNext()
            {
                bool teleported = false;
                try
                {
                    CommandConsole.Log("Began loading next levels.");
                    source.ContinueGeneration();
                    float deadline = Time.realtimeSinceStartup + 60f;
                    while (loader != null && loader == WorldLoader.instance && player != null && player == ENT_Player.GetPlayer() && source != null && loader.currentBranch == branch && branch.currentLevel != null && branch.currentLevel.GetLevel() == source)
                    {
                        if (isLoaded)
                        {
                            M_Level destinationLevel = FindNextLevel();
                            if (destinationLevel != null)
                            {
                                teleported = TeleportToNext(destinationLevel);
                                yield break;
                            }
                        }
                        if (Time.realtimeSinceStartup >= deadline)
                        {
                            Debug.LogWarning("Next-level teleport stopped because no destination finished loading.");
                            yield break;
                        }
                        yield return null;
                    }
                }
                finally
                {
                    if (pendingTeleportLevel == source) pendingTeleportLevel = null;
                    completed?.Invoke(teleported);
                }
            }
        }


        [HarmonyPatch(typeof(LevelInfo), nameof(LevelInfo.DestroyLevelObject))]
        private static class KeepOccupiedLevelsLoadedPatch
        {
            private static bool Prefix(LevelInfo __instance)
            {
                if (ductUnloadInProgress)
                    return true;

                M_Level level = __instance != null ? __instance.GetLevel() : null;
                ENT_Player player = ENT_Player.GetPlayer();
                if (level == null || player == null || !IsLastLevelBeforeRegion(__instance))
                    return true;

                return !IsPlayerWithinLevel(level, player.transform.position);
            }
        }

        internal static bool IsLastLevelBeforeRegion(LevelInfo info)
        {
            if (info == null || info.branch == null || info.branch.levelTracker == null)
                return false;

            M_Level level = info.GetLevel();
            string levelName = level != null ? level.levelName : info.GetLevelAssetReference()?.id;
            if (!string.IsNullOrEmpty(levelName) &&
                levelName.ContainsInsensitive("to_") &&
                levelName.ContainsInsensitive("interlude"))
                return false;

            BranchInfo branch = info.branch;
            int index = branch.levelTracker.IndexOf(info);
            M_Region currentRegion = GetLevelRegion(info);
            if (currentRegion == null)
                return false;

            int nextIndex = index + 1;
            if (index < 0 && branch.parentLevel == info)
                nextIndex = 0;
            else if (index < 0)
                return false;

            for (int i = nextIndex; i < branch.levelTracker.Count; i++)
            {
                M_Region nextRegion = GetLevelRegion(branch.levelTracker[i]);
                if (nextRegion != null)
                    return nextRegion != currentRegion;
            }

            return false;
        }

        private static M_Region GetLevelRegion(LevelInfo info)
        {
            if (info == null)
                return null;

            M_Level level = info.GetLevel();
            if (level != null && level.region != null)
                return level.region;

            M_Level.LevelAssetHolder holder = info.GetLevelAssetReference();
            return holder != null ? holder.region : null;
        }

        internal static bool IsPlayerWithinLevel(M_Level level, Vector3 position)
        {
            if (level == null)
                return false;

            if (level.levelEntrance != null && level.levelExit != null)
            {
                bool inFrontOfEntrance = Vector3.Dot(position - level.levelEntrance.position, level.levelEntrance.forward) >= 0f;
                bool behindExit = Vector3.Dot(position - level.levelExit.position, level.levelExit.forward) <= 0f;
                if (inFrontOfEntrance && behindExit)
                    return true;
            }

            return level.GetLocalBounds().Contains(level.transform.InverseTransformPoint(position));
        }

        [HarmonyPatch(typeof(GameObject), nameof(GameObject.SetActive), new[] { typeof(bool) })]
        private static class KeepOccupiedLevelActivePatch
        {
            private static bool Prefix(GameObject __instance, bool value)
            {
                if (value || ductUnloadInProgress || CL_GameManager.GetCurrentGamemode() != EndlessMode)
                    return true;

                M_Level level = __instance != null ? __instance.GetComponent<M_Level>() : null;
                ENT_Player player = ENT_Player.GetPlayer();
                WorldLoader loader = WorldLoader.instance;
                return level == null || player == null || loader == null || !IsGuardedTrackedLevel(loader, level) ||
                       !IsPlayerWithinLevel(level, player.transform.position);
            }

            private static bool IsGuardedTrackedLevel(WorldLoader loader, M_Level level)
            {
                if (IsGuardedInBranch(loader.currentBranch, level))
                    return true;

                if (loader.branchTracker == null)
                    return false;

                foreach (BranchInfo branch in loader.branchTracker)
                {
                    if (IsGuardedInBranch(branch, level))
                        return true;
                }

                return false;
            }

            private static bool IsGuardedInBranch(BranchInfo branch, M_Level level)
            {
                if (branch == null)
                    return false;

                if (branch.parentLevel != null && branch.parentLevel.GetLevel() == level && IsLastLevelBeforeRegion(branch.parentLevel))
                    return true;

                if (branch.levelTracker == null)
                    return false;

                foreach (LevelInfo info in branch.levelTracker)
                {
                    if (info != null && info.GetLevel() == level && IsLastLevelBeforeRegion(info))
                        return true;
                }

                return false;
            }
        }

        [HarmonyPatch(typeof(M_Level), "OnLevelActivate")]

        private class SubregionDefinition
        {
            public string DisplayName;
            public string ReplacementLevelAsset;

            public SubregionDefinition(string displayName, string replacementLevelAsset)
            {
                DisplayName = displayName;
                ReplacementLevelAsset = replacementLevelAsset;
            }
        }

        private static readonly Dictionary<string, SubregionDefinition> SubregionDefinitions =
        new Dictionary<string, SubregionDefinition>
        {
{ "Storage",new SubregionDefinition("", "") },
{ "Ventilation",new SubregionDefinition("", "") },
{ "Chambers",new SubregionDefinition("", "") },
{ "Drainage",new SubregionDefinition("", "") },
{ "Waste",new SubregionDefinition("", "") },
{ "Organ",new SubregionDefinition("", "") },
{ "Shaft",new SubregionDefinition("", "") },
{ "LostPier",new SubregionDefinition("", "") },
{ "Labs",new SubregionDefinition("", "") },
{ "Scaffolds",new SubregionDefinition("", "") },
{ "Handle",new SubregionDefinition("", "") },
{ "Gardens",new SubregionDefinition("", "") },
{ "Zone",new SubregionDefinition("", "") },
{ "Infested",new SubregionDefinition("", "") },
{ "Trough",new SubregionDefinition("", "") },
{ "Clean",new SubregionDefinition("", "") },
{ "Decrepit",new SubregionDefinition("", "") },
{ "Void",new SubregionDefinition("", "") },
{ "Crystal",new SubregionDefinition("", "") },
{ "Frigid",new SubregionDefinition("", "") },
{ "Glacial",new SubregionDefinition("", "") },
{ "Icebox",new SubregionDefinition("", "MX_Chimney_Transition_Icebox_To_Spire_01") },
{ "Spire",new SubregionDefinition("", "MX_Chimney_Transition_Spire_To_Shelf_01") },
{ "Shelf",new SubregionDefinition("", "MX_Chimney_Transition_Shelf_To_Frigid_01") },
{ "Chimney_Transition_Interlude",new SubregionDefinition("", "MX_Chimney_Transition_Shelf_To_Frigid_01") },
{ "Corrupted",new SubregionDefinition("", "") },
{ "Prototype",new SubregionDefinition("", "") }
        };

        private static readonly Dictionary<string, string> StartingItemTitles =
        new Dictionary<string, string> {
{ "item_timepiece", "Timepiece" },
{ "item_translocator", "Translocator" },
{ "item_evaglove", "EVA Glove" },
{ "item_rapier", "Rapier" },
{ "item_rebar_return", "Rho Spear" },
{ "item_remote", "Remote" },
{ "item_handgun_kit", "Handgun" },
{ "item_flaregun_kit", "Flaregun" },
{ "item_flashlight", "Flashlight" },
{ "item_cryogun", "Cryogun" }
        };

        private static Dictionary<string, string> StartingPerkTitles =>
        (CL_AssetManager.GetFullCombinedAssetDatabase()?.perkAssets ?? new List<Perk>())
        .Where(p => p != null && !string.IsNullOrEmpty(p.id))
        .OrderBy(p => p.title)
        .GroupBy(p => p.id)
        .Select(group => group.First())
        .ToDictionary(
        p => p.id,
        p => p.title
        );

        public static Sprite LoadSprite(string fileName, float pixelsPerUnit = 100.0f)
        {
            string modFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string filePath = Path.Combine(modFolder, fileName);

            if (!File.Exists(filePath))
            {
                return null;
            }

            byte[] fileData = File.ReadAllBytes(filePath);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Type imageConversion = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            MethodInfo loadImage = imageConversion?.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(method =>
            {
                if (method.Name != "LoadImage") return false;
                ParameterInfo[] parameters = method.GetParameters();
                return parameters.Length >= 2 && parameters[0].ParameterType == typeof(Texture2D) && parameters[1].ParameterType == typeof(byte[]);
            });
            if (loadImage == null)
            {
                UnityEngine.Object.Destroy(texture);
                return null;
            }
            ParameterInfo[] imageParameters = loadImage.GetParameters();
            object[] imageArguments = imageParameters.Length >= 3 ? new object[] { texture, fileData, false } : new object[] { texture, fileData };
            if (!(bool)loadImage.Invoke(null, imageArguments))
            {
                UnityEngine.Object.Destroy(texture);
                return null;
            }

            return Sprite.Create(
            texture,
            new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            pixelsPerUnit
            );
        }




        private static readonly Dictionary<string, UnityEngine.Object> objectCache = new Dictionary<string, UnityEngine.Object>();

        public static T GetCachedObject<T>(string name) where T : UnityEngine.Object
        {
            if (objectCache.TryGetValue(name, out UnityEngine.Object cached) && cached != null && cached is T typedCached)
            {
                return typedCached;
            }

            foreach (T obj in Resources.FindObjectsOfTypeAll<T>())
            {
                if (obj != null && obj.name == name)
                {
                    objectCache[name] = obj;
                    return obj;
                }
            }
            return null;
        }

        private static Event_Cold GetInstanceInternal()
        {
            System.Reflection.FieldInfo field = typeof(Event_Cold).GetField("instance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            return field?.GetValue(null) as Event_Cold;
        }

        private static Event_Cold GetOrSpawnEventCold()
        {
            Event_Cold currentInstance = GetInstanceInternal();
            if (currentInstance != null)
            {
                return currentInstance;
            }

            Event_Cold sceneInstance = UnityEngine.Object.FindObjectOfType<Event_Cold>(true);
            if (sceneInstance != null)
            {
                return sceneInstance;
            }

            foreach (Event_Cold template in Resources.FindObjectsOfTypeAll<Event_Cold>())
            {
                if (template != null)
                {
                    GameObject clone = UnityEngine.Object.Instantiate(template.gameObject);
                    clone.name = "Event_Cold_Spawned";
                    return clone.GetComponent<Event_Cold>();
                }
            }

            return null;
        }

        private static void DisableEventCold(Event_Cold cold)
        {
            if (cold == null) return;

            Event_Cold.SetEventStatus(false);

            if (cold.snowEffect != null)
            {
                cold.snowEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            if (cold.blizzardSnow != null)
            {
                cold.blizzardSnow.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            if (cold.blizzardAudio != null)
            {
                cold.blizzardAudio.Stop();
                cold.blizzardAudio.volume = 0f;
            }

            cold.gameObject.SetActive(false);
        }

    }
}
