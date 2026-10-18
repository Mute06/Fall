using System;
using UnityEngine;

namespace FirstPersonSystem
{
    public class WinchSystem : MonoBehaviour
    {
        public enum WinchState
        {
            Idle,
            CarryingRope,
            WinchRigged
        }

        public static WinchSystem Instance { get; private set; }

        [Header("Layer Masks")]
        [SerializeField] private LayerMask anchorableLayer;
        [SerializeField] private LayerMask groundCollisionLayers;

        [Header("Rope Parameters")]
        [SerializeField] private float maxRopeLength = 30f;
        [SerializeField] private float minRopeLength = 1.0f;
        [SerializeField] private float reelSpeed = 2.0f;
        [SerializeField] private float maxPullForce = 3500f;
        [SerializeField] private float brakeStiffness = 2500f;
        [SerializeField] private float ropeWidth = 0.05f;

        [Header("Visuals & References")]
        [SerializeField] private Material ropeMaterial;
        [SerializeField] private GameObject anchorPrefab;
        [SerializeField] private Transform ropeHandHolder;

        [Header("Anchor Detection")]
        [Tooltip("Maximum raycast distance for targeting anchorable objects (trees).")]
        [SerializeField] private float anchorPlacementDistance = 8.5f;

        [Header("Audio Feedback")]
        [Tooltip("Audio clip played while winching/pulling the coffin.")]
        [SerializeField] private AudioClip pullAudioClip;
        [Tooltip("Audio source used for winch pulling sound. If not assigned, a dedicated child audio source will be created automatically.")]
        [SerializeField] private AudioSource pullAudioSource;
        [Tooltip("Target audio volume for the winch pull sound.")]
        [Range(0f, 1f)]
        [SerializeField] private float pullAudioVolume = 0.8f;
        [Tooltip("Base pitch when low or minimal force is applied.")]
        [Range(0.5f, 2f)]
        [SerializeField] private float minPitch = 0.95f;
        [Tooltip("Maximum pitch when high pulling force is applied.")]
        [Range(0.5f, 2f)]
        [SerializeField] private float maxPitch = 1.18f;
        [Tooltip("Speed at which the pitch smoothly tracks applied force.")]
        [SerializeField] private float pitchSmoothSpeed = 4f;
        [Tooltip("Speed of audio volume fade-in and fade-out.")]
        [SerializeField] private float audioFadeSpeed = 12f;

        // Dependencies
        private FirstPersonController controller;
        private Interactor interactor;
        private InputManager input;
        private WinchEnergySystem energySystem;
        private CoffinDistanceWarningSystem coffinWarningSystem;

        // Runtime State
        private WinchState currentState = WinchState.Idle;
        private AnchorPoint currentAnchor;
        private VerletRope activeRope;
        private CoffinPullable currentCoffin;
        private float currentRopeLength;
        private bool isWinching = false;
        private bool isPullAudioPlaying = false;
        private float attachCooldown = 0f;
        private float currentAppliedPullForce = 0f;
        private bool isAimingAtAnchorable = false;
        private RaycastHit lastAnchorHit;

        public WinchState CurrentState => currentState;
        public bool IsCarryingRope => currentState == WinchState.CarryingRope;
        public bool IsWinchRigged => currentState == WinchState.WinchRigged;
        public float CurrentRopeLength => currentRopeLength;
        public float MaxRopeLength => maxRopeLength;
        public bool IsWinching => isWinching;
        public bool IsAimingAtAnchorable => isAimingAtAnchorable;
        public RaycastHit LastAnchorHit => lastAnchorHit;
        public float AnchorPlacementDistance => anchorPlacementDistance;
        public CoffinDistanceWarningSystem CoffinWarningSystem => coffinWarningSystem;
        public float CurrentAppliedPullForce => currentAppliedPullForce;
        public AudioSource PullAudioSource => pullAudioSource;
        public AudioClip PullAudioClip => pullAudioClip;
        public float PullAudioVolume => pullAudioVolume;
        public float MinPitch => minPitch;
        public float MaxPitch => maxPitch;
        public bool IsPullAudioPlaying => isPullAudioPlaying;

