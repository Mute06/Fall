using UnityEditor;
using UnityEngine;

namespace FirstPersonSystem.Narrative.Editor
{
    [CustomEditor(typeof(NarrativeManager))]
    public class NarrativeManagerEditor : UnityEditor.Editor
    {
        private SerializedProperty voiceAudioSourceProp;
        private SerializedProperty voiceVolumeProp;
        private SerializedProperty delayBetweenSequencesProp;

        private GUIStyle statusHeaderStyle;
        private GUIStyle statusBoxStyle;
        private bool stylesInitialized = false;

        private void OnEnable()
        {
            voiceAudioSourceProp = serializedObject.FindProperty("voiceAudioSource");
            voiceVolumeProp = serializedObject.FindProperty("voiceVolume");
            delayBetweenSequencesProp = serializedObject.FindProperty("delayBetweenSequences");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            InitStyles();

            NarrativeManager manager = (NarrativeManager)target;

            // Header Section
            EditorGUILayout.LabelField("NARRATIVE VOICE-OVER & QUEUE SYSTEM", statusHeaderStyle);
            EditorGUILayout.Space(4);

            // Default Properties
            EditorGUILayout.PropertyField(voiceAudioSourceProp);
            EditorGUILayout.PropertyField(voiceVolumeProp);
            EditorGUILayout.PropertyField(delayBetweenSequencesProp);

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(8);

            // Live Runtime Playback Monitor
            if (Application.isPlaying)
            {
                EditorGUILayout.BeginVertical(statusBoxStyle);
                EditorGUILayout.LabelField("Runtime Playback Status", EditorStyles.boldLabel);

                if (manager.IsPlaying)
                {
                    EditorGUILayout.LabelField("State:", "PLAYING VOICE-OVER", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("Current Sequence:", manager.CurrentSequence != null ? manager.CurrentSequence.title : "None");
                    EditorGUILayout.LabelField("Current Speaker:", manager.CurrentSpeaker);
                    EditorGUILayout.LabelField("Progress:", $"Line {manager.CurrentLineIndex} of {manager.TotalLinesInSequence}");

                    EditorGUILayout.HelpBox($"\"{manager.CurrentSubtitle}\"", MessageType.Info);

                    EditorGUILayout.LabelField("Queued Sequences:", manager.QueuedCount.ToString());

                    if (GUILayout.Button("Stop / Clear Queue"))
                    {
                        manager.StopAndClear();
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("State:", "IDLE (Waiting for trigger)");
                    EditorGUILayout.LabelField("Queued Sequences:", manager.QueuedCount.ToString());
                }

                EditorGUILayout.EndVertical();
            }
        }

        private void InitStyles()
        {
            if (stylesInitialized && statusHeaderStyle != null) return;

            statusHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft
            };

            statusBoxStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 10, 10)
            };

            stylesInitialized = true;
        }

        [MenuItem("GameObject/Narrative System/Setup Narrative Coordinator in Scene", false, 10)]
        public static void SetupNarrativeCoordinator()
        {
            GameObject host = GameObject.Find("[NarrativeSystem]");
            if (host == null)
            {
                host = new GameObject("[NarrativeSystem]");
                Undo.RegisterCreatedObjectUndo(host, "Create Narrative Coordinator");
            }

            AudioSource audioSource = host.GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = host.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f;
            }

            NarrativeManager navMgr = host.GetComponent<NarrativeManager>();
            if (navMgr == null) navMgr = host.AddComponent<NarrativeManager>();

            QuestManager questMgr = host.GetComponent<QuestManager>();
            if (questMgr == null) questMgr = host.AddComponent<QuestManager>();

            NarrativeTriggerManager triggerMgr = host.GetComponent<NarrativeTriggerManager>();
            if (triggerMgr == null) triggerMgr = host.AddComponent<NarrativeTriggerManager>();

            SummitMusicController musicCtrl = host.GetComponent<SummitMusicController>();
            if (musicCtrl == null) musicCtrl = host.AddComponent<SummitMusicController>();

            Selection.activeGameObject = host;
            Debug.Log("[NarrativeSystem] Narrative Coordinator successfully set up in active scene!");
        }
    }
}
