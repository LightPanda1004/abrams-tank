
using UnityEngine;

public class TankExhaustSmoke : MonoBehaviour
{
    [Header("Smoke Prefab")]
    public GameObject effectSource;

    [Header("Movement Detection")]
    public float movementThreshold = 0.05f;
    public float turnThreshold = 2f;

    [Header("Smoke Start Delay")]
    public float startDelay = 0.5f;

    private Transform tank;
    private GameObject smokeInstance;
    private ParticleSystem[] particles;

    private Vector3 lastPosition;
    private float lastRotationY;

    private float movementTimer;
    private bool smokePlaying;

    void Awake()
    {
        tank = transform.parent;

        if (tank == null)
        {
            Debug.LogError(
                "TankExhaustSmoke: TankSmoke must be a child of Tank.",
                this
            );
            enabled = false;
            return;
        }

        if (effectSource == null)
        {
            Debug.LogError(
                "TankExhaustSmoke: Assign Effect Source in the Inspector.",
                this
            );
            enabled = false;
            return;
        }

        CreateSmoke();
    }

    void Start()
    {
        lastPosition = tank.position;
        lastRotationY = tank.eulerAngles.y;
    }

    void CreateSmoke()
    {
        smokeInstance = Instantiate(
            effectSource,
            transform.position,
            transform.rotation
        );

        particles = smokeInstance.GetComponentsInChildren
            <ParticleSystem>(true);

        foreach (ParticleSystem ps in particles)
        {
            var main = ps.main;
            main.playOnAwake = false;

            ps.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );
        }

        smokeInstance.SetActive(false);
        smokePlaying = false;
    }

    void LateUpdate()
    {
        if (tank == null)
            return;

        float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);

        // Measure actual movement speed.
        float speed =
            (tank.position - lastPosition).magnitude / deltaTime;

        // Measure actual hull rotation speed.
        float turnSpeed =
            Mathf.Abs(Mathf.DeltaAngle(
                lastRotationY,
                tank.eulerAngles.y
            )) / deltaTime;

        lastPosition = tank.position;
        lastRotationY = tank.eulerAngles.y;

        bool moving =
            speed > movementThreshold ||
            turnSpeed > turnThreshold;

        // Wait until movement has lasted long enough.
        if (moving)
        {
            movementTimer += Time.deltaTime;
        }
        else
        {
            movementTimer = 0f;
        }

        bool shouldSmoke = movementTimer >= startDelay;

        // Recover if the smoke prefab destroys its own instance.
        if (smokeInstance == null)
        {
            if (shouldSmoke)
                CreateSmoke();
            else
                return;
        }

        smokeInstance.transform.SetPositionAndRotation(
            transform.position,
            transform.rotation
        );

        if (shouldSmoke)
        {
            if (!smokeInstance.activeSelf)
                smokeInstance.SetActive(true);

            bool anyAlive = false;

            foreach (ParticleSystem ps in particles)
            {
                if (ps != null && ps.IsAlive(true))
                {
                    anyAlive = true;
                    break;
                }
            }

            // Restart the effect if its particles have finished.
            if (!anyAlive)
            {
                foreach (ParticleSystem ps in particles)
                {
                    if (ps != null)
                    {
                        ps.Stop(
                            true,
                            ParticleSystemStopBehavior.StopEmittingAndClear
                        );

                        ps.Play(true);
                    }
                }
            }

            smokePlaying = true;
        }
        else if (smokePlaying)
        {
            // Stop emitting and let existing particles fade away.
            foreach (ParticleSystem ps in particles)
            {
                if (ps != null)
                {
                    ps.Stop(
                        true,
                        ParticleSystemStopBehavior.StopEmitting
                    );
                }
            }

            bool anyAlive = false;

            foreach (ParticleSystem ps in particles)
            {
                if (ps != null && ps.IsAlive(true))
                {
                    anyAlive = true;
                    break;
                }
            }

            if (!anyAlive)
            {
                smokeInstance.SetActive(false);
                smokePlaying = false;
            }
        }
    }

    void OnDestroy()
    {
        if (smokeInstance != null)
            Destroy(smokeInstance);
    }
}