        public event Action<WinchState> OnWinchStateChanged;
        public static event Action<AnchorPoint> OnAnyAnchorPlaced;
        public event Action<AnchorPoint> OnAnchorPlaced;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            controller = GetComponent<FirstPersonController>();
            interactor = GetComponent<Interactor>();
            input = InputManager.Instance != null ? InputManager.Instance : GetComponent<InputManager>();
            energySystem = GetComponent<WinchEnergySystem>();
            if (energySystem == null)
            {
                energySystem = gameObject.AddComponent<WinchEnergySystem>();
            }

            coffinWarningSystem = GetComponent<CoffinDistanceWarningSystem>();
            if (coffinWarningSystem == null)
            {
                coffinWarningSystem = gameObject.AddComponent<CoffinDistanceWarningSystem>();
            }

            if (anchorableLayer.value == 0)
            {
                int anchorLayer = LayerMask.NameToLayer("Anchorable");
                anchorableLayer = anchorLayer != -1 ? (1 << anchorLayer) : LayerMask.GetMask("Default");
            }

            if (groundCollisionLayers.value == 0)
            {
                groundCollisionLayers = LayerMask.GetMask("Default");
            }

            if (ropeHandHolder == null)
            {
                Camera cam = GetComponentInChildren<Camera>();
                if (cam != null)
                {
                    Transform holdPos = cam.transform.Find("Hold Position");
                    ropeHandHolder = holdPos != null ? holdPos : cam.transform;
                }
                else
                {
                    ropeHandHolder = transform;
                }
            }

            SetupPullAudio();
        }

        private void Update()
        {
            if (input == null) input = InputManager.Instance;

            // Q cancels/destroys rope at any time
            if (input != null && input.GetInput_CancelDown())
            {
                CancelRope();
                return;
            }

            switch (currentState)
            {
                case WinchState.Idle:
                    HandleIdleState();
                    break;
                case WinchState.CarryingRope:
                    HandleCarryingState();
                    break;
                case WinchState.WinchRigged:
                    HandleRiggedInput();
                    break;
            }

            UpdatePullAudio();
        }

        private void FixedUpdate()
        {
            if (currentState == WinchState.WinchRigged)
            {
                HandleRiggedPhysics();
            }
        }

        private void HandleIdleState()
        {
            isAimingAtAnchorable = false;
            lastAnchorHit = default;

            Camera cam = interactor != null && interactor.Cam != null ? interactor.Cam : Camera.main;
            if (cam == null) cam = GetComponentInChildren<Camera>();
            if (cam == null) return;

            Vector3 rayPoint = interactor != null ? interactor.InteractionRayPoint : new Vector3(0.5f, 0.5f, 0f);
            Ray ray = cam.ViewportPointToRay(rayPoint);
            float checkDist = Mathf.Max(anchorPlacementDistance, interactor != null ? interactor.InteractionDistance : 3.0f);

            if (Physics.Raycast(ray, out RaycastHit hit, checkDist, anchorableLayer))
            {
                isAimingAtAnchorable = true;
                lastAnchorHit = hit;
                if (input != null && input.GetInput_Interacted())
                {
                    PlaceAnchor(hit.point, hit.normal, hit.collider.transform);
                }
            }
        }

        private void PlaceAnchor(Vector3 point, Vector3 normal, Transform parent)
        {
            GameObject anchorObj;
            if (anchorPrefab != null)
            {
                anchorObj = Instantiate(anchorPrefab, point, Quaternion.LookRotation(normal));
            }
            else
            {
                anchorObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                anchorObj.name = "WinchAnchorPin";
                anchorObj.transform.localScale = new Vector3(0.08f, 0.15f, 0.08f);
                anchorObj.transform.position = point + normal * 0.08f;
                anchorObj.transform.rotation = Quaternion.LookRotation(normal) * Quaternion.Euler(90f, 0f, 0f);

                Collider col = anchorObj.GetComponent<Collider>();
                if (col != null) Destroy(col);
            }

            currentAnchor = anchorObj.AddComponent<AnchorPoint>();
            currentAnchor.Initialize(point, normal, parent);

            GameObject ropeObj = new GameObject("ActiveVerletRope");
            activeRope = ropeObj.AddComponent<VerletRope>();

            float initialDistance = Vector3.Distance(currentAnchor.RopeAttachPoint.position, ropeHandHolder.position);
            currentRopeLength = Mathf.Max(initialDistance, 1.0f);

            activeRope.Initialize(currentAnchor.RopeAttachPoint, ropeHandHolder, currentRopeLength, ropeMaterial, ropeWidth);

            currentState = WinchState.CarryingRope;
            OnWinchStateChanged?.Invoke(currentState);
            OnAnchorPlaced?.Invoke(currentAnchor);
            OnAnyAnchorPlaced?.Invoke(currentAnchor);
            Debug.Log("[WinchSystem] Anchor placed. Carrying stiff winch cable.");
        }

