using UnityEngine;

public interface IPickupable
{
    bool IsHeavy { get; }
    string Name { get; }
    GameObject GameObject { get; }
    bool Pickup(GameObject picker);
    void Drop(Vector3 position);
    void Use(GameObject user);
}