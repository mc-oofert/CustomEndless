using Celestial.CustomEndless;
using HarmonyLib;
using MortarLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Unity.VisualScripting;
using UnityEngine;

//Deltarune reference? The wishbone is here btw, it used to be an egg.

public class WishboneStuff
{
    private static AssetBundle GetBundle(string bundleName)
    {
        string assemblyLocation = Assembly.GetExecutingAssembly().Location;
        string modFolder = Path.GetDirectoryName(assemblyLocation);
        if (string.IsNullOrEmpty(modFolder)) return null;

        string siblingPath = Path.Combine(modFolder, bundleName);

        if (CustomLevelLoader._cachedBundles.TryGetValue(siblingPath, out AssetBundle cached) && cached != null)
        {
            return cached;
        }

        foreach (var kvp in CustomLevelLoader._cachedBundles)
        {
            if (kvp.Key.EndsWith(bundleName, StringComparison.OrdinalIgnoreCase) && kvp.Value != null)
            {
                return kvp.Value;
            }
        }

        AssetBundle frameworkBundle = GetFrameworkBundle(modFolder, bundleName);
        if (frameworkBundle != null)
        {
            CustomLevelLoader._cachedBundles[siblingPath] = frameworkBundle;
            return frameworkBundle;
        }

        if (File.Exists(siblingPath))
        {
            try
            {
                AssetBundle loaded = AssetBundle.LoadFromFile(siblingPath);
                if (loaded != null)
                {
                    CustomLevelLoader._cachedBundles[siblingPath] = loaded;
                    return loaded;
                }
            }
            catch { }
        }

        return null;
    }

