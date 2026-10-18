using UnityEditor;
using UnityEngine;

namespace FirstPersonSystem.Narrative.Editor
{
    [CustomEditor(typeof(NarrativeTriggerManager))]
    public class NarrativeTriggerManagerEditor : UnityEditor.Editor
    {
        private SerializedProperty playerTransformProp;
        private SerializedProperty coffinTransformProp;
        private SerializedProperty mountainGeneratorProp;
        private SerializedProperty fallbackProfileProp;

        private SerializedProperty firstMovementSequenceProp;
        private SerializedProperty firstWinchPlacementSequenceProp;
        private SerializedProperty tier1SafeZoneSequenceProp;
        private SerializedProperty tier2SafeZoneSequenceProp;
        private SerializedProperty tier3SafeZoneSequenceProp;
        private SerializedProperty tier4SafeZoneSequenceProp;
        private SerializedProperty summitSequenceProp;

        private bool showWorldRefs = true;
        private bool showTriggers = true;

        private void OnEnable()
        {
            playerTransformProp = serializedObject.FindProperty("playerTransform");
            coffinTransformProp = serializedObject.FindProperty("coffinTransform");
            mountainGeneratorProp = serializedObject.FindProperty("mountainGenerator");
            fallbackProfileProp = serializedObject.FindProperty("fallbackProfile");

            firstMovementSequenceProp = serializedObject.FindProperty("milestone1Sequence") ?? serializedObject.FindProperty("firstMovementSequence");
            firstWinchPlacementSequenceProp = serializedObject.FindProperty("milestone2Sequence") ?? serializedObject.FindProperty("firstWinchPlacementSequence");
            tier1SafeZoneSequenceProp = serializedObject.FindProperty("milestone3Sequence") ?? serializedObject.FindProperty("tier1SafeZoneSequence");
            tier2SafeZoneSequenceProp = serializedObject.FindProperty("milestone4Sequence") ?? serializedObject.FindProperty("tier2SafeZoneSequence");
            tier3SafeZoneSequenceProp = serializedObject.FindProperty("milestone5Sequence") ?? serializedObject.FindProperty("tier3SafeZoneSequence");
            tier4SafeZoneSequenceProp = serializedObject.FindProperty("milestone6Sequence") ?? serializedObject.FindProperty("tier4SafeZoneSequence");
            summitSequenceProp = serializedObject.FindProperty("milestone7Sequence") ?? serializedObject.FindProperty("summitSequence");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("NARRATIVE TRIGGER SYSTEM (7 MILESTONES)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Triggers wife's voice-over letter narratives sequentially. Queues overlapping triggers automatically.", MessageType.Info);
            EditorGUILayout.Space(6);

            // References
            showWorldRefs = EditorGUILayout.Foldout(showWorldRefs, "Scene & World References", true);
            if (showWorldRefs)
            {
                EditorGUI.indentLevel++;
                if (playerTransformProp != null) EditorGUILayout.PropertyField(playerTransformProp);
                if (coffinTransformProp != null) EditorGUILayout.PropertyField(coffinTransformProp);
                if (mountainGeneratorProp != null) EditorGUILayout.PropertyField(mountainGeneratorProp);
                if (fallbackProfileProp != null) EditorGUILayout.PropertyField(fallbackProfileProp);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(6);

            // Triggers
            showTriggers = EditorGUILayout.Foldout(showTriggers, "Narrative Sequences & Voice Lines", true);
            if (showTriggers)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("1. Start & Winch", EditorStyles.boldLabel);
                if (firstMovementSequenceProp != null) EditorGUILayout.PropertyField(firstMovementSequenceProp, new GUIContent("1. First Movement"), true);
                if (firstWinchPlacementSequenceProp != null) EditorGUILayout.PropertyField(firstWinchPlacementSequenceProp, new GUIContent("2. First Winch Placed"), true);

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("2. Safe Zone Plateaus (Climb Tiers 1-4)", EditorStyles.boldLabel);
                if (tier1SafeZoneSequenceProp != null) EditorGUILayout.PropertyField(tier1SafeZoneSequenceProp, new GUIContent("3. Tier 1 Safe Zone"), true);
                if (tier2SafeZoneSequenceProp != null) EditorGUILayout.PropertyField(tier2SafeZoneSequenceProp, new GUIContent("4. Tier 2 Safe Zone"), true);
                if (tier3SafeZoneSequenceProp != null) EditorGUILayout.PropertyField(tier3SafeZoneSequenceProp, new GUIContent("5. Tier 3 Safe Zone"), true);
                if (tier4SafeZoneSequenceProp != null) EditorGUILayout.PropertyField(tier4SafeZoneSequenceProp, new GUIContent("6. Tier 4 Safe Zone"), true);

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("3. Summit & Quest", EditorStyles.boldLabel);
                if (summitSequenceProp != null) EditorGUILayout.PropertyField(summitSequenceProp, new GUIContent("7. Summit (Narrative + Quest)"), true);
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();

            // Quick Play-Mode Test Buttons
            if (Application.isPlaying && NarrativeManager.Instance != null && target is NarrativeTriggerManager triggerMgr)
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Test Triggers (Queue Verification)", EditorStyles.boldLabel);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Trigger First Move"))
                {
                    NarrativeManager.Instance.EnqueueSequence(triggerMgr.Milestone1Sequence);
                }
                if (GUILayout.Button("Trigger Winch Placed"))
                {
                    NarrativeManager.Instance.EnqueueSequence(triggerMgr.Milestone2Sequence);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Trigger Safe Tier 1"))
                {
                    NarrativeManager.Instance.EnqueueSequence(triggerMgr.Milestone3Sequence);
                }
                if (GUILayout.Button("Trigger Safe Tier 2"))
                {
                    NarrativeManager.Instance.EnqueueSequence(triggerMgr.Milestone4Sequence);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Trigger Safe Tier 3"))
                {
                    NarrativeManager.Instance.EnqueueSequence(triggerMgr.Milestone5Sequence);
                }
                if (GUILayout.Button("Trigger Safe Tier 4"))
                {
                    NarrativeManager.Instance.EnqueueSequence(triggerMgr.Milestone6Sequence);
                }
                EditorGUILayout.EndHorizontal();

                if (GUILayout.Button("Trigger Summit & Quest"))
                {
                    NarrativeManager.Instance.EnqueueSequence(triggerMgr.Milestone7Sequence);
                }
            }
        }
    }
}
