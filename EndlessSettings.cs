using MortarLib;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class EndlessSettings : MonoBehaviour
{
    [SerializeField] private MenuSettingsManager menuSettingsManager;
    [SerializeField] private Button panelButton;
    public string menuTag = "EndlessSettings";

    private readonly Dictionary<string, string> startingItemTitles =
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
          { "item_artifact_craftcube", "CUBE" },
          {"item_cryogun", "Cryogun"}
            };
    private Dictionary<string, string> startingPerkTitles =>
CL_AssetManager.GetFullCombinedAssetDatabase()
    .perkAssets
    .Where(p => p != null)
    .OrderBy(p => p.title)
    .ToDictionary(
        p => p.id,
        p => p.title
    );
    public static readonly (string Key, string Name, string BuffId)[] buffSettings = {
    ("buff_jump", "Jump Height", GameUtils.Buffs.Jump),
    ("buff_stamina", "Stamina", GameUtils.Buffs.Stamina),
    ("buff_stamina_regen", "Stamina Regen", GameUtils.Buffs.StaminaRegen),
    ("buff_climb_speed", "Climb Speed", GameUtils.Buffs.ClimbSpeed),
    ("buff_reach", "Reach", GameUtils.Buffs.Reach),
    ("buff_armor", "Damage Resist", GameUtils.Buffs.Armor),
    ("buff_vulnerability", "Damage Multiplier", GameUtils.Buffs.Vulnerability),
    ("buff_knockback_resist", "Knockback Resist", GameUtils.Buffs.KnockbackArmor),
    ("buff_weapon_speed", "Weapon Speed", GameUtils.Buffs.WeaponSpeedUp),
    ("buff_weapon_slow", "Weapon Slow", GameUtils.Buffs.WeaponSpeedDown),
    ("buff_weapon_recoil", "Weapon Recoil", GameUtils.Buffs.WeaponRecoil),
    ("buff_time_slow", "Time Slow", GameUtils.Buffs.SlowTime),
    ("buff_gravity", "Gravity", GameUtils.Buffs.Gravity),
    ("buff_inventory_space", "Inventory Space", GameUtils.Buffs.InventorySpace),
    ("buff_air_control", "Air Control", GameUtils.Buffs.AirControl),
    ("buff_left_hand_climb_speed", "Left Hand Climb Speed", GameUtils.Buffs.LeftHandClimbSpeed),
    ("buff_right_hand_climb_speed", "Right Hand Climb Speed", GameUtils.Buffs.RightHandClimbSpeed),
    ("buff_left_hand_climbing_jump", "Left Hand Climbing Jump", GameUtils.Buffs.LeftHandClimbingJump),
    ("buff_right_hand_climbing_jump", "Right Hand Climbing Jump", GameUtils.Buffs.RightHandClimbingJump),
    ("buff_temporary_climb_speed", "Temporary Climb Speed", GameUtils.Buffs.TemporaryClimbSpeed),
    ("buff_drag", "Drag", GameUtils.Buffs.Drag),
    ("buff_extra_jumps", "Extra Jumps", GameUtils.Buffs.ExtraJumps),
    ("buff_field_of_view", "Field Of View", GameUtils.Buffs.FieldOfView),
    ("buff_freeze_resistance", "Freeze Resistance", GameUtils.Buffs.FreezeResistance),
    ("buff_grip_strength", "Grip Strength", GameUtils.Buffs.GripStrength),
    ("buff_grounded_jump", "Grounded Jump", GameUtils.Buffs.GroundedJump),
    ("buff_grounded_stamina_regen", "Grounded Stamina Regen", GameUtils.Buffs.GroundedStaminaRegen),
    ("buff_grounded_speed", "Grounded Speed", GameUtils.Buffs.GroundedSpeed),
    ("buff_hammer_damage", "Hammer Damage", GameUtils.Buffs.HammerDamage),
    ("buff_hang_speed", "Hang Speed", GameUtils.Buffs.HangSpeed),
    ("buff_left_hand_hang_speed", "Left Hand Hang Speed", GameUtils.Buffs.LeftHandHangSpeed),
    ("buff_right_hand_hang_speed", "Right Hand Hang Speed", GameUtils.Buffs.RightHandHangSpeed),
    ("buff_jump_boost", "Jump Boost", GameUtils.Buffs.JumpBoost),
    ("buff_limp", "Limp", GameUtils.Buffs.Limp),
    ("buff_piton_secure", "Piton Secure", GameUtils.Buffs.PitonSecure),
    ("buff_player_scale", "Player Scale", GameUtils.Buffs.PlayerScale),
    ("buff_player_width", "Player Width", GameUtils.Buffs.PlayerWidth),
    ("buff_pocket_big_item_capacity", "Pocket Big Item Capacity", GameUtils.Buffs.PocketBigItemCapacity),
    ("buff_pocket_capacity", "Pocket Capacity", GameUtils.Buffs.PocketCapacity),
    ("buff_resting_friction", "Resting Friction", GameUtils.Buffs.RestingFriction),
    ("buff_slow", "Slow", GameUtils.Buffs.Slow),
    ("buff_speed", "Movement Speed", GameUtils.Buffs.Speed),
    ("buff_stamina_after_extra_jump", "Stamina After Extra Jump", GameUtils.Buffs.StaminaAfterExtraJump),
    ("buff_stamina_after_jump", "Stamina After Jump", GameUtils.Buffs.StaminaAfterJump),
    ("buff_stamina_after_jump_boost", "Stamina After Jump Boost", GameUtils.Buffs.StaminaAfterJumpBoost),
    ("buff_still_grip_strength", "Still Grip Strength", GameUtils.Buffs.StillGripStrength),
    ("buff_strike_damage", "Strike Damage", GameUtils.Buffs.StrikeDamage),
    ("buff_throw_strength", "Throw Strength", GameUtils.Buffs.ThrowStrength),
    ("buff_bloodied", "Bloodied", GameUtils.Buffs.Bloodied),
    ("buff_boosted", "Boosted", GameUtils.Buffs.Boosted),
    ("buff_duration_multiplier", "Buff Duration Multiplier", GameUtils.Buffs.BuffTimeMultiplier),
    ("buff_cryogenic_ammo", "Cryogenic Ammo", GameUtils.Buffs.CryogenicAmmo),
    ("buff_freeze_counter", "Freeze Counter", GameUtils.Buffs.FreezeCounter),
    ("buff_divide_capacity", "Inventory Capacity Multiplier", GameUtils.Buffs.DivideCapacity),
    ("buff_divide_jump_strain", "Jump Strain Multiplier", GameUtils.Buffs.DivideJumpStrain),
    ("buff_divide_stamina", "Stamina Multiplier", GameUtils.Buffs.DivideStamina),
    ("buff_left_hand_divide_stamina", "Left Hand Stamina Multiplier", GameUtils.Buffs.LeftHandDivideStamina),
    ("buff_right_hand_divide_stamina", "Right Hand Stamina Multiplier", GameUtils.Buffs.RightHandDivideStamina),
    ("buff_extra_jump_on_food", "Extra Jumps On Food", GameUtils.Buffs.ExtraJumpOnFood),
    ("buff_flight", "Flight", GameUtils.Buffs.Flight),
    ("buff_force_crouch_grounded", "Force Crouch Grounded", GameUtils.Buffs.ForceCrouchIfGrounded),
    ("buff_gooped", "Gooped", GameUtils.Buffs.Gooped),
    ("buff_goop_time", "Goop Duration", GameUtils.Buffs.GoopTime),
    ("buff_grab_anything", "Grab Anything", GameUtils.Buffs.GrabAnything),
    ("buff_grip_resistance", "Grip Resistance", GameUtils.Buffs.GripResistance),
    ("buff_leg_break", "Leg Break", GameUtils.Buffs.LegBreak),
    ("buff_left_arm_break", "Left Arm Break", GameUtils.Buffs.LeftArmBreak),
    ("buff_right_arm_break", "Right Arm Break", GameUtils.Buffs.RightArmBreak),
    ("buff_bleed", "Bleed", GameUtils.Buffs.Bleed),
    ("buff_burned", "Burned", GameUtils.Buffs.Burned),
    ("buff_limit_grip_strength", "Grip Strength Limit", GameUtils.Buffs.LimitGripStrength),
    ("buff_slow_mass", "Slow Mass", GameUtils.Buffs.SlowMass),
    ("buff_mass_slow", "Mass Slow", GameUtils.Buffs.MassSlow),
    ("buff_mass_speed_multiplier", "Mass Speed Multiplier", GameUtils.Buffs.MassSpeedMultiplier),
    ("buff_poison_drop_hands", "Poison Drop Hands", GameUtils.Buffs.PoisonDropHands),
    ("buff_poisoned", "Poisoned", GameUtils.Buffs.Poisoned),
    ("buff_poison_stamina_damage", "Poison Stamina Damage", GameUtils.Buffs.PoisonStaminaDamage),
    ("buff_poison_timer", "Poison Timer", GameUtils.Buffs.PoisonTimer),
    ("buff_reduce_fall_damage_distance", "Fall Damage Distance", GameUtils.Buffs.ReduceFallDamageDistance),
    ("buff_roided", "Roided", GameUtils.Buffs.Roided),
    ("buff_slow_time_inventory", "Inventory Time Slow", GameUtils.Buffs.SlowTimeInventory),
    ("buff_unbound", "Unbound", GameUtils.Buffs.Unbound),
    ("buff_warming", "Warming", GameUtils.Buffs.Warming),
};

    private void Awake()
    {
        if (menuSettingsManager == null)
        {
            menuSettingsManager = transform.parent.parent.GetComponentInChildren<MenuSettingsManager>();
        }

        if (panelButton == null)
        {
            panelButton = GetComponent<Button>();
        }

        panelButton?.onClick.AddListener(OnPanelClicked);

        RegisterAllSettings();
    }

    public void RegisterAllSettings()
    {
        if (menuSettingsManager == null) return;

        menuSettingsManager.Register(menuTag, "Use Default Transitions And Breakrooms", "use_default_transitions_and_breakrooms", SettingType.Bool, "false");

        foreach (var kvp in startingItemTitles)
        {
            menuSettingsManager.Register(menuTag, kvp.Value, kvp.Key, SettingType.Integer, "0", "STARTING ITEMS");
        }

        foreach (var kvp in startingPerkTitles)
        {
            menuSettingsManager.Register(menuTag, kvp.Value, kvp.Key, SettingType.Integer, "0", "STARTING PERKS");
        }

        foreach (var buff in buffSettings)
        {
            menuSettingsManager.Register(menuTag, buff.Name, buff.Key, SettingType.Float, "0", "BUFFS");
        }

        menuSettingsManager.Register(menuTag, "Mass Scaling Mult", "mass_speed_mult", SettingType.Float, "1", "MASS");
        menuSettingsManager.Register(menuTag, "Mass Starting Speed", "mass_start_speed", SettingType.Float, "1", "MASS");
        menuSettingsManager.Register(menuTag, "Mass Acceleration Mult", "mass_accel_mult", SettingType.Float, "1", "MASS");
        menuSettingsManager.Register(menuTag, "Rubberband Mult", "mass_rubberband_mult", SettingType.Float, "1", "MASS");
        menuSettingsManager.Register(menuTag, "Enable Rubberband", "mass_rubberband_enabled", SettingType.Bool, "true", "MASS");

        menuSettingsManager.Register(menuTag, "Scale Teeth Each Loop", "scale_teeth_each_loop", SettingType.Bool, "true", "ENEMY SCALING");
        menuSettingsManager.Register(menuTag, "Scale Rat King Each Loop", "scale_rat_king_each_loop", SettingType.Bool, "true", "ENEMY SCALING");
        menuSettingsManager.Register(menuTag, "Scale Remains Each Loop", "scale_remains_each_loop", SettingType.Bool, "true", "ENEMY SCALING");

        menuSettingsManager.Register(menuTag, "Perk Every X", "breakroom_perk_machine_every", SettingType.Integer, "1", "BREAKROOMS");
        menuSettingsManager.Register(menuTag, "Perk After X", "breakroom_perk_machine_after", SettingType.Integer, "0", "BREAKROOMS");
        menuSettingsManager.Register(menuTag, "Experimental Every X", "breakroom_experimental_machine_every", SettingType.Integer, "0", "BREAKROOMS");
        menuSettingsManager.Register(menuTag, "Experimental After X", "breakroom_experimental_machine_after", SettingType.Integer, "0", "BREAKROOMS");
        menuSettingsManager.Register(menuTag, "ATM Every X", "breakroom_atm_every", SettingType.Integer, "1", "BREAKROOMS");
        menuSettingsManager.Register(menuTag, "ATM After X", "breakroom_atm_after", SettingType.Integer, "0", "BREAKROOMS");

        menuSettingsManager.Register(menuTag, "Vender Every X", "breakroom_vending_every", SettingType.Integer, "1", "BREAKROOMS");
        menuSettingsManager.Register(menuTag, "Vender After X", "breakroom_vending_after", SettingType.Integer, "0", "BREAKROOMS");
        menuSettingsManager.Register(menuTag, "Vender T2 Every X", "breakroom_vending_t2_every", SettingType.Integer, "1", "BREAKROOMS");
        menuSettingsManager.Register(menuTag, "Vender T2 After X", "breakroom_vending_t2_after", SettingType.Integer, "0", "BREAKROOMS");
        menuSettingsManager.Register(menuTag, "Recycler Every X", "breakroom_recycler_every", SettingType.Integer, "1", "BREAKROOMS");
        menuSettingsManager.Register(menuTag, "Recycler After X", "breakroom_recycler_after", SettingType.Integer, "0", "BREAKROOMS");
        menuSettingsManager.Register(menuTag, "Cryo Refueler Every X", "breakroom_cryo_refueler_every", SettingType.Integer, "1", "BREAKROOMS");
        menuSettingsManager.Register(menuTag, "Cryo Refueler After X", "breakroom_cryo_refueler_after", SettingType.Integer, "0", "BREAKROOMS");
        menuSettingsManager.Register(menuTag, "Monolith Every X", "breakroom_rho_monolith_every", SettingType.Integer, "3", "BREAKROOMS");
        menuSettingsManager.Register(menuTag, "Monolith After X", "breakroom_rho_monolith_after", SettingType.Integer, "3", "BREAKROOMS");
        menuSettingsManager.Register(menuTag, "Bazaar Every X", "breakroom_manhole_hatch_every", SettingType.Integer, "3", "BREAKROOMS");
        menuSettingsManager.Register(menuTag, "Bazaar After X", "breakroom_manhole_hatch_after", SettingType.Integer, "5", "BREAKROOMS");

        menuSettingsManager.Register(menuTag, "Campaign Or Challenge Win", "campaign_or_challenge_win", SettingType.Bool, "false", "WIN CONDITION");
        menuSettingsManager.Register(menuTag, "Cycles Condition", "cycles_win_enabled", SettingType.Bool, "false", "WIN CONDITION");
        menuSettingsManager.Register(menuTag, "Win After X Cycles", "cycles_win_amount", SettingType.Integer, "1", "WIN CONDITION");
        menuSettingsManager.Register(menuTag, "Height Condition", "height_win_enabled", SettingType.Bool, "false", "WIN CONDITION");
        menuSettingsManager.Register(menuTag, "Win After Climbing X", "height_win_amount", SettingType.Float, "1000", "WIN CONDITION");


        menuSettingsManager.Register(menuTag, "Always Snowing", "fun_always_snowing", SettingType.Bool, "false", "FUN");
        menuSettingsManager.Register(menuTag, "Consumah", "fun_consumah", SettingType.Bool, "false", "FUN");
        menuSettingsManager.Register(menuTag, "Trinket Roaches", "fun_boost_roaches", SettingType.Bool, "false", "FUN");
        menuSettingsManager.Register(menuTag, "All Perks Stack Linearly", "fun_limitless_perks", SettingType.Bool, "false", "FUN");
        menuSettingsManager.Register(menuTag, "Buffs/Debuffs Never End", "fun_limitless_buffs", SettingType.Bool, "false", "FUN");


        menuSettingsManager.Register(menuTag, "Unseen Tuocs", "fun_unseen_amount", SettingType.Integer, "0", "ARACHNOPHOBIA");
        menuSettingsManager.Register(menuTag, "Unseen Cimim", "fun_cimim_amount", SettingType.Integer, "0", "ARACHNOPHOBIA");
        menuSettingsManager.Register(menuTag, "4d Unseen", "always_see_unseen", SettingType.Bool, "true", "ARACHNOPHOBIA");
        menuSettingsManager.Register(menuTag, "Has Eye", "unseen_has_eye", SettingType.Bool, "true", "ARACHNOPHOBIA");
        menuSettingsManager.Register(menuTag, "Eye Size", "unseen_eye_size", SettingType.Float, "1", "ARACHNOPHOBIA");
        menuSettingsManager.Register(menuTag, "General Size", "unseen_general_size", SettingType.Float, "1", "ARACHNOPHOBIA");
        menuSettingsManager.Register(menuTag, "Use Buttons", "unseen_buttons", SettingType.Bool, "true", "ARACHNOPHOBIA");
        menuSettingsManager.Register(menuTag, "Use Items", "unseen_use_items", SettingType.Bool, "true", "ARACHNOPHOBIA");
        menuSettingsManager.Register(menuTag, "Grab Items", "unseen_grab_items", SettingType.Bool, "true", "ARACHNOPHOBIA");
        menuSettingsManager.Register(menuTag, "Quirks", "unseen_quirks", SettingType.Bool, "true", "ARACHNOPHOBIA");
        menuSettingsManager.Register(menuTag, "Crouch Copy", "unseen_crouch", SettingType.Bool, "true", "ARACHNOPHOBIA");
        menuSettingsManager.Register(menuTag, "Leg Length", "unseen_leg_length", SettingType.Float, "14", "ARACHNOPHOBIA");
        menuSettingsManager.Register(menuTag, "Min Leg Segments", "unseen_leg_seg_min", SettingType.Integer, "1", "ARACHNOPHOBIA");
        menuSettingsManager.Register(menuTag, "Max Leg Segments", "unseen_leg_seg_max", SettingType.Integer, "5", "ARACHNOPHOBIA");
        menuSettingsManager.Register(menuTag, "Leg Min", "unseen_leg_min", SettingType.Integer, "5", "ARACHNOPHOBIA");
        menuSettingsManager.Register(menuTag, "Leg Max", "unseen_leg_max", SettingType.Integer, "12", "ARACHNOPHOBIA");
        menuSettingsManager.Register(menuTag, "Leg Start", "unseen_leg_start", SettingType.Integer, "8", "ARACHNOPHOBIA");
        menuSettingsManager.Register(menuTag, "Leg Form Wait Min", "unseen_leg_form_min", SettingType.Float, "1", "ARACHNOPHOBIA");
        menuSettingsManager.Register(menuTag, "Leg Form Wait Max", "unseen_leg_form_max", SettingType.Float, "5", "ARACHNOPHOBIA");
        menuSettingsManager.Register(menuTag, "Step Time", "unseen_step_time", SettingType.Float, "0.1", "ARACHNOPHOBIA");
        menuSettingsManager.Register(menuTag, "Step Gap", "unseen_step_gap", SettingType.Float, "0.28", "ARACHNOPHOBIA");
        menuSettingsManager.Register(menuTag, "Step Count", "unseen_step_count", SettingType.Integer, "1", "ARACHNOPHOBIA");
        menuSettingsManager.Register(menuTag, "Tap Time", "unseen_tap_time", SettingType.Float, "0.1", "ARACHNOPHOBIA");
        menuSettingsManager.Register(menuTag, "Tap Gap", "unseen_tap_gap", SettingType.Float, "0.1", "ARACHNOPHOBIA");
        menuSettingsManager.Register(menuTag, "Tap Dmg", "unseen_tap_dmg", SettingType.Float, "1", "ARACHNOPHOBIA");
        menuSettingsManager.Register(menuTag, "Prop Dmg", "unseen_prop_dmg", SettingType.Float, "0.3", "ARACHNOPHOBIA");
        menuSettingsManager.Register(menuTag, "Move Cap", "unseen_move_cap", SettingType.Float, "28", "ARACHNOPHOBIA");
        menuSettingsManager.Register(menuTag, "Force Per Leg", "unseen_force_per_leg", SettingType.Float, "6", "ARACHNOPHOBIA");
        menuSettingsManager.Register(menuTag, "Patrol Speed", "unseen_patrol_spd", SettingType.Float, "22", "ARACHNOPHOBIA");
        menuSettingsManager.Register(menuTag, "Catch Speed", "unseen_catch_spd", SettingType.Float, "28", "ARACHNOPHOBIA");
        menuSettingsManager.Register(menuTag, "Attack Spweed", "unseen_attack_spd", SettingType.Float, "12", "ARACHNOPHOBIA");
        menuSettingsManager.Register(menuTag, "Grab Chance", "unseen_grab_rate", SettingType.Float, "0.8", "ARACHNOPHOBIA");
        menuSettingsManager.Register(menuTag, "Throw Speed", "unseen_throw_spd", SettingType.Float, "8", "ARACHNOPHOBIA");
        menuSettingsManager.Register(menuTag, "Carry Time", "unseen_carry_time", SettingType.Float, "4", "ARACHNOPHOBIA");
        menuSettingsManager.Register(menuTag, "Carry Speed Mult", "unseen_carry_spd", SettingType.Float, "0.25", "ARACHNOPHOBIA");
        menuSettingsManager.Register(menuTag, "Give Chnce", "unseen_give_rate", SettingType.Float, "0.5", "ARACHNOPHOBIA");
        menuSettingsManager.Register(menuTag, "Button Chance", "unseen_button_rate", SettingType.Float, "0.35", "ARACHNOPHOBIA");
        menuSettingsManager.Register(menuTag, "Friend Use Chance", "unseen_friend_use", SettingType.Float, "0.35", "ARACHNOPHOBIA");
        menuSettingsManager.Register(menuTag, "Hostile Use Chance", "unseen_hostile_use", SettingType.Float, "0.7", "ARACHNOPHOBIA");
        menuSettingsManager.Register(menuTag, "Quirk Min", "unseen_quirk_min", SettingType.Integer, "1", "ARACHNOPHOBIA");
        menuSettingsManager.Register(menuTag, "Quirk Max", "unseen_quirk_max", SettingType.Integer, "3", "ARACHNOPHOBIA");
        menuSettingsManager.Register(menuTag, "Quirk Chance", "unseen_quirk_rate", SettingType.Float, "0.65", "ARACHNOPHOBIA");
        menuSettingsManager.Register(menuTag, "Quirk Min Time", "unseen_quirk_min_time", SettingType.Float, "18", "ARACHNOPHOBIA");
        menuSettingsManager.Register(menuTag, "Quirk Max Time", "unseen_quirk_max_time", SettingType.Float, "42", "ARACHNOPHOBIA");
        menuSettingsManager.Register(menuTag, "Shadow Size", "unseen_shadow_size", SettingType.Integer, "64", "ARACHNOPHOBIA");
        menuSettingsManager.Register(menuTag, "Shadow Frames", "unseen_shadow_frames", SettingType.Integer, "13", "ARACHNOPHOBIA");
        menuSettingsManager.Register(menuTag, "Core Size", "unseen_core_size", SettingType.Float, "0.5", "ARACHNOPHOBIA");
        menuSettingsManager.Register(menuTag, "Tendril Min", "unseen_tendril_min", SettingType.Integer, "20", "ARACHNOPHOBIA");
        menuSettingsManager.Register(menuTag, "Tendril Max", "unseen_tendril_max", SettingType.Integer, "32", "ARACHNOPHOBIA");
        menuSettingsManager.Register(menuTag, "Tendril Min Len", "unseen_tendril_min_len", SettingType.Float, "0.48", "ARACHNOPHOBIA");
        menuSettingsManager.Register(menuTag, "Tendril Max Len", "unseen_tendril_max_len", SettingType.Float, "3.05", "ARACHNOPHOBIA");
        menuSettingsManager.Register(menuTag, "Tendril Thickness", "unseen_tendril_thick", SettingType.Float, "7.5", "ARACHNOPHOBIA");
        menuSettingsManager.Register(menuTag, "Tendril Curving", "unseen_tendril_curve", SettingType.Float, "1", "ARACHNOPHOBIA");
        menuSettingsManager.Register(menuTag, "Smoothing Passes", "unseen_cell_passes", SettingType.Integer, "1", "ARACHNOPHOBIA");
        menuSettingsManager.Register(menuTag, "Edge Fade (px)", "unseen_edge_fade", SettingType.Float, "1", "ARACHNOPHOBIA");
        menuSettingsManager.Register(menuTag, "Leg Roll Chance", "unseen_leg_roll", SettingType.Float, "0.35", "ARACHNOPHOBIA");

    }

    public void OnPanelClicked()
    {
        menuSettingsManager?.LoadMenu(menuTag);
    }
}
