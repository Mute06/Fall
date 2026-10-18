using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FirstPersonSystem.Narrative
{
    /// <summary>
    /// Cinematic, movie-style subtitle HUD using Unity's uGUI Canvas system.
    /// Renders pure subtitle text at the bottom of the screen with no background box and no title.
    /// Utilizes TextMeshProUGUI with built-in font outline/shadow and CanvasGroup for smooth alpha transitions.
    /// </summary>
    public class NarrativeSubtitleHUD : MonoBehaviour
    {
        [Header("UI Canvas References")]
        [Tooltip("Root Canvas. If null, automatically locates the scene Canvas.")]
        [SerializeField] private Canvas targetCanvas;

        [Tooltip("CanvasGroup controlling subtitle alpha fading.")]
        [SerializeField] private CanvasGroup canvasGroup;

        [Tooltip("TextMeshProUGUI rendering the movie subtitle text.")]
        [SerializeField] private TextMeshProUGUI subtitleText;

        [Header("Positioning & Sizing (Reference 1080p)")]
        [SerializeField] private float bottomOffset = 64f;
        [SerializeField] private float widthPercent = 0.74f;
        [SerializeField] private float maxHeight = 160f;

        [Header("Animation")]
        [SerializeField] private float fadeInSpeed = 8f;
        [SerializeField] private float fadeOutSpeed = 6f;

        [Header("Typography")]
        [SerializeField] private float fontSize = 32f;
        [SerializeField] private Color textColor = new Color(0.98f, 0.98f, 0.98f, 1f);
        [SerializeField] private Color outlineColor = new Color(0f, 0f, 0f, 0.96f);
        [SerializeField] private float outlineWidth = 0.24f;

        private NarrativeManager narrativeManager;
        private string cachedSubtitle = "";

        public CanvasGroup CanvasGroup => canvasGroup;
        public TextMeshProUGUI SubtitleText => subtitleText;

        private void Awake()
        {
            EnsureUIHierarchy();
        }

        private void Start()
        {
            narrativeManager = NarrativeManager.Instance;
            EnsureUIHierarchy();
        }

        private void Update()
        {
            if (narrativeManager == null)
            {
                narrativeManager = NarrativeManager.Instance;
                if (narrativeManager == null) return;
            }

            string currentSub = narrativeManager.CurrentSubtitle;
            bool shouldShow = !string.IsNullOrEmpty(currentSub);

            if (shouldShow && currentSub != cachedSubtitle)
            {
                cachedSubtitle = currentSub;
                if (subtitleText != null)
                {
                    subtitleText.text = cachedSubtitle;
                }
            }

            if (canvasGroup != null)
            {
                float targetAlpha = shouldShow ? 1f : 0f;
                float speed = shouldShow ? fadeInSpeed : fadeOutSpeed;
                canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, Time.deltaTime * speed);
            }
        }

        /// <summary>
        /// Automatically discovers or constructs the movie-style subtitle hierarchy under the Canvas.
        /// </summary>
        public void EnsureUIHierarchy()
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

            // 1. Root Subtitle Container under Canvas
            Transform existing = targetCanvas.transform.Find("SubtitleHUD");
            GameObject hudObj;
            if (existing != null)
            {
                hudObj = existing.gameObject;
            }
            else
            {
                hudObj = new GameObject("SubtitleHUD", typeof(RectTransform), typeof(CanvasGroup));
                hudObj.transform.SetParent(targetCanvas.transform, false);
            }

            canvasGroup = hudObj.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = hudObj.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            RectTransform rect = hudObj.GetComponent<RectTransform>();
            float margin = (1f - widthPercent) * 0.5f;
            rect.anchorMin = new Vector2(margin, 0f);
            rect.anchorMax = new Vector2(1f - margin, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, bottomOffset);
            rect.sizeDelta = new Vector2(0f, maxHeight);

            // 2. TextMeshProUGUI Element
            Transform textTrans = hudObj.transform.Find("SubtitleText");
            GameObject textObj;
            if (textTrans != null)
            {
                textObj = textTrans.gameObject;
            }
            else
            {
                textObj = new GameObject("SubtitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
                textObj.transform.SetParent(hudObj.transform, false);
            }

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            textRect.pivot = new Vector2(0.5f, 0f);

            subtitleText = textObj.GetComponent<TextMeshProUGUI>();
            subtitleText.raycastTarget = false;
            subtitleText.alignment = TextAlignmentOptions.Bottom;
            subtitleText.enableWordWrapping = true;
            subtitleText.fontSize = fontSize;
            subtitleText.fontStyle = FontStyles.Bold;
            subtitleText.color = textColor;

            // Apply high-contrast text outline
            subtitleText.outlineColor = outlineColor;
            subtitleText.outlineWidth = outlineWidth;

            if (TMP_Settings.defaultFontAsset != null && subtitleText.font == null)
            {
                subtitleText.font = TMP_Settings.defaultFontAsset;
            }
        }
    }
}
