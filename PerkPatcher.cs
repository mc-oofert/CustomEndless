using Celestial.CustomEndless;
using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

[HarmonyPatch(typeof(Perk))]
public static class PerkPatcher
{
    [ThreadStatic]
    internal static string currentCanSpawnPerkId;

    [ThreadStatic]
    internal static Perk currentUpdatingPerk;

    [ThreadStatic]
    internal static PerkModule currentUpdatingModule;

    [HarmonyPatch(nameof(Perk.Initialize))]
    [HarmonyPostfix]
    public static void Initialize_Postfix(Perk __instance)
    {
        if (!EndlessConfiguration.CustomUIManager.GetToggleState("fun_limitless_perks")) return;
        __instance.stackMax = int.MaxValue;
    }

    [HarmonyPatch(nameof(Perk.CanSpawn))]
    [HarmonyPrefix]
    public static bool CanSpawn_Prefix(Perk __instance, ref bool __result)
    {
        if (!EndlessConfiguration.CustomUIManager.GetToggleState("fun_limitless_perks")) return true;

        __instance.stackMax = int.MaxValue;
        if (!__instance.spawnInEndless && CL_GameManager.gamemode.isEndless)
        {
            __result = false;
            return false;
        }
        if (CL_GameManager.GetCurrentGamemode().IsCompetitive() && !__instance.competitive)
        {
            __result = false;
            return false;
        }
        if (__instance.unlockProgressionID != "" && !CL_ProgressionManager.HasProgressionUnlock(__instance.unlockProgressionID))
        {
            __result = false;
            return false;
        }
        if (CL_ProgressionManager.playerExperience < __instance.unlockXP)
        {
            __result = false;
            return false;
        }

        string previousPerkId = currentCanSpawnPerkId;
        currentCanSpawnPerkId = __instance.id;
        try
        {
            __result = __instance.spawnSettings.RandomCheck();
        }
        finally
        {
            currentCanSpawnPerkId = previousPerkId;
        }
        return false;
    }

    [HarmonyPatch(nameof(Perk.AddStack))]
    [HarmonyPrefix]
    public static void AddStack_Prefix(Perk __instance, ref bool firstTime)
    {
        if (!EndlessConfiguration.CustomUIManager.GetToggleState("fun_limitless_perks")) return;
        firstTime = true;
    }

    [HarmonyPatch(nameof(Perk.GetStackEvaluationMultiplier))]
    [HarmonyPrefix]
    public static bool GetStackEvaluationMultiplier_Prefix(Perk __instance, ref float __result)
    {
        if (!EndlessConfiguration.CustomUIManager.GetToggleState("fun_limitless_perks")) return true;
        __result = Mathf.Max(1f, (float)__instance.stackAmount);
        return false;
    }
}

[HarmonyPatch(typeof(SSM_PerkChecker), nameof(SSM_PerkChecker.Check))]
public static class PerkSpawnSettingPatcher
{
    [HarmonyPrefix]
    public static bool Check_Prefix(SSM_PerkChecker __instance, ref float __result)
    {
        if (!EndlessConfiguration.CustomUIManager.GetToggleState("fun_limitless_perks")) return true;
        if (PerkPatcher.currentCanSpawnPerkId == null || __instance.perkID != PerkPatcher.currentCanSpawnPerkId) return true;
        __result = 1f;
        return false;
    }
}

[HarmonyPatch(typeof(Perk), nameof(Perk.Update))]
public static class PerkUpdateStackPatcher
{
    [HarmonyPrefix]
    public static void Prefix(Perk __instance, out Perk __state)
    {
        __state = PerkPatcher.currentUpdatingPerk;
        if (EndlessConfiguration.CustomUIManager.GetToggleState("fun_limitless_perks"))
        {
            PerkPatcher.currentUpdatingPerk = __instance;
        }
    }

    [HarmonyFinalizer]
    public static void Finalizer(Perk __state)
    {
        PerkPatcher.currentUpdatingPerk = __state;
    }
}

