using System;
using System.Collections.Generic;

namespace Celestial.CustomEndless
{
    public static partial class EndlessConfiguration
    {
        private class RegionDefinition
        {
            public string RegionAsset;
            public string BreakroomAsset;
        }

        private static readonly Dictionary<string, RegionDefinition> RegionDefinitions =
        new Dictionary<string, RegionDefinition>();

        private static readonly Dictionary<(string From, string To), string> TransitionDefinitions =
        new Dictionary<(string, string), string>();

        private static readonly List<string> RegionOrder = new List<string>();
        private static readonly HashSet<M_Region> CampaignGenerationRegions = new HashSet<M_Region>();
        private static readonly BreakroomEntityDefinition[] BreakroomEntities =
        {
            new BreakroomEntityDefinition("PerkMachine", "Prop_UpgradeConsole_Main_Endless", "breakroom_perk_machine_every", "breakroom_perk_machine_after"),
            new BreakroomEntityDefinition("ExperimentalMachine", "UpgradeConsole", "breakroom_experimental_machine_every", "breakroom_experimental_machine_after"),
            new BreakroomEntityDefinition("ATM", "Prop_ATM", "breakroom_atm_every", "breakroom_atm_after"),
            new BreakroomEntityDefinition("VendingMachineT2", "Prop_VendingMachine_T2", "breakroom_vending_t2_every", "breakroom_vending_t2_after", false, true),
            new BreakroomEntityDefinition("Recycler", "Prop_Recycler", "breakroom_recycler_every", "breakroom_recycler_after"),
            new BreakroomEntityDefinition("CryoRefueler", "CryoRefueler_Endless", "breakroom_cryo_refueler_every", "breakroom_cryo_refueler_after"),
            new BreakroomEntityDefinition("VendingMachine", "Prop_VendingMachine.01", "breakroom_vending_every", "breakroom_vending_after", false, true),
            new BreakroomEntityDefinition("RhoMonolith", "Rho_Monolith", "breakroom_rho_monolith_every", "breakroom_rho_monolith_after", true),
            new BreakroomEntityDefinition("ManholeHatch", "ManholeHatch", "breakroom_manhole_hatch_every", "breakroom_manhole_hatch_after")
        };
        private static int universalBreakroomVisit;
        private static int universalBreakroomIndex;

        private sealed class BreakroomEntityDefinition
        {
            public readonly string Name;
            public readonly string SearchName;
            public readonly string EveryKey;
            public readonly string AfterKey;
            public readonly bool RemoveSpawnChance;
            public readonly bool UsesForwardVector;

            public BreakroomEntityDefinition(
                string name,
                string searchName,
                string everyKey,
                string afterKey,
                bool removeSpawnChance = false,
                bool usesForwardVector = false)
            {
                Name = name;
                SearchName = searchName;
                EveryKey = everyKey;
                AfterKey = afterKey;
                RemoveSpawnChance = removeSpawnChance;
                UsesForwardVector = usesForwardVector;
            }
        }

        private static bool UseDefaultTransitionsAndBreakrooms =>
        CustomUIManager.GetToggleState("use_default_transitions_and_breakrooms");




        private static void RegisterRegion(
        string name,
        string regionAsset,
        string breakroomAsset,
        params (string To, string Transition)[] transitions)
        {
            RegionDefinitions[name] = new RegionDefinition
            {
                RegionAsset = regionAsset,
                BreakroomAsset = breakroomAsset
            };

            if (!RegionOrder.Contains(name))
                RegionOrder.Add(name);

            foreach (var transition in transitions)
                TransitionDefinitions[(name, transition.To)] = transition.Transition;
        }
        private static M_Region transition_intro;
        private static WKAssetDatabase registeredRegionDatabase;
        private static int registeredRegionScene = -1;
        private static readonly Dictionary<string, M_Region> registeredCustomRegions = new Dictionary<string, M_Region>(StringComparer.OrdinalIgnoreCase);

