using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using FirstPersonSystem.ProceduralGeneration;

namespace FirstPersonSystem.Narrative
{
    /// <summary>
    /// Coordinates all narrative triggers in the walking simulator:
    /// 1. Milestone 1: First Player Movement (The Wish & The Climb)
    /// 2. Milestone 2: First Winch Anchor Placed on Tree/Rock (The Worth of Limited Time)
    /// 3. Milestone 3: Safe Zone 1 reached by Coffin (~20% Elevation — The Confession)
    /// 4. Milestone 4: Safe Zone 2 reached by Coffin (~40% Elevation — Choosing Joy Over Anticipated Grief)
    /// 5. Milestone 5: Safe Zone 3 reached by Coffin (~60% Elevation — Forgiveness & Sacred Days)
    /// 6. Milestone 6: Safe Zone 4 reached by Coffin (~80% Elevation — The Peak in View)
    /// 7. Summit Plateau Reached: Activates "Lay your wife to rest" Quest
    /// 8. Milestone 7: Coffin placed in Burial Hollow at Summit Cairn (The Rest & Farewell)
    /// </summary>
    public class NarrativeTriggerManager : MonoBehaviour
    {
        public static NarrativeTriggerManager Instance { get; private set; }

        [Header("Target & World References")]
        [Tooltip("Transform of the player. If null, will locate FirstPersonController automatically.")]
        [SerializeField] private Transform playerTransform;

        [Tooltip("Transform of the coffin. If null, will locate CoffinPullable automatically.")]
        [SerializeField] private Transform coffinTransform;

        [Tooltip("Procedural Mountain Generator in the scene.")]
        [SerializeField] private ProceduralMountainGenerator mountainGenerator;

        [Tooltip("Mountain Generation Profile fallback if generator is not immediately available.")]
        [SerializeField] private MountainGenerationProfile fallbackProfile;

        [Header("Milestone 1: First Movement (The Wish & The Climb)")]
        [Tooltip("Trigger: Player steps outside the cabin and begins moving.")]
        [FormerlySerializedAs("firstMovementSequence")]
        [SerializeField] private NarrativeSequence milestone1Sequence;

        [Header("Milestone 2: First Anchor Placed (The Worth of Limited Time)")]
        [Tooltip("Trigger: Player drives the first winch anchor into a tree or rock.")]
        [FormerlySerializedAs("firstWinchPlacementSequence")]
        [SerializeField] private NarrativeSequence milestone2Sequence;

        [Header("Milestone 3: Safe Zone 1 (~20% Elevation — The Confession)")]
        [Tooltip("Trigger: Coffin reaches the first resting terrace.")]
        [FormerlySerializedAs("tier1SafeZoneSequence")]
        [SerializeField] private NarrativeSequence milestone3Sequence;

        [Header("Milestone 4: Safe Zone 2 (~40% Elevation — Choosing Joy Over Anticipated Grief)")]
        [Tooltip("Trigger: Coffin reaches the second resting shelf.")]
        [FormerlySerializedAs("tier2SafeZoneSequence")]
        [SerializeField] private NarrativeSequence milestone4Sequence;

        [Header("Milestone 5: Safe Zone 3 (~60% Elevation — Forgiveness & Sacred Days)")]
        [Tooltip("Trigger: Coffin rests on the third shelf before the rocky cliffs.")]
        [FormerlySerializedAs("tier3SafeZoneSequence")]
        [SerializeField] private NarrativeSequence milestone5Sequence;

        [Header("Milestone 6: Safe Zone 4 (~80% Elevation — The Peak in View)")]
        [Tooltip("Trigger: Coffin reaches the final resting shelf before the summit plateau.")]
        [FormerlySerializedAs("tier4SafeZoneSequence")]
        [SerializeField] private NarrativeSequence milestone6Sequence;

        [Header("Milestone 7: Final Area (The Rest & Farewell)")]
        [Tooltip("Trigger: Coffin is placed into the burial hollow at the summit cairn.")]
        [FormerlySerializedAs("summitSequence")]
        [SerializeField] private NarrativeSequence milestone7Sequence;

