using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;

//Behold the idiot creature.

namespace Celestial.CustomEndless
{
    public sealed class CimimPopulationManager : MonoBehaviour
    {
        private static CimimPopulationManager instance;
        private readonly List<Cimim> cimims = new List<Cimim>();
        private M_Level currentLevel;
        private float nextPopulationCheck;

        public static CimimPopulationManager GetOrCreate()
        {
            if (instance != null) return instance;
            GameObject managerObject = new GameObject("CimimPopulationManager");
            DontDestroyOnLoad(managerObject);
            instance = managerObject.AddComponent<CimimPopulationManager>();
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
            if (WorldLoader.instance != null && WorldLoader.instance.GetCurrentLevel() != null && WorldLoader.instance.GetCurrentLevel().GetLevel() != null)
                currentLevel = WorldLoader.instance.GetCurrentLevel().GetLevel();
            RefreshPopulation();
        }

        private void RefreshPopulation()
        {
            ENT_Player player = ENT_Player.GetPlayer();
            int population = EndlessConfiguration.GetCimimPopulationForLevel(currentLevel);
            int target = (int)Math.Min(int.MaxValue, Math.Abs((long)population));
            bool friendly = population < 0;
            Transform levelGoal = currentLevel?.GetLevelExit();
            for (int i = cimims.Count - 1; i >= 0; i--)
            {
                Cimim creature = cimims[i];
                if (creature == null || !creature.gameObject.activeInHierarchy || player != null && Vector3.Distance(creature.transform.position, player.transform.position) > 100f)
                {
                    if (creature != null) Destroy(creature.gameObject);
                    cimims.RemoveAt(i);
                    continue;
                }
                creature.ConfigureGoal(friendly, levelGoal);
            }
            while (cimims.Count > target)
            {
                Cimim creature = cimims[cimims.Count - 1];
                cimims.RemoveAt(cimims.Count - 1);
                if (creature != null) Destroy(creature.gameObject);
            }
            if (player == null || currentLevel == null || !currentLevel.gameObject.activeInHierarchy) return;
            while (cimims.Count < target) cimims.Add(Cimim.Spawn(GetEntranceSpawnPosition(currentLevel), currentLevel, friendly, levelGoal));
        }

        private Vector3 GetEntranceSpawnPosition(M_Level level)
        {
            Transform entrance = level.GetLevelExit();
            Vector3 forward = entrance != null ? entrance.forward : Vector3.forward;
            Vector3 right = entrance != null ? entrance.right : Vector3.right;
            Vector3 origin = entrance != null ? entrance.position : level.transform.position;
            return origin;
        }
    }

    public sealed class Cimim : MonoBehaviour
    {
        private sealed class Leg
        {
            public Transform root;
            public List<Transform> bones;
            public List<Transform> segments;
            public DEN_Hunter_Arm arm;
            public Rigidbody body;
            public Rigidbody tipBody;
            public SphereCollider tipCollider;
            public ConfigurableJoint reachJoint;
            public Transform tipVisual;
            public CL_Prop prop;
            public Vector3 relativeTarget;
            public Vector3 anchorPosition;
            public bool anchored;
            public bool grounded;
            public float strength;

            public void MoveTo(Vector3 position, bool isRelative = true, bool applyBodyReaction = true)
            {
                if (body == null || tipBody == null || strength <= 0f) return;
                ENT_Player player = ENT_Player.GetPlayer();
                if (prop != null && player != null)
                    foreach (ENT_Player.Hand hand in player.hands)
                        if (hand != null && hand.grabTarget == prop)
                        {
                            SetAnchored(false);
                            return;
                        }
                Vector3 target = isRelative ? body.transform.TransformPoint(position) : position;
                Vector3 error = target - tipBody.position;
                Vector3 targetVelocity = body.GetPointVelocity(target);
                float stiffness = strength;
                float damping = 2f * Mathf.Sqrt(stiffness * Mathf.Max(0.001f, tipBody.mass));
                Vector3 force = error * stiffness - (tipBody.velocity - targetVelocity) * damping;
                force = Vector3.ClampMagnitude(force, strength);
                if (!anchored) tipBody.AddForce(force, ForceMode.Force);
                if (applyBodyReaction) body.AddForceAtPosition(-force, target, ForceMode.Force);
            }

            public void PullBodyToward(Vector3 direction)
            {
                if (!anchored || body == null || direction.sqrMagnitude < 0.0001f) return;
                body.AddForceAtPosition(direction.normalized * strength, anchorPosition, ForceMode.Force);
            }

            public void SetAnchored(bool value)
            {
                if (tipBody == null || anchored == value) return;
                anchored = value;
                if (value)
                {
                    anchorPosition = tipBody.position;
                    if (!tipBody.isKinematic)
                    {
                        tipBody.velocity = Vector3.zero;
                        tipBody.angularVelocity = Vector3.zero;
                    }
                }
                tipBody.isKinematic = value;
            }
        }

        public abstract class MovementState
        {
            protected readonly Cimim cimim;
            private bool hasProgressSample;
            private float bestProgressDistance;
            private float noProgressTime;

            protected MovementState(Cimim creature)
            {
                cimim = creature;
            }

            public abstract bool ShouldUse();
            public virtual void Enter() { ResetProgressWatch(); }
            public virtual void Exit() { }
            public abstract void FixedUpdate();

            protected void ResetProgressWatch()
            {
                hasProgressSample = false;
                bestProgressDistance = float.PositiveInfinity;
                noProgressTime = 0f;
            }

            protected bool HasStalled(float remainingDistance)
            {
                if (float.IsNaN(remainingDistance) || float.IsInfinity(remainingDistance)) return false;
                if (!hasProgressSample)
                {
                    hasProgressSample = true;
                    bestProgressDistance = remainingDistance;
                    noProgressTime = 0f;
                    return false;
                }
                if (remainingDistance <= bestProgressDistance - 0.12f)
                {
                    bestProgressDistance = remainingDistance;
                    noProgressTime = 0f;
                    return false;
                }
                noProgressTime += Time.fixedDeltaTime;
                return noProgressTime >= 5f;
            }
        }

        public abstract class MovementPlanNode
        {
            protected readonly Cimim cimim;
            private bool hasProgressSample;
            private float bestProgressDistance;
            private float noProgressTime;

            protected MovementPlanNode(Cimim creature)
            {
                cimim = creature;
            }

            public abstract bool ShouldUse();
            public abstract void Begin();
            public abstract bool FixedUpdate();
            public virtual void End() { }
            public virtual string GetStatus() => GetType().Name;

            protected void ResetProgressWatch()
            {
                hasProgressSample = false;
                bestProgressDistance = float.PositiveInfinity;
                noProgressTime = 0f;
            }

            protected bool HasStalled(float remainingDistance)
            {
                if (float.IsNaN(remainingDistance) || float.IsInfinity(remainingDistance)) return false;
                if (!hasProgressSample)
                {
                    hasProgressSample = true;
                    bestProgressDistance = remainingDistance;
                    noProgressTime = 0f;
                    return false;
                }
                if (remainingDistance <= bestProgressDistance - 0.12f)
                {
                    bestProgressDistance = remainingDistance;
                    noProgressTime = 0f;
                    return false;
                }
                noProgressTime += Time.fixedDeltaTime;
                return noProgressTime >= 5f;
            }
        }

        private sealed class ClimbTarget
        {
            public CL_Handhold handhold;
            public Collider collider;
            public Vector3 point;
            public Vector3 localPoint;
            public bool isSurfaceTop;
            public Vector3 CurrentPoint => collider != null ? collider.transform.TransformPoint(localPoint) : handhold != null ? handhold.transform.TransformPoint(localPoint) : point;
        }

        private sealed class GrabHandhold : MovementState
        {
            private readonly Leg leg;
            private readonly ClimbTarget target;

            public GrabHandhold(Cimim creature, Leg limb, ClimbTarget destination) : base(creature)
            {
                leg = limb;
                target = destination;
            }

            public override bool ShouldUse() => false;

            public override void Exit()
            {
                leg?.SetAnchored(false);
            }

            public override void FixedUpdate()
            {
                if (leg == null || target == null || target.handhold == null || !target.handhold.active)
                {
                    cimim.movementState = null;
                    return;
                }
                Vector3 point = target.CurrentPoint;
                float remainingDistance = Vector3.Distance(leg.tipBody.position, point);
                if (remainingDistance <= 0.4f && (target.collider == null || Cimim.TryGetSurfaceContact(target.collider, point, out _)))
                {
                    Vector3 contact = point;
                    if (target.collider != null) Cimim.TryGetSurfaceContact(target.collider, point, out contact);
                    leg.MoveTo(contact, false);
                    leg.SetAnchored(true);
                    leg.anchorPosition = contact;
                    foreach (Leg otherLeg in cimim.legs)
                        if (otherLeg != leg) otherLeg.SetAnchored(false);
                    cimim.movementState = null;
                    return;
                }
                if (HasStalled(remainingDistance))
                {
                    leg.SetAnchored(false);
                    cimim.movementState = null;
                    return;
                }
                leg.MoveTo(point, false);
            }
        }

        private sealed class ReachFromHandhold : MovementState
        {
            private readonly Leg anchorLeg;
            private readonly Leg reachingLeg;
            private readonly ClimbTarget target;
            private float elapsed;

            public ReachFromHandhold(Cimim creature, Leg anchor, Leg reaching, ClimbTarget destination) : base(creature)
            {
                anchorLeg = anchor;
                reachingLeg = reaching;
                target = destination;
            }

            public override bool ShouldUse() => false;

            public override void Enter()
            {
                base.Enter();
                elapsed = 0f;
                reachingLeg.SetAnchored(false);
            }

            public override void FixedUpdate()
            {
                if (anchorLeg == null || !anchorLeg.anchored || reachingLeg == null || target == null || target.handhold == null || !target.handhold.active || target.collider != null && !target.collider.enabled)
                {
                    cimim.movementState = null;
                    return;
                }
                Vector3 point = target.CurrentPoint;
                Vector3 toPoint = point - cimim.transform.position;
                float bodyDistance = toPoint.magnitude;
                float tipDistance = Vector3.Distance(reachingLeg.tipBody.position, point);
                elapsed += Time.fixedDeltaTime;
                if (Vector3.Distance(anchorLeg.anchorPosition, point) > Cimim.LegLength * 1.7f || elapsed > 8f || HasStalled(bodyDistance + tipDistance))
                {
                    cimim.movementState = null;
                    return;
                }
                if (bodyDistance > Cimim.LegLength * 0.85f)
                {
                    anchorLeg.PullBodyToward(toPoint);
                    Vector3 reachPoint = cimim.transform.position + toPoint.normalized * Cimim.LegLength * 0.8f;
                    reachingLeg.MoveTo(reachPoint, false, false);
                    return;
                }
                anchorLeg.PullBodyToward(toPoint);
                reachingLeg.MoveTo(point, false, false);
                if (tipDistance > 0.4f || target.collider != null && !Cimim.TryGetSurfaceContact(target.collider, point, out _)) return;
                Vector3 contact = point;
                if (target.collider != null) Cimim.TryGetSurfaceContact(target.collider, point, out contact);
                reachingLeg.SetAnchored(true);
                reachingLeg.anchorPosition = contact;
                anchorLeg.SetAnchored(false);
                cimim.movementState = null;
            }
        }

        private sealed class LaunchToHandholdFromHandhold : MovementState
        {
            private readonly Leg anchorLeg;
            private readonly Leg reachingLeg;
            private readonly ClimbTarget target;
            private float elapsed;
            private float anchoredPushElapsed;
            private bool releasedForJump;
            private const float AnchoredPushDuration = 0.15f;

