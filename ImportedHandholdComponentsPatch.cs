using HarmonyLib;
using UnityEngine;

namespace Celestial.CustomEndless
{
    [HarmonyPatch(typeof(HandholdManager), "CopyComponents")]
    internal static class ImportedHandholdComponentsPatch
    {
        private static bool Prefix(GameObject source, GameObject target)
        {
            M_Level level = target?.GetComponentInParent<M_Level>();
            if (source == null || level == null || level.transform.Find("Geometry") == null ||
                CL_GameManager.GetCurrentGamemode() != EndlessConfiguration.EndlessMode) return true;
            MeshFilter mesh = target.GetComponent<MeshFilter>();
            if (mesh == null || mesh.sharedMesh == null) return true;
            foreach (Component component in source.GetComponents<Component>())
            {
                if (component == null || component is Transform || component is Renderer || component is MeshFilter || component is Collider || component is ObjectTagger) continue;
                Component copy = target.GetComponent(component.GetType());
                if (copy == null) copy = target.AddComponent(component.GetType());
                if (copy != null)
                {
                    try { JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(component), copy); }
                    catch (System.ArgumentException exception) { Debug.LogWarning("Could not copy handhold component " + component.GetType().Name + ": " + exception.Message); }
                }
            }
            if (target.GetComponent<Collider>() == null)
            {
                MeshCollider collider = target.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh.sharedMesh;
            }
            ObjectTagger tagger = target.GetComponent<ObjectTagger>();
            if (tagger == null) tagger = target.AddComponent<ObjectTagger>();
            tagger.AddTag("Handhold");
            CL_Handhold handhold = target.GetComponent<CL_Handhold>();
            if (handhold != null) handhold.handholdRenderer = target.GetComponent<Renderer>();
            return false;
        }
    }
}
