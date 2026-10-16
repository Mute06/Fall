using UnityEngine;

namespace FirstPersonSystem
{
    public class WinchHUD : MonoBehaviour
    {
        [SerializeField] private bool showHUD = true;

        private WinchSystem winch;
        private WinchEnergySystem energy;
        private GUIStyle promptStyle;
        private GUIStyle energyStyle;
        private Texture2D barBackgroundTexture;
        private Texture2D barFillTexture;

        private void Start()
        {
            winch = WinchSystem.Instance;
            energy = GetComponent<WinchEnergySystem>();
            if (energy == null && winch != null)
            {
                energy = winch.GetComponent<WinchEnergySystem>();
            }

            InitStyles();
        }

        private void InitStyles()
        {
            barBackgroundTexture = new Texture2D(1, 1);
            barBackgroundTexture.SetPixel(0, 0, new Color(0.1f, 0.1f, 0.1f, 0.7f));
            barBackgroundTexture.Apply();

            barFillTexture = new Texture2D(1, 1);
            barFillTexture.SetPixel(0, 0, new Color(0.2f, 0.7f, 1f, 0.9f));
            barFillTexture.Apply();
        }

        private void OnGUI()
        {
            if (!showHUD) return;
            if (winch == null) winch = WinchSystem.Instance;
            if (winch == null) return;
            if (energy == null) energy = winch.GetComponent<WinchEnergySystem>();

            if (promptStyle == null)
            {
                promptStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 18,
                    fontStyle = FontStyle.Bold
                };
                promptStyle.normal.textColor = Color.white;
            }

            if (energyStyle == null)
            {
                energyStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fontSize = 14,
                    fontStyle = FontStyle.Bold
                };
                energyStyle.normal.textColor = Color.white;
            }

            float screenW = Screen.width;
            float screenH = Screen.height;

            // 1. Contextual Action Prompts
            string promptText = "";
            switch (winch.CurrentState)
            {
                case WinchSystem.WinchState.Idle:
                    // Only show small prompt if looking near an anchorable or nothing
                    break;
                case WinchSystem.WinchState.CarryingRope:
                    promptText = $"<b>Carrying Rope:</b> {winch.CurrentRopeLength:F1}m / {winch.MaxRopeLength:F0}m\n" +
                                 "<color=#FFA500>[LMB]</color> Attach to Coffin  |  <color=#FF4444>[Q]</color> Cancel Rope";
                    break;
                case WinchSystem.WinchState.WinchRigged:
                    string motorState = winch.IsWinching ? "<color=#00FF88>Winching Active</color>" : "<color=#FFFF00>Brake Locked</color>";
                    promptText = $"<b>Winch Connected:</b> {motorState}\n" +
                                 "<color=#FFA500>[Hold LMB]</color> Pull Coffin  |  <color=#FF4444>[Q]</color> Disconnect (No Brake!)";
                    break;
            }

            if (!string.IsNullOrEmpty(promptText))
            {
                Rect promptRect = new Rect(screenW * 0.5f - 250f, screenH - 120f, 500f, 60f);
                GUI.Label(promptRect, promptText, promptStyle);
            }

            // 2. Winch Energy Bar (visible when rigged or winching)
            if (winch.CurrentState == WinchSystem.WinchState.WinchRigged && energy != null)
            {
                float barW = 240f;
                float barH = 18f;
                float barX = screenW * 0.5f - barW * 0.5f;
                float barY = screenH - 50f;

                // Background
                GUI.DrawTexture(new Rect(barX, barY, barW, barH), barBackgroundTexture);

                // Fill
                float fillW = barW * energy.EnergyRatio;
                GUI.DrawTexture(new Rect(barX, barY, fillW, barH), barFillTexture);

                // Label
                Rect labelRect = new Rect(barX, barY - 22f, barW, 20f);
                GUI.Label(labelRect, $"WINCH BATTERY: {energy.CurrentEnergy:F0} / {energy.MaxEnergy:F0}", energyStyle);
            }
        }
    }
}
