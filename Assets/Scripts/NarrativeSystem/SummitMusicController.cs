using System;
using System.Collections;
using UnityEngine;

namespace FirstPersonSystem.Narrative
{
    /// <summary>
    /// Controls atmospheric background music when the player reaches the summit / top area.
    /// Plays 'The Weight of Autumn' on loop, smoothly fading in.
    /// Implements real-time audio ducking: lowers music volume while wife's voice-over narrative
    /// is playing, and smoothly restores volume when the voice-over finishes.
    /// </summary>
    public class SummitMusicController : MonoBehaviour
    {
        public static SummitMusicController Instance { get; private set; }

        [Header("Audio Configuration")]
        [Tooltip("Summit ambient music track ('The_Weight_of_Autumn.mp3').")]
        [SerializeField] private AudioClip summitMusicClip;

        [Tooltip("Dedicated AudioSource for music. If null, created automatically.")]
        [SerializeField] private AudioSource musicAudioSource;

        [Header("Volume & Ducking Settings")]
        [Range(0f, 1f)]
        [Tooltip("Standard music playback volume when no voice-over is active.")]
        [SerializeField] private float normalVolume = 0.75f;

        [Range(0f, 1f)]
        [Tooltip("Ducked music volume while the wife's letter voice-over is actively playing.")]
        [SerializeField] private float duckedVolume = 0.18f;

        [Tooltip("Duration in seconds for the music to smoothly fade in when first triggered.")]
        [SerializeField] private float fadeInDuration = 3.5f;

        [Tooltip("Speed multiplier for ducking volume down when voice-over starts.")]
        [SerializeField] private float duckSpeed = 2.5f;

        [Tooltip("Speed multiplier for swelling volume back up when voice-over ends.")]
        [SerializeField] private float unduckSpeed = 1.2f;

        [Header("Trigger Detection")]
        [Tooltip("Transform of the player to monitor for reaching the summit plateau.")]
        [SerializeField] private Transform playerTransform;

        [Tooltip("Distance before summit boundary to trigger music.")]
        [SerializeField] private float summitThresholdOffset = 4.0f;

        // State
        private bool hasTriggered = false;
        private bool isFadingIn = false;
        private float currentVolume = 0f;
        private float fadeInTimer = 0f;
        private float targetSummitZ = 250f;

        public bool HasTriggered => hasTriggered;
        public bool IsPlayingMusic => musicAudioSource != null && musicAudioSource.isPlaying;
        public float CurrentVolume => currentVolume;
        public float NormalVolume => normalVolume;
        public float DuckedVolume => duckedVolume;
        public AudioSource MusicAudioSource => musicAudioSource;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            LoadAudioClipIfMissing();
            EnsureAudioSource();
        }

