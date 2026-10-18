using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using FirstPersonSystem.SceneManagement;

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

        [Header("Endgame Scene Transition")]
        [Tooltip("If enabled, automatically loads the start scene after the quest is completed and delay elapses.")]
        [SerializeField] private bool returnToStartSceneOnComplete = true;

        [Tooltip("Delay in seconds after quest completion before returning to the start scene.")]
        [SerializeField] private float returnToStartSceneDelay = 10.0f;

        [Tooltip("Name of the start scene to return to.")]
        [SerializeField] private string startSceneName = "Start";

        [Header("Audio")]
        [SerializeField] private AudioClip questTriggeredSound;
        [SerializeField] private AudioClip questCompletedSound;
        private AudioSource questAudioSource;

        // Runtime State
        private QuestState currentState = QuestState.Inactive;
        private string activeTitle = "";
        private string activeObjective = "";
        private CoffinPullable trackedCoffin;
        private Coroutine returnToStartCoroutine;

        // Events
        public event Action<string, string> OnQuestActivated;
        public event Action<string> OnQuestCompleted;

        public QuestState CurrentState => currentState;
        public string ActiveTitle => activeTitle;
        public string ActiveObjective => activeObjective;
        public Transform SummitTarget => summitRestingTarget;
        public bool ReturnToStartSceneOnComplete
        {
            get => returnToStartSceneOnComplete;
            set => returnToStartSceneOnComplete = value;
        }
        public float ReturnToStartSceneDelay
        {
            get => returnToStartSceneDelay;
            set => returnToStartSceneDelay = value;
        }
        public string StartSceneName
        {
            get => startSceneName;
            set => startSceneName = value;
        }

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

        private void OnDisable()
        {
            if (returnToStartCoroutine != null)
            {
                StopCoroutine(returnToStartCoroutine);
                returnToStartCoroutine = null;
            }
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

            // Automatically return to the start scene after 10 seconds has passed
            if (Application.isPlaying && returnToStartSceneOnComplete)
            {
                if (returnToStartCoroutine != null)
                {
                    StopCoroutine(returnToStartCoroutine);
                }
                returnToStartCoroutine = StartCoroutine(ReturnToStartSceneRoutine(returnToStartSceneDelay));
            }
        }

        private IEnumerator ReturnToStartSceneRoutine(float delay)
        {
            // Allow the player to witness the summit resting completion and farewell dialogue,
            // then smoothly fade out to black during the final 2 seconds of the 10-second countdown.
            float fadeDuration = 2.0f;
            float waitBeforeFade = Mathf.Max(0f, delay - fadeDuration);

            if (waitBeforeFade > 0f)
            {
                yield return new WaitForSeconds(waitBeforeFade);
            }

            float remainingFadeTime = Mathf.Min(fadeDuration, delay);
            if (remainingFadeTime > 0f)
            {
                LoadingScreenManager.FadeToBlack(remainingFadeTime);
                yield return new WaitForSeconds(remainingFadeTime);
            }

            Debug.Log($"[QuestManager] {delay:F1} seconds elapsed since quest completion. Returning to '{startSceneName}' scene.");

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            try
            {
                LoadingScreenManager.LoadScene(startSceneName);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[QuestManager] LoadingScreenManager.LoadScene failed: {ex.Message}. Loading directly via SceneManager.");
                SceneManager.LoadSceneAsync(startSceneName);
            }
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
