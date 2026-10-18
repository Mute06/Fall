using System;
using UnityEngine;
using TMPro;

namespace FirstPersonSystem
{
    /// <summary>
    /// Monitors player distance from the coffin. If the player strays too far uphill or downhill,
    /// emits an atmospheric warning ("Don't forget the coffin") and stops outward movement
    /// using the tether displacement and force physics established by the winch system.
    /// </summary>
    public class CoffinDistanceWarningSystem : MonoBehaviour
    {
        public static CoffinDistanceWarningSystem Instance { get; private set; }

        [Header("Target References")]
        [Tooltip("The coffin to track. If left null, automatically finds CoffinPullable in the scene.")]
        [SerializeField] private CoffinPullable targetCoffin;

        [Header("Distance Thresholds (Meters)")]
        [Tooltip("Distance from the coffin at which the 'Don't forget the coffin' warning appears.")]
        [SerializeField] private float warningDistance = 30f;

        [Tooltip("Maximum allowed distance from the coffin. The player is physically stopped from moving further away.")]
        [SerializeField] private float maxDistance = 42f;

        [Tooltip("Hysteresis buffer (meters) to prevent rapid flickering at the warning threshold.")]
        [SerializeField] private float warningHysteresis = 1.0f;

        [Tooltip("If true, distance calculations only consider the horizontal (XZ) plane.")]
        [SerializeField] private bool horizontalDistanceOnly = false;

        [Header("Tether Physics (Matching WinchSystem)")]
        [Tooltip("Spring stiffness pulling player back when exceeding maxDistance.")]
        [SerializeField] private float pullBackStiffness = 200f;

        [Tooltip("Constant base force applied back toward the coffin when exceeding maxDistance.")]
        [SerializeField] private float basePullForce = 15f;

        [Tooltip("Maximum safety clamp on the applied pull force to prevent erratic catapulting.")]
        [SerializeField] private float maxPullForce = 60f;

        [Header("Warning UI (HUD)")]
        [SerializeField] private bool showHUDWarning = true;
        [SerializeField] private bool showDistanceMeter = true;
        [SerializeField] private bool showDirectionCue = true;
        [SerializeField] private bool showBoundaryVignette = true;
        [SerializeField] private string warningHeader = "DON'T FORGET THE COFFIN";
        [SerializeField] private string warningSubText = "You are wandering too far from the coffin!";
        [SerializeField] private string boundarySubText = "MAXIMUM DISTANCE REACHED — RETURN TO COFFIN";
        [SerializeField] private TextMeshProUGUI customCanvasText;

        [Header("Audio Feedback")]
        [SerializeField] private bool enableAudio = true;
        [SerializeField] private AudioClip customWarningClip;
        [SerializeField] private AudioClip customBoundaryClip;
        [SerializeField] private float warningAudioInterval = 2.4f;

        // Dependencies
        private FirstPersonController controller;
        private Camera playerCamera;
        private AudioSource audioSource;

        // Runtime State
        private float currentDistance = 0f;
        private bool isWarningActive = false;
        private bool isRestrained = false;
        private float bannerAlpha = 0f;
        private float audioTimer = 0f;
        private AudioClip generatedWarningClip;
        private AudioClip generatedBoundaryClip;

        // Public Properties & Events
        public float CurrentDistance => currentDistance;
        public float WarningDistance => warningDistance;
        public float MaxDistance => maxDistance;
        public bool IsWarningActive => isWarningActive;
        public bool IsRestrained => isRestrained;
        public CoffinPullable TargetCoffin => targetCoffin;

        public event Action<bool> OnWarningStateChanged;
        public event Action OnMaxDistanceHit;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            controller = GetComponent<FirstPersonController>();
            if (controller == null)
            {
                controller = FindFirstObjectByType<FirstPersonController>();
            }

            playerCamera = GetComponentInChildren<Camera>();
            if (playerCamera == null && Camera.main != null)
            {
                playerCamera = Camera.main;
            }

            SetupAudio();
        }

        private void OnValidate()
        {
            if (warningDistance < 2f) warningDistance = 2f;
            if (maxDistance <= warningDistance + 2f) maxDistance = warningDistance + 5f;
            if (warningHysteresis < 0.2f) warningHysteresis = 0.2f;
        }

        private void Start()
        {
            LocateCoffinIfMissing();
            InitStyles();
        }

        private void LocateCoffinIfMissing()
        {
            if (targetCoffin == null)
            {
                targetCoffin = CoffinPullable.Instance != null
                    ? CoffinPullable.Instance
                    : FindFirstObjectByType<CoffinPullable>();
            }
        }

