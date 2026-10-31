
using UnityEngine;

public class TankTrackTextureAnimation : MonoBehaviour
{
    [Header("Track Object")]
    public Renderer trackRenderer;

    [Header("Tank Movement")]
    public TankMovement tankMovement;

    [Header("Animation Speed")]
    public float moveTextureSpeed = 0.3f;

    [Header("Texture Direction")]
    public Vector2 textureDirection = Vector2.left;

    private Material trackMaterial;
    private Vector2 textureOffset;

    void Start()
    {
        if (trackRenderer == null)
            trackRenderer = GetComponent<Renderer>();

        if (tankMovement == null)
            tankMovement = GetComponentInParent<TankMovement>();

        if (trackRenderer == null)
        {
            Debug.LogError(
                "Track animation: Assign the track Renderer!"
            );
            enabled = false;
            return;
        }

        if (tankMovement == null)
        {
            Debug.LogError(
                "Track animation: Assign the TankMovement script!"
            );
            enabled = false;
            return;
        }

        trackMaterial = trackRenderer.material;
        textureOffset = trackMaterial.mainTextureOffset;
    }

    void Update()
    {
        // Use actual tank speed, including deceleration after releasing W/S.
        float actualSpeed = tankMovement.currentSpeed;

        AnimateTrack(actualSpeed);
    }

    void AnimateTrack(float speed)
    {
        if (trackMaterial == null ||
            !trackMaterial.HasProperty("_MainTex"))
            return;

        textureOffset += textureDirection
            * speed
            * moveTextureSpeed
            * Time.deltaTime;

        textureOffset.x = Mathf.Repeat(textureOffset.x, 1f);
        textureOffset.y = Mathf.Repeat(textureOffset.y, 1f);

        trackMaterial.mainTextureOffset = textureOffset;
    }

    void OnDestroy()
    {
        if (trackMaterial != null)
            Destroy(trackMaterial);
    }
}
