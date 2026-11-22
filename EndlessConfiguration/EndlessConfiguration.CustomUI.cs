using HarmonyLib;
using MortarLib;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

//When I was young, I never got the appeal of logging errors and warnings

//but it feels so nice and official, like formatting does.

// Take up my mantle

//Log everything
namespace Celestial.CustomEndless
{
    public class UIProxySync : MonoBehaviour
    {
        public GameObject linkedUI;

        private void OnEnable()
        {
            linkedUI?.SetActive(true);
        }

        private void OnDisable()
        {

            //EndlessConfiguration.UpdateRegions();
            linkedUI?.SetActive(false);
        }

        private void OnDestroy()
        {
            //EndlessConfiguration.UpdateRegions();

            if (linkedUI != null)
                Destroy(linkedUI);
        }
    }

    [HarmonyPatch(typeof(UI_GamemodeScreen_Panel), "Initialize")]
    public static class UI_GamemodeScreen_Panel_ScrollSettings_Patch
    {
        private static readonly string Marker = "Custom_Settings_Scroll";

        private static AssetBundle _cachedBundle;
        private static GameObject _cachedCanvasPrefab;

        private static void Postfix(UI_GamemodeScreen_Panel __instance)
        {
            Transform root = __instance.settingRoot;
            if (root == null || root.Find(Marker) != null)
                return;

            if (_cachedBundle == null)
            {
                string modFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string bundlePath = Path.Combine(modFolder, "menu_ui");

                if (File.Exists(bundlePath))
                {
                    _cachedBundle = AssetBundle.LoadFromFile(bundlePath);
                }

                if (_cachedBundle == null)
                {
                    Debug.LogError($"Could not load AssetBundle at: {bundlePath}");
                    return;
                }
            }

            if (_cachedCanvasPrefab == null)
            {
                _cachedCanvasPrefab = _cachedBundle.LoadAllAssets<GameObject>()
                .FirstOrDefault(go => go.GetComponent<Canvas>() != null)
                ?? _cachedBundle.LoadAllAssets<GameObject>().FirstOrDefault();

                if (_cachedCanvasPrefab == null)
                {
                    Debug.LogError("No valid GameObject asset found in AssetBundle 'menu_ui' Check your folders.");
                    return;
                }
            }


            GameObject menuInstance = UnityEngine.Object.Instantiate(_cachedCanvasPrefab);
            menuInstance.name = Marker;
            menuInstance.AddComponent<Challenges>();
            RepairTextFonts(menuInstance, root);

            Canvas topLevelCanvas = root.GetComponentInParent<Canvas>();
            Transform mainCanvasTransform = topLevelCanvas != null ? topLevelCanvas.transform : root.parent.parent;

            RectTransform instanceRect = menuInstance.GetComponent<RectTransform>();
            if (instanceRect != null)
            {
                instanceRect.SetParent(mainCanvasTransform, false);

                instanceRect.anchorMin = new Vector2(0.5f, 0.5f);
                instanceRect.anchorMax = new Vector2(0.5f, 0.5f);
                instanceRect.pivot = new Vector2(0.5f, 0.5f);
                instanceRect.anchoredPosition = Vector2.zero;
                instanceRect.localScale = Vector3.one;
            }

            UIProxySync sync = root.gameObject.GetComponent<UIProxySync>();
            if (sync == null)
            {
                sync = root.gameObject.AddComponent<UIProxySync>();
            }

            if (sync.linkedUI != null && sync.linkedUI != menuInstance)
            {
                UnityEngine.Object.Destroy(sync.linkedUI);
            }

            sync.linkedUI = menuInstance;

            EndlessConfiguration.CustomUIManager.UISettingsCacheSync sync2 = root.gameObject.GetComponent<EndlessConfiguration.CustomUIManager.UISettingsCacheSync>();
            if (sync2 == null)
            {
                sync2 = root.gameObject.AddComponent<EndlessConfiguration.CustomUIManager.UISettingsCacheSync>();
            }




            Canvas canvas = menuInstance.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.overrideSorting = true;
                canvas.sortingOrder = 9999;
            }

