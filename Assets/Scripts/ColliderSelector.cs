using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
public class ColliderSelector : EditorWindow
{
    [MenuItem("Tools/Select All Colliders in Children")]
    static void SelectAllCollidersInChildren()
    {
        // Проверяем, выбран ли объект в иерархии
        if (Selection.activeGameObject == null)
        {
            Debug.LogWarning("Пожалуйста, выберите родительский объект.");
            return;
        }

        // Получаем все коллайдеры на выбранном объекте и его детях
        Collider[] colliders = Selection.activeGameObject.GetComponentsInChildren<Collider>();

        if (colliders.Length == 0)
        {
            Debug.Log("Коллайдеры не найдены.");
            return;
        }

        // Создаем список объектов, на которых есть коллайдеры, чтобы выделить их в иерархии
        List<GameObject> objectsWithColliders = new List<GameObject>();
        foreach (Collider col in colliders)
        {
            objectsWithColliders.Add(col.gameObject);
        }

        // Выделяем найденные объекты в иерархии
        Selection.objects = objectsWithColliders.ToArray();
        Debug.Log($"Выделено объектов с коллайдерами: {objectsWithColliders.Count}");
    }
}
