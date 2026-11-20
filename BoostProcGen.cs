using Celestial.CustomEndless;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
//Unfinished, I burnt myself out on all the (sparkles emoji) math (sparkles emoji)
public class BoostProcGen
{
    public int targetHeight = 400;
    public float baseJumpForce = 14f;
    public float gravity = 9.81f;
    public float playerMass = 1.0f;
    public float fixedDeltaTime = 0.02f;
    public int hardNodeCap = 400;

    public int maxItemsPerJump = 2;
    public int maxInventorySize = 10;
    public int minExcludedItems = 0;
    public int maxExcludedItems = 2;
    public int maxGroundedItemsPerJump = 2;

    public float startPosVerticalOffset = 1.5f;
    public float startPosForwardOffset = 9f;
    public float eggSpawnVerticalOffset = 1f;
    public float itemLocalOffsetMin = -1f;
    public float itemLocalOffsetMax = 1f;
    public float winButtonVerticalOffset = 2.5f;
    public float winButtonRotationDeg = 45f;

    public string unlitColorShader = "Unlit/Color";
    public string tagHandhold = "Handhold";
    public string cubePrefabName = "Cube";
    public string boostPlatformName = "BoostPlatform";
    public string defaultTheme = "boost";
    public string voxelPlatformName = "VoxelPlatform";
    public string activeTheme = "boost";

    public Dictionary<string, ItemDef> itemDatabase = new Dictionary<string, ItemDef>
    {
        { "Rebar", new ItemDef { Id = "Item_Rebar_Bone", Force = 12f, IsGrounded = false } },
        { "Wishbone", new ItemDef { Id = "Item_Wishbone", Force = 80f, IsGrounded = false } },
    };

    private static List<BoostPathNode> availableNodes = new List<BoostPathNode>();
    private static bool initialized = false;

    public class ItemDef
    {
        public string Id;
        public float Force;
        public bool IsGrounded;
    }

    public abstract class BoostPathNode
    {
        public BoostProcGen genRef;
        public virtual string Theme => genRef != null ? genRef.defaultTheme : "boost";
        public virtual float Weight => 1f;
        public virtual Vector3 Extents => new Vector3(0.5f, 0.5f, 0.5f);

        public abstract List<ItemDef> GenerateItems();
        public abstract Vector3 Spawn(BoostNodeData data, M_Level level);

        public virtual Vector3 GetExitPoint(BoostNodeData data) { return data.Position; }

        public static GameObject GetPrefab(string name, M_Level level = null)
        {
            return ProcGen.PathNode.GetPrefab(name, level);
        }

        protected GameObject SpawnVoxelPlatform(Vector3 position, Quaternion rotation, Vector3Int size, GameObject cubePrefab, Transform parent, float scale = 1)
        {
            string pName = genRef != null ? genRef.voxelPlatformName : "VoxelPlatform";
            GameObject root = new GameObject(pName);
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
    }

    public class Platform3x3 : BoostPathNode
    {
        public override float Weight => 2f;
        public override Vector3 Extents => new Vector3(1.5f, 0.5f, 1.5f);

        public override List<ItemDef> GenerateItems()
        {
            List<ItemDef> items = new List<ItemDef>();
            int count = UnityEngine.Random.Range(0, 5);
            for (int i = 0; i < count; i++)
            {
                if (genRef != null && genRef.itemDatabase.TryGetValue("Rebar", out ItemDef template))
                    items.Add(new ItemDef { Id = template.Id, Force = template.Force, IsGrounded = template.IsGrounded });
            }
            return items;
        }

