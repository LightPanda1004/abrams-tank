
using UnityEngine;

public class CircularReloadUI : MonoBehaviour
{
    [Header("Position and Size")]
    public Vector2 screenPosition = new Vector2(100f, 150f);
    public float size = 80f;
    public float thickness = 8f;

    [Header("Colors")]
    public Color backgroundColor =
        new Color(0.2f, 0.2f, 0.2f, 0.8f);

    public Color reloadColor = Color.green;
    public Color textColor = Color.white;

    [Header("Countdown")]
    public bool showCountdown = true;
    public int fontSize = 18;

    private Texture2D backgroundTexture;
    private Texture2D progressTexture;

    private float duration = 3f;
    private float remainingTime = 0f;
    private float progress = 1f;

    private bool isVisible = false;
    private GUIStyle countdownStyle;

    void Awake()
    {
        CreateTextures();

        // GUI.skin can only be accessed inside OnGUI().
        countdownStyle = new GUIStyle
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = fontSize,
            fontStyle = FontStyle.Bold
        };

        countdownStyle.normal.textColor = textColor;
    }

    void Update()
    {
        if (remainingTime <= 0f)
            return;

        remainingTime = Mathf.Max(
            0f,
            remainingTime - Time.deltaTime
        );

        progress = 1f -
            Mathf.Clamp01(remainingTime / duration);

        UpdateProgressTexture();

        if (remainingTime <= 0f)
            isVisible = false;
    }

    public void StartReload(float reloadDuration)
    {
        duration = Mathf.Max(0.1f, reloadDuration);
        remainingTime = duration;
        progress = 0f;
        isVisible = true;

        UpdateProgressTexture();
    }

    public void UpdateReload(float remaining, float reloadDuration)
    {
        duration = Mathf.Max(0.1f, reloadDuration);
        remainingTime = Mathf.Clamp(remaining, 0f, duration);

        progress = 1f -
            Mathf.Clamp01(remainingTime / duration);

        isVisible = remainingTime > 0f;

        UpdateProgressTexture();
    }

    public void SetVisible(bool visible)
    {
        isVisible = visible;

        // Hiding the UI must not stop the reload timer.
        if (!visible && remainingTime > 0f)
            isVisible = false;
    }

    void OnGUI()
    {
        if (!isVisible)
            return;

        if (backgroundTexture == null ||
            progressTexture == null)
            return;

        // GUI style settings are refreshed safely here.
        if (countdownStyle == null)
            countdownStyle = new GUIStyle();

        countdownStyle.fontSize = fontSize;
        countdownStyle.normal.textColor = textColor;
        float drawX = (Screen.width -100);
        float drawY = (Screen.height - 140);

        Rect rect = new Rect(drawX, drawY, size, size);

        GUI.DrawTexture(rect, backgroundTexture);
        GUI.DrawTexture(rect, progressTexture);

        if (showCountdown && remainingTime > 0f)
        {
            GUI.Label(
                rect,
                remainingTime.ToString("0.0") + "s",
                countdownStyle
            );
        }
    }

    void CreateTextures()
    {
        const int textureSize = 128;

        backgroundTexture = CreateRingTexture(
            textureSize,
            1f,
            backgroundColor
        );

        progressTexture = CreateRingTexture(
            textureSize,
            0f,
            reloadColor
        );
    }

    Texture2D CreateRingTexture(
        int textureSize,
        float fillAmount,
        Color ringColor)
    {
        Texture2D texture = new Texture2D(
            textureSize,
            textureSize,
            TextureFormat.RGBA32,
            false
        );

        texture.name = "Generated Circular Reload Ring";
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        UpdateRingPixels(texture, fillAmount, ringColor);

        return texture;
    }

    void UpdateProgressTexture()
    {
        if (progressTexture == null)
            return;

        UpdateRingPixels(
            progressTexture,
            progress,
            reloadColor
        );
    }

    void UpdateRingPixels(
        Texture2D texture,
        float fillAmount,
        Color ringColor)
    {
        int width = texture.width;
        int height = texture.height;

        float centerX = (width - 1) / 2f;
        float centerY = (height - 1) / 2f;

        float outerRadius = Mathf.Min(width, height) * 0.46f;

        float innerRadius = outerRadius *
            (1f - Mathf.Clamp01(thickness / size));

        float fillAngle = Mathf.Clamp01(fillAmount) * 360f;

        Color[] pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float dx = x - centerX;
                float dy = y - centerY;

                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                Color pixel = Color.clear;

                if (distance <= outerRadius &&
                    distance >= innerRadius)
                {
                    float angle = Mathf.Atan2(dy, dx) *
                        Mathf.Rad2Deg;

                    // Begin at the top and fill clockwise.
                    float clockwiseAngle =
                        (90f - angle + 360f) % 360f;

                    if (fillAmount >= 0.999f ||
                        clockwiseAngle <= fillAngle)
                    {
                        pixel = ringColor;
                    }
                }

                pixels[y * width + x] = pixel;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, false);
    }

    void OnDestroy()
    {
        if (backgroundTexture != null)
            Destroy(backgroundTexture);

        if (progressTexture != null)
            Destroy(progressTexture);
    }
}