            public LaunchToHandholdFromHandhold(Cimim creature, Leg anchor, Leg reaching, ClimbTarget destination) : base(creature)
            {
                anchorLeg = anchor;
                reachingLeg = reaching;
                target = destination;
            }

            public override bool ShouldUse() => false;

            public override void Enter()
            {
                base.Enter();
                foreach (Leg leg in cimim.legs)
                    if (leg != anchorLeg) leg.SetAnchored(false);
            }

            public override void Exit()
            {
                anchorLeg?.SetAnchored(false);
                reachingLeg?.SetAnchored(false);
            }

            public override void FixedUpdate()
            {
                if (anchorLeg == null || reachingLeg == null || target == null || target.handhold == null || !target.handhold.active)
                {
                    anchorLeg?.SetAnchored(false);
                    reachingLeg?.SetAnchored(false);
                    cimim.movementState = null;
                    return;
                }
                Vector3 point = target.CurrentPoint;
                Vector3 toPoint = point - cimim.transform.position;
                float remainingDistance = Mathf.Min(Vector3.Distance(cimim.transform.position, point), Vector3.Distance(reachingLeg.tipBody.position, point));
                float horizontalDistance = Vector3.ProjectOnPlane(toPoint, Vector3.up).magnitude;
                if ((!releasedForJump && !anchorLeg.anchored) || Vector3.Distance(cimim.transform.position, point) > Cimim.LegLength * 1.75f || toPoint.y > Cimim.LegLength * 1.5f)
                {
                    anchorLeg.SetAnchored(false);
                    reachingLeg.SetAnchored(false);
                    cimim.movementState = null;
                    return;
                }
                if (HasStalled(remainingDistance))
                {
                    anchorLeg.SetAnchored(false);
                    reachingLeg.SetAnchored(false);
                    cimim.movementState = null;
                    return;
                }
                if (!releasedForJump && anchorLeg.anchored && toPoint.sqrMagnitude > 0.01f)
                {
                    anchorLeg.PullBodyToward(toPoint.normalized);
                    anchoredPushElapsed += Time.fixedDeltaTime;
                    if (anchoredPushElapsed >= AnchoredPushDuration)
                    {
                        anchorLeg.SetAnchored(false);
                        releasedForJump = true;
                    }
                }
                reachingLeg.MoveTo(point, false, false);
                elapsed += Time.fixedDeltaTime;
                if ((reachingLeg.tipBody.position - point).sqrMagnitude <= 0.16f && (target.collider == null || Cimim.TryGetSurfaceContact(target.collider, point, out _)))
                {
                    Vector3 contact = point;
                    if (target.collider != null) Cimim.TryGetSurfaceContact(target.collider, point, out contact);
                    reachingLeg.MoveTo(contact, false, false);
                    reachingLeg.SetAnchored(true);
                    reachingLeg.anchorPosition = contact;
                    if (anchorLeg.anchored) anchorLeg.SetAnchored(false);
                    cimim.movementState = null;
                    return;
                }
                if (elapsed > 10f)
                {
                    anchorLeg.SetAnchored(false);
                    reachingLeg.SetAnchored(false);
                    cimim.movementState = null;
                }
            }
        }

        private sealed class CircleAroundSurface : MovementState
        {
            private readonly Vector3 center;
            private Leg anchorLeg;
            private Leg movingLeg;
            private Vector3 radial;
            private int step;
            private float elapsed;

            public CircleAroundSurface(Cimim creature, Leg firstAnchor, Vector3 surfacePoint) : base(creature)
            {
                anchorLeg = firstAnchor;
                center = surfacePoint;
                radial = Vector3.ProjectOnPlane(creature.transform.position - surfacePoint, Vector3.up);
                if (radial.sqrMagnitude < 0.01f) radial = creature.transform.forward;
                radial.Normalize();
            }

            public override bool ShouldUse() => false;

            public override void Exit()
            {
                foreach (Leg leg in cimim.legs) leg.SetAnchored(false);
            }

            public override void FixedUpdate()
            {
                elapsed += Time.fixedDeltaTime;
                if (cimim.transform.position.y >= center.y + Cimim.LegLength * 0.5f || elapsed >= 10f || step >= 6)
                {
                    foreach (Leg leg in cimim.legs) leg.SetAnchored(false);
                    cimim.movementState = null;
                    return;
                }
                float remainingHeight = Mathf.Max(0f, center.y + Cimim.LegLength * 0.5f - cimim.transform.position.y);
                if (HasStalled(remainingHeight + (6 - step) * 0.25f))
                {
                    foreach (Leg leg in cimim.legs) leg.SetAnchored(false);
                    cimim.movementState = null;
                    return;
                }
                if (movingLeg == null)
                {
                    movingLeg = cimim.GetFreeLeg();
                    if (movingLeg == null)
                    {
                        anchorLeg.SetAnchored(false);
                        movingLeg = cimim.GetFreeLeg();
                    }
                    if (movingLeg == null) return;
                }
                float progress = (step + 1f) / 6f;
                Vector3 direction = Quaternion.AngleAxis(progress * 180f, Vector3.up) * radial;
                Vector3 destination = center + direction * 0.65f + Vector3.up * (progress * Cimim.LegLength * 0.5f);
                movingLeg.MoveTo(destination, false);
                if ((movingLeg.tipBody.position - destination).sqrMagnitude > 0.25f) return;
                movingLeg.SetAnchored(true);
                movingLeg.anchorPosition = movingLeg.tipBody.position;
                anchorLeg.SetAnchored(false);
                anchorLeg = movingLeg;
                movingLeg = null;
                step++;
            }
        }

        private sealed class DescendPlan : MovementPlanNode
        {
            private static readonly float[] DirectionOffsets = { 0f, 30f, -30f, 60f, -60f, 90f, -90f, 135f, -135f, 180f };
            private Vector3 ledgeGoal;
            private float nextSearchTime;
            private bool committed;

            public DescendPlan(Cimim creature) : base(creature) { }

            public override bool ShouldUse()
            {
                Vector3 target = cimim.GetPrimaryTargetPosition();
                if (target.y >= cimim.transform.position.y - Cimim.LegLength)
                {
                    committed = false;
                    return false;
                }
                if (committed || Time.time < nextSearchTime) return false;
                nextSearchTime = Time.time + 0.5f;
                return FindLedge(target);
            }

            public override void Begin()
            {
                if (cimim.movementState != null && cimim.movementState != cimim.walkingState)
                {
                    cimim.movementState.Exit();
                    cimim.movementState = null;
                }
                cimim.hasMovementGoalOverride = true;
                ResetProgressWatch();
            }

            public override string GetStatus() => "Descend:ToLedge";

            public override bool FixedUpdate()
            {
                Vector3 target = cimim.GetPrimaryTargetPosition();
                if (target.y >= cimim.transform.position.y - Cimim.LegLength)
                {
                    End();
                    cimim.movementPlanNode = null;
                    return false;
                }
                cimim.movementGoalOverride = new Vector3(ledgeGoal.x, cimim.transform.position.y, ledgeGoal.z);
                if (cimim.movementState == cimim.walkingState)
                {
                    cimim.movementState.FixedUpdate();
                    if (cimim.movementState != null) return true;
                }
                float remaining = Vector3.ProjectOnPlane(ledgeGoal - cimim.transform.position, Vector3.up).magnitude;
                if (remaining > Cimim.LegLength * 0.8f && HasStalled(remaining))
                {
                    End();
                    cimim.movementPlanNode = null;
                    return false;
                }
                if (remaining > 0.5f && cimim.walkingState.ShouldUse()) return false;
                if (remaining > Cimim.LegLength * 1.5f)
                {
                    End();
                    cimim.movementPlanNode = null;
                    return false;
                }
                foreach (Leg leg in cimim.legs) leg.SetAnchored(false);
                Rigidbody body = cimim.GetComponent<Rigidbody>();
                if (body != null)
                {
                    Vector3 launchDirection = (target - body.position).normalized;
                    float launchSpeed = Mathf.Clamp(cimim.legStrength / 8f, 1f, 3.5f);
                    body.AddForce(launchDirection * launchSpeed, ForceMode.VelocityChange);
                }
                committed = true;
                End();
                cimim.movementPlanNode = null;
                return false;
            }

            public override void End()
            {
                cimim.hasMovementGoalOverride = false;
                if (cimim.movementState != null && cimim.movementState != cimim.walkingState)
                {
                    cimim.movementState.Exit();
                    cimim.movementState = null;
                }
            }

            private bool FindLedge(Vector3 target)
            {
                if (!cimim.TryGetStandSurface(cimim.transform.position, out RaycastHit ground)) return false;
                Vector3 desired = Vector3.ProjectOnPlane(target - ground.point, Vector3.up);
                if (desired.sqrMagnitude < 0.01f) desired = Vector3.ProjectOnPlane(cimim.transform.forward, Vector3.up);
                desired.Normalize();
                float bestScore = float.MaxValue;
                Vector3 bestLedge = default;
                foreach (float offset in DirectionOffsets)
                {
                    Vector3 direction = Quaternion.AngleAxis(offset, Vector3.up) * desired;
                    Vector3 lastSupported = ground.point;
                    bool foundEdge = false;
                    for (float distance = 0.5f; distance <= Cimim.LegLength * 5f; distance += 0.5f)
                    {
                        Vector3 sample = ground.point + direction * distance;
                        if (!cimim.TryGetHorizontalSurface(sample, 1.5f, 2f, out RaycastHit surface) || Vector3.Dot(surface.normal, Vector3.up) < 0.85f || Mathf.Abs(surface.point.y - ground.point.y) > 0.5f)
                        {
                            foundEdge = true;
                            break;
                        }
                        lastSupported = surface.point;
                    }
                    if (!foundEdge) continue;
                    Vector3 approach = lastSupported - direction * Mathf.Min(1f, cimim.walkStepLength * 0.3f);
                    float score = Vector3.Distance(cimim.transform.position, approach) + Mathf.Abs(offset) * 0.02f;
                    if (score >= bestScore) continue;
                    bestScore = score;
                    bestLedge = approach;
                }
                if (bestScore == float.MaxValue) return false;
                ledgeGoal = bestLedge;
                return true;
            }
        }

        private sealed class EdgeClimbPlan : MovementPlanNode
        {
            private ClimbTarget source;
            private ClimbTarget destination;
            private Vector3 surfacePoint;
            private float nextSearchTime;
            private int stage;

            public EdgeClimbPlan(Cimim creature) : base(creature) { }

            public override bool ShouldUse()
            {
                if (Time.time < nextSearchTime) return false;
                nextSearchTime = Time.time + 0.5f;
                return FindRoute();
            }

            public override void Begin()
            {
                stage = 0;
                if (cimim.movementState != null)
                {
                    cimim.movementState.Exit();
                    cimim.movementState = null;
                }
                ResetProgressWatch();
            }

            public override string GetStatus()
            {
                string sourceName = source != null && source.handhold != null ? source.handhold.name : "?";
                string destinationName = destination != null && destination.handhold != null ? destination.handhold.name : "?";
                return "EdgeClimb:" + stage + ":" + sourceName + ">" + destinationName;
            }

