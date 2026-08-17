using Mirror;
using System.Collections;
using UnityEngine;

public class DrinkPlaceablePoint : MonoBehaviour, IInteractable
{
    [SerializeField] private DrinkMachineMiniGame drinkMachine;
    [SerializeField] private float wrongItemDestroyDelay = 3f;
    [SerializeField]private Color correctColor;

    public void SetCorrectColor(Color color)
    {
        correctColor = color;
    }

    public void Interact(GameObject gm)
    {
        if (!NetworkServer.active) return;

        Player player = gm.GetComponent<Player>();
        IPickupable item = player?.GetSelectedIPickable();

        if (item == null || !(item is CupItem)) return;

        CupItem cup = item as CupItem;
        GameObject cupObj = player.DropItem();

        if (cupObj != null)
        {
            cupObj.transform.position = transform.position + Vector3.up;
            DisableCollider(cupObj);

            Debug.Log($"Cup color: {cup.GetColor()}, Correct color: {correctColor}");
            Debug.Log($"Colors match: {ColorsMatch(cup.GetColor(), correctColor)}");

            if (drinkMachine != null && ColorsMatch(cup.GetColor(), correctColor))
            {
                Debug.Log("Победа! Цвета совпадают");
                drinkMachine.CompleteGame(true);
            }
            else
            {
                Debug.Log("Цвета не совпадают. Удаление через " + wrongItemDestroyDelay + " секунд");
                StartCoroutine(DestroyAfterDelay(cupObj, wrongItemDestroyDelay));
            }
        }
    }

    private void DisableCollider(GameObject obj)
    {
        var colliders = obj.GetComponentsInChildren<Collider>();
        foreach (var collider in colliders)
        {
            collider.enabled = false;
        }
    }
    private bool ColorsMatch(Color a, Color b)
    {
        float threshold = 0.1f;
        return Mathf.Abs(a.r - b.r) < threshold &&
               Mathf.Abs(a.g - b.g) < threshold &&
               Mathf.Abs(a.b - b.b) < threshold;
    }
    private IEnumerator DestroyAfterDelay(GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (obj != null)
        {
            NetworkServer.Destroy(obj);
        }
    }
}