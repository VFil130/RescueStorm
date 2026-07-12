using Mirror;
using UnityEngine;

public class EvacuateZone : NetworkBehaviour, IInteractable
{
    public void Interact(GameObject interactor)
    {
        if (!isServer) return;

        Player player = interactor.GetComponent<Player>();
        if (player == null) return;

        GameObject item = player.GetSelectedItem();
        if (item == null) return;

        Survivor survivor = item.GetComponent<Survivor>();
        if (survivor == null) return;

        survivor.Save();
        player.RemoveSelectedItem();
    }
}