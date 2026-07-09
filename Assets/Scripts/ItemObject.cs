using Mirror;
using UnityEngine;

public class ItemObject : NetworkBehaviour, IInteractable, IPickupable
{
    [Header("Item Data")]
    public string itemName;
    public int charges = 1;
    public bool isHeavy = false;
    public Sprite icon;

    public void Interact(GameObject interactor)
    {
        Debug.Log("inter");
        Player player = interactor.GetComponent<Player>();
        if (player != null)
        {
            Pickup(interactor);
        }
    }

    public bool Pickup(GameObject picker)
    {
        Player player = picker.GetComponent<Player>();
        if (player == null) return false;
        return player.PickupItem(this);
    }

    public void Drop(Vector3 position)
    {
        transform.position = position;
        gameObject.SetActive(true);
    }
}