        private static void RegisterCustomRegion(string name, M_Region region)
        {
            if (string.IsNullOrEmpty(name) || region == null) return;
            registeredCustomRegions[name] = region;
        }

        private static bool TryGetRegisteredCustomRegion(string name, out M_Region region)
        {
            if (registeredCustomRegions.TryGetValue(name, out region) && region != null) return true;
            registeredCustomRegions.Remove(name);
            region = null;
            return false;
        }

        private static void RegisterRegions()
        {
            RegisterPlayerRegions();
            WKAssetDatabase database = CL_AssetManager.GetBaseAssetDatabase();
            if (database == null) return;
            int scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetHashCode();
            if (registeredRegionDatabase == database && registeredRegionScene == scene && CustomLevelRegistry.Load("From_Silos") != null && CustomLevelRegistry.Load("From_Spawn") != null) return;
            CustomLevelRegistry.Clear();
            registeredCustomRegions.Clear();

            RegisterRegion(
            "Silos",
            "Region_Silos_Endless",
            "Region_Silos_Saferoom_Endless",
            ("Pipeworks", "Transition_Silo_To_Pipeworks_01"));




            RegisterRegion(
            "Pipeworks",
            "Region_Pipeworks_Endless",
            "Region_Pipeworks_Breakroom",
            ("Habitation", "Transition_Pipeworks_To_Habitation"),
            ("Silos", "Transition_Pipeworks_To_Silo"));



            RegisterRegion(
            "Chute",
            "Region_Shortcut_ExpulsionChute",
            "");


            RegisterRegion(
            "Habitation",
            "Region_Habitation_Endless",
            "Region_Habitation_Breakroom",
            ("Abyss", "Transition_Habitation_To_Abyss_01"));


            RegisterRegion(
            "Abyss",
            "Region_Abyss_Endless",
            "Region_Abyss_Breakroom",
            ("Nest", "M5_Nest_Endless_Transition_Intro"),
            ("Silos", "Transition_Abyss_To_Silo"));

            RegisterRegion(
            "Nest",
            "Region_Nest_Endless",
            "Region_Nest_Endless_Breakroom",
            ("Silos", "Transition_Nest_To_Silo"));

            RegisterRegion(
            "Ladder",
            "Region_Ladder_Endless",
            "");

            RegisterRegion(
            "Chimney",
            "Region_Chimney_Endless",
            "");


            RegisterRegion(
            "Sink",
            "Region_Shortcut_TangledSink",
            "");

            RegisterRegion(
            "Duct",
            "Region_Shortcut_RottenDuct",
            "");

            RegisterRegion(
            "Body Cavity",
            "Region_Habitation_BodyCavity",
            "");

            RegisterRegion(
            "Diver",
            "Region_Shortcut_HadalDiver",
            "");


            var level = CustomLevelLoader.LoadRoom("anywhere", "From_Silos", "From_Silos");

            CustomLevelRegistry.Register(
            "From_Silos",
            level.level);

            level = CustomLevelLoader.LoadRoom("anywhere", "From_Silos", "From_Chimney");

            CustomLevelRegistry.Register(
            "From_Chimney",
            level.level);

            level = CustomLevelLoader.LoadRoom("anywhere", "To_Silos", "To_Silos");

            CustomLevelRegistry.Register(
            "To_Silos",
            level.level);

            level = CustomLevelLoader.LoadRoom("anywhere", "To_Silos", "To_Chimney");
            CustomLevelRegistry.Register(
            "To_Chimney",
            level.level);

            level = CustomLevelLoader.LoadRoom("anywhere", "To_Nest", "To_Nest");

            CustomLevelRegistry.Register(
            "To_Nest",
            level.level);
            level = CustomLevelLoader.LoadRoom("anywhere", "From_Nest", "From_Nest");

            CustomLevelRegistry.Register(
            "From_Nest",
            level.level);


            level = CustomLevelLoader.LoadRoom("anywhere", "To_Ladder", "To_Ladder");

            CustomLevelRegistry.Register(
            "To_Ladder",
            level.level);


            level = CustomLevelLoader.LoadRoom("anywhere", "From_Ladder", "From_Ladder");
            CustomLevelRegistry.Register(
            "From_Ladder",
            level.level);


            level = CustomLevelLoader.LoadRoom("anywhere", "To_Abyss", "To_Abyss");
            CustomLevelRegistry.Register(
            "To_Abyss",
            level.level);

            level = CustomLevelLoader.LoadRoom("anywhere", "From_Abyss", "From_Abyss");
            CustomLevelRegistry.Register(
            "From_Abyss",
            level.level);

            level = CustomLevelLoader.LoadRoom("anywhere", "To_Habitation", "To_Habitation");
            CustomLevelRegistry.Register(
            "To_Habitation",
            level.level);

            level = CustomLevelLoader.LoadRoom("anywhere", "From_Habitation", "From_Habitation");
            CustomLevelRegistry.Register(
            "From_Habitation",
            level.level);

            level = CustomLevelLoader.LoadRoom("anywhere", "To_Pipeworks", "To_Pipeworks");
            CustomLevelRegistry.Register(
            "To_Pipeworks",
            level.level);

            level = CustomLevelLoader.LoadRoom("anywhere", "To_Duct", "To_Duct");
            CustomLevelRegistry.Register(
            "To_Duct",
            level.level);

            M_Level cavityEntrance = UnityEngine.Object.Instantiate(level.level);
            RegisterRuntimeObject(cavityEntrance.gameObject, false);
            cavityEntrance.name = "To_Cavity";
            cavityEntrance.levelName = "To_Cavity";
            cavityEntrance.gameObject.SetActive(false);
            CustomLevelRegistry.Register("To_Cavity", cavityEntrance);

            level = CustomLevelLoader.LoadRoom("anywhere", "To_Diver", "To_Diver");
            if (level != null && level.level != null && level.level.levelEntrance != null && level.level.levelExit != null &&
                FindChildByName(level.level.transform, "Lever") != null)
                CustomLevelRegistry.Register("To_Diver", level.level);

            level = CustomLevelLoader.LoadRoom("anywhere", "From_Duct", "From_Duct");
            CustomLevelRegistry.Register(
            "From_Duct",
            level.level);

            level = CustomLevelLoader.LoadRoom("anywhere", "From_Pipeworks", "From_Pipeworks");
            CustomLevelRegistry.Register(
            "From_Pipeworks",
            level.level);


            level = CustomLevelLoader.LoadRoom("anywhere", "Assets", "Assets");
            CustomLevelRegistry.Register(
            "Assets",
            level.level);

            level = CustomLevelLoader.LoadRoom("anywhere", "Breakroom", "Universal_Breakroom");
            CustomLevelRegistry.Register(
            "Universal_Breakroom",
            level.level);
            if (!Plugin.DumbItDown)
            {
                RegisterRegionBreakrooms();
                RegisterConnectorAdaptors();
                RegisterAdditionalTransitionRooms();
            }



            CustomLevelRegistry.Register(
            "Pier Intro",
            GetCachedObject<M_Level>("M3_Habitation_Pier_Entrance_01"));

            M_Level shaftTemplate = GetCachedObject<M_Level>("M3_Habitation_Shaft_To_Pier");
            if (shaftTemplate == null || shaftTemplate.levelEntrance == null || shaftTemplate.levelExit == null) return;
            level = M_Level.LevelAssetHolder.GetNewHolderFromLevel(shaftTemplate);
            level.level = UnityEngine.Object.Instantiate(level.level);
            RegisterRuntimeObject(level.level.gameObject, false);
            Transform trans = new GameObject("transformer").transform;
            trans.SetParent(level.level.gameObject.transform);
            trans.position = level.level.levelExit.position + Vector3.up * 1f;
            trans.Rotate(0, 180, 0);
            level.level.spawnPosition = trans;
            level.level.levelEntrance.Rotate(90, 0, 0);
            CustomLevelRegistry.Register(
            "Shaft To Pier",
            level.level
            );
            level.level.gameObject.SetActive(false);

            level = CustomLevelLoader.LoadRoom("anywhere", "P-SpaceSpawn", "Omicron-Prototype-1");
            trans = new GameObject("transformer").transform;
            trans.SetParent(level.level.gameObject.transform);
            trans.position = level.level.levelEntrance.position + Vector3.up * 15f;
            level.level.levelExit.position += Vector3.up * 100f;
            level.level.spawnPosition = trans;
            CustomLevelRegistry.Register(
            "Omicron-Prototype-1",
            level.level);


            CustomLevelRegistry.Register(
            "Pier Start",
            GetCachedObject<M_Level>("M3_Habitation_Shaft_Intro"));
            CustomLevelRegistry.Register(
            "Lobby",
            GetCachedObject<M_Level>("M3_Habitation_Lab_Lobby"));
            CustomLevelRegistry.Register(
            "Spire_Transition",
            GetCachedObject<M_Level>("MX_Chimney_Transition_Spire_To_Shelf_01"));
            CustomLevelRegistry.Register(
            "Shelf_Transition",
            GetCachedObject<M_Level>("MX_Chimney_Transition_Shelf_To_Frigid_01"));
            CustomLevelRegistry.Register(
            "Icebox_Transition",
            GetCachedObject<M_Level>("MX_Chimney_Transition_Icebox_To_Spire_01"));

            M_Level level22 = GetCachedObject<M_Level>("MH_Extraction_Prison_01");
            if (level22 == null || level22.spawnPosition == null) return;
            //level22.spawnPosition.Rotate(0, -90, 0);
            trans = new GameObject().transform;
            trans.SetParent(level22.transform);
            trans.position = level22.spawnPosition.position;
            trans.rotation = level22.spawnPosition.rotation;
            trans.Rotate(0, 180, 0);
            level22.levelEntrance = trans;
            CustomLevelRegistry.Register(
            "Parasite_Prison",
            level22);
            CustomLevelRegistry.Register(
            "Interlude_1",
            GetCachedObject<M_Level>("Campaign_Interlude_Pipeworks_To_Habitation_01"));
            CustomLevelRegistry.Register(
            "Interlude_2",
            GetCachedObject<M_Level>("Campaign_Interlude_Pipeworks_To_Habitation_02"));
            CustomLevelRegistry.Register(
            "Interlude_3",
            GetCachedObject<M_Level>("Campaign_Interlude_Pipeworks_To_Habitation_03"));
            CustomLevelRegistry.Register(
            "Habitation_Entryway",
            GetCachedObject<M_Level>("M3_Habitation_Entryway_01"));

            CustomLevelRegistry.Register(
            "Pier_1",
            GetCachedObject<M_Level>("M3_Habitation_Pier_01"));
            CustomLevelRegistry.Register(
            "Pier_2",
            GetCachedObject<M_Level>("M3_Habitation_Pier_02"));
            CustomLevelRegistry.Register(
            "Pier_3",
            GetCachedObject<M_Level>("M3_Habitation_Pier_03"));
            CustomLevelRegistry.Register(
            "Pier_4",
            GetCachedObject<M_Level>("M3_Habitation_Pier_04"));

            transition_intro = BuildRegionFromLevelName("Transition_Silo_To_Pipeworks_01");

            string[] levels = { "Parasite_Prison" };
            KeyValuePair<string, string>[] subregions = { new KeyValuePair<string, string>("Prison", "Prison") };
            BuildRegionFromRegistry("Parasite", levels, subregions, false);
            RegisterRegion("Parasite", "Parasite", "");

            string[] levels2 = { "Interlude_1", "Interlude_2", "Interlude_3", };
            KeyValuePair<string, string>[] subregions2 = { new KeyValuePair<string, string>("1", "Ascent"), new KeyValuePair<string, string>("2", "Ascent"), new KeyValuePair<string, string>("3", "Ascent") };
            BuildRegionFromRegistry("Interlude", levels2, subregions2, false);
            RegisterRegion("Interlude", "Interlude", "Region_Pipeworks_Breakroom");

            string[] levels3 = { "Habitation_Entryway", };
            KeyValuePair<string, string>[] subregions3 = { new KeyValuePair<string, string>("Habitation", "Entryway") };
            BuildRegionFromRegistry("Habitation Intro", levels3, subregions3, false);
            RegisterRegion("Habitation Intro", "Habitation Intro", "");

            string[] levels4 = { "Pier_1", "Pier_2", "Pier_3", "Pier_4" };
            KeyValuePair<string, string>[] subregions4 = { new KeyValuePair<string, string>("1", "Pier"), new KeyValuePair<string, string>("2", "Pier"), new KeyValuePair<string, string>("3", "Pier"), new KeyValuePair<string, string>("4", "Pier") };
            BuildRegionFromRegistry("Pier", levels4, subregions4, false);
            RegisterRegion("Pier", "Pier", "");

            string[] levels5 = { "Pier Start", "Shaft To Pier", "Pier Intro" };
            KeyValuePair<string, string>[] subregions5 = { new KeyValuePair<string, string>("Pier", "Haunted") };
            BuildRegionFromRegistry("Pier Intro", levels5, subregions5, true);
            RegisterRegion("Pier Intro", "Pier Intro", "");

            string[] levels6 = { "Lobby", };
            KeyValuePair<string, string>[] subregions6 = { new KeyValuePair<string, string>("Lobby", "Lobby") };
            BuildRegionFromRegistry("Lobby", levels6, subregions6, false);
            RegisterRegion("Lobby", "Lobby", "");

            level = CustomLevelLoader.LoadRoom("anywhere", "To_Anywhere", "To_Anywhere");
            CustomLevelRegistry.Register(
            "To_Anywhere",
            level.level);

            Transform entrance;

            //level = CustomLevelLoader.LoadRoom("anywhere", "From_Anywhere", "From_Pier");
            //entrance = level.level.levelEntrance;
            //if (entrance == null)
            //{
            //foreach (Transform t in level.level.GetComponentsInChildren<Transform>(true))
            //{
            //if (t.name.ContainsInsensitive("entrance"))
            //{
            //entrance = t;
            //break;
            //}
            //}
            //}

            //if (entrance != null)
            //{
            //entrance.Rotate(-90f, 0f, 0f);
            //}
            //CustomLevelRegistry.Register("From_Pier", level.level);

            level = CustomLevelLoader.LoadRoom("anywhere", "To_Anywhere", "To_Comms Array");
            if (level == null || level.level == null || level.level.levelEntrance == null || level.level.levelExit == null)
            {
                Debug.LogWarning("Custom region registration stopped because a required room is missing or incomplete.");
                return;
            }
            entrance = level.level.levelExit;
            entrance?.Rotate(-90f, 0f, 0f);
            CustomLevelRegistry.Register("To_Comms Array", level.level);


            level = CustomLevelLoader.LoadRoom("anywhere", "To_Abyss", "To_Sink");
            if (level == null || level.level == null || level.level.levelEntrance == null || level.level.levelExit == null)
            {
                Debug.LogWarning("Custom region registration stopped because a required room is missing or incomplete.");
                return;
            }
            CustomLevelRegistry.Register("To_Sink", level.level);

            level = CustomLevelLoader.LoadRoom("anywhere", "From_Anywhere", "From_Spawn");
            if (level == null || level.level == null || level.level.levelEntrance == null || level.level.levelExit == null)
            {
                Debug.LogWarning("Custom region registration stopped because a required room is missing or incomplete.");
                return;
            }
            level.level.levelExit.rotation = Quaternion.identity;
            Transform spawnPos = new GameObject().transform;
            spawnPos.SetParent(level.level.gameObject.transform);
            spawnPos.position = level.level.levelExit.position + (level.level.levelExit.forward * -9f);
            spawnPos.rotation = level.level.levelExit.rotation;
            level.level.spawnPosition = spawnPos;
            level.level.levelExit.Rotate(new Vector3(0, 0, 180));
            CustomLevelRegistry.Register("From_Spawn", level.level);

            level = CustomLevelLoader.LoadRoom("anywhere", "From_Anywhere", "From_Anywhere");
            if (level == null || level.level == null || level.level.levelEntrance == null || level.level.levelExit == null)
            {
                Debug.LogWarning("Custom region registration stopped because a required room is missing or incomplete.");
                return;
            }
            entrance = level.level.levelEntrance;

            if (entrance != null)
            {
                entrance.position += new Vector3(Vector3.down.x * 300, Vector3.down.y * 300, Vector3.down.z * 300);
            }
            spawnPos = new GameObject().transform;
            spawnPos.SetParent(level.level.gameObject.transform);
            spawnPos.position = level.level.levelExit.position + (level.level.levelExit.forward * -9f);
            spawnPos.rotation = level.level.levelExit.rotation;
            level.level.spawnPosition = spawnPos;
            CustomLevelRegistry.Register("From_Anywhere", level.level);


            level = CustomLevelLoader.LoadRoom("anywhere", "From_Anywhere", "From_Comms Array");
            entrance = level.level.levelEntrance;
            if (entrance != null)
            {
                entrance.Rotate(-90f, 0f, 0f);
                entrance.position += (new Vector3(Vector3.down.x * 300, Vector3.down.y * 300, Vector3.down.z * 300));
            }
            spawnPos = new GameObject().transform;
            spawnPos.SetParent(level.level.gameObject.transform);
            spawnPos.position = level.level.levelExit.position + (level.level.levelExit.forward * -9f);
            spawnPos.rotation = level.level.levelExit.rotation;
            level.level.spawnPosition = spawnPos;
            CustomLevelRegistry.Register("From_Comms Array", level.level);


            CustomLevelRegistry.Register("AdvancedCourse", GetCachedObject<M_Level>("CH_01_AdvancedCourse"));
            BuildRegionFromRegistry("AdvancedCourse", new string[] { "AdvancedCourse" }, new KeyValuePair<string, string>[] { new KeyValuePair<string, string>("AdvancedCourse", "Advanced Course") }, false);
            RegisterRegion("Advanced Course", "AdvancedCourse", "");

            CustomLevelRegistry.Register("Shattered", GetCachedObject<M_Level>("CH_02_Shattered"));
            BuildRegionFromRegistry("Shattered", new string[] { "Shattered" }, new KeyValuePair<string, string>[] { new KeyValuePair<string, string>("Shattered", "Shattered") }, false);
            RegisterRegion("Shattered Territory", "Shattered", "");


            M_Level roachRunTemplate = GetCachedObject<M_Level>("CH_03_RoachRun");
            if (roachRunTemplate == null) return;
            var RoachRunClone = UnityEngine.Object.Instantiate(roachRunTemplate);
            RegisterRuntimeObject(RoachRunClone.gameObject, false);
            foreach (Transform transform in RoachRunClone.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name == "Mass")
                    UnityEngine.Object.Destroy(transform.gameObject);
            }
            RoachRunClone.gameObject.SetActive(false);
            CustomLevelRegistry.Register("RoachRun", RoachRunClone);
            BuildRegionFromRegistry("RoachRun", new string[] { "RoachRun" }, new KeyValuePair<string, string>[] { new KeyValuePair<string, string>("RoachRun", "Roach Run") }, false);
            RegisterRegion("Roach Run", "RoachRun", "");

