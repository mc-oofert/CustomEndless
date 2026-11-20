using Celestial.CustomEndless;
using HarmonyLib;
using MortarLib;
using System.Collections.Generic;
using UnityEngine;
using static Celestial.CustomEndless.EndlessConfiguration;
//Booster roaches. Roaches that give perks. If you have minor perk ideas this is a good place to put them.
[HarmonyPatch(typeof(DEN_Roach), "Start")]
public class RoachPatch
{
    public static List<Perk> perkList = new List<Perk>() {
        new PerkBuilder("perk_roach_jump_buff", "Jump Roach").WithStacking(true, 1000).WithDescription("Each stack of this roach gives +5% jump height.").WithBuff("roach_jump_height", GameUtils.Buffs.Jump, 0.05f).Build(),
        new PerkBuilder("perk_roach_reach_buff", "Reach Roach").WithStacking(true, 1000).WithDescription("Each stack of this roach gives +5% reach.").WithBuff("roach_reach", GameUtils.Buffs.Reach, 0.05f).Build(),
        new PerkBuilder("perk_roach_gravity_buff", "Gravity Roach").WithStacking(true, 1000).WithDescription("Each stack of this roach gives -5% gravity.").WithBuff("roach_gravity", GameUtils.Buffs.Gravity, -0.05f).Build(),
        new PerkBuilder("perk_roach_climb_speed_buff", "Climb Speed Roach").WithStacking(true, 1000).WithDescription("Each stack of this roach gives +10% climb speed.").WithBuff("roach_climb_speed", GameUtils.Buffs.ClimbSpeed, 0.1f).Build(),
        new PerkBuilder("perk_roach_recoil_buff", "Recoil Roach").WithStacking(true, 1000).WithDescription("Each stack of this roach gives +0.15 weapon recoil.").WithBuff("roach_weapon_recoil", GameUtils.Buffs.WeaponRecoil, 0.15f).Build(),
        new PerkBuilder("perk_roach_weapon_speed_buff", "Weapon Speed Roach").WithStacking(true, 1000).WithDescription("Each stack of this roach gives +5% weapon speed.").WithBuff("roach_weapon_speed", GameUtils.Buffs.WeaponSpeedUp, 0.05f).Build(),
        new PerkBuilder("perk_roach_stamina_buff", "Stamina Roach").WithStacking(true, 1000).WithDescription("Each stack of this roach gives +5% maximum stamina.").WithBuff("roach_stamina", GameUtils.Buffs.Stamina, 0.05f).Build(),
        new PerkBuilder("perk_roach_inventory1", "Microscopic Roach").WithStacking(true, 1000).WithDescription("Each stack of this roach gives +3 inventory slots.").WithBuff("roach_inventory1", GameUtils.Buffs.InventorySpace, 3).Build(),
        new PerkBuilder("perk_roach_inventory2", "Tiny Roach").WithStacking(true, 1000).WithDescription("Each stack of this roach gives +2 inventory slots.").WithBuff("roach_inventory2", GameUtils.Buffs.InventorySpace, 2).Build(),
        new PerkBuilder("perk_roach_inventory3", "Small Roach").WithStacking(true, 1000).WithDescription("Each stack of this roach gives +1 inventory slot.").WithBuff("roach_inventory3", GameUtils.Buffs.InventorySpace, 1).Build(),
    };

    private static bool perksRegistered = false;

    public static void RegisterPerks()
    {
        if (perksRegistered) return;

        WKAssetDatabase baseDb = CL_AssetManager.GetBaseAssetDatabase();
        if (baseDb == null) return;

        foreach (Perk perk in perkList)
        {
            if (!baseDb.perkAssets.Contains(perk))
            {
                baseDb.perkAssets.Add(perk);
            }
        }
        perksRegistered = true;
    }

    public static void Postfix(DEN_Roach __instance)
    {
        if (UnityEngine.Random.value > 0.25f) return;
        if (!CustomUIManager.GetToggleState("fun_boost_roaches")) return;

        CL_AssetManager.InitializeAssetManager();
        //RegisterPerks();

        GameObject itemGO = __instance.gameObject;
        Item_Object newItem = itemGO.GetComponent<Item_Object>();
        if (newItem == null) return;

        DuplicateComponent(newItem, itemGO);
        UnityEngine.Component.DestroyImmediate(newItem);
        newItem = itemGO.GetComponent<Item_Object>();

        ProcGen.SetTheFuckingColorOfThisMesh(__instance.gameObject, Color.blue);
        newItem.itemData = DuplicateItem(newItem.itemData);

        ItemExecutionModule_PerkAdder perkAdder = new ItemExecutionModule_PerkAdder
        {
            onEnterInventory = true,
            onPickupAndDrop = false,
            excludeInHand = false,
            GetPerks = new List<Perk>()
        };

        if (perkList != null && perkList.Count > 0)
        {
            Perk randomPerk = perkList[UnityEngine.Random.Range(0, perkList.Count)];
            perkAdder.GetPerks.Add(randomPerk);
        }

        newItem.itemData.executionModules = new List<ItemExecutionModule> { perkAdder };
        newItem.itemData.prefabName = perkAdder.GetPerks[0].id;
        newItem.gameObject.name = newItem.itemData.prefabName;

        GameObject databasePrefab = UnityEngine.Object.Instantiate(newItem.gameObject);
        databasePrefab.name = newItem.itemData.prefabName;

        databasePrefab.SetActive(false);
        EndlessConfiguration.RegisterRuntimeObject(databasePrefab, false);

        WishboneStuff.AddItemToBaseDatabase(databasePrefab);

        perkAdder.Initialize(newItem.itemData);
    }

    public static T DuplicateComponent<T>(T original, GameObject destination) where T : Component
    {
        T copy = destination.AddComponent<T>();
        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(original), copy);
        return copy;
    }

    public static Item DuplicateItem(Item original)
    {
        Item copy = new Item();
        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(original), copy);
        return copy;
    }
}