        private void HandleCarryingState()
        {
            if (currentAnchor == null || activeRope == null)
            {
                CancelRope();
                return;
            }

            Vector3 anchorPos = currentAnchor.RopeAttachPoint.position;
            Vector3 handPos = ropeHandHolder.position;
            float currentDist = Vector3.Distance(anchorPos, handPos);

            // Auto-spool cable taut to player to eliminate loose flopping
            currentRopeLength = Mathf.Clamp(currentDist, minRopeLength, maxRopeLength);
            activeRope.SetRopeLength(currentRopeLength);

            // Restrict player from walking beyond maxRopeLength
            if (currentDist >= maxRopeLength)
            {
                Vector3 toAnchor = (anchorPos - controller.transform.position);
                Vector3 pullDir = toAnchor.normalized;
                float overshoot = currentDist - maxRopeLength;

                if (overshoot > 0f)
                {
                    controller.ApplyExternalDisplacement(pullDir * overshoot);
                    controller.AddExternalForce(pullDir * (overshoot * 200f + 15f));
                }
            }

            // Direct click-to-attach coffin if looking at coffin within 5.5m
            if (input != null && input.GetInput_Interacted())
            {
                Camera cam = interactor != null && interactor.Cam != null ? interactor.Cam : Camera.main;
                if (cam == null) cam = GetComponentInChildren<Camera>();
                if (cam != null)
                {
                    Vector3 rayPoint = interactor != null ? interactor.InteractionRayPoint : new Vector3(0.5f, 0.5f, 0f);
                    Ray ray = cam.ViewportPointToRay(rayPoint);
                    if (Physics.Raycast(ray, out RaycastHit hit, 5.5f))
                    {
                        CoffinPullable coffin = hit.collider.GetComponentInParent<CoffinPullable>();
                        if (coffin != null)
                        {
                            Transform closestHook = coffin.GetClosestHookPoint(hit.point);
                            AttachRopeToCoffin(coffin, closestHook);
                        }
                    }
                }
            }
        }

        public void AttachRopeToCoffin(CoffinPullable coffin, Transform specificHookPoint = null)
        {
            if (currentState != WinchState.CarryingRope || coffin == null) return;

            currentCoffin = coffin;
            Transform hookPoint = specificHookPoint != null ? specificHookPoint : currentCoffin.TowHookPoint;
            currentCoffin.SetActiveHookPoint(hookPoint);
            currentCoffin.SetRopeConnected(true);

            // Set rope endpoint and smoothly align nodes directly to selected hook point
            activeRope.SetEndPoint(hookPoint, snapNodes: true);

            float distanceToAnchor = Vector3.Distance(currentAnchor.RopeAttachPoint.position, hookPoint.position);
            currentRopeLength = Mathf.Max(distanceToAnchor, minRopeLength);
            activeRope.SetRopeLength(currentRopeLength);

            // Prevent initial click from firing an immediate winch impulse
            attachCooldown = 0.35f;
            currentAppliedPullForce = 0f;
            isWinching = false;

            currentState = WinchState.WinchRigged;
            OnWinchStateChanged?.Invoke(currentState);
            Debug.Log($"[WinchSystem] Cable attached to hook '{hookPoint.name}'. Winch remote active.");
        }

