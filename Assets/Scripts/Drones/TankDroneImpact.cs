using UnityEngine;
using System.Collections.Generic;
using Mirror;

[RequireComponent(typeof(Rigidbody))]
public class TankDroneImpact : MonoBehaviour
{
    public GameObject explosionPrefab;
    public GameObject firePrefab;
    public float explosionEffectDuration = 5f;
    public float explosionRadius = 6f;
    public float explosionForce = 500f;
    public float upwardsModifier = 0.5f;
    public float sinkSpeed = 12f;
    public float destroyDelay = 8f;

    private TankDronecontroll droneController;
    private Rigidbody droneBody;
    private bool isDestroyed;
    private float destroyAt;

    public float ExplosionEffectDuration =>
        Mathf.Max(0f, explosionEffectDuration);
    public bool IsDeployed =>
        droneController != null && droneController.IsDeployed;
    public NetworkTankPlayer NetworkPlayer =>
        droneController != null
            ? droneController.GetComponent<NetworkTankPlayer>()
            : null;

    private void Awake()
    {
        droneBody = GetComponent<Rigidbody>();
    }

    public void Initialize(TankDronecontroll controller, Rigidbody body)
    {
        droneController = controller;
        droneBody = body;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isDestroyed || droneController == null ||
            !droneController.CanHandleDroneImpact)
        {
            return;
        }

        Vector3 impactPoint = collision.contactCount > 0
            ? collision.GetContact(0).point
            : transform.position;

        TankHealth hitTank =
            collision.collider.GetComponentInParent<TankHealth>();
        if (hitTank != null)
        {
            NetworkTankPlayer attackingPlayer =
                droneController.GetComponent<NetworkTankPlayer>();

            if (NetworkClient.isConnected)
            {
                if (attackingPlayer != null)
                    attackingPlayer.RequestDroneImpactDamage(hitTank);
            }
            else
            {
                hitTank.TakeDamage(100f, droneController.transform);
            }
        }

        isDestroyed = true;
        Destroy(gameObject, destroyDelay);
    }
}
