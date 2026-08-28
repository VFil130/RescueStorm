using UnityEngine;

public class Vent : MonoBehaviour
{
    [SerializeField] private Transform spawnPoint;

    public Vector3 SpawnPosition =>
        spawnPoint != null ? spawnPoint.position : transform.position + Vector3.up;

    public Quaternion SpawnRotation =>
        spawnPoint != null ? spawnPoint.rotation : transform.rotation;

    private void OnEnable()
    {
        if (VentManager.Instance != null)
            VentManager.Instance.Register(this);
    }

    private void OnDisable()
    {
        if (VentManager.Instance != null)
            VentManager.Instance.Unregister(this);
    }
}
