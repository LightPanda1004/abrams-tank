
using UnityEngine;

public class DroneCamera : MonoBehaviour
{
    [Header("Camera Settings")]
    public Camera mainCamera;
    public Camera droneCamera;

    private bool isDroneView = false;

    private void Start()
    {
        if (mainCamera == null || droneCamera == null)
        {
            Debug.LogError(
                "DroneCamera: Assign Main Camera and Drone Camera in the Inspector!"
            );
            enabled = false;
            return;
        }

        // Start with the normal camera.
        mainCamera.enabled = true;
        droneCamera.enabled = false;

        isDroneView = false;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.C))
        {
            SwitchCamera();
        }
    }

    private void SwitchCamera()
    {
        isDroneView = !isDroneView;

        mainCamera.enabled = !isDroneView;
        droneCamera.enabled = isDroneView;

        Debug.Log(
            isDroneView
                ? "Drone camera activated."
                : "Main camera activated."
        );
    }
}