[HarmonyPatch(typeof(PerkModule), nameof(PerkModule.Update))]
public static class PerkModuleUpdateStackPatcher
{
    [HarmonyPrefix]
    public static void Prefix(PerkModule __instance, out PerkModule __state)
    {
        __state = PerkPatcher.currentUpdatingModule;
        if (EndlessConfiguration.CustomUIManager.GetToggleState("fun_limitless_perks"))
        {
            PerkPatcher.currentUpdatingModule = __instance;
        }
    }

    [HarmonyFinalizer]
    public static void Finalizer(PerkModule __state)
    {
        PerkPatcher.currentUpdatingModule = __state;
    }
}

[HarmonyPatch(typeof(BuffContainer), nameof(BuffContainer.SetMultiplier))]
public static class BuffMultiplierStackPatcher
{
    [HarmonyPrefix]
    public static void Prefix(ref float m)
    {
        Perk perk = PerkPatcher.currentUpdatingPerk;
        if (!EndlessConfiguration.CustomUIManager.GetToggleState("fun_limitless_perks") || perk == null)
        {
            return;
        }
        if (PerkPatcher.currentUpdatingModule is PerkModule_BuffFromInventory ||
            PerkPatcher.currentUpdatingModule is PerkModule_BuffFromMovement ||
            PerkPatcher.currentUpdatingModule is PerkModule_BuffFromStat ||
            PerkPatcher.currentUpdatingModule is PerkModule_ColdBuff ||
            PerkPatcher.currentUpdatingModule is PerkModule_FallBuff ||
            PerkPatcher.currentUpdatingModule is PerkModule_HoldDrop_CreateItem ||
            PerkPatcher.currentUpdatingModule is PerkModule_PlayerStates)
        {
            return;
        }
        m *= Mathf.Max(1f, (float)perk.GetStackAmount());
    }
}

[HarmonyPatch(typeof(PerkModule_ConsumeBuff), nameof(PerkModule_ConsumeBuff.Initialize))]
public static class ConsumeBuffStackPatcher
{
    [HarmonyPostfix]
    public static void Postfix(PerkModule_ConsumeBuff __instance, Perk ___perk)
    {
        if (EndlessConfiguration.CustomUIManager.GetToggleState("fun_limitless_perks"))
        {
            __instance.canStack = true;
        }
    }
}

[HarmonyPatch(typeof(PerkModule_BuffFromStat), nameof(PerkModule_BuffFromStat.Initialize))]
public static class BuffFromStatStackPatcher
{
    [HarmonyPrefix]
    public static void Prefix(PerkModule_BuffFromStat __instance, Perk ___perk)
    {
        if (EndlessConfiguration.CustomUIManager.GetToggleState("fun_limitless_perks"))
        {
            __instance.increaseBuffUsesPerkStack = true;
        }
    }

    [HarmonyPostfix]
    public static void Postfix(PerkModule_BuffFromStat __instance, Perk ___perk)
    {
        if (EndlessConfiguration.CustomUIManager.GetToggleState("fun_limitless_perks"))
        {
            __instance.increaseBuffUsesPerkStack = true;
        }
    }
}

[HarmonyPatch(typeof(PerkModule_ColdBuff), nameof(PerkModule_ColdBuff.Initialize))]
public static class ColdBuffCurvePatcher
{
    [HarmonyPostfix]
    public static void Postfix(PerkModule_ColdBuff __instance, Perk ___perk)
    {
        if (!EndlessConfiguration.CustomUIManager.GetToggleState("fun_limitless_perks")) return;
        __instance.useCurve = true;
        __instance.curve = PerkCurveStackPatcher.Linear(1);
    }
}

[HarmonyPatch(typeof(PerkModule_PlayerStates), nameof(PerkModule_PlayerStates.Initialize))]
public static class PlayerStatesCurvePatcher
{
    [HarmonyPostfix]
    public static void Postfix(PerkModule_PlayerStates __instance, Perk ___perk)
    {
        if (!EndlessConfiguration.CustomUIManager.GetToggleState("fun_limitless_perks")) return;
        __instance.useCurve = true;
        __instance.curve = PerkCurveStackPatcher.Linear(1);
    }
}

