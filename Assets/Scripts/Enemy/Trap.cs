using Mirror;
using UnityEngine;

public class Trap : NetworkBehaviour
{
    [Header("Settings")]
    [SerializeField] private int damage = 1;
    [SerializeField] private float slowMultiplier = 0.4f;
    [SerializeField] private float slowDuration = 3f;
    [SerializeField] private float armDelay = 0.5f;

    private float armTimer;
    private bool armed;

    private void Update()
    {
        if (!isServer) return;

        if (!armed)
        {
            armTimer += Time.deltaTime;
            if (armTimer >= armDelay)
                armed = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isServer || !armed) return;

        Player player = other.GetComponent<Player>();
        if (player == null || player.Health <= 0) return;

        player.TakeDamage(damage);
        player.ApplySlow(slowMultiplier, slowDuration);
        RpcPlayEffect();
        NetworkServer.Destroy(gameObject);
    }

    [ClientRpc]
    private void RpcPlayEffect()
    {
        // Placeholder for trap VFX/SFX
    }
}
