using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Events;

namespace Celestial.CustomEndless
{
    public sealed class TransitionRoomDoors : MonoBehaviour
    {
        private sealed class DoorBinding
        {
            internal UT_Door door;
            internal TransitionRoomDoors owner;
            internal Transform connector;
            internal long command;
            internal bool open;
        }

        private static readonly Dictionary<UT_Door, DoorBinding> bindings = new Dictionary<UT_Door, DoorBinding>();
        private static long nextCommand;
        private static bool synchronizing;
        private readonly List<UT_Door> doors = new List<UT_Door>();
        private readonly List<Collider> colliders = new List<Collider>();
        private float enteredAt = -1f;
        private bool collisionRemoved;
        private bool initialized;
        private M_Level level;

        private static string DoorKey(string name)
        {
            string key = Regex.Replace(Regex.Replace(name ?? string.Empty, @"\.\d+$", ""), "[^a-zA-Z0-9]", "").ToLowerInvariant();
            return Regex.IsMatch(key, "^(entrance|exit)(left|right)(inner|outer)[0-9]*$")
                ? "envhabbluedoor02" + (key.Contains("left") ? "l" : "r")
                : key;
        }

        private static bool IsDoorPanel(string name)
        {
            string key = DoorKey(name);
            return Regex.IsMatch(key, "^(envhabbluedoor02[lr]|gondoladoor[lr]|silodoor|door)[0-9]*$") || key == "securedoor";
        }

        public void Initialize()
        {
            if (initialized) return;
            initialized = true;
            level = GetComponent<M_Level>();
            UT_Door[] templates = Resources.FindObjectsOfTypeAll<UT_Door>()
                .Where(door => door != null && door.GetComponentInParent<TransitionRoomDoors>() == null).ToArray();
            foreach (Transform panel in GetComponentsInChildren<Transform>(true))
            {
                if (panel == transform || !IsDoorPanel(panel.name)) continue;
                if (panel.GetComponent<Renderer>() == null && panel.GetComponentInChildren<Renderer>(true) == null) continue;
                UT_Door door = panel.GetComponent<UT_Door>();
                if (door == null)
                {
                    string key = DoorKey(panel.name);
                    UT_Door template = templates.FirstOrDefault(candidate => DoorKey(candidate.name) == key);
                    bool blue = key.StartsWith("envhabbluedoor02");
                    if (template == null && blue)
                        template = templates.FirstOrDefault(candidate => DoorKey(candidate.name).StartsWith("envhabbluedoor02l") && candidate.openClip != null);
                    door = panel.gameObject.AddComponent<UT_Door>();
                    door.startOpen = false;
                    door.moveToStartPositionInstantly = false;
                    door.startOpenDelay = 0f;
                    door.space = UT_Door.UseSpace.Local;
                    door.moveSpeed = template != null ? template.moveSpeed : 1.5f;
                    door.openEvent = new UnityEvent();
                    door.closeEvent = new UnityEvent();
                    door.finishEvent = new UnityEvent();
                    door.finishOpenEvent = new UnityEvent();
                    door.finishCloseEvent = new UnityEvent();
                    UT_Door soundTemplate = template;
                    if (soundTemplate == null)
                        soundTemplate = templates.FirstOrDefault(candidate => DoorKey(candidate.name).StartsWith("envhabbluedoor02l") && candidate.openClip != null);
                    if (soundTemplate != null)
                    {
                        door.openClip = soundTemplate.openClip;
                        door.closeClip = soundTemplate.closeClip;
                        door.finishOpenClip = soundTemplate.finishOpenClip;
                        door.finishCloseClip = soundTemplate.finishCloseClip;
                    }
                    if (template != null)
                    {
                        door.finishOpenShake = template.finishOpenShake;
                        door.finishOpenShakeDistance = template.finishOpenShakeDistance;
                        door.finishCloseShake = template.finishCloseShake;
                        door.finishCloseShakeDistance = template.finishCloseShakeDistance;
                    }
                    Vector3 movement;
                    if (blue)
                    {
                        Transform other = GetComponentsInChildren<Transform>(true).FirstOrDefault(candidate => candidate != panel &&
                            DoorKey(candidate.name).StartsWith(key.StartsWith("envhabbluedoor02l") ? "envhabbluedoor02r" : "envhabbluedoor02l") &&
                            Vector3.Distance(candidate.position, panel.position) < 0.5f);
                        Renderer renderer = panel.GetComponentInChildren<Renderer>(true);
                        Renderer otherRenderer = other?.GetComponentInChildren<Renderer>(true);
                        Vector3 direction = otherRenderer != null ? renderer.bounds.center - otherRenderer.bounds.center : panel.right * (key.StartsWith("envhabbluedoor02l") ? 1f : -1f);
                        movement = direction.normalized * 2.26f;
                    }
                    else if (template != null)
                    {
                        Vector3 sourceMove = template.space == UT_Door.UseSpace.World ? template.move :
                            template.space == UT_Door.UseSpace.LocalRelativeToSelf ? template.transform.right * template.move.x + template.transform.forward * template.move.y + template.transform.up * template.move.z :
                            template.transform.parent != null ? template.transform.parent.TransformVector(template.move) : template.move;
                        movement = panel.rotation * Quaternion.Inverse(template.transform.rotation) * sourceMove;
                        door.rotate = template.rotate;
                    }
                    else
                    {
                        Renderer renderer = panel.GetComponentInChildren<Renderer>(true);
                        movement = Vector3.up * (renderer.bounds.size.y + 0.5f);
                    }
                    door.move = panel.parent != null ? panel.parent.InverseTransformVector(movement) : movement;
                }
                doors.Add(door);
                Renderer closedRenderer = panel.GetComponentInChildren<Renderer>(true);
                Transform entrance = level?.GetLevelEntrance();
                Transform exit = level?.GetLevelExit();
                Transform connector = null;
                if (closedRenderer != null)
                {
                    if (entrance != null && closedRenderer.bounds.SqrDistance(entrance.position) <= 1f) connector = entrance;
                    if (exit != null && closedRenderer.bounds.SqrDistance(exit.position) <= 1f &&
                        (connector == null || (exit.position - closedRenderer.bounds.center).sqrMagnitude < (connector.position - closedRenderer.bounds.center).sqrMagnitude)) connector = exit;
                }
                bindings[door] = new DoorBinding { door = door, owner = this, connector = connector, open = door.open, command = door.open ? ++nextCommand : 0 };
                panel.gameObject.isStatic = false;
                colliders.AddRange(panel.GetComponentsInChildren<Collider>(true));
            }
        }

