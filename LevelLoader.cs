using Celestial.CustomEndless;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Unity.VisualScripting;
using UnityEngine;

public static class CustomLevelLoader
{
    public static readonly Dictionary<string, AssetBundle> _cachedBundles = new Dictionary<string, AssetBundle>(StringComparer.OrdinalIgnoreCase);
    public static bool Override;
    private static Shader overrideShader;
    private static readonly Dictionary<Material, Shader> originalShaders = new Dictionary<Material, Shader>();
    private static readonly Dictionary<string, Shader> nativeRoomShaders = new Dictionary<string, Shader>(StringComparer.Ordinal);
    private static readonly HashSet<M_Level> customLevels = new HashSet<M_Level>();

    public static M_Level.LevelAssetHolder LoadRoom(string bundlePath, string assetName, string levelName)
    {
        CacheNativeRoomShaders();
        Type frameworkType = Type.GetType("Severon.GamemodeFramework.GamemodeFramework, GamemodeFramework", false);
        MethodInfo frameworkLoader = frameworkType == null ? null : frameworkType.GetMethod("LoadLevel", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string), typeof(string), typeof(string), typeof(string) }, null);
        if (frameworkLoader != null)
        {

            string modPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            M_Level.LevelAssetHolder level;

            try { level = frameworkLoader.Invoke(null, new object[] { modPath, bundlePath, assetName, levelName }) as M_Level.LevelAssetHolder; }
            catch (TargetInvocationException exception)
            {
                Debug.LogWarning(exception.InnerException ?? exception);
                return LoadRoomFallback(bundlePath, assetName, levelName);
            }
            if (level == null || level.level == null) return LoadRoomFallback(bundlePath, assetName, levelName);
            PrepareRoom(level.level);
            RegisterCustomLevel(level.level);
            ConfigureLighting(level.level);
            ConfigureRoomShaders(level.level);
            if (level.level.GetComponent<ImportedRoomRendering>() == null) level.level.gameObject.AddComponent<ImportedRoomRendering>();
            return level;
        }
        return LoadRoomFallback(bundlePath, assetName, levelName);
    }

    internal static IEnumerable<string> GetRoomAssetNames(string bundlePath)
    {
        AssetBundle bundle = GetRoomBundle(bundlePath);
        if (bundle == null) return Array.Empty<string>();
        return Array.ConvertAll(Array.FindAll(bundle.GetAllAssetNames(), path => path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)), Path.GetFileNameWithoutExtension);
    }

    private static AssetBundle GetRoomBundle(string bundlePath)
    {
        string modFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (string.IsNullOrEmpty(modFolder)) return null;
        string path = Path.Combine(modFolder, bundlePath);
        if (_cachedBundles.TryGetValue(path, out AssetBundle cached) && cached != null) return cached;
        foreach (AssetBundle loaded in AssetBundle.GetAllLoadedAssetBundles())
        {
            if (loaded != null && string.Equals(loaded.name, Path.GetFileName(bundlePath), StringComparison.OrdinalIgnoreCase))
            {
                _cachedBundles[path] = loaded;
                return loaded;
            }
        }
        if (!File.Exists(path)) return null;
        AssetBundle bundle;
        try { bundle = AssetBundle.LoadFromFile(path); }
        catch (Exception exception) { Debug.LogWarning(exception.Message); return null; }
        if (bundle != null) _cachedBundles[path] = bundle;
        return bundle;
    }

    private static M_Level.LevelAssetHolder LoadRoomFallback(string bundlePath, string assetName, string levelName)
    {
        AssetBundle bundle = GetRoomBundle(bundlePath);
        if (bundle == null) return null;
        string assetPath = Array.Find(bundle.GetAllAssetNames(), path => string.Equals(Path.GetFileNameWithoutExtension(path), assetName, StringComparison.OrdinalIgnoreCase) && path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase));
        if (assetPath == null) return null;
        GameObject prefab = bundle.LoadAsset<GameObject>(assetPath);
        if (prefab == null) return null;
        GameObject room = UnityEngine.Object.Instantiate(prefab);
        EndlessConfiguration.RegisterRuntimeObject(room, false);
        room.name = levelName;
        M_Level level = room.GetComponent<M_Level>();
        if (level == null) level = room.AddComponent<M_Level>();
        Transform entrance = room.transform.Find("Entrance") ?? FindRecursiveContains(room.transform, "entrance");
        Transform exit = room.transform.Find("Exit") ?? FindRecursiveContains(room.transform, "exit");
        if (entrance == null || exit == null)
        {
            UnityEngine.Object.Destroy(room);
            return null;
        }
        foreach (Transform transform in room.GetComponentsInChildren<Transform>(true))
        {
            if (transform.name.ContainsInsensitive("handhold"))
            {
                ObjectTagger tagger = transform.GetComponent<ObjectTagger>();
                if (tagger == null) tagger = transform.gameObject.AddComponent<ObjectTagger>();
                tagger.AddTag("Handhold");
            }
        }
        level.levelName = levelName;
        level.levelEntrance = entrance;
        level.levelExit = exit;
        level.isLODGap = false;
        level.spawnPosition = entrance;
        PrepareRoom(level);
        level.canFlip = false;
        level.showInLevelList = true;
        level.allowEvents = true;
        level.spawnSettings = new SpawnTable.SpawnSettings { spawnChance = 1f };
        ConfigureLighting(level);
        ConfigureRoomShaders(level);
        if (level.GetComponent<ImportedRoomRendering>() == null) level.gameObject.AddComponent<ImportedRoomRendering>();
        RegisterCustomLevel(level);
        M_Level.LevelAssetHolder holder;
        try { holder = M_Level.LevelAssetHolder.GetNewHolderFromLevel(level); }
        catch { UnityEngine.Object.Destroy(room); return null; }
        room.SetActive(false);
        return holder;
    }

    private static void ConfigureLighting(M_Level level)
    {
        foreach (Light light in level.GetComponentsInChildren<Light>(true))
        {
            if (light.type != LightType.Point && light.type != LightType.Spot) continue;
            CL_Lamp lamp = light.GetComponent<CL_Lamp>();
            if (lamp == null) lamp = light.gameObject.AddComponent<CL_Lamp>();
            Color color = light.color;
            lamp.SetColor(new Color(LightValue(color.r, 1f, 0f, 1f), LightValue(color.g, 1f, 0f, 1f), LightValue(color.b, 1f, 0f, 1f), 1f));
            lamp.range = LightValue(light.range * 2f, 18f, 1f, 240f);
            lamp.curRange = lamp.range;
            lamp.intensity = LightValue(lamp.intensity, 1f, 0f, 1f);
            lamp.SetAngle(light.type == LightType.Spot ? LightValue(light.spotAngle, 90f, 1f, 179f) : 360f);
            lamp.bypassLightmap = 1f;
            lamp.lampActive = true;
            lamp.prioritizeOnEnable = false;
            light.enabled = false;
        }
    }

    private static void CacheNativeRoomShaders()
    {
        if (nativeRoomShaders.ContainsKey("Dark Machine/SHDR_Base")) return;
        foreach (M_Level level in Resources.FindObjectsOfTypeAll<M_Level>())
        {
            if (level == null || customLevels.Contains(level)) continue;
            string name = level.name;
            if (name.Length < 2 || char.ToLowerInvariant(name[0]) != 'm' || !char.IsDigit(name[1])) continue;
            foreach (Renderer renderer in level.GetComponentsInChildren<Renderer>(true))
                foreach (Material material in renderer.sharedMaterials)
                    if (material != null && material.shader != null && !nativeRoomShaders.ContainsKey(material.shader.name))
                        nativeRoomShaders.Add(material.shader.name, material.shader);
        }
    }

    internal static bool RefreshRoomRendering(M_Level level)
    {
        if (level == null) return false;
        CacheNativeRoomShaders();
        ConfigureRoomShaders(level);
        foreach (CL_Lamp lamp in level.GetComponentsInChildren<CL_Lamp>(true))
            if (lamp.enabled && lamp.gameObject.activeInHierarchy && FXManager.fxMan != null) FXManager.fxMan.AddLamp(lamp);
        return nativeRoomShaders.ContainsKey("Dark Machine/SHDR_Base");
    }

    private static void ConfigureRoomShaders(M_Level level)
    {
        var replacements = new Dictionary<Material, Material>();
        foreach (Renderer renderer in level.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            bool changed = false;
            for (int index = 0; index < materials.Length; index++)
            {
                Material material = materials[index];
                if (material == null || material.shader == null) continue;
                string name = material.shader.name == "Standard" ? "Dark Machine/SHDR_Base" : material.shader.name;
                if (!nativeRoomShaders.TryGetValue(name, out Shader shader) || shader == material.shader) continue;
                if (!replacements.TryGetValue(material, out Material replacement))
                {
                    replacement = new Material(material);
                    replacement.shader = shader;
                    replacements.Add(material, replacement);
                }
                materials[index] = replacement;
                changed = true;
            }
            if (changed) renderer.sharedMaterials = materials;
        }
    }

    private static float LightValue(float value, float fallback, float min, float max)
    {
        return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max);
    }

    private static Transform FindRecursiveContains(Transform root, string substring)
    {
        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true)) if (transform.name.ContainsInsensitive(substring)) return transform;
        return null;
    }

    internal static void PrepareRoom(M_Level level)
    {
        if (level == null) return;
        Transform entrance = level.transform.Find("Entrance");
        Transform exit = level.transform.Find("Exit");
        if (entrance != null) level.levelEntrance = entrance;
        if (exit != null) level.levelExit = exit;
        if (level.levelEntrance == null || level.levelExit == null) return;
        level.customBounds = true;
        level.boundsObject = null;
        level.levelBounds = CalculateDuctBounds(level);
        typeof(M_Level).GetField("currentBoundsLocal", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(level, level.levelBounds);
        if (LevelSpawnOrientationPatch.IsTransitionRoom(level)) level.loadAdjacent = true;
    }

    private static Bounds CalculateDuctBounds(M_Level level)
    {
        Transform root = level.transform;
        Bounds bounds = new Bounds(root.InverseTransformPoint(level.levelEntrance.position), Vector3.zero);
        bounds.Encapsulate(root.InverseTransformPoint(level.levelExit.position));

        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null)
                continue;
            bool inactive = false;
            for (Transform ancestor = filter.transform; ancestor != null && ancestor != root; ancestor = ancestor.parent)
                if (!ancestor.gameObject.activeSelf) { inactive = true; break; }
            if (inactive) continue;

            Bounds meshBounds = filter.sharedMesh.bounds;
            Matrix4x4 matrix = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            Vector3 min = meshBounds.min;
            Vector3 max = meshBounds.max;
            for (int x = 0; x < 2; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    for (int z = 0; z < 2; z++)
                    {
                        bounds.Encapsulate(matrix.MultiplyPoint3x4(new Vector3(
                            x == 0 ? min.x : max.x,
                            y == 0 ? min.y : max.y,
                            z == 0 ? min.z : max.z)));
                    }
                }
            }
        }

        bounds.Expand(2f);
        return bounds;
    }

    public static void ClearCache()
    {
        foreach (AssetBundle bundle in _cachedBundles.Values) if (bundle != null) bundle.Unload(false);
        _cachedBundles.Clear();
    }

    private static void RegisterCustomLevel(M_Level level)
    {
        if (level != null) customLevels.Add(level);
    }

    internal static IEnumerable<M_Level> GetCustomLevels()
    {
        customLevels.RemoveWhere(level => level == null);
        return customLevels;
    }


}


public sealed class ImportedRoomRendering : MonoBehaviour
{
    private bool ready;
    private float retryAt;

    private void OnEnable()
    {
        ready = CustomLevelLoader.RefreshRoomRendering(GetComponent<M_Level>());
    }

    private void Start()
    {
        ready = CustomLevelLoader.RefreshRoomRendering(GetComponent<M_Level>());
    }

    private void Update()
    {
        if (ready || Time.unscaledTime < retryAt) return;
        retryAt = Time.unscaledTime + 1f;
        ready = CustomLevelLoader.RefreshRoomRendering(GetComponent<M_Level>());
    }
}
