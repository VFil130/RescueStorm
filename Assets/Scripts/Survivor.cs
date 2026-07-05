using Mirror;
using UnityEngine;

public class Survivor : NetworkBehaviour
{
    [SyncVar] private SurvivorState state = SurvivorState.Waiting;
    [SyncVar] private uint carrierNetId;

    private GameObject carrier;
    private GameManager gameManager;
    private bool taskCompleted = false;

    public enum SurvivorState { Waiting, Carried, Saved }

    void Start()
    {
        if (isServer)
        {
            gameManager = FindObjectOfType<GameManager>();
        }
    }

    void Update()
    {
        if (!isServer || state != SurvivorState.Carried || carrier == null) return;
        transform.position = carrier.transform.position + Vector3.up * 0.5f;
    }

    [Server]
    public bool TryPickup(GameObject player)
    {
        if (state != SurvivorState.Waiting) return false;

        if (!taskCompleted)
        {
            RpcPlayEffect("Refuse", transform.position);
            return false;
        }

        carrier = player;
        carrierNetId = player.GetComponent<NetworkIdentity>().netId;
        state = SurvivorState.Carried;
        RpcPlayEffect("Pickup", transform.position);
        return true;
    }

    [Server]
    public void CompleteTask()
    {
        taskCompleted = true;
        RpcPlayEffect("TaskComplete", transform.position);
    }

    [Server]
    public void Drop()
    {
        if (state != SurvivorState.Carried) return;

        carrier = null;
        carrierNetId = 0;
        state = SurvivorState.Waiting;
        RpcPlayEffect("Drop", transform.position);
    }

    [Server]
    public void Save()
    {
        if (state == SurvivorState.Saved) return;

        state = SurvivorState.Saved;
        carrier = null;
        carrierNetId = 0;

        gameManager?.AddSavedSurvivor();
        RpcPlayEffect("Saved", transform.position);
    }

    [ClientRpc]
    private void RpcPlayEffect(string effectType, Vector3 position)
    {
        switch (effectType)
        {
            case "Pickup":
                Debug.Log($"Survivor picked up at {position}");
                break;
            case "Drop":
                Debug.Log($"Survivor dropped at {position}");
                break;
            case "Saved":
                Debug.Log($"Survivor saved at {position}");
                break;
            case "Refuse":
                Debug.Log($"Survivor refused at {position}");
                break;
            case "TaskComplete":
                Debug.Log($"Survivor task complete at {position}");
                break;
        }
    }

    public SurvivorState GetState() => state;
    public GameObject GetCarrier() => carrier;
    public bool IsTaskCompleted() => taskCompleted;
}