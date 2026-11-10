
using UnityEngine;

public class TankScopeZoom : MonoBehaviour
{
    [Header("Cameras")]
    public Camera tankCamera;
    public Camera CannonCamera;

    [Header("Scope UI")]
    public GameObject scopeImage;

    [Header("Zoom Levels")]
    public float[] zoomLevels = { 30f, 15f, 8f, 5f };

    [Header("Scroll Zoom")]
    public float minFOV = 5f;
    public float maxFOV = 40f;
    public float scrollZoomSpeed = 3f;

    [Header("Hit Shake")]
    public float hitShakeDuration = 0.9f;
    public Vector3 hitShakeRotation = new Vector3(30f, 40f, 28f);
    public float hitShakePosition = 0.18f;
    public float hitShakeFrequency = 35f;

    private bool isAiming = false;
    private bool droneScopeMode;
    private int zoomIndex = 0;
    private bool ownsScopeImage;
    private bool hasAimCameraPose;
    private float hitShakeRemaining;
    private Vector3 cannonCameraAimPosition;
    private Quaternion cannonCameraAimRotation;

    public bool IsAiming => isAiming && !droneScopeMode;

    private void Start()
    {
        if (scopeImage != null && !scopeImage.scene.IsValid())
        {
            scopeImage = Instantiate(scopeImage);
            ownsScopeImage = true;
        }

        if (tankCamera == null)
        {
            TankCameraFollow cameraFollow =
                FindObjectOfType<TankCameraFollow>();

            tankCamera = cameraFollow != null
                ? cameraFollow.GetComponent<Camera>()
                : Camera.main;
        }

        if (tankCamera == null || CannonCamera == null)
        {
            Debug.LogError(
                "TankScopeZoom: Could not find the player camera or cannon camera."
            );
            enabled = false;
            return;
        }

        if (zoomLevels == null || zoomLevels.Length == 0)
        {
            Debug.LogError(
                "TankScopeZoom: Add at least one zoom level!"
            );
            enabled = false;
            return;
        }

        tankCamera.enabled = true;
        CannonCamera.enabled = false;

        if (scopeImage != null)
            scopeImage.SetActive(false);

        zoomIndex = 0;
        CannonCamera.fieldOfView = Mathf.Clamp(
            zoomLevels[zoomIndex],
            minFOV,
            maxFOV
        );
    }

    private void OnDestroy()
    {
        if (ownsScopeImage && scopeImage != null)
            Destroy(scopeImage);
    }

    private void Update()
    {
        if (droneScopeMode)
        {
            UpdateDroneScopeVisibility();
            return;
        }

        if (GameDirector.Instance != null &&
            (GameDirector.Instance.IsPaused ||
             GameDirector.Instance.IsGameOver))
        {
            ExitAiming();
            return;
        }

        bool keyboardMode =
            GameDirector.Instance != null &&
            !GameDirector.Instance.MouseTurretControlEnabled;

        // RMB in mouse mode; Shift in keyboard mode.
        bool toggleAim = false;

        if (keyboardMode)
        {
            toggleAim =
                Input.GetKeyDown(KeyCode.LeftShift) ||
                Input.GetKeyDown(KeyCode.RightShift);
        }
        else
        {
            toggleAim = Input.GetMouseButtonDown(1);
        }

        if (toggleAim)
        {
            if (!isAiming)
            {
                EnterAiming();
            }
            else if (zoomIndex < zoomLevels.Length - 1)
            {
                zoomIndex++;

                CannonCamera.fieldOfView = Mathf.Clamp(
                    zoomLevels[zoomIndex],
                    minFOV,
                    maxFOV
                );
            }
            else
            {
                ExitAiming();
            }
        }

        // Mouse wheel adjusts zoom while aiming.
        if (isAiming)
        {
            float scroll = Input.mouseScrollDelta.y;

            if (Mathf.Abs(scroll) > 0.01f)
            {
                CannonCamera.fieldOfView -=
                    scroll * scrollZoomSpeed;

                CannonCamera.fieldOfView = Mathf.Clamp(
                    CannonCamera.fieldOfView,
                    minFOV,
                    maxFOV
                );
            }
        }
    }