            CustomLevelRegistry.Register("CommsArray", GetCachedObject<M_Level>("CH_04_CommsArray"));
            BuildRegionFromRegistry("CommsArray", new string[] { "CommsArray" }, new KeyValuePair<string, string>[] { new KeyValuePair<string, string>("Comms", "Comms Array") }, false);
            RegisterRegion("Comms Array", "CommsArray", "");

            CustomLevelRegistry.Register("Boost Course", GetCachedObject<M_Level>("CH_06_BoostCourse"));
            BuildRegionFromRegistry("Boost Course", new string[] { "Boost Course" }, new KeyValuePair<string, string>[] { new KeyValuePair<string, string>("Boost Course", "Boost Course") }, false);
            RegisterRegion("Boost Course", "Boost Course", "");

            CustomLevelRegistry.Register("Checkpoint", GetCachedObject<M_Level>("M1_Silos_SafeArea_Endless_01"));
            BuildRegionFromRegistry("Checkpoint", new string[] { "Checkpoint" }, new KeyValuePair<string, string>[] { new KeyValuePair<string, string>("Checkpoint", "Checkpoint") }, false);
            RegisterRegion("Checkpoint", "Checkpoint", "");

            BuildRegionFromRegistry("Omicron", new string[] { "Omicron-Prototype-1" }, new KeyValuePair<string, string>[] { new KeyValuePair<string, string>("Prototype", "Prototype") }, false);
            registeredRegionDatabase = database;
            registeredRegionScene = scene;
            RegisterRegion("Omicron", "Omicron", "");