    private static AssetBundle GetFrameworkBundle(string modFolder, string bundleName)
    {
        Type frameworkType = Type.GetType("Severon.GamemodeFramework.GamemodeFramework, GamemodeFramework", false);
        MethodInfo method = frameworkType == null ? null : frameworkType.GetMethod("GetCachedAssetBundle", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string), typeof(string) }, null);
        try { return method == null ? null : method.Invoke(null, new object[] { modFolder, bundleName }) as AssetBundle; }
        catch { return null; }
    }

    public static Item_Object SpawnWishbone(Vector3 position)
    {
        Item_Object blinkEyeTemplate = EndlessConfiguration.GetCachedObject<Item_Object>("Item_Artifact_Remote");
        Item_Object transTemplate = EndlessConfiguration.GetCachedObject<Item_Object>("Item_Artifact_Translocator");
        if (blinkEyeTemplate == null || blinkEyeTemplate.itemData == null || blinkEyeTemplate.itemData.handItemAsset == null) return null;
        Item_Object wishbone = UnityEngine.Object.Instantiate(blinkEyeTemplate, position, Quaternion.identity);
        GameObject wishbonePrefab = ProcGen.PathNode.GetPrefab("Wishbone");

        if (wishbonePrefab != null)
        {
            MeshFilter wishboneFilter = wishbonePrefab.GetComponentInChildren<MeshFilter>();
            if (wishboneFilter != null && wishboneFilter.sharedMesh != null)
            {
                Mesh bakedMesh = UnityEngine.Object.Instantiate(wishboneFilter.sharedMesh);
                Vector3[] vertices = bakedMesh.vertices;
                Vector3 scale = wishboneFilter.transform.localScale;

                for (int i = 0; i < vertices.Length; i++)
                {
                    vertices[i] = Vector3.Scale(vertices[i], scale);
                }

                bakedMesh.vertices = vertices;
                bakedMesh.RecalculateBounds();
                bakedMesh.RecalculateNormals();

                foreach (MeshFilter filter in wishbone.GetComponentsInChildren<MeshFilter>(true))
                {
                    filter.sharedMesh = bakedMesh;
                }
            }
        }




        if (blinkEyeTemplate != null && blinkEyeTemplate.itemData != null && blinkEyeTemplate.itemData.handItemAsset != null)
        {
            HandItem sourceHandItem = blinkEyeTemplate.itemData.handItemAsset;
            GameObject customHandPrefab = new GameObject(sourceHandItem.gameObject.name);
            customHandPrefab.transform.localScale = sourceHandItem.transform.localScale;
            SpriteRenderer sourceRenderer = sourceHandItem.GetComponent<SpriteRenderer>();
            SpriteRenderer customRenderer = customHandPrefab.AddComponent<SpriteRenderer>();
            if (sourceRenderer != null)
            {
                customRenderer.sharedMaterial = sourceRenderer.sharedMaterial;
                customRenderer.sprite = sourceRenderer.sprite;
                customRenderer.color = sourceRenderer.color;
                customRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
                customRenderer.sortingOrder = sourceRenderer.sortingOrder;
            }
            customHandPrefab.SetActive(false);
            EndlessConfiguration.RegisterRuntimeObject(customHandPrefab, false);

            ViewSway sourceSway = sourceHandItem.GetComponent<ViewSway>();
            ViewSway customSway = customHandPrefab.AddComponent<ViewSway>();
            if (sourceSway != null) JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(sourceSway), customSway);
            if (customSway.parameters == null) customSway.parameters = new ViewSway.ViewSwayParameters();

            HandItem_Wishbone wishboneComponent = customHandPrefab.AddComponent<HandItem_Wishbone>();
            wishboneComponent.interactionModules = new List<Item_InteractionModule>();
            wishboneComponent.itemCosmetics = new List<Cosmetic_Item>();
            wishboneComponent.useStats = new List<string>();
            wishboneComponent.simpleUseModules = new List<SimpleModule>();
            wishboneComponent.firstGrabEvent = new UnityEngine.Events.UnityEvent();
            wishboneComponent.itemUseEvent = new UnityEngine.Events.UnityEvent();

            foreach (Hand_SpriteController sr in customHandPrefab.GetComponentsInChildren<Hand_SpriteController>(true))
            {
                sr.spriteName = "none";
                sr.enabled = false;
            }
            foreach (SpriteRenderer sr in customHandPrefab.GetComponentsInChildren<SpriteRenderer>(true))
            {
                sr.enabled = false;
            }
            SpriteRenderer takeoverBackground = null;
            SpriteRenderer takeoverForeground = null;
            if (transTemplate != null && transTemplate.itemData != null && transTemplate.itemData.handItemAsset != null)
            {
                foreach (SpriteRenderer sr in transTemplate.itemData.handItemAsset.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (sr.name.ContainsInsensitive("Sprite"))
                    {
                        var ob = UnityEngine.Object.Instantiate(sr.gameObject);
                        ob.transform.SetParent(customHandPrefab.transform, false);
                        ob.transform.Rotate(0, 0, 45);
                        takeoverBackground = ob.GetComponent<SpriteRenderer>();
                        foreach (SpriteRenderer copiedRenderer in ob.GetComponentsInChildren<SpriteRenderer>(true))
                        {
                            if (copiedRenderer.name.ContainsInsensitive("ItemFingers")) takeoverForeground = copiedRenderer;
                        }
                        break;
                    }
                }
            }
            foreach (SpriteRenderer sr in customHandPrefab.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr.name.ContainsInsensitive("handle") || sr.name.ContainsInsensitive("Fingers")) sr.enabled = false;
            }
            GameObject targetObject = new GameObject("WishboneTarget");
            targetObject.transform.SetParent(customHandPrefab.transform);
            SpriteRenderer targetSR = targetObject.GetComponent<SpriteRenderer>();
            if (targetSR == null) targetSR = targetObject.AddComponent<SpriteRenderer>();
            if (sourceRenderer != null) targetSR.sharedMaterial = sourceRenderer.sharedMaterial;
            wishboneComponent.targetRenderer = targetSR; //why is it big why is it big why is it big
            if (takeoverForeground != null) wishboneComponent.targetRenderer.sortingLayerID = takeoverForeground.sortingLayerID;
            wishboneComponent.targetRenderer.sortingOrder = 2;

            AssetBundle bundle = GetBundle("anywhere");
            if (bundle != null)
            {
                wishboneComponent.spriteC1 = bundle.LoadAsset<Sprite>("WishC1");
                wishboneComponent.spriteC2 = bundle.LoadAsset<Sprite>("WishC2");
                wishboneComponent.spriteC3 = bundle.LoadAsset<Sprite>("WishC3");

            }

            wishbone.itemData.handItemAsset = customHandPrefab.GetComponent<HandItem_Wishbone>();
        }

        wishbone.itemData.SetItemAsset(wishbone);
        wishbone.itemData.prefabName = "Item_Wishbone";
        wishbone.name = wishbone.itemData.prefabName;
        wishbone.gameObject.name = wishbone.itemData.prefabName;
        wishbone.itemData.handItemAsset.name = wishbone.itemData.prefabName;
        GameObject databasePrefab = UnityEngine.Object.Instantiate(wishbone.gameObject);
        databasePrefab.name = wishbone.itemData.prefabName;

        databasePrefab.SetActive(false);
        EndlessConfiguration.RegisterRuntimeObject(databasePrefab, false);

        AddItemToBaseDatabase(databasePrefab);
        wishbone.gameObject.SetActive(true);
        return wishbone;
    }
    public static void AddItemToBaseDatabase(GameObject itemPrefab)
    {
        WKAssetDatabase baseDb = CL_AssetManager.GetBaseAssetDatabase();

        if (baseDb != null && itemPrefab != null)
        {
            if (baseDb.itemPrefabs == null) baseDb.itemPrefabs = new List<GameObject>();
            bool removedNullPrefabs = baseDb.itemPrefabs.RemoveAll(prefab => prefab == null) > 0;
            bool containsNamedPrefab = baseDb.itemPrefabs.Exists(prefab => prefab != null && string.Equals(prefab.name, itemPrefab.name, StringComparison.OrdinalIgnoreCase));
            if (!containsNamedPrefab)
            {
                baseDb.itemPrefabs.Add(itemPrefab);
            }
            if (!containsNamedPrefab || removedNullPrefabs) CL_AssetManager.RefreshDatabaseList();
        }
    }
}