public static class PerkCurveStackPatcher
{
    public static AnimationCurve Linear(float maximum)
    {
        AnimationCurve curve = AnimationCurve.Linear(0f, 0f, maximum, maximum);
        curve.preWrapMode = WrapMode.ClampForever;
        curve.postWrapMode = WrapMode.ClampForever;
        return curve;
    }
}

[HarmonyPatch(typeof(PerkModule_ItemAdder), nameof(PerkModule_ItemAdder.AddModule))]
public static class ItemAdderStackPatcher
{
    [HarmonyPrefix]
    public static bool Prefix(PerkModule_ItemAdder __instance, int amount, Perk ___perk)
    {
        if (!EndlessConfiguration.CustomUIManager.GetToggleState("fun_limitless_perks")) return true;
        for (int i = 0; i < amount; i++)
        {
            List<Item> items = new List<Item>();
            foreach (Item_Object item in __instance.itemsToAdd)
            {
                if (item == null) continue;
                Item clone = item.itemData.GetClone();
                clone.bagPosition = new Vector3(0f, 0f, 1f) + UnityEngine.Random.insideUnitSphere * 0.01f;
                clone.bagRotation = Quaternion.LookRotation(clone.upDirection);
                items.Add(clone);
            }
            foreach (Item item in items)
            {
                Inventory.instance.AddItemToInventoryScreen(item.bagPosition, item, localSpacePosition: true, useStoredValues: true);
                item.inventory = Inventory.instance;
            }
        }
        return false;
    }
}

[HarmonyPatch(typeof(PerkModule_RemovalTimer), nameof(PerkModule_RemovalTimer.AddModule))]
public static class RemovalTimerStackPatcher
{
    [HarmonyPrefix]
    public static bool Prefix(int amount, bool firstTime, Perk ___perk, ref float ___removeTime, ref float ___startTime)
    {
        if (!EndlessConfiguration.CustomUIManager.GetToggleState("fun_limitless_perks")) return true;
        if (firstTime && Mathf.Approximately(___removeTime, ___startTime))
        {
            ___removeTime = ___startTime * amount;
        }
        else
        {
            ___removeTime += ___startTime * amount;
        }
        return false;
    }
}

[HarmonyPatch(typeof(PerkModule_DamagePlayer), nameof(PerkModule_DamagePlayer.Update))]
public static class DamagePlayerStackPatcher
{
    [HarmonyPrefix]
    public static void Prefix(PerkModule_DamagePlayer __instance, Perk ___perk, out float[] __state)
    {
        __state = new float[] { __instance.damageAmount, __instance.damageGripAmount };
        if (EndlessConfiguration.CustomUIManager.GetToggleState("fun_limitless_perks"))
        {
            float multiplier = Mathf.Max(1f, (float)___perk.GetStackAmount());
            __instance.damageAmount *= multiplier;
            __instance.damageGripAmount *= multiplier;
        }
    }

    [HarmonyFinalizer]
    public static void Finalizer(PerkModule_DamagePlayer __instance, float[] __state)
    {
        if (__state == null) return;
        __instance.damageAmount = __state[0];
        __instance.damageGripAmount = __state[1];
    }
}

[HarmonyPatch(typeof(PerkModule_DamagePlayerOnAction), nameof(PerkModule_DamagePlayerOnAction.Damage), new Type[] { })]
public static class DamagePlayerOnActionStackPatcher
{
    [HarmonyPrefix]
    public static void Prefix(PerkModule_DamagePlayerOnAction __instance, Perk ___perk, out float[] __state)
    {
        __state = new float[] { __instance.amount, __instance.staminaDamage, __instance.perHandStaminaDamage };
        if (EndlessConfiguration.CustomUIManager.GetToggleState("fun_limitless_perks"))
        {
            float multiplier = Mathf.Max(1f, (float)___perk.GetStackAmount());
            __instance.amount *= multiplier;
            __instance.staminaDamage *= multiplier;
            __instance.perHandStaminaDamage *= multiplier;
        }
    }

