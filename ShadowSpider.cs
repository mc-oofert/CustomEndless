using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.VisualScripting;
using UnityEngine;
namespace Celestial.CustomEndless
{
    [HarmonyPatch(typeof(M_Level), "OnEnter")]
    public static class UnseenLevelEnterPatch
    {
        [HarmonyPostfix]
        public static void Postfix(M_Level __instance)
        {
            if (__instance != null)
            {
                UnseenPopulationManager.GetOrCreate().SetCurrentLevel(__instance);
                CimimPopulationManager.GetOrCreate().SetCurrentLevel(__instance);
            }
        }
    }
    public static class UnseenPlayerInteractionTracker
    {
        public static Item_Object droppedItem;
        public static Rigidbody tossedProp;
        public static GameEntity hitEntity;
        public static float droppedItemTime;
        public static float tossedPropTime;
        public static float hitEntityTime;
        public static int droppedItemVersion;
        public static int tossedPropVersion;
        public static int hitEntityVersion;
        public static void ReportDroppedItem(Item_Object item)
        {
            if (item == null) return;
            droppedItem = item;
            droppedItemTime = Time.time;
            droppedItemVersion++;
        }
        public static void ReportTossedProp(Rigidbody rigidbody)
        {
            if (rigidbody == null) return;
            tossedProp = rigidbody;
            tossedPropTime = Time.time;
            tossedPropVersion++;
        }
        public static void ReportHitEntity(GameEntity entity)
        {
            if (entity == null || entity is ENT_Player) return;
            hitEntity = entity;
            hitEntityTime = Time.time;
            hitEntityVersion++;
        }
    }
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.DropItemIntoWorld))]
    public static class UnseenPlayerDropItemPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Inventory __instance, Item item)
        {
            if (__instance == null || item == null || __instance != Inventory.instance) return;
            UnseenPlayerInteractionTracker.ReportDroppedItem(item.GetDropObject());
        }
    }
    [HarmonyPatch(typeof(ENT_Player), "DropGrabbedProp")]
    public static class UnseenPlayerTossPropPatch
    {
        [HarmonyPrefix]
        public static void Prefix(ENT_Player __instance, int hand, out Rigidbody __state)
        {
            __state = null;
            if (__instance == null || __instance != ENT_Player.GetPlayer() || hand < 0 || hand >= __instance.hands.Length || __instance.hands[hand].grabTarget == null) return;
            __state = __instance.hands[hand].grabTarget.GetRigidbody();
        }
        [HarmonyPostfix]
        public static void Postfix(Rigidbody __state)
        {
            UnseenPlayerInteractionTracker.ReportTossedProp(__state);
        }
    }
    [HarmonyPatch(typeof(GameEntity), nameof(GameEntity.Damage))]
    public static class UnseenPlayerDamagePatch
    {
        [HarmonyPostfix]
        public static void Postfix(GameEntity __instance, Damageable.DamageInfo info)
        {
            ENT_Player player = ENT_Player.GetPlayer();
            if (__instance == null || info == null || player == null) return;
            if (info.sourceEntity == player || info.sourceObject == player.gameObject || info.HasTag("player")) UnseenPlayerInteractionTracker.ReportHitEntity(__instance);
        }
    }
    [HarmonyPatch(typeof(ENT_Player), nameof(ENT_Player.Kill), new[] { typeof(string), typeof(Damageable.DamageInfo) })]
    public static class UnseenPlayerKillPatch
    {
        [HarmonyPrefix]
        public static void Prefix(ref string type, Damageable.DamageInfo damageInfo)
        {
            if (damageInfo != null && damageInfo.HasTag("unseen")) type = "unseen";
        }
    }
    [HarmonyPatch(typeof(CL_GameManager), nameof(CL_GameManager.Die))]
    public static class UnseenDeathMessagePatch
    {
        [HarmonyPostfix]
        public static void Postfix(string type)
        {
            if (!string.Equals(type, "unseen", StringComparison.OrdinalIgnoreCase) || CL_GameManager.gMan == null || CL_GameManager.gMan.statManager == null) return;
            CL_GameManager.gMan.statManager.GetScoreScreen().SetDeathText("Inspected");
        }
    }
    public sealed class UnseenPopulationManager : MonoBehaviour
    {
        private static UnseenPopulationManager instance;
        private readonly List<Unseen> unseen = new List<Unseen>();
        private M_Level currentLevel;
        private float nextPopulationCheck;
        public static UnseenPopulationManager GetOrCreate()
        {
            if (instance != null) return instance;
            GameObject managerObject = new GameObject("UnseenPopulationManager");
            DontDestroyOnLoad(managerObject);
            instance = managerObject.AddComponent<UnseenPopulationManager>();
            return instance;
        }
        public void SetCurrentLevel(M_Level level)
        {
            currentLevel = level;
            RefreshPopulation();
        }
        private void Update()
        {
            if (Time.time < nextPopulationCheck) return;
            nextPopulationCheck = Time.time + 0.5f;
            if (WorldLoader.instance != null && WorldLoader.instance.GetCurrentLevel() != null && WorldLoader.instance.GetCurrentLevel().GetLevel() != null) currentLevel = WorldLoader.instance.GetCurrentLevel().GetLevel();
            RefreshPopulation();
        }
        private void RefreshPopulation()
        {
            ENT_Player player = ENT_Player.GetPlayer();
            EndlessConfiguration.GetUnseenPopulationForLevel(currentLevel, out int hostileTarget, out int helpfulTarget);
            for (int i = unseen.Count - 1; i >= 0; i--)
            {
                Unseen creature = unseen[i];
                if (creature == null || !creature.gameObject.activeInHierarchy || player != null && Vector3.Distance(creature.transform.position, player.transform.position) > 100f)
                {
                    if (creature != null) Destroy(creature.gameObject);
                    unseen.RemoveAt(i);
                }
            }
            TrimPopulation(false, hostileTarget);
            TrimPopulation(true, helpfulTarget);
            if (player == null || currentLevel == null || !currentLevel.gameObject.activeInHierarchy) return;
            SpawnPopulation(false, hostileTarget);
            SpawnPopulation(true, helpfulTarget);
        }
        private void TrimPopulation(bool helpful, int target)
        {
            int count = 0;
            for (int i = unseen.Count - 1; i >= 0; i--)
            {
                Unseen creature = unseen[i];
                if (creature == null || creature.IsHelpful != helpful) continue;
                count++;
                if (count <= target) continue;
                Destroy(creature.gameObject);
                unseen.RemoveAt(i);
            }
        }
        private void SpawnPopulation(bool helpful, int target)
        {
            int count = 0;
            for (int i = 0; i < unseen.Count; i++) if (unseen[i] != null && unseen[i].IsHelpful == helpful) count++;
            while (count < target)
            {
                unseen.Add(Unseen.Spawn(GetEntranceSpawnPosition(currentLevel), currentLevel, helpful));
                count++;
            }
        }
        private Vector3 GetEntranceSpawnPosition(M_Level level)
        {
            Transform entrance = level.GetLevelEntrance();
            Vector3 forward = entrance != null ? entrance.forward : Vector3.forward;
            Vector3 right = entrance != null ? entrance.right : Vector3.right;
            Vector3 origin = entrance != null ? entrance.position : level.transform.position;
            return origin + forward * UnityEngine.Random.Range(4f, 10f) + right * UnityEngine.Random.Range(-4f, 4f) + Vector3.up * UnityEngine.Random.Range(2f, 5f);
        }
    }
    public sealed class Unseen : MonoBehaviour
    {
        public bool IsHelpful
        {
            get
            {
                return this.helpful;
            }
        }

        public static Unseen Spawn(Vector3 position, M_Level sourceLevel, bool helpful)
        {
            GameObject gameObject = new GameObject("Unseen");
            gameObject.transform.position = position;
            Unseen unseen = gameObject.AddComponent<Unseen>();
            unseen.Initialize(sourceLevel, helpful);
            gameObject.transform.SetParent(sourceLevel.transform, true);
            return unseen;
        }

        private void ApplyArachnophobiaSettings()
        {
            buttonsEnabled = EndlessConfiguration.CustomUIManager.GetToggleState("unseen_buttons");
            itemUseEnabled = EndlessConfiguration.CustomUIManager.GetToggleState("unseen_use_items");
            itemGrabEnabled = EndlessConfiguration.CustomUIManager.GetToggleState("unseen_grab_items");
            quirksEnabled = EndlessConfiguration.CustomUIManager.GetToggleState("unseen_quirks");
            crouchEnabled = EndlessConfiguration.CustomUIManager.GetToggleState("unseen_crouch");
            legTotalLength = Mathf.Max(0.1f, EndlessConfiguration.CustomUIManager.GetFloatState("unseen_leg_length"));
            minimumLegSegments = Mathf.Max(1, EndlessConfiguration.CustomUIManager.GetCountState("unseen_leg_seg_min"));
            maximumLegSegments = Mathf.Max(minimumLegSegments, EndlessConfiguration.CustomUIManager.GetCountState("unseen_leg_seg_max"));
            hasEye = EndlessConfiguration.CustomUIManager.GetToggleState("unseen_has_eye");
            eyeSize *= Mathf.Max(0.01f, EndlessConfiguration.CustomUIManager.GetFloatState("unseen_eye_size"));
            eyeOrbitDistance *= Mathf.Max(0.01f, EndlessConfiguration.CustomUIManager.GetFloatState("unseen_eye_size"));
            minimumLegCount = Mathf.Max(1, EndlessConfiguration.CustomUIManager.GetCountState("unseen_leg_min"));
            maximumLegCount = Mathf.Max(minimumLegCount, EndlessConfiguration.CustomUIManager.GetCountState("unseen_leg_max"));
            startingLegCount = Mathf.Clamp(EndlessConfiguration.CustomUIManager.GetCountState("unseen_leg_start"), minimumLegCount, maximumLegCount);
            legMutationIntervalMinimum = Mathf.Max(0.05f, EndlessConfiguration.CustomUIManager.GetFloatState("unseen_leg_form_min"));
            legMutationIntervalMaximum = Mathf.Max(legMutationIntervalMinimum, EndlessConfiguration.CustomUIManager.GetFloatState("unseen_leg_form_max"));
            walkStepDuration = Mathf.Max(0.01f, EndlessConfiguration.CustomUIManager.GetFloatState("unseen_step_time"));
            walkStepInterval = Mathf.Max(0.01f, EndlessConfiguration.CustomUIManager.GetFloatState("unseen_step_gap"));
            maximumConcurrentWalkingLegs = Mathf.Max(1, EndlessConfiguration.CustomUIManager.GetCountState("unseen_step_count"));
            tapStepDuration = Mathf.Max(0.01f, EndlessConfiguration.CustomUIManager.GetFloatState("unseen_tap_time"));
            tapInterval = Mathf.Max(0.01f, EndlessConfiguration.CustomUIManager.GetFloatState("unseen_tap_gap"));
            playerLegTapDamage = Mathf.Max(0f, EndlessConfiguration.CustomUIManager.GetFloatState("unseen_tap_dmg"));
            entityTapDamage = Mathf.Max(0f, EndlessConfiguration.CustomUIManager.GetFloatState("unseen_prop_dmg"));
            maximumVelocity = Mathf.Max(0.1f, EndlessConfiguration.CustomUIManager.GetFloatState("unseen_move_cap"));
            hostilePatrolSpeed = Mathf.Max(0.1f, EndlessConfiguration.CustomUIManager.GetFloatState("unseen_patrol_spd"));
            hostileCatchUpSpeed = Mathf.Max(0.1f, EndlessConfiguration.CustomUIManager.GetFloatState("unseen_catch_spd"));
            hostileAttackApproachSpeed = Mathf.Max(0.1f, EndlessConfiguration.CustomUIManager.GetFloatState("unseen_attack_spd"));
            rigidbodyGrabChance = Mathf.Clamp01(EndlessConfiguration.CustomUIManager.GetFloatState("unseen_grab_rate"));
            rigidbodyThrowForce = Mathf.Max(0f, EndlessConfiguration.CustomUIManager.GetFloatState("unseen_throw_spd"));
            carryDuration = Mathf.Max(0.1f, EndlessConfiguration.CustomUIManager.GetFloatState("unseen_carry_time"));
            carrySpeedMultiplier = Mathf.Max(0f, EndlessConfiguration.CustomUIManager.GetFloatState("unseen_carry_spd"));
            friendlyGiveChance = Mathf.Clamp01(EndlessConfiguration.CustomUIManager.GetFloatState("unseen_give_rate"));
            buttonPressChance = Mathf.Clamp01(EndlessConfiguration.CustomUIManager.GetFloatState("unseen_button_rate"));
            friendlyHandItemUseChance = Mathf.Clamp01(EndlessConfiguration.CustomUIManager.GetFloatState("unseen_friend_use"));
            hostileHandItemUseChance = Mathf.Clamp01(EndlessConfiguration.CustomUIManager.GetFloatState("unseen_hostile_use"));
            quirkMinimumCount = Mathf.Max(0, EndlessConfiguration.CustomUIManager.GetCountState("unseen_quirk_min"));
            quirkMaximumCount = Mathf.Max(quirkMinimumCount, EndlessConfiguration.CustomUIManager.GetCountState("unseen_quirk_max"));
            quirkActivationChance = Mathf.Clamp01(EndlessConfiguration.CustomUIManager.GetFloatState("unseen_quirk_rate"));
            quirkFrequencyMinimum = Mathf.Max(0.1f, EndlessConfiguration.CustomUIManager.GetFloatState("unseen_quirk_min_time"));
            quirkFrequencyMaximum = Mathf.Max(quirkFrequencyMinimum, EndlessConfiguration.CustomUIManager.GetFloatState("unseen_quirk_max_time"));
            shadowTextureSize = Mathf.Max(8, EndlessConfiguration.CustomUIManager.GetCountState("unseen_shadow_size"));
            shadowGenerationFrameCount = Mathf.Max(1, EndlessConfiguration.CustomUIManager.GetCountState("unseen_shadow_frames"));
            shadowCoreRadiusFraction = Mathf.Max(0.01f, EndlessConfiguration.CustomUIManager.GetFloatState("unseen_core_size"));
            shadowTendrilCountMinimum = Mathf.Max(0, EndlessConfiguration.CustomUIManager.GetCountState("unseen_tendril_min"));
            shadowTendrilCountMaximum = Mathf.Max(shadowTendrilCountMinimum, EndlessConfiguration.CustomUIManager.GetCountState("unseen_tendril_max"));
            shadowTendrilLengthMinimum = Mathf.Max(0f, EndlessConfiguration.CustomUIManager.GetFloatState("unseen_tendril_min_len"));
            shadowTendrilLengthMaximum = Mathf.Max(shadowTendrilLengthMinimum, EndlessConfiguration.CustomUIManager.GetFloatState("unseen_tendril_max_len"));
            shadowTendrilThickness = Mathf.Max(0.01f, EndlessConfiguration.CustomUIManager.GetFloatState("unseen_tendril_thick"));
            shadowTendrilCurve = Mathf.Max(0f, EndlessConfiguration.CustomUIManager.GetFloatState("unseen_tendril_curve"));
            shadowCellularPasses = Mathf.Max(0, EndlessConfiguration.CustomUIManager.GetCountState("unseen_cell_passes"));
            shadowEdgeFadePixels = Mathf.Max(0f, EndlessConfiguration.CustomUIManager.GetFloatState("unseen_edge_fade"));
            legSegmentRollChance = Mathf.Clamp01(EndlessConfiguration.CustomUIManager.GetFloatState("unseen_leg_roll"));
            movementForcePerAnchoredLeg = Mathf.Max(0f, EndlessConfiguration.CustomUIManager.GetFloatState("unseen_force_per_leg"));
            generalSize = Mathf.Max(0.05f, EndlessConfiguration.CustomUIManager.GetFloatState("unseen_general_size"));
            legTotalLength *= generalSize;
            eyeSize *= generalSize;
            eyeOrbitDistance *= generalSize;
            shadowCoreRadiusFraction *= generalSize;
            shadowTendrilLengthMinimum *= generalSize;
            shadowTendrilLengthMaximum *= generalSize;
            shadowTendrilThickness *= generalSize;
            shadowTendrilTipThickness *= generalSize;
        }
        private void Initialize(M_Level sourceLevel, bool isHelpful)
        {
            this.level = sourceLevel;
            this.helpful = isHelpful;
            this.ApplyArachnophobiaSettings();
            this.personalityPhase = UnityEngine.Random.Range(0f, 100f);
            this.pace = UnityEngine.Random.Range(0.8f, 1.15f);
            this.curiosity = UnityEngine.Random.Range(0.65f, 1.35f);
            this.nextSocialTime = Time.time + UnityEngine.Random.Range(2f, 6f);
            bool flag = this.helpful;
            if (flag)
            {
                this.eyeColor = UnityEngine.Random.ColorHSV(0f, 1f, 0.55f, 1f, 0.65f, 1f);
            }
            this.body = base.gameObject.AddComponent<Rigidbody>();
            this.body.useGravity = false;
            this.body.constraints = RigidbodyConstraints.FreezeRotation;
            this.body.interpolation = RigidbodyInterpolation.Interpolate;
            this.body.drag = 1.2f;
            SphereCollider sphereCollider = base.gameObject.AddComponent<SphereCollider>();
            sphereCollider.isTrigger = true;
            sphereCollider.radius = this.bodyCollisionRadius;
            this.CreateShadow();
            for (int i = 0; i < 8; i++)
            {
                int num = this.nextLegId;
                this.nextLegId = num + 1;
                this.CreateLeg(num, false);
            }
            this.nextLegMutationTime = Time.time + UnityEngine.Random.Range(1f, 5f);
            this.AssignQuirks();
            this.ChooseNextDestination();
        }

        private void FixedUpdate()
        {
            bool flag = this.body == null;
            if (!flag)
            {
                this.UpdateParentLevel();
                int settledAnchorCount = this.GetSettledAnchorCount();
                float num = (this.activeQuirk != Unseen.QuirkType.None) ? 0f : ((this.behaviorState == Unseen.BehaviorState.Recovering && this.GetRecoveryLockCount() > 0) ? 0f : ((this.legs.Count == 0) ? 1f : (1f - (float)settledAnchorCount / (float)this.legs.Count)));
                bool flag2 = num > 0f;
                if (flag2)
                {
                    this.body.AddForce(Physics.gravity * this.body.mass * num, ForceMode.Force);
                }
                this.UpdateFriendlyCrouchMirror();
                bool flag3 = this.UpdateQuirkBehavior(settledAnchorCount);
                if (flag3)
                {
                    this.CapVelocity();
                }
                else
                {
                    bool flag4 = this.helpful;
                    if (flag4)
                    {
                        bool flag5 = this.behaviorState == Unseen.BehaviorState.Recovering;
                        if (flag5)
                        {
                            this.UpdateRecoveryMovement(settledAnchorCount);
                            this.CapVelocity();
                        }
                        else
                        {
                            this.UpdateHelpfulBehavior(settledAnchorCount);
                            this.CapVelocity();
                        }
                    }
                    else
                    {
                        bool flag6 = this.TryUpdateHostileDroppedItem(settledAnchorCount);
                        if (flag6)
                        {
                            this.CapVelocity();
                        }
                        else
                        {
                            bool flag7 = this.carriedRigidbody != null || this.manipulatedRigidbody != null;
                            if (flag7)
                            {
                                this.UpdateHostileCarryBehavior(settledAnchorCount);
                                this.CapVelocity();
                            }
                            else
                            {
                                ENT_Player player = ENT_Player.GetPlayer();
                                bool flag8 = this.IsVisibleToPlayer();
                                bool flag9 = flag8;
                                if (flag9)
                                {
                                    this.attackSightUntil = Time.time + this.attackSightHoldDuration;
                                }
                                bool flag10 = flag8 || Time.time < this.attackSightUntil || (player != null && Time.time < this.handItemAggressionUntil);
                                bool flag11 = flag10 && this.behaviorState != Unseen.BehaviorState.Recovering;
                                if (flag11)
                                {
                                    bool flag12 = flag8 && !this.playerWasVisible && UnityEngine.Random.value < this.playerLookTaskAbandonChance;
                                    if (flag12)
                                    {
                                        this.AbandonCurrentTask();
                                    }
                                    bool flag13 = flag8;
                                    if (flag13)
                                    {
                                        this.playerWasVisible = true;
                                    }
                                    this.behaviorState = Unseen.BehaviorState.Attacking;
                                    bool flag14 = player != null;
                                    if (flag14)
                                    {
                                        Vector3 vector = player.transform.position - base.transform.position;
                                        float magnitude = vector.magnitude;
                                        bool flag15 = vector.sqrMagnitude > 0.0001f;
                                        if (flag15)
                                        {
                                            this.curvedMovementDirection = vector.normalized;
                                        }
                                        bool flag16 = magnitude > this.hostilePlayerTapRange;
                                        if (flag16)
                                        {
                                            this.ClearTap();
                                            this.inspectedPlayer = null;
                                            this.inspectedItem = null;
                                            this.inspectedEntity = null;
                                            this.inspectedButton = null;
                                            this.ChargePlayer(settledAnchorCount);
                                        }
                                        else
                                        {
                                            bool flag17 = this.IsPlayerLookingAtUnseen();
                                            if (flag17)
                                            {
                                                this.inspectedPlayer = player;
                                                this.inspectedItem = null;
                                                this.inspectedEntity = null;
                                                this.inspectedButton = null;
                                            }
                                            else
                                            {
                                                this.ClearTap();
                                                this.inspectedPlayer = null;
                                            }
                                            this.body.AddForce(-this.body.velocity * Mathf.Max(0f, this.hostilePlayerTapBraking), ForceMode.Acceleration);
                                        }
                                    }
                                    this.CapVelocity();
                                }
                                else
                                {
                                    bool flag18 = this.behaviorState == Unseen.BehaviorState.Attacking;
                                    if (flag18)
                                    {
                                        this.ClearTap();
                                        this.inspectedPlayer = null;
                                        this.behaviorState = Unseen.BehaviorState.Moving;
                                    }
                                    bool flag19 = !flag10;
                                    if (flag19)
                                    {
                                        this.playerWasVisible = false;
                                        bool flag20 = this.behaviorState != Unseen.BehaviorState.Recovering;
                                        if (flag20)
                                        {
                                            this.KeepDistanceFromPlayer(settledAnchorCount);
                                        }
                                    }
                                    bool flag21 = this.behaviorState == Unseen.BehaviorState.Recovering;
                                    if (flag21)
                                    {
                                        this.UpdateRecoveryMovement(settledAnchorCount);
                                        this.CapVelocity();
                                    }
                                    else
                                    {
                                        bool flag22 = this.behaviorState == Unseen.BehaviorState.Inspecting;
                                        if (flag22)
                                        {
                                            ENT_Player player2 = ENT_Player.GetPlayer();
                                            bool flag23 = player2 != null && (Vector3.Distance(base.transform.position, player2.transform.position) > this.hostileCatchUpDistance || player2.transform.position.y - base.transform.position.y > 10f);
                                            if (flag23)
                                            {
                                                this.ChooseNextDestination();
                                                this.UpdateDestinationMovement(settledAnchorCount);
                                                this.CapVelocity();
                                            }
                                            else
                                            {
                                                this.ApplyInspectionIdleMovement();
                                                bool flag24 = Time.time >= this.inspectUntil;
                                                if (flag24)
                                                {
                                                    this.ChooseNextDestination();
                                                }
                                                this.CapVelocity();
                                            }
                                        }
                                        else
                                        {
                                            this.UpdateDestinationMovement(settledAnchorCount);
                                            this.CapVelocity();
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private void LateUpdate()
        {
            this.UpdateConsumption();
            this.UpdateItemManipulation();
            ENT_Player player = ENT_Player.GetPlayer();
            this.playerCamera = ((player != null && player.cam != null) ? player.cam : Camera.main);
            this.ConfigureOverlayCamera();
            bool flag = this.playerCamera != null && this.shadowRenderer != null;
            if (flag)
            {
                Vector3 vector = this.playerCamera.transform.position - this.shadowRenderer.transform.position;
                bool flag2 = vector.sqrMagnitude > 0.0001f;
                if (flag2)
                {
                    this.shadowRenderer.transform.rotation = Quaternion.LookRotation(vector.normalized, this.playerCamera.transform.up);
                }
            }
            this.UpdateEye();
            bool flag3 = this.shadowRenderer != null;
            if (flag3)
            {
                float num = Mathf.Sin(Time.time * (1.3f * this.pace) + this.personalityPhase) * 0.025f;
                float num2 = Mathf.Lerp(1f, Mathf.Clamp(this.friendlyCrouchBodyScale, 0.1f, 1f), this.crouchBlend);
                this.shadowRenderer.transform.localPosition = Vector3.down * this.crouchBlend * Mathf.Max(0f, this.friendlyCrouchBodyDrop);
                this.shadowRenderer.transform.localScale = new Vector3((1.25f + num) * num2, (1.25f - num * 0.6f) * num2, 1f);
                this.UpdateShadowFadeNoise();
            }
            this.UpdateLegAppearances();
            this.UpdateLegMutation();
            this.UpdateCrouchLegOrigins();
            foreach (Unseen.Leg leg in this.legs)
            {
                this.UpdateLegAnchor(leg);
                this.ApplyQuirkLegPose(leg);
                DEN_Hunter_Arm.ApplyPoleNormal(leg.bones, this.GetKneePoleNormal(leg), this.kneePoleWeight);
                leg.arm.Solve(leg.bones, leg.targetPosition, (leg == this.tappingLeg || this.IsHoldingLeg(leg)) ? 8 : 5, 1f);
                this.UpdateLegRenderers(leg);
                this.UpdateLegContactShadows(leg);
            }
            this.UpdateHeldItemPose();
            bool flag4 = this.heldHandItem != null;
            if (flag4)
            {
                this.heldHandItem.UpdateVisual();
            }
            this.UpdateContactShadowFades();
            bool flag5 = (this.behaviorState == Unseen.BehaviorState.Inspecting || (this.behaviorState == Unseen.BehaviorState.Attacking && this.inspectedPlayer != null)) && Time.time >= this.nextTapTime;
            if (flag5)
            {
                this.TapNearbySurface();
            }
        }

        private void CreateShadow()
        {
            GameObject gameObject = new GameObject("UnseenBody");
            gameObject.layer = 31;
            gameObject.transform.SetParent(base.transform, false);
            gameObject.transform.localPosition = Vector3.zero;
            gameObject.transform.localScale = Vector3.one * 1.25f;
            this.shadowRenderer = gameObject.AddComponent<SpriteRenderer>();
            this.shadowRenderer.sprite = this.GetFallbackShadowFrame(0);
            this.shadowRenderer.color = Color.black;
            this.shadowRenderer.sharedMaterial = Unseen.GetOverlayMaterial();
            this.shadowRenderer.sortingOrder = 32759;
            this.unseenRenderers.Add(this.shadowRenderer);
            this.CreateEye();
        }

        private void CreateEye()
        {
            Sprite sprite = Unseen.GetUnseenEyeSprite();
            bool flag = sprite == null;
            if (!flag)
            {
                GameObject gameObject = new GameObject("UnseenEye");
                gameObject.transform.SetParent(base.transform, true);
                gameObject.layer = 30;
                this.eyeRenderer = gameObject.AddComponent<SpriteRenderer>();
                this.eyeRenderer.sprite = sprite;
                this.eyeRenderer.sharedMaterial = Unseen.GetEyeMaterial();
                this.eyeRenderer.color = this.eyeColor;
                this.eyeRenderer.sortingOrder = 32767;
            }
        }

        private void UpdateShadowFadeNoise()
        {
            int num = (Unseen.fallbackShadowFrames == null) ? Mathf.Max(1, this.shadowGenerationFrameCount) : Unseen.fallbackShadowFrames.Length;
            int num2 = Mathf.FloorToInt(Time.time * Mathf.Max(0.1f, this.shadowFadeNoiseFrameRate) + this.personalityPhase * 3f) % num;
            bool flag = num2 == this.shadowFadeNoiseFrame;
            if (!flag)
            {
                this.shadowFadeNoiseFrame = num2;
                this.shadowRenderer.sprite = this.GetFallbackShadowFrame(num2);
            }
        }

        private void UpdateEye()
        {
            bool flag = this.eyeRenderer == null;
            if (flag)
            {
                bool flag2 = Time.time >= this.nextEyeLookupTime;
                if (flag2)
                {
                    this.nextEyeLookupTime = Time.time + 1f;
                    this.CreateEye();
                }
            }
            else
            {
                Vector3 forward = this.GetEyeDirection();
                bool flag3 = forward.sqrMagnitude < 0.0001f;
                if (flag3)
                {
                    forward = base.transform.forward;
                }
                this.eyeDirection = ((this.activeQuirk == Unseen.QuirkType.EyeSpin) ? forward.normalized : Vector3.Slerp((this.eyeDirection.sqrMagnitude > 0.001f) ? this.eyeDirection : forward.normalized, forward.normalized, 1f - Mathf.Exp(-8f * Time.deltaTime)));
                this.eyeRenderer.transform.position = base.transform.position + this.eyeDirection * this.eyeOrbitDistance + Vector3.down * this.crouchBlend * Mathf.Max(0f, this.friendlyCrouchBodyDrop);
                this.eyeRenderer.transform.rotation = Quaternion.LookRotation(this.eyeDirection, Vector3.up);
            }
        }

        private Vector3 GetEyeDirection()
        {
            ENT_Player player = ENT_Player.GetPlayer();
            bool flag = this.activeQuirk == Unseen.QuirkType.EyeSpin;
            Vector3 result;
            if (flag)
            {
                result = Quaternion.AngleAxis(Time.time * this.quirkEyeSpinSpeed, base.transform.forward) * base.transform.up;
            }
            else
            {
                bool flag2 = this.activeQuirk == Unseen.QuirkType.GroundBounce && this.body != null && this.body.velocity.sqrMagnitude > 0.01f;
                if (flag2)
                {
                    result = this.body.velocity;
                }
                else
                {
                    bool flag3 = this.activeQuirk == Unseen.QuirkType.GroundBounce;
                    if (flag3)
                    {
                        result = Vector3.up;
                    }
                    else
                    {
                        bool flag4 = !this.helpful && this.behaviorState == Unseen.BehaviorState.Attacking && player != null;
                        if (flag4)
                        {
                            result = player.transform.position - base.transform.position;
                        }
                        else
                        {
                            bool flag5 = this.carriedRigidbody != null;
                            if (flag5)
                            {
                                result = this.carriedRigidbody.position - base.transform.position;
                            }
                            else
                            {
                                bool flag6 = this.manipulatedRigidbody != null;
                                if (flag6)
                                {
                                    result = this.manipulatedRigidbody.position - base.transform.position;
                                }
                                else
                                {
                                    bool flag7 = this.tappingLeg != null;
                                    if (flag7)
                                    {
                                        result = this.repeatedTapPoint - base.transform.position;
                                    }
                                    else
                                    {
                                        bool flag8 = this.inspectedItem != null;
                                        if (flag8)
                                        {
                                            result = this.inspectedItem.transform.position - base.transform.position;
                                        }
                                        else
                                        {
                                            bool flag9 = this.inspectedEntity != null;
                                            if (flag9)
                                            {
                                                result = this.inspectedEntity.transform.position - base.transform.position;
                                            }
                                            else
                                            {
                                                bool flag10 = this.inspectedButton != null;
                                                if (flag10)
                                                {
                                                    result = this.inspectedButton.transform.position - base.transform.position;
                                                }
                                                else
                                                {
                                                    bool flag11 = this.helpful && player != null && (Time.time < this.socialUntil || this.carriedRigidbody != null);
                                                    if (flag11)
                                                    {
                                                        result = player.transform.position + Vector3.up - base.transform.position;
                                                    }
                                                    else
                                                    {
                                                        bool flag12 = this.body != null && this.body.velocity.sqrMagnitude > 1f;
                                                        if (flag12)
                                                        {
                                                            result = this.body.velocity;
                                                        }
                                                        else
                                                        {
                                                            bool flag13 = Time.time >= this.nextGlanceTime;
                                                            if (flag13)
                                                            {
                                                                this.nextGlanceTime = Time.time + UnityEngine.Random.Range(1.2f, 3.5f) / this.curiosity;
                                                                this.glanceDirection = (this.curvedMovementDirection + UnityEngine.Random.onUnitSphere * 0.65f).normalized;
                                                            }
                                                            result = this.glanceDirection;
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return result;
        }

        public static Sprite GetUnseenEyeSprite()
        {
            bool flag = Unseen.unseenEyeSprite != null;
            Sprite result;
            if (flag)
            {
                result = Unseen.unseenEyeSprite;
            }
            else
            {
                foreach (AssetBundle assetBundle in Unseen.GetCachedBundles())
                {
                    Sprite x = assetBundle.LoadAsset<Sprite>("UnseenEye");
                    bool flag2 = x == null;
                    if (flag2)
                    {
                        foreach (Sprite sprite in assetBundle.LoadAllAssets<Sprite>())
                        {
                            bool flag3 = sprite != null && string.Equals(sprite.name, "UnseenEye", StringComparison.OrdinalIgnoreCase);
                            if (flag3)
                            {
                                x = sprite;
                                break;
                            }
                        }
                    }
                    bool flag4 = x != null;
                    if (flag4)
                    {
                        Unseen.unseenEyeSprite = x;
                        return Unseen.unseenEyeSprite;
                    }
                    Texture2D texture2D = assetBundle.LoadAsset<Texture2D>("UnseenEye");
                    bool flag5 = texture2D == null;
                    if (flag5)
                    {
                        foreach (Texture2D texture2D2 in assetBundle.LoadAllAssets<Texture2D>())
                        {
                            bool flag6 = texture2D2 != null && texture2D2.name.ContainsInsensitive("UnseenEye");
                            if (flag6)
                            {
                                texture2D = texture2D2;
                                break;
                            }
                        }
                    }
                    bool flag7 = texture2D == null;
                    if (!flag7)
                    {
                        Unseen.unseenEyeSprite = Sprite.Create(texture2D, new Rect(0f, 0f, (float)texture2D.width, (float)texture2D.height), new Vector2(0.5f, 0.5f), (float)Mathf.Max(texture2D.width, texture2D.height));
                        return Unseen.unseenEyeSprite;
                    }
                }
                result = null;
            }
            return result;
        }

        public static IEnumerable<AssetBundle> GetCachedBundles()
        {
            foreach (AssetBundle bundle in CustomLevelLoader._cachedBundles.Values)
            {
                if (bundle != null)
                {
                    yield return bundle;
                }
            }
            foreach (AssetBundle bundle in Resources.FindObjectsOfTypeAll<AssetBundle>())
            {
                if (bundle != null)
                {
                    yield return bundle;
                }
            }
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type loaderType = assembly.GetType("Severon.GamemodeFramework.AssetBundleLevelLoader", throwOnError: false);
                FieldInfo field = loaderType?.GetField("bundles", BindingFlags.NonPublic | BindingFlags.Static);
                IDictionary dictionary = field?.GetValue(null) as IDictionary;
                if (dictionary == null)
                {
                    continue;
                }
                foreach (object value in dictionary.Values)
                {
                    if (value is AssetBundle bundle && bundle != null)
                    {
                        yield return bundle;
                    }
                }
            }
        }

        public static int SharedCimimShadowFrameCount => 10;
        public static Transform CreateSharedLegSegment(Transform parent, string objectName)
        {
            if (cimimSegmentMeshes == null)
            {
                cimimSegmentMeshes = new SegmentMeshDefinition[10];
                for (int i = 0; i < cimimSegmentMeshes.Length; i++)
                    cimimSegmentMeshes[i] = CreateSegmentMeshDefinition(UnityEngine.Random.Range(7, 13), 0.025f, 0.05f);
            }
            SegmentMeshDefinition definition = cimimSegmentMeshes[UnityEngine.Random.Range(0, cimimSegmentMeshes.Length)];
            GameObject segmentObject = new GameObject(objectName);
            segmentObject.layer = 0;
            segmentObject.transform.SetParent(parent, true);
            MeshFilter filter = segmentObject.AddComponent<MeshFilter>();
            filter.sharedMesh = definition.mesh;
            MeshRenderer renderer = segmentObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = GetOverlayMaterial();
            renderer.sortingOrder = RenderOrder;
            cimimSegmentLengths[segmentObject.transform] = definition.longestDistance;
            return segmentObject.transform;
        }
        public static void UpdateSharedLegSegment(Transform segment, Vector3 start, Vector3 end)
        {
            if (segment == null || !cimimSegmentLengths.TryGetValue(segment, out float longestDistance)) return;
            Vector3 direction = end - start;
            float length = direction.magnitude;
            if (length < 0.0001f) return;
            segment.position = start + direction * 0.5f;
            segment.rotation = Quaternion.FromToRotation(Vector3.right, direction / length);
            segment.localScale = new Vector3(length / Mathf.Max(0.0001f, longestDistance), 1f, 1f);
        }
        public static void ReleaseSharedLegSegment(Transform segment)
        {
            if (segment != null) cimimSegmentLengths.Remove(segment);
        }
        public static Sprite GetSharedCimimShadowFrame(int frame)
        {
            if (cimimShadowFrames == null)
            {
                const int size = 64;
                const int frameCount = 10;
                float center = (size - 1) * 0.5f;
                float radius = 18f;
                cimimShadowFrames = new Sprite[frameCount];
                for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
                {
                    float[] shape = new float[size * size];
                    for (int y = 0; y < size; y++)
                        for (int x = 0; x < size; x++)
                        {
                            float dx = x - center;
                            float dy = y - center;
                            if (dx * dx + dy * dy <= radius * radius) shape[y * size + x] = 1f;
                        }
                    int tendrilCount = 14;
                    for (int tendril = 0; tendril < tendrilCount; tendril++)
                    {
                        float angle = GetShadowNoise(tendril * 107 + 5, frameIndex * 13 + 17, frameIndex) * Mathf.PI * 2f;
                        float start = 13f + GetShadowNoise(tendril * 43 + 3, 29, frameIndex) * 4f;
                        float length = 5f + GetShadowNoise(tendril * 71 + 9, 41, frameIndex) * 10f;
                        float curve = Mathf.Lerp(-0.32f, 0.32f, GetShadowNoise(tendril * 89 + 11, 53, frameIndex));
                        int steps = Mathf.Max(1, Mathf.CeilToInt(length * 1.5f));
                        for (int step = 0; step <= steps; step++)
                        {
                            float progress = step / (float)steps;
                            float currentAngle = angle + Mathf.Sin(progress * Mathf.PI) * curve;
                            float currentRadius = start + length * progress;
                            int x = Mathf.RoundToInt(center + Mathf.Cos(currentAngle) * currentRadius);
                            int y = Mathf.RoundToInt(center + Mathf.Sin(currentAngle) * currentRadius);
                            PaintShadowWisp(shape, size, x, y, Mathf.Lerp(2.4f, 1.4f, progress));
                        }
                    }
                    Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                    texture.filterMode = FilterMode.Point;
                    texture.wrapMode = TextureWrapMode.Clamp;
                    for (int y = 0; y < size; y++)
                        for (int x = 0; x < size; x++)
                        {
                            float alpha = shape[y * size + x];
                            if (alpha > 0f) alpha = Mathf.Lerp(0.94f, 1f, GetShadowNoise(x + 31, y + 47, frameIndex));
                            texture.SetPixel(x, y, new Color(0f, 0f, 0f, alpha));
                        }
                    texture.Apply(false, true);
                    cimimShadowFrames[frameIndex] = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
                }
            }
            return cimimShadowFrames[Mathf.Abs(frame) % cimimShadowFrames.Length];
        }
        public static void UpdateSharedEyePose(SpriteRenderer renderer, Transform origin, Vector3 direction, float orbitDistance, float size, float verticalOffset = 0f)
        {
            if (renderer == null || origin == null) return;
            if (direction.sqrMagnitude < 0.0001f) direction = origin.forward;
            direction.Normalize();
            renderer.transform.position = origin.position + direction * orbitDistance + Vector3.down * verticalOffset;
            renderer.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            renderer.transform.localScale = Vector3.one * size;
        }
        private void CreateLeg(int index, bool growing)
        {
            int num = UnityEngine.Random.Range(1, 6);
            float d = 14f / (float)(num + 1);
            int num2 = index % 8;
            float f = (float)num2 * 3.1415927f * 2f / 8f + UnityEngine.Random.Range(-0.12f, 0.12f);
            float num3 = Mathf.Lerp(-0.78f, 0.78f, ((float)num2 + 0.5f) / 8f);
            float num4 = Mathf.Sqrt(1f - num3 * num3);
            Vector3 normalized = new Vector3(Mathf.Cos(f) * num4, num3, Mathf.Sin(f) * num4).normalized;
            Vector3 normalized2 = Vector3.ProjectOnPlane(normalized, Vector3.up).normalized;
            GameObject gameObject = new GameObject("UnseenLegRoot_" + index.ToString());
            Transform transform = gameObject.transform;
            transform.SetParent(base.transform, false);
            transform.localPosition = normalized2 * 0.22f;
            transform.rotation = Quaternion.FromToRotation(Vector3.right, normalized);
            List<Transform> list = new List<Transform>
            {
                transform
            };
            Transform parent = transform;
            for (int i = 0; i <= num; i++)
            {
                GameObject gameObject2 = new GameObject((i == num) ? ("UnseenLegTipEnd_" + index.ToString()) : ("UnseenLegBone_" + index.ToString() + "_" + i.ToString()));
                Transform transform2 = gameObject2.transform;
                transform2.SetParent(parent, false);
                transform2.localPosition = Vector3.right * d;
                list.Add(transform2);
                parent = transform2;
            }
            DEN_Hunter_Arm den_Hunter_Arm = gameObject.AddComponent<DEN_Hunter_Arm>();
            den_Hunter_Arm.enabled = false;
            den_Hunter_Arm.armSegments = list;
            den_Hunter_Arm.endTransform = list[list.Count - 1];
            Unseen.Leg leg = new Unseen.Leg
            {
                arm = den_Hunter_Arm,
                bones = list,
                root = transform,
                baseRootLocalPosition = transform.localPosition,
                searchDirection = normalized,
                targetPosition = transform.position + normalized * 12.599999f,
                desiredTargetPosition = transform.position + normalized * 12.599999f,
                segmentRenderers = new List<Transform>(),
                kneeRenderers = new List<Transform>(),
                maximumReach = 10.5f,
                probeOffset = UnityEngine.Random.Range(0f, 6.2831855f),
                appearanceScale = (growing ? 0f : 1f),
                appearanceDirection = (growing ? 1f : 0f)
            };
            leg.restDirection = normalized;
            leg.nextProbeTime = Time.time + UnityEngine.Random.Range(0f, 0.08f);
            den_Hunter_Arm.SetTargetPosition(leg.targetPosition, Vector3.up);
            for (int j = 0; j < num; j++)
            {
                leg.segmentRenderers.Add(this.CreatePrimitive("UnseenLegSegment_" + index.ToString() + "_" + j.ToString(), PrimitiveType.Cube).transform);
            }
            for (int k = 1; k <= num; k++)
            {
                leg.kneeRenderers.Add(this.CreatePrimitive("UnseenLegKnee_" + index.ToString() + "_" + k.ToString(), PrimitiveType.Sphere).transform);
            }
            leg.tip = this.CreateTip("UnseenLegTip_" + index.ToString()).transform;
            this.legs.Add(leg);
        }

        private void UpdateCrouchLegOrigins()
        {
            float d = this.crouchBlend * Mathf.Max(0f, this.friendlyCrouchBodyDrop);
            foreach (Unseen.Leg leg in this.legs)
            {
                bool flag = leg != null && leg.root != null;
                if (flag)
                {
                    leg.root.localPosition = leg.baseRootLocalPosition + Vector3.down * d;
                }
            }
        }

        private void UpdateLegAnchor(Unseen.Leg leg)
        {
            bool flag = leg.appearanceDirection < 0f;
            if (!flag)
            {
                bool flag2 = this.IsHoldingLeg(leg);
                if (flag2)
                {
                    Vector3 a = (this.curvedMovementDirection.sqrMagnitude > 0.001f) ? this.curvedMovementDirection : base.transform.forward;
                    leg.anchorTransform = null;
                    leg.recoveryLocked = false;
                    leg.desiredTargetPosition = base.transform.position + a * 2.2f + Vector3.up * 0.7f + leg.restDirection * 0.8f;
                    leg.targetPosition = Vector3.Lerp(leg.targetPosition, leg.desiredTargetPosition, 1f - Mathf.Exp(-7f * Time.deltaTime));
                    leg.isMovingToAnchor = false;
                }
                else
                {
                    bool flag3 = Time.time < leg.tapUntil;
                    if (flag3)
                    {
                        leg.desiredTargetPosition = leg.tapTargetPosition;
                        this.MoveLegTarget(leg);
                    }
                    else
                    {
                        bool flag4 = leg.anchorTransform != null;
                        if (flag4)
                        {
                            leg.desiredTargetPosition = leg.anchorTransform.TransformPoint(leg.anchorLocalPosition);
                            bool recoveryLocked = leg.recoveryLocked;
                            if (recoveryLocked)
                            {
                                this.MoveLegTarget(leg);
                                return;
                            }
                            float num = Vector3.Distance(leg.bones[0].position, leg.desiredTargetPosition);
                            bool flag5 = num > leg.maximumReach || this.ShouldRepositionLeg(leg);
                            bool flag6 = !flag5;
                            if (flag6)
                            {
                                this.MoveLegTarget(leg);
                                return;
                            }
                            bool flag7 = !this.CanScheduleWalkStep(leg, this.GetWalkDirection(), num > leg.maximumReach * 1.15f);
                            if (flag7)
                            {
                                this.MoveLegTarget(leg);
                                return;
                            }
                            bool flag8 = !this.TryReleaseLegAnchor(leg);
                            if (flag8)
                            {
                                this.MoveLegTarget(leg);
                                return;
                            }
                            this.ScheduleWalkStep(leg);
                        }
                        leg.recoveryLocked = false;
                        Vector3 position = leg.bones[0].position;
                        Vector3 desiredLegDirection = this.GetDesiredLegDirection(leg);
                        float num2 = Time.time * 2.5f + leg.probeOffset;
                        Vector3 vector = Vector3.Cross(desiredLegDirection, Vector3.up);
                        bool flag9 = vector.sqrMagnitude < 0.0001f;
                        if (flag9)
                        {
                            vector = Vector3.right;
                        }
                        vector.Normalize();
                        Vector3 normalized = Vector3.Cross(desiredLegDirection, vector).normalized;
                        Vector3 vector2 = Quaternion.AngleAxis(Mathf.Sin(num2) * 70f, vector) * desiredLegDirection;
                        vector2 = Quaternion.AngleAxis(Mathf.Cos(num2 * 1.7f) * 70f, normalized) * vector2;
                        vector2.Normalize();
                        leg.desiredTargetPosition = position + vector2 * leg.maximumReach;
                        bool flag10 = Time.time < leg.nextProbeTime;
                        if (flag10)
                        {
                            this.MoveLegTarget(leg);
                        }
                        else
                        {
                            leg.nextProbeTime = Time.time + UnityEngine.Random.Range(0.06f, 0.12f);
                            for (int i = 0; i < 3; i++)
                            {
                                Vector3 direction = (i == 0) ? vector2 : (vector2 + UnityEngine.Random.insideUnitSphere * 0.8f).normalized;
                                bool flag11 = this.TryAnchorLeg(leg, position, direction);
                                if (flag11)
                                {
                                    break;
                                }
                            }
                            this.MoveLegTarget(leg);
                        }
                    }
                }
            }
        }

        private bool TryAnchorLeg(Unseen.Leg leg, Vector3 origin, Vector3 direction)
        {
            int num = Physics.SphereCastNonAlloc(origin, 0.06f, direction, this.surfaceHits, leg.maximumReach, -5, QueryTriggerInteraction.Ignore);
            int num2 = -1;
            float num3 = float.PositiveInfinity;
            for (int i = 0; i < num; i++)
            {
                RaycastHit raycastHit = this.surfaceHits[i];
                bool flag = raycastHit.collider == null || raycastHit.collider.transform.IsChildOf(base.transform) || (raycastHit.rigidbody == this.heldBody && this.heldBody != null) || raycastHit.collider.GetComponentInParent<ENT_Player>() != null;
                if (!flag)
                {
                    bool flag2 = raycastHit.distance >= num3 || this.IsAnchorTooClose(leg, raycastHit.point);
                    if (!flag2)
                    {
                        num2 = i;
                        num3 = raycastHit.distance;
                    }
                }
            }
            bool flag3 = num2 < 0;
            bool result;
            if (flag3)
            {
                result = false;
            }
            else
            {
                RaycastHit raycastHit2 = this.surfaceHits[num2];
                leg.anchorTransform = raycastHit2.collider.transform;
                leg.anchorLocalPosition = leg.anchorTransform.InverseTransformPoint(raycastHit2.point);
                leg.surfaceNormal = ((raycastHit2.normal.sqrMagnitude > 0.001f) ? raycastHit2.normal : Vector3.up);
                this.BeginLegStep(leg, raycastHit2.point, this.walkStepDuration * UnityEngine.Random.Range(0.85f, 1.15f) / this.pace);
                result = true;
            }
            return result;
        }

        private bool IsAnchorTooClose(Unseen.Leg selectedLeg, Vector3 position)
        {
            foreach (Unseen.Leg leg in this.legs)
            {
                bool flag = leg == selectedLeg || leg.anchorTransform == null;
                if (!flag)
                {
                    Vector3 b = leg.anchorTransform.TransformPoint(leg.anchorLocalPosition);
                    bool flag2 = Vector3.Distance(position, b) < this.legPersonalSpace;
                    if (flag2)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private void MoveLegTarget(Unseen.Leg leg)
        {
            bool flag = leg.anchorTransform == null && Time.time >= leg.tapUntil;
            if (flag)
            {
                leg.targetPosition = Vector3.Lerp(leg.targetPosition, leg.desiredTargetPosition, 1f - Mathf.Exp(-5f * this.pace * Time.deltaTime));
                leg.isMovingToAnchor = true;
            }
            else
            {
                bool flag2 = leg.stepDuration <= 0f;
                if (flag2)
                {
                    this.BeginLegStep(leg, leg.desiredTargetPosition, this.walkStepDuration / this.pace);
                }
                leg.stepTargetPosition = leg.desiredTargetPosition;
                leg.stepProgress = Mathf.Min(leg.stepProgress + Time.deltaTime, leg.stepDuration);
                float num = (leg.stepDuration <= 0f) ? 1f : (leg.stepProgress / leg.stepDuration);
                float t = num * num * (3f - 2f * num);
                float d = (leg.anchorTransform != null && !leg.recoveryLocked) ? (Mathf.Sin(num * 3.1415927f) * Mathf.Min(0.65f, Vector3.Distance(leg.stepStartPosition, leg.stepTargetPosition) * 0.15f)) : 0f;
                leg.targetPosition = Vector3.Lerp(leg.stepStartPosition, leg.stepTargetPosition, t) + leg.surfaceNormal * d;
                leg.isMovingToAnchor = (num < 1f);
            }
        }

        private void BeginLegStep(Unseen.Leg leg, Vector3 targetPosition, float duration)
        {
            leg.desiredTargetPosition = targetPosition;
            leg.stepStartPosition = leg.targetPosition;
            leg.stepTargetPosition = targetPosition;
            leg.stepProgress = 0f;
            leg.stepDuration = duration;
        }

        private Vector3 GetDesiredLegDirection(Unseen.Leg leg)
        {
            bool flag = this.body == null || this.body.velocity.sqrMagnitude < 0.25f;
            Vector3 result;
            if (flag)
            {
                result = leg.searchDirection;
            }
            else
            {
                float num = (float)this.GetSettledAnchorCount() * 2f;
                bool flag2 = num <= 0f || this.body.velocity.magnitude < num * 0.55f;
                if (flag2)
                {
                    result = leg.searchDirection;
                }
                else
                {
                    result = Vector3.Slerp(leg.searchDirection, this.body.velocity.normalized, 0.7f).normalized;
                }
            }
            return result;
        }

        private bool ShouldRepositionLeg(Unseen.Leg leg)
        {
            bool flag = Time.time < leg.nextAnchorRepositionTime || !this.CanRepositionLeg(leg);
            bool result;
            if (flag)
            {
                result = false;
            }
            else
            {
                Vector3 lhs = leg.desiredTargetPosition - leg.bones[0].position;
                bool flag2 = lhs.sqrMagnitude < 0.0001f;
                if (flag2)
                {
                    result = false;
                }
                else
                {
                    lhs.Normalize();
                    float num = (float)this.GetSettledAnchorCount() * 2f;
                    bool flag3 = this.body != null && num > 0f && this.body.velocity.magnitude >= num * 0.65f;
                    if (flag3)
                    {
                        result = (Vector3.Dot(lhs, this.body.velocity.normalized) < 0.2f);
                    }
                    else
                    {
                        result = (Vector3.Dot(lhs, leg.searchDirection.normalized) < 0.35f);
                    }
                }
            }
            return result;
        }

        private bool CanRepositionLeg(Unseen.Leg selectedLeg)
        {
            bool flag = this.IsHoldingLeg(selectedLeg);
            bool result;
            if (flag)
            {
                result = false;
            }
            else
            {
                int num = 0;
                foreach (Unseen.Leg leg in this.legs)
                {
                    bool flag2 = leg != selectedLeg && leg.anchorTransform != null && leg.isMovingToAnchor;
                    if (flag2)
                    {
                        num++;
                    }
                }
                result = (num < Mathf.Max(1, this.maximumConcurrentWalkingLegs) && (selectedLeg.anchorTransform == null || this.GetSettledAnchorCount() > 2));
            }
            return result;
        }

        private Vector3 GetWalkDirection()
        {
            bool flag = this.curvedMovementDirection.sqrMagnitude > 0.0001f;
            Vector3 result;
            if (flag)
            {
                result = this.curvedMovementDirection.normalized;
            }
            else
            {
                bool flag2 = this.body != null && this.body.velocity.sqrMagnitude > 0.0001f;
                if (flag2)
                {
                    result = this.body.velocity.normalized;
                }
                else
                {
                    result = base.transform.forward;
                }
            }
            return result;
        }

        private bool CanScheduleWalkStep(Unseen.Leg selectedLeg, Vector3 movementDirection, bool urgent)
        {
            bool flag = !this.CanRepositionLeg(selectedLeg);
            bool result;
            if (flag)
            {
                result = false;
            }
            else
            {
                int settledAnchorCount = this.GetSettledAnchorCount();
                bool flag2 = urgent || settledAnchorCount <= 3;
                if (flag2)
                {
                    result = true;
                }
                else
                {
                    bool flag3 = Time.time < this.nextWalkStepTime;
                    if (flag3)
                    {
                        result = false;
                    }
                    else
                    {
                        int num = 0;
                        int num2 = 0;
                        foreach (Unseen.Leg leg in this.legs)
                        {
                            bool flag4 = leg.anchorTransform == null || leg.appearanceDirection < 0f || leg.isMovingToAnchor;
                            if (!flag4)
                            {
                                Vector3 lhs = leg.anchorTransform.TransformPoint(leg.anchorLocalPosition) - base.transform.position;
                                bool flag5 = Vector3.Dot(lhs, movementDirection) >= 0f;
                                if (flag5)
                                {
                                    num++;
                                }
                                else
                                {
                                    num2++;
                                }
                            }
                        }
                        Vector3 lhs2 = selectedLeg.anchorTransform.TransformPoint(selectedLeg.anchorLocalPosition) - base.transform.position;
                        bool flag6 = Vector3.Dot(lhs2, movementDirection) < 0f;
                        result = (flag6 && num2 > num + Mathf.Max(0, this.walkSupportBalanceTolerance));
                    }
                }
            }
            return result;
        }

        private void ScheduleWalkStep(Unseen.Leg leg)
        {
            leg.nextAnchorRepositionTime = Time.time + Mathf.Max(0.1f, this.walkStepInterval) / Mathf.Max(0.1f, this.pace);
            this.nextWalkStepTime = leg.nextAnchorRepositionTime;
        }

        private int GetSettledAnchorCount()
        {
            int num = 0;
            foreach (Unseen.Leg leg in this.legs)
            {
                bool flag = leg.anchorTransform != null && leg.appearanceScale >= 0.99f && (leg.recoveryLocked || !leg.isMovingToAnchor);
                if (flag)
                {
                    num++;
                }
            }
            return num;
        }

        private void UpdateDestinationMovement(int anchoredLegs)
        {
            ENT_Player player = ENT_Player.GetPlayer();
            bool flag = !this.helpful && player != null && (Vector3.Distance(base.transform.position, player.transform.position) > this.hostileCatchUpDistance || player.transform.position.y - base.transform.position.y > 10f);
            bool flag2 = flag;
            if (flag2)
            {
                this.ClearTap();
                this.inspectedItem = null;
                this.inspectedEntity = null;
                this.inspectedPlayer = null;
                this.destination = player.transform.position + Vector3.up * 4f;
                this.destinationTimeout = Time.time + 2f;
            }
            bool flag3 = Time.time >= this.destinationTimeout;
            if (flag3)
            {
                this.ChooseNextDestination();
            }
            else
            {
                bool flag4 = this.inspectedPlayer != null;
                if (flag4)
                {
                    this.destination = this.inspectedPlayer.transform.position + this.playerInspectionOffset;
                }
                bool flag5 = !flag && this.inspectedPlayer == null && this.inspectedItem == null && this.inspectedEntity == null && this.inspectedButton == null;
                if (flag5)
                {
                    Item_Object x = this.ChooseItemToInspect();
                    bool flag6 = x != null;
                    if (flag6)
                    {
                        this.inspectedItem = x;
                        this.BeginInspection();
                        return;
                    }
                    GameEntity x2 = this.ChooseEntityToInspect();
                    bool flag7 = x2 != null;
                    if (flag7)
                    {
                        this.inspectedEntity = x2;
                        this.BeginInspection();
                        return;
                    }
                    CL_Button x3 = this.ChooseButtonToInspect();
                    bool flag8 = x3 != null;
                    if (flag8)
                    {
                        this.inspectedButton = x3;
                        this.BeginInspection();
                        return;
                    }
                }
                bool flag9 = this.inspectedButton != null;
                if (flag9)
                {
                    this.destination = this.inspectedButton.transform.position;
                }
                Vector3 a = this.destination - base.transform.position;
                float magnitude = a.magnitude;
                bool flag10 = !flag && magnitude <= 5f;
                if (flag10)
                {
                    this.BeginInspection();
                }
                else
                {
                    bool flag11 = !flag && Time.time >= this.nextProgressCheck;
                    if (flag11)
                    {
                        bool flag12 = magnitude >= this.lastDestinationDistance - 3f;
                        if (flag12)
                        {
                            this.ChooseNextDestination();
                            return;
                        }
                        this.lastDestinationDistance = magnitude;
                        this.nextProgressCheck = Time.time + 4f;
                    }
                    bool flag13 = anchoredLegs <= 0;
                    if (!flag13)
                    {
                        float num = this.helpful ? ((float)anchoredLegs * 2f) : (Mathf.Min(flag ? this.hostileCatchUpSpeed : this.hostilePatrolSpeed, (float)anchoredLegs * 5.5f) * this.pace);
                        Vector3 direction = a / magnitude;
                        Vector3 defaultMovementDirection = this.GetDefaultMovementDirection(direction);
                        float num2 = Mathf.Max(0f, Vector3.Dot(this.body.velocity, defaultMovementDirection));
                        float num3 = num2 * num2 / 72f;
                        float num4 = (magnitude <= num3 + 1.25f) ? Mathf.Sqrt(Mathf.Max(0f, 72f * Mathf.Max(0f, magnitude - 1.25f))) : num;
                        num4 = Mathf.Min(num4, num);
                        this.MoveWithDefaultSteering(defaultMovementDirection, num4, true);
                    }
                }
            }
        }

        private void MoveWithDefaultSteering(Vector3 direction, float desiredSpeed)
        {
            this.MoveWithDefaultSteering(direction, desiredSpeed, false);
        }

        private void MoveWithDefaultSteering(Vector3 direction, float desiredSpeed, bool directionIsSmoothed)
        {
            bool flag = this.body == null || direction.sqrMagnitude < 0.0001f || desiredSpeed <= 0f;
            if (!flag)
            {
                desiredSpeed *= this.GetUnseenHandItemSpeedMultiplier();
                Vector3 vector = directionIsSmoothed ? direction.normalized : this.GetDefaultMovementDirection(direction);
                this.PrepareLegsForMovement(vector);
                this.ApplyDefaultMovement(vector, desiredSpeed);
            }
        }

        private Vector3 GetDefaultMovementDirection(Vector3 direction)
        {
            direction.Normalize();
            bool flag = this.curvedMovementDirection.sqrMagnitude < 0.0001f;
            if (flag)
            {
                this.curvedMovementDirection = direction;
            }
            this.curvedMovementDirection = Vector3.Slerp(this.curvedMovementDirection, direction, Time.fixedDeltaTime * this.movementTurnRate).normalized;
            return this.curvedMovementDirection;
        }

        private void ApplyDefaultMovement(Vector3 direction, float desiredSpeed)
        {
            Vector3 a = direction * desiredSpeed;
            this.body.AddForce(Vector3.ClampMagnitude((a - this.body.velocity) * 10f, this.helpful ? 32f : 48f), ForceMode.Acceleration);
            bool flag = this.body.velocity.magnitude > desiredSpeed + 2f;
            if (flag)
            {
                this.body.velocity = this.body.velocity.normalized * (desiredSpeed + 2f);
            }
        }

        private void PrepareLegsForMovement(Vector3 movementDirection)
        {
            bool flag = Time.time < this.nextWalkStepTime;
            if (!flag)
            {
                Unseen.Leg leg = null;
                float num = float.NegativeInfinity;
                foreach (Unseen.Leg leg2 in this.legs)
                {
                    bool flag2 = leg2.appearanceDirection < 0f || leg2.recoveryLocked || this.IsHoldingLeg(leg2) || Time.time < leg2.tapUntil;
                    if (!flag2)
                    {
                        leg2.searchDirection = Vector3.Slerp(leg2.restDirection, movementDirection, 0.35f).normalized;
                        bool flag3 = leg2.anchorTransform == null || leg2.isMovingToAnchor;
                        if (!flag3)
                        {
                            Vector3 a = leg2.anchorTransform.TransformPoint(leg2.anchorLocalPosition) - leg2.bones[0].position;
                            float magnitude = a.magnitude;
                            bool flag4 = magnitude < 0.0001f;
                            if (!flag4)
                            {
                                float num2 = Vector3.Dot(a / magnitude, movementDirection);
                                bool flag5 = magnitude < leg2.maximumReach * this.movementAnchorReleaseDistanceFraction && num2 >= this.movementAnchorReleaseBehindDot;
                                if (!flag5)
                                {
                                    float num3 = magnitude / leg2.maximumReach - num2;
                                    bool flag6 = num3 <= num;
                                    if (!flag6)
                                    {
                                        num = num3;
                                        leg = leg2;
                                    }
                                }
                            }
                        }
                    }
                }
                bool flag7 = leg == null || !this.CanScheduleWalkStep(leg, movementDirection, false);
                if (!flag7)
                {
                    bool flag8 = !this.TryReleaseLegAnchor(leg);
                    if (!flag8)
                    {
                        this.ScheduleWalkStep(leg);
                    }
                }
            }
        }

        private bool TryReleaseLegAnchor(Unseen.Leg leg)
        {
            bool flag = leg == null || leg.anchorTransform == null;
            bool result;
            if (flag)
            {
                result = true;
            }
            else
            {
                bool flag2 = this.GetGroundedAnchorCountExcluding(leg) == 0 && this.IsAtOrBeyondSurfaceReach(leg.maximumReach / 2f);
                if (flag2)
                {
                    this.BeginRecovery();
                    result = false;
                }
                else
                {
                    leg.anchorTransform = null;
                    leg.recoveryLocked = false;
                    result = true;
                }
            }
            return result;
        }

        private int GetGroundedAnchorCountExcluding(Unseen.Leg excludedLeg)
        {
            int num = 0;
            foreach (Unseen.Leg leg in this.legs)
            {
                bool flag = leg != excludedLeg && leg.anchorTransform != null && leg.appearanceDirection >= 0f;
                if (flag)
                {
                    num++;
                }
            }
            return num;
        }

        private bool IsAtOrBeyondSurfaceReach(float maximumReach)
        {
            int num = Physics.OverlapSphereNonAlloc(base.transform.position, maximumReach, this.recoverySurfaceColliders, -5, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < num; i++)
            {
                Collider collider = this.recoverySurfaceColliders[i];
                this.recoverySurfaceColliders[i] = null;
                bool flag = collider == null || collider.transform.IsChildOf(base.transform) || collider.GetComponentInParent<ENT_Player>() != null;
                if (!flag)
                {
                    bool flag2 = Vector3.Distance(base.transform.position, collider.ClosestPoint(base.transform.position)) < maximumReach;
                    if (flag2)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private void UpdateFriendlyCrouchMirror()
        {
            bool flag = !this.helpful;
            if (flag)
            {
                this.crouchBlend = Mathf.MoveTowards(this.crouchBlend, 0f, Mathf.Max(0.1f, this.friendlyCrouchTransitionSpeed) * Time.fixedDeltaTime);
            }
            else
            {
                ENT_Player player = ENT_Player.GetPlayer();
                bool flag2 = player == null;
                if (!flag2)
                {
                    bool flag3 = player.IsCrouching();
                    bool flag4 = !this.playerCrouchKnown || flag3 != this.playerCrouching;
                    if (flag4)
                    {
                        this.playerCrouchKnown = true;
                        this.playerCrouching = flag3;
                        this.scheduledCrouchState = flag3;
                        float b = Mathf.Min(this.friendlyCrouchDelayMinimum, this.friendlyCrouchDelayMaximum);
                        float b2 = Mathf.Max(this.friendlyCrouchDelayMinimum, this.friendlyCrouchDelayMaximum);
                        this.scheduledCrouchTime = Time.time + UnityEngine.Random.Range(Mathf.Max(0f, b), Mathf.Max(0f, b2));
                    }
                    bool flag5 = Time.time >= this.scheduledCrouchTime;
                    if (flag5)
                    {
                        this.crouchBlend = Mathf.MoveTowards(this.crouchBlend, this.scheduledCrouchState ? 1f : 0f, Mathf.Max(0.1f, this.friendlyCrouchTransitionSpeed) * Time.fixedDeltaTime);
                    }
                }
            }
        }

        private void AssignQuirks()
        {
            this.quirks.Clear();
            List<Unseen.QuirkType> list = new List<Unseen.QuirkType>
            {
                Unseen.QuirkType.PropTapping,
                Unseen.QuirkType.ItemPile,
                Unseen.QuirkType.EyeSpin,
                Unseen.QuirkType.CrabRave,
                Unseen.QuirkType.GroundBounce,
                Unseen.QuirkType.PlayerStalk,
                Unseen.QuirkType.EatObject,
                Unseen.QuirkType.ButtonSequence
            };
            int num = Mathf.Clamp(this.quirkMinimumCount, 1, list.Count);
            int num2 = Mathf.Clamp(this.quirkMaximumCount, num, list.Count);
            int num3 = UnityEngine.Random.Range(num, num2 + 1);
            float num4 = Mathf.Max(1f, Mathf.Min(this.quirkFrequencyMinimum, this.quirkFrequencyMaximum));
            float maxInclusive = Mathf.Max(num4, Mathf.Max(this.quirkFrequencyMinimum, this.quirkFrequencyMaximum));
            for (int i = 0; i < num3; i++)
            {
                int index = UnityEngine.Random.Range(0, list.Count);
                Unseen.QuirkSchedule quirkSchedule = new Unseen.QuirkSchedule();
                quirkSchedule.type = list[index];
                quirkSchedule.frequency = UnityEngine.Random.Range(num4, maxInclusive);
                quirkSchedule.nextTime = Time.time + UnityEngine.Random.Range(quirkSchedule.frequency * 0.35f, quirkSchedule.frequency);
                this.quirks.Add(quirkSchedule);
                list.RemoveAt(index);
            }
        }

        private bool UpdateQuirkBehavior(int anchoredLegs)
        {
            bool flag = this.behaviorState == Unseen.BehaviorState.Recovering;
            bool result;
            if (flag)
            {
                bool flag2 = this.activeQuirk > Unseen.QuirkType.None;
                if (flag2)
                {
                    this.EndQuirk();
                }
                result = false;
            }
            else
            {
                bool flag3 = this.activeQuirk > Unseen.QuirkType.None;
                if (flag3)
                {
                    bool flag4 = !this.HasQuirkSupport(anchoredLegs);
                    if (flag4)
                    {
                        this.EndQuirk();
                        this.BeginRecovery();
                        this.body.velocity = Vector3.zero;
                        result = false;
                    }
                    else
                    {
                        bool flag5 = Time.time < this.quirkUntil && this.UpdateActiveQuirk(anchoredLegs);
                        bool flag6 = !flag5;
                        if (flag6)
                        {
                            this.EndQuirk();
                            result = false;
                        }
                        else
                        {
                            result = true;
                        }
                    }
                }
                else
                {
                    bool flag7 = this.carriedRigidbody != null || this.manipulatedRigidbody != null || this.heldBody != null || this.consumedRigidbody != null;
                    if (flag7)
                    {
                        result = false;
                    }
                    else
                    {
                        foreach (Unseen.QuirkSchedule quirkSchedule in this.quirks)
                        {
                            bool flag8 = Time.time < quirkSchedule.nextTime;
                            if (!flag8)
                            {
                                bool flag9 = UnityEngine.Random.value > Mathf.Clamp01(this.quirkActivationChance);
                                if (flag9)
                                {
                                    quirkSchedule.nextTime = Time.time + quirkSchedule.frequency * UnityEngine.Random.Range(0.55f, 1.1f);
                                }
                                else
                                {
                                    bool flag10 = this.BeginQuirk(quirkSchedule.type);
                                    if (flag10)
                                    {
                                        quirkSchedule.nextTime = Time.time + quirkSchedule.frequency * UnityEngine.Random.Range(0.75f, 1.3f);
                                        return true;
                                    }
                                    quirkSchedule.nextTime = Time.time + Mathf.Min(8f, quirkSchedule.frequency * 0.25f);
                                }
                            }
                        }
                        result = false;
                    }
                }
            }
            return result;
        }

        private bool BeginQuirk(Unseen.QuirkType type)
        {
            this.ClearTap();
            this.inspectedPlayer = null;
            this.inspectedItem = null;
            this.inspectedEntity = null;
            this.inspectedButton = null;
            this.quirkLegs.Clear();
            this.quirkLegTapCounts.Clear();
            this.quirkButtons.Clear();
            this.quirkProp = null;
            this.quirkItem = null;
            this.quirkStartPosition = base.transform.position;
            this.quirkGroundPoint = this.GetQuirkGroundPoint(base.transform.position);
            this.quirkLegIndex = 0;
            this.quirkButtonIndex = 0;
            this.nextQuirkActionTime = Time.time;
            this.activeQuirk = type;
            bool flag = type == Unseen.QuirkType.PropTapping;
            bool result;
            if (flag)
            {
                this.quirkProp = this.ChooseQuirkProp();
                bool flag2 = this.quirkProp == null;
                if (flag2)
                {
                    result = this.CancelQuirkStart();
                }
                else
                {
                    List<Unseen.Leg> list = new List<Unseen.Leg>();
                    foreach (Unseen.Leg leg in this.legs)
                    {
                        bool flag3 = this.IsLegUsable(leg) && leg.appearanceDirection >= 0f && !leg.recoveryLocked && !this.IsHoldingLeg(leg);
                        if (flag3)
                        {
                            list.Add(leg);
                        }
                    }
                    int num = Mathf.Min(list.Count, UnityEngine.Random.Range(2, 5));
                    for (int i = 0; i < num; i++)
                    {
                        int index = UnityEngine.Random.Range(0, list.Count);
                        this.quirkLegs.Add(list[index]);
                        this.quirkLegTapCounts.Add(0);
                        list.RemoveAt(index);
                    }
                    bool flag4 = this.quirkLegs.Count == 0;
                    if (flag4)
                    {
                        result = this.CancelQuirkStart();
                    }
                    else
                    {
                        this.quirkUntil = Time.time + Mathf.Max(8f, (float)(this.quirkPropTapsPerLimb * this.quirkLegs.Count) * Mathf.Max(0.1f, this.quirkPropTapInterval) * 2f);
                        result = true;
                    }
                }
            }
            else
            {
                bool flag5 = type == Unseen.QuirkType.ItemPile;
                if (flag5)
                {
                    this.quirkUntil = Time.time + UnityEngine.Random.Range(5f, 9f);
                    result = true;
                }
                else
                {
                    bool flag6 = type == Unseen.QuirkType.EyeSpin;
                    if (flag6)
                    {
                        this.quirkUntil = Time.time + UnityEngine.Random.Range(2f, 5f);
                        result = true;
                    }
                    else
                    {
                        bool flag7 = type == Unseen.QuirkType.CrabRave;
                        if (flag7)
                        {
                            this.AddQuirkPoseLegs(2);
                            bool flag8 = this.quirkLegs.Count < 2;
                            if (flag8)
                            {
                                result = this.CancelQuirkStart();
                            }
                            else
                            {
                                this.quirkUntil = Time.time + UnityEngine.Random.Range(4f, 8f);
                                result = true;
                            }
                        }
                        else
                        {
                            bool flag9 = type == Unseen.QuirkType.GroundBounce;
                            if (flag9)
                            {
                                this.quirkUntil = Time.time + UnityEngine.Random.Range(3f, 6f);
                                this.quirkBounceStartTime = Time.time;
                                bool flag10 = !this.PrepareGroundBounceLegs();
                                result = (!flag10 || this.CancelQuirkStart());
                            }
                            else
                            {
                                bool flag11 = type == Unseen.QuirkType.PlayerStalk;
                                if (flag11)
                                {
                                    bool flag12 = ENT_Player.GetPlayer() == null;
                                    if (flag12)
                                    {
                                        result = this.CancelQuirkStart();
                                    }
                                    else
                                    {
                                        this.quirkUntil = Time.time + UnityEngine.Random.Range(4f, 7f);
                                        result = true;
                                    }
                                }
                                else
                                {
                                    bool flag13 = type == Unseen.QuirkType.EatObject;
                                    if (flag13)
                                    {
                                        this.quirkItem = this.ChooseQuirkItem();
                                        bool flag14 = this.quirkItem == null;
                                        if (flag14)
                                        {
                                            this.quirkProp = this.ChooseQuirkProp();
                                        }
                                        bool flag15 = this.quirkItem == null && this.quirkProp == null;
                                        if (flag15)
                                        {
                                            result = this.CancelQuirkStart();
                                        }
                                        else
                                        {
                                            this.quirkUntil = Time.time + 10f;
                                            result = true;
                                        }
                                    }
                                    else
                                    {
                                        bool flag16 = type == Unseen.QuirkType.ButtonSequence;
                                        if (flag16)
                                        {
                                            bool flag17 = this.level == null;
                                            if (flag17)
                                            {
                                                result = this.CancelQuirkStart();
                                            }
                                            else
                                            {
                                                foreach (CL_Button cl_Button in this.level.GetComponentsInChildren<CL_Button>(true))
                                                {
                                                    bool flag18 = cl_Button != null && cl_Button.gameObject.activeInHierarchy && cl_Button.CanInteract() && Vector3.Distance(base.transform.position, cl_Button.transform.position) <= this.quirkSearchRange;
                                                    if (flag18)
                                                    {
                                                        this.quirkButtons.Add(cl_Button);
                                                    }
                                                }
                                                this.ShuffleQuirkButtons();
                                                bool flag19 = this.quirkButtons.Count == 0;
                                                if (flag19)
                                                {
                                                    result = this.CancelQuirkStart();
                                                }
                                                else
                                                {
                                                    this.quirkButtonRounds = UnityEngine.Random.Range(2, 5);
                                                    this.quirkUntil = Time.time + (float)(this.quirkButtons.Count * this.quirkButtonRounds) * Mathf.Max(0.15f, this.quirkButtonPressInterval) * 6f;
                                                    result = true;
                                                }
                                            }
                                        }
                                        else
                                        {
                                            result = this.CancelQuirkStart();
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return result;
        }

        private bool CancelQuirkStart()
        {
            this.activeQuirk = Unseen.QuirkType.None;
            return false;
        }

        private bool UpdateActiveQuirk(int anchoredLegs)
        {
            bool flag = this.activeQuirk == Unseen.QuirkType.PropTapping;
            bool result;
            if (flag)
            {
                result = this.UpdatePropTapQuirk(anchoredLegs);
            }
            else
            {
                bool flag2 = this.activeQuirk == Unseen.QuirkType.ItemPile;
                if (flag2)
                {
                    result = this.UpdateItemPileQuirk();
                }
                else
                {
                    bool flag3 = this.activeQuirk == Unseen.QuirkType.EyeSpin;
                    if (flag3)
                    {
                        result = true;
                    }
                    else
                    {
                        bool flag4 = this.activeQuirk == Unseen.QuirkType.CrabRave;
                        if (flag4)
                        {
                            result = this.UpdateCrabRaveQuirk(anchoredLegs);
                        }
                        else
                        {
                            bool flag5 = this.activeQuirk == Unseen.QuirkType.GroundBounce;
                            if (flag5)
                            {
                                result = this.UpdateGroundBounceQuirk();
                            }
                            else
                            {
                                bool flag6 = this.activeQuirk == Unseen.QuirkType.PlayerStalk;
                                if (flag6)
                                {
                                    result = this.UpdatePlayerStalkQuirk(anchoredLegs);
                                }
                                else
                                {
                                    bool flag7 = this.activeQuirk == Unseen.QuirkType.EatObject;
                                    if (flag7)
                                    {
                                        result = this.UpdateEatObjectQuirk(anchoredLegs);
                                    }
                                    else
                                    {
                                        bool flag8 = this.activeQuirk == Unseen.QuirkType.ButtonSequence;
                                        result = (flag8 && this.UpdateButtonSequenceQuirk(anchoredLegs));
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return result;
        }

        private bool UpdatePropTapQuirk(int anchoredLegs)
        {
            bool flag = this.quirkProp == null || this.quirkProp.dead;
            bool result;
            if (flag)
            {
                result = false;
            }
            else
            {
                bool flag2 = Vector3.Distance(base.transform.position, this.quirkProp.transform.position) > 4f;
                if (flag2)
                {
                    this.MoveQuirkTowards(this.quirkProp.transform.position, anchoredLegs, 7f);
                    result = true;
                }
                else
                {
                    bool flag3 = Time.time < this.nextQuirkActionTime;
                    if (flag3)
                    {
                        result = true;
                    }
                    else
                    {
                        bool flag4 = this.quirkLegs.Count == 0;
                        if (flag4)
                        {
                            result = false;
                        }
                        else
                        {
                            int num = this.quirkLegIndex;
                            this.quirkLegIndex = num + 1;
                            int num2 = num % this.quirkLegs.Count;
                            Unseen.Leg leg = this.quirkLegs[num2];
                            bool flag5 = !this.IsLegUsable(leg);
                            if (flag5)
                            {
                                result = true;
                            }
                            else
                            {
                                Vector3 quirkTapPoint = this.GetQuirkTapPoint(this.quirkProp.transform, leg.bones[0].position);
                                bool flag6 = Vector3.Distance(leg.bones[0].position, quirkTapPoint) > leg.maximumReach;
                                if (flag6)
                                {
                                    result = true;
                                }
                                else
                                {
                                    bool flag7 = !this.TryReleaseLegAnchor(leg);
                                    if (flag7)
                                    {
                                        result = false;
                                    }
                                    else
                                    {
                                        leg.tapUntil = Time.time + Mathf.Max(0.1f, this.quirkPropTapInterval) * 1.5f;
                                        leg.tapTargetPosition = quirkTapPoint;
                                        this.BeginLegStep(leg, quirkTapPoint, Mathf.Max(0.05f, this.tapStepDuration));
                                        List<int> list = this.quirkLegTapCounts;
                                        num = num2;
                                        int num3 = list[num];
                                        list[num] = num3 + 1;
                                        this.quirkProp.Damage(Damageable.DamageInfo.CreateDamageInfo(this.quirkPropTapDamage, "unseen"));
                                        Rigidbody componentInChildren = this.quirkProp.GetComponentInChildren<Rigidbody>();
                                        bool flag8 = componentInChildren != null && !componentInChildren.isKinematic;
                                        if (flag8)
                                        {
                                            Vector3 a = (this.quirkProp.transform.position - base.transform.position).normalized + Vector3.up * 0.3f;
                                            componentInChildren.AddForceAtPosition(a * this.quirkPropTapForce, quirkTapPoint, ForceMode.Impulse);
                                        }
                                        this.nextQuirkActionTime = Time.time + Mathf.Max(0.08f, this.quirkPropTapInterval);
                                        foreach (int num4 in this.quirkLegTapCounts)
                                        {
                                            bool flag9 = num4 < Mathf.Max(1, this.quirkPropTapsPerLimb);
                                            if (flag9)
                                            {
                                                return true;
                                            }
                                        }
                                        result = false;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return result;
        }

        private bool UpdateItemPileQuirk()
        {
            bool flag = this.level == null;
            bool result;
            if (flag)
            {
                result = false;
            }
            else
            {
                foreach (Item_Object item_Object in this.level.GetComponentsInChildren<Item_Object>(true))
                {
                    bool flag2 = item_Object == null || item_Object.itemData == null || item_Object.itemData.inBag || !item_Object.gameObject.activeInHierarchy || item_Object.transform.IsChildOf(base.transform) || Vector3.Distance(base.transform.position, item_Object.transform.position) > this.quirkSearchRange;
                    if (!flag2)
                    {
                        Rigidbody componentInChildren = item_Object.GetComponentInChildren<Rigidbody>(true);
                        bool flag3 = componentInChildren == null || componentInChildren.isKinematic;
                        if (!flag3)
                        {
                            Vector3 a = this.quirkGroundPoint - componentInChildren.position;
                            componentInChildren.AddForce(Vector3.ClampMagnitude(a * this.quirkItemPileForce, this.quirkItemPileForce * 2f), ForceMode.Acceleration);
                        }
                    }
                }
                result = true;
            }
            return result;
        }

        private bool UpdateCrabRaveQuirk(int anchoredLegs)
        {
            float num = (Time.time + this.personalityPhase) * this.quirkDanceSpeed;
            Vector3 b = base.transform.right * Mathf.Sin(num) * 3f + base.transform.forward * (Mathf.Cos(num * 2f) - 0.5f) * 1.2f;
            this.MoveQuirkTowards(this.quirkStartPosition + b, anchoredLegs, 5f);
            return true;
        }

        private bool UpdateGroundBounceQuirk()
        {
            float num = 10.5f;
            float f = (Time.time - this.quirkBounceStartTime) * 2.4f;
            float num2 = Mathf.Lerp(0.8f, 0.8f + num, (Mathf.Sin(f) + 1f) * 0.5f);
            float num3 = this.quirkGroundPoint.y + num2 - base.transform.position.y;
            this.body.AddForce(Vector3.up * (num3 * this.quirkBounceForce - this.body.velocity.y * this.quirkBounceForce * 0.35f), ForceMode.Acceleration);
            Vector3 a = Vector3.ProjectOnPlane(this.body.velocity, Vector3.up);
            this.body.AddForce(-a * this.quirkBounceForce, ForceMode.Acceleration);
            return true;
        }

        private bool UpdatePlayerStalkQuirk(int anchoredLegs)
        {
            ENT_Player player = ENT_Player.GetPlayer();
            bool flag = player == null;
            bool result;
            if (flag)
            {
                result = false;
            }
            else
            {
                Transform transform = (player.cam != null) ? player.cam.transform : player.transform;
                Vector3 a = player.transform.position - Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized * 4f;
                Vector3 vector = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized * Mathf.Sin(Time.time * 1.7f + this.personalityPhase) * 2f;
                bool flag2 = this.IsVisibleToPlayer();
                if (flag2)
                {
                    vector *= -1f;
                }
                this.MoveQuirkTowards(a + vector, anchoredLegs, 8f);
                result = true;
            }
            return result;
        }

        private bool UpdateEatObjectQuirk(int anchoredLegs)
        {
            Transform transform = (this.quirkItem != null) ? this.quirkItem.transform : ((this.quirkProp != null) ? this.quirkProp.transform : null);
            bool flag = transform == null;
            bool result;
            if (flag)
            {
                result = false;
            }
            else
            {
                bool flag2 = Vector3.Distance(base.transform.position, transform.position) > 2.5f;
                if (flag2)
                {
                    this.MoveQuirkTowards(transform.position, anchoredLegs, 7f);
                    result = true;
                }
                else
                {
                    bool flag3 = this.quirkItem != null;
                    if (flag3)
                    {
                        UnityEngine.Object.Destroy(this.quirkItem.gameObject);
                    }
                    else
                    {
                        bool flag4 = this.quirkProp != null;
                        if (flag4)
                        {
                            UnityEngine.Object.Destroy(this.quirkProp.gameObject);
                        }
                    }
                    result = false;
                }
            }
            return result;
        }

        private bool UpdateButtonSequenceQuirk(int anchoredLegs)
        {
            int count = this.quirkButtons.Count;
            while (count-- > 0 && (this.quirkButtons[this.quirkButtonIndex] == null || !this.quirkButtons[this.quirkButtonIndex].gameObject.activeInHierarchy))
            {
                this.AdvanceQuirkButton();
            }
            bool flag = this.quirkButtons.Count == 0 || this.quirkButtonRounds <= 0;
            bool result;
            if (flag)
            {
                result = false;
            }
            else
            {
                CL_Button cl_Button = this.quirkButtons[this.quirkButtonIndex];
                bool flag2 = cl_Button == null || !cl_Button.gameObject.activeInHierarchy;
                if (flag2)
                {
                    result = false;
                }
                else
                {
                    bool flag3 = Vector3.Distance(base.transform.position, cl_Button.transform.position) > 4f;
                    if (flag3)
                    {
                        this.MoveQuirkTowards(cl_Button.transform.position, anchoredLegs, 7f);
                        result = true;
                    }
                    else
                    {
                        bool flag4 = Time.time < this.nextQuirkActionTime;
                        if (flag4)
                        {
                            result = true;
                        }
                        else
                        {
                            bool flag5 = cl_Button.CanInteract();
                            if (flag5)
                            {
                                cl_Button.Interact();
                            }
                            this.nextQuirkActionTime = Time.time + Mathf.Max(0.1f, this.quirkButtonPressInterval);
                            this.AdvanceQuirkButton();
                            result = (this.quirkButtons.Count > 0 && this.quirkButtonRounds > 0);
                        }
                    }
                }
            }
            return result;
        }

        private void AdvanceQuirkButton()
        {
            bool flag = this.quirkButtons.Count == 0;
            if (!flag)
            {
                this.quirkButtonIndex++;
                bool flag2 = this.quirkButtonIndex < this.quirkButtons.Count;
                if (!flag2)
                {
                    this.quirkButtonIndex = 0;
                    this.quirkButtonRounds--;
                    this.ShuffleQuirkButtons();
                }
            }
        }

        private void ShuffleQuirkButtons()
        {
            for (int i = this.quirkButtons.Count - 1; i > 0; i--)
            {
                int index = UnityEngine.Random.Range(0, i + 1);
                CL_Button value = this.quirkButtons[i];
                this.quirkButtons[i] = this.quirkButtons[index];
                this.quirkButtons[index] = value;
            }
        }

        private void AddQuirkPoseLegs(int count)
        {
            List<Unseen.Leg> list = new List<Unseen.Leg>();
            foreach (Unseen.Leg leg in this.legs)
            {
                bool flag = this.IsLegUsable(leg) && leg.appearanceDirection >= 0f && !leg.recoveryLocked && !this.IsHoldingLeg(leg);
                if (flag)
                {
                    list.Add(leg);
                }
            }
            while (this.quirkLegs.Count < count && list.Count > 0)
            {
                int index = UnityEngine.Random.Range(0, list.Count);
                this.quirkLegs.Add(list[index]);
                list.RemoveAt(index);
            }
        }

        private void ApplyQuirkLegPose(Unseen.Leg leg)
        {
            bool flag = !this.IsLegUsable(leg);
            if (!flag)
            {
                bool flag2 = this.activeQuirk == Unseen.QuirkType.CrabRave;
                if (flag2)
                {
                    int num = this.quirkLegs.IndexOf(leg);
                    bool flag3 = num < 0;
                    if (!flag3)
                    {
                        float f = (Time.time + this.personalityPhase) * this.quirkDanceSpeed;
                        Vector3 a = base.transform.right * ((num == 0) ? -1f : 1f);
                        Vector3 vector = base.transform.position + Vector3.up * (1.5f + Mathf.Abs(Mathf.Sin(f)) * 1.2f) + a * 2f + base.transform.forward * Mathf.Cos(f) * 0.8f;
                        leg.targetPosition = vector;
                        leg.desiredTargetPosition = vector;
                        leg.isMovingToAnchor = false;
                    }
                }
            }
        }

        private bool PrepareGroundBounceLegs()
        {
            Vector3 vector;
            bool flag = !this.TryGetQuirkGroundPoint(base.transform.position, out vector);
            bool result;
            if (flag)
            {
                result = false;
            }
            else
            {
                this.quirkGroundPoint = vector;
                this.quirkGroundTargets.Clear();
                this.quirkLegs.Clear();
                int num = Mathf.Max(1, this.legs.Count);
                for (int i = 0; i < this.legs.Count; i++)
                {
                    Unseen.Leg leg = this.legs[i];
                    bool flag2 = !this.IsLegUsable(leg) || leg.appearanceDirection < 0f || this.IsHoldingLeg(leg);
                    if (!flag2)
                    {
                        float f = (float)i * 3.1415927f * 2f / (float)num;
                        Vector3 vector2 = this.quirkGroundPoint + new Vector3(Mathf.Cos(f), 0f, Mathf.Sin(f)) * 0.16f;
                        bool flag3 = Vector3.Distance(leg.bones[0].position, vector2) > leg.maximumReach;
                        if (!flag3)
                        {
                            leg.anchorTransform = null;
                            leg.recoveryLocked = false;
                            leg.tapTargetPosition = vector2;
                            leg.tapUntil = this.quirkUntil;
                            this.BeginLegStep(leg, vector2, Mathf.Max(0.1f, this.quirkGroundStepDuration));
                            this.quirkLegs.Add(leg);
                            this.quirkGroundTargets.Add(vector2);
                        }
                    }
                }
                result = (this.quirkLegs.Count > 0);
            }
            return result;
        }

        private bool HasQuirkSupport(int anchoredLegs)
        {
            bool flag = this.activeQuirk == Unseen.QuirkType.GroundBounce;
            bool result;
            if (flag)
            {
                result = (this.quirkLegs.Count > 0 && this.quirkGroundTargets.Count == this.quirkLegs.Count);
            }
            else
            {
                bool flag2 = anchoredLegs > 0;
                Vector3 b;
                result = (flag2 || (this.TryGetQuirkGroundPoint(base.transform.position, out b) && Vector3.Distance(base.transform.position, b) <= Mathf.Max(14f, this.quirkSupportDistance)));
            }
            return result;
        }

        private void MoveQuirkTowards(Vector3 target, int anchoredLegs, float speed)
        {
            Vector3 direction = target - base.transform.position;
            bool flag = direction.sqrMagnitude < 0.04f || anchoredLegs <= 0;
            if (!flag)
            {
                this.MoveWithDefaultSteering(direction, Mathf.Min(speed * this.pace, Mathf.Sqrt(direction.magnitude * 16f)));
            }
        }

        private CL_Prop ChooseQuirkProp()
        {
            bool flag = this.level == null;
            CL_Prop result;
            if (flag)
            {
                result = null;
            }
            else
            {
                CL_Prop cl_Prop = null;
                int num = 0;
                foreach (CL_Prop cl_Prop2 in this.level.GetComponentsInChildren<CL_Prop>(true))
                {
                    bool flag2 = cl_Prop2 == null || cl_Prop2.dead || !cl_Prop2.gameObject.activeInHierarchy || cl_Prop2.transform.IsChildOf(base.transform) || Vector3.Distance(base.transform.position, cl_Prop2.transform.position) > this.quirkSearchRange;
                    if (!flag2)
                    {
                        num++;
                        bool flag3 = UnityEngine.Random.Range(0, num) == 0;
                        if (flag3)
                        {
                            cl_Prop = cl_Prop2;
                        }
                    }
                }
                result = cl_Prop;
            }
            return result;
        }

        private Item_Object ChooseQuirkItem()
        {
            bool flag = this.level == null;
            Item_Object result;
            if (flag)
            {
                result = null;
            }
            else
            {
                Item_Object item_Object = null;
                int num = 0;
                foreach (Item_Object item_Object2 in this.level.GetComponentsInChildren<Item_Object>(true))
                {
                    bool flag2 = item_Object2 == null || item_Object2.itemData == null || item_Object2.itemData.inBag || !item_Object2.gameObject.activeInHierarchy || item_Object2.transform.IsChildOf(base.transform) || Vector3.Distance(base.transform.position, item_Object2.transform.position) > this.quirkSearchRange;
                    if (!flag2)
                    {
                        num++;
                        bool flag3 = UnityEngine.Random.Range(0, num) == 0;
                        if (flag3)
                        {
                            item_Object = item_Object2;
                        }
                    }
                }
                result = item_Object;
            }
            return result;
        }

        private Vector3 GetQuirkTapPoint(Transform target, Vector3 origin)
        {
            foreach (Collider collider in target.GetComponentsInChildren<Collider>(true))
            {
                bool flag = collider != null;
                if (flag)
                {
                    return collider.ClosestPoint(origin);
                }
            }
            return target.position;
        }

        private bool TryGetQuirkGroundPoint(Vector3 origin, out Vector3 groundPoint)
        {
            RaycastHit raycastHit;
            bool flag = Physics.Raycast(origin + Vector3.up * 3f, Vector3.down, out raycastHit, Mathf.Max(12f, this.quirkSupportDistance), -5, QueryTriggerInteraction.Ignore) && raycastHit.collider != null && !raycastHit.collider.transform.IsChildOf(base.transform) && raycastHit.collider.GetComponentInParent<ENT_Player>() == null;
            bool result;
            if (flag)
            {
                groundPoint = raycastHit.point + raycastHit.normal * 0.03f;
                result = true;
            }
            else
            {
                groundPoint = origin;
                result = false;
            }
            return result;
        }

        private Vector3 GetQuirkGroundPoint(Vector3 origin)
        {
            Vector3 vector;
            return this.TryGetQuirkGroundPoint(origin, out vector) ? vector : (origin + Vector3.down * 2f);
        }

        private void EndQuirk()
        {
            foreach (Unseen.Leg leg in this.quirkLegs)
            {
                bool flag = leg != null;
                if (flag)
                {
                    leg.tapUntil = 0f;
                }
            }
            this.activeQuirk = Unseen.QuirkType.None;
            this.quirkLegs.Clear();
            this.quirkLegTapCounts.Clear();
            this.quirkButtons.Clear();
            this.quirkGroundTargets.Clear();
            this.quirkProp = null;
            this.quirkItem = null;
            bool flag2 = this.behaviorState != Unseen.BehaviorState.Recovering;
            if (flag2)
            {
                this.ChooseNextDestination();
            }
        }

        private void ChooseNextDestination()
        {
            this.ClearTap();
            this.inspectedPlayer = ((!this.helpful && UnityEngine.Random.value < this.playerInspectionChance) ? ENT_Player.GetPlayer() : null);
            this.inspectedItem = null;
            this.inspectedEntity = null;
            this.inspectedButton = null;
            this.playerInspectionOffset = UnityEngine.Random.onUnitSphere * this.playerInspectionDistance;
            this.destination = ((this.inspectedPlayer != null) ? (this.inspectedPlayer.transform.position + this.playerInspectionOffset) : this.GetInspectionDestination());
            this.behaviorState = Unseen.BehaviorState.Moving;
            this.destinationTimeout = Time.time + UnityEngine.Random.Range(this.destinationDurationMinimum, this.destinationDurationMaximum);
            this.lastDestinationDistance = Vector3.Distance(base.transform.position, this.destination);
            this.nextProgressCheck = Time.time + 4f;
        }

        private Item_Object ChooseItemToInspect()
        {
            bool flag = this.level == null || Time.time < this.nextItemSearchTime;
            Item_Object result;
            if (flag)
            {
                result = null;
            }
            else
            {
                this.nextItemSearchTime = Time.time + UnityEngine.Random.Range(0.65f, 1.2f) / this.curiosity;
                Item_Object[] componentsInChildren = this.level.GetComponentsInChildren<Item_Object>(true);
                Item_Object item_Object = null;
                int num = 0;
                foreach (Item_Object item_Object2 in componentsInChildren)
                {
                    bool flag2 = item_Object2 == null || item_Object2.itemData == null || item_Object2.itemData.inBag || item_Object2.transform.IsChildOf(base.transform) || !item_Object2.gameObject.activeInHierarchy || Vector3.Distance(base.transform.position, item_Object2.transform.position) > this.inspectionItemRange;
                    if (!flag2)
                    {
                        num++;
                        bool flag3 = UnityEngine.Random.Range(0, num) == 0;
                        if (flag3)
                        {
                            item_Object = item_Object2;
                        }
                    }
                }
                result = item_Object;
            }
            return result;
        }

        private GameEntity ChooseEntityToInspect()
        {
            bool flag = Time.time < this.nextEntitySearchTime;
            GameEntity result;
            if (flag)
            {
                result = null;
            }
            else
            {
                this.nextEntitySearchTime = Time.time + UnityEngine.Random.Range(0.8f, 1.5f) / this.curiosity;
                Collider[] array = Physics.OverlapSphere(base.transform.position, this.inspectionItemRange, -5, QueryTriggerInteraction.Ignore);
                GameEntity gameEntity = null;
                int num = 0;
                foreach (Collider collider in array)
                {
                    bool flag2 = collider == null || collider.transform.IsChildOf(base.transform);
                    if (!flag2)
                    {
                        GameEntity componentInParent = collider.GetComponentInParent<GameEntity>();
                        bool flag3 = componentInParent == null || componentInParent is ENT_Player || (!(componentInParent is CL_Prop) && !(componentInParent is Denizen));
                        if (!flag3)
                        {
                            num++;
                            bool flag4 = UnityEngine.Random.Range(0, num) == 0;
                            if (flag4)
                            {
                                gameEntity = componentInParent;
                            }
                        }
                    }
                }
                bool flag5 = this.level != null;
                if (flag5)
                {
                    foreach (Denizen denizen in this.level.GetComponentsInChildren<Denizen>(true))
                    {
                        bool flag6 = denizen == null || denizen.dead || !denizen.gameObject.activeInHierarchy || Vector3.Distance(base.transform.position, denizen.transform.position) > this.inspectionItemRange;
                        if (!flag6)
                        {
                            num++;
                            bool flag7 = UnityEngine.Random.Range(0, num) == 0;
                            if (flag7)
                            {
                                gameEntity = denizen;
                            }
                        }
                    }
                }
                result = gameEntity;
            }
            return result;
        }

        private CL_Button ChooseButtonToInspect()
        {
            bool flag = this.level == null || Time.time < this.nextButtonSearchTime;
            CL_Button result;
            if (flag)
            {
                result = null;
            }
            else
            {
                this.nextButtonSearchTime = Time.time + UnityEngine.Random.Range(1.2f, 2.5f) / this.curiosity;
                CL_Button cl_Button = null;
                int num = 0;
                foreach (CL_Button cl_Button2 in this.level.GetComponentsInChildren<CL_Button>(true))
                {
                    bool flag2 = cl_Button2 == null || !cl_Button2.gameObject.activeInHierarchy || !cl_Button2.CanInteract() || Vector3.Distance(base.transform.position, cl_Button2.transform.position) > this.inspectionItemRange;
                    if (!flag2)
                    {
                        num++;
                        bool flag3 = UnityEngine.Random.Range(0, num) == 0;
                        if (flag3)
                        {
                            cl_Button = cl_Button2;
                        }
                    }
                }
                result = cl_Button;
            }
            return result;
        }

        private Vector3 GetRandomLevelPoint()
        {
            bool flag = this.level == null;
            Vector3 result;
            if (flag)
            {
                result = base.transform.position + UnityEngine.Random.insideUnitSphere * 10f;
            }
            else
            {
                Bounds localBounds = this.level.GetLocalBounds();
                Vector3 extents = localBounds.extents;
                Vector3 vector = base.transform.position;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 position = localBounds.center + new Vector3(UnityEngine.Random.Range(-extents.x, extents.x), UnityEngine.Random.Range(-extents.y * 0.15f, extents.y), UnityEngine.Random.Range(-extents.z, extents.z));
                    Vector3 vector2 = this.level.transform.TransformPoint(position);
                    vector2.y = Mathf.Lerp(vector2.y, Mathf.Max(vector2.y, base.transform.position.y), this.downwardDestinationAvoidance);
                    vector = vector2;
                    bool flag2 = Vector3.Distance(base.transform.position, vector2) >= this.minimumDestinationDistance;
                    if (flag2)
                    {
                        break;
                    }
                }
                result = vector;
            }
            return result;
        }

        private Vector3 GetInspectionDestination()
        {
            M_Level m_Level = (WorldLoader.instance != null && WorldLoader.instance.GetCurrentLevel() != null) ? WorldLoader.instance.GetCurrentLevel().GetLevel() : null;
            Transform transform = (m_Level != null) ? m_Level.GetLevelExit() : null;
            bool flag = transform != null && UnityEngine.Random.value < this.exitInspectionBias;
            Vector3 result;
            if (flag)
            {
                Vector3 vector = transform.position + UnityEngine.Random.insideUnitSphere * this.inspectionItemRange * 0.5f;
                vector.y = Mathf.Lerp(vector.y, Mathf.Max(vector.y, base.transform.position.y), this.downwardDestinationAvoidance);
                result = vector;
            }
            else
            {
                result = this.GetRandomLevelPoint();
            }
            return result;
        }

        private void BeginInspection()
        {
            this.behaviorState = Unseen.BehaviorState.Inspecting;
            this.inspectUntil = Time.time + (this.helpful ? UnityEngine.Random.Range(3f, 7f) : UnityEngine.Random.Range(0.8f, 2.4f));
            this.nextTapTime = Time.time;
            this.idleCenter = base.transform.position;
        }

        private void TapNearbySurface()
        {
            bool flag = !this.helpful && this.inspectedPlayer != null && !this.IsPlayerLookingAtUnseen();
            if (flag)
            {
                this.ClearTap();
                this.inspectedPlayer = null;
            }
            else
            {
                bool flag2 = this.heldBody != null;
                if (!flag2)
                {
                    bool flag3 = this.tappingLeg != null && (!this.IsLegUsable(this.tappingLeg) || Time.time >= this.tappingLeg.tapUntil);
                    if (flag3)
                    {
                        this.ClearTap();
                    }
                    bool flag4 = this.tappingLeg != null;
                    if (flag4)
                    {
                        bool flag5 = this.tappedRigidbody != null;
                        if (flag5)
                        {
                            this.repeatedTapPoint = this.tappedRigidbody.transform.TransformPoint(this.tapLocalPoint);
                            bool flag6 = this.tapExtended;
                            if (flag6)
                            {
                                this.tappingLeg.tapTargetPosition = this.repeatedTapPoint;
                            }
                        }
                        bool flag7 = Time.time < this.nextTapTime;
                        if (!flag7)
                        {
                            bool flag8 = this.tapExtended && Vector3.Distance(this.GetLegTipPosition(this.tappingLeg), this.repeatedTapPoint) > this.tapContactDistance;
                            if (flag8)
                            {
                                this.nextTapTime = Time.time + Time.deltaTime;
                            }
                            else
                            {
                                bool flag9 = this.tapExtended;
                                if (flag9)
                                {
                                    this.remainingTaps--;
                                    this.ApplyTapEffect();
                                    bool flag10 = this.heldBody != null || this.remainingTaps <= 0;
                                    if (flag10)
                                    {
                                        this.ClearTap();
                                        this.nextTapTime = Time.time + UnityEngine.Random.Range(0.5f, 1.3f);
                                        return;
                                    }
                                }
                                this.tapExtended = !this.tapExtended;
                                Vector3 vector = this.repeatedTapPoint - this.tappingLeg.bones[0].position;
                                Vector3 vector2 = this.tapExtended ? this.repeatedTapPoint : (this.tappingLeg.bones[0].position + vector.normalized * Mathf.Min(this.tappingLeg.maximumReach * this.tapRetractFraction, vector.magnitude * this.tapRetractFraction));
                                this.tappingLeg.tapTargetPosition = vector2;
                                this.BeginLegStep(this.tappingLeg, vector2, this.tapStepDuration);
                                this.nextTapTime = Time.time + this.tapInterval * UnityEngine.Random.Range(0.85f, 1.2f) / this.pace;
                            }
                        }
                    }
                    else
                    {
                        bool flag11 = this.legs.Count == 0;
                        if (!flag11)
                        {
                            for (int i = 0; i < this.legs.Count; i++)
                            {
                                List<Unseen.Leg> list = this.legs;
                                int num = this.tapLegIndex;
                                this.tapLegIndex = num + 1;
                                Unseen.Leg leg = list[num % this.legs.Count];
                                bool flag12 = leg.recoveryLocked || leg.isMovingToAnchor || this.IsHoldingLeg(leg) || leg.appearanceDirection < 0f;
                                if (!flag12)
                                {
                                    Vector3 position = leg.bones[0].position;
                                    Vector3 vector3 = this.GetInspectionTapPoint();
                                    Collider collider = null;
                                    Collider[] array = Physics.OverlapSphere(position, leg.maximumReach, -5, QueryTriggerInteraction.Ignore);
                                    bool flag13 = this.inspectedItem != null;
                                    if (flag13)
                                    {
                                        Collider[] componentsInChildren = this.inspectedItem.GetComponentsInChildren<Collider>(true);
                                        foreach (Collider collider2 in componentsInChildren)
                                        {
                                            bool flag14 = collider2 == null;
                                            if (!flag14)
                                            {
                                                Vector3 vector4 = collider2.ClosestPoint(position);
                                                bool flag15 = Vector3.Distance(position, vector4) > leg.maximumReach;
                                                if (!flag15)
                                                {
                                                    vector3 = vector4;
                                                    collider = collider2;
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                    bool flag16 = this.inspectedEntity != null;
                                    if (flag16)
                                    {
                                        Collider[] componentsInChildren2 = this.inspectedEntity.GetComponentsInChildren<Collider>(true);
                                        foreach (Collider collider3 in componentsInChildren2)
                                        {
                                            bool flag17 = collider3 == null;
                                            if (!flag17)
                                            {
                                                Vector3 vector5 = collider3.ClosestPoint(position);
                                                bool flag18 = Vector3.Distance(position, vector5) > leg.maximumReach;
                                                if (!flag18)
                                                {
                                                    vector3 = vector5;
                                                    collider = collider3;
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                    bool flag19 = this.inspectedPlayer == null && this.inspectedItem == null && this.inspectedEntity == null;
                                    if (flag19)
                                    {
                                        foreach (Collider collider4 in array)
                                        {
                                            bool flag20 = collider4 == null || collider4.transform.IsChildOf(base.transform);
                                            if (!flag20)
                                            {
                                                Vector3 vector6 = collider4.ClosestPoint(position);
                                                bool flag21 = Vector3.Distance(position, vector6) > leg.maximumReach;
                                                if (!flag21)
                                                {
                                                    vector3 = vector6;
                                                    collider = collider4;
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                    bool flag22 = Vector3.Distance(position, vector3) > leg.maximumReach;
                                    if (!flag22)
                                    {
                                        bool flag23 = !this.TryReleaseLegAnchor(leg);
                                        if (flag23)
                                        {
                                            break;
                                        }
                                        leg.tapTargetPosition = vector3;
                                        leg.tapUntil = Time.time + this.tapInterval * (float)(this.maximumTaps * 2 + 2);
                                        this.tappingLeg = leg;
                                        this.repeatedTapPoint = vector3;
                                        this.remainingTaps = UnityEngine.Random.Range(this.minimumTaps, this.maximumTaps + 1);
                                        this.tapExtended = false;
                                        this.tappedRigidbody = ((collider != null) ? collider.attachedRigidbody : null);
                                        bool flag24 = this.tappedRigidbody != null;
                                        if (flag24)
                                        {
                                            this.tapLocalPoint = this.tappedRigidbody.transform.InverseTransformPoint(vector3);
                                        }
                                        this.nextTapTime = Time.time;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private Vector3 GetInspectionTapPoint()
        {
            bool flag = this.inspectedPlayer != null && this.inspectedPlayer.cam != null;
            Vector3 result;
            if (flag)
            {
                result = this.inspectedPlayer.cam.transform.position + this.inspectedPlayer.cam.transform.forward * this.playerCameraTapDistance;
            }
            else
            {
                bool flag2 = this.inspectedPlayer != null;
                if (flag2)
                {
                    result = this.inspectedPlayer.transform.position;
                }
                else
                {
                    bool flag3 = this.playerInteractionRigidbody != null;
                    if (flag3)
                    {
                        result = this.playerInteractionRigidbody.position;
                    }
                    else
                    {
                        bool flag4 = this.inspectedItem != null;
                        if (flag4)
                        {
                            result = this.inspectedItem.transform.position;
                        }
                        else
                        {
                            bool flag5 = this.inspectedEntity != null;
                            if (flag5)
                            {
                                result = this.inspectedEntity.transform.position;
                            }
                            else
                            {
                                bool flag6 = this.inspectedButton != null;
                                if (flag6)
                                {
                                    result = this.inspectedButton.transform.position;
                                }
                                else
                                {
                                    result = this.destination;
                                }
                            }
                        }
                    }
                }
            }
            return result;
        }

        private bool IsLegUsable(Unseen.Leg leg)
        {
            return leg != null && leg.root != null && leg.bones != null && leg.bones.Count > 0 && leg.bones[0] != null;
        }

        private void ClearTap()
        {
            bool flag = this.tappingLeg != null;
            if (flag)
            {
                this.tappingLeg.tapUntil = 0f;
            }
            this.tappingLeg = null;
            this.tappedRigidbody = null;
            this.remainingTaps = 0;
        }

        private void ApplyTapEffect()
        {
            bool flag = !this.helpful && this.inspectedPlayer != null;
            if (flag)
            {
                this.inspectedPlayer.Damage(Damageable.DamageInfo.CreateDamageInfo(this.playerLegTapDamage, "unseen"));
            }
            GameEntity inspectableDamageTarget = this.GetInspectableDamageTarget();
            bool flag2 = inspectableDamageTarget != null;
            if (flag2)
            {
                inspectableDamageTarget.Damage(Damageable.DamageInfo.CreateDamageInfo(this.entityTapDamage, "unseen"));
            }
            bool flag3 = this.inspectedButton != null && Time.time >= this.nextButtonPressTime && this.inspectedButton.CanInteract() && UnityEngine.Random.value <= Mathf.Clamp01(this.buttonPressChance);
            if (flag3)
            {
                this.inspectedButton.Interact();
                this.nextButtonPressTime = Time.time + Mathf.Max(0.1f, this.inspectedButton.pressCooldown);
            }
            Rigidbody orCreateInteractionRigidbody = this.tappedRigidbody;
            bool flag4 = orCreateInteractionRigidbody == null && this.playerInteractionItem != null;
            if (flag4)
            {
                orCreateInteractionRigidbody = this.GetOrCreateInteractionRigidbody(this.playerInteractionItem);
            }
            bool flag5 = orCreateInteractionRigidbody != null && (orCreateInteractionRigidbody == this.playerInteractionRigidbody || this.playerInteractionItem != null);
            bool flag6 = this.helpful && this.playerInteractionItem != null && !this.forceFriendlyOffer && this.IsFood(this.playerInteractionItem) && orCreateInteractionRigidbody != null;
            if (flag6)
            {
                this.BeginConsumption(orCreateInteractionRigidbody);
            }
            else
            {
                bool flag7 = orCreateInteractionRigidbody != null && flag5 && this.TryCarryRigidbody(orCreateInteractionRigidbody, true);
                if (!flag7)
                {
                    bool flag8 = this.tappedRigidbody != null && this.TryCarryRigidbody(this.tappedRigidbody, false);
                    if (!flag8)
                    {
                        bool flag9 = this.tappedRigidbody != null;
                        if (flag9)
                        {
                            this.TryConsume(this.tappedRigidbody);
                        }
                        bool flag10 = this.tappedRigidbody == null || this.tappingLeg == null;
                        if (!flag10)
                        {
                            bool flag11 = this.consumedRigidbody == this.tappedRigidbody;
                            if (flag11)
                            {
                            }
                        }
                    }
                }
            }
        }

        private GameEntity GetInspectableDamageTarget()
        {
            bool flag = this.inspectedEntity is Denizen || this.inspectedEntity is CL_Prop;
            GameEntity result;
            if (flag)
            {
                result = this.inspectedEntity;
            }
            else
            {
                bool flag2 = this.inspectedItem == null;
                if (flag2)
                {
                    result = null;
                }
                else
                {
                    Denizen denizen;
                    if ((denizen = this.inspectedItem.GetComponent<Denizen>()) == null)
                    {
                        denizen = (this.inspectedItem.GetComponentInParent<Denizen>() ?? this.inspectedItem.GetComponentInChildren<Denizen>(true));
                    }
                    Denizen denizen2 = denizen;
                    bool flag3 = denizen2 != null;
                    if (flag3)
                    {
                        result = denizen2;
                    }
                    else
                    {
                        CL_Prop cl_Prop;
                        if ((cl_Prop = this.inspectedItem.GetComponent<CL_Prop>()) == null)
                        {
                            cl_Prop = (this.inspectedItem.GetComponentInParent<CL_Prop>() ?? this.inspectedItem.GetComponentInChildren<CL_Prop>(true));
                        }
                        CL_Prop cl_Prop2 = cl_Prop;
                        result = cl_Prop2;
                    }
                }
            }
            return result;
        }

        private Rigidbody GetOrCreateInteractionRigidbody(Item_Object item)
        {
            bool flag = item == null;
            Rigidbody result;
            if (flag)
            {
                result = null;
            }
            else
            {
                Rigidbody rigidbody = item.GetComponentInChildren<Rigidbody>(true);
                bool flag2 = rigidbody != null;
                if (flag2)
                {
                    result = rigidbody;
                }
                else
                {
                    rigidbody = item.gameObject.AddComponent<Rigidbody>();
                    rigidbody.isKinematic = true;
                    rigidbody.useGravity = false;
                    this.temporaryInteractionRigidbody = rigidbody;
                    this.forceFriendlyOffer = this.helpful;
                    result = rigidbody;
                }
            }
            return result;
        }

        private void TryConsume(Rigidbody rigidbody)
        {
            bool flag = this.consumedRigidbody != null || UnityEngine.Random.value > this.consumeChance || rigidbody == null;
            if (!flag)
            {
                Item_Object componentInParent = rigidbody.GetComponentInParent<Item_Object>();
                bool flag2 = componentInParent != null && (componentInParent.itemData == null || componentInParent.itemData.inBag);
                if (!flag2)
                {
                    GameEntity componentInParent2 = rigidbody.GetComponentInParent<GameEntity>();
                    bool flag3 = !this.IsFood(componentInParent) && (componentInParent2 == null || !componentInParent2.dead);
                    if (!flag3)
                    {
                        this.BeginConsumption(rigidbody);
                    }
                }
            }
        }

        private bool BeginConsumption(Rigidbody rigidbody)
        {
            bool flag = this.consumedRigidbody != null || rigidbody == null || !this.ClaimBody(rigidbody);
            bool result;
            if (flag)
            {
                result = false;
            }
            else
            {
                this.consumedRigidbody = rigidbody;
                this.consumedWasKinematic = rigidbody.isKinematic;
                this.consumedRigidbody.isKinematic = true;
                this.ClearPlayerInteraction();
                result = true;
            }
            return result;
        }

        private bool IsFood(Item_Object item)
        {
            bool flag = item == null;
            bool result;
            if (flag)
            {
                result = false;
            }
            else
            {
                ObjectTagger componentInParent = item.GetComponentInParent<ObjectTagger>();
                bool flag2 = componentInParent == null || componentInParent.tags == null;
                if (flag2)
                {
                    result = false;
                }
                else
                {
                    foreach (string text in componentInParent.tags)
                    {
                        bool flag3 = !string.IsNullOrEmpty(text) && (string.Equals(text, "roach", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "food", StringComparison.OrdinalIgnoreCase));
                        if (flag3)
                        {
                            return true;
                        }
                    }
                    result = false;
                }
            }
            return result;
        }

        private void UpdateConsumption()
        {
            bool flag = this.consumedRigidbody == null;
            if (flag)
            {
                this.ReleaseBodyOwnership(this.consumedRigidbody);
                this.consumedRigidbody = null;
            }
            else
            {
                Item_Object componentInParent = this.consumedRigidbody.GetComponentInParent<Item_Object>();
                bool flag2 = componentInParent != null && componentInParent.itemData != null && componentInParent.itemData.inBag;
                if (flag2)
                {
                    this.ReleaseBodyOwnership(this.consumedRigidbody);
                    this.consumedRigidbody = null;
                }
                else
                {
                    Vector3 position = base.transform.position;
                    this.consumedRigidbody.position = Vector3.MoveTowards(this.consumedRigidbody.position, position, this.consumePullSpeed * Time.deltaTime);
                    bool flag3 = Vector3.Distance(this.consumedRigidbody.position, position) > this.consumeDistance;
                    if (!flag3)
                    {
                        UnityEngine.Object.Destroy(this.consumedRigidbody.gameObject);
                        this.ReleaseBodyOwnership(this.consumedRigidbody);
                        this.consumedRigidbody = null;
                    }
                }
            }
        }

        private void UpdateHelpfulBehavior(int anchoredLegs)
        {
            ENT_Player player = ENT_Player.GetPlayer();
            bool flag = player == null;
            if (!flag)
            {
                float num = Vector3.Distance(base.transform.position, player.transform.position);
                this.KeepDistanceFromPlayer(anchoredLegs);
                bool flag2 = this.TryUpdateFriendlyPlayerInteraction(player, anchoredLegs);
                if (!flag2)
                {
                    bool flag3 = this.carriedRigidbody != null;
                    if (flag3)
                    {
                        this.ClearTap();
                        this.behaviorState = Unseen.BehaviorState.Moving;
                        this.UpdateFriendlyCarryBehavior(player, anchoredLegs, num);
                    }
                    else
                    {
                        bool flag4 = this.manipulatedRigidbody != null;
                        if (flag4)
                        {
                            this.ClearTap();
                            this.ApplyInspectionIdleMovement();
                        }
                        else
                        {
                            bool flag5 = num > this.helpfulFollowDistance;
                            if (flag5)
                            {
                                this.ClearTap();
                                this.inspectedItem = null;
                                this.inspectedEntity = null;
                                this.inspectedButton = null;
                                this.socialUntil = 0f;
                                this.behaviorState = Unseen.BehaviorState.Moving;
                                this.MoveTowardsPosition(player.transform.position, anchoredLegs);
                            }
                            else
                            {
                                bool flag6 = this.behaviorState == Unseen.BehaviorState.Inspecting && Time.time >= this.inspectUntil;
                                if (flag6)
                                {
                                    this.ClearTap();
                                    this.inspectedItem = null;
                                    this.inspectedEntity = null;
                                    this.inspectedButton = null;
                                    this.behaviorState = Unseen.BehaviorState.Moving;
                                }
                                bool flag7 = this.behaviorState == Unseen.BehaviorState.Inspecting;
                                if (flag7)
                                {
                                    this.ApplyInspectionIdleMovement();
                                }
                                else
                                {
                                    bool flag8 = Time.time < this.socialUntil;
                                    if (flag8)
                                    {
                                        this.ApplyInspectionIdleMovement();
                                    }
                                    else
                                    {
                                        bool flag9 = Time.time >= this.nextSocialTime && num < this.helpfulWanderRadius * 1.5f;
                                        if (flag9)
                                        {
                                            this.socialUntil = Time.time + UnityEngine.Random.Range(1.4f, 3.2f) / this.pace;
                                            this.nextSocialTime = this.socialUntil + UnityEngine.Random.Range(4f, 9f) / this.curiosity;
                                            this.idleCenter = base.transform.position;
                                            foreach (Unseen.Leg leg in this.legs)
                                            {
                                                bool flag10 = !this.IsLegUsable(leg) || leg.appearanceDirection < 0f || leg.isMovingToAnchor || leg.recoveryLocked || !this.CanRepositionLeg(leg);
                                                if (!flag10)
                                                {
                                                    bool flag11 = !this.TryReleaseLegAnchor(leg);
                                                    if (flag11)
                                                    {
                                                        break;
                                                    }
                                                    Vector3 normalized = (player.transform.position + Vector3.up - leg.root.position).normalized;
                                                    leg.tapTargetPosition = leg.root.position + normalized * Mathf.Min(3f, num * 0.35f) + Vector3.up * 1.2f;
                                                    leg.tapUntil = this.socialUntil;
                                                    this.BeginLegStep(leg, leg.tapTargetPosition, 0.55f / this.pace);
                                                    break;
                                                }
                                            }
                                        }
                                        else
                                        {
                                            bool flag12 = this.inspectedItem == null && this.inspectedEntity == null && this.inspectedButton == null && this.behaviorState != Unseen.BehaviorState.Inspecting && num <= this.inspectionItemRange;
                                            if (flag12)
                                            {
                                                Item_Object x = this.ChooseItemToInspect();
                                                bool flag13 = x != null;
                                                if (flag13)
                                                {
                                                    this.inspectedItem = x;
                                                    this.BeginInspection();
                                                    return;
                                                }
                                                GameEntity x2 = this.ChooseEntityToInspect();
                                                bool flag14 = x2 != null;
                                                if (flag14)
                                                {
                                                    this.inspectedEntity = x2;
                                                    this.BeginInspection();
                                                    return;
                                                }
                                                CL_Button x3 = this.ChooseButtonToInspect();
                                                bool flag15 = x3 != null;
                                                if (flag15)
                                                {
                                                    this.inspectedButton = x3;
                                                    this.BeginInspection();
                                                    return;
                                                }
                                            }
                                            this.MoveHelpfulWander(player, anchoredLegs);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private bool TryUpdateFriendlyPlayerInteraction(ENT_Player player, int anchoredLegs)
        {
            bool flag = this.carriedRigidbody != null || this.manipulatedRigidbody != null;
            bool result;
            if (flag)
            {
                result = false;
            }
            else
            {
                this.ObservePlayerInteractionSignals(false);
                result = this.UpdatePlayerInteractionMovement(anchoredLegs);
            }
            return result;
        }

        private bool TryUpdateHostileDroppedItem(int anchoredLegs)
        {
            bool flag = this.carriedRigidbody != null || this.manipulatedRigidbody != null;
            bool result;
            if (flag)
            {
                result = false;
            }
            else
            {
                bool flag2 = UnseenPlayerInteractionTracker.droppedItemVersion > this.observedDroppedItemVersion;
                if (flag2)
                {
                    this.observedDroppedItemVersion = UnseenPlayerInteractionTracker.droppedItemVersion;
                    Item_Object droppedItem = UnseenPlayerInteractionTracker.droppedItem;
                    bool flag3 = this.IsRecentPlayerInteraction(UnseenPlayerInteractionTracker.droppedItemTime) && Unseen.IsUsableWorldItem(droppedItem) && this.IsWithinPlayerInteractionRange(droppedItem.transform.position);
                    if (flag3)
                    {
                        this.BeginPlayerItemInteraction(droppedItem, true);
                    }
                }
                result = this.UpdatePlayerInteractionMovement(anchoredLegs);
            }
            return result;
        }

        private void ObservePlayerInteractionSignals(bool hostile)
        {
            if (!hostile)
            {
                bool flag = UnseenPlayerInteractionTracker.hitEntityVersion > this.observedHitEntityVersion;
                if (flag)
                {
                    this.observedHitEntityVersion = UnseenPlayerInteractionTracker.hitEntityVersion;
                    GameEntity hitEntity = UnseenPlayerInteractionTracker.hitEntity;
                    bool flag2 = this.IsRecentPlayerInteraction(UnseenPlayerInteractionTracker.hitEntityTime) && Unseen.IsUsableInteractionEntity(hitEntity) && this.IsWithinPlayerInteractionRange(hitEntity.transform.position);
                    if (flag2)
                    {
                        this.BeginPlayerEntityInteraction(hitEntity);
                    }
                }
                bool flag3 = this.playerInteractionItem != null || this.playerInteractionRigidbody != null || this.playerInteractionEntity != null;
                if (!flag3)
                {
                    bool flag4 = UnseenPlayerInteractionTracker.tossedPropVersion > this.observedTossedPropVersion;
                    if (flag4)
                    {
                        this.observedTossedPropVersion = UnseenPlayerInteractionTracker.tossedPropVersion;
                        Rigidbody tossedProp = UnseenPlayerInteractionTracker.tossedProp;
                        bool flag5 = this.IsRecentPlayerInteraction(UnseenPlayerInteractionTracker.tossedPropTime) && Unseen.IsUsableInteractionBody(tossedProp) && this.IsWithinPlayerInteractionRange(tossedProp.position);
                        if (flag5)
                        {
                            this.BeginPlayerPropInteraction(tossedProp);
                        }
                    }
                    bool flag6 = this.playerInteractionItem != null || this.playerInteractionRigidbody != null || this.playerInteractionEntity != null;
                    if (!flag6)
                    {
                        bool flag7 = UnseenPlayerInteractionTracker.droppedItemVersion > this.observedDroppedItemVersion;
                        if (flag7)
                        {
                            this.observedDroppedItemVersion = UnseenPlayerInteractionTracker.droppedItemVersion;
                            Item_Object droppedItem = UnseenPlayerInteractionTracker.droppedItem;
                            bool flag8 = this.IsRecentPlayerInteraction(UnseenPlayerInteractionTracker.droppedItemTime) && Unseen.IsUsableWorldItem(droppedItem) && this.IsWithinPlayerInteractionRange(droppedItem.transform.position);
                            if (flag8)
                            {
                                this.BeginPlayerItemInteraction(droppedItem, false);
                            }
                        }
                    }
                }
            }
        }

        private bool UpdatePlayerInteractionMovement(int anchoredLegs)
        {
            Transform playerInteractionTarget = this.GetPlayerInteractionTarget();
            bool flag = playerInteractionTarget == null;
            bool result;
            if (flag)
            {
                result = false;
            }
            else
            {
                bool flag2 = this.manipulatedRigidbody != null || this.carriedRigidbody != null;
                if (flag2)
                {
                    result = false;
                }
                else
                {
                    bool flag3 = Vector3.Distance(base.transform.position, playerInteractionTarget.position) > Mathf.Max(0.5f, this.playerInteractionApproachDistance);
                    if (flag3)
                    {
                        this.behaviorState = Unseen.BehaviorState.Moving;
                        this.MoveTowardsPosition(playerInteractionTarget.position, anchoredLegs);
                        result = true;
                    }
                    else
                    {
                        bool flag4 = this.behaviorState != Unseen.BehaviorState.Inspecting;
                        if (flag4)
                        {
                            this.inspectedItem = this.playerInteractionItem;
                            this.inspectedEntity = this.playerInteractionEntity;
                            this.inspectedButton = null;
                            this.BeginInspection();
                            this.inspectUntil = Time.time + Mathf.Min(2.5f, Mathf.Max(0.8f, this.itemLimbHoldTime + 1.2f));
                        }
                        bool flag5 = Time.time >= this.inspectUntil;
                        if (flag5)
                        {
                            this.ClearTap();
                            this.inspectedItem = null;
                            this.inspectedEntity = null;
                            this.behaviorState = Unseen.BehaviorState.Moving;
                            this.ClearPlayerInteraction();
                            result = false;
                        }
                        else
                        {
                            this.ApplyInspectionIdleMovement();
                            result = true;
                        }
                    }
                }
            }
            return result;
        }

        private void BeginPlayerItemInteraction(Item_Object item, bool hostile)
        {
            bool flag = !Unseen.IsUsableWorldItem(item);
            if (!flag)
            {
                this.ClearTap();
                this.inspectedItem = null;
                this.inspectedEntity = null;
                this.inspectedButton = null;
                this.playerInteractionItem = item;
                this.playerInteractionRigidbody = item.GetComponentInChildren<Rigidbody>(true);
                this.playerInteractionEntity = null;
                this.returnCarriedItemToPlayer = false;
                this.forceFriendlyOffer = (!hostile && this.playerInteractionRigidbody == null);
                this.consumeCarriedItem = hostile;
                this.behaviorState = Unseen.BehaviorState.Moving;
            }
        }

        private void BeginPlayerPropInteraction(Rigidbody rigidbody)
        {
            bool flag = !Unseen.IsUsableInteractionBody(rigidbody);
            if (!flag)
            {
                this.ClearTap();
                this.inspectedItem = null;
                this.inspectedEntity = rigidbody.GetComponentInParent<CL_Prop>();
                this.inspectedButton = null;
                this.playerInteractionItem = null;
                this.playerInteractionRigidbody = rigidbody;
                this.playerInteractionEntity = this.inspectedEntity;
                this.returnCarriedItemToPlayer = true;
                this.forceFriendlyOffer = false;
                this.consumeCarriedItem = false;
                this.behaviorState = Unseen.BehaviorState.Moving;
            }
        }

        private void BeginPlayerEntityInteraction(GameEntity entity)
        {
            bool flag = !Unseen.IsUsableInteractionEntity(entity);
            if (!flag)
            {
                this.ClearTap();
                this.inspectedItem = null;
                this.inspectedEntity = null;
                this.inspectedButton = null;
                this.playerInteractionItem = null;
                this.playerInteractionRigidbody = null;
                this.playerInteractionEntity = entity;
                this.returnCarriedItemToPlayer = false;
                this.forceFriendlyOffer = false;
                this.consumeCarriedItem = false;
                this.behaviorState = Unseen.BehaviorState.Moving;
            }
        }

        private Transform GetPlayerInteractionTarget()
        {
            bool flag = this.playerInteractionItem != null;
            Transform result;
            if (flag)
            {
                result = this.playerInteractionItem.transform;
            }
            else
            {
                bool flag2 = this.playerInteractionRigidbody != null;
                if (flag2)
                {
                    result = this.playerInteractionRigidbody.transform;
                }
                else
                {
                    bool flag3 = this.playerInteractionEntity != null;
                    if (flag3)
                    {
                        result = this.playerInteractionEntity.transform;
                    }
                    else
                    {
                        result = null;
                    }
                }
            }
            return result;
        }

        private bool IsRecentPlayerInteraction(float interactionTime)
        {
            return Time.time - interactionTime <= Mathf.Max(0.1f, this.playerInteractionMemoryDuration);
        }

        private bool IsWithinPlayerInteractionRange(Vector3 position)
        {
            return Vector3.Distance(base.transform.position, position) <= Mathf.Max(0.5f, this.playerInteractionResponseRange);
        }

        private static bool IsUsableWorldItem(Item_Object item)
        {
            return item != null && item.gameObject.activeInHierarchy && item.itemData != null && !item.itemData.inBag;
        }

        private static bool IsUsableInteractionBody(Rigidbody rigidbody)
        {
            return rigidbody != null && rigidbody.gameObject.activeInHierarchy && rigidbody.GetComponentInParent<ENT_Player>() == null;
        }

        private static bool IsUsableInteractionEntity(GameEntity entity)
        {
            return entity != null && entity.gameObject.activeInHierarchy && !(entity is ENT_Player) && !entity.dead;
        }

        private void ClearPlayerInteraction()
        {
            this.playerInteractionItem = null;
            this.playerInteractionRigidbody = null;
            this.playerInteractionEntity = null;
            this.returnCarriedItemToPlayer = false;
            this.forceFriendlyOffer = false;
            this.consumeCarriedItem = false;
            this.temporaryInteractionRigidbody = null;
        }

        private void MoveHelpfulWander(ENT_Player player, int anchoredLegs)
        {
            bool flag = Time.time >= this.helpfulWanderUntil || Vector3.Distance(player.transform.position, this.helpfulWanderDestination) > this.helpfulFollowDistance * 0.8f;
            if (flag)
            {
                Vector3 vector = UnityEngine.Random.onUnitSphere * this.helpfulWanderRadius;
                vector.y = Mathf.Max(vector.y, -this.helpfulWanderRadius * 0.25f);
                this.helpfulWanderDestination = player.transform.position + vector;
                this.helpfulWanderUntil = Time.time + UnityEngine.Random.Range(this.helpfulWanderDurationMinimum, this.helpfulWanderDurationMaximum);
            }
            bool flag2 = Vector3.Distance(base.transform.position, this.helpfulWanderDestination) <= this.helpfulWanderMinimumDistance;
            if (flag2)
            {
                this.body.AddForce(-this.body.velocity * 3f, ForceMode.Acceleration);
            }
            else
            {
                this.MoveTowardsPosition(this.helpfulWanderDestination, anchoredLegs);
            }
        }

        private void MoveTowardsPosition(Vector3 targetPosition, int anchoredLegs)
        {
            bool flag = anchoredLegs <= 0;
            if (!flag)
            {
                Vector3 direction = targetPosition - base.transform.position;
                bool flag2 = direction.sqrMagnitude < 0.01f;
                if (!flag2)
                {
                    this.MoveWithDefaultSteering(direction, Mathf.Min((float)anchoredLegs * 2f * this.pace, Mathf.Sqrt(direction.magnitude * 12f)));
                }
            }
        }

        private bool TryCarryRigidbody(Rigidbody rigidbody, bool force = false)
        {
            bool flag = rigidbody == null || this.carriedRigidbody != null || this.manipulatedRigidbody != null || this.tappingLeg == null;
            bool result;
            if (flag)
            {
                result = false;
            }
            else
            {
                bool flag2 = rigidbody.GetComponentInParent<ENT_Player>() != null || rigidbody.transform.IsChildOf(base.transform) || (!force && UnityEngine.Random.value > Mathf.Clamp01(this.rigidbodyGrabChance));
                if (flag2)
                {
                    result = false;
                }
                else
                {
                    Item_Object componentInParent = rigidbody.GetComponentInParent<Item_Object>();
                    bool flag3 = componentInParent != null && (componentInParent.itemData == null || componentInParent.itemData.inBag);
                    if (flag3)
                    {
                        result = false;
                    }
                    else
                    {
                        bool flag4 = !this.ClaimBody(rigidbody);
                        if (flag4)
                        {
                            result = false;
                        }
                        else
                        {
                            this.BeginItemManipulation(rigidbody, this.tappingLeg);
                            result = true;
                        }
                    }
                }
            }
            return result;
        }

        private bool ClaimBody(Rigidbody rigidbody)
        {
            bool flag = rigidbody == null;
            bool result;
            if (flag)
            {
                result = false;
            }
            else
            {
                Unseen x;
                bool flag2 = Unseen.bodyOwners.TryGetValue(rigidbody, out x) && x != null;
                if (flag2)
                {
                    result = false;
                }
                else
                {
                    Unseen.bodyOwners[rigidbody] = this;
                    result = true;
                }
            }
            return result;
        }

        private void ReleaseBodyOwnership(Rigidbody rigidbody)
        {
            bool flag = rigidbody == null;
            if (!flag)
            {
                Unseen unseen;
                bool flag2 = Unseen.bodyOwners.TryGetValue(rigidbody, out unseen) && unseen == this;
                if (flag2)
                {
                    Unseen.bodyOwners.Remove(rigidbody);
                }
            }
        }

        private void BeginItemManipulation(Rigidbody rigidbody, Unseen.Leg leg)
        {
            bool flag = rigidbody == null || leg == null || this.manipulatedRigidbody != null || this.carriedRigidbody != null;
            if (!flag)
            {
                this.manipulatedRigidbody = rigidbody;
                this.manipulationLeg = leg;
                this.AttachItemToLeg(this.manipulatedRigidbody);
                leg.anchorTransform = null;
                leg.tapUntil = 0f;
                this.manipulationReleaseTime = Time.time + Mathf.Max(0.2f, this.itemLimbHoldTime);
            }
        }

        private void UpdateItemManipulation()
        {
            bool flag = this.heldBody == null;
            if (flag)
            {
                this.ReleaseHeldItem();
            }
            Item_Object item_Object = (this.heldBody != null) ? this.heldBody.GetComponentInParent<Item_Object>() : null;
            bool flag2 = item_Object != null && item_Object.itemData != null && item_Object.itemData.inBag;
            if (flag2)
            {
                this.ReleaseHeldItem();
                this.manipulatedRigidbody = null;
                this.manipulationLeg = null;
                this.carriedRigidbody = null;
                this.carriedItem = null;
                this.carriedLeg = null;
            }
            bool flag3 = this.manipulatedRigidbody != null;
            if (flag3)
            {
                bool flag4 = !this.IsLegUsable(this.manipulationLeg);
                if (flag4)
                {
                    this.ReleaseHeldItem();
                    this.manipulatedRigidbody = null;
                    this.manipulationLeg = null;
                    return;
                }
                bool flag5 = Time.time < this.manipulationReleaseTime;
                if (flag5)
                {
                    return;
                }
                this.carriedRigidbody = this.manipulatedRigidbody;
                this.carriedItem = this.carriedRigidbody.GetComponentInParent<Item_Object>();
                this.carriedLeg = this.manipulationLeg;
                this.ReleaseUnseenHandItem();
                this.heldHandItem = ((this.carriedItem != null) ? UnseenHandItem.Create(this, this.carriedItem.itemData) : null);
                this.manipulatedRigidbody = null;
                this.manipulationLeg = null;
                this.carriedUntil = Time.time + (this.consumeCarriedItem ? Mathf.Max(0.5f, this.hostileDroppedItemCarryDuration) : (Mathf.Max(0.5f, this.carryDuration) * UnityEngine.Random.Range(0.75f, 1.35f)));
                this.carryDisposition = (this.forceFriendlyOffer ? Unseen.CarryDisposition.Offering : Unseen.CarryDisposition.Wander);
                this.offerUntil = (this.forceFriendlyOffer ? (Time.time + Mathf.Max(0.25f, this.friendlyOfferDuration)) : 0f);
                this.inspectedItem = null;
                this.behaviorState = Unseen.BehaviorState.Moving;
            }
            bool flag6 = this.carriedRigidbody != null && !this.IsLegUsable(this.carriedLeg);
            if (flag6)
            {
                this.ReleaseHeldItem();
                this.carriedRigidbody = null;
                this.carriedItem = null;
                this.carriedLeg = null;
                this.ReleaseUnseenHandItem();
            }
        }

        private Vector3 GetLegTipPosition(Unseen.Leg leg)
        {
            return leg.bones[leg.bones.Count - 1].position;
        }

        private bool IsHoldingLeg(Unseen.Leg leg)
        {
            return this.heldBody != null && (leg == this.manipulationLeg || leg == this.carriedLeg);
        }

        private void UpdateHeldItemPose()
        {
            Unseen.Leg leg = (this.carriedRigidbody != null) ? this.carriedLeg : this.manipulationLeg;
            bool flag = this.heldBody == null || !this.IsLegUsable(leg);
            if (!flag)
            {
                Vector3 b = this.heldBody.transform.TransformVector(this.heldLocalPoint);
                this.heldBody.position = this.GetLegTipPosition(leg) - b;
                this.heldBody.transform.position = this.heldBody.position;
            }
        }

        private void AttachItemToLeg(Rigidbody rigidbody)
        {
            this.heldBody = rigidbody;
            this.heldWasKinematic = false;
            this.heldInterpolation = rigidbody.interpolation;
            this.heldLocalPoint = rigidbody.transform.InverseTransformPoint(this.repeatedTapPoint);
            rigidbody.velocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
            rigidbody.isKinematic = true;
            rigidbody.interpolation = RigidbodyInterpolation.None;
            this.UpdateHeldItemPose();
        }

        private void ReleaseHeldItem()
        {
            this.ReleaseBodyOwnership(this.heldBody);
            bool flag = this.heldBody != null;
            if (flag)
            {
                Item_Object componentInParent = this.heldBody.GetComponentInParent<Item_Object>();
                bool flag2 = componentInParent != null && componentInParent.itemData != null && componentInParent.itemData.inBag;
                bool flag3 = !flag2;
                if (flag3)
                {
                    this.heldBody.isKinematic = this.heldWasKinematic;
                }
                this.heldBody.interpolation = this.heldInterpolation;
                bool flag4 = !flag2 && !this.heldWasKinematic;
                if (flag4)
                {
                    this.heldBody.velocity = ((this.body != null) ? this.body.velocity : Vector3.zero);
                }
            }
            this.heldBody = null;
        }

        private void GiveCarriedItem(ENT_Player player)
        {
            Inventory inventory = (player != null) ? player.GetComponentInChildren<Inventory>() : null;
            bool flag = this.carriedItem == null || this.carriedItem.itemData == null || inventory == null;
            if (!flag)
            {
                Item itemData = this.carriedItem.itemData;
                Rigidbody rigidbody = this.temporaryInteractionRigidbody;
                this.ReleaseHeldItem();
                this.carriedRigidbody = null;
                this.carriedItem = null;
                this.carriedLeg = null;
                this.ReleaseUnseenHandItem();
                this.behaviorState = Unseen.BehaviorState.Moving;
                this.socialUntil = Time.time + 1.8f;
                this.idleCenter = base.transform.position;
                this.nextSocialTime = this.socialUntil + UnityEngine.Random.Range(4f, 8f);
                inventory.AddItemToInventoryCenter(itemData);
                bool flag2 = rigidbody != null;
                if (flag2)
                {
                    UnityEngine.Object.Destroy(rigidbody);
                }
                this.ClearPlayerInteraction();
            }
        }

        private void UpdateFriendlyCarryBehavior(ENT_Player player, int anchoredLegs, float playerDistance)
        {
            bool flag = this.carriedRigidbody == null;
            if (!flag)
            {
                this.TryUseHeldHandItem();
                bool flag2 = this.returnCarriedItemToPlayer;
                if (flag2)
                {
                    this.MoveCarryTowards(player.transform.position + Vector3.up * 0.75f, anchoredLegs);
                    bool flag3 = playerDistance <= Mathf.Max(0.5f, this.playerInteractionApproachDistance);
                    if (flag3)
                    {
                        this.ThrowCarriedItem(player.transform.position - base.transform.position);
                    }
                }
                else
                {
                    bool flag4 = this.carryDisposition == Unseen.CarryDisposition.Wander && Time.time >= this.carriedUntil;
                    if (flag4)
                    {
                        this.carryDisposition = Unseen.CarryDisposition.Offering;
                        this.offerUntil = Time.time + Mathf.Max(0.25f, this.friendlyOfferDuration);
                    }
                    bool flag5 = this.carryDisposition == Unseen.CarryDisposition.Wander;
                    if (flag5)
                    {
                        Vector3 targetPosition = player.transform.position + (base.transform.position - player.transform.position).normalized * Mathf.Min(this.helpfulWanderRadius, 7f);
                        this.MoveCarryTowards(targetPosition, anchoredLegs);
                    }
                    else
                    {
                        this.MoveCarryTowards(player.transform.position + Vector3.up * 0.75f, anchoredLegs);
                        bool flag6 = Time.time < this.offerUntil || playerDistance > this.helpfulGiveDistance;
                        if (!flag6)
                        {
                            bool flag7 = this.carriedItem != null && this.carriedItem.itemData != null && (this.forceFriendlyOffer || UnityEngine.Random.value <= Mathf.Clamp01(this.friendlyGiveChance));
                            if (flag7)
                            {
                                this.GiveCarriedItem(player);
                            }
                            else
                            {
                                this.ThrowCarriedItem(player.transform.position - base.transform.position);
                            }
                        }
                    }
                }
            }
        }

        private void UpdateHostileCarryBehavior(int anchoredLegs)
        {
            ENT_Player player = ENT_Player.GetPlayer();
            bool flag = this.manipulatedRigidbody != null;
            if (!flag)
            {
                bool flag2 = this.carriedRigidbody == null;
                if (!flag2)
                {
                    this.TryUseHeldHandItem();
                    bool flag3 = Time.time >= this.carriedUntil;
                    if (flag3)
                    {
                        bool flag4 = this.consumeCarriedItem;
                        if (flag4)
                        {
                            this.ConsumeCarriedItem();
                        }
                        else
                        {
                            Vector3 direction = (player != null) ? (player.transform.position - base.transform.position) : (this.curvedMovementDirection + UnityEngine.Random.insideUnitSphere).normalized;
                            this.ThrowCarriedItem(direction);
                        }
                    }
                    else
                    {
                        Vector3 targetPosition = (player != null) ? (player.transform.position + Vector3.Cross(player.transform.position - base.transform.position, Vector3.up).normalized * 6f) : this.destination;
                        this.MoveCarryTowards(targetPosition, anchoredLegs);
                    }
                }
            }
        }

        private void ConsumeCarriedItem()
        {
            Rigidbody rigidbody = this.carriedRigidbody;
            this.ReleaseHeldItem();
            this.carriedRigidbody = null;
            this.carriedItem = null;
            this.carriedLeg = null;
            this.ReleaseUnseenHandItem();
            this.carryDisposition = Unseen.CarryDisposition.Wander;
            bool flag = rigidbody == null || !this.ClaimBody(rigidbody);
            if (flag)
            {
                this.ClearPlayerInteraction();
            }
            else
            {
                this.consumedRigidbody = rigidbody;
                this.consumedWasKinematic = rigidbody.isKinematic;
                rigidbody.isKinematic = true;
                this.ClearPlayerInteraction();
            }
        }

        private void MoveCarryTowards(Vector3 targetPosition, int anchoredLegs)
        {
            bool flag = anchoredLegs <= 0;
            if (!flag)
            {
                Vector3 direction = targetPosition - base.transform.position;
                bool flag2 = direction.sqrMagnitude < 0.01f;
                if (!flag2)
                {
                    float desiredSpeed = Mathf.Min((float)anchoredLegs * 2f * this.pace * Mathf.Max(0.1f, this.carrySpeedMultiplier), Mathf.Sqrt(direction.magnitude * 12f));
                    this.MoveWithDefaultSteering(direction, desiredSpeed);
                }
            }
        }

        private void ThrowCarriedItem(Vector3 direction)
        {
            bool flag = this.carriedRigidbody == null;
            if (!flag)
            {
                Rigidbody rigidbody = this.carriedRigidbody;
                Rigidbody rigidbody2 = this.temporaryInteractionRigidbody;
                this.ReleaseHeldItem();
                this.carriedRigidbody = null;
                this.carriedItem = null;
                this.carriedLeg = null;
                this.ReleaseUnseenHandItem();
                this.carryDisposition = Unseen.CarryDisposition.Wander;
                Vector3 a = (direction.sqrMagnitude > 0.001f) ? direction.normalized : UnityEngine.Random.onUnitSphere;
                rigidbody.velocity = (a + UnityEngine.Random.insideUnitSphere * 0.2f).normalized * this.rigidbodyThrowForce;
                bool flag2 = rigidbody2 != null;
                if (flag2)
                {
                    UnityEngine.Object.Destroy(rigidbody2);
                }
                this.ClearPlayerInteraction();
            }
        }

        private void UpdateParentLevel()
        {
            bool flag = Time.time < this.nextLevelCheckTime;
            if (!flag)
            {
                this.nextLevelCheckTime = Time.time + 0.5f;
                WorldLoader.LevelInfo closestLevelToPosition = WorldLoader.GetClosestLevelToPosition(base.transform.position);
                M_Level x = (closestLevelToPosition != null) ? closestLevelToPosition.GetLevel() : null;
                bool flag2 = x == null || x == this.level;
                if (!flag2)
                {
                    this.level = x;
                    base.transform.SetParent(this.level.transform, true);
                }
            }
        }

        private void StopInspectionForAttack()
        {
            bool flag = this.behaviorState != Unseen.BehaviorState.Inspecting;
            if (!flag)
            {
                this.ClearTap();
                this.inspectedItem = null;
                this.inspectedEntity = null;
                this.inspectedButton = null;
                this.inspectedPlayer = null;
                this.behaviorState = Unseen.BehaviorState.Moving;
            }
        }

        private void AbandonCurrentTask()
        {
            this.ClearTap();
            this.inspectedItem = null;
            this.inspectedEntity = null;
            this.inspectedButton = null;
            this.inspectedPlayer = null;
            this.behaviorState = Unseen.BehaviorState.Moving;
            this.destination = this.GetRandomLevelPoint();
            this.destinationTimeout = Time.time + UnityEngine.Random.Range(this.destinationDurationMinimum, this.destinationDurationMaximum);
        }

        private void BeginRecovery()
        {
            this.behaviorState = Unseen.BehaviorState.Recovering;
            foreach (Unseen.Leg leg in this.legs)
            {
                bool flag = leg.anchorTransform != null && leg.appearanceDirection >= 0f;
                if (flag)
                {
                    leg.recoveryLocked = true;
                }
            }
            this.body.velocity = Vector3.zero;
            this.body.angularVelocity = Vector3.zero;
        }

        private void UpdateRecoveryMovement(int anchoredLegs)
        {
            this.ConstrainRecoveryLocks();
            bool flag = anchoredLegs >= this.legs.Count - ((this.heldBody != null) ? 1 : 0);
            if (flag)
            {
                foreach (Unseen.Leg leg in this.legs)
                {
                    leg.recoveryLocked = false;
                }
                this.ChooseNextDestination();
            }
            else
            {
                Vector3 a = Vector3.zero;
                int num = 0;
                foreach (Unseen.Leg leg2 in this.legs)
                {
                    bool flag2 = !leg2.recoveryLocked || leg2.anchorTransform == null;
                    if (!flag2)
                    {
                        a += leg2.anchorTransform.TransformPoint(leg2.anchorLocalPosition);
                        num++;
                    }
                }
                bool flag3 = num == 0 || anchoredLegs == 0;
                if (!flag3)
                {
                    a /= (float)num;
                    Vector3 a2 = a - base.transform.position;
                    float magnitude = a2.magnitude;
                    bool flag4 = magnitude <= 0.2f;
                    if (!flag4)
                    {
                        Vector3 a3 = a2 / magnitude;
                        float a4 = (float)anchoredLegs * this.recoveryMaximumSpeedPerLeg;
                        Vector3 a5 = a3 * Mathf.Min(a4, magnitude * 2f);
                        this.body.AddForce(Vector3.ClampMagnitude((a5 - this.body.velocity) * this.recoveryPullForce, this.recoveryRopeForce), ForceMode.Acceleration);
                        this.ConstrainRecoveryLocks();
                    }
                }
            }
        }

        private void ConstrainRecoveryLocks()
        {
            foreach (Unseen.Leg leg in this.legs)
            {
                bool flag = !leg.recoveryLocked || leg.anchorTransform == null;
                if (!flag)
                {
                    Vector3 b = leg.anchorTransform.TransformPoint(leg.anchorLocalPosition);
                    Vector3 a = base.transform.position - b;
                    float magnitude = a.magnitude;
                    float num = leg.maximumReach * 0.72f;
                    bool flag2 = magnitude <= num;
                    if (!flag2)
                    {
                        Vector3 vector = a / magnitude;
                        float num2 = magnitude - num;
                        float b2 = Vector3.Dot(this.body.velocity, vector);
                        this.body.AddForce(-vector * (num2 * this.recoveryRopeForce + Mathf.Max(0f, b2) * this.recoveryRopeDamping), ForceMode.Acceleration);
                    }
                }
            }
        }

        private int GetRecoveryLockCount()
        {
            int num = 0;
            foreach (Unseen.Leg leg in this.legs)
            {
                bool flag = leg.recoveryLocked && leg.anchorTransform != null;
                if (flag)
                {
                    num++;
                }
            }
            return num;
        }

        private Vector3 GetKneePoleNormal(Unseen.Leg leg)
        {
            Vector3 vector = leg.bones[0].position - base.transform.position;
            bool flag = vector.sqrMagnitude < 0.0001f;
            if (flag)
            {
                vector = Vector3.forward;
            }
            return (Vector3.up + vector.normalized * 0.2f).normalized;
        }

        private void KeepDistanceFromPlayer(int anchoredLegs)
        {
            ENT_Player player = ENT_Player.GetPlayer();
            bool flag = player == null || anchoredLegs <= 0;
            if (!flag)
            {
                Vector3 a = base.transform.position - player.transform.position;
                float magnitude = a.magnitude;
                bool flag2 = magnitude >= this.playerBodyClearance;
                if (!flag2)
                {
                    Vector3 direction = (magnitude > 0.0001f) ? (a / magnitude) : UnityEngine.Random.onUnitSphere;
                    float desiredSpeed = Mathf.Min((float)anchoredLegs * 2f, (this.playerBodyClearance - magnitude) * this.playerAvoidanceForce * 0.1f);
                    this.MoveWithDefaultSteering(direction, desiredSpeed);
                }
            }
        }

        private bool IsVisibleToPlayer()
        {
            bool flag = this.helpful;
            bool result;
            if (flag)
            {
                result = false;
            }
            else
            {
                ENT_Player player = ENT_Player.GetPlayer();
                Camera camera = (player != null) ? player.cam : null;
                bool flag2 = this.IsVisibleToCamera(camera);
                result = (flag2 || (camera != Camera.main && this.IsVisibleToCamera(Camera.main)));
            }
            return result;
        }

        private bool IsPlayerLookingAtUnseen()
        {
            ENT_Player player = ENT_Player.GetPlayer();
            Camera camera = (player != null) ? player.cam : null;
            bool flag = camera == null;
            bool result;
            if (flag)
            {
                result = false;
            }
            else
            {
                Vector3 vector = camera.WorldToViewportPoint(base.transform.position);
                result = (vector.z > 0f && vector.x >= 0f && vector.x <= 1f && vector.y >= 0f && vector.y <= 1f);
            }
            return result;
        }

        private bool IsVisibleToCamera(Camera camera)
        {
            bool flag = camera == null;
            bool result;
            if (flag)
            {
                result = false;
            }
            else
            {
                Vector3 vector = camera.WorldToViewportPoint(base.transform.position);
                result = (vector.z > 0f && vector.x >= -this.playerSightMargin && vector.x <= 1f + this.playerSightMargin && vector.y >= -this.playerSightMargin && vector.y <= 1f + this.playerSightMargin);
            }
            return result;
        }

        private void ChargePlayer(int anchoredLegs)
        {
            ENT_Player player = ENT_Player.GetPlayer();
            bool flag = player == null || this.body == null || anchoredLegs <= 0;
            if (!flag)
            {
                Vector3 direction = player.transform.position - base.transform.position;
                bool flag2 = direction.sqrMagnitude < 0.0001f;
                if (!flag2)
                {
                    float desiredSpeed = Mathf.Min(this.hostileAttackApproachSpeed, (float)anchoredLegs * 2.5f);
                    this.MoveWithDefaultSteering(direction, desiredSpeed);
                }
            }
        }

        private void ApplyIdleMovement()
        {
            Vector3 b = new Vector3(Mathf.Sin(Time.time * this.idleMovementFrequency), Mathf.Sin(Time.time * this.idleMovementFrequency * 1.37f), Mathf.Cos(Time.time * this.idleMovementFrequency * 0.83f)) * this.idleMovementRadius;
            Vector3 direction = this.idleCenter + b - base.transform.position;
            bool flag = direction.sqrMagnitude > 0.0001f;
            if (flag)
            {
                this.MoveWithDefaultSteering(direction, Mathf.Min(this.idleMovementSpeed, direction.magnitude * this.idleMovementForce * 0.1f));
            }
        }

        private void ApplyInspectionIdleMovement()
        {
            Vector3 b = new Vector3(Mathf.Sin((Time.time * this.pace + this.personalityPhase) * this.inspectionIdleMovementFrequency), Mathf.Sin((Time.time * this.pace + this.personalityPhase) * this.inspectionIdleMovementFrequency * 1.37f), Mathf.Cos((Time.time * this.pace + this.personalityPhase) * this.inspectionIdleMovementFrequency * 0.83f)) * this.inspectionIdleMovementRadius;
            Vector3 direction = this.idleCenter + b - base.transform.position;
            bool flag = direction.sqrMagnitude > 0.0001f;
            if (flag)
            {
                this.MoveWithDefaultSteering(direction, Mathf.Min(this.inspectionIdleMovementSpeed, direction.magnitude * this.inspectionIdleMovementForce * 0.1f));
            }
            this.body.AddForce(-this.body.velocity * this.inspectionIdleMovementDamping, ForceMode.Acceleration);
        }

        private void CapVelocity()
        {
            float num = (this.behaviorState == Unseen.BehaviorState.Attacking) ? this.visiblePlayerChargeMaximumVelocity : (this.helpful ? this.maximumVelocity : Mathf.Max(this.maximumVelocity, this.hostileCatchUpSpeed * this.pace));
            bool flag = this.body.velocity.magnitude > num;
            if (flag)
            {
                this.body.velocity = this.body.velocity.normalized * num;
            }
        }

        private void UpdateLegAppearances()
        {
            for (int i = this.legs.Count - 1; i >= 0; i--)
            {
                Unseen.Leg leg = this.legs[i];
                bool flag = leg.appearanceDirection == 0f;
                if (!flag)
                {
                    leg.appearanceScale = Mathf.MoveTowards(leg.appearanceScale, (leg.appearanceDirection > 0f) ? 1f : 0f, Time.deltaTime);
                    bool flag2 = leg.appearanceScale > 0f || leg.appearanceDirection > 0f;
                    if (!flag2)
                    {
                        bool flag3 = leg == this.tappingLeg;
                        if (flag3)
                        {
                            this.ClearTap();
                        }
                        List<string> list = new List<string>();
                        foreach (KeyValuePair<string, Unseen.ContactShadow> keyValuePair in this.contactShadows)
                        {
                            bool flag4 = keyValuePair.Key.StartsWith(leg.root.GetInstanceID().ToString() + "_");
                            if (flag4)
                            {
                                list.Add(keyValuePair.Key);
                            }
                        }
                        foreach (string key in list)
                        {
                            this.RemoveContactShadow(key);
                        }
                        UnityEngine.Object.Destroy(leg.root.gameObject);
                        foreach (Transform transform in leg.segmentRenderers)
                        {
                            bool flag5 = transform != null;
                            if (flag5)
                            {
                                UnityEngine.Object.Destroy(transform.gameObject);
                            }
                        }
                        foreach (Transform transform2 in leg.kneeRenderers)
                        {
                            bool flag6 = transform2 != null;
                            if (flag6)
                            {
                                UnityEngine.Object.Destroy(transform2.gameObject);
                            }
                        }
                        bool flag7 = leg.tip != null;
                        if (flag7)
                        {
                            UnityEngine.Object.Destroy(leg.tip.gameObject);
                        }
                        this.legs.RemoveAt(i);
                    }
                }
            }
        }

        private void UpdateLegMutation()
        {
            bool flag = Time.time < this.nextLegMutationTime;
            if (!flag)
            {
                this.nextLegMutationTime = Time.time + UnityEngine.Random.Range(1f, 5f);
                bool flag2 = this.legs.Count <= 5;
                if (flag2)
                {
                    int num = this.nextLegId;
                    this.nextLegId = num + 1;
                    this.CreateLeg(num, true);
                }
                else
                {
                    bool flag3 = this.legs.Count >= 12;
                    if (flag3)
                    {
                        this.BeginLegRetraction();
                    }
                    else
                    {
                        bool flag4 = UnityEngine.Random.value < 0.5f;
                        if (flag4)
                        {
                            int num = this.nextLegId;
                            this.nextLegId = num + 1;
                            this.CreateLeg(num, true);
                        }
                        else
                        {
                            this.BeginLegRetraction();
                        }
                    }
                }
            }
        }

        private void BeginLegRetraction()
        {
            List<Unseen.Leg> list = new List<Unseen.Leg>();
            foreach (Unseen.Leg leg in this.legs)
            {
                bool flag = leg != this.tappingLeg && !this.IsHoldingLeg(leg) && !leg.recoveryLocked && leg.appearanceDirection >= 0f;
                if (flag)
                {
                    list.Add(leg);
                }
            }
            bool flag2 = list.Count == 0;
            if (flag2)
            {
                bool flag3 = this.legs.Count < 12;
                if (flag3)
                {
                    int num = this.nextLegId;
                    this.nextLegId = num + 1;
                    this.CreateLeg(num, true);
                }
            }
            else
            {
                Unseen.Leg leg2 = list[UnityEngine.Random.Range(0, list.Count)];
                bool flag4 = !this.TryReleaseLegAnchor(leg2);
                if (!flag4)
                {
                    leg2.appearanceDirection = -1f;
                }
            }
        }

        private void UpdateLegRenderers(Unseen.Leg leg)
        {
            for (int i = 0; i < leg.segmentRenderers.Count; i++)
            {
                Vector3 position = leg.bones[i].position;
                Vector3 position2 = leg.bones[i + 1].position;
                Vector3 a = position2 - position;
                float magnitude = a.magnitude;
                bool flag = magnitude < 0.0001f;
                if (!flag)
                {
                    Transform transform = leg.segmentRenderers[i];
                    transform.position = position + a * 0.5f;
                    transform.rotation = Quaternion.LookRotation(a / magnitude, base.transform.up);
                    transform.localScale = new Vector3(0.1f * leg.appearanceScale, 0.1f * leg.appearanceScale, magnitude);
                }
            }
            for (int j = 0; j < leg.kneeRenderers.Count; j++)
            {
                Transform transform2 = leg.kneeRenderers[j];
                transform2.position = leg.bones[j + 1].position;
                transform2.localScale = Vector3.one * 0.1f * leg.appearanceScale;
            }
            Vector3 position3 = leg.bones[leg.segmentRenderers.Count].position;
            Vector3 forward = leg.bones[leg.bones.Count - 1].position - position3;
            bool flag2 = forward.sqrMagnitude < 0.0001f;
            if (flag2)
            {
                forward = position3 - leg.bones[leg.segmentRenderers.Count - 1].position;
            }
            float magnitude2 = forward.magnitude;
            forward.Normalize();
            leg.tip.position = position3;
            leg.tip.rotation = Quaternion.LookRotation(forward, base.transform.up);
            leg.tip.localScale = new Vector3(0.1f * leg.appearanceScale, 0.1f * leg.appearanceScale, magnitude2);
        }

        private void UpdateLegContactShadows(Unseen.Leg leg)
        {
            for (int i = 0; i < leg.segmentRenderers.Count; i++)
            {
                Vector3 position = leg.bones[i].position;
                Vector3 position2 = leg.bones[i + 1].position;
                Vector3 vector = position2 - position;
                float magnitude = vector.magnitude;
                string str = leg.root.GetInstanceID().ToString() + "_" + i.ToString();
                this.UpdateLegContactShadow(str + "_Forward", position, vector, magnitude);
                this.UpdateLegContactShadow(str + "_Reverse", position2, -vector, magnitude);
            }
            int count = leg.segmentRenderers.Count;
            Vector3 position3 = leg.bones[count].position;
            Vector3 position4 = leg.bones[leg.bones.Count - 1].position;
            Vector3 vector2 = position4 - position3;
            float magnitude2 = vector2.magnitude;
            string str2 = leg.root.GetInstanceID().ToString() + "_" + count.ToString();
            this.UpdateLegContactShadow(str2 + "_Forward", position3, vector2, magnitude2);
            this.UpdateLegContactShadow(str2 + "_Reverse", position4, -vector2, magnitude2);
        }

        private void UpdateLegContactShadow(string key, Vector3 origin, Vector3 direction, float length)
        {
            Collider collider = null;
            Vector3 a = Vector3.zero;
            Vector3 vector = Vector3.up;
            int num = (length > 0.0001f) ? Physics.RaycastNonAlloc(origin, direction / length, this.surfaceHits, length, -5, QueryTriggerInteraction.Ignore) : 0;
            float num2 = float.PositiveInfinity;
            for (int i = 0; i < num; i++)
            {
                RaycastHit raycastHit = this.surfaceHits[i];
                bool flag = raycastHit.distance >= num2 || !this.IsContactShadowCollider(raycastHit.collider);
                if (!flag)
                {
                    num2 = raycastHit.distance;
                    collider = raycastHit.collider;
                    a = raycastHit.point;
                    vector = ((raycastHit.normal.sqrMagnitude > 0.0001f) ? raycastHit.normal.normalized : Vector3.up);
                }
            }
            bool flag2 = collider == null;
            if (flag2)
            {
                this.RemoveContactShadow(key);
            }
            else
            {
                Unseen.ContactShadow contactShadow;
                bool flag3 = !this.contactShadows.TryGetValue(key, out contactShadow) || contactShadow == null || contactShadow.renderer == null;
                if (flag3)
                {
                    GameObject gameObject = new GameObject("UnseenLegContactShadow");
                    gameObject.transform.SetParent(collider.transform, true);
                    gameObject.layer = 0;
                    SpriteRenderer spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
                    spriteRenderer.sprite = Unseen.GetContactShadow();
                    spriteRenderer.color = new Color(this.creatureColor.r, this.creatureColor.g, this.creatureColor.b, 0f);
                    spriteRenderer.sharedMaterial = Unseen.GetNormalMaterial();
                    spriteRenderer.sortingOrder = 32758;
                    contactShadow = new Unseen.ContactShadow
                    {
                        renderer = spriteRenderer
                    };
                    this.contactShadows[key] = contactShadow;
                }
                contactShadow.collider = collider;
                contactShadow.targetAlpha = 1f;
                bool flag4 = contactShadow.renderer.transform.parent != collider.transform;
                if (flag4)
                {
                    contactShadow.renderer.transform.SetParent(collider.transform, true);
                }
                contactShadow.renderer.transform.position = a + vector * 0.01f;
                contactShadow.renderer.transform.rotation = Quaternion.LookRotation(vector);
                contactShadow.renderer.transform.localScale = Vector3.one * this.contactShadowScale;
            }
        }

        private bool IsContactShadowCollider(Collider collider)
        {
            bool flag = collider == null || collider.transform.IsChildOf(base.transform);
            bool result;
            if (flag)
            {
                result = false;
            }
            else
            {
                int layer = collider.gameObject.layer;
                result = (layer == LayerMask.NameToLayer("Default") || layer == LayerMask.NameToLayer("World"));
            }
            return result;
        }

        private void RemoveContactShadow(string key)
        {
            Unseen.ContactShadow contactShadow;
            bool flag = !this.contactShadows.TryGetValue(key, out contactShadow);
            if (!flag)
            {
                bool flag2 = contactShadow != null;
                if (flag2)
                {
                    contactShadow.targetAlpha = 0f;
                }
            }
        }

        private void UpdateContactShadowFades()
        {
            List<string> list = this.expiredShadowKeys;
            list.Clear();
            foreach (KeyValuePair<string, Unseen.ContactShadow> keyValuePair in this.contactShadows)
            {
                Unseen.ContactShadow value = keyValuePair.Value;
                bool flag = value == null || value.renderer == null;
                if (flag)
                {
                    list.Add(keyValuePair.Key);
                }
                else
                {
                    value.alpha = Mathf.MoveTowards(value.alpha, value.targetAlpha, this.contactShadowFadeSpeed * Time.deltaTime);
                    value.renderer.color = new Color(this.creatureColor.r, this.creatureColor.g, this.creatureColor.b, value.alpha);
                    bool flag2 = value.targetAlpha <= 0f && value.alpha <= 0f;
                    if (flag2)
                    {
                        UnityEngine.Object.Destroy(value.renderer.gameObject);
                        list.Add(keyValuePair.Key);
                    }
                }
            }
            foreach (string key in list)
            {
                this.contactShadows.Remove(key);
            }
        }

        private GameObject CreatePrimitive(string objectName, PrimitiveType type)
        {
            GameObject gameObject = GameObject.CreatePrimitive(type);
            gameObject.name = objectName;
            gameObject.layer = 31;
            Collider component = gameObject.GetComponent<Collider>();
            bool flag = component != null;
            if (flag)
            {
                UnityEngine.Object.Destroy(component);
            }
            Renderer component2 = gameObject.GetComponent<Renderer>();
            bool flag2 = component2 != null;
            if (flag2)
            {
                component2.sharedMaterial = Unseen.GetOverlayMaterial();
                component2.sortingOrder = 32759;
                this.unseenRenderers.Add(component2);
            }
            gameObject.transform.SetParent(base.transform, true);
            return gameObject;
        }

        private GameObject CreateTip(string objectName)
        {
            GameObject gameObject = new GameObject(objectName);
            gameObject.layer = 31;
            MeshFilter meshFilter = gameObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = Unseen.CreateTipMesh();
            MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = Unseen.GetOverlayMaterial();
            meshRenderer.sortingOrder = 32759;
            this.unseenRenderers.Add(meshRenderer);
            gameObject.transform.SetParent(base.transform, true);
            return gameObject;
        }

        private void ConfigureOverlayCamera()
        {
            bool flag = this.playerCamera == null;
            if (!flag)
            {
                bool toggleState = EndlessConfiguration.CustomUIManager.GetToggleState("always_see_unseen");
                this.ApplyRenderMode(toggleState);
                bool flag2 = this.maskedCamera != this.playerCamera;
                if (flag2)
                {
                    this.RestoreMaskedCamera();
                    this.maskedCamera = this.playerCamera;
                    Unseen.CameraMaskState cameraMaskState;
                    bool flag3 = !Unseen.cameraMasks.TryGetValue(this.maskedCamera, out cameraMaskState);
                    if (flag3)
                    {
                        cameraMaskState = new Unseen.CameraMaskState
                        {
                            layers = (this.maskedCamera.cullingMask & -1073741824)
                        };
                        Unseen.cameraMasks.Add(this.maskedCamera, cameraMaskState);
                    }
                    cameraMaskState.users++;
                    this.maskedCamera.cullingMask &= 1073741823;
                }
                bool flag4 = this.eyeRenderer != null;
                if (flag4)
                {
                    bool flag5 = this.eyeCamera == null;
                    if (flag5)
                    {
                        GameObject gameObject = new GameObject("UnseenEyeCamera");
                        gameObject.transform.SetParent(base.transform, false);
                        this.eyeCamera = gameObject.AddComponent<Camera>();
                    }
                    this.eyeCamera.CopyFrom(this.playerCamera);
                    this.eyeCamera.transform.position = this.playerCamera.transform.position;
                    this.eyeCamera.transform.rotation = this.playerCamera.transform.rotation;
                    this.eyeCamera.cullingMask = 1073741824;
                    this.eyeCamera.clearFlags = CameraClearFlags.Depth;
                    this.eyeCamera.depth = this.playerCamera.depth + 101f;
                    this.eyeCamera.useOcclusionCulling = false;
                    this.eyeCamera.enabled = true;
                }
                else
                {
                    bool flag6 = this.eyeCamera != null;
                    if (flag6)
                    {
                        this.eyeCamera.enabled = false;
                    }
                }
                bool flag7 = !toggleState;
                if (flag7)
                {
                    bool flag8 = this.overlayCamera != null;
                    if (flag8)
                    {
                        this.overlayCamera.enabled = false;
                    }
                }
                else
                {
                    bool flag9 = this.overlayCamera == null;
                    if (flag9)
                    {
                        GameObject gameObject2 = new GameObject("UnseenOverlayCamera");
                        gameObject2.transform.SetParent(base.transform, false);
                        this.overlayCamera = gameObject2.AddComponent<Camera>();
                    }
                    this.overlayCamera.CopyFrom(this.playerCamera);
                    this.overlayCamera.transform.position = this.playerCamera.transform.position;
                    this.overlayCamera.transform.rotation = this.playerCamera.transform.rotation;
                    this.overlayCamera.cullingMask = int.MinValue;
                    this.overlayCamera.clearFlags = CameraClearFlags.Depth;
                    this.overlayCamera.depth = this.playerCamera.depth + 100f;
                    this.overlayCamera.useOcclusionCulling = false;
                    this.overlayCamera.enabled = true;
                }
            }
        }

        private void ApplyRenderMode(bool renderOnTop)
        {
            Material creatureMaterial = this.GetCreatureMaterial(renderOnTop);
            foreach (Renderer renderer in this.unseenRenderers)
            {
                bool flag = renderer == null;
                if (!flag)
                {
                    renderer.gameObject.layer = (renderOnTop ? 31 : 0);
                    renderer.sharedMaterial = creatureMaterial;
                    renderer.sortingOrder = 32759;
                }
            }
        }

        private Material GetCreatureMaterial(bool renderOnTop)
        {
            Material material = renderOnTop ? this.coloredOverlayMaterial : this.coloredNormalMaterial;
            bool flag = material != null;
            Material result;
            if (flag)
            {
                result = material;
            }
            else
            {
                material = new Material(renderOnTop ? Unseen.GetOverlayMaterial() : Unseen.GetNormalMaterial());
                bool flag2 = material.HasProperty("_Color");
                if (flag2)
                {
                    material.SetColor("_Color", this.creatureColor);
                }
                if (renderOnTop)
                {
                    this.coloredOverlayMaterial = material;
                }
                else
                {
                    this.coloredNormalMaterial = material;
                }
                result = material;
            }
            return result;
        }

        private void RestoreMaskedCamera()
        {
            bool flag = this.maskedCamera == null;
            if (!flag)
            {
                Unseen.CameraMaskState cameraMaskState;
                bool flag2;
                if (Unseen.cameraMasks.TryGetValue(this.maskedCamera, out cameraMaskState))
                {
                    Unseen.CameraMaskState cameraMaskState2 = cameraMaskState;
                    int num = cameraMaskState2.users - 1;
                    cameraMaskState2.users = num;
                    flag2 = (num <= 0);
                }
                else
                {
                    flag2 = false;
                }
                bool flag3 = flag2;
                if (flag3)
                {
                    bool flag4 = this.maskedCamera != null;
                    if (flag4)
                    {
                        this.maskedCamera.cullingMask &= 1073741823;
                        this.maskedCamera.cullingMask |= cameraMaskState.layers;
                    }
                    Unseen.cameraMasks.Remove(this.maskedCamera);
                }
                this.maskedCamera = null;
            }
        }

        private void TryUseHeldHandItem()
        {
            bool flag = this.heldHandItem == null;
            if (!flag)
            {
                this.heldHandItem.UpdateVisual();
                bool flag2 = Time.time < this.nextHandItemUseTime;
                if (!flag2)
                {
                    float value = this.helpful ? this.friendlyHandItemUseChance : this.hostileHandItemUseChance;
                    float b = this.helpful ? this.friendlyHandItemUseInterval : this.hostileHandItemUseInterval;
                    this.nextHandItemUseTime = Time.time + Mathf.Max(0.05f, b);
                    bool flag3 = UnityEngine.Random.value <= Mathf.Clamp01(value);
                    if (flag3)
                    {
                        this.heldHandItem.TryUse();
                    }
                }
            }
        }

        public Item_Object GetUnseenHeldItemObject()
        {
            return this.carriedItem;
        }

        public void DestroyUnseenHeldHandItem(Item item)
        {
            bool flag = item == null || this.carriedItem == null || this.carriedItem.itemData != item;
            if (!flag)
            {
                Item_Object item_Object = this.carriedItem;
                item.SetUsed(true);
                this.ReleaseHeldItem();
                this.carriedRigidbody = null;
                this.carriedItem = null;
                this.carriedLeg = null;
                this.ReleaseUnseenHandItem();
                item.Destroy(false);
                bool flag2 = item_Object != null;
                if (flag2)
                {
                    UnityEngine.Object.Destroy(item_Object.gameObject);
                }
                this.ClearPlayerInteraction();
                this.behaviorState = Unseen.BehaviorState.Moving;
            }
        }

        private void ReleaseUnseenHandItem()
        {
            bool flag = this.heldHandItem != null;
            if (flag)
            {
                this.heldHandItem.Release();
            }
            this.heldHandItem = null;
        }

        private float GetUnseenHandItemSpeedMultiplier()
        {
            bool flag = Time.time >= this.handItemSpeedUntil;
            float result;
            if (flag)
            {
                this.handItemSpeedMultiplier = 1f;
                result = 1f;
            }
            else
            {
                result = Mathf.Max(0.1f, this.handItemSpeedMultiplier);
            }
            return result;
        }

        public Vector3 GetUnseenHandItemOrigin()
        {
            return (this.carriedRigidbody != null) ? this.carriedRigidbody.position : base.transform.position;
        }

        public Vector3 GetUnseenHandItemDirection()
        {
            bool flag = this.body != null && this.body.velocity.sqrMagnitude > 0.01f;
            Vector3 result;
            if (flag)
            {
                result = this.body.velocity.normalized;
            }
            else
            {
                result = base.transform.forward;
            }
            return result;
        }

        public GameEntity GetUnseenHandItemTarget(float range)
        {
            ENT_Player player = ENT_Player.GetPlayer();
            bool flag = !this.helpful && player != null && Vector3.Distance(this.GetUnseenHandItemOrigin(), player.transform.position) <= range;
            GameEntity result;
            if (flag)
            {
                result = player;
            }
            else
            {
                Collider[] array = Physics.OverlapSphere(this.GetUnseenHandItemOrigin(), range, -5, QueryTriggerInteraction.Ignore);
                GameEntity gameEntity = null;
                float num = float.PositiveInfinity;
                foreach (Collider collider in array)
                {
                    bool flag2 = collider == null || collider.transform.IsChildOf(base.transform);
                    if (!flag2)
                    {
                        GameEntity componentInParent = collider.GetComponentInParent<GameEntity>();
                        bool flag3 = componentInParent == null || componentInParent is ENT_Player || componentInParent.dead || (!(componentInParent is Denizen) && !(componentInParent is CL_Prop));
                        if (!flag3)
                        {
                            float sqrMagnitude = (componentInParent.transform.position - this.GetUnseenHandItemOrigin()).sqrMagnitude;
                            bool flag4 = sqrMagnitude >= num;
                            if (!flag4)
                            {
                                gameEntity = componentInParent;
                                num = sqrMagnitude;
                            }
                        }
                    }
                }
                result = gameEntity;
            }
            return result;
        }

        public void ApplyUnseenHandItemRecoil(Vector3 force)
        {
            bool flag = this.body != null && force.sqrMagnitude > 0.0001f;
            if (flag)
            {
                this.body.AddForce(force, ForceMode.VelocityChange);
            }
        }

        public bool TryUnseenHandItemBlink(Vector3 direction, float distance, LayerMask mask)
        {
            bool flag = this.body == null || direction.sqrMagnitude < 0.0001f;
            bool result;
            if (flag)
            {
                result = false;
            }
            else
            {
                Vector3 position = base.transform.position;
                float num = Mathf.Max(1f, distance);
                int layerMask = (mask.value == 0) ? -5 : mask.value;
                RaycastHit raycastHit;
                bool flag2 = Physics.SphereCast(position, this.bodyCollisionRadius * 0.5f, direction, out raycastHit, num, layerMask, QueryTriggerInteraction.Ignore);
                if (flag2)
                {
                    num = Mathf.Max(0.5f, raycastHit.distance - this.bodyCollisionRadius);
                }
                Vector3 position2 = position + direction * num;
                this.body.position = position2;
                this.body.velocity *= 0.25f;
                base.transform.position = position2;
                result = true;
            }
            return result;
        }

        public bool TryUnseenHandItemGrapple(Vector3 direction, float distance, float force, LayerMask mask)
        {
            bool flag = this.body == null || direction.sqrMagnitude < 0.0001f;
            bool result;
            if (flag)
            {
                result = false;
            }
            else
            {
                int layerMask = (mask.value == 0) ? -5 : mask.value;
                RaycastHit raycastHit;
                bool flag2 = !Physics.SphereCast(this.GetUnseenHandItemOrigin(), 0.1f, direction, out raycastHit, Mathf.Max(1f, distance), layerMask, QueryTriggerInteraction.Ignore);
                if (flag2)
                {
                    result = false;
                }
                else
                {
                    this.body.AddForce(direction.normalized * Mathf.Max(1f, force) * 12f, ForceMode.VelocityChange);
                    result = true;
                }
            }
            return result;
        }

        public void ApplyUnseenHandItemConsumable(BuffContainer primary, List<BuffContainer> secondary, float potency)
        {
            float num = 3f;
            float b = 1f + Mathf.Clamp(potency, 0f, 12f) * 0.08f;
            bool flag = false;
            Unseen.ApplyUnseenHandItemBuff(primary, ref num, ref b, ref flag);
            bool flag2 = secondary != null;
            if (flag2)
            {
                foreach (BuffContainer buff in secondary)
                {
                    Unseen.ApplyUnseenHandItemBuff(buff, ref num, ref b, ref flag);
                }
            }
            this.handItemSpeedMultiplier = Mathf.Max(this.handItemSpeedMultiplier, b);
            this.handItemSpeedUntil = Mathf.Max(this.handItemSpeedUntil, Time.time + num);
            bool flag3 = flag || !this.helpful;
            if (flag3)
            {
                this.handItemAggressionUntil = Mathf.Max(this.handItemAggressionUntil, Time.time + num);
            }
        }

        private static void ApplyUnseenHandItemBuff(BuffContainer buff, ref float duration, ref float multiplier, ref bool aggressive)
        {
            bool flag = buff == null;
            if (!flag)
            {
                duration = Mathf.Max(duration, (buff.loseOverTime && buff.loseRate > 0f) ? (1f / buff.loseRate) : 6f);
                bool flag2 = buff.buffs == null;
                if (!flag2)
                {
                    foreach (BuffContainer.Buff buff2 in buff.buffs)
                    {
                        bool flag3 = buff2 == null;
                        if (!flag3)
                        {
                            string haystack = buff2.id ?? string.Empty;
                            float num = Mathf.Abs(buff2.maxAmount * buff.multiplier);
                            bool flag4 = haystack.ContainsInsensitive("jump") || haystack.ContainsInsensitive("speed") || haystack.ContainsInsensitive("move");
                            if (flag4)
                            {
                                multiplier += num;
                            }
                            bool flag5 = haystack.ContainsInsensitive("roid") || haystack.ContainsInsensitive("damage");
                            if (flag5)
                            {
                                aggressive = true;
                            }
                        }
                    }
                }
            }
        }

        private void OnDestroy()
        {
            this.RestoreMaskedCamera();
            this.ReleaseHeldItem();
            foreach (KeyValuePair<string, Unseen.ContactShadow> keyValuePair in this.contactShadows)
            {
                bool flag = keyValuePair.Value != null && keyValuePair.Value.renderer != null;
                if (flag)
                {
                    UnityEngine.Object.Destroy(keyValuePair.Value.renderer.gameObject);
                }
            }
            this.contactShadows.Clear();
            bool flag2 = this.coloredOverlayMaterial != null;
            if (flag2)
            {
                UnityEngine.Object.Destroy(this.coloredOverlayMaterial);
            }
            bool flag3 = this.coloredNormalMaterial != null;
            if (flag3)
            {
                UnityEngine.Object.Destroy(this.coloredNormalMaterial);
            }
        }

        private void OnDisable()
        {
            this.RestoreMaskedCamera();
            bool flag = this.overlayCamera != null;
            if (flag)
            {
                this.overlayCamera.enabled = false;
            }
            bool flag2 = this.eyeCamera != null;
            if (flag2)
            {
                this.eyeCamera.enabled = false;
            }
            this.ReleaseHeldItem();
            this.manipulatedRigidbody = null;
            this.manipulationLeg = null;
            this.carriedRigidbody = null;
            this.carriedItem = null;
            this.carriedLeg = null;
            this.ReleaseUnseenHandItem();
            this.ClearTap();
            bool flag3 = this.consumedRigidbody != null;
            if (flag3)
            {
                Item_Object componentInParent = this.consumedRigidbody.GetComponentInParent<Item_Object>();
                bool flag4 = componentInParent == null || componentInParent.itemData == null || !componentInParent.itemData.inBag;
                if (flag4)
                {
                    this.consumedRigidbody.isKinematic = this.consumedWasKinematic;
                }
            }
            this.ReleaseBodyOwnership(this.consumedRigidbody);
            this.consumedRigidbody = null;
        }

        public static Mesh CreateTipMesh()
        {
            Mesh mesh = new Mesh();
            mesh.vertices = new Vector3[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(0f, 0f, 1f)
            };
            mesh.triangles = new int[]
            {
                0,
                1,
                4,
                1,
                2,
                4,
                2,
                3,
                4,
                3,
                0,
                4,
                3,
                2,
                1,
                3,
                1,
                0
            };
            mesh.RecalculateNormals();
            return mesh;
        }

        public static Material GetOverlayMaterial()
        {
            bool flag = Unseen.overlayMaterial != null;
            Material result;
            if (flag)
            {
                result = Unseen.overlayMaterial;
            }
            else
            {
                Shader shader = Shader.Find("UI/Default");
                bool flag2 = shader == null;
                if (flag2)
                {
                    shader = Shader.Find("Sprites/Default");
                }
                bool flag3 = shader == null;
                if (flag3)
                {
                    shader = Shader.Find("Unlit/Color");
                }
                Unseen.overlayMaterial = new Material(shader);
                bool flag4 = Unseen.overlayMaterial.HasProperty("_Color");
                if (flag4)
                {
                    Unseen.overlayMaterial.SetColor("_Color", Color.black);
                }
                bool flag5 = Unseen.overlayMaterial.HasProperty("_ZTest");
                if (flag5)
                {
                    Unseen.overlayMaterial.SetInt("_ZTest", 8);
                }
                Unseen.overlayMaterial.renderQueue = 5000;
                result = Unseen.overlayMaterial;
            }
            return result;
        }

        public static Material GetEyeMaterial()
        {
            bool flag = Unseen.eyeMaterial != null;
            Material result;
            if (flag)
            {
                result = Unseen.eyeMaterial;
            }
            else
            {
                Shader shader = Shader.Find("UI/Default");
                bool flag2 = shader == null;
                if (flag2)
                {
                    shader = Shader.Find("Sprites/Default");
                }
                bool flag3 = shader == null;
                if (flag3)
                {
                    shader = Shader.Find("Unlit/Color");
                }
                Unseen.eyeMaterial = new Material(shader);
                bool flag4 = Unseen.eyeMaterial.HasProperty("_Color");
                if (flag4)
                {
                    Unseen.eyeMaterial.SetColor("_Color", Color.white);
                }
                bool flag5 = Unseen.eyeMaterial.HasProperty("_ZTest");
                if (flag5)
                {
                    Unseen.eyeMaterial.SetInt("_ZTest", 8);
                }
                Unseen.eyeMaterial.renderQueue = 5001;
                result = Unseen.eyeMaterial;
            }
            return result;
        }

        public static Material GetNormalMaterial()
        {
            bool flag = Unseen.normalMaterial != null;
            Material result;
            if (flag)
            {
                result = Unseen.normalMaterial;
            }
            else
            {
                Shader shader = Shader.Find("Sprites/Default");
                bool flag2 = shader == null;
                if (flag2)
                {
                    shader = Shader.Find("Unlit/Color");
                }
                Unseen.normalMaterial = new Material(shader);
                bool flag3 = Unseen.normalMaterial.HasProperty("_Color");
                if (flag3)
                {
                    Unseen.normalMaterial.SetColor("_Color", Color.black);
                }
                result = Unseen.normalMaterial;
            }
            return result;
        }

        public static Sprite GetContactShadow()
        {
            bool flag = Unseen.contactShadowSprite != null;
            Sprite result;
            if (flag)
            {
                result = Unseen.contactShadowSprite;
            }
            else
            {
                float num = 15.5f;
                float num2 = num;
                Texture2D texture2D = new Texture2D(32, 32, TextureFormat.RGBA32, false);
                texture2D.filterMode = FilterMode.Point;
                texture2D.wrapMode = TextureWrapMode.Clamp;
                texture2D.anisoLevel = 0;
                for (int i = 0; i < 32; i++)
                {
                    for (int j = 0; j < 32; j++)
                    {
                        float num3 = (float)j - num;
                        float num4 = (float)i - num;
                        float a = (num3 * num3 + num4 * num4 <= num2 * num2) ? 1f : 0f;
                        texture2D.SetPixel(j, i, new Color(0f, 0f, 0f, a));
                    }
                }
                texture2D.Apply(false, true);
                Unseen.contactShadowSprite = Sprite.Create(texture2D, new Rect(0f, 0f, 32f, 32f), new Vector2(0.5f, 0.5f), 32f);
                result = Unseen.contactShadowSprite;
            }
            return result;
        }

        private Sprite GetFallbackShadowFrame(int frame)
        {
            int num = Mathf.Max(1, this.shadowGenerationFrameCount);
            int num2 = Mathf.Clamp(this.shadowTextureSize, 16, 256);
            bool flag = Unseen.fallbackShadowFrames == null;
            if (flag)
            {
                Unseen.fallbackShadowFrames = new Sprite[num];
                float num3 = (float)(num2 - 1) * 0.5f;
                float num4 = num3 * Mathf.Clamp(this.shadowCoreRadiusFraction, 0.1f, 0.9f);
                float num5 = num4 * Mathf.Max(this.shadowTendrilStartMinimum, this.shadowTendrilStartMaximum);
                float num6 = num3 * Mathf.Max(this.shadowTendrilLengthMinimum, this.shadowTendrilLengthMaximum);
                float num7 = Mathf.Max(this.shadowTendrilThickness, this.shadowTendrilTipThickness) + (float)Mathf.Clamp(this.shadowCellularPasses, 0, 8) + Mathf.Max(0f, this.shadowEdgeFadePixels) + 3f;
                int num8 = Mathf.Clamp(Mathf.CeilToInt(num5 + num6 + num7), num2 / 2, 512);
                int num9 = num8 * 2 + 1;
                float[][] array = new float[num][];
                int num10 = num9;
                int num11 = num9;
                int num12 = -1;
                int num13 = -1;
                for (int i = 0; i < num; i++)
                {
                    float[] shadowShape = this.GetShadowShape(num9, num3, i);
                    array[i] = shadowShape;
                    for (int j = 0; j < num9; j++)
                    {
                        for (int k = 0; k < num9; k++)
                        {
                            bool flag2 = shadowShape[j * num9 + k] < this.shadowShapeThreshold;
                            if (!flag2)
                            {
                                bool flag3 = k < num10;
                                if (flag3)
                                {
                                    num10 = k;
                                }
                                bool flag4 = k > num12;
                                if (flag4)
                                {
                                    num12 = k;
                                }
                                bool flag5 = j < num11;
                                if (flag5)
                                {
                                    num11 = j;
                                }
                                bool flag6 = j > num13;
                                if (flag6)
                                {
                                    num13 = j;
                                }
                            }
                        }
                    }
                }
                bool flag7 = num12 < num10 || num13 < num11;
                if (flag7)
                {
                    num10 = num8;
                    num12 = num8;
                    num11 = num8;
                    num13 = num8;
                }
                int num14 = Mathf.Max(0, this.shadowBoundsPadding);
                num10 = Mathf.Max(0, num10 - num14);
                num11 = Mathf.Max(0, num11 - num14);
                num12 = Mathf.Min(num9 - 1, num12 + num14);
                num13 = Mathf.Min(num9 - 1, num13 + num14);
                int num15 = num12 - num10 + 1;
                int num16 = num13 - num11 + 1;
                Vector2 pivot = new Vector2(((float)(num8 - num10) + 0.5f) / (float)num15, ((float)(num8 - num11) + 0.5f) / (float)num16);
                for (int l = 0; l < num; l++)
                {
                    Texture2D texture2D = new Texture2D(num15, num16, TextureFormat.RGBA32, false);
                    texture2D.filterMode = FilterMode.Point;
                    texture2D.wrapMode = TextureWrapMode.Clamp;
                    texture2D.anisoLevel = 0;
                    float[] shape = array[l];
                    for (int m = 0; m < num16; m++)
                    {
                        for (int n = 0; n < num15; n++)
                        {
                            int x = num10 + n;
                            int y = num11 + m;
                            float shadowEdgeAlpha = this.GetShadowEdgeAlpha(shape, num9, x, y);
                            texture2D.SetPixel(n, m, new Color(0f, 0f, 0f, shadowEdgeAlpha));
                        }
                    }
                    texture2D.Apply(false, true);
                    Unseen.fallbackShadowFrames[l] = Sprite.Create(texture2D, new Rect(0f, 0f, (float)num15, (float)num16), pivot, (float)num2);
                }
                Unseen.fallbackShadow = Unseen.fallbackShadowFrames[0];
            }
            return Unseen.fallbackShadowFrames[Mathf.Abs(frame) % num];
        }

        private float[] GetShadowShape(int size, float radius, int frame)
        {
            float[] array = new float[size * size];
            float num = (float)(size - 1) * 0.5f;
            float num2 = radius * Mathf.Clamp(this.shadowCoreRadiusFraction, 0.1f, 0.9f);
            for (int i = 0; i < size; i++)
            {
                for (int j = 0; j < size; j++)
                {
                    float num3 = (float)j - num;
                    float num4 = (float)i - num;
                    bool flag = num3 * num3 + num4 * num4 <= num2 * num2;
                    if (flag)
                    {
                        array[i * size + j] = 1f;
                    }
                }
            }
            int num5 = Mathf.Max(0, this.shadowTendrilCountMinimum);
            int num6 = Mathf.Max(num5, this.shadowTendrilCountMaximum);
            int num7 = Mathf.Clamp(num5 + Mathf.FloorToInt(Unseen.GetShadowNoise(71, 19, frame) * (float)(num6 - num5 + 1)), num5, num6);
            for (int k = 0; k < num7; k++)
            {
                float num8 = Unseen.GetShadowNoise(k * 101 + 3, 17, frame) * 3.1415927f * 2f;
                float num9 = num2 * Mathf.Lerp(this.shadowTendrilStartMinimum, this.shadowTendrilStartMaximum, Unseen.GetShadowNoise(k * 47 + 11, 29, frame));
                float num10 = radius * Mathf.Lerp(this.shadowTendrilLengthMinimum, this.shadowTendrilLengthMaximum, Unseen.GetShadowNoise(k * 83 + 23, 43, frame));
                float num11 = Mathf.Lerp(-this.shadowTendrilCurve, this.shadowTendrilCurve, Unseen.GetShadowNoise(k * 59 + 31, 61, frame));
                float num12 = Mathf.Lerp(-0.24f, 0.24f, Unseen.GetShadowNoise(k * 73 + 37, 79, frame));
                int num13 = Mathf.Max(1, Mathf.CeilToInt(num10 * 1.75f));
                for (int l = 0; l <= num13; l++)
                {
                    float num14 = (float)l / (float)Mathf.Max(1, num13);
                    float f = num8 + Mathf.Sin(num14 * 3.1415927f) * num11 + Mathf.Sin(num14 * 3.1415927f * 3f) * num12;
                    float num15 = num9 + num10 * num14;
                    int centerX = Mathf.RoundToInt(num + Mathf.Cos(f) * num15);
                    int centerY = Mathf.RoundToInt(num + Mathf.Sin(f) * num15);
                    float thickness = Mathf.Lerp(this.shadowTendrilThickness, this.shadowTendrilTipThickness, num14);
                    Unseen.PaintShadowWisp(array, size, centerX, centerY, thickness);
                }
            }
            int num16 = Mathf.Clamp(this.shadowCellularPasses, 0, 8);
            for (int m = 0; m < num16; m++)
            {
                array = this.SmoothShadowWisps(array, size);
            }
            return array;
        }

        private static SegmentMeshDefinition CreateSegmentMeshDefinition(int pointCount, float wobble, float baseThickness)
        {
            Vector3[] points = new Vector3[pointCount];
            wobble = Mathf.Max(0.001f, wobble);
            for (int index = 0; index < pointCount; index++)
            {
                float progress = index / (float)(pointCount - 1);
                float endFade = Mathf.Sin(progress * Mathf.PI);
                points[index] = new Vector3(Mathf.Lerp(-0.5f, 0.5f, progress), UnityEngine.Random.Range(-wobble, wobble) * endFade, UnityEngine.Random.Range(-wobble, wobble) * endFade);
            }
            int first = 0;
            int second = 1;
            float longestDistance = 0f;
            for (int a = 0; a < points.Length; a++) for (int b = a + 1; b < points.Length; b++)
                {
                    float distance = Vector3.Distance(points[a], points[b]);
                    if (distance <= longestDistance) continue;
                    longestDistance = distance;
                    first = a;
                    second = b;
                }
            Vector3 axis = (points[second] - points[first]).normalized;
            Vector3 reference = Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.9f ? Vector3.forward : Vector3.up;
            Vector3 up = Vector3.Cross(reference, axis).normalized;
            Vector3 forward = Vector3.Cross(axis, up).normalized;
            Vector3 center = (points[first] + points[second]) * 0.5f;
            Vector3[] vertices = new Vector3[pointCount * 4];
            baseThickness = Mathf.Max(0.001f, baseThickness);
            for (int index = 0; index < pointCount; index++)
            {
                Vector3 offset = points[index] - center;
                Vector3 localPoint = new Vector3(Vector3.Dot(offset, axis), Vector3.Dot(offset, up), Vector3.Dot(offset, forward));
                float progress = index / (float)(pointCount - 1);
                float thickness = baseThickness * Mathf.Lerp(0.45f, 1f, Mathf.Sin(progress * Mathf.PI)) * UnityEngine.Random.Range(0.7f, 1.25f);
                float depth = thickness * UnityEngine.Random.Range(0.7f, 1.25f);
                int vertex = index * 4;
                vertices[vertex] = localPoint + Vector3.up * thickness + Vector3.forward * depth;
                vertices[vertex + 1] = localPoint + Vector3.up * thickness - Vector3.forward * depth;
                vertices[vertex + 2] = localPoint - Vector3.up * thickness - Vector3.forward * depth;
                vertices[vertex + 3] = localPoint - Vector3.up * thickness + Vector3.forward * depth;
            }
            int[] triangles = new int[(pointCount - 1) * 24 + 12];
            int triangleIndex = 0;
            for (int index = 0; index < pointCount - 1; index++)
            {
                int start = index * 4;
                int end = start + 4;
                for (int side = 0; side < 4; side++)
                {
                    int next = (side + 1) % 4;
                    triangles[triangleIndex++] = start + side;
                    triangles[triangleIndex++] = end + side;
                    triangles[triangleIndex++] = end + next;
                    triangles[triangleIndex++] = start + side;
                    triangles[triangleIndex++] = end + next;
                    triangles[triangleIndex++] = start + next;
                }
            }
            triangles[triangleIndex++] = 0;
            triangles[triangleIndex++] = 2;
            triangles[triangleIndex++] = 1;
            triangles[triangleIndex++] = 0;
            triangles[triangleIndex++] = 3;
            triangles[triangleIndex++] = 2;
            int last = (pointCount - 1) * 4;
            triangles[triangleIndex++] = last;
            triangles[triangleIndex++] = last + 1;
            triangles[triangleIndex++] = last + 2;
            triangles[triangleIndex++] = last;
            triangles[triangleIndex++] = last + 2;
            triangles[triangleIndex++] = last + 3;
            Mesh mesh = new Mesh();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return new SegmentMeshDefinition { mesh = mesh, longestDistance = Mathf.Max(0.0001f, longestDistance) };
        }
        private static void PaintShadowWisp(float[] shape, int size, int centerX, int centerY, float thickness)
        {
            int num = Mathf.Max(1, Mathf.CeilToInt(thickness));
            float num2 = Mathf.Max(0.25f, thickness * thickness);
            for (int i = centerY - num; i <= centerY + num; i++)
            {
                for (int j = centerX - num; j <= centerX + num; j++)
                {
                    bool flag = j < 0 || i < 0 || j >= size || i >= size;
                    if (!flag)
                    {
                        float num3 = (float)(j - centerX);
                        float num4 = (float)(i - centerY);
                        bool flag2 = num3 * num3 + num4 * num4 > num2;
                        if (!flag2)
                        {
                            int num5 = i * size + j;
                            shape[num5] = 1f;
                        }
                    }
                }
            }
        }

        private float[] SmoothShadowWisps(float[] shape, int size)
        {
            float[] array = new float[shape.Length];
            int num = Mathf.Clamp(this.shadowCellularGrowthNeighbors, 1, 8);
            for (int i = 0; i < size; i++)
            {
                for (int j = 0; j < size; j++)
                {
                    int num2 = i * size + j;
                    int num3 = 0;
                    for (int k = Mathf.Max(0, i - 1); k <= Mathf.Min(size - 1, i + 1); k++)
                    {
                        for (int l = Mathf.Max(0, j - 1); l <= Mathf.Min(size - 1, j + 1); l++)
                        {
                            bool flag = l == j && k == i;
                            if (!flag)
                            {
                                bool flag2 = shape[k * size + l] >= this.shadowShapeThreshold;
                                if (flag2)
                                {
                                    num3++;
                                }
                            }
                        }
                    }
                    bool flag3 = shape[num2] >= this.shadowShapeThreshold;
                    array[num2] = ((flag3 || num3 >= num) ? 1f : 0f);
                }
            }
            return array;
        }

        private float GetShadowEdgeAlpha(float[] shape, int size, int x, int y)
        {
            int num = y * size + x;
            bool flag = shape[num] < this.shadowShapeThreshold;
            float result;
            if (flag)
            {
                result = 0f;
            }
            else
            {
                float num2 = Mathf.Max(0f, this.shadowEdgeFadePixels);
                bool flag2 = num2 <= 0f;
                if (flag2)
                {
                    result = 1f;
                }
                else
                {
                    int num3 = Mathf.Max(1, Mathf.CeilToInt(num2));
                    float num4 = Mathf.Min(num2 + 1f, Mathf.Min(Mathf.Min((float)x + 1f, (float)(size - x)), Mathf.Min((float)y + 1f, (float)(size - y))));
                    for (int i = Mathf.Max(0, y - num3); i <= Mathf.Min(size - 1, y + num3); i++)
                    {
                        for (int j = Mathf.Max(0, x - num3); j <= Mathf.Min(size - 1, x + num3); j++)
                        {
                            bool flag3 = shape[i * size + j] >= this.shadowShapeThreshold;
                            if (!flag3)
                            {
                                float num5 = (float)(j - x);
                                float num6 = (float)(i - y);
                                float num7 = Mathf.Sqrt(num5 * num5 + num6 * num6);
                                bool flag4 = num7 < num4;
                                if (flag4)
                                {
                                    num4 = num7;
                                }
                            }
                        }
                    }
                    bool flag5 = num4 > num2;
                    if (flag5)
                    {
                        result = 1f;
                    }
                    else
                    {
                        float a = Mathf.Clamp01(this.shadowEdgeMinimumAlpha);
                        result = Mathf.Lerp(a, 1f, Mathf.Clamp01(num4 / num2));
                    }
                }
            }
            return result;
        }

        private static float GetShadowNoise(int x, int y, int frame)
        {
            uint num = (uint)(x * 1973 + y * 9277 + frame * 26699 + 911);
            num = (num << 13 ^ num);
            num = num * (num * num * 15731U + 789221U) + 1376312589U;
            return (num & 65535U) / 65535f;
        }

        private const int RenderOrder = 32759;

        private const float SegmentWidth = 0.1f;

        private const float LegTotalLength = 14f;

        private const int OverlayLayer = 31;

        private const int EyeOverlayLayer = 30;

        public float walkStepDuration = 0.1f;

        public float walkStepInterval = 0.28f;

        public int walkSupportBalanceTolerance = 1;

        public int maximumConcurrentWalkingLegs = 1;

        public float tapStepDuration = 0.1f;

        public float tapInterval = 0.1f;

        public int minimumTaps = 3;

        public int maximumTaps = 7;

        public float tapRetractFraction = 0.82f;

        public float playerInspectionChance = 0.5f;

        public float playerTapStaminaDamage = 1f;

        public float playerLegTapDamage = 1f;

        public float hostilePlayerTapRange = 8f;

        public float hostileAttackApproachSpeed = 12f;

        public float hostilePlayerTapBraking = 8f;

        public float playerCameraTapDistance = 0.9f;

        public float tapContactDistance = 0.15f;

        public float rigidbodyGrabForce = 35f;

        public float rigidbodyThrowForce = 8f;

        public float recoveryRopeForce = 400f;

        public float recoveryRopeDamping = 10f;

        public float recoveryPullForce = 100f;

        public float recoveryMaximumSpeedPerLeg = 1.5f;

        public float maximumVelocity = 18f;

        public float playerBodyClearance = 3f;

        public float playerAvoidanceForce = 80f;

        public float playerInspectionDistance = 10f;

        public float inspectionItemRange = 10f;

        public float legPersonalSpace = 3f;

        public float movementTurnRate = 2f;

        public float movementAnchorReleaseDistanceFraction = 0.58f;

        public float movementAnchorReleaseBehindDot = 0.15f;

        public float kneePoleWeight = 1f;

        public float idleMovementRadius = 3f;

        public float idleMovementFrequency = 0.8f;

        public float idleMovementForce = 14f;

        public float idleMovementSpeed = 2f;

        public float bodyCollisionRadius = 0.55f;

        public float bodyContactDamage = 1f;

        public float bodyContactDamageInterval = 0.08f;

        public float visiblePlayerChargeForce = 0f;

        public float visiblePlayerChargeMaximumVelocity = 30f;

        public float attackMovementSpeedMultiplier = 105f;

        public float inspectionIdleMovementRadius = 1.5f;

        public float inspectionIdleMovementFrequency = 0.65f;

        public float inspectionIdleMovementForce = 20f;

        public float inspectionIdleMovementDamping = 3f;

        public float inspectionIdleMovementSpeed = 1.5f;

        public float entityTapDamage = 0.3f;

        public float consumeChance = 0.3f;

        public float consumePullSpeed = 50f;

        public float consumeDistance = 0.3f;

        public float contactShadowScale = 0.33f;

        public float contactShadowFadeSpeed = 5f;

        public float shadowFadeNoiseFrameRate = 7f;

        public int shadowTextureSize = 64;

        public int shadowGenerationFrameCount = 13;

        public float shadowCoreRadiusFraction = 0.5f;

        public int shadowTendrilCountMinimum = 20;

        public int shadowTendrilCountMaximum = 32;

        public float shadowTendrilStartMinimum = 0.68f;

        public float shadowTendrilStartMaximum = 0.96f;

        public float shadowTendrilLengthMinimum = 0.48f;

        public float shadowTendrilLengthMaximum = 5.05f;

        public float shadowTendrilCurve = 1f;

        public float shadowTendrilThickness = 7.5f;

        public float shadowTendrilTipThickness = 0.01f;

        public int shadowCellularPasses = 1;

        public int shadowCellularGrowthNeighbors = 2;

        public float shadowShapeThreshold = 0.22f;

        public float shadowEdgeFadePixels = 1f;

        public float shadowEdgeMinimumAlpha = 0.1f;

        public int shadowBoundsPadding = 2;

        public float friendlyCrouchDelayMinimum = 0.01f;

        public float friendlyCrouchDelayMaximum = 0.1f;

        public float friendlyCrouchTransitionSpeed = 10f;

        public float friendlyCrouchBodyDrop = 2.2f;

        public float friendlyCrouchBodyScale = 0.8f;

        public int quirkMinimumCount = 1;

        public int quirkMaximumCount = 4;

        public float quirkFrequencyMinimum = 18f;

        public float quirkFrequencyMaximum = 42f;

        public float quirkActivationChance = 0.65f;

        public float quirkSearchRange = 20f;

        public float quirkPropTapDamage = 0.5f;

        public float quirkPropTapForce = 3f;

        public float quirkPropTapInterval = 0.22f;

        public int quirkPropTapsPerLimb = 10;

        public float quirkItemPileForce = 18f;

        public float quirkEyeSpinSpeed = 900f;

        public float quirkDanceSpeed = 2.6f;

        public float quirkBounceForce = 18f;

        public float quirkGroundStepDuration = 0.6f;

        public float quirkSupportDistance = 24f;

        public float quirkButtonPressInterval = 0.45f;

        public float exitInspectionBias = 0.7f;

        public float helpfulFollowDistance = 42f;

        public float helpfulGiveDistance = 12f;

        public float helpfulWanderRadius = 12f;

        public float helpfulWanderMinimumDistance = 4f;

        public float helpfulWanderDurationMinimum = 2f;

        public float helpfulWanderDurationMaximum = 5f;

        public float itemLimbHoldTime = 0.8f;

        public float destinationDurationMinimum = 2f;

        public float destinationDurationMaximum = 8f;

        public float minimumDestinationDistance = 24f;

        public float downwardDestinationAvoidance = 0.45f;

        public float playerSightMargin = 0.35f;

        public float eyeOrbitDistance = 0.55f;

        public float playerLookTaskAbandonChance = 0.3f;

        public float attackSightHoldDuration = 0.4f;

        public float attackingVelocityDamping = 0f;

        public float hostilePatrolSpeed = 22f;

        public float hostileCatchUpSpeed = 28f;

        public float hostileCatchUpDistance = 24f;

        public float rigidbodyGrabChance = 0.8f;

        public float carryDuration = 4f;

        public float carrySpeedMultiplier = 0.25f;

        public float friendlyGiveChance = 0.5f;

        public float friendlyOfferDuration = 2f;

        public float buttonPressChance = 0.35f;

        public float playerInteractionResponseRange = 18f;

        public float playerInteractionMemoryDuration = 6f;

        public float playerInteractionApproachDistance = 4f;

        public float hostileDroppedItemCarryDuration = 3f;

        public float friendlyHandItemUseChance = 0.35f;

        public float hostileHandItemUseChance = 0.7f;

        public float friendlyHandItemUseInterval = 2.2f;

        public float hostileHandItemUseInterval = 0.7f;

        public float heldHandItemVisualPositionSmoothTime = 0.075f;

        public float heldHandItemVisualRotationSpeed = 16f;

        private readonly List<Unseen.Leg> legs = new List<Unseen.Leg>();

        private readonly List<Unseen.QuirkSchedule> quirks = new List<Unseen.QuirkSchedule>();

        private readonly List<Unseen.Leg> quirkLegs = new List<Unseen.Leg>();

        private readonly List<int> quirkLegTapCounts = new List<int>();

        private readonly List<CL_Button> quirkButtons = new List<CL_Button>();

        private readonly List<Vector3> quirkGroundTargets = new List<Vector3>();

        private readonly Dictionary<string, Unseen.ContactShadow> contactShadows = new Dictionary<string, Unseen.ContactShadow>();

        private readonly List<Renderer> unseenRenderers = new List<Renderer>();

        private readonly Collider[] recoverySurfaceColliders = new Collider[32];

        private SpriteRenderer shadowRenderer;

        private SpriteRenderer eyeRenderer;

        private float nextEyeLookupTime;

        private Camera playerCamera;

        private Rigidbody body;

        private Camera overlayCamera;

        private Camera eyeCamera;

        private Camera maskedCamera;

        private static readonly Dictionary<Camera, Unseen.CameraMaskState> cameraMasks = new Dictionary<Camera, Unseen.CameraMaskState>();

        private static readonly Dictionary<Rigidbody, Unseen> bodyOwners = new Dictionary<Rigidbody, Unseen>();

        private bool consumedWasKinematic;

        private static Material overlayMaterial;

        private static Material eyeMaterial;

        private static Material normalMaterial;

        private static Sprite fallbackShadow;

        private static Sprite[] fallbackShadowFrames;

        private static Sprite contactShadowSprite;

        private bool buttonsEnabled = true;
        private bool itemUseEnabled = true;
        private bool itemGrabEnabled = true;
        private bool quirksEnabled = true;
        private bool crouchEnabled = true;
        public float legTotalLength = 14f;
        public int minimumLegSegments = 1;
        public int maximumLegSegments = 5;
        public bool hasEye = true;
        public float eyeSize = 1f;
        public int minimumLegCount = 5;
        public int maximumLegCount = 12;
        public int startingLegCount = 8;
        public float legMutationIntervalMinimum = 1f;
        public float legMutationIntervalMaximum = 5f;
        public float legSegmentRollChance = 0.35f;
        public float movementForcePerAnchoredLeg = 6f;
        public float generalSize = 1f;
        private static Sprite[] cimimShadowFrames;
        private static SegmentMeshDefinition[] cimimSegmentMeshes;
        private static readonly Dictionary<Transform, float> cimimSegmentLengths = new Dictionary<Transform, float>();
        private static Sprite unseenEyeSprite;

        private M_Level level;

        private Unseen.BehaviorState behaviorState;

        private Vector3 destination;

        private Item_Object inspectedItem;

        private GameEntity inspectedEntity;

        private CL_Button inspectedButton;

        private float destinationTimeout;

        private float nextProgressCheck;

        private float lastDestinationDistance;

        private float inspectUntil;

        private float nextTapTime;

        private int tapLegIndex;

        private int nextLegId;

        private float nextLegMutationTime;

        private float nextWalkStepTime;

        private Unseen.Leg tappingLeg;

        private Vector3 repeatedTapPoint;

        private int remainingTaps;

        private bool tapExtended;

        private Rigidbody tappedRigidbody;

        private Vector3 tapLocalPoint;

        private ENT_Player inspectedPlayer;

        private Vector3 playerInspectionOffset;

        private Vector3 curvedMovementDirection;

        private Vector3 idleCenter;

        private Rigidbody consumedRigidbody;

        private bool helpful;

        private Color creatureColor = Color.black;

        private Color eyeColor = Color.white;

        private int shadowFadeNoiseFrame = -1;

        private Rigidbody carriedRigidbody;

        private Item_Object carriedItem;

        private Unseen.Leg carriedLeg;

        private UnseenHandItem heldHandItem;

        private float handItemSpeedUntil;

        private float handItemSpeedMultiplier = 1f;

        private float handItemAggressionUntil;

        private float nextHandItemUseTime;

        private Rigidbody manipulatedRigidbody;

        private Unseen.Leg manipulationLeg;

        private float manipulationReleaseTime;

        private float carriedUntil;

        private float offerUntil;

        private Unseen.CarryDisposition carryDisposition;

        private float nextButtonSearchTime;

        private float nextButtonPressTime;

        private Rigidbody heldBody;

        private bool heldWasKinematic;

        private RigidbodyInterpolation heldInterpolation;

        private Vector3 heldLocalPoint;

        private readonly RaycastHit[] surfaceHits = new RaycastHit[32];

        private readonly List<string> expiredShadowKeys = new List<string>();

        private float nextItemSearchTime;

        private float nextEntitySearchTime;

        private float nextLevelCheckTime;

        private float personalityPhase;

        private float pace;

        private float curiosity;

        private float nextSocialTime;

        private float socialUntil;

        private float nextGlanceTime;

        private Vector3 glanceDirection;

        private Vector3 eyeDirection;

        private bool playerCrouchKnown;

        private bool playerCrouching;

        private bool scheduledCrouchState;

        private float scheduledCrouchTime;

        private float crouchBlend;

        private Unseen.QuirkType activeQuirk;

        private float quirkUntil;

        private float nextQuirkActionTime;

        private CL_Prop quirkProp;

        private Item_Object quirkItem;

        private Vector3 quirkGroundPoint;

        private Vector3 quirkStartPosition;

        private float quirkBounceStartTime;

        private int quirkLegIndex;

        private int quirkButtonIndex;

        private int quirkButtonRounds;

        private Item_Object playerInteractionItem;

        private Rigidbody playerInteractionRigidbody;

        private GameEntity playerInteractionEntity;

        private bool returnCarriedItemToPlayer;

        private bool forceFriendlyOffer;

        private bool consumeCarriedItem;

        private Rigidbody temporaryInteractionRigidbody;

        private int observedDroppedItemVersion;

        private int observedTossedPropVersion;

        private int observedHitEntityVersion;

        private bool playerWasVisible;

        private float attackSightUntil;

        private Vector3 helpfulWanderDestination;

        private float helpfulWanderUntil;

        private Material coloredOverlayMaterial;

        private Material coloredNormalMaterial;

        private sealed class SegmentMeshDefinition
        {
            public Mesh mesh;
            public float longestDistance;
        }
        private enum BehaviorState
        {
            Moving,
            Inspecting,
            Recovering,
            Attacking
        }

        private enum CarryDisposition
        {
            Wander,
            Offering
        }

        private enum QuirkType
        {
            None,
            PropTapping,
            ItemPile,
            EyeSpin,
            CrabRave,
            GroundBounce,
            PlayerStalk,
            EatObject,
            ButtonSequence
        }

        private sealed class QuirkSchedule
        {
            public Unseen.QuirkType type;

            public float frequency;

            public float nextTime;
        }

        private sealed class Leg
        {
            public DEN_Hunter_Arm arm;

            public List<Transform> bones;

            public List<Transform> segmentRenderers;

            public List<Transform> kneeRenderers;

            public Transform tip;

            public Vector3 searchDirection;

            public Vector3 targetPosition;

            public Transform anchorTransform;

            public Vector3 anchorLocalPosition;

            public float maximumReach;

            public float probeOffset;

            public Vector3 desiredTargetPosition;

            public bool isMovingToAnchor;

            public float nextAnchorRepositionTime;

            public bool recoveryLocked;

            public Vector3 tapTargetPosition;

            public float tapUntil;

            public Transform root;

            public Vector3 baseRootLocalPosition;

            public float appearanceScale;

            public float appearanceDirection;

            public Vector3 stepStartPosition;

            public Vector3 stepTargetPosition;

            public float stepProgress;

            public float stepDuration;

            public Vector3 restDirection;

            public Vector3 surfaceNormal = Vector3.up;

            public float nextProbeTime;
        }

        private sealed class ContactShadow
        {
            public SpriteRenderer renderer;

            public Collider collider;

            public float alpha;

            public float targetAlpha;
        }

        private sealed class CameraMaskState
        {
            public int users;

            public int layers;
        }
    }
}
