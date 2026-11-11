using System.Collections;
using UnityEngine;

public class TankDronecontroll : MonoBehaviour
{
    [Header("Drone")]
    public Transform droneTransform;
    public Camera droneCamera;
    public GameObject minimapDroneMarkerPrefab;
    public float deploymentHeight = 5f;
    public float deploymentSpeed = 3f;
    public float returnSpeed = 4f;

    [Header("Flight")]
    public float maximumSpeed = 8f;
    public float acceleration = 5f;
    public float maximumTurnSpeed = 90f;
    public float turnAcceleration = 180f;
    public float turnDeceleration = 240f;
    public float mouseSensitivityY = 0.3f;
    [Min(0.01f)]
    public float pitchLevelingSmoothTime = 0.8f;
    public float maximumPitch = 45f;
    public float maximumBank = 25f;
    public float propellerRotationSpeed = 1800f;

    private Rigidbody droneBody;
    private TankDroneImpact droneImpact;
    private MinimapDroneMarker minimapDroneMarker;
    private TankMovement tankMovement;
    private TankTurretMove turretControls;
    private TankFire tankFire;
    private AntiDroneLauncher missileLauncher;
    private TankScopeZoom scopeControls;
    private Camera tankCamera;
    private Camera cannonCamera;
    private Camera[] gameplayCameras = new Camera[0];
    private DroneCamerafollow droneCameraFollow;
    private AudioListener droneAudioListener;
    private AudioListener[] localAudioListeners =
        new AudioListener[0];
    private bool[] originalAudioListenerStates =
        new bool[0];

    private Transform homeParent;
    private Vector3 homeLocalPosition;
    private Quaternion homeLocalRotation;
    private Collider[] droneColliders = new Collider[0];
    private Collider[] tankColliders = new Collider[0];
    private Transform[] propellers = new Transform[0];

    private bool isLocalOwner;
    private bool isDeployed;
    private bool isDroneView;
    private bool isDestroyed;
    private float yaw;
    private float pitch;
    private float pitchLevelingVelocity;
    private float turnVelocity;
    private float throttleInput;
    private float turnInput;
    private float liftInput;

    private FlightState flightState;
    private ReturnPhase returnPhase;
    private Vector3 returnClimbPosition;
    private float returnAltitude;

    private enum FlightState
    {
        Docked,
        Deploying,
        Flying,
        Returning
    }

    private enum ReturnPhase
    {
        Climbing,
        Traversing,
        Landing
    }

    public bool IsDeployed => isDeployed && !isDestroyed;
    public bool IsPilotingDrone =>
        isLocalOwner && isDeployed && isDroneView &&
        flightState != FlightState.Returning && !isDestroyed;
    public bool IsActiveForDetection =>
        isLocalOwner && isDeployed &&
        flightState != FlightState.Returning && !isDestroyed;
    public Camera DetectionDisplayCamera =>
        isDroneView
            ? droneCamera
            : scopeControls != null && scopeControls.IsAiming
                ? cannonCamera
                : tankCamera;
    public bool CanHandleDroneImpact =>
        isLocalOwner && isDeployed && !isDestroyed;

    private void Awake()
    {
        if (droneCamera != null)
        {
            droneCamera.enabled = false;
            droneAudioListener =
                droneCamera.GetComponent<AudioListener>();
            if (droneAudioListener != null)
                droneAudioListener.enabled = false;
        }

        if (droneTransform != null)
        {
            droneBody = droneTransform.GetComponentInParent<Rigidbody>();
            if (droneBody != null)
                droneTransform = droneBody.transform;

            homeParent = droneTransform.parent;
            homeLocalPosition = droneTransform.localPosition;
            homeLocalRotation = droneTransform.localRotation;
            if (droneBody != null)
            {
                droneBody.useGravity = false;
                droneBody.interpolation = RigidbodyInterpolation.None;
                droneBody.collisionDetectionMode =
                    CollisionDetectionMode.ContinuousDynamic;
                droneBody.isKinematic = true;
            }

            CachePropellers();
            droneColliders =
                droneTransform.GetComponentsInChildren<Collider>(true);
            tankColliders = GetComponentsInChildren<Collider>(true);
            SetTankCollisionsIgnored(true);
            droneImpact = droneTransform.GetComponent<TankDroneImpact>();
        }
    }

