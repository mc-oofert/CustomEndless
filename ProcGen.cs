using Celestial.CustomEndless;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class ProcGen
{
    public int targetHeight = 400;
    public int minPaths = 1;
    public int maxPaths = 2;
    public float centerBias = 0.01f;
    public float offshootChance = 0.3f;
    public float maxRadius = 100f;
    public string activeTheme = "generic";
    public float baseJumpForce = 12f;
    public float gravity = 9.81f;
    public float minJumpAngle = 85f;
    public float maxJumpAngle = 90f;
    public float minVerticalJumpFraction = 0.55f;
    public float maxVerticalJumpFraction = 0.85f;

    public abstract class PathNode
    {
        private Vector3 launchVelocityAdjustment;
        public virtual string Theme { get; } = "generic";
        public virtual bool OffshootOnly { get; } = false;
        public virtual float Weight { get; } = 1f;
        public virtual float MinHorizontalDistance { get; } = 3f;
        public virtual float MaxHorizontalDistance { get; } = 6f;
        public virtual float MinVerticalDistance { get; } = 3f;
        public virtual float MaxVerticalDistance { get; } = 6f;

        public virtual float NextHorizontalMultiplier { get; } = 1f;
        public virtual float NextVerticalMultiplier { get; } = 1f;

        public virtual float GetNextHorizontalDistance()
        {
            return UnityEngine.Random.Range(MinHorizontalDistance, MaxHorizontalDistance) * NextHorizontalMultiplier;
        }

        public virtual float GetNextVerticalDistance()
        {
            return UnityEngine.Random.Range(MinVerticalDistance, MaxVerticalDistance) * NextVerticalMultiplier;
        }

        public abstract Vector3 Spawn(Vector3 position, Quaternion rotation, M_Level level);

        public virtual Vector3 GetExitPoint(Vector3 position, Quaternion rotation, Vector3 spawnedExitPoint) { return spawnedExitPoint; }

        public Vector3 SpawnAndGetExitPoint(Vector3 position, Quaternion rotation, M_Level level)
        {
            launchVelocityAdjustment = Vector3.zero;
            return GetExitPoint(position, rotation, Spawn(position, rotation, level));
        }

        public Vector3 GetLaunchVelocityAdjustment() { return launchVelocityAdjustment; }

        protected void RecordHandholdLaunchVelocity(CL_Handhold handhold, Quaternion rotation, float baseJumpForce)
        {
            if (handhold == null) return;
            launchVelocityAdjustment += Vector3.up * baseJumpForce * (handhold.jumpMult - 1f);
            if (handhold.modules == null) return;
            foreach (HandholdModule module in handhold.modules)
            {
                HandholdModule_JumpBoost jumpBoost = module as HandholdModule_JumpBoost;
                if (jumpBoost == null || !jumpBoost.enabled) continue;
                Vector3 boostDirection = Vector3.Lerp(Vector3.up, rotation * Vector3.forward, jumpBoost.boostInLookDir);
                launchVelocityAdjustment += boostDirection * (jumpBoost.jumpBoost / 10f);
            }
        }

        public static GameObject GetPrefab(string name, M_Level bob = null)
        {
            var level = bob;
            if (bob != null)
            {
                GameObject found = EndlessConfiguration.M_Level_OnLevelActivate_Items_Patch.FindChildByName(level.gameObject.transform, name);
                if (found != null) return found;
                level = CustomLevelRegistry.Load("Assets");
                found = EndlessConfiguration.M_Level_OnLevelActivate_Items_Patch.FindChildByName(level.gameObject.transform, name);
                if (found != null) return found;
            }
            else
            {
                level = CustomLevelRegistry.Load("Assets");
                GameObject found = EndlessConfiguration.M_Level_OnLevelActivate_Items_Patch.FindChildByName(level.gameObject.transform, name);
                if (found != null) return found;
            }

            return EndlessConfiguration.GetCachedObject<GameObject>(name);
        }
    }

    public class DoubleLadder : PathNode
    {
        public override float Weight => 0.4f;
        public override float MinHorizontalDistance => 4f;
        public override float MaxHorizontalDistance => 8f;
        public override float MinVerticalDistance => 5f;
        public override float MaxVerticalDistance => 10f;

        public override Vector3 Spawn(Vector3 position, Quaternion rotation, M_Level level)
        {
            GameObject prefab = GetPrefab("Cube", level);
            if (prefab == null) return position;

            var platform1 = SpawnVoxelPlatform(position, rotation, new Vector3Int(1, 5, 1), prefab, level.transform, 10);
            platform1.transform.position += Vector3.up * 24;

            Vector3 secondPos = position + (rotation * Vector3.forward * 5f) + Vector3.up * 10f;
            var platform2 = SpawnVoxelPlatform(secondPos, rotation, new Vector3Int(1, 5, 1), prefab, level.transform, 10);
            platform2.transform.position += Vector3.up * 20;

            return secondPos + (Vector3.up * 20f);
        }
    }

    public class SuperLaunchHandhold : PathNode
    {
        public override float Weight => 0.1f;
        public override float MinHorizontalDistance => 20f;
        public override float MaxHorizontalDistance => 32f;
        public override float MinVerticalDistance => 10f;
        public override float MaxVerticalDistance => 20f;

        public override Vector3 Spawn(Vector3 position, Quaternion rotation, M_Level level)
        {
            GameObject prefab = EndlessConfiguration.GetCachedObject<GameObject>("Item_Artifact_Remote_Handhold");
            if (prefab != null)
            {
                GameObject instance = UnityEngine.Object.Instantiate(prefab, position, rotation, level.transform);

                var timerModule = instance.GetComponent<UT_DestroyAfterTime>();
                if (timerModule != null) timerModule.enabled = false;

                var otherModule = instance.GetComponent<UT_Timer>();
                if (otherModule != null)
                {
                    otherModule.endEvent.Invoke();
                    otherModule.enabled = false;
                }

                foreach (ParticleSystem particles in instance.GetComponentsInChildren<ParticleSystem>())
                {
                    SetTheFuckingColorOfThisParticleSystem(particles, Color.yellow);
                }
                SetTheFuckingColorOfThisMesh(instance, Color.yellow);

                var handholdModule = instance.GetComponent<CL_Handhold>();
                if (handholdModule != null)
                {
                    handholdModule.jumpMult = 0.1f;
                    handholdModule.velocityMult = 2.5f;
                    HandholdModule_JumpBoost module = handholdModule.modules.Find(test => test is HandholdModule_JumpBoost) as HandholdModule_JumpBoost;
                    if (module != null)
                    {
                        module.boostInLookDir = 1;
                        module.fovAdjust *= 8;
                        module.doubleHandBoost = true;
                        module.jumpBoost *= 10;
                    }
                    RecordHandholdLaunchVelocity(handholdModule, rotation, 14f);
                }

                AddProximityActivator(instance);
            }
            return position;
        }
    }

    public class StaggeredWallRunThing : PathNode
    {
        public override float Weight => 0.2f;
        public override float MinHorizontalDistance => 8f;
        public override float MaxHorizontalDistance => 14f;
        public override float MinVerticalDistance => 0f;
        public override float MaxVerticalDistance => 4f;

        public override Vector3 Spawn(Vector3 position, Quaternion rotation, M_Level level)
        {
            GameObject prefab = GetPrefab("Cube", level);
            if (prefab == null) return position;

            Vector3 currentPos = position;
            int walls = UnityEngine.Random.Range(3, 6);

            for (int i = 0; i < walls; i++)
            {
                float sideOffset = (i % 2 == 0) ? -3.5f : 3.5f;
                Vector3 wallPos = currentPos + (rotation * new Vector3(sideOffset, 0, i * 4.5f));
                SpawnVoxelPlatform(wallPos, rotation, new Vector3Int(1, 4, 4), prefab, level.transform);
            }

            return currentPos + (rotation * Vector3.forward * (walls * 4.5f));
        }
    }


    //public class RopeSwingChain : PathNode
    //{
    //    public override float Weight => 0.05f;
    //    public override float MinHorizontalDistance => 4f;
    //    public override float MaxHorizontalDistance => 5f;
    //    public override float MinVerticalDistance => 0f;
    //    public override float MaxVerticalDistance => 4f;

    //    public override Vector3 Spawn(Vector3 position, Quaternion rotation, M_Level level)
    //    {
    //        GameObject cubePrefab = GetPrefab("Cube", level);
    //        var ropeLevelObj = EndlessConfiguration.GetCachedObject<M_Level>("M4_Abyss_Transit_02");

    //        if (ropeLevelObj == null) return position;

    //        int ropeCount = UnityEngine.Random.Range(3, 5);
    //        Vector3 currentPos = position;

    //        for (int i = 0; i < ropeCount; i++)
    //        {
    //            Vector3 ropeAnchorPos = currentPos + Vector3.up * 8f;
    //            if (cubePrefab != null)
    //            {
    //                SpawnVoxelPlatform(ropeAnchorPos, rotation, new Vector3Int(1, 1, 1), cubePrefab, level.transform);
    //            }
    //            CreateRope(ropeLevelObj.gameObject, 4, ropeAnchorPos);

    //            currentPos += rotation * Vector3.forward * UnityEngine.Random.Range(5f, 7f);
    //        }

    //        return currentPos;
    //    }
    //}



    public class Stairs : PathNode
    {
        public override float Weight => 0.8f;
        public override float MinHorizontalDistance => 2f;
        public override float MaxHorizontalDistance => 5f;
        public override float MinVerticalDistance => 3f;
        public override float MaxVerticalDistance => 8f;

        public override Vector3 Spawn(Vector3 position, Quaternion rotation, M_Level level)
        {
            GameObject prefab = GetPrefab("Cube", level);
            if (prefab == null) return position;

            Vector3 currentPos = position;
            int steps = UnityEngine.Random.Range(4, 7);

            for (int i = 0; i < steps; i++)
            {
                float rotAngle = UnityEngine.Random.Range(-30f, 30f);
                Quaternion stepRot = rotation * Quaternion.Euler(0, rotAngle, 0);
                Vector3 offset = stepRot * Vector3.forward * UnityEngine.Random.Range(2f, 4f) + Vector3.up * UnityEngine.Random.Range(2.5f, 4f);
                currentPos += offset;

                SpawnVoxelPlatform(currentPos, stepRot, new Vector3Int(1, 1, 1), prefab, level.transform);
            }

            return currentPos;
        }
    }

    public class BasicPlatform : PathNode
    {
        public override float Weight => 2f;
        public override float MinHorizontalDistance => 4f;
        public override float MaxHorizontalDistance => 7f;
        public override float MinVerticalDistance => 3f;
        public override float MaxVerticalDistance => 5f;

        public override Vector3 Spawn(Vector3 position, Quaternion rotation, M_Level level)
        {
            GameObject prefab = GetPrefab("Cube", level);
            if (prefab != null)
            {
                GeneratePlatform(position, 4, prefab);
            }
            return position;
        }
    }

    public class LongJumpPlatform : PathNode
    {
        public override float MinHorizontalDistance => 7f;
        public override float MaxHorizontalDistance => 15f;
        public override float MinVerticalDistance => -1f;
        public override float MaxVerticalDistance => 1f;

        public override Vector3 Spawn(Vector3 position, Quaternion rotation, M_Level level)
        {
            GameObject prefab = GetPrefab("Cube", level);
            if (prefab != null)
            {
                SpawnVoxelPlatform(position, rotation, new Vector3Int(2, 1, 6), prefab, level.transform);
            }
            return position;
        }
    }

    public class LongJumpPlatform2 : PathNode
    {
        public override float MinHorizontalDistance => 14f;
        public override float MaxHorizontalDistance => 18f;
        public override float MinVerticalDistance => -1f;
        public override float MaxVerticalDistance => 1f;

        public override Vector3 Spawn(Vector3 position, Quaternion rotation, M_Level level)
        {
            GameObject prefab = GetPrefab("Cube", level);
            if (prefab != null)
            {
                SpawnVoxelPlatform(position, rotation, new Vector3Int(1, 1, UnityEngine.Random.Range(13, 20)), prefab, level.transform);
            }
            return position;
        }
    }

    public class AirBarnacle : PathNode
    {
        public override bool OffshootOnly => true;
        public override float MinHorizontalDistance => 3f;
        public override float MaxHorizontalDistance => 5f;
        public override float MinVerticalDistance => -8f;
        public override float MaxVerticalDistance => -5f;

        public override Vector3 Spawn(Vector3 position, Quaternion rotation, M_Level level)
        {
            Transform parentTransform = level != null ? level.transform : null;
            GameObject prefab = GetPrefab("Denizen_Barnacle_Small", level);

            if (prefab != null)
            {
                var instance = UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity, parentTransform);
                if (instance == null) return position;

                var meshRenderer = instance.GetComponentInChildren<SkinnedMeshRenderer>();
                if (meshRenderer != null)
                {
                    meshRenderer.enabled = false;
                }

                var animator = instance.GetComponentInChildren<Animator>();
                if (animator != null)
                {
                    animator.enabled = false;
                }

                var barnacle = instance.GetComponent<DEN_Barnacle>();
                if (barnacle != null)
                {
                    barnacle.spawnPolyps = false;

                    if (barnacle.geo != null)
                    {
                        barnacle.geo.localPosition = Vector3.zero;
                        barnacle.geo.localRotation = Quaternion.identity;

                        var geoTransforms = barnacle.geo.GetComponentsInChildren<Transform>();
                        if (geoTransforms != null)
                        {
                            foreach (Transform child in geoTransforms)
                            {
                                if (child != null) child.localPosition = Vector3.zero;
                            }
                        }
                    }

                    var module = barnacle.tongueInterface as Barnacle_Tongue_Interface_Module_Basic;

                    if (module != null && module.tongues != null && module.tongues.Count > 0 && module.tongues[0] != null && module.tongues[0].tongue != null)
                    {
                        GameObject prefabTongue = module.tongues[0].tongue.gameObject;

                        if (prefabTongue != null)
                        {
                            Transform tongueParent = prefabTongue.transform.parent != null ? prefabTongue.transform.parent : barnacle.transform;

                            prefabTongue.transform.localPosition = Vector3.zero;

                            var tongueTransforms = prefabTongue.GetComponentsInChildren<Transform>();
                            if (tongueTransforms != null)
                            {
                                foreach (Transform child in tongueTransforms)
                                {
                                    if (child != null) child.localPosition = Vector3.zero;
                                }
                            }

                            int tonguesToAdd = 3;
                            for (int j = 0; j < tonguesToAdd; j++)
                            {
                                GameObject newTongueGO = UnityEngine.Object.Instantiate(prefabTongue, tongueParent);
                                if (newTongueGO != null)
                                {
                                    newTongueGO.name = "Barnacle_Tongue_" + module.tongues.Count;
                                    newTongueGO.transform.localPosition = Vector3.zero;
                                    newTongueGO.transform.localRotation = Quaternion.identity;
                                    newTongueGO.transform.localScale = prefabTongue.transform.localScale;

                                    var newTongueTransforms = newTongueGO.GetComponentsInChildren<Transform>();
                                    if (newTongueTransforms != null)
                                    {
                                        foreach (Transform child in newTongueTransforms)
                                        {
                                            if (child != null) child.localPosition = Vector3.zero;
                                        }
                                    }

                                    Barnacle_Tongue_Basic newTongue = new Barnacle_Tongue_Basic();
                                    newTongue.tongue = newTongueGO.transform;
                                    newTongue.canReachForAir = true;
                                    module.tongues.Add(newTongue);
                                }
                            }

                            module.tongueScatter = 3.15f;
                            module.scanDistance = 10f;
                            module.tongueAimTime = 2;
                            module.tongueSpeed += 0.1f;
                            module.Initialize(barnacle);
                        }
                    }

                    var barnacleTransforms = barnacle.GetComponentsInChildren<Transform>();
                    if (barnacleTransforms != null)
                    {
                        foreach (Transform child in barnacleTransforms)
                        {
                            if (child != null && child != barnacle.transform && child.parent == barnacle.transform)
                            {
                                child.localPosition = Vector3.zero;
                            }
                        }
                    }

                    GameObject prefab2 = GetPrefab("AirBarnacle", level);
                    if (prefab2 != null)
                    {
                        UnityEngine.Object.Instantiate(prefab2, barnacle.transform.position, Quaternion.identity, instance.transform);
                    }

                    if (barnacle.gameObject != null)
                    {
                        barnacle.gameObject.SetActive(true);
                    }

                    barnacle.enabled = true;
                    barnacle.Start();
                }

                instance.SetActive(true);
            }

            return position;
        }
    }

    public class Ladder : PathNode
    {
        public override float Weight => 0.8f;
        public override float MinHorizontalDistance => 3f;
        public override float MaxHorizontalDistance => 5f;
        public override float MinVerticalDistance => 0f;
        public override float MaxVerticalDistance => 5f;

        public override Vector3 Spawn(Vector3 position, Quaternion rotation, M_Level level)
        {
            GameObject prefab = GetPrefab("Cube", level);
            if (prefab != null)
            {
                var platform = SpawnVoxelPlatform(position, rotation, new Vector3Int(1, 10, 1), prefab, level.transform, 2);
                platform.transform.position += Vector3.up * 10;
                return position + (Vector3.up * 20f);
            }
            return position;
        }
    }

    public class Handhold : PathNode
    {
        public override bool OffshootOnly => true;
        public override float MinHorizontalDistance => 17f;
        public override float MaxHorizontalDistance => 25f;
        public override float MinVerticalDistance => 15f;
        public override float MaxVerticalDistance => 23f;

        public override Vector3 Spawn(Vector3 position, Quaternion rotation, M_Level level)
        {

            GameObject prefab = EndlessConfiguration.GetCachedObject<GameObject>("Item_Artifact_Remote_Handhold");
            if (prefab != null)
            {
                GameObject instance = UnityEngine.Object.Instantiate(prefab, position, rotation, level.transform);
                var timerModule = instance.GetComponent<UT_DestroyAfterTime>();
                if (timerModule != null)
                {
                    timerModule.enabled = false;
                }
                var otherModule = instance.GetComponent<UT_Timer>();
                if (otherModule != null)
                {
                    otherModule.endEvent.Invoke();
                    otherModule.enabled = false;
                }
                foreach (ParticleSystem particles in instance.GetComponentsInChildren<ParticleSystem>())
                {
                    SetTheFuckingColorOfThisParticleSystem(particles, Color.black);
                }
                SetTheFuckingColorOfThisMesh(instance, Color.red);
                var handholdModule = instance.GetComponent<CL_Handhold>();
                if (handholdModule != null)
                {
                    handholdModule.jumpMult = 0.3f;
                    handholdModule.velocityMult = 1;
                    HandholdModule_JumpBoost module = handholdModule.modules.Find(test => test is HandholdModule_JumpBoost) as HandholdModule_JumpBoost;
                    module.boostInLookDir = 1;
                    module.fovAdjust *= 5;
                    module.doubleHandBoost = false;
                    module.jumpBoost *= 4;
                    RecordHandholdLaunchVelocity(handholdModule, rotation, 14f);
                }

                AddProximityActivator(instance);
            }
            return position;
        }
    }

    public class Handhold2 : PathNode
    {
        public override float MinHorizontalDistance => 5f;
        public override float MaxHorizontalDistance => 13f;
        public override float MinVerticalDistance => 5f;
        public override float MaxVerticalDistance => 13f;

        public override Vector3 Spawn(Vector3 position, Quaternion rotation, M_Level level)
        {
            GameObject prefab = EndlessConfiguration.GetCachedObject<GameObject>("Item_Artifact_Remote_Handhold"); ;
            if (prefab != null)
            {
                GameObject instance = UnityEngine.Object.Instantiate(prefab, position, rotation, level.transform);
                var timerModule = instance.GetComponent<UT_DestroyAfterTime>();
                if (timerModule != null)
                {
                    timerModule.enabled = false;
                }
                var otherModule = instance.GetComponent<UT_Timer>();
                if (otherModule != null)
                {
                    otherModule.enabled = false;
                }

                CL_Handhold handholdModule = instance.GetComponent<CL_Handhold>();
                if (handholdModule != null) RecordHandholdLaunchVelocity(handholdModule, rotation, 14f);

                AddProximityActivator(instance);
            }
            return position;
        }
    }

    public class HandholdChain : PathNode
    {
        public override float Weight => 0.3f;
        public override float MinHorizontalDistance => 3f;
        public override float MaxHorizontalDistance => 6f;
        public override float MinVerticalDistance => 2f;
        public override float MaxVerticalDistance => 5f;

        public override Vector3 Spawn(Vector3 position, Quaternion rotation, M_Level level)
        {
            GameObject prefab = EndlessConfiguration.GetCachedObject<GameObject>("Item_Artifact_Remote_Handhold");
            if (prefab == null) return position;

            Vector3 currentPos = position;
            int count = UnityEngine.Random.Range(5, 10);

            for (int i = 0; i < count; i++)
            {
                GameObject instance = UnityEngine.Object.Instantiate(prefab, currentPos, rotation, level.transform);

                var timerModule = instance.GetComponent<UT_DestroyAfterTime>();
                if (timerModule != null) timerModule.enabled = false;

                var otherModule = instance.GetComponent<UT_Timer>();
                if (otherModule != null)
                {
                    otherModule.endEvent.Invoke();
                    otherModule.enabled = false;
                }
                foreach (ParticleSystem particles in instance.GetComponentsInChildren<ParticleSystem>())
                {
                    SetTheFuckingColorOfThisParticleSystem(particles, Color.magenta);


                }
                var handholdModule = instance.GetComponent<CL_Handhold>();

                if (handholdModule != null)
                {
                    handholdModule.jumpMult = 0.3f;
                    handholdModule.velocityMult = 1f;
                    HandholdModule_JumpBoost module = handholdModule.modules.Find(test => test is HandholdModule_JumpBoost) as HandholdModule_JumpBoost;
                    module.boostInLookDir = 1;
                    module.fovAdjust /= 2;
                    module.doubleHandBoost = false;
                    //module.jumpBoost *= 0.5f;
                    RecordHandholdLaunchVelocity(handholdModule, rotation, 14f);
                }


                SetTheFuckingColorOfThisMesh(instance, Color.magenta / 2);


                AddProximityActivator(instance);

                if (i < count - 1)
                {
                    Vector3 forwardDir = rotation * Vector3.forward;
                    forwardDir = Quaternion.Euler(UnityEngine.Random.Range(-45f, 45f), UnityEngine.Random.Range(-70f, 70f), 0) * forwardDir;
                    currentPos += forwardDir.normalized * UnityEngine.Random.Range(4f, 7f) + Vector3.up * UnityEngine.Random.Range(2f, 4f);
                    rotation = Quaternion.LookRotation(forwardDir);
                }
            }

            return currentPos;
        }
    }

    public class HighJumpPlatform : PathNode
    {
        public override float MinHorizontalDistance => 1f;
        public override float MaxHorizontalDistance => 2f;
        public override float MinVerticalDistance => 5f;
        public override float MaxVerticalDistance => 8f;

        public override Vector3 Spawn(Vector3 position, Quaternion rotation, M_Level level)
        {
            GameObject prefab = GetPrefab("Cube", level);
            if (prefab != null)
            {
                SpawnVoxelPlatform(position, rotation, new Vector3Int(2, 1, 2), prefab, level.transform);
            }
            return position;
        }
    }

    public class ItemPlatform : PathNode
    {
        public override bool OffshootOnly => true;
        public override float MinHorizontalDistance => 1f;
        public override float MaxHorizontalDistance => 2f;
        public override float MinVerticalDistance => 5f;
        public override float MaxVerticalDistance => 8f;

        public List<string> itemIds = new List<string> { "Item_Beans", "Item_Beans", "Item_Beans", "Item_Rebar_Bone", "Item_Beans", "Item_Rebar_Bone", "Item_Rebar_Bone", "Item_Rebar_Explosive", "Item_Injector", "Item_Food_Bar", "Item_Food_Bar", "Item_Pillbottle", "Item_Wishbone" };

        public override Vector3 Spawn(Vector3 position, Quaternion rotation, M_Level level)
        {
            GameObject prefab = GetPrefab("Cube", level);
            if (prefab != null)
            {
                Transform Cube = SpawnVoxelPlatform(position, rotation, new Vector3Int(2, 1, 2), prefab, level.transform).GetComponentInChildren<Transform>();
                SpawnRandomItems(position, rotation, level, Cube);
            }
            return position;
        }

        private void SpawnRandomItems(Vector3 position, Quaternion rotation, M_Level level, Transform parent)
        {
            if (itemIds == null || itemIds.Count == 0) return;

            int itemCount = UnityEngine.Random.Range(1, 4);

            for (int i = 0; i < itemCount; i++)
            {
                string randomId = itemIds[UnityEngine.Random.Range(0, itemIds.Count)];
                var prePrefab = EndlessConfiguration.GetCachedObject<Item_Object>(randomId);

                GameObject itemPrefab = null;
                if (prePrefab != null)
                    itemPrefab = prePrefab.gameObject;

                if (itemPrefab != null)
                {
                    Vector3 localOffset = new Vector3(UnityEngine.Random.Range(-0.8f, 0.8f), 0.6f, UnityEngine.Random.Range(-0.8f, 0.8f));
                    Vector3 spawnPosition = position + (rotation * localOffset);

                    UnityEngine.Object.Instantiate(itemPrefab, spawnPosition, rotation, parent).GetComponent<Rigidbody>().useGravity = false;
                }
                else
                {
                    {
                        Vector3 localOffset = new Vector3(UnityEngine.Random.Range(-0.8f, 0.8f), 0.6f, UnityEngine.Random.Range(-0.8f, 0.8f));
                        Vector3 spawnPosition = position + (rotation * localOffset);

                        WishboneStuff.SpawnWishbone(spawnPosition);
                    }
                }
            }
        }
    }
    //public class ChainPlatform : PathNode
    //{
    //    public override float Weight => 0.1f;
    //    public override float MinHorizontalDistance => 10f;
    //    public override float MaxHorizontalDistance => 16f;
    //    public override float MinVerticalDistance => 0f;
    //    public override float MaxVerticalDistance => 0f;

    //    public override Vector3 Spawn(Vector3 position, Quaternion rotation, M_Level level)
    //    {
    //        Vector3 newPos = position + Vector3.up * 10;
    //        GameObject prefab = GetPrefab("Cube", level);
    //        if (prefab != null)
    //        {
    //            SpawnVoxelPlatform(newPos, rotation, new Vector3Int(1, 1, 1), prefab, level.transform);
    //        }
    //        CreateRope(EndlessConfiguration.GetCachedObject<M_Level>("M4_Abyss_Transit_02").gameObject, 4, newPos);

    //        return position;
    //    }
    //}

    private static List<PathNode> availableNodes = new List<PathNode>();
    private static List<PathNode> mainPathNodes = new List<PathNode>();
    private static List<PathNode> offshootNodes = new List<PathNode>();
    private static bool initialized = false;

    public static void Init()
    {
        if (initialized) return;
        initialized = true;
        availableNodes.Clear();
        mainPathNodes.Clear();
        offshootNodes.Clear();

        var nodeTypes = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => typeof(PathNode).IsAssignableFrom(t) && !t.IsAbstract && t != typeof(PathNode));

        foreach (var type in nodeTypes)
        {
            if (Activator.CreateInstance(type) is PathNode node)
            {
                availableNodes.Add(node);
                offshootNodes.Add(node);
                if (!node.OffshootOnly)
                {
                    mainPathNodes.Add(node);
                }
            }
        }
    }

    public static void Generate(M_Level level)
    {
        ProcGen instance = new ProcGen();
        instance.StartGeneration(level);
    }

    public virtual void StartGeneration(M_Level level)
    {
        if (!initialized) Init();

        Vector3 startPos = level.transform.position + (Vector3.up * 1.5f) + (Vector3.forward * 9);
        Vector3 targetPos = startPos + (Vector3.up * targetHeight);

        int pathCount = UnityEngine.Random.Range(minPaths, maxPaths + 1);

        for (int i = 0; i < pathCount; i++)
        {
            GeneratePath(startPos, targetPos, level);
        }

        CreateWinButton(targetPos, level);
    }

    private PathNode GetRandomNode(List<PathNode> nodes)
    {
        float totalWeight = nodes.Sum(n => n.Weight);
        float randomVal = UnityEngine.Random.Range(0, totalWeight);
        float currentWeight = 0;

        foreach (var node in nodes)
        {
            currentWeight += node.Weight;
            if (randomVal <= currentWeight)
            {
                return node;
            }
        }
        return nodes.Last();
    }

    protected virtual Vector3 GetBallisticJumpOffset(Vector3 direction, Vector3 launchVelocityAdjustment)
    {
        return GetBallisticJumpOffset(direction, launchVelocityAdjustment, float.NaN);
    }

    protected virtual Vector3 GetBallisticJumpOffset(Vector3 direction, Vector3 launchVelocityAdjustment, float targetVerticalDistance)
    {
        float angle = UnityEngine.Random.Range(minJumpAngle, maxJumpAngle) * Mathf.Deg2Rad;
        Vector3 baseLaunchVelocity = direction.normalized * (baseJumpForce * Mathf.Cos(angle)) + Vector3.up * (baseJumpForce * Mathf.Sin(angle));
        Vector3 launchVelocity = baseLaunchVelocity + launchVelocityAdjustment;
        float verticalVelocity = launchVelocity.y;
        float maximumHeight = (verticalVelocity * verticalVelocity) / (2f * gravity);
        float verticalDistance = float.IsNaN(targetVerticalDistance) ? UnityEngine.Random.Range(minVerticalJumpFraction, maxVerticalJumpFraction) * maximumHeight : Mathf.Min(targetVerticalDistance, maximumHeight);
        float discriminant = (verticalVelocity * verticalVelocity) - (2f * gravity * verticalDistance);
        if (discriminant < 0f) discriminant = 0f;
        float flightTime = (verticalVelocity + Mathf.Sqrt(discriminant)) / gravity;
        Vector3 horizontalVelocity = Vector3.ProjectOnPlane(launchVelocity, Vector3.up);
        Vector3 horizontalOffset = horizontalVelocity * flightTime;
        if (horizontalOffset.magnitude < 3f) horizontalOffset = direction.normalized * 3f;
        return horizontalOffset + Vector3.up * verticalDistance;
    }

    protected virtual void GeneratePath(Vector3 start, Vector3 target, M_Level level)
    {
        Vector3 currentStartPos = start;
        PathNode currentNode = new BasicPlatform();
        Vector2 centerXZ = new Vector2(start.x, start.z);

        float height1 = start.y + (target.y - start.y) / 3f;
        float height2 = start.y + (target.y - start.y) * 2f / 3f;

        Vector2 phase3Dir = Vector2.zero;
        bool phase3Set = false;

        var validMainNodes = mainPathNodes.Where(n => n.Theme == activeTheme).ToList();
        if (validMainNodes.Count == 0) validMainNodes = mainPathNodes;

        while (currentStartPos.y < target.y)
        {
            PathNode nextNode = GetRandomNode(validMainNodes);

            Vector2 currXZ = new Vector2(currentStartPos.x, currentStartPos.z);
            Vector2 randomDir = UnityEngine.Random.insideUnitCircle.normalized;

            if (currentStartPos.y >= height1 && currentStartPos.y < height2)
            {
                Vector2 dirToCenter = (centerXZ - currXZ).normalized;
                randomDir = (randomDir + dirToCenter * centerBias).normalized;
            }
            else if (currentStartPos.y >= height2)
            {
                if (!phase3Set)
                {
                    phase3Dir = (centerXZ - currXZ).normalized;
                    phase3Set = true;
                }
                randomDir = (randomDir + phase3Dir * centerBias).normalized;
            }

            Vector3 direction = new Vector3(randomDir.x, 0, randomDir.y);
            Quaternion rotation = Quaternion.identity;
            if (direction.sqrMagnitude > 0.001f)
            {
                rotation = Quaternion.LookRotation(direction.normalized);
            }

            Vector3 currentEndPos = currentNode.SpawnAndGetExitPoint(currentStartPos, rotation, level);

            if (UnityEngine.Random.value < offshootChance)
            {
                GenerateOffshoot(currentEndPos, direction.normalized, level);
            }

            Vector3 jumpOffset = GetBallisticJumpOffset(direction, currentNode.GetLaunchVelocityAdjustment());
            Vector2 endXZ = new Vector2(currentEndPos.x, currentEndPos.z);
            Vector2 nextXZ = endXZ + new Vector2(jumpOffset.x, jumpOffset.z);

            if (Vector2.Distance(nextXZ, centerXZ) > maxRadius)
            {
                nextXZ = centerXZ + (nextXZ - centerXZ).normalized * maxRadius;
            }

            Vector3 nextPos = new Vector3(nextXZ.x, currentEndPos.y + jumpOffset.y, nextXZ.y);

            if (nextPos.y > target.y)
            {
                nextPos.y = target.y;
            }

            currentStartPos = nextPos;
            currentNode = nextNode;
        }

        Vector2 targetXZ = new Vector2(target.x, target.z);

        while (Vector2.Distance(new Vector2(currentStartPos.x, currentStartPos.z), targetXZ) > 6f)
        {
            PathNode nextNode = new BasicPlatform();

            Vector2 currXZ = new Vector2(currentStartPos.x, currentStartPos.z);
            Vector2 dirToTarget = (targetXZ - currXZ).normalized;

            Vector3 direction = new Vector3(dirToTarget.x, 0, dirToTarget.y);
            Quaternion rotation = Quaternion.identity;
            if (direction.sqrMagnitude > 0.001f)
            {
                rotation = Quaternion.LookRotation(direction.normalized);
            }

            Vector3 currentEndPos = currentNode.SpawnAndGetExitPoint(currentStartPos, rotation, level);

            Vector3 jumpOffset = GetBallisticJumpOffset(direction, currentNode.GetLaunchVelocityAdjustment(), target.y - currentEndPos.y);
            Vector2 endXZ = new Vector2(currentEndPos.x, currentEndPos.z);
            float remainingDistance = Vector2.Distance(endXZ, targetXZ);
            Vector2 nextXZ = endXZ + dirToTarget * Mathf.Min(remainingDistance, new Vector2(jumpOffset.x, jumpOffset.z).magnitude);

            if (Vector2.Distance(nextXZ, centerXZ) > maxRadius)
            {
                nextXZ = centerXZ + (nextXZ - centerXZ).normalized * maxRadius;
            }

            Vector3 nextPos = new Vector3(nextXZ.x, currentEndPos.y + jumpOffset.y, nextXZ.y);

            currentStartPos = nextPos;
            currentNode = nextNode;
        }

        currentNode.SpawnAndGetExitPoint(currentStartPos, Quaternion.identity, level);
    }

    protected virtual void GenerateOffshoot(Vector3 startPos, Vector3 mainDirection, M_Level level)
    {
        int length = UnityEngine.Random.Range(2, 6);
        Vector3 currentStartPos = startPos;
        PathNode currentNode = new BasicPlatform();

        Vector2 centerXZ = new Vector2(level.transform.position.x, level.transform.position.z + 9f);

        var validOffshoot = offshootNodes.Where(n => n.Theme == activeTheme).ToList();
        if (validOffshoot.Count == 0) validOffshoot = offshootNodes;

        Vector2 baseDir2D = new Vector2(mainDirection.x, mainDirection.z);
        if (baseDir2D.sqrMagnitude < 0.001f)
        {
            baseDir2D = UnityEngine.Random.insideUnitCircle.normalized;
        }
        else
        {
            baseDir2D.Normalize();
        }

        float turnAngle = UnityEngine.Random.Range(0, 2) == 0 ? UnityEngine.Random.Range(60f, 120f) : UnityEngine.Random.Range(-120f, -60f);
        Vector2 offshootDir2D = Quaternion.Euler(0, 0, turnAngle) * baseDir2D;

        for (int i = 0; i < length; i++)
        {
            PathNode nextNode = GetRandomNode(validOffshoot);

            Vector2 stepDir = (offshootDir2D + UnityEngine.Random.insideUnitCircle * 0.2f).normalized;
            Vector3 direction = new Vector3(stepDir.x, 0, stepDir.y);
            Quaternion rotation = Quaternion.identity;
            if (direction.sqrMagnitude > 0.001f)
            {
                rotation = Quaternion.LookRotation(direction.normalized);
            }

            Vector3 currentEndPos = currentNode.SpawnAndGetExitPoint(currentStartPos, rotation, level);
            Vector3 jumpOffset = GetBallisticJumpOffset(direction, currentNode.GetLaunchVelocityAdjustment());

            Vector2 endXZ = new Vector2(currentEndPos.x, currentEndPos.z);
            Vector2 nextXZ = endXZ + new Vector2(jumpOffset.x, jumpOffset.z);

            if (Vector2.Distance(nextXZ, centerXZ) > maxRadius)
            {
                nextXZ = centerXZ + (nextXZ - centerXZ).normalized * maxRadius;
            }

            Vector3 nextPos = new Vector3(nextXZ.x, currentEndPos.y + jumpOffset.y, nextXZ.y);

            currentStartPos = nextPos;
            currentNode = nextNode;
        }

        currentNode.SpawnAndGetExitPoint(currentStartPos, Quaternion.identity, level);
    }

    public static void AddProximityActivator(GameObject instance)
    {
        instance.SetActive(false);
        GameObject parent = new GameObject(instance.name + "_Activator");
        parent.transform.position = instance.transform.position;
        parent.transform.SetParent(instance.transform.parent, true);
        parent.AddComponent<ProximityChildActivator>();
        instance.transform.SetParent(parent.transform, true);
    }

    public static void GeneratePlatform(Vector3 position, int size, GameObject cube)
    {
        GameObject root = new GameObject("BasicPlatform");
        root.transform.position = position;

        float center = (size - 1) / 2f;
        float maxRadius = size / 2f;
        float seedX = UnityEngine.Random.Range(0f, 500f);
        float seedZ = UnityEngine.Random.Range(0f, 500f);
        int originX = Mathf.RoundToInt(position.x) - (size / 2);
        int originZ = Mathf.RoundToInt(position.z) - (size / 2);
        int posY = Mathf.RoundToInt(position.y);

        for (int x = 0; x < size; x++)
        {
            for (int z = 0; z < size; z++)
            {
                float distNorm = Vector2.Distance(new Vector2(x, z), new Vector2(center, center)) / maxRadius;
                float noise = Mathf.PerlinNoise(x * 0.3f + seedX, z * 0.3f + seedZ);
                if (distNorm <= 0.45f || (distNorm <= 1.1f && noise > distNorm * 0.65f))
                {
                    Vector3Int gridPos = new Vector3Int(originX + x, posY, originZ + z);
                    UnityEngine.Object.Instantiate(cube, gridPos, Quaternion.identity, root.transform);
                }
            }
        }

    }

    private static GameObject SpawnVoxelPlatform(Vector3 position, Quaternion rotation, Vector3Int size, GameObject cubePrefab, Transform parent, float scale = 1)
    {
        GameObject root = new GameObject("VoxelPlatform");
        root.transform.SetParent(parent);
        root.transform.position = position;
        root.transform.rotation = rotation;

        Vector3 offset = new Vector3((size.x * scale - 1) * 0.5f, (size.y * scale - 1) * 0.5f, (size.z * scale - 1) * 0.5f);

        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                for (int z = 0; z < size.z; z++)
                {
                    GameObject voxel = UnityEngine.Object.Instantiate(cubePrefab, root.transform);
                    voxel.transform.localPosition = new Vector3(x * scale, y * scale, z * scale) - offset;
                    voxel.transform.localRotation = Quaternion.identity;
                }
            }
        }

        return root;
    }

    private void CreateWinButton(Vector3 position, M_Level level)
    {
        PathNode winPlat = new BasicPlatform();
        winPlat.Spawn(position, Quaternion.identity, level);

        GameObject diamond = GameObject.CreatePrimitive(PrimitiveType.Cube);
        diamond.transform.SetParent(level.transform);
        diamond.transform.position = position + new Vector3(0f, 2.5f, 0f);
        diamond.transform.rotation = Quaternion.Euler(45, 0, 45);

        Material whiteMat = new Material(Shader.Find("Unlit/Color"));
        whiteMat.color = Color.white;
        diamond.GetComponent<MeshRenderer>().material = whiteMat;

        CL_Handhold btn = diamond.AddComponent<CL_Handhold>();
        btn.activeEvent = new UnityEvent();
        btn.activeEvent.AddListener(() => { CL_GameManager.gMan.Win(); });
        ObjectTagger tagger = btn.GetComponent<ObjectTagger>() ?? btn.gameObject.AddComponent<ObjectTagger>();
        tagger.AddTag("Handhold");

    }

    public static ENV_Rope CreateRope(GameObject source, int length, Vector3 position)
    {
        ENV_Rope original = source.GetComponentInChildren<ENV_Rope>(true);
        if (original == null || original.meshRoot == null || original.meshRoot.transform.childCount == 0)
        {
            return null;
        }

        GameObject ropeObj = new GameObject("Custom_ENV_Rope");
        ropeObj.transform.position = position;
        ENV_Rope newRope = ropeObj.AddComponent<ENV_Rope>();

        newRope.nodeDistance = original.nodeDistance * 3f;
        newRope.gravity = original.gravity;
        newRope.drag = original.drag;
        newRope.tightness = original.tightness;
        newRope.tightMult = original.tightMult;
        newRope.constraintIterations = original.constraintIterations;
        newRope.targetRigidbodyGravity = original.targetRigidbodyGravity;
        newRope.layerMask = original.layerMask;
        newRope.grabVelocityTransfer = original.grabVelocityTransfer;
        newRope.grabVelocityMult = original.grabVelocityMult;
        newRope.swingAudio01 = original.swingAudio01;
        newRope.swingAudio02 = original.swingAudio02;
        newRope.swingAudioVolume = original.swingAudioVolume;
        newRope.swingAudioVelocityMagnitude = original.swingAudioVelocityMagnitude;
        newRope.swingAudioPitchMin = original.swingAudioPitchMin;
        newRope.swingAudioPitchMax = original.swingAudioPitchMax;

        GameObject meshRootObj = new GameObject("MeshRoot");
        meshRootObj.transform.SetParent(ropeObj.transform);
        meshRootObj.transform.position = position;
        newRope.meshRoot = meshRootObj;

        int origChildCount = original.meshRoot.transform.childCount;
        for (int i = 0; i < length - 1; i++)
        {
            Transform template = original.meshRoot.transform.GetChild(i % origChildCount);
            GameObject nodeInst = UnityEngine.Object.Instantiate(template.gameObject, meshRootObj.transform);
            nodeInst.transform.position = position;
        }

        GameObject targetObj = new GameObject("Target");
        targetObj.transform.SetParent(ropeObj.transform);
        targetObj.transform.position = position + (Vector3.down * (length * newRope.nodeDistance));
        targetObj.AddComponent<Rigidbody>();
        newRope.target = targetObj.transform;


        return newRope;
    }
    private static readonly string[] ColorPropertyNames = new string[]
    {
        "_Color",
        "_BaseColor",
        "_TintColor",
        "_EmissionColor",
        "_SpecColor",
        "_MainColor",
        "_UnlitColor",
        "_FaceColor",
        "_OutlineColor",
        "_GlowColor",
        "_DetailColor"
    };

    public static void SetTheFuckingColorOfThisParticleSystem(ParticleSystem ps, Color targetColor, bool includeChildren = true)
    {
        if (ps == null) return;

        ParticleSystem[] systems = includeChildren ? ps.GetComponentsInChildren<ParticleSystem>(true) : new[] { ps };

        foreach (var system in systems)
        {
            ParticleSystem.MinMaxGradient gradient = new ParticleSystem.MinMaxGradient(targetColor);

            var main = system.main;
            main.startColor = gradient;

            var colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = gradient;

            var colorBySpeed = system.colorBySpeed;
            colorBySpeed.enabled = true;
            colorBySpeed.color = gradient;

            var trails = system.trails;
            if (trails.enabled)
            {
                trails.colorOverLifetime = gradient;
                trails.colorOverTrail = gradient;
            }

            var customData = system.customData;
            if (customData.enabled)
            {
                customData.SetColor(ParticleSystemCustomData.Custom1, targetColor);
                customData.SetColor(ParticleSystemCustomData.Custom2, targetColor);
            }

            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                MaterialPropertyBlock block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                block.SetColor("_Color", targetColor);
                block.SetColor("_TintColor", targetColor);
                block.SetColor("_BaseColor", targetColor);
                block.SetColor("_EmissionColor", targetColor);
                renderer.SetPropertyBlock(block);
            }

            ParticleSystem.Particle[] particles = new ParticleSystem.Particle[system.particleCount];
            int particleCount = system.GetParticles(particles);
            for (int i = 0; i < particleCount; i++)
            {
                particles[i].startColor = targetColor;
            }
            system.SetParticles(particles, particleCount);
        }
    }

    public static void SetTheFuckingColorOfThisMesh(GameObject target, Color targetColor, bool includeChildren = true)
    {
        if (target == null) return;

        Transform[] transforms = includeChildren ? target.GetComponentsInChildren<Transform>(true) : new[] { target.transform };

        foreach (var t in transforms)
        {
            GameObject go = t.gameObject;

            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                MaterialPropertyBlock block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                foreach (string prop in ColorPropertyNames)
                {
                    block.SetColor(prop, targetColor);
                }
                renderer.SetPropertyBlock(block);
            }

            Graphic graphic = go.GetComponent<Graphic>();
            if (graphic != null)
            {
                graphic.color = targetColor;
            }

            CanvasRenderer canvasRenderer = go.GetComponent<CanvasRenderer>();
            if (canvasRenderer != null)
            {
                canvasRenderer.SetColor(targetColor);
            }
        }
    }
}
