using Mirror;
using UnityEngine;

public class CupItem : ItemObject
{
    [SyncVar] private Color cupColor;

    public void SetColor(Color color)
    {
        cupColor = color;
        var renderers = GetComponentsInChildren<MeshRenderer>();
        foreach (var renderer in renderers)
        {
            renderer.material.color = color;
        }
    }

    public Color GetColor()
    {
        return cupColor;
    }
}