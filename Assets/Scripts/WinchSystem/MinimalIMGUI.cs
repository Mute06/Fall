using System;
using System.IO;
using UnityEngine;

namespace FirstPersonSystem
{
    /// <summary>
    /// Shared IMGUI style manager and rendering utility for minimal, high-contrast, atmospheric HUDs.
    /// Caches all GUIStyles, procedural fallbacks, and 9-slice textures to avoid per-frame GC allocations.
    /// </summary>
    public static class MinimalIMGUI
    {
        private static bool isInitialized = false;

        // Textures
        public static Texture2D PanelTexture { get; private set; }
        public static Texture2D KeycapTexture { get; private set; }
        public static Texture2D StatusPillTexture { get; private set; }
        public static Texture2D GaugeTrackTexture { get; private set; }
        public static Texture2D GaugeFillTexture { get; private set; }
        public static Texture2D VignetteTexture { get; private set; }
        public static Texture2D IconWarning { get; private set; }
        public static Texture2D IconBattery { get; private set; }
        public static Texture2D IconAnchor { get; private set; }
        public static Texture2D IconDirection { get; private set; }
        public static Texture2D WhiteTexture { get; private set; }

        // Styles
        public static GUIStyle PanelStyle { get; private set; }
        public static GUIStyle KeycapStyle { get; private set; }
        public static GUIStyle KeycapTextStyle { get; private set; }
        public static GUIStyle StatusPillStyle { get; private set; }
        public static GUIStyle StatusPillTextStyle { get; private set; }
        public static GUIStyle GaugeTrackStyle { get; private set; }
        public static GUIStyle GaugeFillStyle { get; private set; }
        public static GUIStyle HeaderStyle { get; private set; }
        public static GUIStyle SubHeaderStyle { get; private set; }
        public static GUIStyle ActionPromptStyle { get; private set; }
        public static GUIStyle ValueStyle { get; private set; }

        // Colors
        public static readonly Color ColorDarkBg = new Color(0.06f, 0.08f, 0.10f, 0.90f);
        public static readonly Color ColorBorderSubtle = new Color(1f, 1f, 1f, 0.15f);
        public static readonly Color ColorKeycapBg = new Color(0.12f, 0.14f, 0.18f, 0.95f);
        public static readonly Color ColorKeycapAccent = new Color(1f, 0.72f, 0.22f, 1f); // Warm gold/amber
        public static readonly Color ColorCyan = new Color(0.22f, 0.74f, 0.97f, 1f);      // Winch sky blue
        public static readonly Color ColorEmerald = new Color(0.20f, 0.85f, 0.55f, 1f);   // Active motor green
        public static readonly Color ColorAmber = new Color(1f, 0.75f, 0.15f, 1f);       // Warning / brake gold
        public static readonly Color ColorCrimson = new Color(0.96f, 0.28f, 0.28f, 1f);   // Restraint red

        public static void EnsureInitialized()
        {
            if (isInitialized && PanelStyle != null && WhiteTexture != null) return;

            InitTextures();
            InitStyles();
            isInitialized = true;
        }

        private static void InitTextures()
        {
            if (WhiteTexture == null)
            {
                WhiteTexture = new Texture2D(1, 1);
                WhiteTexture.SetPixel(0, 0, Color.white);
                WhiteTexture.Apply();
            }

            PanelTexture = LoadTextureOrDefault("panel_background.png", 64, 64, new Color(0.08f, 0.10f, 0.13f, 0.92f), true);
            KeycapTexture = LoadTextureOrDefault("keycap_badge.png", 48, 48, new Color(0.14f, 0.16f, 0.22f, 0.95f), true);
            StatusPillTexture = LoadTextureOrDefault("status_pill.png", 64, 32, new Color(0.10f, 0.12f, 0.16f, 0.90f), true);
            GaugeTrackTexture = LoadTextureOrDefault("gauge_track.png", 64, 24, new Color(0.04f, 0.05f, 0.07f, 0.95f), true);
            GaugeFillTexture = LoadTextureOrDefault("gauge_fill.png", 64, 24, Color.white, false);
            VignetteTexture = LoadTextureOrDefault("vignette_gradient.png", 128, 128, Color.white, false);
            IconWarning = LoadTextureOrDefault("icon_warning.png", 32, 32, Color.white, false);
            IconBattery = LoadTextureOrDefault("icon_battery.png", 32, 32, Color.white, false);
            IconAnchor = LoadTextureOrDefault("icon_anchor.png", 32, 32, Color.white, false);
            IconDirection = LoadTextureOrDefault("icon_direction.png", 32, 32, Color.white, false);
        }

