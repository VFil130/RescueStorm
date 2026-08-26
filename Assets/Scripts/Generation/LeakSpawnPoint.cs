using UnityEngine;

[DisallowMultipleComponent]
public sealed class LeakSpawnPoint : MonoBehaviour
{
    [SerializeField] private Vector3 leakOffset;
    [SerializeField] private Vector3 leakEulerAngles;

    public Vector3 Position => transform.TransformPoint(leakOffset);
    public Quaternion Rotation => transform.rotation * Quaternion.Euler(leakEulerAngles);

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(Position, 0.2f);
        Gizmos.DrawLine(Position, Position + Rotation * Vector3.forward * 0.5f);
    }
}
