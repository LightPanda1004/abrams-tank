
using UnityEngine;

public class AIRandomSpawner : MonoBehaviour
{
    [Header("References")]
    public GameObject aiTankPrefab;
    public Transform playerTank;

    [Header("Random Tank Count")]
    public int minTankCount = 1;
    public int maxTankCount = 7;

    [Header("Spawn Distance")]
    public float minDistance = 20f;
    public float maxDistance = 100f;

    [Header("Ground Detection")]
    public LayerMask groundLayers = ~0;
    public float raycastHeight = 150f;
    public float groundOffset = 0.1f;

    [Header("Spawn Attempts")]
    public int attemptsPerTank = 30;

    private void Start()
    {
        SpawnTanks();
    }

    public void SpawnTanks()
    {
        if (aiTankPrefab == null || playerTank == null)
        {
            Debug.LogError(
                "Assign both AI Tank Prefab and Player Tank!",
                this
            );
            return;
        }

        int count = Random.Range(
            Mathf.Max(4, minTankCount),
            Mathf.Max(4, maxTankCount) + 1
        );

        int spawned = 0;

        for (int i = 0; i < count; i++)
        {
            if (TrySpawnTank(i + 1))
                spawned++;
        }

    }

    private bool TrySpawnTank(int tankNumber)
    {
        for (int attempt = 0; attempt < attemptsPerTank; attempt++)
        {
            Vector2 direction = Random.insideUnitCircle;

            if (direction.sqrMagnitude < 0.01f)
                continue;

            direction.Normalize();

            float distance = Random.Range(minDistance, maxDistance);

            Vector3 candidate = playerTank.position +
                new Vector3(direction.x, (float)0.6, direction.y) * distance;

            Vector3 spawnPosition = candidate;
            bool foundGround = false;

            Vector3 rayOrigin = new Vector3(
                candidate.x,
                playerTank.position.y + raycastHeight,
                candidate.z
            );

            // First try the configured ground layers.
            if (Physics.Raycast(
                rayOrigin,
                Vector3.down,
                out RaycastHit hit,
                raycastHeight * 2f,
                groundLayers,
                QueryTriggerInteraction.Ignore))
            {
                spawnPosition = hit.point + Vector3.up * groundOffset;
                foundGround = true;
            }

            // If Ground layer setup is incorrect, try all layers.
            if (!foundGround &&
                Physics.Raycast(
                    rayOrigin,
                    Vector3.down,
                    out hit,
                    raycastHeight * 2f,
                    ~0,
                    QueryTriggerInteraction.Ignore))
            {
                // Don't spawn on the player tank itself.
                if (hit.transform != playerTank &&
                    !hit.transform.IsChildOf(playerTank))
                {
                    spawnPosition = hit.point + Vector3.up * groundOffset;
                    foundGround = true;
                }
            }

            // Final fallback for a flat battlefield.
            if (!foundGround)
            {
                spawnPosition.y = playerTank.position.y;
                
            }

            Quaternion rotation = Quaternion.Euler(
                0f,
                Random.Range(0f, 360f),
                0f
            );

            GameObject tank = Instantiate(
                aiTankPrefab,
                spawnPosition,
                rotation
            );
            TankHealth health = tank.GetComponent<TankHealth>();

            if (health != null && GameDirector.Instance != null)
            {
                GameDirector.Instance.RegisterAITank(health);
            }

            tank.name = "AI Tank " + tankNumber;


            return true;
        }

        return false;
    }
}
