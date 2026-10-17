#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace FirstPersonSystem.ProceduralGeneration.Editor
{
    [CustomEditor(typeof(ProceduralMountainGenerator))]
    public class ProceduralMountainGeneratorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            ProceduralMountainGenerator generator = (ProceduralMountainGenerator)target;

            EditorGUILayout.Space(12f);
            EditorGUILayout.LabelField("Procedural Generation Actions", EditorStyles.boldLabel);

            if (generator.Profile == null)
            {
                EditorGUILayout.HelpBox(
                    "No MountainGenerationProfile assigned! Click below to create a configurable Profile asset, or generate using default settings.",
                    MessageType.Info);

                if (GUILayout.Button("Create & Assign New Mountain Generation Profile", GUILayout.Height(28f)))
                {
                    CreateAndAssignProfile(generator);
                }
            }
            else
            {
                EditorGUILayout.Space(6f);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"Active Profile: {generator.Profile.name}", EditorStyles.boldLabel);
                int matCount = generator.Profile.treeMaterials != null ? generator.Profile.treeMaterials.Count : 0;
                string dist = generator.Profile.randomizeTreeMaterials ? generator.Profile.treeMaterialDistribution.ToString() : "Disabled";
                EditorGUILayout.LabelField($"Tree Materials Pool: {matCount} material(s) | Distribution: {dist}", EditorStyles.miniLabel);

                if (GUILayout.Button("Select Profile in Project Window", GUILayout.Height(22f)))
                {
                    Selection.activeObject = generator.Profile;
                    EditorGUIUtility.PingObject(generator.Profile);
                }
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.35f, 0.85f, 0.45f);
            if (GUILayout.Button("Generate Mountain", GUILayout.Height(34f)))
            {
                Undo.RegisterFullObjectHierarchyUndo(generator.gameObject, "Generate Procedural Mountain");
                generator.GenerateMountain();
                EditorUtility.SetDirty(generator);
            }

            GUI.backgroundColor = new Color(0.35f, 0.7f, 0.95f);
            if (GUILayout.Button("Randomize Seed & Generate", GUILayout.Height(34f)))
            {
                Undo.RegisterFullObjectHierarchyUndo(generator.gameObject, "Randomize & Generate Procedural Mountain");
                generator.RandomizeSeedAndGenerate();
                EditorUtility.SetDirty(generator);
            }
            EditorGUILayout.EndHorizontal();

            GUI.backgroundColor = new Color(0.95f, 0.45f, 0.45f);
            if (GUILayout.Button("Clear Spawned Objects", GUILayout.Height(26f)))
            {
                Undo.RegisterFullObjectHierarchyUndo(generator.gameObject, "Clear Spawned Mountain Objects");
                generator.ClearSpawnedObjects();
                EditorUtility.SetDirty(generator);
            }

            GUI.backgroundColor = Color.white;
        }

        private void CreateAndAssignProfile(ProceduralMountainGenerator generator)
        {
            string defaultPath = "Assets/MountainGenerationProfile.asset";
            defaultPath = AssetDatabase.GenerateUniqueAssetPath(defaultPath);

            MountainGenerationProfile asset = ScriptableObject.CreateInstance<MountainGenerationProfile>();
            AssetDatabase.CreateAsset(asset, defaultPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            SerializedObject so = new SerializedObject(generator);
            SerializedProperty profileProp = so.FindProperty("profile");
            if (profileProp != null)
            {
                profileProp.objectReferenceValue = asset;
                so.ApplyModifiedProperties();
            }

            EditorGUIUtility.PingObject(asset);
            Debug.Log($"[ProceduralMountainGeneratorEditor] Created and assigned profile at '{defaultPath}'.");
        }
    }
}
#endif
