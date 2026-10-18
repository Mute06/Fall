using System;
using UnityEngine;

namespace FirstPersonSystem.Narrative
{
    public enum QuestState
    {
        Inactive,
        Active,
        Completed
    }

    /// <summary>
    /// Manages the endgame quest triggered at the mountain summit:
    /// 'Lay your wife to rest' (carry and place the coffin at the summit marker).
    /// </summary>
    public class QuestManager : MonoBehaviour
    {
        public static QuestManager Instance { get; set; }

        [Header("Summit Quest Configuration")]
        [SerializeField] private string defaultQuestTitle = "Lay your wife to rest";
        [SerializeField] private string defaultQuestObjective = "Drag the coffin into the grave hole at the end of the path.";

        [Header("Summit Resting Shrine Target")]
        [Tooltip("Transform representing the final resting spot at the summit. If not assigned, will search for 'SummitMarker' or player summit position.")]
        [SerializeField] private Transform summitRestingTarget;

        [Tooltip("Distance from summit target within which the coffin is considered successfully delivered.")]
        [SerializeField] private float completionDistance = 6.0f;

        [Header("Audio")]
        [SerializeField] private AudioClip questTriggeredSound;
        [SerializeField] private AudioClip questCompletedSound;
        private AudioSource questAudioSource;

        // Runtime State
        private QuestState currentState = QuestState.Inactive;
        private string activeTitle = "";
        private string activeObjective = "";
        private CoffinPullable trackedCoffin;

        // Events
        public event Action<string, string> OnQuestActivated;
        public event Action<string> OnQuestCompleted;

        public QuestState CurrentState => currentState;
        public string ActiveTitle => activeTitle;
        public string ActiveObjective => activeObjective;
        public Transform SummitTarget => summitRestingTarget;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            questAudioSource = GetComponent<AudioSource>();
            if (questAudioSource == null)
            {
                questAudioSource = gameObject.AddComponent<AudioSource>();
                questAudioSource.playOnAwake = false;
                questAudioSource.spatialBlend = 0f;
            }

            activeTitle = defaultQuestTitle;
            activeObjective = defaultQuestObjective;
        }

        private void Start()
        {
            // Auto locate coffin if available
            if (trackedCoffin == null)
            {
                trackedCoffin = CoffinPullable.Instance != null ? CoffinPullable.Instance : FindAnyObjectByType<CoffinPullable>();
            }

            // Auto locate or spawn summit resting marker if none exists
            if (summitRestingTarget == null)
            {
                GameObject marker = GameObject.Find("SummitRestingMarker");
                if (marker != null)
                {
                    summitRestingTarget = marker.transform;
                }
            }

            // Listen to NarrativeManager quest requests
            if (NarrativeManager.Instance != null)
            {
                NarrativeManager.Instance.OnQuestTriggerRequested += ActivateQuest;
            }
        }

        private void Update()
        {
            if (currentState != QuestState.Active) return;

            // If we have both a tracked coffin and a summit target, check for delivery completion
            if (trackedCoffin == null)
            {
                trackedCoffin = CoffinPullable.Instance != null ? CoffinPullable.Instance : FindAnyObjectByType<CoffinPullable>();
            }

            if (trackedCoffin != null && summitRestingTarget != null)
            {
                // If the target has a SummitRestingMarker, allow its exact trigger collider/bounding box to drive completion
                var marker = summitRestingTarget.GetComponent<SummitRestingMarker>();
                if (marker == null)
                {
                    float distance = Vector3.Distance(trackedCoffin.transform.position, summitRestingTarget.position);
                    if (distance <= completionDistance)
                    {
                        CompleteQuest();
                    }
                }
            }
        }

        /// <summary>
        /// Activates the summit quest with the given title and objective.
        /// </summary>
        public void ActivateQuest(string title, string objective)
        {
            if (currentState == QuestState.Completed) return;

            activeTitle = string.IsNullOrEmpty(title) ? defaultQuestTitle : title;
            activeObjective = string.IsNullOrEmpty(objective) ? defaultQuestObjective : objective;
            currentState = QuestState.Active;

            Debug.Log($"[QuestManager] Quest Activated: '{activeTitle}' — '{activeObjective}'");

            if (questTriggeredSound != null && questAudioSource != null)
            {
                questAudioSource.PlayOneShot(questTriggeredSound);
            }

            OnQuestActivated?.Invoke(activeTitle, activeObjective);
        }

        /// <summary>
        /// Marks the current quest as completed.
        /// </summary>
        public void CompleteQuest()
        {
            if (currentState == QuestState.Completed) return;

            currentState = QuestState.Completed;
            activeObjective = "May you find peace, Elena. Her final rest has been granted.";
            Debug.Log($"[QuestManager] Quest Completed: '{activeTitle}'!");

            if (questCompletedSound != null && questAudioSource != null)
            {
                questAudioSource.PlayOneShot(questCompletedSound);
            }

            OnQuestCompleted?.Invoke(activeTitle);
        }

        /// <summary>
        /// Sets or creates the summit resting target transform.
        /// </summary>
        public void SetSummitTarget(Transform target)
        {
            summitRestingTarget = target;
        }
    }
}