            public override bool FixedUpdate()
            {
                if (destination == null || destination.handhold == null || !destination.handhold.active || destination.collider == null)
                {
                    Finish();
                    return false;
                }
                if (cimim.GetPrimaryTargetPosition().y <= cimim.transform.position.y + 0.25f)
                {
                    Finish();
                    return false;
                }
                if (stage == 0)
                {
                    float distance = Vector3.Distance(cimim.transform.position, source.CurrentPoint);
                    if (distance > Cimim.LegLength)
                    {
                        Vector3 toSource = source.CurrentPoint - cimim.transform.position;
                        float horizontalDistance = Vector3.ProjectOnPlane(toSource, Vector3.up).magnitude;
                        if (toSource.y < -Cimim.LegLength * 0.5f && horizontalDistance < Cimim.LegLength * 0.3f && distance <= Cimim.LegLength * 1.7f)
                        {
                            Leg anchor = cimim.GetAnchoredLeg();
                            if (anchor == null)
                                foreach (Leg leg in cimim.legs)
                                    if (cimim.TryGetGround(leg, out _))
                                    {
                                        leg.SetAnchored(true);
                                        anchor = leg;
                                        break;
                                    }
                            Leg reaching = cimim.GetFreeLeg();
                            if (anchor != null && reaching != null)
                            {
                                cimim.movementState = new LaunchToHandholdFromHandhold(cimim, anchor, reaching, source);
                                cimim.movementState.Enter();
                                stage = 5;
                                return true;
                            }
                            else
                            {
                                Finish();
                                return false;
                            }
                        }
                        else
                        {
                            cimim.movementGoalOverride = new Vector3(source.CurrentPoint.x, cimim.transform.position.y, source.CurrentPoint.z);
                            cimim.hasMovementGoalOverride = true;
                            return false;
                        }
                    }
                    if (stage != 5)
                    {
                        Leg freeLeg = cimim.GetFreeLeg();
                        if (freeLeg == null)
                        {
                            foreach (Leg leg in cimim.legs)
                                if (leg != null && !cimim.IsLegHeld(leg)) leg.SetAnchored(false);
                            freeLeg = cimim.GetFreeLeg();
                        }
                        if (freeLeg == null)
                        {
                            Finish();
                            return false;
                        }
                        cimim.movementGoalOverride = source.CurrentPoint;
                        cimim.hasMovementGoalOverride = true;
                        cimim.movementState = new GrabHandhold(cimim, freeLeg, source);
                        stage = 1;
                    }
                }
                if (stage == 1)
                {
                    cimim.movementState?.FixedUpdate();
                    if (cimim.movementState != null) return true;
                    if (!HasAnchorAt(source))
                    {
                        Finish();
                        return false;
                    }
                    stage = 2;
                }
                if (stage == 2)
                {
                    Leg anchor = FindAnchorAt(source);
                    Leg reaching = cimim.GetFreeLeg();
                    if (anchor == null || reaching == null)
                    {
                        Finish();
                        return false;
                    }
                    cimim.movementState = new LaunchToHandholdFromHandhold(cimim, anchor, reaching, destination);
                    cimim.movementState.Enter();
                    stage = 3;
                }
                if (stage == 3)
                {
                    cimim.movementState?.FixedUpdate();
                    if (cimim.movementState != null) return true;
                    Leg anchor = FindAnchorAt(destination);
                    if (anchor == null)
                    {
                        Finish();
                        return false;
                    }
                    cimim.movementState = new CircleAroundSurface(cimim, anchor, surfacePoint);
                    stage = 4;
                }
                if (stage == 4)
                {
                    cimim.movementState?.FixedUpdate();
                    if (cimim.movementState != null) return true;
                    Finish();
                    return false;
                }
                if (stage == 5)
                {
                    cimim.movementState?.FixedUpdate();
                    if (cimim.movementState != null) return true;
                    if (!HasAnchorAt(source))
                    {
                        Finish();
                        return false;
                    }
                    stage = 2;
                }
                return true;
            }

            public override void End()
            {
                cimim.hasMovementGoalOverride = false;
                if (cimim.movementState != null)
                {
                    cimim.movementState.Exit();
                    cimim.movementState = null;
                }
            }

            private void Finish()
            {
                End();
                cimim.movementPlanNode = null;
            }

            private bool FindRoute()
            {
                source = null;
                destination = null;
                Vector3 bodyPosition = cimim.transform.position;
                Vector3 goal = cimim.GetPrimaryTargetPosition();
                if (goal.y <= bodyPosition.y + 0.5f) return false;
                CL_Handhold[] handholds = Resources.FindObjectsOfTypeAll<CL_Handhold>();
                List<Collider> edgeColliders = new List<Collider>();
                foreach (CL_Handhold handhold in handholds)
                {
                    if (handhold == null || !handhold.active || !handhold.enabled || !handhold.gameObject.scene.IsValid() || !handhold.gameObject.scene.isLoaded) continue;
                    if (!handhold.name.ContainsInsensitive("edge") && !handhold.gameObject.name.ContainsInsensitive("edge")) continue;
                    Collider[] colliders = handhold.GetComponentsInChildren<Collider>();
                    foreach (Collider collider in colliders)
                    {
                        if (collider == null || !collider.enabled || collider.isTrigger) continue;
                        edgeColliders.Add(collider);
                    }
                }
                float launchReach = cimim.GetClimbReach();
                float sourceRange = Mathf.Max(Cimim.LegLength * 0.85f, launchReach * 1.5f);
                float bestScore = float.MaxValue;
                foreach (CL_Handhold handhold in handholds)
                {
                    if (handhold == null || !handhold.active || !handhold.enabled || !handhold.gameObject.scene.IsValid() || !handhold.gameObject.scene.isLoaded) continue;
                    if (!handhold.name.ContainsInsensitive("edge") && !handhold.gameObject.name.ContainsInsensitive("edge")) continue;
                    Collider[] sourceColliders = handhold.GetComponentsInChildren<Collider>();
                    foreach (Collider sourceCollider in sourceColliders)
                    {
                        if (sourceCollider == null || !sourceCollider.enabled || sourceCollider.isTrigger) continue;
                        List<Vector3> sourcePoints = FindNearbyEdgePoints(sourceCollider, bodyPosition, sourceRange);
                        foreach (Vector3 fromPoint in sourcePoints)
                        {
                            float approachDistance = Vector3.Distance(bodyPosition, fromPoint);
                            if (approachDistance > sourceRange || fromPoint.y >= goal.y) continue;
                            if (Mathf.Abs(fromPoint.y - bodyPosition.y) > 0.15f && approachDistance > Cimim.LegLength * 1.7f) continue;
                            Vector3 destinationProbe = fromPoint + Vector3.up * Mathf.Min(Cimim.LegLength, Mathf.Max(0.5f, goal.y - fromPoint.y));
                            foreach (Collider destinationCollider in edgeColliders)
                            {
                                if (destinationCollider == null || !destinationCollider.enabled || destinationCollider.isTrigger) continue;
                                List<Vector3> destinationPoints = FindNearbyEdgePoints(destinationCollider, destinationProbe, launchReach);
                                foreach (Vector3 toPoint in destinationPoints)
                                {
                                    float heightGain = toPoint.y - fromPoint.y;
                                    if (heightGain < 0.5f || toPoint.y > goal.y + 0.05f) continue;
                                    float launchDistance = Vector3.Distance(fromPoint, toPoint);
                                    if (launchDistance > launchReach) continue;
                                    if (!TryFindFlatSurfaceAtEdge(toPoint, out Vector3 landingSurface)) continue;
                                    if (landingSurface.y <= bodyPosition.y + 0.25f) continue;
                                    float score = approachDistance + launchDistance * 0.35f - heightGain * 0.15f;
                                    if (score >= bestScore) continue;
                                    bestScore = score;
                                    source = new ClimbTarget { handhold = handhold, collider = sourceCollider, point = fromPoint, localPoint = sourceCollider.transform.InverseTransformPoint(fromPoint) };
                                    CL_Handhold destinationHandhold = destinationCollider.GetComponentInParent<CL_Handhold>();
                                    destination = new ClimbTarget { handhold = destinationHandhold ?? handhold, collider = destinationCollider, point = toPoint, localPoint = destinationCollider.transform.InverseTransformPoint(toPoint) };
                                    surfacePoint = landingSurface;
                                }
                            }
                        }
                    }
                }
                return source != null && destination != null;
            }

            private bool HasAnchorAt(ClimbTarget target) => FindAnchorAt(target) != null;

            private Leg FindAnchorAt(ClimbTarget target)
            {
                if (target == null) return null;
                foreach (Leg leg in cimim.legs)
                    if (leg != null && leg.anchored && Vector3.Distance(leg.anchorPosition, target.CurrentPoint) <= 0.75f) return leg;
                return null;
            }

            public static List<Vector3> FindNearbyEdgePoints(Collider collider, Vector3 center, float radius)
            {
                List<Vector3> points = new List<Vector3>(26);
                if (collider == null || radius <= 0f) return points;
                for (int pitchIndex = -1; pitchIndex <= 1; pitchIndex++)
                {
                    float pitch = pitchIndex * 55f * Mathf.Deg2Rad;
                    for (int yawIndex = 0; yawIndex < 8; yawIndex++)
                    {
                        float yaw = yawIndex * 45f * Mathf.Deg2Rad;
                        Vector3 direction = new Vector3(Mathf.Cos(pitch) * Mathf.Cos(yaw), Mathf.Sin(pitch), Mathf.Cos(pitch) * Mathf.Sin(yaw));
                        if (!collider.Raycast(new Ray(center + direction * radius, -direction), out RaycastHit hit, radius * 2f)) continue;
                        bool duplicate = false;
                        foreach (Vector3 point in points)
                            if ((point - hit.point).sqrMagnitude < 0.16f)
                            {
                                duplicate = true;
                                break;
                            }
                        if (!duplicate) points.Add(hit.point);
                    }
                }
                return points;
            }

            private bool TryFindFlatSurfaceAtEdge(Vector3 edgePoint, out Vector3 surfacePoint)
            {
                surfacePoint = default;
                float bestDistance = float.MaxValue;
                for (int ring = 0; ring < 3; ring++)
                {
                    float radius = 0.35f + ring * 0.65f;
                    for (int directionIndex = 0; directionIndex < 8; directionIndex++)
                    {
                        float angle = directionIndex * 45f * Mathf.Deg2Rad;
                        Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                        Vector3 sample = edgePoint + offset;
                        if (!cimim.TryGetHorizontalSurface(sample, 1.25f, 2f, out RaycastHit hit) || Vector3.Dot(hit.normal, Vector3.up) < 0.85f) continue;
                        if (Mathf.Abs(hit.point.y - edgePoint.y) > Cimim.LegLength * 0.5f) continue;
                        float distance = Vector3.Distance(hit.point, edgePoint);
                        if (distance >= bestDistance) continue;
                        bestDistance = distance;
                        surfacePoint = hit.point;
                    }
                }
                return bestDistance < Cimim.LegLength * 0.75f;
            }
        }

        private sealed class PrioritizeClimb : MovementPlanNode
        {
            private readonly List<ClimbTarget> candidates = new List<ClimbTarget>();
            private readonly Dictionary<CL_Handhold, float> blockedUntil = new Dictionary<CL_Handhold, float>();
            private ClimbTarget target;
            private Leg climbAnchor;
            private float nextScanTime;
            private float nextAttemptTime;
            private float anchorDeadline;
            private string phase = "Searching";

            public PrioritizeClimb(Cimim creature) : base(creature) { }

            public bool HasClimbAnchor => climbAnchor != null && climbAnchor.anchored;

            public override bool ShouldUse()
            {
                return Time.time >= nextAttemptTime && cimim.GetPrimaryTargetPosition().y > cimim.transform.position.y + 0.5f;
            }

