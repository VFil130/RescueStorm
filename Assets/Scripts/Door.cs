using Mirror;
using UnityEngine;

public class Door : NetworkBehaviour, IInteractable
{
    [Header("Door Settings")]
    [SerializeField] private Transform doorTransform;
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float closedYOffset = 0f;
    [SerializeField] private float openedYOffset = 3f;

    [SyncVar(hook = nameof(OnDoorStateChanged))]
    private bool isOpen = false;

    private Vector3 targetPosition;
    private bool isMoving = false;

    void Start()
    {
        if (doorTransform == null)
            doorTransform = transform;

        Vector3 pos = doorTransform.localPosition;
        pos.y = isOpen ? openedYOffset : closedYOffset;
        doorTransform.localPosition = pos;
        targetPosition = doorTransform.localPosition;
    }

    void Update()
    {
        if (!isMoving) return;

        Vector3 currentPos = doorTransform.localPosition;
        currentPos.y = Mathf.MoveTowards(currentPos.y, targetPosition.y, moveSpeed * Time.deltaTime);
        doorTransform.localPosition = currentPos;

        if (Mathf.Approximately(currentPos.y, targetPosition.y))
            isMoving = false;
    }

    public void Interact(GameObject interactor)
    {
        if (!isServer) return;
        isOpen = !isOpen;
        RpcOnDoorInteracted(interactor);
    }

    [ClientRpc]
    private void RpcOnDoorInteracted(GameObject interactor)
    {
        OnDoorInteracted(interactor);
    }

    private void OnDoorInteracted(GameObject interactor)
    {

    }

    private void OnDoorStateChanged(bool oldValue, bool newValue)
    {
        Vector3 pos = doorTransform.localPosition;
        pos.y = newValue ? openedYOffset : closedYOffset;
        targetPosition = pos;
        isMoving = true;
    }
}