            menuInstance.SetActive(false);
        }

        private static TMP_FontAsset menuRuntimeFont;
        private static Material menuRuntimeMaterial;

        [HarmonyPatch(typeof(TextMeshProUGUI), "SetArraySizes")]
        private static class MenuFontReferencePatch
        {
            private static void Prefix(TextMeshProUGUI __instance)
            {
                if (menuRuntimeFont == null || menuRuntimeMaterial == null ||
                    __instance.GetComponentInParent<UISettingsManager>(true) == null) return;
                if (menuRuntimeFont.material == null) menuRuntimeFont.material = menuRuntimeMaterial;
                if (__instance.font == null || __instance.font.material == null)
                    __instance.font = menuRuntimeFont;
                if (__instance.fontSharedMaterial == null)
                    __instance.fontSharedMaterial = menuRuntimeMaterial;
            }
        }

        private static void RepairTextFonts(GameObject menu, Transform root)
        {
            TMP_FontAsset font = null;
            TMP_SpriteAsset sprite = null;
            Material material = null;
            Canvas sourceCanvas = root.GetComponentInParent<Canvas>();
            if (sourceCanvas != null)
                foreach (TMP_Text source in sourceCanvas.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (source == null || source.font == null || source.font.material == null) continue;
                    font = source.font;
                    material = source.fontSharedMaterial ?? source.font.material;
                    if (source.spriteAsset != null && source.spriteAsset.material != null) sprite = source.spriteAsset;
                    break;
                }
            if (font == null && TMP_Settings.defaultFontAsset != null && TMP_Settings.defaultFontAsset.material != null)
            {
                font = TMP_Settings.defaultFontAsset;
                material = font.material;
            }
            if (font == null || material == null)
            {
                Debug.LogError("No valid UI font was found for the custom menu.");
                return;
            }
            menuRuntimeFont = font;
            menuRuntimeMaterial = material;
            if (sprite == null && TMP_Settings.defaultSpriteAsset != null && TMP_Settings.defaultSpriteAsset.material != null)
                sprite = TMP_Settings.defaultSpriteAsset;
            foreach (TMP_Text text in menu.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text == null) continue;
                Material authored = text.fontSharedMaterial;
                text.font = font;
                Material repaired = new Material(material);
                if (authored != null)
                {
                    repaired.CopyPropertiesFromMaterial(authored);
                    repaired.shader = material.shader;
                    repaired.shaderKeywords = authored.shaderKeywords;
                }
                repaired.name = "Menu Text Atlas";
                Texture2D atlas = text.font.atlasTexture;
                if (repaired.HasProperty("_MainTex"))
                {
                    repaired.SetTexture("_MainTex", atlas);
                    repaired.SetTextureScale("_MainTex", Vector2.one);
                    repaired.SetTextureOffset("_MainTex", Vector2.zero);
                }
                if (repaired.HasProperty("_TextureWidth")) repaired.SetFloat("_TextureWidth", atlas.width);
                if (repaired.HasProperty("_TextureHeight")) repaired.SetFloat("_TextureHeight", atlas.height);
                string[] samplingProperties = { "_GradientScale", "_WeightNormal", "_WeightBold", "_ScaleRatioA", "_ScaleRatioB", "_ScaleRatioC" };
                foreach (string property in samplingProperties)
                    if (repaired.HasProperty(property) && font.material.HasProperty(property)) repaired.SetFloat(property, font.material.GetFloat(property));
                MenuTextMaterials owner = menu.GetComponent<MenuTextMaterials>() ?? menu.AddComponent<MenuTextMaterials>();
                owner.Materials.Add(repaired);
                if (text.font.material == null) text.font.material = repaired;
                text.fontSharedMaterial = repaired;
                if (text.spriteAsset == null) text.spriteAsset = sprite;
                text.UpdateMeshPadding();
            }
        }

        private sealed class MenuTextMaterials : MonoBehaviour
        {
            public readonly List<Material> Materials = new List<Material>();

            private void OnDestroy()
            {
                foreach (Material material in Materials) if (material != null) UnityEngine.Object.Destroy(material);
            }
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            if (go == null) return;
            go.layer = layer;
            foreach (Transform child in go.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }
    }

    public class UniversalBreakroomSettings : MonoBehaviour
    {
        public string regionKey;
        public int slotIndex;
        public int breakroomNumber;
        public bool configured;
    }

    public static partial class EndlessConfiguration
    {
        public static class CustomUIManager
        {
            public static GameObject CustomToggleButton;
            public static GameObject CustomMenuPanel;

            public static Dictionary<string, bool> SettingToggles = new Dictionary<string, bool>();
            public static Dictionary<string, int> SettingCounts = new Dictionary<string, int>();
            public static Dictionary<string, float> SettingFloats = new Dictionary<string, float>();
            public static Dictionary<string, string> SettingStrings = new Dictionary<string, string>();

            private static void RebuildLayouts(RectTransform rect)
            {
                while (rect != null)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                    Canvas.ForceUpdateCanvases();
                    rect = rect.parent as RectTransform;
                }
            }

            public static void ApplyConfiguredBuffs()
            {
                ENT_Player player = ENT_Player.GetPlayer();
                if (player == null)
                    return;

                foreach (var buff in EndlessSettings.buffSettings)
                {
                    float amount = GetFloatState(buff.Key);

                    if (Mathf.Approximately(amount, 0f))
                        continue;

                    var perk = new PerkBuilder($"endless_{buff.Key}", buff.Name)
                    .WithBuff($"endless_{buff.Key}_buff", buff.BuffId, amount)
                    .Build();

                    player.AddPerk(perk);
                }

                if (GetToggleState("buff_inf_charge"))
                {
                    player.AddPerk(new PerkBuilder("endless_infcharge", "Infinite Charge")
                    .WithBuff("endless_infcharge_buff", "infiniteCharge", 1f)
                    .Build());
                }

                if (GetToggleState("buff_hang_grounded"))
                {
                    player.AddPerk(new PerkBuilder("endless_grounded", "Grounded While Hanging")
                    .WithBuff("endless_grounded_buff", GameUtils.Buffs.HangingGrounded, 1f)
                    .Build());
                }
            }


            public static string GetStringState(string id)
            {
                return SettingStrings.TryGetValue(id, out string val) ? val : string.Empty;
            }
            public static bool GetToggleState(string id)
            {
                return SettingToggles.TryGetValue(id, out bool val) ? val : false;
            }

            public static int GetCountState(string id, int def = 0)
            {
                return SettingCounts.TryGetValue(id, out int val) ? val : def;
            }

            public static float GetFloatState(string id)
            {
                return SettingFloats.TryGetValue(id, out float val) ? val : 0f;
            }

            public class UISettingsCacheSync : MonoBehaviour
            {
                private void OnDisable()
                {
                    var manager = UISettingsManager.instance;
                    if (manager == null)
                        return;

                    Cache<bool>("boolSettings", CustomUIManager.SettingToggles, manager);
                    Cache<int>("intSettings", CustomUIManager.SettingCounts, manager);
                    Cache<float>("floatSettings", CustomUIManager.SettingFloats, manager);
                    Cache<string>("stringSettings", CustomUIManager.SettingStrings, manager);

                    //UpdateRegions();
                }

                private static void Cache<T>(string fieldName, Dictionary<string, T> destination, UISettingsManager manager)
                {
                    var field = typeof(UISettingsManager).GetField(
                    fieldName,
                    BindingFlags.NonPublic | BindingFlags.Instance);

                    if (field?.GetValue(manager) is Dictionary<string, T> source)
                    {
                        destination.Clear();

                        foreach (var kv in source)
                            destination[kv.Key] = kv.Value;
                    }
                }
            }
        }
    }
}
