using System;
using System.Collections;
using Mirror;
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
    [SerializeField] private GameObject startingRepairToolPrefab;
    [SyncVar(hook = nameof(OnSelectedSlotChanged))]
    private int selectedSlot;

    [Header("Item Display")]
    [SerializeField] private Transform handTransform;
    [SerializeField] private Transform headTransform;

    private readonly SyncList<uint> inventoryNetIds = new SyncList<uint>();
    private InventorySlot[] inventory;
    private GameObject currentDisplayedItem;
    private Coroutine inventoryRefreshCoroutine;
    private int heavySlotIndex;

    public event Action<int, int> OnHealthChanged;
    public event Action<InventorySlot[]> OnInventoryChanged;
    public event Action<int> OnSelectedSlotChangedEvent;

    public int Health => health;
    public int MaxHealth => maxHealth;
    public int SelectedSlot => selectedSlot;
    public InventorySlot[] InventoryItems => inventory;
    public GameObject StartingRepairToolPrefab => startingRepairToolPrefab;

    private bool HasStateAuthority => NetworkServer.active || !NetworkClient.active;

    private void Awake()
    {
        heavySlotIndex = lightSlotsCount;
        inventory = new InventorySlot[lightSlotsCount + 1];
        for (int i = 0; i < inventory.Length; i++)
            inventory[i] = new InventorySlot { slotIndex = i };
    }
    private void Start()
    {
        if (!NetworkClient.active && !NetworkServer.active)
            GiveStartingRepairTool(networked: false);
    }


    public override void OnStartServer()
    {
        base.OnStartServer();
        EnsureNetworkSlots();
        GiveStartingRepairTool(networked: true);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        inventoryNetIds.Callback += OnNetworkInventoryChanged;
        RefreshInventoryFromNetwork();
    }

    public override void OnStopClient()
    {
        inventoryNetIds.Callback -= OnNetworkInventoryChanged;
        if (inventoryRefreshCoroutine != null)
        {
            StopCoroutine(inventoryRefreshCoroutine);
            inventoryRefreshCoroutine = null;
        }

        base.OnStopClient();
    }

    private void GiveStartingRepairTool(bool networked)
    {
        if (startingRepairToolPrefab == null)
        {
            Debug.LogError("Player has no starting repair tool prefab.", this);
            return;
        }

        GameObject toolObject = Instantiate(
            startingRepairToolPrefab,
            transform.position,
            transform.rotation);
        RepairToolItem tool = toolObject.GetComponent<RepairToolItem>();
        if (tool == null)
        {
            Debug.LogError("Starting repair tool prefab needs RepairToolItem.", toolObject);
            Destroy(toolObject);
            return;
        }

        if (networked) NetworkServer.Spawn(toolObject);

        if (!PickupItem(tool))
        {
            if (networked)
                NetworkServer.Destroy(toolObject);
            else
                Destroy(toolObject);
            return;
        }

        SelectSlot(0);
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

    private void Die()
    {
    }

    public bool PickupItem(IPickupable item)
    {
        if (!HasStateAuthority || item == null) return false;

        int slot = item.IsHeavy ? heavySlotIndex : FindFreeLightSlot();
        if (!IsValidSlot(slot) || inventory[slot].itemObject != null) return false;

        SetInventorySlot(slot, item);
        if (item.IsHeavy) SelectSlot(slot);
        return true;
    }

    public void SelectSlot(int slot)
    {
        if (!HasStateAuthority || !IsValidSlot(slot)) return;

        selectedSlot = slot;
        ApplyInventoryVisuals();
        OnSelectedSlotChangedEvent?.Invoke(selectedSlot);
    }

    public void UseItem()
    {
        if (!HasStateAuthority || !IsValidSlot(selectedSlot)) return;

        IPickupable item = inventory[selectedSlot].itemObject;
        if (item == null) return;

        item.Use(gameObject);
        if (item is not ItemObject itemObject) return;

        itemObject.charges--;
        if (itemObject.charges > 0) return;

        GameObject consumedItem = itemObject.gameObject;
        SetInventorySlot(selectedSlot, null);
        DestroyNetworkObject(consumedItem);
    }

    public GameObject GetSelectedItem()
    {
        return GetSelectedIPickable()?.GameObject;
    }

    public IPickupable GetSelectedIPickable()
    {
        return IsValidSlot(selectedSlot) ? inventory[selectedSlot].itemObject : null;
    }

    public void RemoveSelectedItem()
    {
        if (!HasStateAuthority || !IsValidSlot(selectedSlot)) return;
        SetInventorySlot(selectedSlot, null);
    }

    public void ReplaceSelectedItem(GameObject newItemPrefab)
    {
        if (!HasStateAuthority || !IsValidSlot(selectedSlot) || newItemPrefab == null) return;

        DestroySelectedItem();

        Transform spawnPoint = handTransform != null ? handTransform : transform;
        GameObject newItem = Instantiate(newItemPrefab, spawnPoint.position, spawnPoint.rotation);
        IPickupable pickupable = newItem.GetComponent<IPickupable>();
        if (pickupable == null)
        {
            DestroyNetworkObject(newItem);
            return;
        }

        if (NetworkServer.active) NetworkServer.Spawn(newItem);
        SetInventorySlot(selectedSlot, pickupable);
    }

    public void DestroySelectedItem()
    {
        if (!HasStateAuthority || !IsValidSlot(selectedSlot)) return;

        GameObject selectedItem = inventory[selectedSlot].itemObject?.GameObject;
        if (selectedItem == null) return;

        SetInventorySlot(selectedSlot, null);
        DestroyNetworkObject(selectedItem);
    }

    public GameObject DropItem()
    {
        return HasStateAuthority ? DropItemFromSlot(selectedSlot) : null;
    }

    public void DropFromSlot(int slotIndex)
    {
        if (HasStateAuthority) DropItemFromSlot(slotIndex);
    }

    private GameObject DropItemFromSlot(int slotIndex)
    {
        if (!IsValidSlot(slotIndex)) return null;

        IPickupable item = inventory[slotIndex].itemObject;
        if (item == null) return null;

        Vector3 dropPosition = transform.position + transform.forward * 2f;
        GameObject droppedObject = item.GameObject;
        SetInventorySlot(slotIndex, null);
        item.Drop(dropPosition);
        SetWorldItemVisuals(droppedObject);
        return droppedObject;
    }

    private int FindFreeLightSlot()
    {
        for (int i = 0; i < lightSlotsCount; i++)
        {
            if (inventory[i].itemObject == null) return i;
        }

        return -1;
    }

    private void SetInventorySlot(int slotIndex, IPickupable item)
    {
        inventory[slotIndex].itemObject = item;

        if (NetworkServer.active)
        {
            EnsureNetworkSlots();
            inventoryNetIds[slotIndex] = GetNetworkId(item);
        }

        ApplyInventoryVisuals();
        OnInventoryChanged?.Invoke(inventory);
    }

    private void EnsureNetworkSlots()
    {
        while (inventoryNetIds.Count < inventory.Length)
            inventoryNetIds.Add(0);
    }

    private static uint GetNetworkId(IPickupable item)
    {
        if (item == null) return 0;

        NetworkIdentity identity = item.GameObject.GetComponent<NetworkIdentity>();
        return identity != null ? identity.netId : 0;
    }

    private void OnNetworkInventoryChanged(
        SyncList<uint>.Operation _,
        int __,
        uint oldItem,
        uint ___)
    {
        if (oldItem != 0 && !inventoryNetIds.Contains(oldItem) &&
            TryResolvePickupable(oldItem, out IPickupable removedItem))
            SetWorldItemVisuals(removedItem.GameObject);

        RefreshInventoryFromNetwork();
    }

    private void RefreshInventoryFromNetwork()
    {
        bool needsRetry = false;
        int count = Mathf.Min(inventory.Length, inventoryNetIds.Count);
        for (int i = 0; i < inventory.Length; i++)
        {
            uint netId = i < count ? inventoryNetIds[i] : 0;
            if (netId == 0)
            {
                inventory[i].itemObject = null;
                continue;
            }

            if (TryResolvePickupable(netId, out IPickupable item))
                inventory[i].itemObject = item;
            else
                needsRetry = true;
        }

        ApplyInventoryVisuals();
        OnInventoryChanged?.Invoke(inventory);

        if (needsRetry && inventoryRefreshCoroutine == null && isActiveAndEnabled)
            inventoryRefreshCoroutine = StartCoroutine(RefreshInventoryNextFrame());
    }

    private IEnumerator RefreshInventoryNextFrame()
    {
        yield return null;
        inventoryRefreshCoroutine = null;
        RefreshInventoryFromNetwork();
    }

    private static bool TryResolvePickupable(uint netId, out IPickupable item)
    {
        item = null;
        NetworkIdentity identity = null;

        if (NetworkClient.active)
            NetworkClient.spawned.TryGetValue(netId, out identity);
        if (identity == null && NetworkServer.active)
            NetworkServer.spawned.TryGetValue(netId, out identity);

        if (identity == null) return false;
        item = identity.GetComponent<IPickupable>();
        return item != null;
    }

    private void ApplyInventoryVisuals()
    {
        currentDisplayedItem = null;
        for (int i = 0; i < inventory.Length; i++)
        {
            IPickupable item = inventory[i].itemObject;
            if (item == null) continue;

            bool selected = i == selectedSlot;
            SetHeldItemVisuals(item.GameObject, selected);
            if (selected) currentDisplayedItem = item.GameObject;
        }
    }

    private void DisplayItem()
    {
        if (currentDisplayedItem == null) return;

        Transform target = selectedSlot == heavySlotIndex ? headTransform : handTransform;
        if (target != null)
            currentDisplayedItem.transform.SetPositionAndRotation(target.position, target.rotation);
    }

    private static void SetHeldItemVisuals(GameObject item, bool visible)
    {
        SetCollidersEnabled(item, false);
        SetRenderersEnabled(item, visible);
        SetItemLayer(item, true);
    }

    private static void SetWorldItemVisuals(GameObject item)
    {
        if (item == null) return;

        SetCollidersEnabled(item, true);
        SetRenderersEnabled(item, true);
        SetItemLayer(item, false);
    }

    private static void SetCollidersEnabled(GameObject item, bool enabled)
    {
        foreach (Collider itemCollider in item.GetComponentsInChildren<Collider>(true))
            itemCollider.enabled = enabled;
    }

    private static void SetRenderersEnabled(GameObject item, bool enabled)
    {
        foreach (Renderer itemRenderer in item.GetComponentsInChildren<Renderer>(true))
            itemRenderer.enabled = enabled;
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
        return slotIndex >= 0 && slotIndex < inventory.Length;
    }

    private static void DestroyNetworkObject(GameObject item)
    {
        if (item == null) return;

        if (NetworkServer.active)
            NetworkServer.Destroy(item);
        else
            Destroy(item);
    }

    private void OnSelectedSlotChanged(int _, int newValue)
    {
        selectedSlot = newValue;
        ApplyInventoryVisuals();
        OnSelectedSlotChangedEvent?.Invoke(newValue);
    }
}
