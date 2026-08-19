using UnityEngine;

[DisallowMultipleComponent]
public class DoorPoint : MonoBehaviour
{
    [Tooltip("Legacy local X/Z direction. Existing room prefabs already contain this value.")]
    public Vector2Int direction = Vector2Int.up;

    [SerializeField]
    [Tooltip("Use the marker's blue Z axis instead of the legacy direction field.")]
    private bool useTransformForward;

    [SerializeField, HideInInspector] private bool isOccupied;

    public bool IsConnected { get; private set; }
    public bool IsSealed { get; private set; }
    public bool IsOccupied => isOccupied;
    public bool UsesTransformForward => useTransformForward;
    public DoorPoint ConnectedTo { get; private set; }

    public Vector3 GetWorldDirection(Transform roomRoot)
    {
        Vector3 worldDirection;
        if (useTransformForward)
        {
            worldDirection = transform.forward;
        }
        else
        {
            Vector3 localDirection = new Vector3(direction.x, 0f, direction.y);
            worldDirection = roomRoot.TransformDirection(localDirection);
        }

        worldDirection.y = 0f;
        return worldDirection.sqrMagnitude > 0.0001f
            ? worldDirection.normalized
            : roomRoot.forward;
    }

    internal void Connect(DoorPoint other)
    {
        IsConnected = true;
        IsSealed = false;
        ConnectedTo = other;
        isOccupied = true;
    }

    internal void Disconnect(DoorPoint expectedOther)
    {
        if (ConnectedTo != expectedOther) return;

        IsConnected = false;
        ConnectedTo = null;
        isOccupied = false;
    }

    internal void Seal()
    {
        if (IsConnected) return;

        IsSealed = true;
        isOccupied = true;
    }

    internal void ResetRuntimeState()
    {
        IsConnected = false;
        IsSealed = false;
        ConnectedTo = null;
        isOccupied = false;
    }

    private void OnDrawGizmos()
    {
        RoomData room = GetComponentInParent<RoomData>();
        Transform roomRoot = room != null ? room.transform : transform.parent;
        if (roomRoot == null) roomRoot = transform;

        Gizmos.color = IsConnected ? Color.green : IsSealed ? Color.gray : Color.cyan;
        Gizmos.DrawSphere(transform.position, 0.12f);
        Gizmos.DrawLine(transform.position, transform.position + GetWorldDirection(roomRoot));
    }
}
