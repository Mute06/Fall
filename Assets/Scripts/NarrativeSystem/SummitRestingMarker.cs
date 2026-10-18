using System;
using UnityEngine;

namespace FirstPersonSystem.Narrative
{
    /// <summary>
    /// Interactive & spatial trigger marker located inside the excavated grave hole at the summit.
    /// Accurately snaps to the terrain floor inside the hole and detects when the coffin is dragged into it.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class SummitRestingMarker : Interactable
    {
        [Header("Grave Hole Trigger Dimensions")]
        [Tooltip("Trigger dimensions covering the grave pit.")]
        [SerializeField] private Vector3 triggerAreaSize = new Vector3(3.0f, 2.6f, 4.6f);

        [Tooltip("Optional visual aura or highlight marker shown around the grave pit.")]
        [SerializeField] private GameObject restingAura;

        [Header("Status")]
        [SerializeField] private bool isCompleted = false;

        private BoxCollider triggerCollider;
        private QuestManager questManager;
        private CoffinPullable coffin;

        public bool IsCompleted => isCompleted;
        public Vector3 TriggerAreaSize => triggerAreaSize;

        public override void Awake()
        {
            base.Awake();
            gameObject.name = "SummitRestingMarker";
            EnsureTriggerCollider();
            SnapToTerrain();
        }

        private void Start()
        {
            SnapToTerrain();

            questManager = QuestManager.Instance;
            if (questManager != null)
            {
                questManager.SetSummitTarget(transform);
            }

            coffin = CoffinPullable.Instance != null ? CoffinPullable.Instance : FindAnyObjectByType<CoffinPullable>();
        }

        private void OnValidate()
        {
            EnsureTriggerCollider();
        }

        /// <summary>
        /// Snaps the marker's Y position to the actual terrain surface/hole bottom.
        /// </summary>
        public void SnapToTerrain()
        {
            Terrain terrain = Terrain.activeTerrain;
            if (terrain != null)
            {
                float groundY = terrain.SampleHeight(new Vector3(transform.position.x, 0f, transform.position.z)) + terrain.transform.position.y;
                transform.position = new Vector3(transform.position.x, groundY + 0.1f, transform.position.z);
            }
        }

        /// <summary>
        /// Configures the trigger box dimensions from the generator's profile.
        /// </summary>
        public void ConfigureGraveTrigger(Vector2 holeSize, float depth)
        {
            triggerAreaSize = new Vector3(
                Mathf.Max(2.0f, holeSize.x + 0.5f),
                Mathf.Max(2.0f, depth + 1.2f),
                Mathf.Max(3.0f, holeSize.y + 0.5f)
            );

            EnsureTriggerCollider();
            SnapToTerrain();
        }

        private void EnsureTriggerCollider()
        {
            if (triggerCollider == null) triggerCollider = GetComponent<BoxCollider>();
            if (triggerCollider == null) triggerCollider = gameObject.AddComponent<BoxCollider>();

            triggerCollider.isTrigger = true;
            triggerCollider.size = triggerAreaSize;
            // Center the trigger slightly elevated so the bottom sits at terrain floor and reaches up
            triggerCollider.center = new Vector3(0f, triggerAreaSize.y * 0.45f, 0f);
        }

        private void Update()
        {
            if (isCompleted) return;

            if (questManager == null) questManager = QuestManager.Instance;
            if (questManager == null || questManager.CurrentState != QuestState.Active) return;

            if (coffin == null)
            {
                coffin = CoffinPullable.Instance != null ? CoffinPullable.Instance : FindAnyObjectByType<CoffinPullable>();
            }

            // Spatial check in addition to OnTriggerEnter (ensures physics sweeps or kinematic moves are caught)
            if (coffin != null)
            {
                Vector3 coffinPos = coffin.transform.position;
                Vector3 markerPos = transform.position;

                float halfX = triggerAreaSize.x * 0.55f;
                float halfZ = triggerAreaSize.z * 0.55f;

                bool inX = Mathf.Abs(coffinPos.x - markerPos.x) <= halfX;
                bool inZ = Mathf.Abs(coffinPos.z - markerPos.z) <= halfZ;
                bool inY = Mathf.Abs(coffinPos.y - markerPos.y) <= (triggerAreaSize.y * 0.8f);

                if (inX && inZ && inY)
                {
                    CompleteResting();
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            HandleTrigger(other);
        }

        private void OnTriggerStay(Collider other)
        {
            HandleTrigger(other);
        }

        private void HandleTrigger(Collider other)
        {
            if (isCompleted) return;

            CoffinPullable candidate = other.GetComponentInParent<CoffinPullable>();
            if (candidate != null)
            {
                CompleteResting();
            }
        }

        public override void OnInteractStart(FirstPersonController player)
        {
            if (questManager == null) questManager = QuestManager.Instance;
            if (questManager != null && questManager.CurrentState == QuestState.Active && !isCompleted)
            {
                CompleteResting();
            }
        }

        public override void OnFocus() { }
        public override void OnLoseFocus() { }
        public override void OnStartFocus() { }
        public override void OnInteracting(FirstPersonController player) { }
        public override void OnInteractEnd(FirstPersonController player) { }

        public void CompleteResting()
        {
            if (isCompleted) return;
            isCompleted = true;

            Debug.Log("[SummitRestingMarker] Coffin successfully placed inside the summit grave hole! Completing quest and playing final farewell.");
            if (questManager == null) questManager = QuestManager.Instance;
            if (questManager != null)
            {
                questManager.CompleteQuest();
            }

            if (NarrativeTriggerManager.Instance != null)
            {
                NarrativeTriggerManager.Instance.TriggerMilestone7();
            }

            if (restingAura != null)
            {
                restingAura.SetActive(false);
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = isCompleted ? new Color(0.2f, 0.9f, 0.4f, 0.45f) : new Color(1f, 0.76f, 0.2f, 0.45f);
            Vector3 center = transform.position + new Vector3(0f, triggerAreaSize.y * 0.45f, 0f);
            Gizmos.DrawWireCube(center, triggerAreaSize);

            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.8f);
            Gizmos.DrawSphere(transform.position, 0.35f);
        }
    }
}
