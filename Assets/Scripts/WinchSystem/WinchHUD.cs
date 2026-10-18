using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FirstPersonSystem
{
    /// <summary>
    /// Modern uGUI HUD for the winch and tethering system.
    /// Uses UI sprite textures (9-sliced panels, keycaps, status pills, gauge tracks, and icons),
    /// positioned above subtitles (bottomOffset = 210f) and fully responsive to player actions.
    /// </summary>
    public class WinchHUD : MonoBehaviour
    {
        [Header("UI Canvas References")]
        [Tooltip("Root Canvas. If null, automatically locates the scene Canvas.")]
        [SerializeField] private Canvas targetCanvas;

        [Tooltip("Root CanvasGroup controlling winch HUD alpha fading.")]
        [SerializeField] private CanvasGroup rootCanvasGroup;

        [Header("UI Sprites (from Assets/Textures/UI/)")]
        [SerializeField] private Sprite panelBgSprite;
        [SerializeField] private Sprite keycapSprite;
        [SerializeField] private Sprite statusPillSprite;
        [SerializeField] private Sprite gaugeTrackSprite;
        [SerializeField] private Sprite gaugeFillSprite;
        [SerializeField] private Sprite iconAnchorSprite;
        [SerializeField] private Sprite iconBatterySprite;
        [SerializeField] private Sprite iconWarningSprite;

        [Header("State Containers")]
        [SerializeField] private GameObject idleContainer;
        [SerializeField] private GameObject carryingRopeContainer;
        [SerializeField] private GameObject winchRiggedContainer;

        [Header("Idle Elements")]
        [SerializeField] private TextMeshProUGUI idleKeycapText;
        [SerializeField] private TextMeshProUGUI idleActionText;

        [Header("Carrying Rope Elements")]
        [SerializeField] private Image carryingStatusPill;
        [SerializeField] private TextMeshProUGUI carryingStatusText;
        [SerializeField] private Image cableFillBar;
        [SerializeField] private TextMeshProUGUI cableLengthText;
        [SerializeField] private TextMeshProUGUI carryingLmbActionText;

        [Header("Winch Rigged Elements")]
        [SerializeField] private Image motorStatusPill;
        [SerializeField] private TextMeshProUGUI motorStatusBadgeText;
        [SerializeField] private Image motorStatusDot;
        [SerializeField] private Image batteryFillBar;
        [SerializeField] private TextMeshProUGUI batteryWhText;
        [SerializeField] private TextMeshProUGUI riggedLmbActionText;
        [SerializeField] private TextMeshProUGUI riggedCableText;

        [Header("Display Settings (Reference 1080p)")]
        [SerializeField] private bool showHUD = true;
        [Tooltip("Positioned above subtitles (Y=60..150) so they never overlap.")]
        [SerializeField] private float bottomOffset = 210f;

        // References
        private WinchSystem winch;
        private WinchEnergySystem energy;
        private Interactor interactor;

        // Animation smoothers
        private float displayedEnergy = 1f;
        private float promptAlpha = 0f;

        private static readonly Color ColorDarkBg = new Color(0.08f, 0.10f, 0.14f, 0.94f);
        private static readonly Color ColorKeycapBg = new Color(0.14f, 0.17f, 0.23f, 0.98f);
        private static readonly Color ColorKeycapAccent = new Color(1f, 0.74f, 0.22f, 1f); // Warm gold/amber
        private static readonly Color ColorCyan = new Color(0.22f, 0.74f, 0.97f, 1f);      // Winch sky blue
        private static readonly Color ColorEmerald = new Color(0.20f, 0.85f, 0.55f, 1f);   // Active motor green
        private static readonly Color ColorAmber = new Color(1f, 0.75f, 0.15f, 1f);       // Warning / brake gold
        private static readonly Color ColorCrimson = new Color(0.96f, 0.28f, 0.28f, 1f);   // Restraint red

        private void Awake()
        {
            LoadSpritesIfMissing();
            EnsureUIHierarchy();
        }

        private void Start()
        {
            winch = WinchSystem.Instance;
            energy = GetComponent<WinchEnergySystem>();
            if (energy == null && winch != null)
            {
                energy = winch.GetComponent<WinchEnergySystem>();
            }

            interactor = GetComponent<Interactor>();
            if (interactor == null && winch != null)
            {
                interactor = winch.GetComponent<Interactor>();
            }

            LoadSpritesIfMissing();
            EnsureUIHierarchy();
        }

        private void LoadSpritesIfMissing()
        {
#if UNITY_EDITOR
            if (panelBgSprite == null) panelBgSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/panel_background.png");
            if (keycapSprite == null) keycapSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/keycap_badge.png");
            if (statusPillSprite == null) statusPillSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/status_pill.png");
            if (gaugeTrackSprite == null) gaugeTrackSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/gauge_track.png");
            if (gaugeFillSprite == null) gaugeFillSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/gauge_fill.png");
            if (iconAnchorSprite == null) iconAnchorSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_anchor.png");
            if (iconBatterySprite == null) iconBatterySprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_battery.png");
            if (iconWarningSprite == null) iconWarningSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_warning.png");
#endif
        }

        private void Update()
        {
            if (winch == null) winch = WinchSystem.Instance;
            if (winch == null) return;
            if (energy == null) energy = winch.GetComponent<WinchEnergySystem>();
            if (interactor == null) interactor = winch.GetComponent<Interactor>();

            float dt = Time.deltaTime;

            // Smooth energy lerp for seamless meter animation
            float targetEnergy = energy != null ? energy.EnergyRatio : 1f;
            displayedEnergy = Mathf.MoveTowards(displayedEnergy, targetEnergy, dt * 2.5f);

            // Calculate target alpha for HUD deck
            float targetAlpha = 0f;
            if (showHUD)
            {
                switch (winch.CurrentState)
                {
                    case WinchSystem.WinchState.Idle:
                        targetAlpha = winch.IsAimingAtAnchorable ? 1f : 0f;
                        break;
                    case WinchSystem.WinchState.CarryingRope:
                    case WinchSystem.WinchState.WinchRigged:
                        targetAlpha = 1f;
                        break;
                }
            }

            promptAlpha = Mathf.MoveTowards(promptAlpha, targetAlpha, dt * 6f);

            if (rootCanvasGroup != null)
            {
                rootCanvasGroup.alpha = promptAlpha;
            }

            if (promptAlpha <= 0.005f) return;

            // Update contextual state panels
            UpdateStatePanels();
        }

        private void UpdateStatePanels()
        {
            WinchSystem.WinchState state = winch.CurrentState;

            if (idleContainer != null) idleContainer.SetActive(state == WinchSystem.WinchState.Idle);
            if (carryingRopeContainer != null) carryingRopeContainer.SetActive(state == WinchSystem.WinchState.CarryingRope);
            if (winchRiggedContainer != null) winchRiggedContainer.SetActive(state == WinchSystem.WinchState.WinchRigged);

            // 1. Idle State: Aiming at Anchorable Tree
            if (state == WinchSystem.WinchState.Idle)
            {
                if (idleActionText != null)
                {
                    if (winch.LastAnchorHit.collider != null)
                    {
                        float dist = winch.LastAnchorHit.distance;
                        idleActionText.text = $"DRIVE ANCHOR INTO TREE ({dist:F1}m)";
                    }
                    else
                    {
                        idleActionText.text = "DRIVE ANCHOR INTO TREE";
                    }
                }
            }
            // 2. Carrying Rope State
            else if (state == WinchSystem.WinchState.CarryingRope)
            {
                float currentLength = winch.CurrentRopeLength;
                float maxLength = winch.MaxRopeLength;
                float ratio = maxLength > 0f ? Mathf.Clamp01(currentLength / maxLength) : 0f;

                if (cableLengthText != null)
                {
                    cableLengthText.text = $"{currentLength:F1}m / {maxLength:F0}m";
                }

                bool isStrained = ratio > 0.85f;
                if (cableFillBar != null)
                {
                    cableFillBar.fillAmount = ratio;
                    cableFillBar.color = isStrained ? ColorCrimson : ColorCyan;
                }

                if (carryingStatusText != null)
                {
                    carryingStatusText.text = isStrained ? "CABLE LIMIT" : "CABLE ACTIVE";
                    carryingStatusText.color = isStrained ? ColorCrimson : ColorCyan;
                }

                if (carryingStatusPill != null)
                {
                    carryingStatusPill.color = isStrained ? new Color(ColorCrimson.r, ColorCrimson.g, ColorCrimson.b, 0.45f) : new Color(ColorCyan.r, ColorCyan.g, ColorCyan.b, 0.35f);
                }

                // Check distance and line of sight to Coffin
                CoffinPullable coffin = CoffinPullable.Instance;
                float distToCoffin = 0f;
                bool isAimingCoffin = false;

                if (coffin != null)
                {
                    Camera cam = Camera.main;
                    Vector3 camPos = cam != null ? cam.transform.position : transform.position;
                    distToCoffin = Vector3.Distance(camPos, coffin.transform.position);

                    if (interactor != null && interactor.CurrentInteractable is CoffinPullable)
                    {
                        isAimingCoffin = true;
                    }
                    else if (distToCoffin <= 4.5f)
                    {
                        isAimingCoffin = true;
                    }
                }

                if (carryingLmbActionText != null)
                {
                    if (isAimingCoffin)
                    {
                        carryingLmbActionText.text = "ATTACH TO COFFIN";
                        carryingLmbActionText.color = ColorEmerald;
                    }
                    else if (coffin != null)
                    {
                        carryingLmbActionText.text = $"CARRY TO COFFIN ({distToCoffin:F1}m)";
                        carryingLmbActionText.color = new Color(0.92f, 0.94f, 0.97f, 1f);
                    }
                    else
                    {
                        carryingLmbActionText.text = "ATTACH TO COFFIN";
                        carryingLmbActionText.color = new Color(0.92f, 0.94f, 0.97f, 1f);
                    }
                }
            }
            // 3. Winch Rigged State
            else if (state == WinchSystem.WinchState.WinchRigged)
            {
                bool isReeling = winch.IsWinching;
                bool hasBattery = energy == null || energy.CurrentEnergy > 0.05f;

                if (motorStatusBadgeText != null)
                {
                    if (!hasBattery)
                    {
                        motorStatusBadgeText.text = "BATTERY DEPLETED";
                        motorStatusBadgeText.color = ColorCrimson;
                    }
                    else if (isReeling)
                    {
                        motorStatusBadgeText.text = "REELING ACTIVE";
                        motorStatusBadgeText.color = ColorEmerald;
                    }
                    else
                    {
                        motorStatusBadgeText.text = "BRAKE LOCKED";
                        motorStatusBadgeText.color = ColorAmber;
                    }
                }

                if (motorStatusDot != null)
                {
                    float pulse = (Mathf.Sin(Time.time * 8f) + 1f) * 0.5f;
                    Color dotCol = !hasBattery ? ColorCrimson : (isReeling ? Color.Lerp(ColorEmerald, Color.white, pulse * 0.45f) : ColorAmber);
                    motorStatusDot.color = dotCol;
                }

                if (riggedCableText != null)
                {
                    riggedCableText.text = $"{winch.CurrentRopeLength:F1}m";
                }

                if (riggedLmbActionText != null)
                {
                    if (!hasBattery)
                    {
                        riggedLmbActionText.text = "NO POWER";
                        riggedLmbActionText.color = ColorCrimson;
                    }
                    else if (isReeling)
                    {
                        riggedLmbActionText.text = "PULLING COFFIN...";
                        riggedLmbActionText.color = ColorEmerald;
                    }
                    else
                    {
                        riggedLmbActionText.text = "REEL WINCH";
                        riggedLmbActionText.color = new Color(0.92f, 0.94f, 0.97f, 1f);
                    }
                }

                if (energy != null)
                {
                    if (batteryFillBar != null)
                    {
                        batteryFillBar.fillAmount = displayedEnergy;
                        if (displayedEnergy > 0.35f)
                            batteryFillBar.color = ColorCyan;
                        else if (displayedEnergy > 0.15f)
                            batteryFillBar.color = ColorAmber;
                        else
                        {
                            float flash = (Mathf.Sin(Time.time * 10f) + 1f) * 0.5f;
                            batteryFillBar.color = Color.Lerp(ColorCrimson, Color.white, flash * 0.5f);
                        }
                    }

                    if (batteryWhText != null)
                    {
                        batteryWhText.text = $"{energy.CurrentEnergy:F0} Wh";
                    }
                }
            }
        }

        /// <summary>
        /// Automatically discovers or constructs the complete uGUI winch hierarchy under the Canvas.
        /// </summary>
        public void EnsureUIHierarchy()
        {
            if (targetCanvas == null)
            {
                // Prefer dedicated MountainGameHUDCanvas
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

            LoadSpritesIfMissing();

            // 1. Root Winch HUD Container under Canvas
            Transform existing = targetCanvas.transform.Find("WinchHUD");
            GameObject rootObj;
            if (existing != null)
            {
                rootObj = existing.gameObject;
            }
            else
            {
                rootObj = new GameObject("WinchHUD", typeof(RectTransform), typeof(CanvasGroup));
                rootObj.transform.SetParent(targetCanvas.transform, false);
            }

            rootCanvasGroup = rootObj.GetComponent<CanvasGroup>();
            if (rootCanvasGroup == null) rootCanvasGroup = rootObj.AddComponent<CanvasGroup>();
            rootCanvasGroup.alpha = 0f;
            rootCanvasGroup.blocksRaycasts = false;
            rootCanvasGroup.interactable = false;

            RectTransform rootRect = rootObj.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0f);
            rootRect.anchorMax = new Vector2(0.5f, 0f);
            rootRect.pivot = new Vector2(0.5f, 0f);
            rootRect.anchoredPosition = new Vector2(0f, bottomOffset);
            rootRect.sizeDelta = new Vector2(560f, 88f);

            // 2. Idle State Container
            idleContainer = EnsureChildPanel(rootObj.transform, "IdlePanel", new Vector2(340f, 52f), (panel) =>
            {
                // Anchor Icon
                CreateIcon(panel.transform, new Vector2(-130f, 0f), new Vector2(24f, 24f), iconAnchorSprite, ColorKeycapAccent);

                // Keycap Prompt: [LMB] DRIVE ANCHOR INTO TREE
                CreateKeycapPrompt(panel.transform, new Vector2(-20f, 0f), "LMB", "DRIVE ANCHOR INTO TREE", out idleKeycapText, out idleActionText);
            });

            // 3. Carrying Rope State Container
            carryingRopeContainer = EnsureChildPanel(rootObj.transform, "CarryingRopePanel", new Vector2(540f, 82f), (panel) =>
            {
                // Row 1: Status Pill Badge + Cable Bar + Numeric Readout
                CreateStatusPill(panel.transform, new Vector2(-170f, 16f), new Vector2(130f, 24f), "CABLE ACTIVE", ColorCyan, statusPillSprite, out carryingStatusPill, out carryingStatusText);

                cableFillBar = CreateProgressBar(panel.transform, new Vector2(10f, 16f), new Vector2(160f, 10f), gaugeTrackSprite, gaugeFillSprite);

                cableLengthText = CreateText(panel.transform, new Vector2(185f, 16f), new Vector2(120f, 22f), "0.0m / 50m", 13f, Color.white, TextAlignmentOptions.Right);

                // Row 2: Contextual Actions
                CreateKeycapPrompt(panel.transform, new Vector2(-80f, -18f), "LMB", "ATTACH TO COFFIN", out _, out carryingLmbActionText, ColorEmerald);
                CreateKeycapPrompt(panel.transform, new Vector2(150f, -18f), "Q", "CANCEL CABLE", out _, out _, new Color(1f, 0.45f, 0.45f, 1f));
            });

            // 4. Winch Rigged State Container
            winchRiggedContainer = EnsureChildPanel(rootObj.transform, "WinchRiggedPanel", new Vector2(560f, 88f), (panel) =>
            {
                // Row 1: Status Pill Badge with Dot
                CreateStatusPillWithDot(panel.transform, new Vector2(-185f, 18f), new Vector2(146f, 26f), "REELING ACTIVE", ColorEmerald, statusPillSprite, out motorStatusPill, out motorStatusBadgeText, out motorStatusDot);

                // Cable length readout
                riggedCableText = CreateText(panel.transform, new Vector2(-65f, 18f), new Vector2(65f, 22f), "24.5m", 12f, ColorCyan, TextAlignmentOptions.Center);

                // Battery Icon
                CreateIcon(panel.transform, new Vector2(-15f, 18f), new Vector2(18f, 18f), iconBatterySprite, ColorKeycapAccent);

                // Battery Bar
                batteryFillBar = CreateProgressBar(panel.transform, new Vector2(75f, 18f), new Vector2(130f, 12f), gaugeTrackSprite, gaugeFillSprite);

                // Battery Numeric Readout
                batteryWhText = CreateText(panel.transform, new Vector2(195f, 18f), new Vector2(85f, 22f), "100 Wh", 13f, Color.white, TextAlignmentOptions.Right);

                // Row 2: Rigged Keycap Actions
                CreateKeycapPrompt(panel.transform, new Vector2(-95f, -18f), "HOLD LMB", "REEL WINCH", out _, out riggedLmbActionText);
                CreateKeycapPrompt(panel.transform, new Vector2(130f, -18f), "Q", "DETACH CABLE", out _, out _, new Color(1f, 0.45f, 0.45f, 1f));
            });
        }

        private GameObject EnsureChildPanel(Transform parent, string name, Vector2 size, Action<GameObject> buildCallback)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing.gameObject;

            GameObject panelObj = new GameObject(name, typeof(RectTransform), typeof(Image));
            panelObj.transform.SetParent(parent, false);

            RectTransform rect = panelObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;

            Image bg = panelObj.GetComponent<Image>();
            if (panelBgSprite != null)
            {
                bg.sprite = panelBgSprite;
                bg.type = Image.Type.Sliced;
                bg.color = ColorDarkBg;
            }
            else
            {
                bg.color = ColorDarkBg;
            }
            bg.raycastTarget = false;

            buildCallback?.Invoke(panelObj);
            return panelObj;
        }

        private void CreateIcon(Transform parent, Vector2 pos, Vector2 size, Sprite sprite, Color tint)
        {
            if (sprite == null) return;
            GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObj.transform.SetParent(parent, false);

            RectTransform r = iconObj.GetComponent<RectTransform>();
            r.anchoredPosition = pos;
            r.sizeDelta = size;

            Image img = iconObj.GetComponent<Image>();
            img.sprite = sprite;
            img.color = tint;
            img.raycastTarget = false;
        }

        private void CreateKeycapPrompt(Transform parent, Vector2 anchoredPos, string key, string actionText, out TextMeshProUGUI keyOut, out TextMeshProUGUI actionOut, Color? customAccent = null)
        {
            Color accent = customAccent ?? ColorKeycapAccent;
            GameObject group = new GameObject($"KeyPrompt_{key}", typeof(RectTransform));
            group.transform.SetParent(parent, false);

            RectTransform gRect = group.GetComponent<RectTransform>();
            gRect.anchoredPosition = anchoredPos;
            gRect.sizeDelta = new Vector2(200f, 26f);

            // Keycap Box (uses keycap_badge.png 9-sliced)
            GameObject keyBox = new GameObject("Keycap", typeof(RectTransform), typeof(Image));
            keyBox.transform.SetParent(group.transform, false);
            RectTransform kRect = keyBox.GetComponent<RectTransform>();
            kRect.anchorMin = new Vector2(0f, 0.5f);
            kRect.anchorMax = new Vector2(0f, 0.5f);
            kRect.pivot = new Vector2(0f, 0.5f);
            float keyW = Mathf.Max(38f, key.Length * 11f + 18f);
            kRect.sizeDelta = new Vector2(keyW, 24f);

            Image kImg = keyBox.GetComponent<Image>();
            if (keycapSprite != null)
            {
                kImg.sprite = keycapSprite;
                kImg.type = Image.Type.Sliced;
                kImg.color = ColorKeycapBg;
            }
            else
            {
                kImg.color = ColorKeycapBg;
            }
            kImg.raycastTarget = false;

            // Keycap Text
            GameObject kTextObj = new GameObject("KeyText", typeof(RectTransform), typeof(TextMeshProUGUI));
            kTextObj.transform.SetParent(keyBox.transform, false);
            RectTransform ktRect = kTextObj.GetComponent<RectTransform>();
            ktRect.anchorMin = Vector2.zero;
            ktRect.anchorMax = Vector2.one;
            ktRect.sizeDelta = Vector2.zero;
            keyOut = kTextObj.GetComponent<TextMeshProUGUI>();
            keyOut.text = key;
            keyOut.fontSize = 11.5f;
            keyOut.fontStyle = FontStyles.Bold;
            keyOut.alignment = TextAlignmentOptions.Center;
            keyOut.color = accent;
            keyOut.raycastTarget = false;

            // Action Label
            GameObject actObj = new GameObject("ActionText", typeof(RectTransform), typeof(TextMeshProUGUI));
            actObj.transform.SetParent(group.transform, false);
            RectTransform aRect = actObj.GetComponent<RectTransform>();
            aRect.anchorMin = new Vector2(0f, 0.5f);
            aRect.anchorMax = new Vector2(0f, 0.5f);
            aRect.pivot = new Vector2(0f, 0.5f);
            aRect.anchoredPosition = new Vector2(keyW + 8f, 0f);
            aRect.sizeDelta = new Vector2(170f, 22f);
            actionOut = actObj.GetComponent<TextMeshProUGUI>();
            actionOut.text = actionText;
            actionOut.fontSize = 11.5f;
            actionOut.fontStyle = FontStyles.Bold;
            actionOut.alignment = TextAlignmentOptions.Left;
            actionOut.color = new Color(0.92f, 0.94f, 0.97f, 1f);
            actionOut.raycastTarget = false;
        }

        private void CreateStatusPill(Transform parent, Vector2 pos, Vector2 size, string text, Color color, Sprite pillSprite, out Image pillImg, out TextMeshProUGUI pillText)
        {
            GameObject badge = new GameObject("StatusPill", typeof(RectTransform), typeof(Image));
            badge.transform.SetParent(parent, false);
            RectTransform bRect = badge.GetComponent<RectTransform>();
            bRect.anchoredPosition = pos;
            bRect.sizeDelta = size;
            pillImg = badge.GetComponent<Image>();
            if (pillSprite != null)
            {
                pillImg.sprite = pillSprite;
                pillImg.type = Image.Type.Sliced;
            }
            pillImg.color = new Color(color.r, color.g, color.b, 0.35f);
            pillImg.raycastTarget = false;

            GameObject bText = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            bText.transform.SetParent(badge.transform, false);
            RectTransform btRect = bText.GetComponent<RectTransform>();
            btRect.anchorMin = Vector2.zero;
            btRect.anchorMax = Vector2.one;
            btRect.sizeDelta = Vector2.zero;
            pillText = bText.GetComponent<TextMeshProUGUI>();
            pillText.text = text;
            pillText.fontSize = 11f;
            pillText.fontStyle = FontStyles.Bold;
            pillText.alignment = TextAlignmentOptions.Center;
            pillText.color = color;
            pillText.raycastTarget = false;
        }

        private void CreateStatusPillWithDot(Transform parent, Vector2 pos, Vector2 size, string text, Color color, Sprite pillSprite, out Image pillImg, out TextMeshProUGUI badgeText, out Image badgeDot)
        {
            GameObject badge = new GameObject("StatusPill", typeof(RectTransform), typeof(Image));
            badge.transform.SetParent(parent, false);
            RectTransform bRect = badge.GetComponent<RectTransform>();
            bRect.anchoredPosition = pos;
            bRect.sizeDelta = size;
            pillImg = badge.GetComponent<Image>();
            if (pillSprite != null)
            {
                pillImg.sprite = pillSprite;
                pillImg.type = Image.Type.Sliced;
            }
            pillImg.color = new Color(color.r, color.g, color.b, 0.35f);
            pillImg.raycastTarget = false;

            // Dot
            GameObject dotObj = new GameObject("Dot", typeof(RectTransform), typeof(Image));
            dotObj.transform.SetParent(badge.transform, false);
            RectTransform dRect = dotObj.GetComponent<RectTransform>();
            dRect.anchorMin = new Vector2(0f, 0.5f);
            dRect.anchorMax = new Vector2(0f, 0.5f);
            dRect.pivot = new Vector2(0f, 0.5f);
            dRect.anchoredPosition = new Vector2(10f, 0f);
            dRect.sizeDelta = new Vector2(7f, 7f);
            badgeDot = dotObj.GetComponent<Image>();
            badgeDot.color = color;
            badgeDot.raycastTarget = false;

            // Text
            GameObject bText = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            bText.transform.SetParent(badge.transform, false);
            RectTransform btRect = bText.GetComponent<RectTransform>();
            btRect.anchorMin = new Vector2(0f, 0f);
            btRect.anchorMax = new Vector2(1f, 1f);
            btRect.offsetMin = new Vector2(22f, 0f);
            btRect.offsetMax = Vector2.zero;
            badgeText = bText.GetComponent<TextMeshProUGUI>();
            badgeText.text = text;
            badgeText.fontSize = 11f;
            badgeText.fontStyle = FontStyles.Bold;
            badgeText.alignment = TextAlignmentOptions.Left;
            badgeText.color = color;
            badgeText.raycastTarget = false;
        }

        private Image CreateProgressBar(Transform parent, Vector2 pos, Vector2 size, Sprite trackSprite, Sprite fillSprite)
        {
            GameObject track = new GameObject("GaugeTrack", typeof(RectTransform), typeof(Image));
            track.transform.SetParent(parent, false);
            RectTransform tRect = track.GetComponent<RectTransform>();
            tRect.anchoredPosition = pos;
            tRect.sizeDelta = size;
            Image tImg = track.GetComponent<Image>();
            if (trackSprite != null)
            {
                tImg.sprite = trackSprite;
                tImg.type = Image.Type.Sliced;
                tImg.color = new Color(0.05f, 0.07f, 0.10f, 0.95f);
            }
            else
            {
                tImg.color = new Color(0.04f, 0.05f, 0.08f, 0.95f);
            }
            tImg.raycastTarget = false;

            GameObject fill = new GameObject("GaugeFill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(track.transform, false);
            RectTransform fRect = fill.GetComponent<RectTransform>();
            fRect.anchorMin = Vector2.zero;
            fRect.anchorMax = Vector2.one;
            fRect.sizeDelta = Vector2.zero;
            Image fImg = fill.GetComponent<Image>();
            if (fillSprite != null)
            {
                fImg.sprite = fillSprite;
            }
            fImg.type = Image.Type.Filled;
            fImg.fillMethod = Image.FillMethod.Horizontal;
            fImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            fImg.fillAmount = 1f;
            fImg.color = ColorCyan;
            fImg.raycastTarget = false;

            return fImg;
        }

        private TextMeshProUGUI CreateText(Transform parent, Vector2 pos, Vector2 size, string text, float fontSize, Color color, TextAlignmentOptions align)
        {
            GameObject txtObj = new GameObject("TextLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            txtObj.transform.SetParent(parent, false);
            RectTransform r = txtObj.GetComponent<RectTransform>();
            r.anchoredPosition = pos;
            r.sizeDelta = size;

            var tm = txtObj.GetComponent<TextMeshProUGUI>();
            tm.text = text;
            tm.fontSize = fontSize;
            tm.fontStyle = FontStyles.Bold;
            tm.color = color;
            tm.alignment = align;
            tm.raycastTarget = false;
            return tm;
        }
    }
}