        public override Vector3 Spawn(BoostNodeData data, M_Level level)
        {
            GameObject prefab = GetPrefab(genRef != null ? genRef.cubePrefabName : "Cube", level);
            if (prefab == null) return data.Position;

            var root = SpawnVoxelPlatform(data.Position, data.Rotation, new Vector3Int(3, 1, 3), prefab, level.transform);
            root.name = data.ParentName;

            float offsetMin = genRef != null ? genRef.itemLocalOffsetMin : -1f;
            foreach (var itemDef in data.ProvidedItems)
            {
                var itemPrefabCached = EndlessConfiguration.GetCachedObject<Item_Object>(itemDef.Id);
                GameObject itemPrefab = itemPrefabCached != null ? itemPrefabCached.gameObject : null;

                Vector3 localOffset = new Vector3(UnityEngine.Random.Range(offsetMin, 1f), 1f, UnityEngine.Random.Range(offsetMin, 1f));
                Vector3 spawnPosition = data.Position + (data.Rotation * localOffset);

                if (itemPrefab != null)
                {
                    var item = UnityEngine.Object.Instantiate(itemPrefab, spawnPosition, data.Rotation, root.transform);
                    var rb = item.GetComponent<Rigidbody>();
                    if (rb != null) rb.useGravity = false;
                }
                else WishboneStuff.SpawnWishbone(spawnPosition);
            }
            return data.Position;
        }
    }

    public class Platform1x1 : BoostPathNode
    {
        public override float Weight => 1f;
        public override Vector3 Extents => new Vector3(0.5f, 0.5f, 0.5f);

        public override List<ItemDef> GenerateItems()
        {
            List<ItemDef> items = new List<ItemDef>();
            if (genRef != null && genRef.itemDatabase.TryGetValue("Wishbone", out ItemDef template))
                items.Add(new ItemDef { Id = template.Id, Force = template.Force, IsGrounded = template.IsGrounded });
            return items;
        }

        public override Vector3 Spawn(BoostNodeData data, M_Level level)
        {
            GameObject prefab = GetPrefab("Cube", level);
            if (prefab == null) return data.Position;

            var root = SpawnVoxelPlatform(data.Position, data.Rotation, new Vector3Int(1, 1, 1), prefab, level.transform);
            root.name = data.ParentName;

            float eggOffset = genRef != null ? genRef.eggSpawnVerticalOffset : 1f;
            foreach (var itemDef in data.ProvidedItems)
            {
                var itemPrefabCached = EndlessConfiguration.GetCachedObject<Item_Object>(itemDef.Id);
                GameObject itemPrefab = itemPrefabCached != null ? itemPrefabCached.gameObject : null;

                Vector3 spawnPosition = data.Position + (data.Rotation * new Vector3(0f, eggOffset, 0f));

                if (itemPrefab != null)
                {
                    var item = UnityEngine.Object.Instantiate(itemPrefab, spawnPosition, data.Rotation, root.transform);
                    var rb = item.GetComponent<Rigidbody>();
                    if (rb != null) rb.useGravity = false;
                }
                else WishboneStuff.SpawnWishbone(spawnPosition);
            }
            return data.Position;
        }
    }

    public class BoostNodeData
    {
        public BoostPathNode NodeType;
        public Vector3 Position;
        public Quaternion Rotation;
        public List<ItemDef> ProvidedItems = new List<ItemDef>();
        public string ParentName = "BoostPlatform";

        public BoostNodeData PathPrev;
        public BoostNodeData PathNext;

        public Vector3 GetExitPoint() { return NodeType.GetExitPoint(this); }
    }

    public void Init()
    {
        if (initialized) return;
        initialized = true;
        availableNodes.Clear();

        var nodeTypes = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => typeof(BoostPathNode).IsAssignableFrom(t) && !t.IsAbstract);

        foreach (var type in nodeTypes)
        {
            if (Activator.CreateInstance(type) is BoostPathNode node)
            {
                node.genRef = this;
                if (node.Theme == activeTheme) availableNodes.Add(node);
            }
        }
    }

    private BoostPathNode GetRandomNode()
    {
        float totalWeight = availableNodes.Sum(n => n.Weight);
        float randomVal = UnityEngine.Random.Range(0, totalWeight);
        float currentWeight = 0;

        foreach (var node in availableNodes)
        {
            currentWeight += node.Weight;
            if (randomVal <= currentWeight) return node;
        }
        return availableNodes.Last();
    }

