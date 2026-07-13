using Mirror;
using UnityEngine;

public class PlaceablePoint : MonoBehaviour, IInteractable
{
    [SerializeField] private string requiredItem;
    [SerializeField] private Transform placePoint;

    public void Interact(GameObject gm)
    {
        if (!NetworkServer.active) return;

        Player player = gm.GetComponent<Player>();
        IPickupable item = player?.GetSelectedIPickable();

        if (item == null || item.Name != requiredItem) return;

        GameObject requiredGm = player.DropItem();
        if (requiredGm != null) requiredGm.transform.position = placePoint.position;
    }
}