    [HarmonyFinalizer]
    public static void Finalizer(PerkModule_DamagePlayerOnAction __instance, float[] __state)
    {
        if (__state == null) return;
        __instance.amount = __state[0];
        __instance.staminaDamage = __state[1];
        __instance.perHandStaminaDamage = __state[2];
    }
}

[HarmonyPatch(typeof(PerkModule_JazzHand), nameof(PerkModule_JazzHand.Update))]
public static class JazzHandStackPatcher
{
    [HarmonyPostfix]
    public static void Postfix(PerkModule_JazzHand __instance, Perk ___perk, BuffContainer ___currentBuff, float ___currentMultiplier)
    {
        if (EndlessConfiguration.CustomUIManager.GetToggleState("fun_limitless_perks") && ___currentBuff != null)
        {
            ___currentBuff.SetMultiplier(___currentMultiplier * Mathf.Max(1f, (float)___perk.GetStackAmount()));
        }
    }
}

[HarmonyPatch(typeof(PerkModule_Flight), nameof(PerkModule_Flight.Update))]
public static class FlightStackPatcher
{
    [HarmonyPostfix]
    public static void Postfix(PerkModule_Flight __instance, Perk ___perk, BuffContainer ___currentBuff, BuffContainer ___currentDebuff)
    {
        if (!EndlessConfiguration.CustomUIManager.GetToggleState("fun_limitless_perks")) return;
        float multiplier = Mathf.Max(1f, (float)___perk.GetStackAmount());
        if (___currentBuff != null) ___currentBuff.multiplier *= multiplier;
        if (___currentDebuff != null) ___currentDebuff.multiplier *= multiplier;
    }
}

[HarmonyPatch(typeof(PerkModule_MassController), nameof(PerkModule_MassController.Update))]
public static class MassControllerStackPatcher
{
    [HarmonyPostfix]
    public static void Postfix(PerkModule_MassController __instance, Perk ___perk)
    {
        if (!EndlessConfiguration.CustomUIManager.GetToggleState("fun_limitless_perks")) return;
        float multiplier = Mathf.Max(1f, (float)___perk.GetStackAmount());
        if (DEN_DeathFloor.instance != null) DEN_DeathFloor.instance.SetUpdateSpeedMultiplier(__instance.massMult * multiplier);
        AccessTools.Method(typeof(DEN_Face), "SetGlobalUpdateSpeedMultiplier")?.Invoke(null, new object[] { __instance.massMult * multiplier });
    }
}

[HarmonyPatch(typeof(PerkModule_MassProximityEffect), nameof(PerkModule_MassProximityEffect.Update))]
public static class MassProximityStackPatcher
{
    [HarmonyPostfix]
    public static void Postfix(PerkModule_MassProximityEffect __instance, Perk ___perk)
    {
        if (!EndlessConfiguration.CustomUIManager.GetToggleState("fun_limitless_perks")) return;
        if (ENT_Player.GetPlayer() == null || DEN_DeathFloor.instance == null || ___perk == null) return;
        float height = ENT_Player.GetPlayer().transform.position.y - DEN_DeathFloor.GetHeight();
        if (height < __instance.massProximityDistance && DEN_DeathFloor.instance.IsActiveAndMoving())
        {
            float multiplier = Mathf.Max(1f, (float)___perk.GetStackAmount());
            DEN_DeathFloor.instance.SetUpdateSpeedMultiplier(__instance.proximityMassSpeedMult * multiplier);
            AccessTools.Method(typeof(DEN_Face), "SetGlobalUpdateSpeedMultiplier")?.Invoke(null, new object[] { __instance.proximityMassSpeedMult * multiplier });
        }
    }
}

[HarmonyPatch(typeof(PerkModule_ObjectSpawner), nameof(PerkModule_ObjectSpawner.Update))]
public static class ObjectSpawnerStackPatcher
{
    [HarmonyPrefix]
    public static void Prefix(PerkModule_ObjectSpawner __instance, Perk ___perk, out float __state)
    {
        __state = __instance.spawnFrequency;
        if (EndlessConfiguration.CustomUIManager.GetToggleState("fun_limitless_perks"))
        {
            __instance.spawnFrequency /= Mathf.Max(1f, (float)___perk.GetStackAmount());
        }
    }

