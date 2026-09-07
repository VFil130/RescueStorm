using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{
    public UnityEvent OnTalkStarted;
    public UnityEvent OnTalkStopped;

    [SerializeField] private InputActionAsset inputActions;

    private IControllable controlledPlayer;
    private InputActionMap playerActions;
    private bool inputEnabled;

    private void Awake()
    {
        controlledPlayer = GetComponent<IControllable>();
        if (inputActions == null)
        {
            Debug.LogError("Player input actions are not assigned.", this);
            return;
        }

        playerActions = inputActions.FindActionMap("Player");
        if (playerActions == null)
            Debug.LogError("Input action map 'Player' was not found.", this);
    }

    public void EnableInput()
    {
        if (inputEnabled || playerActions == null) return;

        SubscribeToActions();
        playerActions.Enable();
        inputEnabled = true;
    }

    public void DisableInput()
    {
        if (!inputEnabled || playerActions == null) return;

        playerActions.Disable();
        UnsubscribeFromActions();
        inputEnabled = false;
    }

    private void OnDisable()
    {
        DisableInput();
    }

    private void SubscribeToActions()
    {
        playerActions.FindAction("Move").performed += OnMove;
        playerActions.FindAction("Move").canceled += OnMoveCanceled;
        playerActions.FindAction("Look").performed += OnLook;
        playerActions.FindAction("Attack").started += OnPrimaryActionStarted;
        playerActions.FindAction("Attack").canceled += OnPrimaryActionStopped;
        playerActions.FindAction("Jump").performed += OnJump;
        playerActions.FindAction("Sprint").performed += OnSprintStarted;
        playerActions.FindAction("Sprint").canceled += OnSprintStopped;
        playerActions.FindAction("Crouch").performed += OnCrouch;
        playerActions.FindAction("Interact").performed += OnInteract;
        playerActions.FindAction("Slot1").performed += OnSlot1;
        playerActions.FindAction("Slot2").performed += OnSlot2;
        playerActions.FindAction("Slot3").performed += OnSlot3;
        playerActions.FindAction("Slot4").performed += OnSlot4;
        playerActions.FindAction("Slot5").performed += OnSlot5;
        playerActions.FindAction("Talk").performed += OnTalk;
        playerActions.FindAction("Talk").canceled += OnTalkCanceled;
        playerActions.FindAction("Flashlight").performed += OnFlashlight;
    }

    private void UnsubscribeFromActions()
    {
        playerActions.FindAction("Move").performed -= OnMove;
        playerActions.FindAction("Move").canceled -= OnMoveCanceled;
        playerActions.FindAction("Look").performed -= OnLook;
        playerActions.FindAction("Attack").started -= OnPrimaryActionStarted;
        playerActions.FindAction("Attack").canceled -= OnPrimaryActionStopped;
        playerActions.FindAction("Jump").performed -= OnJump;
        playerActions.FindAction("Sprint").performed -= OnSprintStarted;
        playerActions.FindAction("Sprint").canceled -= OnSprintStopped;
        playerActions.FindAction("Crouch").performed -= OnCrouch;
        playerActions.FindAction("Interact").performed -= OnInteract;
        playerActions.FindAction("Slot1").performed -= OnSlot1;
        playerActions.FindAction("Slot2").performed -= OnSlot2;
        playerActions.FindAction("Slot3").performed -= OnSlot3;
        playerActions.FindAction("Slot4").performed -= OnSlot4;
        playerActions.FindAction("Slot5").performed -= OnSlot5;
        playerActions.FindAction("Talk").performed -= OnTalk;
        playerActions.FindAction("Talk").canceled -= OnTalkCanceled;
        playerActions.FindAction("Flashlight").performed -= OnFlashlight;
    }

    private void OnMove(InputAction.CallbackContext context)
    {
        controlledPlayer?.Move(context.ReadValue<Vector2>());
    }

    private void OnMoveCanceled(InputAction.CallbackContext _)
    {
        controlledPlayer?.Move(Vector2.zero);
    }

    private void OnLook(InputAction.CallbackContext context)
    {
        controlledPlayer?.Look(context.ReadValue<Vector2>());
    }
    private void OnPrimaryActionStarted(InputAction.CallbackContext _)
    {
        controlledPlayer?.SetPrimaryAction(true);
    }

    private void OnPrimaryActionStopped(InputAction.CallbackContext _)
    {
        controlledPlayer?.SetPrimaryAction(false);
    }


    private void OnJump(InputAction.CallbackContext _)
    {
        controlledPlayer?.Jump();
    }

    private void OnSprintStarted(InputAction.CallbackContext _)
    {
        controlledPlayer?.SetSprint(true);
    }

    private void OnSprintStopped(InputAction.CallbackContext _)
    {
        controlledPlayer?.SetSprint(false);
    }

    private void OnCrouch(InputAction.CallbackContext _)
    {
        controlledPlayer?.ToggleCrouch();
    }

    private void OnInteract(InputAction.CallbackContext _)
    {
        controlledPlayer?.Interact();
    }

    private void OnSlot1(InputAction.CallbackContext _)
    {
        controlledPlayer?.SelectSlot(0);
    }

    private void OnSlot2(InputAction.CallbackContext _)
    {
        controlledPlayer?.SelectSlot(1);
    }

    private void OnSlot3(InputAction.CallbackContext _)
    {
        controlledPlayer?.SelectSlot(2);
    }

    private void OnSlot4(InputAction.CallbackContext _)
    {
        controlledPlayer?.SelectSlot(3);
    }
    private void OnSlot5(InputAction.CallbackContext _)
    {
        controlledPlayer?.SelectSlot(4);
    }


    private void OnTalk(InputAction.CallbackContext _)
    {
        OnTalkStarted?.Invoke();
    }

    private void OnTalkCanceled(InputAction.CallbackContext _)
    {
        OnTalkStopped?.Invoke();
    }

    private void OnFlashlight(InputAction.CallbackContext _)
    {
        controlledPlayer?.ToggleFlashlight();
    }
}
