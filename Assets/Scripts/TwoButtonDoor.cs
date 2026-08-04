using Mirror;
using UnityEngine;

public class TwoButtonDoor : NetworkBehaviour
{
    [SerializeField] private Transform doorTransform;
    [SerializeField] private float openHeight = 3f;
    [SerializeField] private float speed = 2f;

    [SyncVar] private bool isOpen = false;
    private int pressCount = 0;
    private Vector3 targetPos;

    void Start()
    {
        targetPos = doorTransform.localPosition;
    }

    void Update()
    {
        Vector3 pos = doorTransform.localPosition;
        pos.y = Mathf.MoveTowards(pos.y, targetPos.y, speed * Time.deltaTime);
        doorTransform.localPosition = pos;
    }

    public void AddPress()
    {
        if (!isServer || isOpen) return;
        pressCount++;
        if (pressCount >= 2) Open();
    }

    public void RemovePress()
    {
        if (!isServer || isOpen) return;
        pressCount--;
        if (pressCount < 0) pressCount = 0;
    }

    private void Open()
    {
        isOpen = true;
        targetPos.y = openHeight;
    }
}