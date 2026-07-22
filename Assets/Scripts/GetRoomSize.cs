using UnityEngine;

[ExecuteInEditMode]
public class GetRoomSize : MonoBehaviour
{
    [ContextMenu("Get Size")]
    public void GetSize()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
        {
            Debug.LogWarning($"У объекта {gameObject.name} и его детей нет Renderer компонентов!");
            return;
        }

        Bounds combinedBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            combinedBounds.Encapsulate(renderers[i].bounds);
        }

        Vector3 size = combinedBounds.size;

        float x = Mathf.Round(size.x * 100f) / 100f;
        float z = Mathf.Round(size.z * 100f) / 100f;

        Debug.Log($"========================================");
        Debug.Log($"Комната: {gameObject.name}");
        Debug.Log($"Физический Размер (X x Z): {x} x {z}");
        Debug.Log($"Рекомендуемый cellSize (для 1 клетки): {Mathf.Max(x, z)}");
        Debug.Log($"========================================");
    }
}