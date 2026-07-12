using System.Collections.Generic;
using UnityEngine;

public class RoomData : MonoBehaviour
{
    public Vector2Int size = Vector2Int.one;
    public Transform survivorSpawnPoint;
    public List<Transform> itemSpawnPoints = new List<Transform>();
}