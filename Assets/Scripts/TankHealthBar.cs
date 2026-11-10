
using UnityEngine;
using UnityEngine.UI;

public class TankHealthBar : MonoBehaviour
{
    [Header("Health Bar Settings")]
    public Slider healthSlider;
    public Vector3 offset = new Vector3(0f, 3f, 0f);

    private Transform target;
    private Camera mainCamera;

    public void Setup(Transform tank, float maxHealth)
    {
        target = tank;
        mainCamera = Camera.main;

        if (healthSlider != null)
        {
            healthSlider.minValue = 0f;
            healthSlider.maxValue = maxHealth;
            healthSlider.value = maxHealth;
        }
        else
        {
            Debug.LogWarning(
                "Health Slider is not assigned to " + gameObject.name
            );
        }
    }

    public void UpdateHealth(float currentHealth)
    {
        if (healthSlider != null)
        {
            healthSlider.value = Mathf.Clamp(
                currentHealth,
                healthSlider.minValue,
                healthSlider.maxValue
            );
        }
    }

    void LateUpdate()
    {
        if (target == null)
            return;

        // Keep the health bar above the tank.
        transform.position = target.position + offset;

        // Make the health bar face the camera.
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera != null)
        {
            transform.rotation = mainCamera.transform.rotation;
        }
    }
}