            public override void Begin()
            {
                if (cimim.movementState != null)
                {
                    cimim.movementState.Exit();
                    cimim.movementState = null;
                }
                target = null;
                climbAnchor = null;
                phase = "Searching";
                nextScanTime = 0f;
                ResetProgressWatch();
                Scan();
            }

            public override void End()
            {
                if (cimim.movementState != null)
                {
                    cimim.movementState.Exit();
                    cimim.movementState = null;
                }
                target = null;
                climbAnchor = null;
                cimim.hasMovementGoalOverride = false;
                cimim.ClearEyeLookOverride();
            }

            public override string GetStatus()
            {
                return "PrioritizeClimb:" + (target != null && target.handhold != null ? target.handhold.name : "Searching") + ":" + phase;
            }

            public bool HasNearbyRegularHandhold()
            {
                Scan();
                foreach (ClimbTarget candidate in candidates)
                {
                    if (IsBlocked(candidate.handhold)) continue;
                    float distance = Vector3.Distance(cimim.transform.position, candidate.CurrentPoint);
                    if (candidate.CurrentPoint.y < cimim.transform.position.y - Cimim.LegLength * 0.75f) continue;
                    if (HasClimbAnchor && Vector3.Distance(climbAnchor.anchorPosition, candidate.CurrentPoint) <= Cimim.LegLength * 1.65f || distance <= Cimim.LegLength * 1.7f || distance <= cimim.sightRange && CanWalkToward(candidate.CurrentPoint)) return true;
                }
                return false;
            }

            public override bool FixedUpdate()
            {
                if (cimim.GetPrimaryTargetPosition().y <= cimim.transform.position.y + 0.5f)
                {
                    End();
                    cimim.movementPlanNode = null;
                    return false;
                }
                Scan();
                if (climbAnchor != null && (!climbAnchor.anchored || cimim.IsLegHeld(climbAnchor))) climbAnchor = null;
                if (cimim.movementState != null)
                {
                    if (cimim.movementState == cimim.walkingState)
                    {
                        if (target != null && Vector3.Distance(cimim.transform.position, target.CurrentPoint) <= Cimim.LegLength * 1.7f)
                        {
                            cimim.movementState.Exit();
                            cimim.movementState = null;
                            ResetProgressWatch();
                        }
                        else
                        {
                            cimim.movementState.FixedUpdate();
                            if (cimim.movementState != null) return true;
                            if (target != null && !CanWalkToward(target.CurrentPoint)) BlockTarget();
                            ResetProgressWatch();
                        }
                    }
                    else
                    {
                        cimim.movementState.FixedUpdate();
                        if (cimim.movementState != null) return true;
                        if (target != null)
                        {
                            Leg reached = FindAnchorAt(target.CurrentPoint);
                            if (reached != null)
                            {
                                climbAnchor = reached;
                                anchorDeadline = Time.time + 3f;
                                blockedUntil[target.handhold] = Time.time + 4f;
                                target = null;
                                phase = "Pull";
                                ResetProgressWatch();
                                return true;
                            }
                            BlockTarget();
                        }
                    }
                }
                if (climbAnchor != null)
                {
                    Vector3 toAnchor = climbAnchor.anchorPosition - cimim.transform.position;
                    if (toAnchor.magnitude > Cimim.LegLength * 0.45f)
                    {
                        if (Time.time >= anchorDeadline)
                        {
                            climbAnchor.SetAnchored(false);
                            climbAnchor = null;
                        }
                        else
                        {
                            climbAnchor.PullBodyToward(toAnchor);
                            phase = "Pull";
                            return true;
                        }
                    }
                }
                if (target == null && !ChooseTarget())
                {
                    nextAttemptTime = Time.time + 0.75f;
                    End();
                    cimim.movementPlanNode = null;
                    return false;
                }
                if (target == null) return true;
                Vector3 point = target.CurrentPoint;
                float distanceToTarget = Vector3.Distance(cimim.transform.position, point);
                cimim.SetEyeLookTarget(point);
                if (HasStalled(distanceToTarget))
                {
                    BlockTarget();
                    return true;
                }
                if (HasClimbAnchor && point.y >= climbAnchor.anchorPosition.y + 0.3f && Vector3.Distance(climbAnchor.anchorPosition, point) <= Cimim.LegLength * 1.65f)
                {
                    Leg reaching = GetReachingLeg(climbAnchor);
                    if (reaching == null)
                    {
                        BlockTarget();
                        return true;
                    }
                    cimim.movementState = new ReachFromHandhold(cimim, climbAnchor, reaching, target);
                    cimim.movementState.Enter();
                    phase = "HandOverHand";
                    cimim.movementState.FixedUpdate();
                    return true;
                }
                if (distanceToTarget <= Cimim.LegLength * 0.95f)
                {
                    Leg reaching = GetReachingLeg();
                    if (reaching == null)
                    {
                        BlockTarget();
                        return true;
                    }
                    cimim.movementState = new GrabHandhold(cimim, reaching, target);
                    cimim.movementState.Enter();
                    phase = "Grab";
                    cimim.movementState.FixedUpdate();
                    return true;
                }
                if (distanceToTarget <= Cimim.LegLength * 1.7f)
                {
                    Leg anchor = FindLaunchAnchor();
                    if (anchor == null)
                    {
                        if (!PrepareLaunchFoot()) BlockTarget();
                        else phase = "Prepare";
                        return true;
                    }
                    Leg reaching = GetReachingLeg(anchor);
                    if (reaching == null)
                    {
                        BlockTarget();
                        return true;
                    }
                    cimim.movementState = new LaunchToHandholdFromHandhold(cimim, anchor, reaching, target);
                    cimim.movementState.Enter();
                    phase = "Launch";
                    cimim.movementState.FixedUpdate();
                    return true;
                }
                cimim.movementGoalOverride = new Vector3(point.x, cimim.transform.position.y, point.z);
                cimim.hasMovementGoalOverride = true;
                if (!cimim.walkingState.ShouldUse())
                {
                    Vector3 direction = Vector3.ProjectOnPlane(point - cimim.transform.position, Vector3.up).normalized;
                    bool foundStep = false;
                    for (float step = Mathf.Min(cimim.walkStepLength, Vector3.Distance(cimim.transform.position, point)); step >= 0.5f; step -= 0.35f)
                    {
                        Vector3 probe = cimim.transform.position + direction * step;
                        if (!cimim.TryGetHorizontalSurface(probe, Cimim.LegLength, Cimim.LegLength * 1.5f, out RaycastHit floor)) continue;
                        if (Vector3.Dot(floor.normal, Vector3.up) < 0.85f || Mathf.Abs(floor.point.y - (cimim.transform.position.y - Cimim.WalkFootDrop)) > 0.65f) continue;
                        cimim.movementGoalOverride = new Vector3(floor.point.x, cimim.transform.position.y, floor.point.z);
                        if (!cimim.walkingState.ShouldUse()) continue;
                        foundStep = true;
                        break;
                    }
                    if (!foundStep)
                    {
                        BlockTarget();
                        return true;
                    }
                }
                cimim.movementState = cimim.walkingState;
                cimim.movementState.Enter();
                phase = "Approach";
                cimim.movementState.FixedUpdate();
                return true;
            }

            private void Scan()
            {
                if (Time.time < nextScanTime) return;
                nextScanTime = Time.time + 0.5f;
                candidates.Clear();
                foreach (CL_Handhold hold in Resources.FindObjectsOfTypeAll<CL_Handhold>())
                {
                    if (hold == null || !hold.active || !hold.enabled || !hold.gameObject.activeInHierarchy || !hold.gameObject.scene.IsValid() || !hold.gameObject.scene.isLoaded) continue;
                    string name = hold.gameObject.name;
                    if (name.ContainsInsensitive("edge")) continue;
                    if (!name.ContainsInsensitive("handhold") && !name.ContainsInsensitive("piton")) continue;
                    if (TryCreateTarget(hold, out ClimbTarget candidate)) candidates.Add(candidate);
                }
            }

            private bool ChooseTarget()
            {
                Vector3 origin = cimim.transform.position;
                float minimumHeight = climbAnchor != null ? climbAnchor.anchorPosition.y + 0.4f : origin.y - Cimim.LegLength * 0.75f;
                float goalHeight = cimim.GetPrimaryTargetPosition().y;
                float bestScore = float.MaxValue;
                ClimbTarget best = null;
                foreach (ClimbTarget candidate in candidates)
                {
                    if (IsBlocked(candidate.handhold)) continue;
                    Vector3 point = candidate.CurrentPoint;
                    if (point.y < minimumHeight || point.y > goalHeight + Cimim.LegLength) continue;
                    float distance = Vector3.Distance(origin, point);
                    if (distance > Mathf.Max(cimim.sightRange, Cimim.LegLength * 6f)) continue;
                    bool handOverHand = HasClimbAnchor && point.y >= climbAnchor.anchorPosition.y + 0.3f && Vector3.Distance(climbAnchor.anchorPosition, point) <= Cimim.LegLength * 1.65f;
                    int tier = handOverHand ? 0 : distance <= Cimim.LegLength * 0.95f ? 1 : distance <= Cimim.LegLength * 1.7f ? 2 : CanWalkToward(point) ? 3 : 4;
                    if (tier == 4) continue;
                    float score = tier * 100f + distance - Mathf.Max(0f, point.y - origin.y) * 0.35f - GetNextHandholdGain(point) * 0.45f;
                    if (score >= bestScore) continue;
                    bestScore = score;
                    best = candidate;
                }
                target = best;
                if (target == null) return false;
                phase = "Selected";
                ResetProgressWatch();
                return true;
            }

            private float GetNextHandholdGain(Vector3 point)
            {
                float gain = 0f;
                foreach (ClimbTarget candidate in candidates)
                {
                    if (IsBlocked(candidate.handhold)) continue;
                    Vector3 next = candidate.CurrentPoint;
                    if (next.y <= point.y + 0.5f || Vector3.Distance(point, next) > Cimim.LegLength * 1.65f) continue;
                    gain = Mathf.Max(gain, next.y - point.y);
                }
                return Mathf.Min(gain, Cimim.LegLength * 2f);
            }

            private bool TryCreateTarget(CL_Handhold hold, out ClimbTarget candidate)
            {
                candidate = null;
                if (hold == null) return false;
                Vector3 origin = cimim.transform.position;
                Collider[] colliders = hold.GetComponentsInChildren<Collider>();
                float bestDistance = float.MaxValue;
                foreach (Collider collider in colliders)
                {
                    if (collider == null || !collider.enabled || collider.isTrigger || !Cimim.TryGetColliderSurfacePoint(collider, origin, out Vector3 point)) continue;
                    float distance = Vector3.Distance(origin, point);
                    if (distance >= bestDistance) continue;
                    bestDistance = distance;
                    candidate = new ClimbTarget { handhold = hold, collider = collider, point = point, localPoint = collider.transform.InverseTransformPoint(point) };
                }
                if (candidate == null)
                    candidate = new ClimbTarget { handhold = hold, point = hold.transform.position, localPoint = Vector3.zero };
                return true;
            }

            private bool CanWalkToward(Vector3 point)
            {
                Vector3 horizontal = Vector3.ProjectOnPlane(point - cimim.transform.position, Vector3.up);
                if (horizontal.magnitude < 0.5f) return false;
                Vector3 probe = cimim.transform.position + horizontal.normalized * Mathf.Min(1.5f, horizontal.magnitude);
                return !cimim.TryGetHorizontalSurface(probe, Cimim.LegLength, Cimim.LegLength * 1.5f, out RaycastHit floor)
                    ? false
                    : Vector3.Dot(floor.normal, Vector3.up) >= 0.85f && Mathf.Abs(floor.point.y - (cimim.transform.position.y - Cimim.WalkFootDrop)) <= 0.75f;
            }

