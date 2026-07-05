using UnityEngine;

public class Interactor : MonoBehaviour
{
    [SerializeField] private float interactionRange = 2.5f;
    [SerializeField] private LayerMask interactionMask = -1;
    [SerializeField] private GameObject promptUI;

    private Camera playerCamera;
    private IInteractable currentInteractable;

    void Start()
    {
        playerCamera = GetComponentInChildren<Camera>();
        if (promptUI != null)
            promptUI.SetActive(false);
    }
    void Update()
    {
        CheckInteraction();
    }
    public void DoInteract()
    {
        currentInteractable?.Interact(this.gameObject);
    }

    private void CheckInteraction()
    {
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));

        if (Physics.Raycast(ray, out RaycastHit hit, interactionRange, interactionMask))
        {
            IInteractable interactable = hit.collider.GetComponent<IInteractable>();

            if (interactable != null)
            {
                if (currentInteractable != interactable)
                {
                    currentInteractable = interactable;
                    ShowPrompt(true);
                }
                return;
            }
        }

        if (currentInteractable != null)
        {
            currentInteractable = null;
            ShowPrompt(false);
        }
    }
    private void ShowPrompt(bool show)
    {
        if (promptUI != null)
            promptUI.SetActive(show);
    }
}