        // Trigger State Flags
        private bool hasMoved = false;
        private bool hasPlacedWinch = false;
        private bool hasTriggeredTier1 = false;
        private bool hasTriggeredTier2 = false;
        private bool hasTriggeredTier3 = false;
        private bool hasTriggeredTier4 = false;
        private bool hasTriggeredSummitQuest = false;
        private bool hasTriggeredMilestone7 = false;

        // Runtime Tracking
        private FirstPersonController controller;
        private Vector3 initialPlayerPosition;
        private List<SafePlateauZone> calculatedSafeZones = new List<SafePlateauZone>();
        private float summitZ = 250f;
        private bool isInitialized = false;

        // Public Accessors
        public static event Action OnSummitAreaReached;
        public float SummitZ => summitZ;
        public NarrativeSequence Milestone1Sequence => milestone1Sequence;
        public NarrativeSequence Milestone2Sequence => milestone2Sequence;
        public NarrativeSequence Milestone3Sequence => milestone3Sequence;
        public NarrativeSequence Milestone4Sequence => milestone4Sequence;
        public NarrativeSequence Milestone5Sequence => milestone5Sequence;
        public NarrativeSequence Milestone6Sequence => milestone6Sequence;
        public NarrativeSequence Milestone7Sequence => milestone7Sequence;

        public bool HasMoved => hasMoved;
        public bool HasPlacedWinch => hasPlacedWinch;
        public bool HasTriggeredTier1 => hasTriggeredTier1;
        public bool HasTriggeredTier2 => hasTriggeredTier2;
        public bool HasTriggeredTier3 => hasTriggeredTier3;
        public bool HasTriggeredTier4 => hasTriggeredTier4;
        public bool HasTriggeredSummitQuest => hasTriggeredSummitQuest;
        public bool HasTriggeredMilestone7 => hasTriggeredMilestone7;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            InitializeDefaultNarrativesIfEmpty();
        }

        private void Start()
        {
            LocateReferences();

            // Subscribe to winch placement events
            WinchSystem.OnAnyAnchorPlaced += HandleAnchorPlaced;
            if (WinchSystem.Instance != null)
            {
                WinchSystem.Instance.OnAnchorPlaced += HandleAnchorPlaced;
            }
            ProceduralMountainGenerator.OnGenerationCompleted += HandleMountainGenerated;

            SetupSafeZoneBoundaries();
            isInitialized = true;
        }

        private void OnDestroy()
        {
            WinchSystem.OnAnyAnchorPlaced -= HandleAnchorPlaced;
            if (WinchSystem.Instance != null)
            {
                WinchSystem.Instance.OnAnchorPlaced -= HandleAnchorPlaced;
            }
            ProceduralMountainGenerator.OnGenerationCompleted -= HandleMountainGenerated;

            if (Instance == this) Instance = null;
        }

        private void HandleMountainGenerated(ProceduralMountainGenerator gen)
        {
            LocateReferences();
            SetupSafeZoneBoundaries();
        }

        private void LocateReferences()
        {
            // Auto-locate player
            if (playerTransform == null)
            {
                controller = FindAnyObjectByType<FirstPersonController>();
                if (controller != null)
                {
                    playerTransform = controller.transform;
                }
                else
                {
                    GameObject pObj = GameObject.FindWithTag("Player");
                    if (pObj != null) playerTransform = pObj.transform;
                }
            }

            if (playerTransform != null)
            {
                initialPlayerPosition = playerTransform.position;
                if (controller == null) controller = playerTransform.GetComponent<FirstPersonController>();
            }

            // Auto-locate coffin
            if (coffinTransform == null)
            {
                var pullable = CoffinPullable.Instance != null ? CoffinPullable.Instance : FindAnyObjectByType<CoffinPullable>();
                if (pullable != null)
                {
                    coffinTransform = pullable.transform;
                }
            }

            // Auto-locate mountain generator & profile
            if (mountainGenerator == null)
            {
                mountainGenerator = FindAnyObjectByType<ProceduralMountainGenerator>();
            }

            if (fallbackProfile == null && mountainGenerator != null)
            {
                fallbackProfile = mountainGenerator.Profile;
            }
        }