        private void HandleRiggedInput()
        {
            if (currentAnchor == null || currentCoffin == null || activeRope == null)
            {
                CancelRope();
                return;
            }

            if (attachCooldown > 0f)
            {
                attachCooldown -= Time.deltaTime;
                isWinching = false;
                return;
            }

            // Player holding Left Mouse: Winch Motor Active
            if (input.GetInput_Interacting() && energySystem.HasEnergy)
            {
                isWinching = true;
                energySystem.ConsumeEnergy(currentAppliedPullForce, reelSpeed, Time.deltaTime);
            }
            else
            {
                isWinching = false;
            }
        }

        private void HandleRiggedPhysics()
        {
            if (currentAnchor == null || currentCoffin == null || activeRope == null) return;

            Vector3 anchorPos = currentAnchor.RopeAttachPoint.position;
            Vector3 coffinPos = currentCoffin.TowHookPoint.position;
            float currentDist = Vector3.Distance(anchorPos, coffinPos);
            Vector3 pullDir = (anchorPos - coffinPos).normalized;

            if (isWinching && energySystem.HasEnergy)
            {
                // 1. Reel in target rope length continuously
                currentRopeLength = Mathf.Max(minRopeLength, currentRopeLength - reelSpeed * Time.fixedDeltaTime);
                activeRope.SetRopeLength(currentRopeLength);

                // 2. Measure current speed along the pull direction
                Vector3 coffinVel = currentCoffin.Rb != null ? currentCoffin.Rb.linearVelocity : Vector3.zero;
                float speedAlongCable = Vector3.Dot(coffinVel, pullDir);
                float speedDeficit = reelSpeed - speedAlongCable;

                // 3. Counter gravity along the slope to sustain continuous motion
                float gravityUphillCounter = 0f;
                if (currentCoffin.Rb != null)
                {
                    gravityUphillCounter = Mathf.Max(0f, -Vector3.Dot(Physics.gravity, pullDir) * currentCoffin.Rb.mass);
                }

                // 4. Elastic tension from cable stretch
                float stretch = Mathf.Max(0f, currentDist - currentRopeLength);
                float stretchForce = stretch * 1600f;

                // 5. Total continuous driving force maintaining steady speed
                float targetForce = Mathf.Clamp(gravityUphillCounter + speedDeficit * 800f + stretchForce, 0f, maxPullForce);
                currentAppliedPullForce = Mathf.MoveTowards(currentAppliedPullForce, targetForce, 3500f * Time.fixedDeltaTime);

                currentCoffin.ApplyWinchForce(pullDir, currentAppliedPullForce);
            }
            else
            {
                currentAppliedPullForce = 0f;

                // Winch Brake: holds coffin securely if it rolls downhill beyond current rope length
                if (currentDist > currentRopeLength)
                {
                    float overshoot = currentDist - currentRopeLength;
                    float brakeForce = Mathf.Clamp(overshoot * brakeStiffness, 0f, maxPullForce);
                    currentCoffin.ApplyWinchForce(pullDir, brakeForce);
                }
            }
        }

        /// <summary>
        /// Cancels the rope, destroys the anchor and cable, and frees the coffin with NO BRAKE.
        /// </summary>
        public void CancelRope()
        {
            if (currentState == WinchState.Idle && currentAnchor == null && activeRope == null) return;

            Debug.Log("[WinchSystem] Cable canceled. Anchor destroyed and coffin released.");

            if (currentCoffin != null)
            {
                currentCoffin.SetRopeConnected(false);
                currentCoffin = null;
            }

            if (activeRope != null)
            {
                Destroy(activeRope.gameObject);
                activeRope = null;
            }

            if (currentAnchor != null)
            {
                currentAnchor.DestroyAnchor();
                currentAnchor = null;
            }

            isWinching = false;
            currentAppliedPullForce = 0f;
            attachCooldown = 0f;
            currentState = WinchState.Idle;
            StopPullAudioImmediate();
            OnWinchStateChanged?.Invoke(currentState);
        }

