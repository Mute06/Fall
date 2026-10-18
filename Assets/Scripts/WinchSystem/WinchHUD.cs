using UnityEngine;

namespace FirstPersonSystem
{
    /// <summary>
    /// Minimalist IMGUI HUD for the winch and tethering system.
    /// Presents contextual action keycaps, winch cable telemetry, and battery gauge
    /// using cached 9-slice textures and high-contrast typography.
    /// </summary>
    public class WinchHUD : MonoBehaviour
    {
        [Header("Display Settings")]
        [SerializeField] private bool showHUD = true;
        [SerializeField] private float bottomOffset = 26f;

        // References
        private WinchSystem winch;
        private WinchEnergySystem energy;

        // Animation smoothers
        private float displayedEnergy = 1f;
        private float promptAlpha = 0f;

        private void Start()
        {
            winch = WinchSystem.Instance;
            energy = GetComponent<WinchEnergySystem>();
            if (energy == null && winch != null)
            {
                energy = winch.GetComponent<WinchEnergySystem>();
            }

            MinimalIMGUI.EnsureInitialized();
        }

        private void Update()
        {
            if (winch == null) winch = WinchSystem.Instance;
            if (winch == null) return;
            if (energy == null) energy = winch.GetComponent<WinchEnergySystem>();

            float dt = Time.deltaTime;

            // Smooth energy lerp for seamless meter animation
            float targetEnergy = energy != null ? energy.EnergyRatio : 1f;
            displayedEnergy = Mathf.MoveTowards(displayedEnergy, targetEnergy, dt * 2.5f);

            // Calculate target alpha for HUD deck
            float targetAlpha = 0f;
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
            promptAlpha = Mathf.MoveTowards(promptAlpha, targetAlpha, dt * 6f);
        }

        private void OnGUI()
        {
            if (!showHUD || Event.current.type != EventType.Repaint) return;
            if (winch == null) winch = WinchSystem.Instance;
            if (winch == null) return;
            if (energy == null) energy = winch.GetComponent<WinchEnergySystem>();

            MinimalIMGUI.EnsureInitialized();

            float screenW = Screen.width;
            float screenH = Screen.height;

            switch (winch.CurrentState)
            {
                case WinchSystem.WinchState.Idle:
                    DrawIdleHUD(screenW, screenH);
                    break;
                case WinchSystem.WinchState.CarryingRope:
                    DrawCarryingRopeHUD(screenW, screenH);
                    break;
                case WinchSystem.WinchState.WinchRigged:
                    DrawWinchRiggedHUD(screenW, screenH);
                    break;
            }
        }

        /// <summary>
        /// Idle State: unobtrusive, only appears if player crosshair is on an anchorable surface.
        /// </summary>
        private void DrawIdleHUD(float screenW, float screenH)
        {
            if (promptAlpha <= 0.005f) return;

            float panelW = 270f;
            float panelH = 46f;
            float panelX = (screenW - panelW) * 0.5f;
            float panelY = screenH - bottomOffset - panelH;
            Rect panelRect = new Rect(panelX, panelY, panelW, panelH);

            // Translucent dark panel with warm gold accent
            Color bgTint = new Color(MinimalIMGUI.ColorDarkBg.r, MinimalIMGUI.ColorDarkBg.g, MinimalIMGUI.ColorDarkBg.b, 0.88f * promptAlpha);
            Color accent = new Color(MinimalIMGUI.ColorAmber.r, MinimalIMGUI.ColorAmber.g, MinimalIMGUI.ColorAmber.b, 0.85f * promptAlpha);
            MinimalIMGUI.DrawPanel(panelRect, bgTint, accent, 2f);

            // Anchor icon
            float iconSize = 20f;
            Rect iconRect = new Rect(panelX + 16f, panelY + (panelH - iconSize) * 0.5f, iconSize, iconSize);
            MinimalIMGUI.DrawIcon(iconRect, MinimalIMGUI.IconAnchor, Color.white, promptAlpha * 0.85f);

            // Keycap [ E ] & Action prompt
            float cursorX = panelX + 46f;
            float cursorY = panelY + (panelH - 22f) * 0.5f;
            MinimalIMGUI.DrawKeyPrompt(ref cursorX, cursorY, "E", "DEPLOY ANCHOR", MinimalIMGUI.ColorKeycapAccent, promptAlpha);
        }

