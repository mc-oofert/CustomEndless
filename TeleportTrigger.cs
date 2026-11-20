using Celestial.CustomEndless;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class TeleportTrigger : MonoBehaviour
{
    private bool hasTriggered;
    private bool teleportPending;

    private void Reset()
    {
        BoxCollider boxCollider = GetComponent<BoxCollider>();
        if (boxCollider != null)
        {
            boxCollider.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered || teleportPending || other == null || other.GetComponentInParent<ENT_Player>() == null) return;


        teleportPending = true;
        EndlessConfiguration.TeleportPlayerToNextLevel(new string[] { "" }, success =>
        {
            if (this == null) return;
            teleportPending = false;
            hasTriggered = success;
            if (success) EndlessConfiguration.DeathFloorHeightPatch.HaltHeightUpdates = false;
        });
    }
}