        public void SetupPullAudio()
        {
            if (pullAudioSource == null)
            {
                Transform child = transform.Find("WinchAudioSource");
                if (child != null)
                {
                    pullAudioSource = child.GetComponent<AudioSource>();
                }

                if (pullAudioSource == null)
                {
                    GameObject audioObj = new GameObject("WinchAudioSource");
                    audioObj.transform.SetParent(transform, false);
                    pullAudioSource = audioObj.AddComponent<AudioSource>();
                }
            }

            pullAudioSource.playOnAwake = false;
            pullAudioSource.loop = true;
            pullAudioSource.spatialBlend = 0f;
            pullAudioSource.volume = 0f;
            pullAudioSource.pitch = minPitch;

#if UNITY_EDITOR
            if (pullAudioClip == null)
            {
                pullAudioClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/motor_rope_pulling_s_#1-1792331422309.mp3");
            }
#endif

            if (pullAudioClip != null)
            {
                pullAudioSource.clip = pullAudioClip;
            }
        }

        public void UpdatePullAudio(float deltaTime = -1f)
        {
            if (pullAudioSource == null)
            {
                SetupPullAudio();
            }
            if (pullAudioSource == null) return;

            float dt = deltaTime >= 0f ? deltaTime : Time.deltaTime;

            if (pullAudioSource.clip == null && pullAudioClip != null)
            {
                pullAudioSource.clip = pullAudioClip;
            }

            bool shouldPlay = isWinching && currentState == WinchState.WinchRigged && currentCoffin != null && pullAudioClip != null;

            if (shouldPlay)
            {
                if (!isPullAudioPlaying)
                {
                    isPullAudioPlaying = true;
                    if (!pullAudioSource.isPlaying)
                    {
                        pullAudioSource.Play();
                    }
                }

                pullAudioSource.volume = Mathf.MoveTowards(pullAudioSource.volume, pullAudioVolume, audioFadeSpeed * dt);

                float forceRatio = Mathf.Clamp01(currentAppliedPullForce / Mathf.Max(1f, maxPullForce));
                float targetPitch = Mathf.Lerp(minPitch, maxPitch, forceRatio);
                pullAudioSource.pitch = Mathf.MoveTowards(pullAudioSource.pitch, targetPitch, pitchSmoothSpeed * dt);
            }
            else
            {
                if (isPullAudioPlaying)
                {
                    pullAudioSource.volume = Mathf.MoveTowards(pullAudioSource.volume, 0f, audioFadeSpeed * dt);
                    if (pullAudioSource.volume <= 0.001f)
                    {
                        if (pullAudioSource.isPlaying)
                        {
                            pullAudioSource.Stop();
                        }
                        pullAudioSource.pitch = minPitch;
                        isPullAudioPlaying = false;
                    }
                }
            }
        }

        public void StopPullAudioImmediate()
        {
            isPullAudioPlaying = false;
            if (pullAudioSource != null)
            {
                if (pullAudioSource.isPlaying)
                {
                    pullAudioSource.Stop();
                }
                pullAudioSource.volume = 0f;
                pullAudioSource.pitch = minPitch;
            }
        }

        public void SetPullAudioClip(AudioClip clip)
        {
            pullAudioClip = clip;
            if (pullAudioSource != null)
            {
                pullAudioSource.clip = clip;
            }
        }

        public void SetPullAudioSource(AudioSource source)
        {
            pullAudioSource = source;
        }

        public void SetPitchRange(float min, float max)
        {
            minPitch = min;
            maxPitch = max;
        }

        public void SetTestWinchState(WinchState state, CoffinPullable coffin, bool winching, float appliedForce)
        {
            currentState = state;
            currentCoffin = coffin;
            isWinching = winching;
            currentAppliedPullForce = appliedForce;
        }

        private void OnDisable()
        {
            StopPullAudioImmediate();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (pullAudioClip == null)
            {
                pullAudioClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/motor_rope_pulling_s_#1-1792331422309.mp3");
            }
            if (minPitch < 0.1f) minPitch = 0.1f;
            if (maxPitch < minPitch) maxPitch = minPitch;
        }
#endif

        private void OnDestroy()
        {
            StopPullAudioImmediate();
            if (Instance == this) Instance = null;
        }
    }
}