            RegisterRegion("Breakroom", "Region_Silos_Saferoom_Endless", "");
            //level = CustomLevelLoader.LoadRoom("delta_ringing", "To_Delta", "To_Delta");
            //CustomLevelRegistry.Register(
            //"To_Delta",
            //level.level);

            //level = CustomLevelLoader.LoadRoom("delta_ringing", "Ringing_01", "Ringing_01");
            //CustomLevelRegistry.Register(
            //"Ringing_01",
            //level.level);

            //level = CustomLevelLoader.LoadRoom("delta_ringing", "Ringing_02", "Ringing_02");
            //CustomLevelRegistry.Register(
            //"Ringing_02",
            //level.level);

            //level = CustomLevelLoader.LoadRoom("delta_ringing", "Ringing_03", "Ringing_03");
            //CustomLevelRegistry.Register(
            //"Ringing_03",
            //level.level);

            //level = CustomLevelLoader.LoadRoom("delta_ringing", "Ringing_04", "Ringing_04");
            //CustomLevelRegistry.Register(
            //"Ringing_04",
            //level.level);

            //level = CustomLevelLoader.LoadRoom("delta_ringing", "Ringing_05", "Ringing_05");
            //CustomLevelRegistry.Register(
            //"Ringing_05",
            //level.level);