    public void Initialize(bool localOwner)
    {
        isLocalOwner = localOwner;

        if (droneTransform == null || droneBody == null ||
            droneCamera == null || homeParent == null)
        {
            Debug.LogError(
                "TankDronecontroll: The drone needs a Rigidbody, transform, and camera in the Abrams prefab.",
                this
            );
            enabled = false;
            return;
        }

        droneCameraFollow = droneCamera.GetComponent<DroneCamerafollow>();
        if (droneCameraFollow == null)
        {
            Debug.LogError(
                "TankDronecontroll: The drone camera needs a DroneCamerafollow component.",
                this
            );
            enabled = false;
            return;
        }

        droneCameraFollow.target = droneTransform;
        if (droneImpact == null)
            droneImpact = droneTransform.GetComponent<TankDroneImpact>();
        if (droneImpact == null)
        {
            Debug.LogError(
                "TankDronecontroll: The drone needs a TankDroneImpact component.",
                this
            );
            enabled = false;
            return;
        }

        droneImpact.Initialize(this, droneBody);
        if (droneBody != null)
        {
            droneBody.isKinematic = true;
            droneBody.interpolation = RigidbodyInterpolation.None;
        }

        if (!isLocalOwner)
        {
            droneCamera.enabled = false;
            return;
        }

        ConfigureMinimapDroneMarker();

        TankCameraFollow cameraFollow =
            FindObjectOfType<TankCameraFollow>();
        tankCamera = cameraFollow != null
            ? cameraFollow.GetComponent<Camera>()
            : Camera.main;
        turretControls = GetComponent<TankTurretMove>();
        tankMovement = GetComponent<TankMovement>();
        tankFire = GetComponent<TankFire>();
        missileLauncher = GetComponentInChildren<AntiDroneLauncher>(true);
        scopeControls = GetComponentInChildren<TankScopeZoom>(true);
        cannonCamera = scopeControls != null
            ? scopeControls.CannonCamera
            : null;
        CacheGameplayCameras();
        CacheAudioListeners();

        if (tankCamera == null || tankCamera == droneCamera)
        {
            Debug.LogError(
                "TankDronecontroll: Could not find a tank camera separate from the drone camera.",
                this
            );
            enabled = false;
            return;
        }

        droneCamera.enabled = false;
        droneCamera.targetTexture = null;
        droneCamera.targetDisplay = tankCamera.targetDisplay;
        droneCamera.rect = tankCamera.rect;
        droneCamera.depth = Mathf.Max(
            droneCamera.depth,
            tankCamera.depth + 1f
        );
        tankCamera.enabled = true;
        flightState = FlightState.Docked;
        SetTankControls(true);
    }

    public void ResetForDespawn()
    {
        isDeployed = false;
        SetMinimapDroneMarkerVisible(false);
        if (droneTransform != null && homeParent != null)
        {
            SetTankCollisionsIgnored(true);
            if (droneCameraFollow != null)
                droneCameraFollow.StopFollowing();

            droneBody.isKinematic = true;
            droneBody.interpolation = RigidbodyInterpolation.None;
            droneTransform.SetParent(homeParent, false);
            droneTransform.localPosition = homeLocalPosition;
            droneTransform.localRotation = homeLocalRotation;
        }

        if (!isLocalOwner)
            return;

        isDroneView = false;
        flightState = FlightState.Docked;
        if (droneCamera != null)
            droneCamera.enabled = false;
        SetDroneAudioView(false);
        if (tankCamera != null)
            tankCamera.enabled = true;
    }

    private void Update()
    {
        if (!isLocalOwner)
            return;

        if (isDestroyed)
        {
            return;
        }

        if (GameDirector.Instance != null &&
            (GameDirector.Instance.IsPaused ||
             GameDirector.Instance.IsGameOver))
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            if (flightState == FlightState.Docked)
                BeginDeployment();
            else
                BeginReturn();

            return;
        }

        if (Input.GetKeyDown(KeyCode.Q) &&
            (flightState == FlightState.Deploying ||
             flightState == FlightState.Flying))
        {
            SetDroneView(!isDroneView);
        }

