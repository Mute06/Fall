using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FirstPersonSystem.Narrative
{
    /// <summary>
    /// Core narrative coordinator for the walking simulator.
    /// Manages letter voice-overs and subtitles, sequencing lines, and queuing narrative triggers.
    /// </summary>
    public class NarrativeManager : MonoBehaviour
    {
        public static NarrativeManager Instance { get; set; }

        [Header("Audio Configuration")]
        [Tooltip("AudioSource used to play the wife's voice-over recordings.")]
        [SerializeField] private AudioSource voiceAudioSource;

        [Range(0f, 1f)]
        [SerializeField] private float voiceVolume = 1f;

        [Header("Timing & Flow")]
        [Tooltip("Delay in seconds between different queued narrative sequences.")]
        [SerializeField] private float delayBetweenSequences = 1.0f;

        // Queue & State
        private readonly Queue<NarrativeSequence> sequenceQueue = new Queue<NarrativeSequence>();
        private Coroutine playbackCoroutine;

        // Current Playback State
        private bool isPlaying = false;
        private NarrativeSequence currentSequence;
        private NarrativeDialogueLine currentLine;
        private int currentLineIndex = 0;
        private int totalLinesInSequence = 0;
        private string currentSpeaker = "";
        private string currentSubtitle = "";

        // Events
        public event Action<NarrativeSequence> OnSequenceStarted;
        public event Action<NarrativeSequence> OnSequenceCompleted;
        public event Action<NarrativeDialogueLine> OnLineStarted;
        public event Action<NarrativeDialogueLine> OnLineEnded;
        public event Action<string, string> OnQuestTriggerRequested;
        public event Action OnQueueEmptied;

        // Public Properties
        public bool IsPlaying => isPlaying;
        public NarrativeSequence CurrentSequence => currentSequence;
        public NarrativeDialogueLine CurrentLine => currentLine;
        public int CurrentLineIndex => currentLineIndex;
        public int TotalLinesInSequence => totalLinesInSequence;
        public string CurrentSpeaker => currentSpeaker;
        public string CurrentSubtitle => currentSubtitle;
        public int QueuedCount => sequenceQueue.Count;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (voiceAudioSource == null)
            {
                voiceAudioSource = GetComponent<AudioSource>();
                if (voiceAudioSource == null)
                {
                    voiceAudioSource = gameObject.AddComponent<AudioSource>();
                    voiceAudioSource.playOnAwake = false;
                    voiceAudioSource.spatialBlend = 0f; // 2D narrator voice inside player's thoughts/letter
                    voiceAudioSource.bypassEffects = true;
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Enqueues a narrative sequence. If nothing is currently playing, starts playback immediately.
        /// If already playing, queues the sequence to play as soon as previous sequences complete.
        /// </summary>
        public void EnqueueSequence(NarrativeSequence sequence)
        {
            if (sequence == null || sequence.lines == null || sequence.lines.Count == 0)
            {
                Debug.LogWarning("[NarrativeManager] Attempted to enqueue empty or null narrative sequence.");
                return;
            }

            sequenceQueue.Enqueue(sequence);
            Debug.Log($"[NarrativeManager] Enqueued narrative sequence '{sequence.sequenceId}' ('{sequence.title}'). Queue length: {sequenceQueue.Count}");

            if (!isPlaying)
            {
                playbackCoroutine = StartCoroutine(ProcessNarrativeQueue());
            }
        }

        /// <summary>
        /// Immediately interrupts any playing narrative, clears the queue, and plays this sequence.
        /// </summary>
        public void PlaySequenceImmediate(NarrativeSequence sequence)
        {
            StopAllCoroutines();
            sequenceQueue.Clear();
            if (voiceAudioSource != null && voiceAudioSource.isPlaying)
            {
                voiceAudioSource.Stop();
            }

            isPlaying = false;
            currentSubtitle = "";
            currentSpeaker = "";

            EnqueueSequence(sequence);
        }

        private IEnumerator ProcessNarrativeQueue()
        {
            isPlaying = true;

            while (sequenceQueue.Count > 0)
            {
                currentSequence = sequenceQueue.Dequeue();
                totalLinesInSequence = currentSequence.lines.Count;
                currentLineIndex = 0;

                Debug.Log($"[NarrativeManager] Starting narrative sequence '{currentSequence.sequenceId}' with {totalLinesInSequence} lines.");
                OnSequenceStarted?.Invoke(currentSequence);

                for (int i = 0; i < currentSequence.lines.Count; i++)
                {
                    currentLine = currentSequence.lines[i];
                    currentLineIndex = i + 1;
                    currentSpeaker = currentLine.speaker;
                    currentSubtitle = currentLine.subtitleText;

                    OnLineStarted?.Invoke(currentLine);

                    // Voice-over playback
                    float displayDuration = currentLine.GetEffectiveDuration();
                    if (currentLine.voiceClip != null && voiceAudioSource != null)
                    {
                        voiceAudioSource.volume = voiceVolume;
                        voiceAudioSource.clip = currentLine.voiceClip;
                        voiceAudioSource.Play();
                    }

                    // Wait for the duration of the line
                    yield return new WaitForSeconds(displayDuration);

                    // Clear subtitle at end of line
                    currentSubtitle = "";
                    OnLineEnded?.Invoke(currentLine);

                    // Stop audio if it was lingering
                    if (voiceAudioSource != null && voiceAudioSource.isPlaying)
                    {
                        voiceAudioSource.Stop();
                    }

                    // Inter-line pause
                    if (currentLine.pauseAfter > 0.01f)
                    {
                        yield return new WaitForSeconds(currentLine.pauseAfter);
                    }
                }

                // Sequence finished
                Debug.Log($"[NarrativeManager] Completed narrative sequence '{currentSequence.sequenceId}'.");
                OnSequenceCompleted?.Invoke(currentSequence);

                // Quest trigger request if flagged
                if (currentSequence.triggerQuest)
                {
                    OnQuestTriggerRequested?.Invoke(currentSequence.questTitle, currentSequence.questObjective);
                }

                currentSequence = null;
                currentLine = null;

                // Brief pause before next sequence in queue
                if (sequenceQueue.Count > 0 && delayBetweenSequences > 0.01f)
                {
                    yield return new WaitForSeconds(delayBetweenSequences);
                }
            }

            isPlaying = false;
            currentSpeaker = "";
            currentSubtitle = "";
            playbackCoroutine = null;
            OnQueueEmptied?.Invoke();
            Debug.Log("[NarrativeManager] All queued narratives completed.");
        }

        /// <summary>
        /// Clears all remaining queued sequences and stops current audio.
        /// </summary>
        public void StopAndClear()
        {
            if (playbackCoroutine != null)
            {
                StopCoroutine(playbackCoroutine);
                playbackCoroutine = null;
            }

            sequenceQueue.Clear();
            if (voiceAudioSource != null && voiceAudioSource.isPlaying)
            {
                voiceAudioSource.Stop();
            }

            isPlaying = false;
            currentSequence = null;
            currentLine = null;
            currentSpeaker = "";
            currentSubtitle = "";
            OnQueueEmptied?.Invoke();
        }
    }
}