    public void Generate(M_Level level)
    {
        if (!initialized) Init();
        if (level == null || availableNodes.Count == 0 || gravity <= 0f || playerMass <= 0f) return;

        Vector3 startPos = level.transform.position + (Vector3.up * startPosVerticalOffset) + (Vector3.forward * startPosForwardOffset);
        float absoluteTargetY = startPos.y + targetHeight;

        List<BoostNodeData> chain = new List<BoostNodeData>();
        List<ItemDef> playerPool = new List<ItemDef>();

        BoostNodeData current = new BoostNodeData
        {
            NodeType = GetRandomNode(),
            Position = startPos,
            Rotation = Quaternion.identity,
            ParentName = boostPlatformName
        };
        current.ProvidedItems = current.NodeType.GenerateItems();
        chain.Add(current);
        playerPool.AddRange(current.ProvidedItems);

        float currentHeadingDeg = UnityEngine.Random.Range(0f, 360f);

        while (chain.Count < hardNodeCap && current.Position.y < absoluteTargetY)
        {
            List<ItemDef> excluded = RollExcludedItems(playerPool);
            List<ItemDef> usable = SelectUsableItems(playerPool, excluded);

            float launchSpeed = ComputeLaunchSpeed(usable);

            float maxPossibleHeight = (launchSpeed * launchSpeed) / (2f * gravity);

            float angleRad = UnityEngine.Random.Range(45f, 70f) * Mathf.Deg2Rad;
            float vx = launchSpeed * Mathf.Cos(angleRad);
            float vy = launchSpeed * Mathf.Sin(angleRad);
            float targetDy = UnityEngine.Random.Range(0.55f, 0.85f) * maxPossibleHeight;
            if (targetDy < 1f) targetDy = 1f;

            float discriminant = (vy * vy) - (2f * gravity * targetDy);
            if (discriminant < 0) discriminant = 0f;

            float t = (vy + Mathf.Sqrt(discriminant)) / gravity;

            float dx = vx * t;
            if (dx < 3f) dx = 3f;

            currentHeadingDeg += UnityEngine.Random.Range(-30f, 30f);
            float headingRad = currentHeadingDeg * Mathf.Deg2Rad;

            Vector3 offset = new Vector3(Mathf.Cos(headingRad) * dx, targetDy, Mathf.Sin(headingRad) * dx);
            Vector3 nextPos = current.GetExitPoint() + offset;

            BoostNodeData nextNode = new BoostNodeData
            {
                NodeType = GetRandomNode(),
                Position = nextPos,
                Rotation = Quaternion.Euler(0, currentHeadingDeg, 0),
                ParentName = boostPlatformName
            };
            nextNode.ProvidedItems = nextNode.NodeType.GenerateItems();

            foreach (var item in usable) playerPool.Remove(item);
            playerPool.AddRange(nextNode.ProvidedItems);
            int inventoryLimit = Mathf.Max(0, maxInventorySize);
            if (playerPool.Count > inventoryLimit) playerPool.RemoveRange(0, playerPool.Count - inventoryLimit);

            current.PathNext = nextNode;
            nextNode.PathPrev = current;
            chain.Add(nextNode);

            current = nextNode;
        }
        foreach (var node in chain)
        {
            node.NodeType.Spawn(node, level);
        }

        CreateWinButton(current.Position, level);
    }

    private float ComputeLaunchSpeed(List<ItemDef> items)
    {
        float totalForce = items.Sum(i => i.Force);
        float addedVelocity = (totalForce / playerMass) * fixedDeltaTime;
        return baseJumpForce + addedVelocity;
    }

    private List<ItemDef> RollExcludedItems(List<ItemDef> pool)
    {
        List<ItemDef> shuffled = pool.OrderBy(x => Guid.NewGuid()).ToList();
        int excludeCount = Mathf.Min(UnityEngine.Random.Range(minExcludedItems, maxExcludedItems), shuffled.Count);
        return shuffled.Take(excludeCount).ToList();
    }

    private List<ItemDef> SelectUsableItems(List<ItemDef> pool, List<ItemDef> excluded)
    {
        List<ItemDef> usable = new List<ItemDef>();
        int groundedUsed = 0;

        foreach (var item in pool)
        {
            if (usable.Count >= maxItemsPerJump) break;
            if (excluded.Contains(item)) continue;

            if (item.IsGrounded)
            {
                if (groundedUsed >= maxGroundedItemsPerJump) continue;
                groundedUsed++;
            }
            usable.Add(item);
        }
        return usable;
    }