        private static bool Connected(DoorBinding first, DoorBinding second)
        {
            return first.connector == null || second.connector == null || first.owner == null || second.owner == null
                ? false
                : first.owner == second.owner
                ? first.connector == second.connector
                : (first.connector.position - second.connector.position).sqrMagnitude <= 0.0625f &&
                Mathf.Abs(Vector3.Dot(first.connector.forward, second.connector.forward)) >= 0.99f;
        }

        internal static void Synchronize(UT_Door source, bool open)
        {
            if (synchronizing || source == null || !bindings.TryGetValue(source, out DoorBinding origin)) return;
            synchronizing = true;
            try
            {
                long command = ++nextCommand;
                foreach (DoorBinding binding in bindings.Values.ToArray())
                {
                    if (binding.door == null || (binding != origin && !Connected(origin, binding))) continue;
                    binding.command = command;
                    binding.open = open;
                    if (binding.door == source || !binding.door.gameObject.activeInHierarchy) continue;
                    binding.door.moveSpeed = source.moveSpeed;
                    if (open) binding.door.Open(); else binding.door.Close();
                }
            }
            finally { synchronizing = false; }
        }

        private void LateUpdate()
        {
            foreach (UT_Door door in doors)
            {
                if (door == null || !door.gameObject.activeInHierarchy || !bindings.TryGetValue(door, out DoorBinding binding)) continue;
                if (binding.command > 0 && door.open != binding.open)
                {
                    synchronizing = true;
                    try { if (binding.open) door.Open(); else door.Close(); }
                    finally { synchronizing = false; }
                }
                DoorBinding leader = binding;
                foreach (DoorBinding candidate in bindings.Values)
                {
                    if (candidate.door == null || !candidate.door.gameObject.activeInHierarchy || (candidate != binding && !Connected(binding, candidate))) continue;
                    if (candidate.command > leader.command) leader = candidate;
                    else if (candidate.command == leader.command)
                    {
                        float candidateAmount = candidate.door.lerpAmount;
                        float leaderAmount = leader.door.lerpAmount;
                        bool further = candidate.open ? candidateAmount > leaderAmount : candidateAmount < leaderAmount;
                        if (further || (Mathf.Approximately(candidateAmount, leaderAmount) && candidate.door.GetInstanceID() < leader.door.GetInstanceID())) leader = candidate;
                    }
                }
                if (leader == null || leader == binding || leader.command == 0) continue;
                synchronizing = true;
                try
                {
                    binding.command = leader.command;
                    binding.open = leader.open;
                    door.moveSpeed = leader.door.moveSpeed;
                    if (leader.open) door.Open(); else door.Close();
                    float amount = leader.door.lerpAmount;
                    if (Mathf.Abs(door.lerpAmount - amount) > 0.00001f) door.SetOpenAmount(Mathf.Max(amount, 0.000001f));
                    door.lerpAmount = amount;
                    door.autoMoving = leader.door.autoMoving;
                    door.latched = leader.door.latched;
                }
                finally { synchronizing = false; }
            }
        }

