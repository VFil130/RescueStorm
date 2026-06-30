using UnityEngine;

public interface IControllable
{
    void Move(Vector2 input);
    void Look(Vector2 input);
    void Jump();
    void SetSprint(bool isSprinting);
    void ToggleCrouch();
    void Interact();
}