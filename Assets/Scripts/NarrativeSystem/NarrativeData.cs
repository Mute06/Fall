using System;
using System.Collections.Generic;
using UnityEngine;

namespace FirstPersonSystem.Narrative
{
    [Serializable]
    public class NarrativeDialogueLine
    {
        [Tooltip("Name or descriptor of the speaker (e.g. 'Elena's Letter').")]
        public string speaker = "Elena's Letter";

        [TextArea(2, 5)]
        [Tooltip("The subtitle text displayed on screen.")]
        public string subtitleText = "";

        [Tooltip("Optional voice-over audio clip spoken by the wife. If not assigned, duration will auto-calculate based on text length.")]
        public AudioClip voiceClip;

        [Tooltip("Optional manual duration in seconds if no audio clip is assigned. Set to 0 to auto-calculate based on reading speed.")]
        [Min(0f)]
        public float customDuration = 0f;

        [Tooltip("Pause duration (in seconds) after this line finishes before playing the next line.")]
        [Range(0.1f, 3f)]
        public float pauseAfter = 0.65f;

        /// <summary>
        /// Calculates the effective duration for this line:
        /// Uses the audio clip length if present, or customDuration, or an estimated reading duration.
        /// </summary>
        public float GetEffectiveDuration()
        {
            if (voiceClip != null && voiceClip.length > 0.05f)
            {
                return voiceClip.length;
            }

            if (customDuration > 0.1f)
            {
                return customDuration;
            }

            // Fallback estimation: average reading speed ~20-25 characters per second, minimum 3.2s
            float charCount = string.IsNullOrEmpty(subtitleText) ? 10f : subtitleText.Length;
            return Mathf.Clamp(charCount * 0.065f, 3.2f, 10f);
        }
    }

    [Serializable]
    public class NarrativeSequence
    {
        [Tooltip("Unique ID for this sequence (e.g., 'FirstMove', 'WinchPlaced', 'SafeZone1', 'Summit').")]
        public string sequenceId = "Narrative_Sequence";

        [Tooltip("Human-readable title for Inspector clarity.")]
        public string title = "Letter Fragment";

        [Tooltip("Sequential list of voice-over lines and subtitles played in order.")]
        public List<NarrativeDialogueLine> lines = new List<NarrativeDialogueLine>();

        [Header("Quest Trigger (Optional)")]
        [Tooltip("If true, triggers a quest objective once this sequence completes or begins.")]
        public bool triggerQuest = false;

        [Tooltip("Title of the quest triggered by this sequence.")]
        public string questTitle = "Lay Your Wife to Rest";

        [Tooltip("Description or objective of the quest.")]
        public string questObjective = "Carry and place the coffin at the summit marker.";

        public NarrativeSequence Clone()
        {
            var copy = new NarrativeSequence
            {
                sequenceId = this.sequenceId,
                title = this.title,
                triggerQuest = this.triggerQuest,
                questTitle = this.questTitle,
                questObjective = this.questObjective,
                lines = new List<NarrativeDialogueLine>()
            };

            if (lines != null)
            {
                foreach (var line in lines)
                {
                    copy.lines.Add(new NarrativeDialogueLine
                    {
                        speaker = line.speaker,
                        subtitleText = line.subtitleText,
                        voiceClip = line.voiceClip,
                        customDuration = line.customDuration,
                        pauseAfter = line.pauseAfter
                    });
                }
            }

            return copy;
        }
    }
}