        private void Start()
        {
            LocateReferences();

            // Subscribe to narrative & quest events
            NarrativeTriggerManager.OnSummitAreaReached += HandleSummitReached;

            if (QuestManager.Instance != null)
            {
                QuestManager.Instance.OnQuestActivated += HandleQuestActivated;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            NarrativeTriggerManager.OnSummitAreaReached -= HandleSummitReached;

            if (QuestManager.Instance != null)
            {
                QuestManager.Instance.OnQuestActivated -= HandleQuestActivated;
            }
        }

        public void LoadAudioClipIfMissing()
        {
#if UNITY_EDITOR
            if (summitMusicClip == null)
            {
                summitMusicClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/The_Weight_of_Autumn.mp3");
            }
#endif
        }

        public void EnsureAudioSource()
        {
            if (musicAudioSource == null)
            {
                // Check child or component
                Transform child = transform.Find("SummitMusicAudioSource");
                if (child != null)
                {
                    musicAudioSource = child.GetComponent<AudioSource>();
                }

                if (musicAudioSource == null)
                {
                    GameObject audioObj = new GameObject("SummitMusicAudioSource");
                    audioObj.transform.SetParent(transform, false);
                    musicAudioSource = audioObj.AddComponent<AudioSource>();
                }
            }

            musicAudioSource.playOnAwake = false;
            musicAudioSource.loop = true;
            musicAudioSource.spatialBlend = 0f; // 2D full stereo ambient music
            musicAudioSource.bypassEffects = true;
            musicAudioSource.bypassListenerEffects = true;
            musicAudioSource.bypassReverbZones = true;

            if (summitMusicClip != null && musicAudioSource.clip == null)
            {
                musicAudioSource.clip = summitMusicClip;
            }
        }

        public void LocateReferences()
        {
            if (playerTransform == null)
            {
                var controller = FindAnyObjectByType<FirstPersonController>();
                if (controller != null) playerTransform = controller.transform;
                else if (Camera.main != null) playerTransform = Camera.main.transform;
            }

            if (NarrativeTriggerManager.Instance != null)
            {
                targetSummitZ = NarrativeTriggerManager.Instance.SummitZ;
            }
            else
            {
                var mountainGen = FindAnyObjectByType<ProceduralGeneration.ProceduralMountainGenerator>();
                if (mountainGen != null && mountainGen.Profile != null)
                {
                    targetSummitZ = mountainGen.Profile.pathEndXZ.y;
                }
            }
        }

        private void HandleSummitReached()
        {
            PlaySummitMusic();
        }

        private void HandleQuestActivated(string title, string objective)
        {
            PlaySummitMusic();
        }

        private void Update()
        {
            if (!hasTriggered)
            {
                CheckPlayerPosition();
                return;
            }

            EvaluateDucking(Time.deltaTime);
        }

        private void CheckPlayerPosition()
        {
            if (playerTransform == null)
            {
                LocateReferences();
                if (playerTransform == null) return;
            }

            if (playerTransform.position.z >= (targetSummitZ - summitThresholdOffset))
            {
                PlaySummitMusic();
            }
        }

        /// <summary>
        /// Begins playing 'The Weight of Autumn' on loop with a gentle fade-in.
        /// </summary>
        public void PlaySummitMusic()
        {
            if (hasTriggered) return;
            hasTriggered = true;

            LoadAudioClipIfMissing();
            EnsureAudioSource();

            if (musicAudioSource != null)
            {
                if (summitMusicClip != null)
                {
                    musicAudioSource.clip = summitMusicClip;
                }

                musicAudioSource.loop = true;
                musicAudioSource.volume = 0f;
                currentVolume = 0f;
                fadeInTimer = 0f;
                isFadingIn = true;

                if (!musicAudioSource.isPlaying && musicAudioSource.clip != null)
                {
                    musicAudioSource.Play();
                }

                Debug.Log("[SummitMusicController] Summit top area reached! Started playing 'The Weight of Autumn' on loop.");
            }
        }

        /// <summary>
        /// Evaluates audio volume interpolation and ducking during voice-over lines.
        /// </summary>
        public void EvaluateDucking(float dt)
        {
            if (musicAudioSource == null) return;

            // Ensure playback is active if triggered
            if (!musicAudioSource.isPlaying && musicAudioSource.clip != null)
            {
                musicAudioSource.Play();
            }

            bool isVoiceoverActive = IsVoiceoverActive();
            float desiredVolume = isVoiceoverActive ? duckedVolume : normalVolume;

            if (isFadingIn)
            {
                fadeInTimer += dt;
                float progress = Mathf.Clamp01(fadeInTimer / Mathf.Max(0.1f, fadeInDuration));
                currentVolume = Mathf.Lerp(0f, desiredVolume, progress);

                if (progress >= 1f)
                {
                    isFadingIn = false;
                }
            }
            else
            {
                float rate = isVoiceoverActive ? duckSpeed : unduckSpeed;
                currentVolume = Mathf.MoveTowards(currentVolume, desiredVolume, dt * rate);
            }

            musicAudioSource.volume = currentVolume;
        }

        /// <summary>
        /// Checks if a voice-over dialogue line is actively being played by the NarrativeManager.
        /// </summary>
        public bool IsVoiceoverActive()
        {
            if (NarrativeManager.Instance == null) return false;

            // When NarrativeManager has an active sequence running
            if (NarrativeManager.Instance.IsPlaying)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Stops the summit music (useful for testing or scene transitions).
        /// </summary>
        public void StopSummitMusic()
        {
            hasTriggered = false;
            isFadingIn = false;
            currentVolume = 0f;

            if (musicAudioSource != null && musicAudioSource.isPlaying)
            {
                musicAudioSource.Stop();
            }
        }

        public void SetVolumes(float normal, float ducked)
        {
            normalVolume = normal;
            duckedVolume = ducked;
        }
    }
}