            private bool IsBlocked(CL_Handhold hold)
            {
                return hold == null || blockedUntil.TryGetValue(hold, out float until) && Time.time < until;
            }

            private void BlockTarget()
            {
                if (target != null && target.handhold != null) blockedUntil[target.handhold] = Time.time + 8f;
                target = null;
                phase = "Searching";
                cimim.hasMovementGoalOverride = false;
                ResetProgressWatch();
            }

            private Leg FindAnchorAt(Vector3 point)
            {
                foreach (Leg leg in cimim.legs)
                    if (leg != null && leg.anchored && Vector3.Distance(leg.anchorPosition, point) <= 0.75f) return leg;
                return null;
            }

            private Leg FindLaunchAnchor()
            {
                if (climbAnchor != null && climbAnchor.anchored) return climbAnchor;
                Leg anchor = cimim.GetAnchoredLeg();
                if (anchor != null) return anchor;
                foreach (Leg leg in cimim.legs)
                {
                    if (leg == null || !cimim.TryGetGround(leg, out _)) continue;
                    leg.SetAnchored(true);
                    return leg;
                }
                return null;
            }

            private bool PrepareLaunchFoot()
            {
                foreach (Leg leg in cimim.legs)
                {
                    if (leg == null || cimim.IsLegHeld(leg) || !cimim.TryGetStandSurface(leg.root.position, out RaycastHit floor)) continue;
                    Vector3 foot = floor.point + floor.normal * Cimim.TipRadius;
                    if (Vector3.Distance(cimim.transform.position, foot) > Cimim.LegLength) continue;
                    leg.MoveTo(foot, false);
                    if (Vector3.Distance(leg.tipBody.position, foot) <= Cimim.TipRadius * 2.5f) leg.SetAnchored(true);
                    return true;
                }
                return false;
            }

            private Leg GetReachingLeg(Leg anchor = null)
            {
                foreach (Leg leg in cimim.legs)
                    if (leg != null && leg != anchor && !leg.anchored && !cimim.IsLegHeld(leg)) return leg;
                foreach (Leg leg in cimim.legs)
                    if (leg != null && leg != anchor && !cimim.IsLegHeld(leg))
                    {
                        leg.SetAnchored(false);
                        return leg;
                    }
                return null;
            }
        }

        private sealed class WalkingState : MovementState
        {
            private int nextLeg;
            private int activeLeg = -1;
            private float stepTime;
            private Vector3 stepStart;
            private Vector3 stepEnd;

            public int ActiveLeg => activeLeg;

            public WalkingState(Cimim creature) : base(creature) { }

            public override bool ShouldUse()
            {
                Vector3 target = cimim.GetTargetPosition();
                Vector3 toTarget = target - cimim.transform.position;
                Vector3 horizontal = Vector3.ProjectOnPlane(toTarget, Vector3.up);
                if (horizontal.magnitude < 0.1f) return false;
                if (Mathf.Abs(toTarget.y) > Cimim.LegLength && cimim.HasHigherSupportInReach()) return false;
                if (cimim.legs.Count != Cimim.LegCount) return false;
                bool leftSupported = cimim.TryGetGround(cimim.legs[0], out RaycastHit leftGround);
                bool rightSupported = cimim.TryGetGround(cimim.legs[1], out RaycastHit rightGround);
                if (!leftSupported && !rightSupported) return false;
                if (leftSupported && (cimim.legs[0].tipBody.position.y >= cimim.transform.position.y || Vector3.Dot(leftGround.normal, Vector3.up) < 0.85f)) return false;
                if (rightSupported && (cimim.legs[1].tipBody.position.y >= cimim.transform.position.y || Vector3.Dot(rightGround.normal, Vector3.up) < 0.85f)) return false;
                if (leftSupported && rightSupported && Mathf.Abs(leftGround.point.y - rightGround.point.y) > 0.35f) return false;
                Vector3 direction = horizontal.normalized;
                Vector3 groundPoint = leftSupported && rightSupported ? (leftGround.point + rightGround.point) * 0.5f : leftSupported ? leftGround.point : rightGround.point;
                float stride = Mathf.Min(cimim.walkStepLength, horizontal.magnitude);
                return cimim.TryGetHorizontalSurface(groundPoint + direction * stride, groundPoint.y + 0.65f, 1.3f, out RaycastHit ahead) && Mathf.Abs(ahead.point.y - groundPoint.y) <= 0.35f && Vector3.Dot(ahead.normal, Vector3.up) >= 0.85f;
            }

            public override void Enter()
            {
                base.Enter();
                foreach (Leg leg in cimim.legs)
                    if (cimim.TryGetGround(leg, out _)) leg.SetAnchored(true);
                nextLeg = 0;
                activeLeg = -1;
                stepTime = 0f;
            }

            public override void Exit()
            {
                foreach (Leg leg in cimim.legs) leg.SetAnchored(false);
                activeLeg = -1;
            }

            public override void FixedUpdate()
            {
                Vector3 toTarget = cimim.GetTargetPosition() - cimim.transform.position;
                Vector3 direction = Vector3.ProjectOnPlane(toTarget, Vector3.up).normalized;
                float remainingDistance = Vector3.ProjectOnPlane(toTarget, Vector3.up).magnitude;
                if (HasStalled(remainingDistance))
                {
                    cimim.movementState = null;
                    Exit();
                    return;
                }
                if (direction.sqrMagnitude < 0.001f || Vector3.ProjectOnPlane(toTarget, Vector3.up).magnitude < 0.1f || Mathf.Abs(toTarget.y) > Cimim.LegLength && cimim.HasHigherSupportInReach())
                {
                    cimim.movementState = null;
                    Exit();
                    return;
                }

                for (int i = 0; i < cimim.legs.Count; i++)
                {
                    Leg leg = cimim.legs[i];
                    cimim.RefreshGroundState(leg);
                    if (!cimim.IsLegHeld(leg) && !leg.anchored && leg.grounded && i != activeLeg) leg.SetAnchored(true);
                }

                if (activeLeg >= 0 && cimim.IsLegHeld(cimim.legs[activeLeg]))
                {
                    nextLeg = 1 - activeLeg;
                    activeLeg = -1;
                }

                foreach (Leg leg in cimim.legs)
                    if (cimim.IsLegHeld(leg)) leg.SetAnchored(false);

                if (activeLeg < 0)
                {
                    int stanceLeg = 1 - nextLeg;
                    if (!cimim.legs[stanceLeg].anchored && cimim.legs[nextLeg].anchored)
                    {
                        nextLeg = stanceLeg;
                        stanceLeg = 1 - nextLeg;
                    }
                    float stride = Mathf.Min(cimim.walkStepLength, Mathf.Max(0.1f, remainingDistance));
                    if (cimim.legs[stanceLeg].anchored && cimim.TryGetHorizontalSurface(cimim.legs[nextLeg].tipBody.position + direction * stride, cimim.legs[nextLeg].tipBody.position.y + cimim.walkStepHeight + 0.7f, cimim.walkStepHeight + 1f, out RaycastHit landing) && Vector3.Dot(landing.normal, Vector3.up) >= 0.85f)
                    {
                        activeLeg = nextLeg;
                        Leg swingLeg = cimim.legs[activeLeg];
                        swingLeg.SetAnchored(false);
                        stepTime = 0f;
                    }
                    else
                    {
                        cimim.movementState = null;
                        Exit();
                        return;
                    }
                }

                cimim.UpdateWalkingBodyTarget(direction, activeLeg);
                if (activeLeg >= 0 && stepTime <= 0f)
                {
                    stepStart = cimim.transform.InverseTransformDirection(cimim.legs[activeLeg].tipBody.position - cimim.bodyTargetPosition);
                    float stride = Mathf.Min(cimim.walkStepLength, Mathf.Max(0.1f, remainingDistance));
                    stepEnd = cimim.transform.InverseTransformDirection(Vector3.down * Cimim.WalkFootDrop + direction * stride);
                }

                for (int i = 0; i < cimim.legs.Count; i++)
                {
                    Leg leg = cimim.legs[i];
                    if (cimim.IsLegHeld(leg))
                    {
                        leg.MoveTo(leg.relativeTarget);
                        continue;
                    }
                    if (i == activeLeg)
                    {
                        stepTime += Time.fixedDeltaTime;
                        float progress = Mathf.Clamp01(stepTime / Mathf.Max(0.05f, cimim.walkStepDuration));
                        float eased = progress * progress * (3f - 2f * progress);
                        Vector3 target = Vector3.Lerp(stepStart, stepEnd, eased) + Vector3.up * (Mathf.Sin(progress * Mathf.PI) * cimim.walkStepHeight);
                        leg.MoveTo(target);
                        if ((leg.tipBody.position - cimim.transform.TransformPoint(stepEnd)).sqrMagnitude <= 0.09f || leg.grounded && progress >= 0.85f)
                        {
                            leg.SetAnchored(true);
                            nextLeg = 1 - activeLeg;
                            activeLeg = -1;
                        }
                    }
                    else if (leg.anchored)
                    {
                        float anchorProgress = Vector3.Dot(leg.anchorPosition - cimim.bodyTargetPosition, direction);
                        Vector3 stanceTargetOffset = Vector3.down * Cimim.WalkFootDrop + direction * (anchorProgress - cimim.walkStancePullback);
                        leg.relativeTarget = cimim.transform.InverseTransformDirection(stanceTargetOffset);
                        leg.MoveTo(leg.relativeTarget);
                    }
                    else
                    {
                        leg.MoveTo(leg.relativeTarget);
                    }
                }
            }
        }

        private sealed class StandUpState : MovementState
        {
            public StandUpState(Cimim creature) : base(creature) { }

            public override bool ShouldUse() => true;

