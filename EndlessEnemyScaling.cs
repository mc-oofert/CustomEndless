using HarmonyLib;
using Unity.VisualScripting;
using UnityEngine;

namespace Celestial.CustomEndless
{
    public sealed class EndlessLoopScalingState : MonoBehaviour
    {
        internal bool teethCaptured;
        internal bool ratKingCaptured;
        internal bool remainsCaptured;
        internal Vector3 originalScale;
        internal float a;
        internal float b;
        internal float c;
        internal float d;
        internal float e;
        internal float f;
    }

    public static class EndlessEnemyScaling
    {
        private static bool IsEndless => CL_GameManager.GetCurrentGamemode() == EndlessConfiguration.EndlessMode;

        public static void ApplyToExisting()
        {
            if (!IsEndless)
                return;

            if (DEN_Teeth.instance != null)
                Apply(DEN_Teeth.instance);

            if (DEN_RatKing.activeRatKings != null)
                foreach (DEN_RatKing ratKing in DEN_RatKing.activeRatKings.ToArray())
                    if (ratKing != null) Apply(ratKing);

            if (GameEntity.entities != null)
                foreach (GameEntity entity in GameEntity.entities.ToArray())
                    if (entity is DEN_Remains_Active remains && remains != null) Apply(remains);
        }

        private static EndlessLoopScalingState GetState(Component enemy)
        {
            EndlessLoopScalingState state = enemy.GetComponent<EndlessLoopScalingState>();
            return state != null ? state : enemy.gameObject.AddComponent<EndlessLoopScalingState>();
        }

        private static float LoopFactor(float increasePerLoop)
        {
            return 1f + Mathf.Max(0, EndlessConfiguration.cycles) * increasePerLoop;
        }

        private static void Apply(DEN_Teeth teeth)
        {
            EndlessLoopScalingState state = GetState(teeth);
            if (!state.teethCaptured)
            {
                state.teethCaptured = true;
                state.a = teeth.baseSpeed;
                state.b = teeth.chargeSpeed;
                state.c = teeth.attackDamage;
                state.d = teeth.aggressionMult;
                state.e = teeth.stunDamage;
                state.f = teeth.walkSpeed;
            }

            bool enabled = EndlessConfiguration.CustomUIManager.GetToggleState("scale_teeth_each_loop");
            float speed = enabled ? LoopFactor(0.12f) : 1f;
            float damage = enabled ? LoopFactor(0.2f) : 1f;
            teeth.baseSpeed = state.a * speed;
            teeth.chargeSpeed = state.b * speed;
            teeth.walkSpeed = state.f * speed;
            teeth.attackDamage = state.c * damage;
            teeth.aggressionMult = state.d * speed;
            teeth.stunDamage = state.e * damage;
        }

        private static void Apply(DEN_RatKing ratKing)
        {
            EndlessLoopScalingState state = GetState(ratKing);
            Rigidbody body = ratKing.GetComponent<Rigidbody>();
            if (!state.ratKingCaptured)
            {
                state.ratKingCaptured = true;
                state.a = ratKing.speed;
                state.b = ratKing.spinupTime;
                state.c = body != null ? body.mass : 0f;
            }

            bool enabled = EndlessConfiguration.CustomUIManager.GetToggleState("scale_rat_king_each_loop");
            float speed = enabled ? LoopFactor(0.14f) : 1f;
            float impact = enabled ? LoopFactor(0.2f) : 1f;
            ratKing.speed = state.a * speed * impact;
            ratKing.spinupTime = state.b / speed;
            if (body != null && state.c > 0f)
                body.mass = state.c * impact;
        }

        private static void Apply(DEN_Remains_Active remains)
        {
            EndlessLoopScalingState state = GetState(remains);
            if (!state.remainsCaptured)
            {
                state.remainsCaptured = true;
                state.originalScale = remains.transform.localScale;
                state.a = remains.moveSpeed;
                state.b = remains.moveSpeedRand;
                state.c = remains.proximityRadius;
                state.d = remains.proximityHazardBuildRate;
                state.e = remains.proximityPerlinSpeed;
                state.f = remains.scanDistance;
            }

            bool enabled = EndlessConfiguration.CustomUIManager.GetToggleState("scale_remains_each_loop");
            float speed = enabled ? LoopFactor(0.12f) : 1f;
            float hazard = enabled ? LoopFactor(0.16f) : 1f;
            float size = enabled ? LoopFactor(0.04f) : 1f;
            remains.transform.localScale = state.originalScale * size;
            remains.moveSpeed = state.a * speed;
            remains.moveSpeedRand = state.b * speed;
            remains.proximityRadius = state.c * hazard;
            remains.proximityHazardBuildRate = state.d * hazard;
            remains.proximityPerlinSpeed = state.e * hazard;
            remains.scanDistance = state.f * hazard;
        }

        private static bool IsTarget(SpawnTable.SpawnTableAsset asset, string target)
        {
            if (asset == null)
                return false;
            if (!string.IsNullOrEmpty(asset.name) && asset.name.ContainsInsensitive(target))
                return true;
            if (asset.prefabs == null)
                return false;
            foreach (GameObject prefab in asset.prefabs)
            {
                if (prefab == null)
                    continue;
                if (target == "teeth" && prefab.GetComponentInChildren<DEN_Teeth>(true) != null)
                    return true;
                if (target == "ratking" && prefab.GetComponentInChildren<DEN_RatKing>(true) != null)
                    return true;
                if (target == "remains" && prefab.GetComponentInChildren<DEN_Remains_Active>(true) != null)
                    return true;
            }

            return false;
        }

        [HarmonyPatch(typeof(DEN_Teeth), "Awake")]
        private static class TeethAwakePatch
        {
            private static void Postfix(DEN_Teeth __instance)
            {
                // hit vern with the steroids (if the time is right)
                if (IsEndless && __instance != null) Apply(__instance);
            }
        }

        [HarmonyPatch(typeof(DEN_RatKing), "Awake")]
        private static class RatKingAwakePatch
        {
            private static void Postfix(DEN_RatKing __instance)
            {
                // hit em with the steroids (if the time is right)
                if (IsEndless && __instance != null) Apply(__instance);
            }
        }

        [HarmonyPatch(typeof(DEN_Remains_Active), "Start")]
        private static class RemainsStartPatch
        {

            private static void Postfix(DEN_Remains_Active __instance)
            {
                // hit em with the steroids (if the time is right)
                //Someone please teach me how to write comments wtf is this
                //This isn't even like a relevant place to write comments afaik
                /* I taught myself programming in high school so new coders, good luck figuring this out. I've gotten better at naming but comments still escape my adhd*/
                //This is fun actually like I can talk to people while I'm programming. It's like I have friends.
                //Hai friend

                if (IsEndless && __instance != null) Apply(__instance);
            }
        }
    }
}