            //level = CustomLevelLoader.LoadRoom("delta_ringing", "Ringing_06", "Ringing_06");
            //CustomLevelRegistry.Register(
            //"Ringing_06",
            //level.level);

            //level = CustomLevelLoader.LoadRoom("delta_ringing", "Ringing_07", "Ringing_07");
            //CustomLevelRegistry.Register(
            //"Ringing_07",
            //level.level);

            //string[] levels = { "Ringing_01", "Ringing_02", "Ringing_03", "Ringing_04", "Ringing_05", "Ringing_06", "Ringing_07"};
            //KeyValuePair<string, string>[] subregions = {new KeyValuePair<string, string>("Ringing","Ringing Jungle")};
            //BuildRegionFromRegistry("Delta",levels,subregions);
            //RegisterRegion("Delta", "Delta", "");
        }


    }

}

namespace Celestial.CustomEndless
{
    public static class CustomLevelRegistry
    {
        private static readonly Dictionary<string, M_Level> loaded =
        new Dictionary<string, M_Level>();
        public static IReadOnlyDictionary<string, M_Level> All => loaded;

        public static void Register(string id, M_Level level)
        {
            loaded[id] = level;
        }

        public static M_Level Load(string id)
        {
            loaded.TryGetValue(id, out var level);
            return level;
        }

        public static void Clear()
        {
            loaded.Clear();
        }
    }

}

