using Mirror;
using UnityEngine;
using UnityEngine.Events;

public class InterButton : NetworkBehaviour, IInteractable
{
    [SerializeField] private float holdTime = 0.2f;

    public UnityEvent onPress;
    public UnityEvent onRelease;

    private bool isPressed = false;

    public void Interact(GameObject interactor)
    {
        if (!isServer || isPressed) return;

        isPressed = true;
        onPress.Invoke();
        Invoke(nameof(Release), holdTime);
    }

    private void Release()
    {
        isPressed = false;
        onRelease.Invoke();
    }
}