        private void SetupSafeZoneBoundaries()
        {
            calculatedSafeZones.Clear();

            // Try to fetch pre-calculated safe zones from ProceduralMountainGenerator
            if (mountainGenerator != null && mountainGenerator.SafeZones != null && mountainGenerator.SafeZones.Count > 0)
            {
                calculatedSafeZones.AddRange(mountainGenerator.SafeZones);
                if (mountainGenerator.Profile != null)
                {
                    summitZ = mountainGenerator.Profile.pathEndXZ.y;
                }
            }
            else if (fallbackProfile != null)
            {
                // Fallback computation using exact MountainGenerationProfile formula
                float startZ = fallbackProfile.pathStartXZ.y;
                float endZ = fallbackProfile.pathEndXZ.y;
                summitZ = endZ;

                float flatEndZ = startZ + fallbackProfile.startingFlatLength;
                calculatedSafeZones.Add(new SafePlateauZone
                {
                    tierIndex = 0,
                    startZ = startZ,
                    endZ = flatEndZ,
                    baseElevation = 0f
                });

                float climbSpanZ = Mathf.Max(10f, endZ - flatEndZ);
                int tiers = Mathf.Max(1, fallbackProfile.climbTierCount);
                float tierLengthZ = climbSpanZ / tiers;
                float rampFraction = 1f - Mathf.Clamp(fallbackProfile.safeAreaRatioPerTier, 0.1f, 0.5f);

                for (int k = 0; k < tiers; k++)
                {
                    float tierStartZ = flatEndZ + k * tierLengthZ;
                    float safeStartZ = tierStartZ + tierLengthZ * rampFraction;
                    float safeEndZ = tierStartZ + tierLengthZ;

                    calculatedSafeZones.Add(new SafePlateauZone
                    {
                        tierIndex = k + 1,
                        startZ = safeStartZ,
                        endZ = safeEndZ,
                        baseElevation = 0f
                    });
                }
            }
            else
            {
                // Standard default values if no profile is linked yet
                summitZ = 250f;
                float startZ = 0f;
                float flatLen = 24f;
                float span = 250f - flatLen;
                float tierLen = span / 4f; // 4 tiers
                float rampFrac = 0.72f;

                for (int k = 0; k < 4; k++)
                {
                    float tStart = flatLen + k * tierLen;
                    calculatedSafeZones.Add(new SafePlateauZone
                    {
                        tierIndex = k + 1,
                        startZ = tStart + tierLen * rampFrac,
                        endZ = tStart + tierLen,
                        baseElevation = 0f
                    });
                }
            }
        }

