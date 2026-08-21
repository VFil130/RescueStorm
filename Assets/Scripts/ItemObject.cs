using Mirror;
using Unity.VisualScripting;
using UnityEngine;

public class ItemObject : NetworkBehaviour, IInteractable, IPickupable
{
    [Header("Item Data")]
    public int charges = 1;
    public GameObject GameObject => gameObject;
    [SerializeField] private string itemName;
    [SerializeField] private bool isHeavy = false;
    public bool IsHeavy => isHeavy;
    public string Name => itemName;
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

    public void Pickup(GameObject picker)
    {
        Player player = picker.GetComponent<Player>();
        if (player == null) return;
        player.PickupItem(this);
    }

    public void Drop(Vector3 position)
    {
        transform.position = position;
        gameObject.SetActive(true);
    }
    public void Use(GameObject GM)
    {
        Debug.Log("Using " + itemName);
    }
}