[HarmonyPatch(typeof(CL_AssetManager.WKDatabaseHolder), nameof(CL_AssetManager.WKDatabaseHolder.RefreshDictionary))]
public static class AssetDatabaseNullPrefabPatcher
{
    [HarmonyPrefix]
    public static void Prefix(CL_AssetManager.WKDatabaseHolder __instance)
    {
        if (__instance == null || __instance.database == null) return;
        if (__instance.database.itemPrefabs == null) __instance.database.itemPrefabs = new List<GameObject>();
        if (__instance.database.entityPrefabs == null) __instance.database.entityPrefabs = new List<GameObject>();
        __instance.database.itemPrefabs.RemoveAll(prefab => prefab == null);
        __instance.database.entityPrefabs.RemoveAll(prefab => prefab == null);
    }
}

public class HandItem_Wishbone : HandItem
{
    private float holdTime;
    private bool isCharging;
    private UT_AudioClipHandler clipHandler;
    public int uses = 1;

    public Sprite spriteC1;
    public Sprite spriteC2;
    public Sprite spriteC3;
    public SpriteRenderer targetRenderer;

    public override void Initialize(Item i, ENT_Player.Hand h)
    {
        if (interactionModules == null) interactionModules = new List<Item_InteractionModule>();
        if (itemCosmetics == null) itemCosmetics = new List<Cosmetic_Item>();
        if (useStats == null) useStats = new List<string>();
        if (simpleUseModules == null) simpleUseModules = new List<SimpleModule>();
        if (firstGrabEvent == null) firstGrabEvent = new UnityEngine.Events.UnityEvent();
        if (itemUseEvent == null) itemUseEvent = new UnityEngine.Events.UnityEvent();
        base.Initialize(i, h);

        this.anim = this.GetComponent<Animator>();

        if (this.clipHandler == null)
        {
            this.clipHandler = base.GetComponent<UT_AudioClipHandler>();
        }

        StartCharging();
    }

