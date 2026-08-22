using Mirror;
using UnityEngine;

public class CupItem : ItemObject
{
    [SyncVar(hook = nameof(OnCupColorChanged))] private Color cupColor;

    public override void OnStartClient()
    {
        base.OnStartClient();
        ApplyColor(cupColor);
    }

    public void SetColor(Color color)
    {
        cupColor = color;
        ApplyColor(color);
    }

    public Color GetColor()
    {
        return cupColor;
    }

    private void OnCupColorChanged(Color _, Color newColor)
    {
        ApplyColor(newColor);
    }

    private void ApplyColor(Color color)
    {
        foreach (MeshRenderer meshRenderer in GetComponentsInChildren<MeshRenderer>())
            meshRenderer.material.color = color;
    }
}
