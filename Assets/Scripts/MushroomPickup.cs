using UnityEngine;

/// <summary>
/// A single-use collectible. Works with the project's existing trigger colliders.
/// </summary>
public class MushroomPickup : MonoBehaviour
{
    public bool IsCollected { get; private set; }

    // Returns false on a second collection attempt, preventing double counting.
    public bool TryCollect()
    {
        if (IsCollected || !gameObject.activeInHierarchy) return false;
        IsCollected = true;
        DisableForCollection();
        return true;
    }

    // Keep the method name used by the original project.
    public void DisableForCollection()
    {
        foreach (Collider collider in GetComponentsInChildren<Collider>())
            collider.enabled = false;
    }
}