        private void Update()
        {
            if (!isInitialized || playerTransform == null)
            {
                LocateReferences();
                if (playerTransform == null && Camera.main != null)
                {
                    playerTransform = Camera.main.transform;
                    initialPlayerPosition = playerTransform.position;
                }
                return;
            }

            Vector3 playerPos = playerTransform.position;

            // Ensure coffinTransform is located if it was spawned dynamically
            if (coffinTransform == null)
            {
                var pullable = CoffinPullable.Instance != null ? CoffinPullable.Instance : FindAnyObjectByType<CoffinPullable>();
                if (pullable != null) coffinTransform = pullable.transform;
            }

            Vector3 coffinPos = coffinTransform != null ? coffinTransform.position : playerPos;

            // 1. TRIGGER 1: First Movement
            if (!hasMoved)
            {
                bool movedByInput = controller != null && controller.CurrentInput.sqrMagnitude > 0.01f;
                bool movedByDistance = Vector3.Distance(playerPos, initialPlayerPosition) > 0.45f;

                if (movedByInput || movedByDistance)
                {
                    hasMoved = true;
                    Debug.Log("[NarrativeTriggerManager] Player first movement detected. Enqueueing Milestone 1.");
                    NarrativeManager.Instance?.EnqueueSequence(milestone1Sequence);
                }
            }

            // 2. SAFE ZONE TRIGGERS (Tiers 1 to 4) - Triggered when Coffin reaches resting terraces
            if (!hasTriggeredTier1 && IsPositionInSafeZoneTier(coffinPos.z, 1))
            {
                hasTriggeredTier1 = true;
                Debug.Log("[NarrativeTriggerManager] Coffin reached Safe Zone 1 (~20% Elevation). Enqueueing Milestone 3.");
                NarrativeManager.Instance?.EnqueueSequence(milestone3Sequence);
            }

            if (!hasTriggeredTier2 && IsPositionInSafeZoneTier(coffinPos.z, 2))
            {
                hasTriggeredTier2 = true;
                Debug.Log("[NarrativeTriggerManager] Coffin reached Safe Zone 2 (~40% Elevation). Enqueueing Milestone 4.");
                NarrativeManager.Instance?.EnqueueSequence(milestone4Sequence);
            }

            if (!hasTriggeredTier3 && IsPositionInSafeZoneTier(coffinPos.z, 3))
            {
                hasTriggeredTier3 = true;
                Debug.Log("[NarrativeTriggerManager] Coffin reached Safe Zone 3 (~60% Elevation). Enqueueing Milestone 5.");
                NarrativeManager.Instance?.EnqueueSequence(milestone5Sequence);
            }

            if (!hasTriggeredTier4 && IsPositionInSafeZoneTier(coffinPos.z, 4))
            {
                hasTriggeredTier4 = true;
                Debug.Log("[NarrativeTriggerManager] Coffin reached Safe Zone 4 (~80% Elevation). Enqueueing Milestone 6.");
                NarrativeManager.Instance?.EnqueueSequence(milestone6Sequence);
            }

            // 3. SUMMIT / LAST FLAT SAFE AREA REACHED: Starts the quest
            if (!hasTriggeredSummitQuest)
            {
                bool playerAtSummit = playerPos.z >= (summitZ - 3.0f);
                bool coffinAtSummit = coffinPos.z >= (summitZ - 3.0f);

                if (playerAtSummit || coffinAtSummit)
                {
                    hasTriggeredSummitQuest = true;
                    Debug.Log("[NarrativeTriggerManager] Reached the summit safe area where golden path ends. Activating quest: Lay your wife to rest.");
                    QuestManager.Instance?.ActivateQuest("Lay your wife to rest", "Drag the coffin into the burial hollow at the summit cairn.");
                    OnSummitAreaReached?.Invoke();
                    SummitMusicController.Instance?.PlaySummitMusic();
                }
            }
        }

        private bool IsPositionInSafeZoneTier(float targetZ, int tierIndex)
        {
            foreach (var zone in calculatedSafeZones)
            {
                if (zone.tierIndex == tierIndex)
                {
                    // Target has reached or entered this tier's safe zone
                    return targetZ >= zone.startZ;
                }
            }

            return false;
        }

        private void HandleAnchorPlaced(AnchorPoint anchor)
        {
            if (hasPlacedWinch) return;

            hasPlacedWinch = true;
            Debug.Log("[NarrativeTriggerManager] First winch anchor placed on tree/rock. Enqueueing Milestone 2.");
            NarrativeManager.Instance?.EnqueueSequence(milestone2Sequence);
        }

        /// <summary>
        /// Triggered when the coffin is successfully placed into the burial hollow at the summit cairn.
        /// </summary>
        public void TriggerMilestone7()
        {
            if (hasTriggeredMilestone7) return;

            hasTriggeredMilestone7 = true;
            Debug.Log("[NarrativeTriggerManager] Coffin placed in burial hollow. Enqueueing Milestone 7 (The Rest & Farewell).");
            NarrativeManager.Instance?.EnqueueSequence(milestone7Sequence);
        }