        if (flightState == FlightState.Docked)
            return;

        if (flightState == FlightState.Flying)
        {
            if (isDroneView)
                CaptureFlightInput();
            else
            {
                throttleInput = 0f;
                turnInput = 0f;
                liftInput = 0f;
            }
        }

        SpinPropellers();
    }

    private void FixedUpdate()
    {
        if (!isLocalOwner || droneBody == null || isDestroyed ||
            (GameDirector.Instance != null &&
             (GameDirector.Instance.IsPaused ||
              GameDirector.Instance.IsGameOver)))
        {
            return;
        }

        if (flightState == FlightState.Docked)
            return;

        if (flightState == FlightState.Deploying)
            UpdateDeployment();
        else if (flightState == FlightState.Returning)
            UpdateReturn();
        else if (flightState == FlightState.Flying)
            UpdateFlight();
    }

    private void LateUpdate()
    {
        if (isLocalOwner && isDroneView)
            SetGameplayCamera(droneCamera);
    }

    private void BeginDeployment()
    {
        isDeployed = true;
        SetMinimapDroneMarkerVisible(true);
        flightState = FlightState.Deploying;
        droneBody.interpolation = RigidbodyInterpolation.Interpolate;
        droneBody.isKinematic = false;
        droneBody.useGravity = false;
        droneBody.velocity = Vector3.zero;
        droneBody.angularVelocity = Vector3.zero;
        turnVelocity = 0f;
        yaw = droneTransform.eulerAngles.y;
        pitch = 0f;
        pitchLevelingVelocity = 0f;

        SetTankCollisionsIgnored(true);
        droneTransform.SetParent(null, true);
        droneCameraFollow.BeginFollowing();
        droneCameraFollow.SetFollowYaw(yaw);
        SetDroneView(true);
    }

    private void UpdateDeployment()
    {
        Vector3 launchPosition = homeParent.TransformPoint(homeLocalPosition);
        Vector3 targetPosition = launchPosition + Vector3.up * deploymentHeight;
        Vector3 offset = targetPosition - droneBody.position;

        if (offset.magnitude > 0.05f)
        {
            droneBody.velocity = offset.normalized * deploymentSpeed;
            droneBody.MoveRotation(Quaternion.RotateTowards(
                droneBody.rotation,
                Quaternion.Euler(0f, yaw, 0f),
                deploymentSpeed * 30f * Time.fixedDeltaTime
            ));
            return;
        }

        droneBody.velocity = Vector3.zero;
        SetTankCollisionsIgnored(false);
        flightState = FlightState.Flying;
        SetDroneView(true);
    }

    private void CaptureFlightInput()
    {
        turnInput = 0f;
        if (Input.GetKey(KeyCode.A))
            turnInput -= 1f;
        if (Input.GetKey(KeyCode.D))
            turnInput += 1f;

        throttleInput = Input.GetAxisRaw("Vertical");
        bool movementKeyHeld =
            !Mathf.Approximately(throttleInput, 0f) ||
            !Mathf.Approximately(turnInput, 0f);
        if (movementKeyHeld)
        {
            pitchLevelingVelocity = 0f;
            pitch = Mathf.Clamp(
                pitch + Input.GetAxis("Mouse Y") * mouseSensitivityY,
                -maximumPitch,
                maximumPitch
            );
        }
        else
        {
            pitch = Mathf.SmoothDamp(
                pitch,
                0f,
                ref pitchLevelingVelocity,
                Mathf.Max(0.01f, pitchLevelingSmoothTime),
                Mathf.Infinity,
                Time.deltaTime
            );
        }

        liftInput = 0f;
        if (Input.GetKey(KeyCode.Space))
            liftInput += 1f;
        if (Input.GetKey(KeyCode.LeftControl) ||
            Input.GetKey(KeyCode.RightControl))
            liftInput -= 1f;
    }

    private void UpdateFlight()
    {
        float targetTurnVelocity = turnInput * maximumTurnSpeed;
        float turnRate = Mathf.Approximately(turnInput, 0f)
            ? turnDeceleration
            : turnAcceleration;
        turnVelocity = Mathf.MoveTowards(
            turnVelocity,
            targetTurnVelocity,
            turnRate * Time.fixedDeltaTime
        );
        yaw += turnVelocity * Time.fixedDeltaTime;
        droneCameraFollow.SetFollowYaw(yaw);

        Quaternion desiredRotation = Quaternion.Euler(
            pitch,
            yaw,
            maximumTurnSpeed > 0f
                ? -turnVelocity / maximumTurnSpeed * maximumBank
                : 0f
        );
        droneBody.MoveRotation(desiredRotation);

        Vector3 desiredVelocity =
            desiredRotation * Vector3.forward * throttleInput +
            Vector3.up * liftInput;
        desiredVelocity = Vector3.ClampMagnitude(
            desiredVelocity * maximumSpeed,
            maximumSpeed
        );

        droneBody.velocity = Vector3.MoveTowards(
            droneBody.velocity,
            desiredVelocity,
            acceleration * Time.fixedDeltaTime
        );
    }

    private void BeginReturn()
    {
        if (flightState == FlightState.Returning ||
            flightState == FlightState.Docked)
        {
            return;
        }

        flightState = FlightState.Returning;
        Vector3 homePosition = homeParent.TransformPoint(homeLocalPosition);
        float clearance = Mathf.Max(1f, deploymentHeight);
        returnAltitude = Mathf.Max(
            droneBody.position.y,
            homePosition.y + clearance
        );
        returnClimbPosition = new Vector3(
            droneBody.position.x,
            returnAltitude,
            droneBody.position.z
        );
        returnPhase = ReturnPhase.Climbing;
        SetTankCollisionsIgnored(true);
        throttleInput = 0f;
        turnInput = 0f;
        liftInput = 0f;
        turnVelocity = 0f;
        SetDroneView(false);
        SetTankControls(true);
    }

    private void UpdateReturn()
    {
        Vector3 homePosition = homeParent.TransformPoint(homeLocalPosition);
        Quaternion homeRotation = homeParent.rotation * homeLocalRotation;
        float clearance = Mathf.Max(1f, deploymentHeight);
        Vector3 landingApproach = new Vector3(
            homePosition.x,
            Mathf.Max(returnAltitude, homePosition.y + clearance),
            homePosition.z
        );

        if (returnPhase == ReturnPhase.Climbing)
        {
            if (MoveReturnTo(returnClimbPosition, homeRotation))
                returnPhase = ReturnPhase.Traversing;
            return;
        }

        if (returnPhase == ReturnPhase.Traversing)
        {
            if (MoveReturnTo(landingApproach, homeRotation))
                returnPhase = ReturnPhase.Landing;
            return;
        }

        if (!MoveReturnTo(homePosition, homeRotation))
            return;

        droneBody.velocity = Vector3.zero;
        droneBody.angularVelocity = Vector3.zero;
        droneBody.position = homePosition;
        droneBody.rotation = homeRotation;
        droneBody.isKinematic = true;
        droneTransform.SetParent(homeParent, false);
        droneTransform.localPosition = homeLocalPosition;
        droneTransform.localRotation = homeLocalRotation;
        droneBody.interpolation = RigidbodyInterpolation.None;
        droneCameraFollow.StopFollowing();
        isDeployed = false;
        SetMinimapDroneMarkerVisible(false);
        flightState = FlightState.Docked;
        pitch = 0f;
        SetDroneView(false);
        SetTankControls(true);
    }

    private bool MoveReturnTo(Vector3 targetPosition, Quaternion targetRotation)
    {
        Vector3 offset = targetPosition - droneBody.position;
        float distance = offset.magnitude;
        float speed = Mathf.Max(0.1f, returnSpeed);
        float deceleration = Mathf.Max(acceleration, speed * 2f);
        float stoppingSpeed = Mathf.Sqrt(2f * deceleration * distance);
        Vector3 desiredVelocity = distance > 0.0001f
            ? offset / distance * Mathf.Min(speed, stoppingSpeed)
            : Vector3.zero;

        droneBody.velocity = Vector3.MoveTowards(
            droneBody.velocity,
            desiredVelocity,
            deceleration * Time.fixedDeltaTime
        );
        droneBody.MoveRotation(Quaternion.Slerp(
            droneBody.rotation,
            targetRotation,
            1f - Mathf.Exp(-speed * Time.fixedDeltaTime)
        ));

        if (distance > 0.02f || droneBody.velocity.sqrMagnitude > 0.01f)
        {
            return false;
        }

        droneBody.velocity = Vector3.zero;
        droneBody.angularVelocity = Vector3.zero;
        droneBody.MoveRotation(targetRotation);
        return true;
    }

    private void SetDroneView(bool droneView)
    {
        isDroneView = droneView;

        if (droneView)
        {
            if (scopeControls != null)
            {
                scopeControls.ExitAiming();
            }

            tankCamera.enabled = false;
            if (cannonCamera != null)
                cannonCamera.enabled = false;
            droneCamera.enabled = true;
            SetDroneAudioView(true);
            SetGameplayCamera(droneCamera);
            SetTankControls(false);
            if (scopeControls != null)
            {
                scopeControls.enabled = true;
                scopeControls.SetDroneScopeMode(true);
            }
            return;
        }

        if (scopeControls != null)
            scopeControls.SetDroneScopeMode(false);

        droneCamera.enabled = false;
        SetDroneAudioView(false);
        tankCamera.enabled = true;
        if (cannonCamera != null)
            cannonCamera.enabled = false;
        SetGameplayCamera(tankCamera);
        SetTankControls(flightState == FlightState.Docked ||
                        flightState == FlightState.Flying);
    }

    private void CacheGameplayCameras()
    {
        gameplayCameras = FindObjectsOfType<Camera>(true);
    }

    private void SetGameplayCamera(Camera activeCamera)
    {
        if (activeCamera == null)
            return;

        foreach (Camera camera in gameplayCameras)
        {
            if (camera == null || camera.targetTexture != null ||
                camera.targetDisplay != activeCamera.targetDisplay)
            {
                continue;
            }

            camera.enabled = camera == activeCamera;
        }
    }

    private void SetTankControls(bool tankEnabled)
    {
        if (tankMovement != null)
            tankMovement.enabled = tankEnabled;
        if (turretControls != null)
            turretControls.enabled = tankEnabled;
        if (tankFire != null)
            tankFire.enabled = tankEnabled;
        if (missileLauncher != null)
            missileLauncher.enabled = tankEnabled;
        if (scopeControls != null)
        {
            if (!tankEnabled)
                scopeControls.ExitAiming();

            scopeControls.enabled = tankEnabled || isDroneView;
        }
    }

    private void CachePropellers()
    {
        Transform[] childTransforms =
            droneTransform.GetComponentsInChildren<Transform>(true);
        System.Collections.Generic.List<Transform> found =
            new System.Collections.Generic.List<Transform>();

        foreach (Transform child in childTransforms)
        {
            string childName = child.name.ToLowerInvariant();
            if (childName.Contains("propeller") || childName.Contains("rotor"))
                found.Add(child);
        }

        propellers = found.ToArray();
    }

    private void SetTankCollisionsIgnored(bool ignored)
    {
        foreach (Collider droneCollider in droneColliders)
        {
            if (droneCollider == null)
                continue;

            foreach (Collider tankCollider in tankColliders)
            {
                if (tankCollider == null || tankCollider == droneCollider ||
                    tankCollider.transform == droneTransform ||
                    tankCollider.transform.IsChildOf(droneTransform))
                {
                    continue;
                }

                Physics.IgnoreCollision(droneCollider, tankCollider, ignored);
            }
        }
    }

    private void CacheAudioListeners()
    {
        localAudioListeners = FindObjectsOfType<AudioListener>(true);
        originalAudioListenerStates =
            new bool[localAudioListeners.Length];

        for (int i = 0; i < localAudioListeners.Length; i++)
        {
            AudioListener listener = localAudioListeners[i];
            originalAudioListenerStates[i] =
                listener != droneAudioListener && listener.enabled;
            if (listener == droneAudioListener)
                listener.enabled = false;
        }
    }

    private void SetDroneAudioView(bool droneView)
    {
        for (int i = 0; i < localAudioListeners.Length; i++)
        {
            AudioListener listener = localAudioListeners[i];
            if (listener == null)
                continue;

            listener.enabled = droneView
                ? listener == droneAudioListener
                : originalAudioListenerStates[i];
        }
    }

    private void SpinPropellers()
    {
        if (!isDeployed || isDestroyed)
            return;

        foreach (Transform propeller in propellers)
        {
            if (propeller != null)
            {
                string propellerName = propeller.name.ToLowerInvariant();
                bool isLeftPropeller =
                    propellerName.Contains("left") ||
                    propellerName.StartsWith("fl_") ||
                    propellerName.StartsWith("bl_");
                bool isRightPropeller =
                    propellerName.Contains("right") ||
                    propellerName.StartsWith("fr_") ||
                    propellerName.StartsWith("br_");
                float side = droneTransform.InverseTransformPoint(
                    propeller.position
                ).x;
                float direction = isLeftPropeller
                    ? -1f
                    : isRightPropeller || side >= 0f
                        ? 1f
                        : -1f;
                propeller.Rotate(
                    Vector3.forward,
                    direction * propellerRotationSpeed * Time.deltaTime,
                    Space.Self
                );
            }
        }
    }

    public void HandleDroneImpact(Vector3 impactPoint, bool sink = true)
    {
        if (!CanHandleDroneImpact)
            return;

        DestroyDrone(impactPoint, sink, true);
    }

    public void ApplyTankShellImpact(Vector3 impactPoint)
    {
        if (isDestroyed)
            return;

        DestroyDrone(impactPoint, true, isLocalOwner);
    }

    private void DestroyDrone(
        Vector3 impactPoint,
        bool sink,
        bool handleLocalControls)
    {
        isDestroyed = true;
        SetMinimapDroneMarkerVisible(false);
        throttleInput = 0f;
        turnInput = 0f;
        liftInput = 0f;
        turnVelocity = 0f;
        SetTankCollisionsIgnored(true);
        if (droneImpact != null)
            droneImpact.BeginDestruction(impactPoint, sink);

        if (handleLocalControls && isDroneView)
            StartCoroutine(SwitchToTankViewAfterExplosion());

        if (handleLocalControls)
        {
            SetTankControls(true);
        }
    }

    public void ApplyMissileImpact(Vector3 impactPoint)
    {
        if (isDestroyed || !isDeployed)
            return;

        if (isLocalOwner)
        {
            HandleDroneImpact(impactPoint, true);
            return;
        }

        isDestroyed = true;
        SetMinimapDroneMarkerVisible(false);
        SetTankCollisionsIgnored(true);
        if (droneImpact != null)
            droneImpact.BeginDestruction(impactPoint, false);
    }

    private void ConfigureMinimapDroneMarker()
    {
        if (minimapDroneMarkerPrefab == null)
        {
            Debug.LogError(
                "TankDronecontroll: A green minimap drone marker prefab is required.",
                this
            );
            return;
        }

        minimapDroneMarker =
            droneTransform.GetComponent<MinimapDroneMarker>();
        if (minimapDroneMarker == null)
        {
            minimapDroneMarker =
                droneTransform.gameObject.AddComponent<MinimapDroneMarker>();
        }

        minimapDroneMarker.targetDrone = droneTransform;
        minimapDroneMarker.directionOrigin = transform;
        minimapDroneMarker.droneMarker = minimapDroneMarkerPrefab;
        minimapDroneMarker.edgeMarker = minimapDroneMarkerPrefab;
        minimapDroneMarker.edgePadding = 1f;

        foreach (Camera childCamera in GetComponentsInChildren<Camera>(true))
        {
            if (childCamera.gameObject.name == "MinimapCamera")
            {
                minimapDroneMarker.minimapCamera = childCamera;
                break;
            }
        }

        minimapDroneMarker.enabled = false;
    }

    private void SetMinimapDroneMarkerVisible(bool visible)
    {
        if (minimapDroneMarker != null)
            minimapDroneMarker.enabled = visible;
    }

    private void SwitchToTankViewAfterImpact()
    {
        if (!isLocalOwner || !isDroneView)
            return;

        SetDroneView(false);
        SetTankControls(true);
    }

    private IEnumerator SwitchToTankViewAfterExplosion()
    {
        yield return new WaitForSeconds(1.5f);
        SwitchToTankViewAfterImpact();
    }


}
