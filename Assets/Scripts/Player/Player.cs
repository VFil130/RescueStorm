using Mirror;
using UnityEngine;

[System.Serializable]
public class InventorySlot
{
    public IPickupable itemObject;
    public int slotIndex;
}

public class Player : NetworkBehaviour
{
    [Header("Stats")]
    [SyncVar][SerializeField] private int health = 5;
    [SyncVar][SerializeField] private int maxHealth = 100;

    [Header("Inventory")]
    [SerializeField] private int lightSlotsCount = 4;
    [SyncVar(hook = nameof(OnSelectedSlotChanged))]
    private int selectedSlot = 0;
    [Header("Item Display")]
    [SerializeField] private Transform handTransform;
    [SerializeField] private Transform headTransform;
    private GameObject currentDisplayedItem;

    private InventorySlot[] inventory;
    private int heavySlotIndex;

    public event System.Action<int, int> OnHealthChanged;
    public event System.Action<InventorySlot[]> OnInventoryChanged;
    public event System.Action<int> OnSelectedSlotChangedEvent;

    public int Health => health;
    public int MaxHealth => maxHealth;
    public int SelectedSlot => selectedSlot;
    public InventorySlot[] InventoryItems => inventory;

    void Awake()
    {
        heavySlotIndex = lightSlotsCount;
        inventory = new InventorySlot[lightSlotsCount + 1];
        for (int i = 0; i < inventory.Length; i++)
        {
            inventory[i] = new InventorySlot();
            inventory[i].slotIndex = i;
        }
    }
    private void Update()
    {
        DisplayItem();
    }
    [Server]
    public void TakeDamage(int amount)
    {
        if (health <= 0) return;
        health = Mathf.Max(0, health - amount);
        OnHealthChanged?.Invoke(health, maxHealth);
        if (health <= 0) Die();
    }

    [Server]
    public void Heal(int amount)
    {
        health = Mathf.Min(maxHealth, health + amount);
        OnHealthChanged?.Invoke(health, maxHealth);
    }
    public override void OnStopClient()
    {
        base.OnStopClient();
        Debug.Log($"Player OnStopClient: {gameObject.name}, netId: {GetComponent<NetworkIdentity>().netId}");
    }
    [Server]
    private void Die() { }

    [Server]
    public bool PickupItem(IPickupable item)
    {
        if (item == null) return false;

        if (item.IsHeavy)
        {
            if (inventory[heavySlotIndex].itemObject != null) return false;
            inventory[heavySlotIndex].itemObject = item;
            inventory[heavySlotIndex].slotIndex = heavySlotIndex;
            SelectSlot(heavySlotIndex);
            item.GameObject.SetActive(false);
            SetCollidersEnabled(item.GameObject, false);
            OnInventoryChanged?.Invoke(inventory);
            OnSelectedSlotChangedEvent?.Invoke(selectedSlot);
            SetItemLayer(item.GameObject, true);
            return true;
        }

        for (int i = 0; i < lightSlotsCount; i++)
        {
            if (inventory[i].itemObject == null)
            {
                inventory[i].itemObject = item;
                inventory[i].slotIndex = i;
                item.GameObject.SetActive(false);
                SetCollidersEnabled(item.GameObject, false);
                OnInventoryChanged?.Invoke(inventory);
                SetItemLayer(item.GameObject, true);
                return true;
            }
        }
        return false;
    }

    [Server]
    public void SelectSlot(int slot)
    {
        if (slot > heavySlotIndex) return;

        selectedSlot = slot;

        for (int i = 0; i < inventory.Length; i++)
        {
            if (inventory[i].itemObject != null)
            {
                inventory[i].itemObject.GameObject.SetActive(false);
            }
        }

        if (inventory[slot].itemObject != null)
        {
            inventory[slot].itemObject.GameObject.SetActive(true);
            currentDisplayedItem = inventory[slot].itemObject.GameObject;
        }
        else
        {
            currentDisplayedItem = null;
        }
        if (inventory[slot].itemObject != null)
        {
            SetItemLayer(inventory[slot].itemObject.GameObject, true);
        }
        OnSelectedSlotChangedEvent?.Invoke(selectedSlot);
    }

    [Server]
    public void UseItem()
    {
        if (selectedSlot < 0 || selectedSlot > heavySlotIndex) return;

        IPickupable item = inventory[selectedSlot].itemObject;
        if (item == null) return;

        item.Use(this.gameObject);

        ItemObject itemObject = item as ItemObject;
        if (itemObject != null)
        {
            itemObject.charges--;
            if (itemObject.charges <= 0)
            {
                NetworkServer.Destroy(itemObject.gameObject);
                inventory[selectedSlot].itemObject = null;
            }
        }

        OnInventoryChanged?.Invoke(inventory);
    }
    public GameObject GetSelectedItem()
    {
        if (selectedSlot < 0 || selectedSlot > heavySlotIndex) return null;
        Debug.Log("GetSelectedItem" + inventory[selectedSlot]?.itemObject);
        return inventory[selectedSlot]?.itemObject.GameObject;
    }
    public IPickupable GetSelectedIPickable()
    {
        if (selectedSlot < 0 || selectedSlot > heavySlotIndex) return null;
        Debug.Log("GetSelectedItem" + inventory[selectedSlot]?.itemObject);
        return inventory[selectedSlot]?.itemObject;
    }

    public void RemoveSelectedItem()
    {
        if (selectedSlot < 0 || selectedSlot > heavySlotIndex) return;
        inventory[selectedSlot].itemObject = null;
        OnInventoryChanged?.Invoke(inventory);
    }
    [Server]
    public GameObject DropItem()
    {
        if (selectedSlot < 0 || selectedSlot > heavySlotIndex) return null;

        IPickupable item = inventory[selectedSlot].itemObject;
        if (item == null) return null;

        item.GameObject.transform.position = transform.position + transform.forward * 2f;
        item.GameObject.SetActive(true);
        SetCollidersEnabled(item.GameObject, true);
        inventory[selectedSlot].itemObject = null;
        currentDisplayedItem = null;
        OnInventoryChanged?.Invoke(inventory);
        SetItemLayer(item.GameObject, false);
        return item.GameObject;
    }

    private void DisplayItem()
    {
        if (currentDisplayedItem == null) return;

        Transform targetTransform = (selectedSlot == heavySlotIndex) ? headTransform : handTransform;
        currentDisplayedItem.transform.position = targetTransform.position;
        currentDisplayedItem.transform.rotation = targetTransform.rotation;
    }
    private void SetCollidersEnabled(GameObject obj, bool enabled)
    {
        foreach (var collider in obj.GetComponentsInChildren<Collider>())
        {
            collider.enabled = enabled;
        }
    }
    private void SetItemLayer(GameObject obj, bool isHeld)
    {
        int layer = isHeld ? LayerMask.NameToLayer("Item") : LayerMask.NameToLayer("Default");
        obj.layer = layer;
    }
    private void OnSelectedSlotChanged(int oldValue, int newValue)
    {
        OnSelectedSlotChangedEvent?.Invoke(newValue);
    }
}