            public override void FixedUpdate()
            {
                if (cimim.movementPlanNode == cimim.prioritizeClimb && cimim.prioritizeClimb.HasClimbAnchor) return;
                if (cimim.movementState != null && cimim.movementState != cimim.walkingState)
                {
                    ResetProgressWatch();
                    return;
                }
                if (cimim.movementState == cimim.walkingState)
                {
                    float walkingPostureError = cimim.bodyTargetPosition.y - cimim.transform.position.y;
                    if (walkingPostureError > 0.35f && HasStalled(walkingPostureError))
                    {
                        foreach (Leg leg in cimim.legs) leg.SetAnchored(false);
                        cimim.movementState.Exit();
                        cimim.movementState = null;
                        ResetProgressWatch();
                        return;
                    }
                    if (walkingPostureError <= 0.35f) ResetProgressWatch();
                    int activeLeg = cimim.walkingState.ActiveLeg;
                    for (int i = 0; i < cimim.legs.Count; i++)
                    {
                        Leg leg = cimim.legs[i];
                        if (walkingPostureError > 0.35f && i != activeLeg && leg.anchored && !cimim.IsLegHeld(leg)) leg.MoveTo(leg.relativeTarget);
                    }
                    return;
                }

                RaycastHit[] supports = cimim.standingSupports;
                bool[] hasSupport = cimim.hasStandingSupport;
                Array.Clear(hasSupport, 0, hasSupport.Length);
                float lowest = float.MaxValue;
                Vector3 center = Vector3.zero;
                int supportCount = 0;
                for (int i = 0; i < cimim.legs.Count; i++)
                {
                    Leg leg = cimim.legs[i];
                    if (leg == null || leg.root == null) continue;
                    Vector3 probe = leg.root.position;
                    if (!cimim.TryGetStandSurface(probe, out supports[i])) continue;
                    hasSupport[i] = true;
                    lowest = Mathf.Min(lowest, supports[i].point.y);
                    center += supports[i].point;
                    supportCount++;
                }
                if (supportCount == 0)
                {
                    ResetProgressWatch();
                    return;
                }
                center /= supportCount;
                cimim.bodyTargetPosition = new Vector3(center.x, lowest + WalkFootDrop, center.z);
                float postureError = cimim.bodyTargetPosition.y - cimim.transform.position.y;
                if (postureError > 0.35f && HasStalled(postureError))
                {
                    for (int i = 0; i < cimim.legs.Count; i++)
                        if (cimim.legs[i].anchored && hasSupport[i] && Mathf.Abs(cimim.legs[i].anchorPosition.y - supports[i].point.y) <= 0.65f)
                            cimim.legs[i].SetAnchored(false);
                    ResetProgressWatch();
                    return;
                }
                if (postureError <= 0.35f) ResetProgressWatch();

                for (int i = 0; i < cimim.legs.Count; i++)
                {
                    Leg leg = cimim.legs[i];
                    if (leg == null || cimim.IsLegHeld(leg) || !hasSupport[i]) continue;
                    cimim.RefreshGroundState(leg);
                    if (!leg.anchored)
                    {
                        Vector3 footTarget = supports[i].point + supports[i].normal * TipRadius;
                        leg.relativeTarget = cimim.transform.InverseTransformDirection(footTarget - cimim.transform.position);
                        leg.MoveTo(leg.relativeTarget);
                        if (Vector3.Distance(leg.tipBody.position, footTarget) <= TipRadius * 2.5f) leg.SetAnchored(true);
                    }
                    if (leg.anchored && Mathf.Abs(leg.anchorPosition.y - supports[i].point.y) <= 0.65f)
                    {
                        leg.relativeTarget = cimim.transform.InverseTransformDirection(leg.anchorPosition - cimim.bodyTargetPosition);
                        leg.MoveTo(leg.relativeTarget);
                    }
                }
            }
        }

        private const int LegCount = 2;
        private const int SegmentCount = 2;
        private const float LegLength = 4f;
        private const float SegmentLength = LegLength / SegmentCount;
        private const float TipRadius = 0.12f;
        private const float WalkFootDrop = 2f;
        private const int ShadowLayer = 0;
        private const int EyeLayer = 0;
        private const int ShadowOrder = 32759;
        private const int EyeOrder = 32767;
        public float legStrength = 18f;
        public float walkStepLength = 3.25f;
        public float walkStepHeight = 0.7f;
        public float walkStepDuration = 0.52f;
        public float walkStancePullback = 0.45f;
        public Vector3 bodyTargetPosition;
        public float sightRange = 25f;
        [Range(0f, 360f)] public float sightFieldOfView = 110f;
        public LayerMask sightLayers = Physics.DefaultRaycastLayers;
        private static PhysicsMaterial gripMaterial;
        private readonly List<Leg> legs = new List<Leg>(LegCount);
        private readonly RaycastHit[] surfaceHitBuffer = new RaycastHit[32];
        private readonly Collider[] overlapBuffer = new Collider[64];
        private readonly Collider[] sightColliderBuffer = new Collider[128];
        private readonly RaycastHit[] sightRaycastBuffer = new RaycastHit[32];
        private readonly RaycastHit[] standingSupports = new RaycastHit[LegCount];
        private readonly bool[] hasStandingSupport = new bool[LegCount];
        private static readonly Vector3[] colliderRayDirections = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back, new Vector3(1f, 1f, 1f).normalized, new Vector3(-1f, 1f, -1f).normalized };
        private MovementState movementState;
        private WalkingState walkingState;
        private StandUpState standUpState;
        private PrioritizeClimb prioritizeClimb;
        private DescendPlan descendPlan;
        private EdgeClimbPlan edgeClimbPlan;
        private MovementPlanNode movementPlanNode;
        private Vector3 movementGoalOverride;
        private bool hasMovementGoalOverride;
        private Vector3 eyeLookTarget;
        private Vector3 smoothedEyeDirection;
        private bool hasEyeLookOverride;
        private bool hasSmoothedEyeDirection;
        private const float EyeTurnSpeed = 3.2f;
        private SpriteRenderer bodyRenderer;
        private SpriteRenderer eyeRenderer;
        private Camera viewCamera;
        private int shadowFrame = -1;
        private float nextShadowFrameTime;
        private bool friendly;
        private M_Level parentLevel;
        private float nextLevelCheckTime;
        private Transform nextLevelGoal;
        private Transform levelGoal;
        private string lastLoggedMovementStatus;

        public static Cimim Spawn(Vector3 position, M_Level sourceLevel, bool isFriendly, Transform destination)
        {
            GameObject creatureObject = new GameObject("Cimim");
            creatureObject.transform.position = position;
            Cimim creature = creatureObject.AddComponent<Cimim>();
            creature.parentLevel = sourceLevel;
            creature.ConfigureGoal(isFriendly, destination);
            creature.Initialize();
            creatureObject.transform.SetParent(sourceLevel.transform, true);
            creature.UpdateParentLevel();
            return creature;
        }

        public void ConfigureGoal(bool isFriendly, Transform destination)
        {
            friendly = isFriendly;
            levelGoal = destination;
        }

        private void Initialize()
        {
            Rigidbody body = gameObject.AddComponent<Rigidbody>();
            body.mass = 0.35f;
            body.useGravity = true;
            body.drag = 0.8f;
            body.angularDrag = 8f;
            body.solverIterations = 16;
            body.solverVelocityIterations = 16;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            CreateBodyVisuals();
            if (gripMaterial == null)
            {
                gripMaterial = new PhysicsMaterial("CimimGrip");
                gripMaterial.staticFriction = 1f;
                gripMaterial.dynamicFriction = 1f;
                gripMaterial.frictionCombine = PhysicsMaterialCombine.Maximum;
                gripMaterial.bounciness = 0f;
                gripMaterial.bounceCombine = PhysicsMaterialCombine.Maximum;
            }
            for (int i = 0; i < LegCount; i++) CreateLeg(i);
            walkingState = new WalkingState(this);
            standUpState = new StandUpState(this);
            prioritizeClimb = new PrioritizeClimb(this);
            descendPlan = new DescendPlan(this);
            edgeClimbPlan = new EdgeClimbPlan(this);
        }

        private void CreateBodyVisuals()
        {
            GameObject bodyObject = new GameObject("CimimShadow");
            bodyObject.layer = ShadowLayer;
            bodyObject.transform.SetParent(transform, false);
            bodyRenderer = bodyObject.AddComponent<SpriteRenderer>();
            bodyRenderer.sprite = Unseen.GetSharedCimimShadowFrame(0);
            bodyRenderer.color = Color.black;
            bodyRenderer.sharedMaterial = Unseen.GetOverlayMaterial();
            bodyRenderer.sortingOrder = ShadowOrder;
            bodyObject.transform.localScale = Vector3.one * 1.25f;
            Sprite eyeSprite = Unseen.GetUnseenEyeSprite();
            if (eyeSprite == null) return;
            GameObject eyeObject = new GameObject("CimimEye");
            eyeObject.layer = EyeLayer;
            eyeObject.transform.SetParent(transform, true);
            eyeRenderer = eyeObject.AddComponent<SpriteRenderer>();
            eyeRenderer.sprite = eyeSprite;
            eyeRenderer.sharedMaterial = Unseen.GetEyeMaterial();
            eyeRenderer.color = Color.white;
            eyeRenderer.sortingOrder = EyeOrder;
        }

        private void CreateLeg(int index)
        {
            float side = index == 0 ? -1f : 1f;
            Vector3 direction = new Vector3(side * 0.72f, -0.69f, 0f).normalized;
            GameObject rootObject = new GameObject("CimimLegRoot_" + index);
            Transform root = rootObject.transform;
            root.SetParent(transform, false);
            root.localPosition = Vector3.right * side * 0.15f;
            root.localRotation = Quaternion.FromToRotation(Vector3.right, direction);
            List<Transform> bones = new List<Transform> { root };
            Transform parent = root;
            for (int i = 0; i < SegmentCount; i++)
            {
                GameObject boneObject = new GameObject("CimimLegBone_" + index + "_" + i);
                Transform bone = boneObject.transform;
                bone.SetParent(parent, false);
                bone.localPosition = Vector3.right * SegmentLength;
                bones.Add(bone);
                parent = bone;
            }
            DEN_Hunter_Arm arm = rootObject.AddComponent<DEN_Hunter_Arm>();
            arm.enabled = false;
            arm.armSegments = bones;
            arm.endTransform = bones[bones.Count - 1];
            Vector3 tipPosition = root.position + direction * (LegLength * 0.75f);
            arm.SetTargetPosition(tipPosition, Vector3.up);
            List<Transform> segments = new List<Transform>(SegmentCount);
            for (int i = 0; i < SegmentCount; i++)
                segments.Add(Unseen.CreateSharedLegSegment(root, "CimimLegSegment_" + index + "_" + i));
            GameObject tipObject = new GameObject("CimimLegTip_" + index);
            tipObject.transform.position = tipPosition;
            GameObject tipVisualObject = new GameObject("CimimLegTipVisual_" + index);
            tipVisualObject.layer = ShadowLayer;
            tipVisualObject.transform.SetParent(tipObject.transform, false);
            MeshFilter tipFilter = tipVisualObject.AddComponent<MeshFilter>();
            tipFilter.sharedMesh = Unseen.CreateTipMesh();
            MeshRenderer tipRenderer = tipVisualObject.AddComponent<MeshRenderer>();
            tipRenderer.sharedMaterial = Unseen.GetOverlayMaterial();
            tipRenderer.sortingOrder = ShadowOrder;
            tipObject.layer = ShadowLayer;
            SphereCollider tipCollider = tipObject.AddComponent<SphereCollider>();
            tipCollider.radius = TipRadius;
            tipCollider.sharedMaterial = gripMaterial;
            tipCollider.isTrigger = false;
            Rigidbody tipBody = tipObject.AddComponent<Rigidbody>();
            tipBody.mass = 0.12f;
            tipBody.drag = 0.45f;
            tipBody.angularDrag = 6f;
            tipBody.useGravity = true;
            tipBody.interpolation = RigidbodyInterpolation.Interpolate;
            tipBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            tipBody.solverIterations = 12;
            tipBody.solverVelocityIterations = 12;
            AudioSource tipAudio = tipObject.AddComponent<AudioSource>();
            tipAudio.spatialBlend = 1f;
            tipAudio.playOnAwake = false;
            tipAudio.volume = 0f;
            CL_Prop prop = tipObject.AddComponent<CL_Prop>();
            prop.onGrabEvent = new UnityEvent();
            prop.onDropEvent = new UnityEvent();
            prop.onDamage = new UnityEvent();
            prop.onKill = new UnityEvent();
            prop.unstickEvent = new UnityEvent();
            prop.damageable = false;
            prop.canCatchFire = false;
            CL_Prop cachedProp = FindCachedPropWithAudio();
            prop.hitSounds = CopyPropAudioEffects(cachedProp?.hitSounds);
            prop.dragSounds = CopyPropAudioEffects(cachedProp?.dragSounds);
            prop.breakSounds = CopyPropAudioEffects(cachedProp?.breakSounds);
            prop.unstickSounds = CopyPropAudioEffects(cachedProp?.unstickSounds);
            prop.damageSounds = CopyPropAudioEffects(cachedProp?.damageSounds);
            SetPropAudioVolumes(prop.hitSounds, 0f);
            SetPropAudioVolumes(prop.dragSounds, 0f);
            SetPropAudioVolumes(prop.breakSounds, 0f);
            SetPropAudioVolumes(prop.unstickSounds, 0f);
            SetPropAudioVolumes(prop.damageSounds, 0f);
            prop.holdDistance = 0.65f;
            prop.volume = 0f;
            prop.dragVolume = 0f;
            ObjectTagger tagger = tipObject.GetComponent<ObjectTagger>() ?? tipObject.AddComponent<ObjectTagger>();
            tagger.AddTag("Prop");
            tagger.AddTag("Pickupable");
            ConfigurableJoint joint = gameObject.AddComponent<ConfigurableJoint>();
            joint.connectedBody = tipBody;
            joint.autoConfigureConnectedAnchor = false;
            joint.anchor = root.localPosition;
            joint.connectedAnchor = Vector3.zero;
            joint.xMotion = ConfigurableJointMotion.Limited;
            joint.yMotion = ConfigurableJointMotion.Limited;
            joint.zMotion = ConfigurableJointMotion.Limited;
            SoftJointLimit limit = joint.linearLimit;
            limit.limit = LegLength;
            joint.linearLimit = limit;
            SoftJointLimitSpring spring = joint.linearLimitSpring;
            spring.spring = 70f;
            spring.damper = 32f;
            joint.linearLimitSpring = spring;
            joint.projectionMode = JointProjectionMode.None;
            joint.enableCollision = false;
            joint.enablePreprocessing = true;
            foreach (Leg other in legs) if (other.tipCollider != null) Physics.IgnoreCollision(tipCollider, other.tipCollider, true);
            legs.Add(new Leg { root = root, bones = bones, segments = segments, arm = arm, body = GetComponent<Rigidbody>(), tipBody = tipBody, tipCollider = tipCollider, reachJoint = joint, tipVisual = tipVisualObject.transform, prop = prop, relativeTarget = new Vector3(side, -LegLength * 0.5f, 0f), strength = legStrength });
        }

