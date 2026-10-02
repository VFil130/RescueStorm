using UnityEngine;

[DisallowMultipleComponent]
public sealed class MiniGameSpawnPoint : MonoBehaviour
{
    [SerializeField] private GameObject miniGamePrefab;

    public GameObject MiniGamePrefab => miniGamePrefab;

    private void OnDrawGizmos()
    {
        if (miniGamePrefab == null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(Vector3.zero, 0.2f);
            return;
        }

        Bounds bounds = GetPrefabBounds();
        Gizmos.color = new Color(0.3f, 1f, 0.3f, 0.9f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }

    private Bounds GetPrefabBounds()
    {
        Renderer[] renderers = miniGamePrefab.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.one);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

        return bounds;
    }
}