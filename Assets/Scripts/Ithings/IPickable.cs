using UnityEngine;

public interface IPickupable
{
    bool Pickup(GameObject picker);
    void Drop(Vector3 position);
}