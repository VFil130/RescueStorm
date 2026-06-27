using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{
    public Vector2 MoveInput { get; private set; }
    public bool IsJumping { get; private set; }
    public bool IsSprinting { get; private set; }
    public bool IsCrouching { get; private set; }

    public void OnMove(InputValue value)
    {
        MoveInput = value.Get<Vector2>();
    }

    public void OnJump(InputValue value)
    {
        IsJumping = value.isPressed;
    }

    public void OnSprint(InputValue value)
    {
        IsSprinting = value.isPressed;
    }

    public void OnCrouch(InputValue value)
    {
        IsCrouching = value.isPressed;
    }
}