    private void CreateWinButton(Vector3 position, M_Level level)
    {
        GameObject diamond = GameObject.CreatePrimitive(PrimitiveType.Cube);
        diamond.transform.SetParent(level.transform);
        diamond.transform.position = position + new Vector3(0f, winButtonVerticalOffset, 0f);
        diamond.transform.rotation = Quaternion.Euler(winButtonRotationDeg, 0, winButtonRotationDeg);

        Material whiteMat = new Material(Shader.Find(unlitColorShader));
        whiteMat.color = Color.white;
        diamond.GetComponent<MeshRenderer>().material = whiteMat;

        CL_Handhold btn = diamond.AddComponent<CL_Handhold>();
        btn.activeEvent = new UnityEvent();
        btn.activeEvent.AddListener(() => { CL_GameManager.gMan.Win(); });
        ObjectTagger tagger = btn.GetComponent<ObjectTagger>() ?? btn.gameObject.AddComponent<ObjectTagger>();
        tagger.AddTag(tagHandhold);
    }
}

public static class PlayerImpulseMonitor
{
    private const float SampleInterval = 5f;
    private const float MinUpwardVelocityThreshold = 1.0f;

    private static bool wasGrounded;
    private static Vector3 lastVelocity;
    private static Vector3 airborneStartPos;
    private static float airborneStartTime;
    private static float recordedLaunchSpeed;
    private static float elapsed;
    private static bool initialized;

    public static float LastRecordedLaunchSpeed => recordedLaunchSpeed;
    public static float LastActualHorizontalDist { get; private set; }
    public static float LastActualVerticalDist { get; private set; }
    public static float LastFlightTime { get; private set; }
    public static float ImpliedHorizontalVelocity { get; private set; }
    public static float ImpliedVerticalVelocity { get; private set; }

    public static void Reset()
    {
        recordedLaunchSpeed = 0f;
        LastActualHorizontalDist = 0f;
        LastActualVerticalDist = 0f;
        LastFlightTime = 0f;
        ImpliedHorizontalVelocity = 0f;
        ImpliedVerticalVelocity = 0f;
        initialized = false;
    }

    public static void Update(CharacterController controller)
    {
        if (controller == null) return;

        bool isGrounded = controller.isGrounded;
        Vector3 currentVelocity = controller.velocity;
        Vector3 currentPos = controller.transform.position;

        if (!initialized)
        {
            wasGrounded = isGrounded;
            lastVelocity = currentVelocity;
            initialized = true;
            return;
        }

        bool leftGround = wasGrounded && !isGrounded;
        bool landed = !wasGrounded && isGrounded;
        bool inAirBoost = !isGrounded && (currentVelocity.y - lastVelocity.y) > MinUpwardVelocityThreshold;

        if (leftGround)
        {
            airborneStartPos = currentPos;
            airborneStartTime = Time.time;
        }

        if ((leftGround || inAirBoost) && currentVelocity.y > 0f)
        {
            recordedLaunchSpeed = currentVelocity.magnitude;
        }

        if (landed)
        {
            LastFlightTime = Time.time - airborneStartTime;
            if (LastFlightTime > 0.05f)
            {
                Vector3 endPos = currentPos;
                LastActualHorizontalDist = Vector2.Distance(new Vector2(airborneStartPos.x, airborneStartPos.z), new Vector2(endPos.x, endPos.z));
                LastActualVerticalDist = endPos.y - airborneStartPos.y;

                ImpliedHorizontalVelocity = LastActualHorizontalDist / LastFlightTime;
                ImpliedVerticalVelocity = (LastActualVerticalDist + (0.5f * 9.81f * LastFlightTime * LastFlightTime)) / LastFlightTime;
            }
        }

        wasGrounded = isGrounded;
        lastVelocity = currentVelocity;
        elapsed += Time.deltaTime;

        if (elapsed >= SampleInterval)
        {
            elapsed -= SampleInterval;
        }
    }
}

[HarmonyPatch(typeof(CharacterController), "Move")]
public static class ENT_Player_Update_Patch
{
    public static void Postfix(CharacterController __instance)
    {
        if (__instance == null) return;
        PlayerImpulseMonitor.Update(__instance);
    }
}
