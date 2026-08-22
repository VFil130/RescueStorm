using Mirror;
using System;
using UnityEngine;

[Serializable]
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
    private int selectedSlot;

    [Header("Item Display")]
    [SerializeField] private Transform handTransform;
    [SerializeField] private Transform headTransform;
    private GameObject currentDisplayedItem;

    private InventorySlot[] inventory;
    private int heavySlotIndex;

    public event Action<int, int> OnHealthChanged;
    public event Action<InventorySlot[]> OnInventoryChanged;
    public event Action<int> OnSelectedSlotChangedEvent;

    public int Health => health;
    public int MaxHealth => maxHealth;
    public int SelectedSlot => selectedSlot;
    public InventorySlot[] InventoryItems => inventory;

    private bool HasStateAuthority => NetworkServer.active || !NetworkClient.active;

    private void Awake()
    {
        heavySlotIndex = lightSlotsCount;
        inventory = new InventorySlot[lightSlotsCount + 1];
        for (int i = 0; i < inventory.Length; i++)
        {
            inventory[i] = new InventorySlot { slotIndex = i };
        }
    }
    private void LateUpdate()
    {
        DisplayItem();
    }

    public void TakeDamage(int amount)
    {
        if (!HasStateAuthority || health <= 0) return;
        health = Mathf.Max(0, health - amount);
        OnHealthChanged?.Invoke(health, maxHealth);
        if (health <= 0) Die();
    }

    public void Heal(int amount)
    {
        if (!HasStateAuthority) return;
        health = Mathf.Min(maxHealth, health + amount);
        OnHealthChanged?.Invoke(health, maxHealth);
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        Debug.Log($"Player OnStopClient: {gameObject.name}, netId: {GetComponent<NetworkIdentity>().netId}");
    }

    private void Die() { }

    public bool PickupItem(IPickupable item)
    {
        if (!HasStateAuthority || item == null) return false;

        if (item.IsHeavy)
        {
            if (inventory[heavySlotIndex].itemObject != null) return false;
            inventory[heavySlotIndex].itemObject = item;
            inventory[heavySlotIndex].slotIndex = heavySlotIndex;
            RpcPickupItemVisual(item.GameObject);
            SelectSlot(heavySlotIndex);
            OnInventoryChanged?.Invoke(inventory);
            RpcUpdateInventory();
            return true;
        }

        for (int i = 0; i < lightSlotsCount; i++)
        {
            if (inventory[i].itemObject == null)
            {
                inventory[i].itemObject = item;
                inventory[i].slotIndex = i;
                RpcPickupItemVisual(item.GameObject);
                item.GameObject.SetActive(false);
                OnInventoryChanged?.Invoke(inventory);
                RpcUpdateInventory();
                return true;
            }
        }
        return false;
    }
    [Command]
    public void CmdPickupItem(NetworkIdentity itemIdentity)
    {
        IPickupable item = itemIdentity.GetComponent<IPickupable>();
        if (item == null) return;

        item.Pickup(gameObject);
    }
    [ClientRpc]
    private void RpcUpdateInventory()
    {
        OnInventoryChanged?.Invoke(inventory);
    }
    [ClientRpc]
    private void RpcPickupItemVisual(GameObject item)
    {
        SetCollidersEnabled(item, false);
        SetItemLayer(item, true);
        item.SetActive(false);
    }

    public void SelectSlot(int slot)
    {
        if (!HasStateAuthority || !IsValidSlot(slot)) return;

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
            SetItemLayer(currentDisplayedItem, true);
            DisplayItem();
        }
        else
        {
            currentDisplayedItem = null;
        }

        OnSelectedSlotChangedEvent?.Invoke(selectedSlot);
    }

    public void UseItem()
    {
        if (!HasStateAuthority || !IsValidSlot(selectedSlot)) return;

        IPickupable item = inventory[selectedSlot].itemObject;
        if (item == null) return;

        item.Use(gameObject);

        if (item is ItemObject itemObject)
        {
            itemObject.charges--;
            if (itemObject.charges <= 0)
            {
                if (currentDisplayedItem == itemObject.gameObject)
                    currentDisplayedItem = null;
                DestroyConsumedItem(itemObject.gameObject);
                inventory[selectedSlot].itemObject = null;
            }
        }

        OnInventoryChanged?.Invoke(inventory);
    }

    public GameObject GetSelectedItem()
    {
        if (!IsValidSlot(selectedSlot)) return null;
        Debug.Log("GetSelectedItem" + inventory[selectedSlot]?.itemObject);
        return inventory[selectedSlot]?.itemObject.GameObject;
    }

    public IPickupable GetSelectedIPickable()
    {
        if (!IsValidSlot(selectedSlot)) return null;
        Debug.Log("GetSelectedItem" + inventory[selectedSlot]?.itemObject);
        return inventory[selectedSlot]?.itemObject;
    }

    public void RemoveSelectedItem()
    {
        if (!IsValidSlot(selectedSlot)) return;

        GameObject removedObject = inventory[selectedSlot].itemObject?.GameObject;
        if (currentDisplayedItem == removedObject)
            currentDisplayedItem = null;
        inventory[selectedSlot].itemObject = null;
        OnInventoryChanged?.Invoke(inventory);
    }

    public void ReplaceSelectedItem(GameObject newItemPrefab)
    {
        if (!HasStateAuthority || !IsValidSlot(selectedSlot) || newItemPrefab == null) return;

        DestroySelectedItem();

        Transform spawnPoint = handTransform != null ? handTransform : transform;
        GameObject newItem = Instantiate(newItemPrefab, spawnPoint.position, spawnPoint.rotation);
        if (NetworkServer.active)
            NetworkServer.Spawn(newItem);

        IPickupable pickupable = newItem.GetComponent<IPickupable>();
        if (pickupable == null)
        {
            DestroyConsumedItem(newItem);
            return;
        }

        inventory[selectedSlot].itemObject = pickupable;
        RpcPickupItemVisual(newItem);
        newItem.SetActive(false);
        OnInventoryChanged?.Invoke(inventory);
    }

    public void DestroySelectedItem()
    {
        if (!HasStateAuthority || !IsValidSlot(selectedSlot)) return;

        GameObject selectedItem = inventory[selectedSlot].itemObject?.GameObject;
        if (selectedItem == null) return;

        if (currentDisplayedItem == selectedItem)
            currentDisplayedItem = null;

        inventory[selectedSlot].itemObject = null;
        DestroyConsumedItem(selectedItem);
        OnInventoryChanged?.Invoke(inventory);
    }

    public GameObject DropItem()
    {
        if (!HasStateAuthority) return null;
        return DropItemFromSlot(selectedSlot);
    }

    public void DropFromSlot(int slotIndex)
    {
        if (!HasStateAuthority) return;
        DropItemFromSlot(slotIndex);
    }

    private GameObject DropItemFromSlot(int slotIndex)
    {
        if (!IsValidSlot(slotIndex)) return null;

        IPickupable item = inventory[slotIndex].itemObject;
        if (item == null) return null;

        GameObject droppedObject = item.GameObject;
        if (currentDisplayedItem == droppedObject)
            currentDisplayedItem = null;

        droppedObject.transform.SetParent(null, true);
        droppedObject.transform.position = transform.position + transform.forward * 2f;
        SetCollidersEnabled(droppedObject, true);
        SetItemLayer(droppedObject, false);
        droppedObject.SetActive(true);
        inventory[slotIndex].itemObject = null;
        OnInventoryChanged?.Invoke(inventory);
        return droppedObject;
    }

    private void DisplayItem()
    {
        if (currentDisplayedItem == null) return;

        Transform target = selectedSlot == heavySlotIndex ? headTransform : handTransform;
        if (target == null) return;

        currentDisplayedItem.transform.SetPositionAndRotation(target.position, target.rotation);
    }

    private static void SetCollidersEnabled(GameObject item, bool enabled)
    {
        foreach (Collider itemCollider in item.GetComponentsInChildren<Collider>(true))
            itemCollider.enabled = enabled;
    }

    private static void SetItemLayer(GameObject item, bool isHeld)
    {
        int layer = LayerMask.NameToLayer(isHeld ? "Item" : "Default");
        if (layer < 0) return;

        foreach (Transform child in item.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = layer;
    }

    private bool IsValidSlot(int slotIndex)
    {
        return slotIndex >= 0 && slotIndex <= heavySlotIndex;
    }

    private static void DestroyConsumedItem(GameObject item)
    {
        if (NetworkServer.active)
            NetworkServer.Destroy(item);
        else
            Destroy(item);
    }

    private void OnSelectedSlotChanged(int oldValue, int newValue)
    {
        OnSelectedSlotChangedEvent?.Invoke(newValue);
    }
}
