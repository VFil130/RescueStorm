using Mirror;
using System.Linq;
using UnityEngine;

public class Interactor : NetworkBehaviour
{
    [SerializeField] private float interactionRange = 2.5f;
    [SerializeField] private LayerMask interactionMask = -1;

    private Camera playerCamera;
    private IInteractable currentInteractable;

    private bool HasLocalControl => isLocalPlayer || (!NetworkClient.active && !NetworkServer.active);

    private void Start()
    {
        playerCamera = GetComponentInChildren<Camera>();
    }

    private void Update()
    {
        if (!HasLocalControl) return;
        CheckInteraction();
    }

    public void DoInteract()
    {
        if (currentInteractable == null) return;

        if (!NetworkClient.active && !NetworkServer.active)
        {
            currentInteractable.Interact(gameObject);
            return;
        }

        if (!isLocalPlayer) return;

        Component targetComponent = currentInteractable as Component;
        NetworkIdentity targetIdentity = targetComponent != null
            ? targetComponent.GetComponentInParent<NetworkIdentity>()
            : null;
        if (targetIdentity == null || targetIdentity.netId == 0)
        {
            Debug.LogWarning(
                $"Network interaction target '{targetComponent?.name}' has no spawned NetworkIdentity.",
                targetComponent);
            return;
        }

        IInteractable[] interactables = FindInteractables(targetIdentity);
        int interactableIndex = System.Array.IndexOf(interactables, currentInteractable);
        if (interactableIndex < 0) return;

        CmdInteract(targetIdentity, interactableIndex);
    }

    [Command]
    private void CmdInteract(NetworkIdentity targetIdentity, int interactableIndex)
    {
        if (targetIdentity == null) return;

        IInteractable[] interactables = FindInteractables(targetIdentity);
        if (interactableIndex < 0 || interactableIndex >= interactables.Length) return;
        IInteractable target = interactables[interactableIndex];

        Component targetComponent = target as Component;
        if (targetComponent == null) return;

        float maximumDistance = interactionRange + 0.5f;
        if ((targetComponent.transform.position - transform.position).sqrMagnitude >
            maximumDistance * maximumDistance)
            return;

        target.Interact(gameObject);
    }

    private static IInteractable[] FindInteractables(NetworkIdentity identity)
    {
        return identity
            .GetComponentsInChildren<MonoBehaviour>(true)
            .OfType<IInteractable>()
            .ToArray();
    }

    private void CheckInteraction()
    {
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));

        if (Physics.Raycast(ray, out RaycastHit hit, interactionRange, interactionMask))
        {
            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();

            if (interactable != null)
            {
                if (currentInteractable != interactable)
                {
                    currentInteractable = interactable;
                }
                return;
            }
        }

        if (currentInteractable != null)
        {
            currentInteractable = null;
        }
    }

    public bool HasTarget()
    {
        return currentInteractable != null;
    }
}