        private void OnDestroy()
        {
            foreach (UT_Door door in doors)
                if (!ReferenceEquals(door, null)) bindings.Remove(door);
        }

        [HarmonyPatch(typeof(UT_Door), nameof(UT_Door.Open))]
        private static class OpenDoorPatch
        {
            private static void Prefix(UT_Door __instance, out bool __state) { __state = __instance.open; }
            private static void Postfix(UT_Door __instance, bool __state)
            {
                if (!__state && __instance.open) Synchronize(__instance, true);
            }
        }

        [HarmonyPatch(typeof(UT_Door), nameof(UT_Door.Close))]
        private static class CloseDoorPatch
        {
            private static void Prefix(UT_Door __instance, out bool __state) { __state = __instance.open; }
            private static void Postfix(UT_Door __instance, bool __state)
            {
                if (__state && !__instance.open) Synchronize(__instance, false);
            }
        }

        public void Enter()
        {
            if (enteredAt < 0f) enteredAt = Time.time;
        }

        private void Update()
        {
            ENT_Player player = ENT_Player.GetPlayer();
            if (enteredAt < 0f && level != null && level.HasEntered()) Enter();
            if (player != null)
            {
                foreach (UT_Door door in doors)
                {
                    if (door == null || !door.gameObject.activeInHierarchy) continue;
                    Renderer renderer = door.GetComponentInChildren<Renderer>(true);
                    Vector3 closest = renderer != null ? renderer.bounds.ClosestPoint(player.transform.position) : door.transform.position;
                    if ((closest - player.transform.position).sqrMagnitude <= 36f) door.Open();
                }
            }
            if (collisionRemoved || enteredAt < 0f || Time.time - enteredAt < 10f) return;
            foreach (Collider collider in colliders)
                if (collider != null) collider.enabled = false;
            foreach (UT_Door door in doors)
                if (door != null)
                {
                    foreach (Collider collider in door.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                    if (bindings.TryGetValue(door, out DoorBinding binding))
                        foreach (DoorBinding connected in bindings.Values)
                            if (connected.door != null && Connected(binding, connected))
                                foreach (Collider collider in connected.door.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                }
            collisionRemoved = true;
        }
    }

    [HarmonyPatch(typeof(M_Level), "OnSpawn")]
    internal static class TransitionRoomDoorSetupPatch
    {
        private static void Postfix(M_Level __instance)
        {
            if (__instance == null || CL_GameManager.GetCurrentGamemode() != EndlessConfiguration.EndlessMode ||
                !LevelSpawnOrientationPatch.IsTransitionRoom(__instance) || __instance.GetComponent<TransitionRoomDoors>() != null) return;
            __instance.gameObject.AddComponent<TransitionRoomDoors>().Initialize();
        }
    }

    [HarmonyPatch(typeof(M_Level), "OnLevelActivate")]
    internal static class TransitionRoomDoorActivationPatch
    {
        private static void Prefix(M_Level __instance)
        {
            if (__instance == null || CL_GameManager.GetCurrentGamemode() != EndlessConfiguration.EndlessMode || !LevelSpawnOrientationPatch.IsTransitionRoom(__instance)) return;
            TransitionRoomDoors controller = __instance.GetComponent<TransitionRoomDoors>();
            if (controller == null) controller = __instance.gameObject.AddComponent<TransitionRoomDoors>();
            controller.Initialize();
        }
    }

    [HarmonyPatch(typeof(M_Level), "OnEnter")]
    internal static class TransitionRoomDoorEntryPatch
    {
        private static void Prefix(M_Level __instance)
        {
            if (__instance == null || CL_GameManager.GetCurrentGamemode() != EndlessConfiguration.EndlessMode || !LevelSpawnOrientationPatch.IsTransitionRoom(__instance)) return;
            TransitionRoomDoors controller = __instance.GetComponent<TransitionRoomDoors>();
            if (controller == null)
            {
                controller = __instance.gameObject.AddComponent<TransitionRoomDoors>();
                controller.Initialize();
            }
            controller.Enter();
        }
    }
}
