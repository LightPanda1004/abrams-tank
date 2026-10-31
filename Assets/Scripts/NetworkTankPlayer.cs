using Unity.Netcode;
using UnityEngine;

public class NetworkTankPlayer : NetworkBehaviour
{
    private readonly NetworkVariable<float> syncedHealth = new NetworkVariable<float>(
        500f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private TankHealth tankHealth;
    private TankMovement tankMovement;
    private TankFire tankFire;
    private float nextShellTime;

    private void Awake()
    {
        tankHealth = GetComponent<TankHealth>();
        tankMovement = GetComponent<TankMovement>();
        tankFire = GetComponent<TankFire>();
    }

    public override void OnNetworkSpawn()
    {
        syncedHealth.OnValueChanged += OnHealthChanged;

        if (IsServer && tankHealth != null)
            syncedHealth.Value = tankHealth.CurrentHealth;

        SetLocalControls(IsOwner);

        if (IsOwner)
        {
            if (GameDirector.Instance != null)
            {
                GameDirector.Instance.playerHealth = tankHealth;
                GameDirector.Instance.playerMovement = tankMovement;
            }

            TankCameraFollow cameraFollow =
                FindObjectOfType<TankCameraFollow>();

            if (cameraFollow != null)
                cameraFollow.target = transform;
        }
    }

    public override void OnNetworkDespawn()
    {
        syncedHealth.OnValueChanged -= OnHealthChanged;
    }

    public void SetServerHealth(float health)
    {
        if (IsServer)
            syncedHealth.Value = Mathf.Max(0f, health);
    }

    [ServerRpc]
    public void FireShellServerRpc(
        float shellSpeed,
        ServerRpcParams rpcParams = default)
    {
        if (Time.time < nextShellTime || tankFire == null ||
            tankFire.shellPrefab == null || tankFire.firePoint == null)
            return;

        nextShellTime = Time.time + Mathf.Max(0.1f, tankFire.fireCooldown);

        GameObject shellObject = Instantiate(
            tankFire.shellPrefab,
            tankFire.firePoint.position,
            tankFire.firePoint.rotation
        );

        Rigidbody shellRigidbody = shellObject.GetComponent<Rigidbody>();
        if (shellRigidbody != null)
        {
            shellRigidbody.isKinematic = false;
            shellRigidbody.velocity = tankFire.firePoint.forward * shellSpeed;
        }

        TankShell shell = shellObject.GetComponent<TankShell>();
        if (shell != null)
            shell.SetOwner(transform);

        NetworkObject shellNetworkObject =
            shellObject.GetComponent<NetworkObject>();

        if (shellNetworkObject == null)
        {
            Debug.LogError("The shell prefab needs a NetworkObject component.");
            Destroy(shellObject);
            return;
        }

        shellNetworkObject.Spawn();
    }

    private void OnHealthChanged(float previousHealth, float newHealth)
    {
        if (!IsServer && tankHealth != null)
            tankHealth.SetReplicatedHealth(newHealth);
    }

    private void SetLocalControls(bool isLocalPlayer)
    {
        if (tankMovement != null)
            tankMovement.enabled = isLocalPlayer;

        TankTurretMove turretControls = GetComponent<TankTurretMove>();
        if (turretControls != null)
            turretControls.enabled = isLocalPlayer;

        if (tankFire != null)
            tankFire.enabled = isLocalPlayer;

        AntiDroneLauncher missileLauncher =
            GetComponentInChildren<AntiDroneLauncher>();
        if (missileLauncher != null)
            missileLauncher.enabled = isLocalPlayer;

        TankScopeZoom scopeControls = GetComponentInChildren<TankScopeZoom>();
        if (scopeControls != null)
            scopeControls.enabled = isLocalPlayer;

        if (!isLocalPlayer)
        {
            foreach (Camera tankCamera in GetComponentsInChildren<Camera>(true))
                tankCamera.enabled = false;

            foreach (AudioListener audioListener in
                     GetComponentsInChildren<AudioListener>(true))
                audioListener.enabled = false;
        }
    }

}
