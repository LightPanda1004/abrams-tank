
using UnityEngine;

public class AIDroneSpawner : MonoBehaviour
{
    [Header("Drone Settings")]
    public GameObject dronePrefab;
    public int dronesPerWave = 2;
    public float spawnInterval = 120f;

    [Header("Random Spawn Area")]
    public Transform playerTank;
    public float minSpawnDistance = 25f;
    public float maxSpawnDistance = 60f;
    public float spawnHeight = 12f;

    [Header("Spawn Safety")]
    public LayerMask obstacleMask;
    public float checkRadius = 2f;
    public int maxAttemptsPerDrone = 20;

    private float spawnTimer;

    private void Start()
    {
        spawnTimer = 0f;
    }

    private void Update()
    {
        if (dronePrefab == null || playerTank == null)
            return;

        GameDirector gameDirector = GameDirector.Instance;
        if (gameDirector != null &&
            (gameDirector.IsPaused || gameDirector.IsMultiplayerMode))
        {
            return;
        }

        spawnTimer -= Time.deltaTime;

        if (spawnTimer <= 0f)
        {
            SpawnWave();
            spawnTimer = spawnInterval;
        }
    }

    private void SpawnWave()
    {
        for (int i = 0; i < dronesPerWave; i++)
        {
            SpawnOneDrone();
        }

        Debug.Log("A new wave of AI drones has spawned!");
    }

    private void SpawnOneDrone()
    {
        for (int attempt = 0; attempt < maxAttemptsPerDrone; attempt++)
        {
            Vector2 randomDirection = Random.insideUnitCircle.normalized;

            float distance = Random.Range(
                minSpawnDistance,
                maxSpawnDistance
            );

            Vector3 spawnPosition = playerTank.position +
                new Vector3(
                    randomDirection.x * distance,
                    spawnHeight,
                    randomDirection.y * distance
                );

            // Do not spawn inside buildings or other obstacles.
            if (Physics.CheckSphere(
                spawnPosition,
                checkRadius,
                obstacleMask,
                QueryTriggerInteraction.Ignore))
            {
                continue;
            }

            GameObject drone = Instantiate(
                dronePrefab,
                spawnPosition,
                Quaternion.identity
            );

            // Ensure the drone targets the player tank.
            AIDroneController controller =
                drone.GetComponent<AIDroneController>();

            if (controller != null)
            {
                controller.tank = playerTank;
            }

            return;
        }

        Debug.LogWarning(
            "Could not find a safe position for an AI drone."
        );
    }

    private void OnDrawGizmosSelected()
    {
        if (playerTank == null)
            return;

        Gizmos.color = Color.yellow;

        Vector3 center = playerTank.position;
        center.y += spawnHeight;

        Gizmos.DrawWireSphere(center, minSpawnDistance);
        Gizmos.DrawWireSphere(center, maxSpawnDistance);
    }
}
