using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Celestial.CustomEndless
{
    public static partial class EndlessConfiguration
    {
        private static readonly Dictionary<string, M_Level> regionBreakrooms = new Dictionary<string, M_Level>(StringComparer.OrdinalIgnoreCase);
        private static readonly BreakroomEntityDefinition[] additionalBreakroomEntities =
        {
            new BreakroomEntityDefinition("VendingMachineBig", "Prop_VendingMachine_Big", "breakroom_vending_every", "breakroom_vending_after"),
            new BreakroomEntityDefinition("EquipmentLocker", "Prop_EquipmentLocker_01", null, null),
            new BreakroomEntityDefinition("DiskVendor", "Prop_DiskVendor_2x", null, null)
        };

        private static string BreakroomKey(string name)
        {
            return Regex.Replace(name ?? string.Empty, "[^a-zA-Z0-9]", string.Empty).ToLowerInvariant();
        }

        private static void RegisterRegionBreakrooms()
        {
            regionBreakrooms.Clear();
            foreach (string name in CustomLevelLoader.GetRoomAssetNames("anywhere"))
            {
                if (!name.StartsWith("Breakroom_", StringComparison.OrdinalIgnoreCase)) continue;
                M_Level.LevelAssetHolder holder = CustomLevelLoader.LoadRoom("anywhere", name, name);
                if (holder == null || holder.level == null || holder.level.levelEntrance == null || holder.level.levelExit == null) continue;
                regionBreakrooms[BreakroomKey(name.Substring("Breakroom_".Length))] = holder.level;
                CustomLevelRegistry.Register(name, holder.level);
            }
        }

        private static void RegisterAdditionalTransitionRooms()
        {
            foreach (string assetName in CustomLevelLoader.GetRoomAssetNames("anywhere"))
            {
                bool from = assetName.StartsWith("From_", StringComparison.OrdinalIgnoreCase);
                if (!from && !assetName.StartsWith("To_", StringComparison.OrdinalIgnoreCase)) continue;
                string suffix = assetName.Substring(from ? 5 : 3);
                string region = RegionDefinitions.Keys.FirstOrDefault(name => BreakroomKey(name) == BreakroomKey(suffix));
                if (region == null) continue;
                string id = (from ? "From_" : "To_") + region;
                if (CustomLevelRegistry.Load(id) != null) continue;
                M_Level.LevelAssetHolder holder = CustomLevelLoader.LoadRoom("anywhere", assetName, id);
                if (holder != null && holder.level != null) CustomLevelRegistry.Register(id, holder.level);
            }
        }

        private static void ConfigureBreakroomDecorations(M_Level level, string regionName)
        {
            string identity = BreakroomKey(GetCustomTransitionIdentity(regionName, false));
            var themes = new Dictionary<string, string>
            {
                { "silosstuff", "silos" }, { "pipestuff", "pipeworks" }, { "habstuff", "habitation" },
                { "abyssstuff", "abyss" }, { "neststuff", "nest" } // WHEN MORE THEMES ARE ADDED CHANGE IT HERE
            };
            foreach (Transform child in level.GetComponentsInChildren<Transform>(true))
            {
                string name = Regex.Replace(child.name.ToLowerInvariant(), @"\.\d+$", "");
                if (themes.TryGetValue(name, out string theme)) child.gameObject.SetActive(theme == identity);
                var match = Regex.Match(name, "^handhold:.*edge-(silo|pipe|hab|abyss|nest)([^a-z]|$)");
                if (match.Success)
                {
                    string themeKey = match.Groups[1].Value;
                    string expected = themeKey == "silo" ? "silos" : themeKey == "pipe" ? "pipeworks" : themeKey == "hab" ? "habitation" : themeKey;
                    child.gameObject.SetActive(expected == identity);
                }
            }
        }

        private static M_Level GetRegionBreakroomSource(string regionName)
        {
            string identity = GetCustomTransitionIdentity(regionName, false);
            return regionBreakrooms.TryGetValue(BreakroomKey(regionName), out M_Level source) && source != null
                ? source
                : regionBreakrooms.TryGetValue(BreakroomKey(identity), out source) && source != null
                ? source
                : GetTransitionLevel("Universal_Breakroom");
        }

        private static BreakroomEntityDefinition GetAuthoredMachineDefinition(string name)
        {
            string key = BreakroomKey(name);
            string entity = null;
            if (key.StartsWith("propupgradeconsoleexperimental") || key.StartsWith("upgradeconsole")) entity = "ExperimentalMachine";
            else if (key.StartsWith("propupgradeconsolemain") || key.StartsWith("propupgradeconsolescreenanddisk")) entity = "PerkMachine";
            else if (key.StartsWith("propatm")) entity = "ATM";
            else if (key.StartsWith("propvendingmachinet")) entity = "VendingMachineT2";
            else if (key.StartsWith("propvendingmachinebig")) entity = "VendingMachineBig";
            else if (key.StartsWith("propvendingmachine")) entity = "VendingMachine";
            else if (key.StartsWith("proprecycler") && !key.Contains("grinder")) entity = "Recycler";
            else if (key.StartsWith("propcryorefuelerendless") || key.StartsWith("cryorefuelerendless")) entity = "CryoRefueler";
            else if (key.StartsWith("proprhomonolith") || key.Contains("rhomonolithhere")) entity = "RhoMonolith";
            else if (key.StartsWith("propmanholehatch") && !key.Contains("locks")) entity = "ManholeHatch";
            else if (key.StartsWith("propequipmentlocker") && !key.Contains("door")) entity = "EquipmentLocker";
            else if (key.StartsWith("propdiskvendor")) entity = "DiskVendor";
            return BreakroomEntities.Concat(additionalBreakroomEntities).FirstOrDefault(definition => definition.Name == entity);
        }

        private static bool HasMachineParent(Transform transform, Transform root)
        {
            for (Transform parent = transform.parent; parent != null && parent != root; parent = parent.parent)
                if (GetAuthoredMachineDefinition(parent.name) != null) return true;
            return false;
        }

        private static bool ConfigureAuthoredBreakroomMachines(M_Level level, UniversalBreakroomSettings settings)
        {
            var placeholders = level.GetComponentsInChildren<Transform>(true)
                .Where(transform => transform != level.transform && GetAuthoredMachineDefinition(transform.name) != null && !HasMachineParent(transform, level.transform))
                .ToArray();
            if (placeholders.Length == 0) return false;

            var placements = new List<(BreakroomEntityDefinition Entity, Transform Anchor)>();
            foreach (Transform placeholder in placeholders)
            {
                BreakroomEntityDefinition entity = GetAuthoredMachineDefinition(placeholder.name);
                bool duplicate = placements.Any(placement => placement.Entity.Name == entity.Name && Vector3.SqrMagnitude(placement.Anchor.position - placeholder.position) < 0.16f);
                if (!duplicate) placements.Add((entity, placeholder));
                placeholder.gameObject.SetActive(false);
            }

            GameObject staging = new GameObject("BreakroomMachineSetup");
            staging.SetActive(false);
            staging.transform.SetParent(level.transform, false);
            try
            {
                foreach (var placement in placements)
                {
                    BreakroomEntityDefinition entity = placement.Entity;
                    int every = entity.EveryKey == null ? 1 : Mathf.Max(0, GetCountStateForSlot(entity.EveryKey, settings.regionKey, settings.slotIndex));
                    int after = entity.AfterKey == null ? 0 : Mathf.Max(0, GetCountStateForSlot(entity.AfterKey, settings.regionKey, settings.slotIndex));
                    if (!ShouldSpawnBreakroomEntity(settings.breakroomNumber, every, after)) continue;
                    GameObject source = GetAuthoredMachineSource(entity);
                    if (source == null)
                    {
                        Debug.LogWarning("No working breakroom machine found for " + entity.Name + " in " + level.name);
                        continue;
                    }
                    GameObject machine = UnityEngine.Object.Instantiate(source, staging.transform);
                    machine.SetActive(false);
                    machine.transform.SetParent(level.transform, true);
                    machine.name = "UniversalBreakroom" + entity.Name;
                    AlignBreakroomMachine(machine, placement.Anchor);
                    foreach (OS_Computer_Interface computer in machine.GetComponentsInChildren<OS_Computer_Interface>(true))
                        computer.computerID = string.Empty;
                    if (entity.RemoveSpawnChance)
                        foreach (UT_SpawnChance chance in machine.GetComponentsInChildren<UT_SpawnChance>(true))
                        {
                            chance.enabled = false;
                            UnityEngine.Object.Destroy(chance);
                        }
                    machine.SetActive(true);
                }
            }
            finally
            {
                foreach (Transform placeholder in placeholders)
                    if (placeholder != null) UnityEngine.Object.Destroy(placeholder.gameObject);
                UnityEngine.Object.Destroy(staging);
            }
            return true;
        }

        private static GameObject GetAuthoredMachineSource(BreakroomEntityDefinition entity)
        {
            GameObject source = GetBreakroomEntitySource(entity);
            if (source != null) return source;
            if (entity.Name == "PerkMachine") source = GetPerkMachineSource();
            else if (entity.Name == "ExperimentalMachine") source = GetExperimentalMachineSource();
            if (source != null) return source;
            string key = BreakroomKey(entity.SearchName);
            return Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(candidate => candidate != null &&
                BreakroomKey(candidate.name).StartsWith(key, StringComparison.Ordinal) &&
                candidate.GetComponentsInChildren<MonoBehaviour>(true).Any(component => component != null && !(component is ObjectTagger) && !(component is CL_Lamp)));
        }

        private static void AlignBreakroomMachine(GameObject machine, Transform anchor)
        {
            MeshFilter targetMesh = anchor.GetComponent<MeshFilter>();
            MeshFilter sourceMesh = machine.GetComponent<MeshFilter>();
            if (sourceMesh == null) sourceMesh = machine.GetComponentInChildren<MeshFilter>(true);
            Transform visual = sourceMesh != null ? sourceMesh.transform : machine.transform;
            Quaternion relativeRotation = Quaternion.Inverse(machine.transform.rotation) * visual.rotation;
            machine.transform.rotation = anchor.rotation * Quaternion.Inverse(relativeRotation);
            Vector3 desiredScale = anchor.lossyScale;
            if (targetMesh != null && targetMesh.sharedMesh != null && sourceMesh != null && sourceMesh.sharedMesh != null)
            {
                Vector3 targetSize = targetMesh.sharedMesh.bounds.size;
                Vector3 sourceSize = sourceMesh.sharedMesh.bounds.size;
                float ratio = targetSize.magnitude / Mathf.Max(sourceSize.magnitude, 0.0001f);
                desiredScale *= ratio;
            }
            else
            {
                desiredScale = machine.transform.lossyScale;
            }
            Vector3 visualScale = visual.lossyScale;
            Vector3 localScale = machine.transform.localScale;
            machine.transform.localScale = new Vector3(
                localScale.x * desiredScale.x / Mathf.Max(Mathf.Abs(visualScale.x), 0.0001f),
                localScale.y * desiredScale.y / Mathf.Max(Mathf.Abs(visualScale.y), 0.0001f),
                localScale.z * desiredScale.z / Mathf.Max(Mathf.Abs(visualScale.z), 0.0001f));

            machine.transform.position += anchor.position - visual.position;
        }
    }
}
