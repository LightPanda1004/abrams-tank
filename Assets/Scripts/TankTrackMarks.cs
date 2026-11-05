
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TankTrackMarks : MonoBehaviour
{
    [Header("References")]
    public Transform tankRoot;
    public Material trackMaterial;

    [Header("Track Placement")]
    public float trackSeparation = 2.2f;
    public float markLength = 0.35f;
    public float markWidth = 0.65f;
    public float markSpacing = 0.12f;

    [Header("Ground Detection")]
    public LayerMask groundLayers = ~0;
    public float raycastHeight = 3f;
    public float raycastDistance = 10f;
    public float groundOffset = 0.025f;

    [Header("Mark Lifetime")]
    [Tooltip("Time before each mark starts fading, in minutes.")]
    public float lifetimeMinutes = 2f;

    [Tooltip("Duration of the fade-out, in seconds.")]
    public float fadeDuration = 5f;

    [Range(0f, 1f)]
    [Tooltip("Initial opacity of the marks.")]
    public float initialOpacity = 0.35f;

    private Vector3 lastLeftPosition;
    private Vector3 lastRightPosition;
    private bool initialized;

    private MaterialPropertyBlock propertyBlock;
    private readonly int baseColorId =
        Shader.PropertyToID("_Color");
    private readonly int baseMapColorId =
        Shader.PropertyToID("_BaseColor");
    private RaycastHit[] raycastBuffer;
    private readonly List<GameObject> activeMarks = new List<GameObject>();

    void Start()
    {
        if (tankRoot == null)
            tankRoot = transform;

        if (trackMaterial == null)
        {
            Debug.LogError(
                "TankTrackMarks: Assign Track Material!", this);
            enabled = false;
            return;
        }

        propertyBlock = new MaterialPropertyBlock();
        raycastBuffer = new RaycastHit[8];

        lastLeftPosition = GetTrackPosition(-1f);
        lastRightPosition = GetTrackPosition(1f);
        initialized = true;
    }

    void OnDisable()
    {
        CleanupMarks();
    }

    void OnDestroy()
    {
        CleanupMarks();
    }

    private void CleanupMarks()
    {
        for (int i = activeMarks.Count - 1; i >= 0; i--)
        {
            GameObject mark = activeMarks[i];
            if (mark != null)
                Destroy(mark);
            activeMarks.RemoveAt(i);
        }
    }

    void Update()
    {
        if (!initialized || tankRoot == null)
            return;

        Vector3 leftPosition = GetTrackPosition(-1f);
        Vector3 rightPosition = GetTrackPosition(1f);

        float spacing = Mathf.Max(0.05f, markSpacing);
        float spacingSquared = spacing * spacing;

        if ((leftPosition - lastLeftPosition).sqrMagnitude
            >= spacingSquared)
        {
            if (PlaceMark(leftPosition))
                lastLeftPosition = leftPosition;
        }

        if ((rightPosition - lastRightPosition).sqrMagnitude
            >= spacingSquared)
        {
            if (PlaceMark(rightPosition))
                lastRightPosition = rightPosition;
        }
    }

    Vector3 GetTrackPosition(float side)
    {
        Vector3 offset = new Vector3(
            side * trackSeparation * 0.5f, 0f, 0f);

        return tankRoot.TransformPoint(offset);
    }

    bool PlaceMark(Vector3 trackPosition)
    {
        Vector3 rayOrigin =
            trackPosition + Vector3.up * raycastHeight;

        int hitCount = Physics.RaycastNonAlloc(
            rayOrigin,
            Vector3.down,
            raycastBuffer,
            raycastDistance,
            groundLayers,
            QueryTriggerInteraction.Ignore);

        if (hitCount == 0)
            return false;

        RaycastHit groundHit = default;
        bool foundGround = false;
        float closestDistance = float.PositiveInfinity;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = raycastBuffer[i];

            if (hit.transform == tankRoot ||
                hit.transform.IsChildOf(tankRoot))
                continue;

            if (hit.distance < closestDistance)
            {
                groundHit = hit;
                closestDistance = hit.distance;
                foundGround = true;
            }
        }

        if (!foundGround)
            return false;

        Vector3 forward = Vector3.ProjectOnPlane(
            tankRoot.forward,
            groundHit.normal).normalized;

        if (forward.sqrMagnitude < 0.001f)
            return false;

        Quaternion rotation = Quaternion.LookRotation(
            groundHit.normal, forward);

        GameObject mark = GameObject.CreatePrimitive(PrimitiveType.Quad);
        activeMarks.Add(mark);

        mark.name = "Tank Track Mark";

        Collider markCollider = mark.GetComponent<Collider>();
        if (markCollider != null)
            Destroy(markCollider);

        mark.transform.position =
            groundHit.point + groundHit.normal * groundOffset;

        mark.transform.rotation = rotation;

        mark.transform.localScale = new Vector3(
            markWidth, -markLength, -1f);

        Renderer markRenderer = mark.GetComponent<Renderer>();

        // Per-mark opacity is supplied by the property block below.
        markRenderer.sharedMaterial = trackMaterial;

        Color startColor = Color.black;
        startColor.a = initialOpacity;

        SetMarkColor(markRenderer, startColor);

        markRenderer.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;
        markRenderer.receiveShadows = false;

        StartCoroutine(FadeAndRemoveMark(
            mark, markRenderer, startColor));

        return true;
    }

    void SetMarkColor(Renderer markRenderer, Color color)
    {
        propertyBlock.Clear();
        propertyBlock.SetColor(baseColorId, color);
        propertyBlock.SetColor(baseMapColorId, color);
        markRenderer.SetPropertyBlock(propertyBlock);
    }

    IEnumerator FadeAndRemoveMark(
        GameObject mark,
        Renderer markRenderer,
        Color startColor)
    {
        // Keep the mark fully visible for the configured lifetime.
        float lifetimeSeconds =
            Mathf.Max(0f, lifetimeMinutes) * 60f;

        yield return new WaitForSeconds(lifetimeSeconds);

        // Gradually fade the mark to transparent.
        float duration = Mathf.Max(0.01f, fadeDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (mark == null)
                yield break;

            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / duration);
            Color fadedColor = startColor;
            fadedColor.a = Mathf.Lerp(
                startColor.a, 0f, t);

            if (markRenderer != null)
                SetMarkColor(markRenderer, fadedColor);

            yield return null;
        }

        if (mark != null)
        {
            activeMarks.Remove(mark);
            Destroy(mark);
        }
    }
}