        private static Texture2D LoadTextureOrDefault(string fileName, int defaultW, int defaultH, Color defaultColor, bool drawBorder)
        {
            try
            {
                string relativePath = Path.Combine("Assets", "Textures", "UI", fileName);
                string fullPath = Path.Combine(Application.dataPath, "Textures", "UI", fileName);

                if (File.Exists(fullPath))
                {
                    byte[] data = File.ReadAllBytes(fullPath);
                    Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (tex.LoadImage(data))
                    {
                        tex.name = fileName;
                        return tex;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MinimalIMGUI] Could not load texture {fileName}: {ex.Message}. Using procedural fallback.");
            }

            // Procedural fallback
            Texture2D fallback = new Texture2D(defaultW, defaultH, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[defaultW * defaultH];
            Color borderCol = new Color(1f, 1f, 1f, 0.18f);

            for (int y = 0; y < defaultH; y++)
            {
                for (int x = 0; x < defaultW; x++)
                {
                    bool isBorder = drawBorder && (x == 0 || y == 0 || x == defaultW - 1 || y == defaultH - 1);
                    pixels[y * defaultW + x] = isBorder ? borderCol : defaultColor;
                }
            }
            fallback.SetPixels(pixels);
            fallback.Apply();
            fallback.name = fileName + "_Fallback";
            return fallback;
        }

        private static void InitStyles()
        {
            // Panel 9-slice Style
            PanelStyle = new GUIStyle
            {
                border = new RectOffset(12, 12, 12, 12)
            };
            PanelStyle.normal.background = PanelTexture;

            // Keycap Badge Style
            KeycapStyle = new GUIStyle
            {
                border = new RectOffset(10, 10, 10, 10)
            };
            KeycapStyle.normal.background = KeycapTexture;

            KeycapTextStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                fontStyle = FontStyle.Bold
            };
            KeycapTextStyle.normal.textColor = ColorKeycapAccent;

            // Status Pill Style
            StatusPillStyle = new GUIStyle
            {
                border = new RectOffset(14, 14, 10, 10)
            };
            StatusPillStyle.normal.background = StatusPillTexture;

            StatusPillTextStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                fontStyle = FontStyle.Bold
            };
            StatusPillTextStyle.normal.textColor = Color.white;

            // Gauge Track & Fill
            GaugeTrackStyle = new GUIStyle
            {
                border = new RectOffset(6, 6, 6, 6)
            };
            GaugeTrackStyle.normal.background = GaugeTrackTexture;

            GaugeFillStyle = new GUIStyle
            {
                border = new RectOffset(6, 6, 6, 6)
            };
            GaugeFillStyle.normal.background = GaugeFillTexture;

            // Typography Styles
            HeaderStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 15,
                fontStyle = FontStyle.Bold
            };
            HeaderStyle.normal.textColor = Color.white;

            SubHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 12,
                fontStyle = FontStyle.Normal
            };
            SubHeaderStyle.normal.textColor = new Color(0.85f, 0.88f, 0.92f, 0.9f);

            ActionPromptStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };
            ActionPromptStyle.normal.textColor = new Color(0.95f, 0.95f, 0.95f, 1f);

            ValueStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleRight,
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };
            ValueStyle.normal.textColor = Color.white;
        }

        // ==========================================
        //  DRAWING UTILITIES
        // ==========================================

        public static void DrawPanel(Rect rect, Color bgTint, Color accentColor = default, float accentHeight = 2.5f)
        {
            EnsureInitialized();

            Color prev = GUI.color;

            // Background panel
            GUI.color = bgTint;
            GUI.Box(rect, GUIContent.none, PanelStyle);

            // Optional top accent line
            if (accentColor.a > 0.01f && accentHeight > 0f)
            {
                GUI.color = accentColor;
                GUI.DrawTexture(new Rect(rect.x + 8f, rect.y + 1f, rect.width - 16f, accentHeight), WhiteTexture);
            }

            GUI.color = prev;
        }

        public static void DrawKeyPrompt(ref float currentX, float y, string key, string actionText, Color keyAccent, float alpha = 1f)
        {
            EnsureInitialized();

            GUIContent keyContent = new GUIContent(key);
            float keyWidth = Mathf.Max(32f, KeycapTextStyle.CalcSize(keyContent).x + 16f);
            Rect keyRect = new Rect(currentX, y, keyWidth, 22f);

            Color prev = GUI.color;

            // Keycap body
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Box(keyRect, GUIContent.none, KeycapStyle);

            // Keycap text
            KeycapTextStyle.normal.textColor = new Color(keyAccent.r, keyAccent.g, keyAccent.b, alpha);
            GUI.Label(keyRect, key, KeycapTextStyle);

            // Action label
            GUIContent actionContent = new GUIContent(actionText);
            float actionWidth = ActionPromptStyle.CalcSize(actionContent).x;
            Rect actionRect = new Rect(currentX + keyWidth + 7f, y + 1f, actionWidth, 20f);

            ActionPromptStyle.normal.textColor = new Color(0.92f, 0.94f, 0.97f, alpha);
            GUI.Label(actionRect, actionText, ActionPromptStyle);

            GUI.color = prev;

            // Advance currentX
            currentX += keyWidth + actionWidth + 18f;
        }

        public static void DrawStatusBadge(Rect rect, string text, Color badgeColor, float alpha = 1f)
        {
            EnsureInitialized();

            Color prev = GUI.color;

            // Semi-transparent colored pill
            GUI.color = new Color(badgeColor.r, badgeColor.g, badgeColor.b, 0.28f * alpha);
            GUI.Box(rect, GUIContent.none, StatusPillStyle);

            // Inner dot indicator
            float dotSize = 6f;
            Rect dotRect = new Rect(rect.x + 9f, rect.y + (rect.height - dotSize) * 0.5f, dotSize, dotSize);
            GUI.color = new Color(badgeColor.r, badgeColor.g, badgeColor.b, alpha);
            GUI.DrawTexture(dotRect, WhiteTexture);

            // Text
            StatusPillTextStyle.alignment = TextAnchor.MiddleLeft;
            StatusPillTextStyle.normal.textColor = new Color(badgeColor.r, badgeColor.g, badgeColor.b, alpha);
            Rect textRect = new Rect(rect.x + 19f, rect.y, rect.width - 24f, rect.height);
            GUI.Label(textRect, text, StatusPillTextStyle);

            GUI.color = prev;
        }

        public static void DrawProgressBar(Rect trackRect, float ratio, Color fillColor, Color trackColor, float alpha = 1f)
        {
            EnsureInitialized();

            Color prev = GUI.color;

            // Track
            GUI.color = new Color(trackColor.r, trackColor.g, trackColor.b, trackColor.a * alpha);
            GUI.Box(trackRect, GUIContent.none, GaugeTrackStyle);

            // Fill
            ratio = Mathf.Clamp01(ratio);
            if (ratio > 0.005f)
            {
                float fillW = Mathf.Max(6f, trackRect.width * ratio);
                Rect fillRect = new Rect(trackRect.x, trackRect.y, fillW, trackRect.height);
                GUI.color = new Color(fillColor.r, fillColor.g, fillColor.b, fillColor.a * alpha);
                GUI.Box(fillRect, GUIContent.none, GaugeFillStyle);
            }

            GUI.color = prev;
        }

        public static void DrawVignette(Color color, float alpha)
        {
            EnsureInitialized();
            if (alpha <= 0.001f || VignetteTexture == null) return;

            Color prev = GUI.color;
            GUI.color = new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha));
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), VignetteTexture);
            GUI.color = prev;
        }

        public static void DrawIcon(Rect rect, Texture2D icon, Color tint, float alpha = 1f)
        {
            if (icon == null) return;
            Color prev = GUI.color;
            GUI.color = new Color(tint.r, tint.g, tint.b, tint.a * alpha);
            GUI.DrawTexture(rect, icon);
            GUI.color = prev;
        }
    }
}
