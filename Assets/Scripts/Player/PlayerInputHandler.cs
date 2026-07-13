using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{
    private IControllable controlledPlayer;
    public void SetControllable(IControllable player)
    {
        controlledPlayer = player;
    }
    public void OnMove(InputValue value)
    {
        controlledPlayer?.Move(value.Get<Vector2>());
    }

    public void OnLook(InputValue value)
    {
        controlledPlayer?.Look(value.Get<Vector2>());
    }

    public void OnJump(InputValue value)
    {
        if (value.isPressed)
            controlledPlayer?.Jump();
    }

    public void OnSprint(InputValue value)
    {
        controlledPlayer?.SetSprint(value.isPressed);
    }

    public void OnCrouch(InputValue value)
    {
        if (value.isPressed)
            controlledPlayer?.ToggleCrouch();
    }

    public void OnInteract(InputValue value)
    {
        if (value.isPressed)
            controlledPlayer?.Interact();
    }
    public void OnSlot1(InputValue value)
    {
        if (value.isPressed) controlledPlayer?.SelectSlot(0);
    }

    public void OnSlot2(InputValue value)
    {
        if (value.isPressed) controlledPlayer?.SelectSlot(1);
    }

    public void OnSlot3(InputValue value)
    {
        if (value.isPressed) controlledPlayer?.SelectSlot(2);
    }

    public void OnSlot4(InputValue value)
    {
        if (value.isPressed) controlledPlayer?.SelectSlot(3);
    }
}