    public override void OnEnable()
    {
        base.OnEnable();
        StartCharging();
    }

    public override void OnTryPocket()
    {
        base.OnTryPocket();
        ResetCharge();
    }

    public override void OnDrop()
    {
        base.OnDrop();
        ResetCharge();
    }

    private void StartCharging()
    {
        this.isCharging = true;
        this.holdTime = 0f;
        UpdateSprite(0);
    }

    private void ResetCharge()
    {
        this.isCharging = false;
        this.holdTime = 0f;

        if (this.anim != null)
        {
            this.anim.SetInteger("Uses", 0);
        }
        UpdateSprite(0);
    }

    private void UpdateSprite(int chargeLevel)
    {
        if (this.targetRenderer == null) return;

        if (chargeLevel == 0)
        {
            this.targetRenderer.sprite = this.spriteC1;
        }
        else if (chargeLevel == 1)
        {
            this.targetRenderer.sprite = this.spriteC2;
        }
        else if (chargeLevel == 2)
        {
            this.targetRenderer.sprite = this.spriteC3;
        }
    }

    public override void LateUpdate()
    {
        base.LateUpdate();

        if (this.isCharging)
        {
            this.holdTime += Time.deltaTime;
            int currentChargeLevel = Mathf.Clamp((int)this.holdTime, 0, 2);

            if (this.anim != null)
            {
                this.anim.SetInteger("Uses", currentChargeLevel + 1);
            }

            UpdateSprite(currentChargeLevel);
        }
    }

    public override void Use()
    {
        ENT_Player player = ENT_Player.GetPlayer();
        if (used || item == null || item.inventory == null || hand == null || player == null || player.cam == null) return;
        CL_Handhold handholdPrefab = EndlessConfiguration.GetCachedObject<CL_Handhold>("Item_Artifact_Remote_Handhold");
        if (handholdPrefab == null) return;

        base.Use();

        this.used = true;

        int chargeLevel = Mathf.Clamp((int)this.holdTime, 0, 2);

        CL_Handhold spawnedHandhold = null;

        if (handholdPrefab != null)
        {
            Transform playerCam = ENT_Player.GetPlayer().cam.transform;
            Vector3 spawnPosition = playerCam.position + playerCam.forward * 1f;
            Quaternion spawnRotation = Quaternion.LookRotation(playerCam.forward);

            spawnedHandhold = UnityEngine.Object.Instantiate(handholdPrefab, spawnPosition, spawnRotation);

            HandholdModule_JumpBoost module = spawnedHandhold.modules == null ? null : spawnedHandhold.modules.Find(test => test is HandholdModule_JumpBoost) as HandholdModule_JumpBoost;
            if (module != null && chargeLevel == 2)
            {
                module.boostInLookDir = 1;
                module.fovAdjust *= 5;
                module.doubleHandBoost = false;
                module.jumpBoost *= 4;
            }
            else if (module != null)
            {
                module.boostInLookDir *= 1;
                module.fovAdjust *= chargeLevel + 1;
                module.jumpBoost *= chargeLevel + 1;
            }
            var otherModule = spawnedHandhold.GetComponent<UT_Timer>();
            if (otherModule != null)
            {
                otherModule.endEvent.Invoke();
                otherModule.enabled = false;
            }
            foreach (ParticleSystem particles in spawnedHandhold.GetComponentsInChildren<ParticleSystem>())
            {
                ProcGen.SetTheFuckingColorOfThisParticleSystem(particles, (Color.red / 5) * (chargeLevel + 1));
            }
            ProcGen.SetTheFuckingColorOfThisMesh(spawnedHandhold.gameObject, (Color.red / 5) * (chargeLevel + 1));
            spawnedHandhold.dropOnInactive = false;
        }
        ENT_Player.Hand handRef = this.hand;
        this.isCharging = false;
        this.RemoveItem();
        if (spawnedHandhold != null && handRef != null)
        {
            handRef.ForceGrabHandhold(spawnedHandhold.transform);
        }
    }
}

