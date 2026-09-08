using UnityEngine;

public interface IPickupable
{
    bool IsHeavy { get; }
    string Name { get; }
    GameObject GameObject { get; }
    void Pickup(GameObject picker);
    void Drop(Vector3 position);
    void Use(GameObject user);
    void UseHeld(GameObject user);
    void ReleaseHeld(GameObject user);
}