        private void SetupAudio()
        {
            if (!enableAudio) return;

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f; // 2D HUD UI audio
                audioSource.volume = 0.65f;
            }

            // Generate crisp retro audio bleeps via LeanAudio if no external audio clip is assigned
            if (customWarningClip == null)
            {
                try
                {
                    AnimationCurve volCurve = new AnimationCurve(
                        new Keyframe(0f, 0.45f),
                        new Keyframe(0.08f, 0.35f),
                        new Keyframe(0.18f, 0f));
                    AnimationCurve freqCurve = new AnimationCurve(
                        new Keyframe(0f, 0.0018f),
                        new Keyframe(0.18f, 0.0028f));
                    generatedWarningClip = LeanAudio.createAudio(volCurve, freqCurve);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[CoffinDistanceWarningSystem] Procedural audio setup skipped: {ex.Message}");
                }
            }

            if (customBoundaryClip == null)
            {
                try
                {
                    AnimationCurve volCurve = new AnimationCurve(
                        new Keyframe(0f, 0.7f),
                        new Keyframe(0.1f, 0.5f),
                        new Keyframe(0.25f, 0f));
                    AnimationCurve freqCurve = new AnimationCurve(
                        new Keyframe(0f, 0.004f),
                        new Keyframe(0.25f, 0.006f));
                    generatedBoundaryClip = LeanAudio.createAudio(volCurve, freqCurve);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[CoffinDistanceWarningSystem] Procedural boundary audio setup skipped: {ex.Message}");
                }
            }
        }

        public void SetReferences(FirstPersonController playerController, CoffinPullable coffin)
        {
            controller = playerController;
            targetCoffin = coffin;
        }

        public void SetDistances(float warning, float max)
        {
            warningDistance = warning;
            maxDistance = max;
        }

        private void LateUpdate()
        {
            EvaluateDistanceState(Time.deltaTime);
        }

        public void EvaluateDistanceState(float dt)
        {
            if (targetCoffin == null)
            {
                LocateCoffinIfMissing();
                if (targetCoffin == null) return;
            }

            if (controller == null)
            {
                controller = GetComponent<FirstPersonController>();
                if (controller == null) return;
            }

            Vector3 coffinPos = targetCoffin.transform.position;
            Vector3 playerPos = controller.transform.position;

            Vector3 toCoffin = coffinPos - playerPos;
            if (horizontalDistanceOnly)
            {
                toCoffin.y = 0f;
            }

            currentDistance = toCoffin.magnitude;
            if (currentDistance <= 0.001f) return;

            Vector3 pullDir = toCoffin / currentDistance;

            // 1. Warning State Assessment (with hysteresis)
            bool shouldWarn = isWarningActive
                ? (currentDistance >= (warningDistance - warningHysteresis))
                : (currentDistance >= warningDistance);

            if (shouldWarn != isWarningActive)
            {
                isWarningActive = shouldWarn;
                OnWarningStateChanged?.Invoke(isWarningActive);
            }

            // Smooth fade banner opacity
            float targetAlpha = isWarningActive ? 1.0f : 0.0f;
            bannerAlpha = Mathf.MoveTowards(bannerAlpha, targetAlpha, dt * 3.5f);

            // 2. Physical Restraint (Tether boundary stopping logic matching WinchSystem)
            if (currentDistance >= maxDistance)
            {
                bool wasRestrained = isRestrained;
                isRestrained = true;

                float overshoot = currentDistance - maxDistance;
                if (overshoot > 0f)
                {
                    // Instant positional correction back to the max distance radius
                    Vector3 displacement = pullDir * Mathf.Clamp(overshoot, 0f, 4f);
                    controller.ApplyExternalDisplacement(displacement);

                    // Elastic pull force counteracting outward momentum
                    float pullForce = Mathf.Clamp(overshoot * pullBackStiffness + basePullForce, 0f, maxPullForce);
                    controller.AddExternalForce(pullDir * pullForce);
                }

                if (!wasRestrained)
                {
                    OnMaxDistanceHit?.Invoke();
                    PlayBoundarySound();
                }
            }
            else
            {
                isRestrained = false;
            }

            // 3. Periodic Warning Audio
            if (isWarningActive && enableAudio)
            {
                audioTimer -= dt;
                if (audioTimer <= 0f)
                {
                    PlayWarningSound();
                    float distanceRatio = Mathf.InverseLerp(warningDistance, maxDistance, currentDistance);
                    float dynamicInterval = Mathf.Lerp(warningAudioInterval, 0.75f, distanceRatio);
                    audioTimer = dynamicInterval;
                }
            }
            else
            {
                audioTimer = 0f;
            }

            // 4. Update optional Canvas TextMeshPro if assigned
            if (customCanvasText != null)
            {
                if (isWarningActive)
                {
                    string statusMsg = isRestrained ? boundarySubText : warningSubText;
                    customCanvasText.text = $"<b>{warningHeader}</b>\n{statusMsg}\nDistance: {currentDistance:F1}m / {maxDistance:F0}m";
                    customCanvasText.enabled = true;
                }
                else
                {
                    customCanvasText.enabled = false;
                }
            }
        }

        private void PlayWarningSound()
        {
            if (audioSource == null) return;
            AudioClip clip = customWarningClip != null ? customWarningClip : generatedWarningClip;
            if (clip != null)
            {
                audioSource.PlayOneShot(clip, 0.7f);
            }
        }

        private void PlayBoundarySound()
        {
            if (audioSource == null) return;
            AudioClip clip = customBoundaryClip != null ? customBoundaryClip : generatedBoundaryClip;
            if (clip != null)
            {
                audioSource.PlayOneShot(clip, 0.95f);
            }
        }

        private void InitStyles()
        {
            MinimalIMGUI.EnsureInitialized();
        }

        private void OnGUI()
        {
            if (!showHUDWarning || bannerAlpha <= 0.001f || Event.current.type != EventType.Repaint) return;

            MinimalIMGUI.EnsureInitialized();

            float screenW = Screen.width;
            float screenH = Screen.height;

            // Compute pulsation animation
            float pulseFrequency = isRestrained ? 8f : Mathf.Lerp(3f, 6.5f, Mathf.InverseLerp(warningDistance, maxDistance, currentDistance));
            float pulse = (Mathf.Sin(Time.time * pulseFrequency) + 1f) * 0.5f;

            // Determine theme color
            Color themeColor;
            if (isRestrained)
            {
                // Pulsing aggressive red
                themeColor = Color.Lerp(MinimalIMGUI.ColorCrimson, new Color(1f, 0.85f, 0.85f), pulse * 0.5f);
            }
            else
            {
                // Amber warning blending towards orange-red as distance approaches maxDistance
                float progress = Mathf.InverseLerp(warningDistance, maxDistance, currentDistance);
                themeColor = Color.Lerp(MinimalIMGUI.ColorAmber, MinimalIMGUI.ColorCrimson, progress);
            }

            // 1. Atmospheric Boundary Screen Vignette when restrained or near boundary
            if (showBoundaryVignette)
            {
                if (isRestrained)
                {
                    float vignetteAlpha = (0.28f + pulse * 0.32f) * bannerAlpha;
                    MinimalIMGUI.DrawVignette(MinimalIMGUI.ColorCrimson, vignetteAlpha);
                }
                else
                {
                    float progress = Mathf.InverseLerp(warningDistance, maxDistance, currentDistance);
                    float vignetteAlpha = (progress * 0.22f) * bannerAlpha;
                    MinimalIMGUI.DrawVignette(MinimalIMGUI.ColorAmber, vignetteAlpha);
                }
            }

            // 2. Main Alert Notification Box (Upper-Center)
            float boxWidth = 510f;
            float boxHeight = 78f;
            float boxX = (screenW - boxWidth) * 0.5f;
            // Smoothly slide down slightly as alpha increases
            float boxY = Mathf.Lerp(16f, 32f, bannerAlpha);
            Rect boxRect = new Rect(boxX, boxY, boxWidth, boxHeight);

            // Draw Background Panel with 9-slice rounded borders and top accent
            Color currentBgColor = new Color(
                MinimalIMGUI.ColorDarkBg.r,
                MinimalIMGUI.ColorDarkBg.g,
                MinimalIMGUI.ColorDarkBg.b,
                0.93f * bannerAlpha
            );
            Color accentColor = new Color(themeColor.r, themeColor.g, themeColor.b, bannerAlpha);
            MinimalIMGUI.DrawPanel(boxRect, currentBgColor, accentColor, 2.5f);

            // Coffin relative direction cue
            string directionStr = "";
            if (showDirectionCue && targetCoffin != null)
            {
                Camera cam = playerCamera != null ? playerCamera : Camera.main;
                directionStr = GetCoffinDirectionString(cam, targetCoffin.transform.position);
            }

            // Row 1: Left Warning Badge | Right Distance Readout
            float row1Y = boxY + 11f;
            Rect badgeRect = new Rect(boxX + 14f, row1Y, isRestrained ? 200f : 170f, 22f);
            string badgeText = isRestrained ? "TETHER LIMIT REACHED" : "TETHER WARNING";
            MinimalIMGUI.DrawStatusBadge(badgeRect, badgeText, themeColor, bannerAlpha);

            // Right Distance Readout
            Rect distValRect = new Rect(boxX + boxWidth - 170f, row1Y, 156f, 22f);
            MinimalIMGUI.ValueStyle.fontSize = 14;
            MinimalIMGUI.ValueStyle.normal.textColor = new Color(0.95f, 0.95f, 0.95f, bannerAlpha);
            GUI.Label(distValRect, $"{currentDistance:F1}m / {maxDistance:F0}m", MinimalIMGUI.ValueStyle);

            // Row 2: Precision Tether Tension Meter
            float row2Y = boxY + 38f;
            if (showDistanceMeter)
            {
                float meterW = boxWidth - 28f;
                Rect meterRect = new Rect(boxX + 14f, row2Y, meterW, 6f);
                float tensionRatio = Mathf.Clamp01(currentDistance / maxDistance);
                MinimalIMGUI.DrawProgressBar(meterRect, tensionRatio, themeColor, new Color(0.08f, 0.10f, 0.14f, 0.9f), bannerAlpha);
            }

            // Row 3: Subtitle Instruction (Left) | Direction Badge (Right)
            float row3Y = boxY + 49f;
            string subText = isRestrained ? boundarySubText : warningSubText;
            Color subTextColor = isRestrained
                ? new Color(1f, 0.45f, 0.45f, bannerAlpha)
                : new Color(0.85f, 0.88f, 0.92f, bannerAlpha);

            MinimalIMGUI.SubHeaderStyle.normal.textColor = subTextColor;
            Rect subRect = new Rect(boxX + 14f, row3Y, boxWidth - (string.IsNullOrEmpty(directionStr) ? 28f : 170f), 20f);
            GUI.Label(subRect, subText, MinimalIMGUI.SubHeaderStyle);

            if (!string.IsNullOrEmpty(directionStr))
            {
                Rect dirRect = new Rect(boxX + boxWidth - 160f, row3Y, 146f, 20f);
                MinimalIMGUI.ValueStyle.fontSize = 11;
                MinimalIMGUI.ValueStyle.normal.textColor = new Color(MinimalIMGUI.ColorAmber.r, MinimalIMGUI.ColorAmber.g, MinimalIMGUI.ColorAmber.b, bannerAlpha);
                GUI.Label(dirRect, $"COFFIN: {directionStr}", MinimalIMGUI.ValueStyle);
            }
        }

        private string GetCoffinDirectionString(Camera cam, Vector3 coffinPos)
        {
            if (cam == null) return "";

            Vector3 camPos = cam.transform.position;
            Vector3 toCoffin = coffinPos - camPos;
            toCoffin.y = 0f;

            if (toCoffin.sqrMagnitude < 0.01f) return "At your feet";

            Vector3 forward = cam.transform.forward;
            forward.y = 0f;
            forward.Normalize();
            toCoffin.Normalize();

            float angle = Vector3.SignedAngle(forward, toCoffin, Vector3.up);

            if (Mathf.Abs(angle) <= 25f)
                return "Ahead [↑]";
            if (angle > 25f && angle < 70f)
                return "Ahead Right [↗]";
            if (angle >= 70f && angle <= 110f)
                return "Right [→]";
            if (angle > 110f && angle < 155f)
                return "Behind Right [↘]";
            if (Mathf.Abs(angle) >= 155f)
                return "Behind [↓]";
            if (angle < -25f && angle > -70f)
                return "Ahead Left [↖]";
            if (angle <= -70f && angle >= -110f)
                return "Left [←]";
            if (angle < -110f && angle > -155f)
                return "Behind Left [↙]";

            return "Behind [↓]";
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 origin = targetCoffin != null ? targetCoffin.transform.position : transform.position;

            Gizmos.color = new Color(1f, 0.8f, 0f, 0.4f);
            Gizmos.DrawWireSphere(origin, warningDistance);

            Gizmos.color = new Color(1f, 0.15f, 0.15f, 0.6f);
            Gizmos.DrawWireSphere(origin, maxDistance);

            if (targetCoffin != null)
            {
                Gizmos.color = isRestrained ? Color.red : (isWarningActive ? Color.yellow : Color.green);
                Gizmos.DrawLine(transform.position, targetCoffin.transform.position);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
