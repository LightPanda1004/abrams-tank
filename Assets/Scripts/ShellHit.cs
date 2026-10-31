
using UnityEngine;

public class ShellHit : MonoBehaviour
{
    [Header("Impact Effect")]
    public GameObject hitExplosionPrefab;
    public float explosionLifetime = 5f;
    public float surfaceOffset = 0.02f;

    private bool hasExploded;

    void OnCollisionEnter(Collision collision)
    {
        if (hasExploded)
            return;

        hasExploded = true;

        if (collision.contactCount > 0 && hitExplosionPrefab != null)
        {
            ContactPoint contact = collision.GetContact(0);

            Vector3 hitPosition =
                contact.point + contact.normal * surfaceOffset;

            // Preserve the prefab's original rotation.
            GameObject explosion = Instantiate(
                hitExplosionPrefab,
                hitPosition,
                hitExplosionPrefab.transform.rotation
            );

            Destroy(explosion, explosionLifetime);
        }

        Destroy(gameObject);
    }
}