        /// <summary>
        /// Populates exact user letter texts and loads voice-over audio clips.
        /// </summary>
        public void InitializeDefaultNarrativesIfEmpty()
        {
            // Milestone 1: First Movement (The Wish & The Climb)
            if (milestone1Sequence == null || milestone1Sequence.lines == null || milestone1Sequence.lines.Count == 0 || string.IsNullOrEmpty(milestone1Sequence.lines[0].subtitleText))
            {
                milestone1Sequence = new NarrativeSequence
                {
                    sequenceId = "Milestone_1",
                    title = "Milestone 1: First Movement (The Wish & The Climb)",
                    lines = new List<NarrativeDialogueLine>
                    {
                        new NarrativeDialogueLine
                        {
                            speaker = "Mary's Letter",
                            subtitleText = "My love, if you are reading this, then I am no longer with you. Yet I have one last wish from you: let me rest on the mountain's top. It is a steep and punishing climb, but use the heavy trees along the path to anchor your line, and bring me to the sky.",
                            pauseAfter = 1.0f
                        }
                    }
                };
            }

            // Milestone 2: First Anchor Placed (The Worth of Limited Time)
            if (milestone2Sequence == null || milestone2Sequence.lines == null || milestone2Sequence.lines.Count == 0 || string.IsNullOrEmpty(milestone2Sequence.lines[0].subtitleText))
            {
                milestone2Sequence = new NarrativeSequence
                {
                    sequenceId = "Milestone_2",
                    title = "Milestone 2: First Anchor Placed (The Worth of Limited Time)",
                    lines = new List<NarrativeDialogueLine>
                    {
                        new NarrativeDialogueLine
                        {
                            speaker = "Mary's Letter",
                            subtitleText = "We didn't get a lifetime of years together, but every single second we shared meant the entire world to me. A love isn't measured by how long it endures, but by how deeply it was felt—and what we had was worth every heartbeat, even if our time was short.",
                            pauseAfter = 1.0f
                        }
                    }
                };
            }

            // Milestone 3: Safe Zone 1 (~20% Elevation — The Confession)
            if (milestone3Sequence == null || milestone3Sequence.lines == null || milestone3Sequence.lines.Count == 0 || string.IsNullOrEmpty(milestone3Sequence.lines[0].subtitleText))
            {
                milestone3Sequence = new NarrativeSequence
                {
                    sequenceId = "Milestone_3",
                    title = "Milestone 3: Safe Zone 1 (~20% Elevation — The Confession)",
                    lines = new List<NarrativeDialogueLine>
                    {
                        new NarrativeDialogueLine
                        {
                            speaker = "Mary's Letter",
                            subtitleText = "I am so sorry I didn't tell you what was coming. I knew my time was slipping away, that these past few days were our very last, and keeping that secret was the heaviest burden I have ever carried.",
                            pauseAfter = 1.0f
                        }
                    }
                };
            }

            // Milestone 4: Safe Zone 2 (~40% Elevation — Choosing Joy Over Anticipated Grief)
            if (milestone4Sequence == null || milestone4Sequence.lines == null || milestone4Sequence.lines.Count == 0 || string.IsNullOrEmpty(milestone4Sequence.lines[0].subtitleText))
            {
                milestone4Sequence = new NarrativeSequence
                {
                    sequenceId = "Milestone_4",
                    title = "Milestone 4: Safe Zone 2 (~40% Elevation — Choosing Joy Over Anticipated Grief)",
                    lines = new List<NarrativeDialogueLine>
                    {
                        new NarrativeDialogueLine
                        {
                            speaker = "Mary's Letter",
                            subtitleText = "I couldn’t bear the thought of watching our final days turn into mourning before I was even gone. I didn't want our hours poisoned by fear or counting down the clock; I just wanted to live with you, truly and happily, right until the quiet took me.",
                            pauseAfter = 1.0f
                        }
                    }
                };
            }

            // Milestone 5: Safe Zone 3 (~60% Elevation — Forgiveness & Sacred Days)
            if (milestone5Sequence == null || milestone5Sequence.lines == null || milestone5Sequence.lines.Count == 0 || string.IsNullOrEmpty(milestone5Sequence.lines[0].subtitleText))
            {
                milestone5Sequence = new NarrativeSequence
                {
                    sequenceId = "Milestone_5",
                    title = "Milestone 5: Safe Zone 3 (~60% Elevation — Forgiveness & Sacred Days)",
                    lines = new List<NarrativeDialogueLine>
                    {
                        new NarrativeDialogueLine
                        {
                            speaker = "Mary's Letter",
                            subtitleText = "I pray you can forgive me for hiding it. Knowing our ending didn't make our days together mean any less—it made every cup of coffee, every shared look, and every laugh across our table sacred. I would choose those short, fleeting days with you a thousand times over.",
                            pauseAfter = 1.0f
                        }
                    }
                };
            }

            // Milestone 6: Safe Zone 4 (~80% Elevation — The Peak in View)
            if (milestone6Sequence == null || milestone6Sequence.lines == null || milestone6Sequence.lines.Count == 0 || string.IsNullOrEmpty(milestone6Sequence.lines[0].subtitleText))
            {
                milestone6Sequence = new NarrativeSequence
                {
                    sequenceId = "Milestone_6",
                    title = "Milestone 6: Safe Zone 4 (~80% Elevation — The Peak in View)",
                    lines = new List<NarrativeDialogueLine>
                    {
                        new NarrativeDialogueLine
                        {
                            speaker = "Mary's Letter",
                            subtitleText = "Look how high your love has carried us; the trees are parting, and the entire valley is glowing below. The climb is almost over, my darling. Catch your breath, and give me one last pull to the ridge.",
                            pauseAfter = 1.0f
                        }
                    }
                };
            }

            // Milestone 7: Final Area (The Rest & Farewell)
            if (milestone7Sequence == null || milestone7Sequence.lines == null || milestone7Sequence.lines.Count == 0 || string.IsNullOrEmpty(milestone7Sequence.lines[0].subtitleText))
            {
                milestone7Sequence = new NarrativeSequence
                {
                    sequenceId = "Milestone_7",
                    title = "Milestone 7: Final Area (The Rest & Farewell)",
                    lines = new List<NarrativeDialogueLine>
                    {
                        new NarrativeDialogueLine
                        {
                            speaker = "Mary's Letter",
                            subtitleText = "Now is the time for me to rest. And remember that our ending doesn't erase the beauty of what we had. Thank you for everything, my love. Goodbye.",
                            pauseAfter = 1.0f
                        }
                    }
                };
            }

#if UNITY_EDITOR
            LoadEditorClipsIfMissing();
#endif
        }

#if UNITY_EDITOR
        public void LoadEditorClipsIfMissing()
        {
            AssignClipIfMissing(milestone1Sequence, "Assets/Audio/Voice Over/ElevenLabs_2026-10-18T12_07_28_MaryPart_1.mp3");
            AssignClipIfMissing(milestone2Sequence, "Assets/Audio/Voice Over/ElevenLabs_2026-10-18T12_08_44_Mary_gen_s50_m2_Part2.mp3");
            AssignClipIfMissing(milestone3Sequence, "Assets/Audio/Voice Over/ElevenLabs_2026-10-18T12_10_00_Mary_gen_s50_m2_Part3.mp3");
            AssignClipIfMissing(milestone4Sequence, "Assets/Audio/Voice Over/ElevenLabs_2026-10-18T12_11_08_Mary_gen_s50_m2_Part4.mp3");
            AssignClipIfMissing(milestone5Sequence, "Assets/Audio/Voice Over/ElevenLabs_2026-10-18T12_12_25_Mary_gen_s50_m2_Part5.mp3");
            AssignClipIfMissing(milestone6Sequence, "Assets/Audio/Voice Over/ElevenLabs_2026-10-18T12_13_24_Mary_gen_s50_m2_Part6.mp3");
            AssignClipIfMissing(milestone7Sequence, "Assets/Audio/Voice Over/ElevenLabs_2026-10-18T12_18_49_Mary_gen_s50_m2_Part7.mp3");
        }

        private void AssignClipIfMissing(NarrativeSequence sequence, string assetPath)
        {
            if (sequence == null || sequence.lines == null || sequence.lines.Count == 0) return;
            if (sequence.lines[0].voiceClip == null)
            {
                var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
                if (clip != null)
                {
                    sequence.lines[0].voiceClip = clip;
                }
            }
        }
#endif
    }
}

