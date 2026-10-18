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

        [Header("Warning UI (uGUI Canvas)")]
        [SerializeField] private bool showHUDWarning = true;
        [SerializeField] private bool showDistanceMeter = true;
        [SerializeField] private bool showDirectionCue = true;
        [SerializeField] private bool showBoundaryVignette = true;
        [SerializeField] private string warningHeader = "DON'T FORGET THE COFFIN";
        [SerializeField] private string warningSubText = "You are wandering too far from the coffin!";
        [SerializeField] private string boundarySubText = "MAXIMUM DISTANCE REACHED — RETURN TO COFFIN";
        [SerializeField] private Canvas targetCanvas;
        [SerializeField] private CanvasGroup warningCanvasGroup;
        [SerializeField] private UnityEngine.UI.Image vignetteOverlay;
        [SerializeField] private UnityEngine.UI.Image warningBarFill;
        [SerializeField] private TextMeshProUGUI warningBadgeText;
        [SerializeField] private TextMeshProUGUI distanceReadoutText;
        [SerializeField] private TextMeshProUGUI subInstructionText;
        [SerializeField] private TextMeshProUGUI directionCueText;
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
            EnsureUIHierarchy();
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
            if (showHUDWarning)
            {
                UpdateCanvasUI();
            }
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

        private void EnsureUIHierarchy()
        {
            if (targetCanvas == null)
            {
                GameObject hudCanvasObj = GameObject.Find("MountainGameHUDCanvas");
                if (hudCanvasObj != null) targetCanvas = hudCanvasObj.GetComponent<Canvas>();

                if (targetCanvas == null)
                {
                    Canvas myCanvas = GetComponentInParent<Canvas>();
                    if (myCanvas != null) targetCanvas = myCanvas;
                }

                if (targetCanvas == null)
                {
                    targetCanvas = FindAnyObjectByType<Canvas>();
                }
            }

            if (targetCanvas == null) return;

#if UNITY_EDITOR
            Sprite vigSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/vignette_gradient.png");
            Sprite panelSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/panel_background.png");
            Sprite trackSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/gauge_track.png");
            Sprite fillSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/gauge_fill.png");
#else
            Sprite vigSprite = null;
            Sprite panelSprite = null;
            Sprite trackSprite = null;
            Sprite fillSprite = null;
#endif

            Transform existing = targetCanvas.transform.Find("DistanceWarningHUD");
            GameObject rootObj;
            if (existing != null)
            {
                rootObj = existing.gameObject;
            }
            else
            {
                rootObj = new GameObject("DistanceWarningHUD", typeof(RectTransform), typeof(CanvasGroup));
                rootObj.transform.SetParent(targetCanvas.transform, false);
            }

            warningCanvasGroup = rootObj.GetComponent<CanvasGroup>();
            if (warningCanvasGroup == null) warningCanvasGroup = rootObj.AddComponent<CanvasGroup>();
            warningCanvasGroup.alpha = 0f;
            warningCanvasGroup.blocksRaycasts = false;
            warningCanvasGroup.interactable = false;

            RectTransform rootRect = rootObj.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            // 1. Fullscreen Vignette Image
            Transform vigTrans = rootObj.transform.Find("Vignette");
            GameObject vigObj;
            if (vigTrans != null)
            {
                vigObj = vigTrans.gameObject;
            }
            else
            {
                vigObj = new GameObject("Vignette", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                vigObj.transform.SetParent(rootObj.transform, false);
            }

            RectTransform vigRect = vigObj.GetComponent<RectTransform>();
            vigRect.anchorMin = Vector2.zero;
            vigRect.anchorMax = Vector2.one;
            vigRect.offsetMin = Vector2.zero;
            vigRect.offsetMax = Vector2.zero;
            vignetteOverlay = vigObj.GetComponent<UnityEngine.UI.Image>();
            if (vigSprite != null) vignetteOverlay.sprite = vigSprite;
            vignetteOverlay.color = new Color(0.96f, 0.28f, 0.28f, 0f);
            vignetteOverlay.raycastTarget = false;

            // 2. Alert Box (Upper Center)
            Transform boxTrans = rootObj.transform.Find("AlertBox");
            GameObject boxObj;
            if (boxTrans != null)
            {
                boxObj = boxTrans.gameObject;
            }
            else
            {
                boxObj = new GameObject("AlertBox", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                boxObj.transform.SetParent(rootObj.transform, false);
            }

            RectTransform boxRect = boxObj.GetComponent<RectTransform>();
            boxRect.anchorMin = new Vector2(0.5f, 1f);
            boxRect.anchorMax = new Vector2(0.5f, 1f);
            boxRect.pivot = new Vector2(0.5f, 1f);
            boxRect.anchoredPosition = new Vector2(0f, -32f);
            boxRect.sizeDelta = new Vector2(510f, 78f);

            var boxImg = boxObj.GetComponent<UnityEngine.UI.Image>();
            if (panelSprite != null)
            {
                boxImg.sprite = panelSprite;
                boxImg.type = UnityEngine.UI.Image.Type.Sliced;
                boxImg.color = new Color(0.08f, 0.10f, 0.14f, 0.95f);
            }
            else
            {
                boxImg.color = new Color(0.06f, 0.08f, 0.11f, 0.94f);
            }
            boxImg.raycastTarget = false;

            // Badge
            Transform badgeTrans = boxObj.transform.Find("WarningBadge");
            GameObject badgeObj = badgeTrans != null ? badgeTrans.gameObject : new GameObject("WarningBadge", typeof(RectTransform), typeof(TextMeshProUGUI));
            badgeObj.transform.SetParent(boxObj.transform, false);
            RectTransform bRect = badgeObj.GetComponent<RectTransform>();
            bRect.anchorMin = new Vector2(0f, 1f);
            bRect.anchorMax = new Vector2(0f, 1f);
            bRect.pivot = new Vector2(0f, 1f);
            bRect.anchoredPosition = new Vector2(16f, -10f);
            bRect.sizeDelta = new Vector2(240f, 22f);
            warningBadgeText = badgeObj.GetComponent<TextMeshProUGUI>();
            warningBadgeText.text = "TETHER WARNING";
            warningBadgeText.fontSize = 13f;
            warningBadgeText.fontStyle = FontStyles.Bold;
            warningBadgeText.alignment = TextAlignmentOptions.Left;
            warningBadgeText.color = new Color(1f, 0.75f, 0.15f, 1f);
            warningBadgeText.raycastTarget = false;

            // Distance Text
            Transform distTrans = boxObj.transform.Find("DistanceText");
            GameObject distObj = distTrans != null ? distTrans.gameObject : new GameObject("DistanceText", typeof(RectTransform), typeof(TextMeshProUGUI));
            distObj.transform.SetParent(boxObj.transform, false);
            RectTransform dRect = distObj.GetComponent<RectTransform>();
            dRect.anchorMin = new Vector2(1f, 1f);
            dRect.anchorMax = new Vector2(1f, 1f);
            dRect.pivot = new Vector2(1f, 1f);
            dRect.anchoredPosition = new Vector2(-16f, -10f);
            dRect.sizeDelta = new Vector2(180f, 22f);
            distanceReadoutText = distObj.GetComponent<TextMeshProUGUI>();
            distanceReadoutText.text = "0.0m / 42m";
            distanceReadoutText.fontSize = 14f;
            distanceReadoutText.fontStyle = FontStyles.Bold;
            distanceReadoutText.alignment = TextAlignmentOptions.Right;
            distanceReadoutText.color = Color.white;
            distanceReadoutText.raycastTarget = false;

            // Tension Progress Bar
            Transform barTrans = boxObj.transform.Find("TensionTrack");
            GameObject barTrack = barTrans != null ? barTrans.gameObject : new GameObject("TensionTrack", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            barTrack.transform.SetParent(boxObj.transform, false);
            RectTransform btRect = barTrack.GetComponent<RectTransform>();
            btRect.anchorMin = new Vector2(0f, 1f);
            btRect.anchorMax = new Vector2(1f, 1f);
            btRect.pivot = new Vector2(0.5f, 1f);
            btRect.anchoredPosition = new Vector2(0f, -36f);
            btRect.sizeDelta = new Vector2(-32f, 6f);
            var trackImg = barTrack.GetComponent<UnityEngine.UI.Image>();
            if (trackSprite != null)
            {
                trackImg.sprite = trackSprite;
                trackImg.type = UnityEngine.UI.Image.Type.Sliced;
                trackImg.color = new Color(0.06f, 0.08f, 0.12f, 0.95f);
            }
            else
            {
                trackImg.color = new Color(0.08f, 0.10f, 0.14f, 0.9f);
            }
            trackImg.raycastTarget = false;

            Transform fillTrans = barTrack.transform.Find("TensionFill");
            GameObject fillObj = fillTrans != null ? fillTrans.gameObject : new GameObject("TensionFill", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            fillObj.transform.SetParent(barTrack.transform, false);
            RectTransform fRect = fillObj.GetComponent<RectTransform>();
            fRect.anchorMin = Vector2.zero;
            fRect.anchorMax = Vector2.one;
            fRect.sizeDelta = Vector2.zero;
            warningBarFill = fillObj.GetComponent<UnityEngine.UI.Image>();
            if (fillSprite != null) warningBarFill.sprite = fillSprite;
            warningBarFill.type = UnityEngine.UI.Image.Type.Filled;
            warningBarFill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            warningBarFill.fillOrigin = (int)UnityEngine.UI.Image.OriginHorizontal.Left;
            warningBarFill.fillAmount = 0.5f;
            warningBarFill.color = new Color(1f, 0.75f, 0.15f, 1f);
            warningBarFill.raycastTarget = false;

            // Subtitle instruction
            Transform subTrans = boxObj.transform.Find("SubText");
            GameObject subObj = subTrans != null ? subTrans.gameObject : new GameObject("SubText", typeof(RectTransform), typeof(TextMeshProUGUI));
            subObj.transform.SetParent(boxObj.transform, false);
            RectTransform sRect = subObj.GetComponent<RectTransform>();
            sRect.anchorMin = new Vector2(0f, 0f);
            sRect.anchorMax = new Vector2(0.7f, 0f);
            sRect.pivot = new Vector2(0f, 0f);
            sRect.anchoredPosition = new Vector2(16f, 10f);
            sRect.sizeDelta = new Vector2(0f, 20f);
            subInstructionText = subObj.GetComponent<TextMeshProUGUI>();
            subInstructionText.text = warningSubText;
            subInstructionText.fontSize = 12f;
            subInstructionText.alignment = TextAlignmentOptions.Left;
            subInstructionText.color = new Color(0.85f, 0.88f, 0.92f, 1f);
            subInstructionText.raycastTarget = false;

            // Direction cue text
            Transform dirTrans = boxObj.transform.Find("DirectionCue");
            GameObject dirObj = dirTrans != null ? dirTrans.gameObject : new GameObject("DirectionCue", typeof(RectTransform), typeof(TextMeshProUGUI));
            dirObj.transform.SetParent(boxObj.transform, false);
            RectTransform dirRect = dirObj.GetComponent<RectTransform>();
            dirRect.anchorMin = new Vector2(0.7f, 0f);
            dirRect.anchorMax = new Vector2(1f, 0f);
            dirRect.pivot = new Vector2(1f, 0f);
            dirRect.anchoredPosition = new Vector2(-16f, 10f);
            dirRect.sizeDelta = new Vector2(0f, 20f);
            directionCueText = dirObj.GetComponent<TextMeshProUGUI>();
            directionCueText.text = "";
            directionCueText.fontSize = 11f;
            directionCueText.fontStyle = FontStyles.Bold;
            directionCueText.alignment = TextAlignmentOptions.Right;
            directionCueText.color = new Color(1f, 0.75f, 0.15f, 1f);
            directionCueText.raycastTarget = false;
        }

        private void UpdateCanvasUI()
        {
            if (warningCanvasGroup == null)
            {
                EnsureUIHierarchy();
                if (warningCanvasGroup == null) return;
            }

            warningCanvasGroup.alpha = bannerAlpha;
            if (bannerAlpha <= 0.005f) return;

            // Pulsation animation
            float pulseFrequency = isRestrained ? 8f : Mathf.Lerp(3f, 6.5f, Mathf.InverseLerp(warningDistance, maxDistance, currentDistance));
            float pulse = (Mathf.Sin(Time.time * pulseFrequency) + 1f) * 0.5f;

            Color themeColor;
            Color crimson = new Color(0.96f, 0.28f, 0.28f, 1f);
            Color amber = new Color(1f, 0.75f, 0.15f, 1f);

            if (isRestrained)
            {
                themeColor = Color.Lerp(crimson, new Color(1f, 0.85f, 0.85f), pulse * 0.5f);
            }
            else
            {
                float progress = Mathf.InverseLerp(warningDistance, maxDistance, currentDistance);
                themeColor = Color.Lerp(amber, crimson, progress);
            }

            if (vignetteOverlay != null && showBoundaryVignette)
            {
                float vigAlpha = isRestrained
                    ? (0.28f + pulse * 0.32f) * bannerAlpha
                    : (Mathf.InverseLerp(warningDistance, maxDistance, currentDistance) * 0.22f) * bannerAlpha;

                vignetteOverlay.color = new Color(themeColor.r, themeColor.g, themeColor.b, vigAlpha);
            }

            if (warningBadgeText != null)
            {
                warningBadgeText.text = isRestrained ? "TETHER LIMIT REACHED" : "TETHER WARNING";
                warningBadgeText.color = themeColor;
            }

            if (distanceReadoutText != null)
            {
                distanceReadoutText.text = $"{currentDistance:F1}m / {maxDistance:F0}m";
            }

            if (warningBarFill != null && showDistanceMeter)
            {
                float ratio = Mathf.Clamp01(currentDistance / maxDistance);
                warningBarFill.fillAmount = ratio;
                warningBarFill.color = themeColor;
            }

            if (subInstructionText != null)
            {
                subInstructionText.text = isRestrained ? boundarySubText : warningSubText;
                subInstructionText.color = isRestrained ? new Color(1f, 0.5f, 0.5f, 1f) : new Color(0.85f, 0.88f, 0.92f, 1f);
            }

            if (directionCueText != null && showDirectionCue && targetCoffin != null)
            {
                Camera cam = playerCamera != null ? playerCamera : Camera.main;
                string dirStr = GetCoffinDirectionString(cam, targetCoffin.transform.position);
                directionCueText.text = string.IsNullOrEmpty(dirStr) ? "" : $"COFFIN: {dirStr}";
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
