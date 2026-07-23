using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{
    private IControllable controlledPlayer;
    public UnityEvent OnTalkStarted;
    public UnityEvent OnTalkStopped;
    [SerializeField] private InputActionAsset inputActions;

    void Awake()
    {
        controlledPlayer = GetComponent<IControllable>();

        var map = inputActions.FindActionMap("Player");

        map.FindAction("Move").performed += ctx => controlledPlayer?.Move(ctx.ReadValue<Vector2>());
        map.FindAction("Move").canceled += ctx => controlledPlayer?.Move(Vector2.zero);

        map.FindAction("Look").performed += ctx => controlledPlayer?.Look(ctx.ReadValue<Vector2>());

        map.FindAction("Jump").performed += ctx => controlledPlayer?.Jump();

        map.FindAction("Sprint").performed += ctx => controlledPlayer?.SetSprint(true);
        map.FindAction("Sprint").canceled += ctx => controlledPlayer?.SetSprint(false);

        map.FindAction("Crouch").performed += ctx => controlledPlayer?.ToggleCrouch();

        map.FindAction("Interact").performed += ctx => controlledPlayer?.Interact();

        map.FindAction("Slot1").performed += ctx => controlledPlayer?.SelectSlot(0);
        map.FindAction("Slot2").performed += ctx => controlledPlayer?.SelectSlot(1);
        map.FindAction("Slot3").performed += ctx => controlledPlayer?.SelectSlot(2);
        map.FindAction("Slot4").performed += ctx => controlledPlayer?.SelectSlot(3);

        map.FindAction("Talk").performed += ctx => OnTalkStarted?.Invoke();
        map.FindAction("Talk").canceled += ctx => OnTalkStopped?.Invoke();
    }

    void OnEnable() => inputActions.Enable();
    void OnDisable() => inputActions.Disable();
}