    private void LateUpdate()
    {
        UpdateHitShake();
    }

    public void SetDroneScopeMode(bool active)
    {
        droneScopeMode = active;
        isAiming = false;
        zoomIndex = 0;
        if (active)
        {
            hitShakeRemaining = 0f;
            RestoreAimCameraPose();
        }

        if (CannonCamera != null)
            CannonCamera.enabled = false;

        if (active)
            UpdateDroneScopeVisibility();
        else if (scopeImage != null)
            scopeImage.SetActive(false);
    }

    private void UpdateDroneScopeVisibility()
    {
        if (scopeImage != null)
            scopeImage.SetActive(false);
    }

    private void EnterAiming()
    {
        isAiming = true;
        zoomIndex = 0;
        cannonCameraAimPosition = CannonCamera.transform.localPosition;
        cannonCameraAimRotation = CannonCamera.transform.localRotation;
        hasAimCameraPose = true;
        hitShakeRemaining = 0f;

        tankCamera.enabled = false;
        CannonCamera.enabled = true;

        CannonCamera.fieldOfView = Mathf.Clamp(
            zoomLevels[zoomIndex],
            minFOV,
            maxFOV
        );

        if (scopeImage != null)
            scopeImage.SetActive(true);
    }

    public void NotifyTankHit()
    {
        if (isAiming && CannonCamera != null &&
            hitShakeDuration > 0f)
        {
            hitShakeRemaining = hitShakeDuration;
        }
    }

    private void UpdateHitShake()
    {
        if (!isAiming || CannonCamera == null)
            return;

        if (hitShakeRemaining <= 0f)
        {
            CannonCamera.transform.localPosition = cannonCameraAimPosition;
            CannonCamera.transform.localRotation = cannonCameraAimRotation;
            return;
        }

        hitShakeRemaining = Mathf.Max(
            0f,
            hitShakeRemaining - Time.deltaTime
        );
        float intensity = hitShakeRemaining /
                          Mathf.Max(0.01f, hitShakeDuration);
        float noiseTime = Time.time * hitShakeFrequency;
        Vector3 rotationNoise = new Vector3(
            GetShakeNoise(noiseTime, 0f),
            GetShakeNoise(noiseTime, 17f),
            GetShakeNoise(noiseTime, 37f)
        );
        Vector3 positionNoise = new Vector3(
            GetShakeNoise(noiseTime, 53f),
            GetShakeNoise(noiseTime, 71f),
            0f
        );

        CannonCamera.transform.localPosition =
            cannonCameraAimPosition +
            positionNoise * (hitShakePosition * intensity);
        CannonCamera.transform.localRotation =
            cannonCameraAimRotation *
            Quaternion.Euler(Vector3.Scale(
                rotationNoise,
                hitShakeRotation * intensity
            ));
    }

    private static float GetShakeNoise(float time, float offset)
    {
        return (Mathf.PerlinNoise(time, offset) - 0.5f) * 2f;
    }

    public void ExitAiming()
    {
        isAiming = false;
        zoomIndex = 0;
        hitShakeRemaining = 0f;

        if (tankCamera != null)
            tankCamera.enabled = true;

        if (CannonCamera != null)
        {
            RestoreAimCameraPose();
            CannonCamera.enabled = false;
        }

        if (scopeImage != null)
            scopeImage.SetActive(false);
    }

    private void RestoreAimCameraPose()
    {
        if (!hasAimCameraPose || CannonCamera == null)
            return;

        CannonCamera.transform.localPosition = cannonCameraAimPosition;
        CannonCamera.transform.localRotation = cannonCameraAimRotation;
        hasAimCameraPose = false;
    }
}
