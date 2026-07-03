using UnityEngine;

[RequireComponent(typeof(Camera))]
public class AutoFocus : MonoBehaviour
{
    public Material grainMaterial; 
    public float smoothSpeed = 5f; 
    public float maxDistance = 100f;
    public LayerMask layerMask = -1; 

    private float currentFocusDistance = 5f;

    void Update()
    {
        if (grainMaterial == null) return;

        Ray ray = GetComponent<Camera>().ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, layerMask))
        {
            currentFocusDistance = Mathf.Lerp(currentFocusDistance, hit.distance, Time.deltaTime * smoothSpeed);
        }
        
        grainMaterial.SetFloat("_FocusDistance", currentFocusDistance);
    }
}