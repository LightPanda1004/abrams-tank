
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

    private bool isAiming = false;
    private int zoomIndex = 0;

    private void Start()
    {
        if (tankCamera == null || CannonCamera == null)
        {
            Debug.LogError(
                "TankScopeZoom: Assign both cameras in the Inspector!"
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

    private void Update()
    {
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

    private void EnterAiming()
    {
        isAiming = true;
        zoomIndex = 0;

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

    private void ExitAiming()
    {
        isAiming = false;
        zoomIndex = 0;

        if (tankCamera != null)
            tankCamera.enabled = true;

        if (CannonCamera != null)
            CannonCamera.enabled = false;

        if (scopeImage != null)
            scopeImage.SetActive(false);
    }
}
