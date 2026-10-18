using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FirstPersonSystem.Narrative
{
    /// <summary>
    /// Quest objective card HUD using Unity's uGUI Canvas system.
    /// Displays active quest status badge, title, and objective details at the top-left of the screen.
    /// Uses CanvasGroup for smooth fade-in and completion fade-out.
    /// </summary>
    public class QuestHUD : MonoBehaviour
    {
        [Header("UI Canvas References")]
        [Tooltip("Root Canvas. If null, automatically locates the scene Canvas.")]
        [SerializeField] private Canvas targetCanvas;

        [Tooltip("CanvasGroup controlling quest HUD alpha fading.")]
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("UI Sprites (from Assets/Textures/UI/)")]
        [SerializeField] private Sprite cardBgSprite;
        [SerializeField] private Sprite badgePillSprite;

        [SerializeField] private Image cardBackground;
        [SerializeField] private Image leftAccentBar;
        [SerializeField] private TextMeshProUGUI statusBadgeText;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI objectiveText;

        [Header("Positioning & Sizing (Reference 1080p)")]
        [SerializeField] private Vector2 anchoredPosition = new Vector2(36f, -36f);
        [SerializeField] private Vector2 cardSize = new Vector2(360f, 108f);

        [Header("Colors")]
        [SerializeField] private Color headerColor = new Color(1f, 0.74f, 0.22f, 1f); // Warm gold
        [SerializeField] private Color bodyColor = new Color(0.90f, 0.92f, 0.95f, 1f);
        [SerializeField] private Color cardBgColor = new Color(0.06f, 0.08f, 0.11f, 0.90f);
        [SerializeField] private Color activeStatusColor = new Color(0.22f, 0.74f, 0.97f, 1f); // Sky blue
        [SerializeField] private Color completeStatusColor = new Color(0.20f, 0.85f, 0.55f, 1f); // Emerald

        private QuestManager questManager;
        private float completedTimer = 0f;

        public CanvasGroup CanvasGroup => canvasGroup;

        private void Awake()
        {
            EnsureUIHierarchy();
        }

        private void Start()
        {
            questManager = QuestManager.Instance;
            EnsureUIHierarchy();
        }

        private void Update()
        {
            if (questManager == null)
            {
                questManager = QuestManager.Instance;
                if (questManager == null) return;
            }

            bool isQuestVisible = questManager.CurrentState != QuestState.Inactive;

            if (questManager.CurrentState == QuestState.Completed)
            {
                completedTimer += Time.deltaTime;
                if (completedTimer > 14f)
                {
                    isQuestVisible = false;
                }
            }

            if (canvasGroup != null)
            {
                float targetAlpha = isQuestVisible ? 1f : 0f;
                canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, Time.deltaTime * 3.5f);
            }

            if (isQuestVisible)
            {
                bool isCompleted = questManager.CurrentState == QuestState.Completed;
                Color statusCol = isCompleted ? completeStatusColor : activeStatusColor;
                Color accentCol = isCompleted ? completeStatusColor : headerColor;

                if (statusBadgeText != null)
                {
                    statusBadgeText.text = isCompleted ? "◆ OBJECTIVE COMPLETED" : "◆ SUMMIT OBJECTIVE";
                    statusBadgeText.color = statusCol;
                }

                if (leftAccentBar != null)
                {
                    leftAccentBar.color = accentCol;
                }

                if (titleText != null)
                {
                    titleText.text = questManager.ActiveTitle.ToUpperInvariant();
                }

                if (objectiveText != null)
                {
                    objectiveText.text = questManager.ActiveObjective;
                }
            }
        }

        /// <summary>
        /// Automatically discovers or constructs the quest card hierarchy under the Canvas.
        /// </summary>
        public void EnsureUIHierarchy()
        {
#if UNITY_EDITOR
            if (cardBgSprite == null) cardBgSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/panel_background.png");
            if (badgePillSprite == null) badgePillSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/status_pill.png");
#endif

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

            // 1. Root Quest HUD Container under Canvas
            Transform existing = targetCanvas.transform.Find("QuestHUD");
            GameObject cardObj;
            if (existing != null)
            {
                cardObj = existing.gameObject;
            }
            else
            {
                cardObj = new GameObject("QuestHUD", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
                cardObj.transform.SetParent(targetCanvas.transform, false);
            }

            canvasGroup = cardObj.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = cardObj.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            RectTransform cardRect = cardObj.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0f, 1f);
            cardRect.anchorMax = new Vector2(0f, 1f);
            cardRect.pivot = new Vector2(0f, 1f);
            cardRect.anchoredPosition = anchoredPosition;
            cardRect.sizeDelta = cardSize;

            cardBackground = cardObj.GetComponent<Image>();
            if (cardBgSprite != null)
            {
                cardBackground.sprite = cardBgSprite;
                cardBackground.type = Image.Type.Sliced;
                cardBackground.color = cardBgColor;
            }
            else
            {
                cardBackground.color = cardBgColor;
            }
            cardBackground.raycastTarget = false;

            // 2. Left Accent Vertical Strip
            Transform accentTrans = cardObj.transform.Find("AccentBar");
            GameObject accentObj;
            if (accentTrans != null)
            {
                accentObj = accentTrans.gameObject;
            }
            else
            {
                accentObj = new GameObject("AccentBar", typeof(RectTransform), typeof(Image));
                accentObj.transform.SetParent(cardObj.transform, false);
            }

            RectTransform accentRect = accentObj.GetComponent<RectTransform>();
            accentRect.anchorMin = new Vector2(0f, 0f);
            accentRect.anchorMax = new Vector2(0f, 1f);
            accentRect.pivot = new Vector2(0f, 0.5f);
            accentRect.anchoredPosition = new Vector2(2f, 0f);
            accentRect.sizeDelta = new Vector2(4f, -6f);

            leftAccentBar = accentObj.GetComponent<Image>();
            leftAccentBar.color = headerColor;
            leftAccentBar.raycastTarget = false;

            // 3. Status Badge Text
            Transform badgeTrans = cardObj.transform.Find("StatusBadge");
            GameObject badgeObj;
            if (badgeTrans != null)
            {
                badgeObj = badgeTrans.gameObject;
            }
            else
            {
                badgeObj = new GameObject("StatusBadge", typeof(RectTransform), typeof(TextMeshProUGUI));
                badgeObj.transform.SetParent(cardObj.transform, false);
            }

            RectTransform badgeRect = badgeObj.GetComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(0f, 1f);
            badgeRect.anchorMax = new Vector2(1f, 1f);
            badgeRect.pivot = new Vector2(0f, 1f);
            badgeRect.anchoredPosition = new Vector2(16f, -10f);
            badgeRect.sizeDelta = new Vector2(-24f, 18f);

            statusBadgeText = badgeObj.GetComponent<TextMeshProUGUI>();
            statusBadgeText.raycastTarget = false;
            statusBadgeText.alignment = TextAlignmentOptions.Left;
            statusBadgeText.fontSize = 12f;
            statusBadgeText.fontStyle = FontStyles.Bold;
            statusBadgeText.color = activeStatusColor;

            // 4. Quest Title Text
            Transform titleTrans = cardObj.transform.Find("TitleText");
            GameObject titleObj;
            if (titleTrans != null)
            {
                titleObj = titleTrans.gameObject;
            }
            else
            {
                titleObj = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
                titleObj.transform.SetParent(cardObj.transform, false);
            }

            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0f, 1f);
            titleRect.anchoredPosition = new Vector2(16f, -30f);
            titleRect.sizeDelta = new Vector2(-24f, 24f);

            titleText = titleObj.GetComponent<TextMeshProUGUI>();
            titleText.raycastTarget = false;
            titleText.alignment = TextAlignmentOptions.Left;
            titleText.fontSize = 16f;
            titleText.fontStyle = FontStyles.Bold;
            titleText.color = Color.white;

            // 5. Objective Description Text
            Transform objTrans = cardObj.transform.Find("ObjectiveText");
            GameObject descObj;
            if (objTrans != null)
            {
                descObj = objTrans.gameObject;
            }
            else
            {
                descObj = new GameObject("ObjectiveText", typeof(RectTransform), typeof(TextMeshProUGUI));
                descObj.transform.SetParent(cardObj.transform, false);
            }

            RectTransform descRect = descObj.GetComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0f, 0f);
            descRect.anchorMax = new Vector2(1f, 1f);
            descRect.pivot = new Vector2(0f, 0f);
            descRect.offsetMin = new Vector2(16f, 10f);
            descRect.offsetMax = new Vector2(-12f, -56f);

            objectiveText = descObj.GetComponent<TextMeshProUGUI>();
            objectiveText.raycastTarget = false;
            objectiveText.alignment = TextAlignmentOptions.TopLeft;
            objectiveText.enableWordWrapping = true;
            objectiveText.fontSize = 13f;
            objectiveText.fontStyle = FontStyles.Normal;
            objectiveText.color = bodyColor;
        }
    }
}