    [HarmonyFinalizer]
    public static void Finalizer(PerkModule_ObjectSpawner __instance, float __state)
    {
        __instance.spawnFrequency = __state;
    }
}

[HarmonyPatch(typeof(PerkModule_HungerMeter), nameof(PerkModule_HungerMeter.Update))]
public static class HungerMeterStackPatcher
{
    [HarmonyPrefix]
    public static void Prefix(PerkModule_HungerMeter __instance, Perk ___perk, out float __state)
    {
        __state = __instance.hungerDecayRate;
        if (EndlessConfiguration.CustomUIManager.GetToggleState("fun_limitless_perks"))
        {
            __instance.hungerDecayRate *= Mathf.Max(1f, (float)___perk.GetStackAmount());
        }
    }

    [HarmonyFinalizer]
    public static void Finalizer(PerkModule_HungerMeter __instance, float __state)
    {
        __instance.hungerDecayRate = __state;
    }
}

[HarmonyPatch(typeof(ENT_Player), "Movement")]
public static class GravityBuffCapPatcher
{
    [HarmonyPrefix]
    public static void Prefix(ENT_Player __instance, ref float ___gravityMult, out float __state)
    {
        __state = ___gravityMult;
        float modifier = 1f + __instance.curBuffs.GetBuff("addGravity");
        ___gravityMult *= modifier / Mathf.Max(modifier, 0.2f);
    }

    [HarmonyFinalizer]
    public static void Finalizer(ref float ___gravityMult, float __state)
    {
        ___gravityMult = __state;
    }
}

[HarmonyPatch]
public static class BuffPatches
{
    [HarmonyPatch(typeof(BuffContainer), nameof(BuffContainer.Update))]
    [HarmonyPrefix]
    public static bool Update_Prefix(BuffContainer __instance, ref bool __result)
    {
        if (!EndlessConfiguration.CustomUIManager.GetToggleState("fun_limitless_buffs")) return true;

        __instance.buffTime = 1f;
        if (__instance.buffs != null)
        {
            for (int i = 0; i < __instance.buffs.Count; i++)
            {
                if (__instance.buffs[i] != null)
                {
                    __instance.buffs[i].amount = __instance.buffs[i].maxAmount;
                }
            }
        }
        __result = false;
        return false;
    }



    [HarmonyPatch(typeof(GameEntity), nameof(GameEntity.RemoveBuff), new Type[] { typeof(string) })]
    [HarmonyPrefix]
    public static bool RemoveBuff_Prefix()
    {
        if (!EndlessConfiguration.CustomUIManager.GetToggleState("fun_limitless_buffs")) return true;
        return false;
    }
    [HarmonyPatch(typeof(GameEntity), nameof(GameEntity.RemoveBuff), new Type[] { typeof(BuffContainer) })]
    [HarmonyPrefix]
    public static bool RemoveBuff_Prefix_2_Electric_Boogaloo()
    {
        if (!EndlessConfiguration.CustomUIManager.GetToggleState("fun_limitless_buffs")) return true;
        return false;
    }
    [HarmonyPatch(typeof(GameEntity.EntityBuff), nameof(GameEntity.EntityBuff.RemoveBuffContainer), new Type[] { typeof(string) })]
    [HarmonyPrefix]
    public static bool RemoveBuffContainer_Prefix()
    {
        if (!EndlessConfiguration.CustomUIManager.GetToggleState("fun_limitless_buffs")) return true;
        return false;
    }

    [HarmonyPatch(typeof(GameEntity.EntityBuff), nameof(GameEntity.EntityBuff.RemoveBuffContainer), new Type[] { typeof(BuffContainer) })]
    [HarmonyPrefix]
    public static bool RemoveBuffContainer_Prefix_2_Electric_Boogaloo()
    {
        if (!EndlessConfiguration.CustomUIManager.GetToggleState("fun_limitless_buffs")) return true;
        return false;
    }
}