        /// <summary>
        /// Carrying Rope State: shows deployed cable length vs capacity, plus attach/cancel controls.
        /// </summary>
        private void DrawCarryingRopeHUD(float screenW, float screenH)
        {
            if (promptAlpha <= 0.005f) return;

            float panelW = 440f;
            float panelH = 76f;
            float panelX = (screenW - panelW) * 0.5f;
            float panelY = screenH - bottomOffset - panelH;
            Rect panelRect = new Rect(panelX, panelY, panelW, panelH);

            Color bgTint = new Color(MinimalIMGUI.ColorDarkBg.r, MinimalIMGUI.ColorDarkBg.g, MinimalIMGUI.ColorDarkBg.b, 0.92f * promptAlpha);
            Color accent = new Color(MinimalIMGUI.ColorCyan.r, MinimalIMGUI.ColorCyan.g, MinimalIMGUI.ColorCyan.b, 0.9f * promptAlpha);
            MinimalIMGUI.DrawPanel(panelRect, bgTint, accent, 2f);

            // Row 1: Status badge & cable length readout
            float row1Y = panelY + 11f;
            Rect badgeRect = new Rect(panelX + 14f, row1Y, 138f, 22f);
            MinimalIMGUI.DrawStatusBadge(badgeRect, "CABLE ACTIVE", MinimalIMGUI.ColorCyan, promptAlpha);

            float currentLength = winch.CurrentRopeLength;
            float maxLength = winch.MaxRopeLength;
            string lengthText = $"{currentLength:F1}m / {maxLength:F0}m";

            Rect lengthRect = new Rect(panelX + panelW - 150f, row1Y, 136f, 22f);
            MinimalIMGUI.ValueStyle.normal.textColor = new Color(0.95f, 0.95f, 0.95f, promptAlpha);
            GUI.Label(lengthRect, lengthText, MinimalIMGUI.ValueStyle);

            // Subtle 3px rope capacity meter
            float ropeRatio = maxLength > 0f ? Mathf.Clamp01(currentLength / maxLength) : 0f;
            Rect barRect = new Rect(panelX + 160f, row1Y + 9f, panelW - 324f, 4f);
            Color ropeBarColor = ropeRatio > 0.85f ? MinimalIMGUI.ColorCrimson : MinimalIMGUI.ColorCyan;
            MinimalIMGUI.DrawProgressBar(barRect, ropeRatio, ropeBarColor, new Color(0.12f, 0.15f, 0.20f, 0.8f), promptAlpha);

            // Row 2: Action Keycaps
            float row2Y = panelY + 42f;
            float cursorX = panelX + 14f;
            MinimalIMGUI.DrawKeyPrompt(ref cursorX, row2Y, "LMB", "ATTACH COFFIN", MinimalIMGUI.ColorKeycapAccent, promptAlpha);
            MinimalIMGUI.DrawKeyPrompt(ref cursorX, row2Y, "Q", "CANCEL", new Color(1f, 0.42f, 0.42f, 1f), promptAlpha);
        }

        /// <summary>
        /// Winch Rigged State: shows motor state, high-tech battery gauge, and reel/detach controls.
        /// </summary>
        private void DrawWinchRiggedHUD(float screenW, float screenH)
        {
            if (promptAlpha <= 0.005f) return;

            float panelW = 490f;
            float panelH = 88f;
            float panelX = (screenW - panelW) * 0.5f;
            float panelY = screenH - bottomOffset - panelH;
            Rect panelRect = new Rect(panelX, panelY, panelW, panelH);

            bool isReeling = winch.IsWinching;
            Color themeColor = isReeling ? MinimalIMGUI.ColorEmerald : MinimalIMGUI.ColorAmber;

            Color bgTint = new Color(MinimalIMGUI.ColorDarkBg.r, MinimalIMGUI.ColorDarkBg.g, MinimalIMGUI.ColorDarkBg.b, 0.94f * promptAlpha);
            MinimalIMGUI.DrawPanel(panelRect, bgTint, themeColor, 2f);

            // Row 1: Motor state badge (Left) & Battery gauge (Right)
            float row1Y = panelY + 12f;

            if (isReeling)
            {
                float pulse = (Mathf.Sin(Time.time * 8f) + 1f) * 0.5f;
                Color activeColor = Color.Lerp(MinimalIMGUI.ColorEmerald, Color.white, pulse * 0.25f);
                Rect statusRect = new Rect(panelX + 14f, row1Y, 134f, 24f);
                MinimalIMGUI.DrawStatusBadge(statusRect, "REELING ACTIVE", activeColor, promptAlpha);
            }
            else
            {
                Rect statusRect = new Rect(panelX + 14f, row1Y, 134f, 24f);
                MinimalIMGUI.DrawStatusBadge(statusRect, "BRAKE LOCKED", MinimalIMGUI.ColorAmber, promptAlpha);
            }

            // Battery section
            if (energy != null)
            {
                float batX = panelX + 162f;
                float batW = panelW - 176f;

                // Battery Icon
                float iconSize = 18f;
                Rect iconRect = new Rect(batX, row1Y + 3f, iconSize, iconSize);
                MinimalIMGUI.DrawIcon(iconRect, MinimalIMGUI.IconBattery, Color.white, promptAlpha * 0.85f);

                // Battery Bar
                float gaugeX = batX + iconSize + 8f;
                float gaugeW = batW - iconSize - 74f;
                Rect gaugeRect = new Rect(gaugeX, row1Y + 5f, gaugeW, 14f);

                Color batFillColor;
                if (displayedEnergy > 0.35f)
                    batFillColor = MinimalIMGUI.ColorCyan;
                else if (displayedEnergy > 0.15f)
                    batFillColor = MinimalIMGUI.ColorAmber;
                else
                {
                    float flash = (Mathf.Sin(Time.time * 10f) + 1f) * 0.5f;
                    batFillColor = Color.Lerp(MinimalIMGUI.ColorCrimson, Color.white, flash * 0.5f);
                }

                MinimalIMGUI.DrawProgressBar(gaugeRect, displayedEnergy, batFillColor, new Color(0.08f, 0.10f, 0.14f, 0.9f), promptAlpha);

                // Numeric readout
                Rect readRect = new Rect(gaugeX + gaugeW + 8f, row1Y, 56f, 24f);
                MinimalIMGUI.ValueStyle.fontSize = 13;
                MinimalIMGUI.ValueStyle.normal.textColor = new Color(0.92f, 0.94f, 0.96f, promptAlpha);
                GUI.Label(readRect, $"{energy.CurrentEnergy:F0} Wh", MinimalIMGUI.ValueStyle);
            }

            // Row 2: Action Keycaps
            float row2Y = panelY + 50f;
            float cursorX = panelX + 14f;
            MinimalIMGUI.DrawKeyPrompt(ref cursorX, row2Y, "HOLD LMB", "PULL COFFIN", MinimalIMGUI.ColorKeycapAccent, promptAlpha);
            MinimalIMGUI.DrawKeyPrompt(ref cursorX, row2Y, "Q", "DISCONNECT", new Color(1f, 0.45f, 0.45f, 1f), promptAlpha);
        }
    }
}
