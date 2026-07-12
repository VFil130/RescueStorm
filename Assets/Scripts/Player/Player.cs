using Mirror;
using UnityEngine;

[System.Serializable]
public class InventorySlot
{
    public ItemObject itemObject;
    public int slotIndex;
}

public class Player : NetworkBehaviour
{
    [Header("Stats")]
    [SyncVar] private int health = 5;
    [SyncVar][SerializeField] private int maxHealth = 100;

    [Header("Inventory")]
    [SerializeField] private int lightSlotsCount = 4;
    [SyncVar(hook = nameof(OnSelectedSlotChanged))]
    private int selectedSlot = 0;

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

    [Server]
    private void Die() { }

    [Server]
    public bool PickupItem(ItemObject item)
    {
        if (item == null) return false;

        if (item.isHeavy)
        {
            if (inventory[heavySlotIndex].itemObject != null) return false;
            inventory[heavySlotIndex].itemObject = item;
            inventory[heavySlotIndex].slotIndex = heavySlotIndex;
            selectedSlot = heavySlotIndex;
            item.gameObject.SetActive(false);
            OnInventoryChanged?.Invoke(inventory);
            OnSelectedSlotChangedEvent?.Invoke(selectedSlot);
            return true;
        }

        for (int i = 0; i < lightSlotsCount; i++)
        {
            if (inventory[i].itemObject == null)
            {
                inventory[i].itemObject = item;
                inventory[i].slotIndex = i;
                item.gameObject.SetActive(false);
                OnInventoryChanged?.Invoke(inventory);
                return true;
            }
        }
        return false;
    }

    [Server]
    public void SelectSlot(int slot)
    {
        if (slot < 0 || slot > heavySlotIndex) return;
        if (inventory[slot].itemObject == null) return;

        selectedSlot = slot;
        OnSelectedSlotChangedEvent?.Invoke(selectedSlot);
    }

    [Server]
    public void UseItem()
    {
        if (selectedSlot < 0 || selectedSlot > heavySlotIndex) return;

        ItemObject item = inventory[selectedSlot].itemObject;
        if (item == null) return;

        if (item.itemName == "Medkit")
        {
            Heal(30);
        }
        else if (item.itemName == "Battery")
        {
            // зарядить генератор
        }

        item.charges--;

        if (item.charges <= 0)
        {
            NetworkServer.Destroy(item.gameObject);
            inventory[selectedSlot].itemObject = null;
        }

        OnInventoryChanged?.Invoke(inventory);
    }
    public ItemObject GetSelectedItem()
    {
        if (selectedSlot < 0 || selectedSlot > heavySlotIndex) return null;
        return inventory[selectedSlot]?.itemObject;
    }

    public void RemoveSelectedItem()
    {
        if (selectedSlot < 0 || selectedSlot > heavySlotIndex) return;
        inventory[selectedSlot].itemObject = null;
        OnInventoryChanged?.Invoke(inventory);
    }
    [Server]
    public void DropItem()
    {
        if (selectedSlot < 0 || selectedSlot > heavySlotIndex) return;

        ItemObject item = inventory[selectedSlot].itemObject;
        if (item == null) return;

        item.gameObject.transform.position = transform.position + transform.forward * 2f;
        item.gameObject.SetActive(true);
        inventory[selectedSlot].itemObject = null;

        OnInventoryChanged?.Invoke(inventory);
    }

    [Server]
    public void DropFromSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex > heavySlotIndex) return;

        ItemObject item = inventory[slotIndex].itemObject;
        if (item == null) return;

        item.gameObject.transform.position = transform.position + transform.forward * 2f;
        item.gameObject.SetActive(true);
        inventory[slotIndex].itemObject = null;

        OnInventoryChanged?.Invoke(inventory);
    }

    private void OnSelectedSlotChanged(int oldValue, int newValue)
    {
        OnSelectedSlotChangedEvent?.Invoke(newValue);
    }
}