        private static CL_Prop FindCachedPropWithAudio()
        {
            CL_Prop[] props = Resources.FindObjectsOfTypeAll<CL_Prop>();
            foreach (CL_Prop prop in props)
                if (prop != null && prop.hitSounds != null && prop.hitSounds.Count > 0 && prop.hitSounds[0] != null && prop.hitSounds[0].clip != null && prop.hitSounds[0].clip.Count > 0)
                    return prop;
            return null;
        }

        private static List<CL_Prop.PropAudioEffect> CopyPropAudioEffects(List<CL_Prop.PropAudioEffect> source)
        {
            List<CL_Prop.PropAudioEffect> copy = new List<CL_Prop.PropAudioEffect>();
            if (source == null) return copy;
            foreach (CL_Prop.PropAudioEffect effect in source)
            {
                if (effect == null || effect.clip == null || effect.clip.Count == 0) continue;
                copy.Add(new CL_Prop.PropAudioEffect
                {
                    name = effect.name,
                    minVelocity = effect.minVelocity,
                    maxVelocity = effect.maxVelocity,
                    minPitch = effect.minPitch,
                    maxPitch = effect.maxPitch,
                    maxVolume = 0f,
                    clip = new List<AudioClip>(effect.clip)
                });
            }
            return copy;
        }

        private static void SetPropAudioVolumes(List<CL_Prop.PropAudioEffect> effects, float volume)
        {
            foreach (CL_Prop.PropAudioEffect effect in effects)
                if (effect != null) effect.maxVolume = volume;
        }

        private void FixedUpdate()
        {
            if (standUpState != null && standUpState.ShouldUse()) standUpState.FixedUpdate();
            if (movementPlanNode == null && movementState == null) hasMovementGoalOverride = false;
            if (movementPlanNode == null && descendPlan != null && descendPlan.ShouldUse())
            {
                movementPlanNode = descendPlan;
                movementPlanNode.Begin();
            }
            if (movementPlanNode == null && prioritizeClimb != null && GetPrimaryTargetPosition().y > transform.position.y + 0.5f)
            {
                if (prioritizeClimb.ShouldUse() && prioritizeClimb.HasNearbyRegularHandhold()) movementPlanNode = prioritizeClimb;
                else if (edgeClimbPlan != null && edgeClimbPlan.ShouldUse()) movementPlanNode = edgeClimbPlan;
                else if (prioritizeClimb.ShouldUse()) movementPlanNode = prioritizeClimb;
                movementPlanNode?.Begin();
            }
            if (movementPlanNode != null && movementPlanNode.FixedUpdate()) return;
            if (movementState == null)
            {
                if (walkingState.ShouldUse())
                {
                    movementState = walkingState;
                    movementState.Enter();
                }
            }
            if (movementState != null)
            {
                movementState.FixedUpdate();
                return;
            }
            foreach (Leg leg in legs) leg?.MoveTo(leg.relativeTarget);
        }

        private void Update()
        {
            if (Time.time >= nextLevelCheckTime)
            {
                nextLevelCheckTime = Time.time + 0.5f;
                UpdateParentLevel();
            }
            string state = movementState != null ? movementState.GetType().Name : "StandUp";
            if (movementState == walkingState) state = "Walking + StandUp";
            string plan = movementPlanNode != null ? movementPlanNode.GetStatus() : "None";
            string status = state + " | " + plan;
            if (status == lastLoggedMovementStatus) return;
            lastLoggedMovementStatus = status;
            Debug.Log("[Cimim] State=" + state + "; Plan=" + plan, this);
        }

        private Vector3 GetTargetPosition()
        {
            return hasMovementGoalOverride ? movementGoalOverride : GetPrimaryTargetPosition();
        }

        public Vector3 GetPrimaryTargetPosition()
        {
            if (friendly)
            {
                ENT_Player player = ENT_Player.GetPlayer();
                return player != null ? player.transform.position : transform.position;
            }
            return nextLevelGoal != null ? nextLevelGoal.position : transform.position;
        }

        private void UpdateParentLevel()
        {
            WorldLoader.LevelInfo closestLevel = WorldLoader.GetClosestLevelToPosition(transform.position);
            M_Level locatedLevel = closestLevel?.GetLevel();
            if (locatedLevel == null) locatedLevel = GetComponentInParent<M_Level>();
            if (locatedLevel != null && locatedLevel != parentLevel)
            {
                parentLevel = locatedLevel;
                transform.SetParent(parentLevel.transform, true);
            }
            nextLevelGoal = FindNextLevelExit(parentLevel);
        }

        private static Transform FindNextLevelExit(M_Level level)
        {
            if (level == null || WorldLoader.instance == null) return null;
            WorldLoader.BranchInfo branch = WorldLoader.instance.GetCurrentBranch();
            if (branch == null || branch.levelTracker == null) return null;
            for (int i = 0; i < branch.levelTracker.Count; i++)
            {
                WorldLoader.LevelInfo info = branch.levelTracker[i];
                if (info == null || info.GetLevel() != level) continue;
                for (int nextIndex = i + 1; nextIndex < branch.levelTracker.Count; nextIndex++)
                {
                    WorldLoader.LevelInfo next = branch.levelTracker[nextIndex];
                    M_Level nextLevel = next?.GetLevel();
                    Transform exit = nextLevel?.GetLevelExit();
                    if (exit != null) return exit;
                }
                return null;
            }
            return null;
        }

        private Leg GetFreeLeg()
        {
            foreach (Leg leg in legs)
                if (leg != null && leg.tipBody != null && !leg.anchored && !IsLegHeld(leg)) return leg;
            return null;
        }

        private Leg GetAnchoredLeg()
        {
            foreach (Leg leg in legs)
                if (leg != null && leg.tipBody != null && leg.anchored && !IsLegHeld(leg)) return leg;
            return null;
        }

        private float GetClimbReach()
        {
            Rigidbody body = GetComponent<Rigidbody>();
            float mass = body != null ? Mathf.Max(0.05f, body.mass) : 0.35f;
            float windupDistance = LegLength * 1.7f;
            float acceleration = Mathf.Max(0f, legStrength) * LegCount / mass;
            float launchVelocity = Mathf.Sqrt(2f * acceleration * windupDistance);
            float estimatedFlightTime = Mathf.Sqrt(2f * windupDistance / Mathf.Max(0.1f, Physics.gravity.magnitude));
            float projectedDistance = launchVelocity * estimatedFlightTime * 0.25f;
            float slack = Mathf.Clamp01(0.8f - Mathf.Max(0f, body != null ? body.drag : 0f) * 0.03f);
            return LegLength + Mathf.Min(windupDistance, projectedDistance) * slack;
        }

        public List<GameObject> GetObjectsInSight(Func<GameObject, bool> filter = null)
        {
            List<GameObject> visibleObjects = new List<GameObject>();
            Vector3 origin = transform.position + GetEyeDirection() * 0.12f;
            int count = Physics.OverlapSphereNonAlloc(origin, Mathf.Max(0f, sightRange), sightColliderBuffer, sightLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider collider = sightColliderBuffer[i];
                if (collider == null || IsOwnCollider(collider)) continue;
                GameObject candidate = GetSightObject(collider);
                if (candidate == null || visibleObjects.Contains(candidate) || filter != null && !filter(candidate)) continue;
                if (IsVisibleFromEye(collider, candidate, origin)) visibleObjects.Add(candidate);
            }
            return visibleObjects;
        }

        public GameObject FindObjectInSight(Func<GameObject, bool> filter)
        {
            List<GameObject> visibleObjects = GetObjectsInSight(filter);
            return visibleObjects.Count > 0 ? visibleObjects[0] : null;
        }

        public bool IsInSight(GameObject candidate, Func<GameObject, bool> filter = null)
        {
            if (candidate == null) return false;
            GameEntity entity = candidate.GetComponent<GameEntity>() ?? candidate.GetComponentInParent<GameEntity>();
            GameObject target = entity != null ? entity.gameObject : candidate;
            if (filter != null && !filter(target)) return false;
            Collider[] colliders = target.GetComponentsInChildren<Collider>();
            Vector3 origin = transform.position + GetEyeDirection() * 0.12f;
            foreach (Collider collider in colliders)
                if (collider != null && !IsOwnCollider(collider) && IsVisibleFromEye(collider, target, origin)) return true;
            return false;
        }

        private GameObject GetSightObject(Collider collider)
        {
            GameEntity entity = collider.GetComponentInParent<GameEntity>();
            if (entity != null) return entity.gameObject;
            Rigidbody attachedBody = collider.attachedRigidbody;
            return attachedBody != null ? attachedBody.gameObject : collider.gameObject;
        }

