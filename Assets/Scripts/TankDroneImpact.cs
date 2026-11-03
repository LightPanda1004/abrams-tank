using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;

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
            NetworkManager networkManager = NetworkManager.Singleton;
            NetworkTankPlayer attackingPlayer =
                droneController.GetComponent<NetworkTankPlayer>();

            if (networkManager != null && networkManager.IsListening)
            {
                if (attackingPlayer != null)
                    attackingPlayer.RequestDroneImpactDamage(hitTank);
            }
            else
            {
                hitTank.TakeDamage(100f);
            }
        }

        droneController.HandleDroneImpact(impactPoint, true);
    }

    public void BeginDestruction(Vector3 impactPoint, bool sink)
    {
        if (isDestroyed)
            return;

        isDestroyed = true;
        float droneLifetime = Mathf.Max(0f, destroyDelay);
        destroyAt = Time.time + droneLifetime;

        if (explosionPrefab != null)
        {
            GameObject explosion = Instantiate(
                explosionPrefab,
                impactPoint,
                Quaternion.identity
            );
            Destroy(explosion, ExplosionEffectDuration);
        }

        if (firePrefab != null)
        {
            GameObject fire = Instantiate(
                firePrefab,
                transform.position,
                Quaternion.identity,
                transform
            );
            fire.transform.localPosition = Vector3.zero;
            KeepParticlesPlaying(fire);
        }

        ApplyExplosionForce(impactPoint);

        if (sink && droneBody != null)
        {
            droneBody.isKinematic = false;
            droneBody.useGravity = true;
            droneBody.velocity = Vector3.down * sinkSpeed;
            droneBody.angularVelocity = Vector3.zero;
        }
        else if (droneBody != null)
        {
            if (!droneBody.isKinematic)
            {
                droneBody.velocity = Vector3.zero;
                droneBody.angularVelocity = Vector3.zero;
            }

            droneBody.useGravity = false;
            droneBody.isKinematic = true;
        }
    }

    public void DestroyFromTankShell(Vector3 impactPoint)
    {
        if (droneController != null)
            droneController.ApplyTankShellImpact(impactPoint);
        else
            BeginDestruction(impactPoint, true);
    }

    private void Update()
    {
        if (isDestroyed && Time.time >= destroyAt)
            Destroy(gameObject);
    }

    private static void KeepParticlesPlaying(GameObject effect)
    {
        ParticleSystem[] particleSystems =
            effect.GetComponentsInChildren<ParticleSystem>(true);

        foreach (ParticleSystem particleSystem in particleSystems)
        {
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particleSystem.main;
            main.loop = true;
            particleSystem.Play(false);
        }
    }

    private void ApplyExplosionForce(Vector3 impactPoint)
    {
        if (explosionRadius <= 0f || explosionForce <= 0f)
            return;

        Collider[] colliders = Physics.OverlapSphere(
            impactPoint,
            explosionRadius,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore
        );
        HashSet<Rigidbody> affectedBodies = new HashSet<Rigidbody>();

        foreach (Collider hitCollider in colliders)
        {
            Rigidbody body = hitCollider.attachedRigidbody;
            if (body == null || body == droneBody ||
                body.transform == transform ||
                body.transform.IsChildOf(transform) ||
                IsPartOfTank(body.transform) ||
                !affectedBodies.Add(body))
            {
                continue;
            }

            body.AddExplosionForce(
                explosionForce,
                impactPoint,
                explosionRadius,
                upwardsModifier,
                ForceMode.Impulse
            );
        }
    }

    private bool IsPartOfTank(Transform candidate)
    {
        return droneController != null &&
               (candidate == droneController.transform ||
                candidate.IsChildOf(droneController.transform));
    }
}