        private bool IsVisibleFromEye(Collider collider, GameObject candidate, Vector3 origin)
        {
            Vector3 closestPoint = GetColliderSurfacePoint(collider, origin);
            Vector3 toCandidate = closestPoint - origin;
            float distance = toCandidate.magnitude;
            if (distance > sightRange) return false;
            if (distance <= 0.001f) return true;
            Vector3 direction = toCandidate / distance;
            if (Vector3.Angle(GetEyeDirection(), direction) > Mathf.Clamp(sightFieldOfView, 0f, 360f) * 0.5f) return false;
            int count = Physics.RaycastNonAlloc(origin, direction, sightRaycastBuffer, distance + 0.02f, sightLayers, QueryTriggerInteraction.Ignore);
            float nearestDistance = float.MaxValue;
            GameObject nearestObject = null;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = sightRaycastBuffer[i];
                if (hit.collider == null || IsOwnCollider(hit.collider) || hit.distance >= nearestDistance) continue;
                nearestDistance = hit.distance;
                nearestObject = GetSightObject(hit.collider);
            }
            return nearestObject == candidate;
        }

        private bool IsLegHeld(Leg leg)
        {
            ENT_Player player = ENT_Player.GetPlayer();
            if (player == null || leg == null || leg.prop == null) return false;
            foreach (ENT_Player.Hand hand in player.hands)
                if (hand != null && hand.grabTarget == leg.prop) return true;
            return false;
        }

        private bool TryGetGround(Leg leg, out RaycastHit ground)
        {
            ground = default;
            if (leg == null || leg.tipBody == null) return false;
            if (!TryGetHorizontalSurface(leg.tipBody.position + Vector3.up * 0.18f, 0.48f, 0.85f, out ground) || Vector3.Dot(ground.normal, Vector3.up) < 0.7f) return false;
            float clearance = leg.tipBody.position.y - TipRadius - ground.point.y;
            return clearance >= -0.05f && clearance <= 0.2f;
        }

        private void RefreshGroundState(Leg leg)
        {
            if (leg == null) return;
            leg.grounded = TryGetGround(leg, out _);
        }

        private bool TryGetStandSurface(Vector3 point, out RaycastHit surface)
        {
            float rayStart = LegLength * 1.5f;
            int count = Physics.RaycastNonAlloc(point + Vector3.up * rayStart, Vector3.down, surfaceHitBuffer, rayStart + LegLength * 4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            surface = default;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = surfaceHitBuffer[i];
                if (hit.collider == null || IsOwnCollider(hit.collider) || Vector3.Dot(hit.normal, Vector3.up) < 0.85f || hit.point.y > transform.position.y + LegLength) continue;
                float distance = Mathf.Abs(point.y - hit.point.y);
                if (distance >= nearest) continue;
                nearest = distance;
                surface = hit;
                found = true;
            }
            return found;
        }

        private static Vector3 GetColliderSurfacePoint(Collider collider, Vector3 query)
        {
            return collider == null
                ? query
                : TryGetColliderSurfacePoint(collider, query, out Vector3 point) ? point : collider.bounds.ClosestPoint(query);
        }

        private static bool TryGetColliderSurfacePoint(Collider collider, Vector3 query, out Vector3 point)
        {
            point = query;
            if (collider == null || !collider.enabled) return false;
            Bounds bounds = collider.bounds;
            float bestDistance = float.MaxValue;
            float reach = Vector3.Distance(query, bounds.center) + bounds.extents.magnitude + 0.1f;
            for (int i = 0; i < colliderRayDirections.Length; i++)
            {
                Vector3 direction = colliderRayDirections[i];
                Ray ray = new Ray(query + direction * reach, -direction);
                if (!collider.Raycast(ray, out RaycastHit hit, reach * 2f)) continue;
                float distance = (hit.point - query).sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                point = hit.point;
            }
            return bestDistance < float.MaxValue;
        }

        private static bool TryGetSurfaceContact(Collider collider, Vector3 point, out Vector3 contact)
        {
            contact = point;
            if (collider == null || !collider.enabled) return false;
            float bestDistance = 0.3f * 0.3f;
            for (int i = 0; i < colliderRayDirections.Length; i++)
            {
                Vector3 direction = colliderRayDirections[i];
                if (!collider.Raycast(new Ray(point + direction * 0.6f, -direction), out RaycastHit hit, 1.2f)) continue;
                float distance = (hit.point - point).sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                contact = hit.point;
            }
            return bestDistance < 0.3f * 0.3f;
        }

        private void UpdateWalkingBodyTarget(Vector3 direction, int swingLegIndex)
        {
            Leg groundingFoot = null;
            Leg leadingFoot = null;
            float leadingProgress = float.NegativeInfinity;
            for (int i = 0; i < legs.Count; i++)
            {
                Leg leg = legs[i];
                if (leg == null || !leg.anchored || IsLegHeld(leg)) continue;
                if (i != swingLegIndex)
                {
                    float progress = Vector3.Dot(leg.anchorPosition - transform.position, direction);
                    if (leadingFoot == null || progress > leadingProgress)
                    {
                        leadingFoot = leg;
                        leadingProgress = progress;
                    }
                }
                if (groundingFoot == null || leg.anchorPosition.y < groundingFoot.anchorPosition.y)
                    groundingFoot = leg;
            }
            if (groundingFoot == null || leadingFoot == null) return;
            bodyTargetPosition = new Vector3(leadingFoot.anchorPosition.x, groundingFoot.anchorPosition.y + WalkFootDrop, leadingFoot.anchorPosition.z);
        }

        private bool TryGetHorizontalSurface(Vector3 point, float startHeight, float distance, out RaycastHit surface)
        {
            int count = Physics.RaycastNonAlloc(point + Vector3.up * startHeight, Vector3.down, surfaceHitBuffer, distance + startHeight, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            surface = default;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = surfaceHitBuffer[i];
                if (hit.collider == null || IsOwnCollider(hit.collider) || Vector3.Dot(hit.normal, Vector3.up) < 0.7f) continue;
                float hitDistance = (hit.point - point).sqrMagnitude;
                if (hitDistance >= nearest) continue;
                nearest = hitDistance;
                surface = hit;
                found = true;
            }
            return found;
        }

        private bool IsOwnCollider(Collider collider)
        {
            foreach (Leg leg in legs)
                if (leg != null && leg.tipCollider == collider) return true;
            return false;
        }

        private bool HasHigherSupportInReach()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, LegLength, overlapBuffer, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                Collider collider = overlapBuffer[i];
                if (collider == null || IsOwnCollider(collider)) continue;
                CL_Handhold handhold = collider.GetComponentInParent<CL_Handhold>();
                Vector3 closest = GetColliderSurfacePoint(collider, transform.position);
                float vertical = closest.y - transform.position.y;
                if (handhold != null && handhold.active && vertical > 1f && Vector3.Distance(transform.position, closest) <= LegLength) return true;
                Bounds bounds = collider.bounds;
                Vector3 topCenter = new Vector3(bounds.center.x, bounds.max.y + 1f, bounds.center.z);
                int hitCount = Physics.RaycastNonAlloc(topCenter, Vector3.down, surfaceHitBuffer, bounds.size.y + 0.3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
                {
                    RaycastHit hit = surfaceHitBuffer[hitIndex];
                    if (hit.collider == collider && Vector3.Dot(hit.normal, Vector3.up) >= 0.85f && hit.point.y > transform.position.y + 0.1f && Vector3.Distance(transform.position, hit.point) <= LegLength) return true;
                }
            }
            return false;
        }

        private void LateUpdate()
        {
            Vector3 desiredEyeDirection = hasEyeLookOverride ? eyeLookTarget - transform.position : GetTargetPosition() - transform.position;
            if (desiredEyeDirection.sqrMagnitude < 0.0001f) desiredEyeDirection = transform.forward;
            desiredEyeDirection.Normalize();
            if (!hasSmoothedEyeDirection)
            {
                smoothedEyeDirection = desiredEyeDirection;
                hasSmoothedEyeDirection = true;
            }
            else
            {
                float turnBlend = 1f - Mathf.Exp(-EyeTurnSpeed * Time.deltaTime);
                smoothedEyeDirection = Vector3.Slerp(smoothedEyeDirection, desiredEyeDirection, turnBlend).normalized;
            }
            viewCamera = Camera.main;
            if (viewCamera != null && bodyRenderer != null)
            {
                Vector3 toCamera = viewCamera.transform.position - bodyRenderer.transform.position;
                if (toCamera.sqrMagnitude > 0.0001f) bodyRenderer.transform.rotation = Quaternion.LookRotation(toCamera.normalized, viewCamera.transform.up);
            }
            if (Time.time >= nextShadowFrameTime && bodyRenderer != null)
            {
                nextShadowFrameTime = Time.time + 0.1f;
                int frameCount = Unseen.SharedCimimShadowFrameCount;
                shadowFrame = (shadowFrame + 1) % frameCount;
                bodyRenderer.sprite = Unseen.GetSharedCimimShadowFrame(shadowFrame);
            }
            if (eyeRenderer != null) Unseen.UpdateSharedEyePose(eyeRenderer, transform, GetEyeDirection(), 0.12f, 0.45f);
            foreach (Leg leg in legs)
            {
                if (leg == null || leg.tipBody == null) continue;
                leg.arm.Solve(leg.bones, leg.tipBody.position, 5, 1f);
                DEN_Hunter_Arm.ApplyPoleNormal(leg.bones, GetLegPoleNormal(leg), 1f);
                for (int i = 0; i < leg.segments.Count; i++)
                    Unseen.UpdateSharedLegSegment(leg.segments[i], leg.bones[i].position, leg.bones[i + 1].position);
                if (leg.tipVisual != null)
                {
                    Vector3 tipEnd = leg.tipBody.position;
                    Vector3 tipDirection = tipEnd - leg.bones[leg.bones.Count - 2].position;
                    if (tipDirection.sqrMagnitude < 0.0001f) tipDirection = leg.root.right;
                    float tipLength = Mathf.Min(0.3f, Vector3.Distance(tipEnd, leg.bones[leg.bones.Count - 2].position));
                    leg.tipVisual.position = tipEnd - tipDirection.normalized * (tipLength * 0.5f);
                    leg.tipVisual.rotation = Quaternion.LookRotation(tipDirection.normalized, transform.up);
                    leg.tipVisual.localScale = new Vector3(0.08f, 0.08f, tipLength);
                }
            }
        }

        private Vector3 GetLegPoleNormal(Leg leg)
        {
            Vector3 limbAxis = leg.tipBody.position - leg.bones[0].position;
            if (limbAxis.sqrMagnitude < 0.0001f) limbAxis = Vector3.down;
            limbAxis.Normalize();
            Vector3 backward = -GetEyeDirection();
            Vector3 sideways = Vector3.ProjectOnPlane(leg.root.position - transform.position, Vector3.up);
            if (sideways.sqrMagnitude < 0.0001f) sideways = transform.right * (leg.root.localPosition.x < 0f ? -1f : 1f);
            sideways.Normalize();
            Vector3 desiredElbowDirection = (Vector3.down * 0.7f + backward * 0.6f + sideways * 0.8f).normalized;
            Vector3 poleNormal = Vector3.Cross(limbAxis, desiredElbowDirection);
            if (poleNormal.sqrMagnitude < 0.0001f) poleNormal = Vector3.Cross(limbAxis, sideways);
            if (poleNormal.sqrMagnitude < 0.0001f) poleNormal = Vector3.Cross(limbAxis, Vector3.forward);
            return poleNormal.normalized;
        }

        private Vector3 GetEyeDirection()
        {
            if (hasSmoothedEyeDirection && smoothedEyeDirection.sqrMagnitude > 0.0001f) return smoothedEyeDirection.normalized;
            Vector3 direction = hasEyeLookOverride ? eyeLookTarget - transform.position : GetTargetPosition() - transform.position;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
        }

        private void SetEyeLookTarget(Vector3 worldPosition)
        {
            eyeLookTarget = worldPosition;
            hasEyeLookOverride = true;
        }

        private void ClearEyeLookOverride()
        {
            hasEyeLookOverride = false;
        }

        private void OnDestroy()
        {
            foreach (Leg leg in legs)
            {
                if (leg == null) continue;
                foreach (Transform segment in leg.segments) Unseen.ReleaseSharedLegSegment(segment);
                if (leg.tipBody != null) Destroy(leg.tipBody.gameObject);
            }
        }
    }
}
