using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class SetupFallingLeaves
{
    private const string SourceImagePath = @"C:\Users\umutk\.gemini\antigravity-cli\brain\ce0684c0-61bf-4aa2-8812-6e205dc9eccf\autumn_leaves_spritesheet_clean_1792331951981.jpg";
    private const string TextureDir = "Assets/Textures/FallingLeaves";
    private const string TextureAssetPath = "Assets/Textures/FallingLeaves/T_AutumnLeaves_Sheet.png";
    private const string MaterialDir = "Assets/Materials";
    private const string MaterialAssetPath = "Assets/Materials/M_FallingLeaves.mat";
    private const string PrefabPath = "Assets/FP_Template/FP_Assets/Prefabs/FirstPersonController.prefab";

    public static string Execute()
    {
        var logs = new System.Text.StringBuilder();

        // Step 1: Process texture and save as transparent PNG
        if (!Directory.Exists(TextureDir))
        {
            Directory.CreateDirectory(TextureDir);
        }

        if (!File.Exists(SourceImagePath))
        {
            return $"Error: Source image not found at {SourceImagePath}";
        }

        byte[] rawBytes = File.ReadAllBytes(SourceImagePath);
        Texture2D srcTex = new Texture2D(2, 2);
        if (!srcTex.LoadImage(rawBytes))
        {
            return "Error: Failed to load raw image into Texture2D";
        }

        int width = srcTex.width;
        int height = srcTex.height;
        Color[] pixels = srcTex.GetPixels();
        Color[] newPixels = new Color[pixels.Length];

        // Key out white background with anti-aliasing & color de-fringing
        for (int i = 0; i < pixels.Length; i++)
        {
            Color c = pixels[i];
            // Brightness / distance from white
            float maxComponent = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            float minComponent = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
            
            // Background is pure white / near white (high brightness and low saturation)
            float saturation = maxComponent > 0.001f ? (maxComponent - minComponent) / maxComponent : 0f;
            
            // If near white and desaturated
            if (c.r > 0.88f && c.g > 0.88f && c.b > 0.88f && saturation < 0.15f)
            {
                // Smooth falloff for edges
                float diff = 1f - minComponent; // 0 for pure white, up to 0.12
                float alpha = Mathf.Clamp01(diff / 0.08f);
                if (diff < 0.03f)
                {
                    alpha = 0f;
                }

                // De-fringe color towards average leaf warm tone so edge transparency doesn't produce white halos
                Color defringed = new Color(c.r * 0.95f, c.g * 0.7f, c.b * 0.2f, alpha);
                newPixels[i] = defringed;
            }
            else
            {
                newPixels[i] = new Color(c.r, c.g, c.b, 1f);
            }
        }

        Texture2D outTex = new Texture2D(width, height, TextureFormat.RGBA32, true);
        outTex.SetPixels(newPixels);
        outTex.Apply();

        byte[] pngBytes = outTex.EncodeToPNG();
        File.WriteAllBytes(TextureAssetPath, pngBytes);
        AssetDatabase.ImportAsset(TextureAssetPath, ImportAssetOptions.ForceUpdate);

        // Configure TextureImporter
        TextureImporter importer = AssetImporter.GetAtPath(TextureAssetPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.sRGBTexture = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
            logs.AppendLine($"Texture imported successfully at {TextureAssetPath} ({width}x{height})");
        }
        else
        {
            logs.AppendLine($"Warning: Could not get TextureImporter for {TextureAssetPath}");
        }

        Texture2D leafTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureAssetPath);

        // Step 2: Create / configure Material
        if (!Directory.Exists(MaterialDir))
        {
            Directory.CreateDirectory(MaterialDir);
        }

        Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (particleShader == null)
        {
            particleShader = Shader.Find("Particles/Standard Unlit");
        }

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialAssetPath);
        if (mat == null)
        {
            mat = new Material(particleShader);
            AssetDatabase.CreateAsset(mat, MaterialAssetPath);
        }
        else
        {
            mat.shader = particleShader;
        }

        mat.mainTexture = leafTexture;
        if (mat.HasProperty("_BaseMap"))
        {
            mat.SetTexture("_BaseMap", leafTexture);
        }

        // Configure URP Particles Unlit for transparency
        if (mat.HasProperty("_Surface"))
        {
            mat.SetFloat("_Surface", 1f); // Transparent
        }
        if (mat.HasProperty("_Blend"))
        {
            mat.SetFloat("_Blend", 0f); // Alpha blend
        }
        if (mat.HasProperty("_Cull"))
        {
            mat.SetFloat("_Cull", 0f); // Double-sided (Off)
        }
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        logs.AppendLine($"Material configured with shader {particleShader.name} at {MaterialAssetPath}");

        // Step 3: Configure Particle System on Prefab & Scene
        ConfigurePrefab(mat, logs);
        ConfigureSceneInstance(mat, logs);

        return logs.ToString();
    }

    private static void ConfigurePrefab(Material mat, System.Text.StringBuilder logs)
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (prefabRoot == null)
        {
            logs.AppendLine($"Warning: Could not load prefab at {PrefabPath}");
            return;
        }

        Transform existingChild = prefabRoot.transform.Find("FallingLeaves_ParticleSystem");
        GameObject psGo;
        if (existingChild != null)
        {
            psGo = existingChild.gameObject;
            logs.AppendLine("Found existing FallingLeaves_ParticleSystem in prefab, updating settings.");
        }
        else
        {
            psGo = new GameObject("FallingLeaves_ParticleSystem");
            psGo.transform.SetParent(prefabRoot.transform, false);
            logs.AppendLine("Created new FallingLeaves_ParticleSystem child in prefab.");
        }

        // Position 7 units above player
        psGo.transform.localPosition = new Vector3(0f, 7f, 0f);
        psGo.transform.localRotation = Quaternion.identity;
        psGo.transform.localScale = Vector3.one;

        SetupParticleSystemComponents(psGo, mat);

        PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
        PrefabUtility.UnloadPrefabContents(prefabRoot);
        logs.AppendLine($"Saved updated prefab to {PrefabPath}");
    }

    private static void ConfigureSceneInstance(Material mat, System.Text.StringBuilder logs)
    {
        var fpcInScene = GameObject.FindAnyObjectByType<FirstPersonSystem.FirstPersonController>();
        if (fpcInScene == null)
        {
            logs.AppendLine("No FirstPersonController found in active scene.");
            return;
        }

        Transform existingChild = fpcInScene.transform.Find("FallingLeaves_ParticleSystem");
        GameObject psGo;
        if (existingChild != null)
        {
            psGo = existingChild.gameObject;
            logs.AppendLine("Found existing FallingLeaves_ParticleSystem in scene FPC, updating settings.");
        }
        else
        {
            psGo = new GameObject("FallingLeaves_ParticleSystem");
            psGo.transform.SetParent(fpcInScene.transform, false);
            logs.AppendLine("Created new FallingLeaves_ParticleSystem child under active scene FirstPersonController.");
        }

        psGo.transform.localPosition = new Vector3(0f, 7f, 0f);
        psGo.transform.localRotation = Quaternion.identity;
        psGo.transform.localScale = Vector3.one;

        SetupParticleSystemComponents(psGo, mat);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        logs.AppendLine($"Saved active scene {SceneManager.GetActiveScene().name} with FallingLeaves_ParticleSystem attached.");
    }

    public static void SetupParticleSystemComponents(GameObject psGo, Material mat)
    {
        ParticleSystem ps = psGo.GetComponent<ParticleSystem>();
        if (ps == null)
        {
            ps = psGo.AddComponent<ParticleSystem>();
        }

        ParticleSystemRenderer psRenderer = psGo.GetComponent<ParticleSystemRenderer>();
        if (psRenderer == null)
        {
            psRenderer = psGo.AddComponent<ParticleSystemRenderer>();
        }

        // --- Main Module ---
        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World; // World space as requested!
        main.simulationSpeed = 0.85f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(7.0f, 11.0f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.0f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.20f, 0.40f);
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
        main.startRotationY = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
        main.gravityModifier = 0.06f; // Slow, gentle downward drift
        main.maxParticles = 250;
        main.loop = true;
        main.playOnAwake = true;

        // Start Color variation with natural autumn tones
        Gradient startGrad = new Gradient();
        startGrad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(new Color(1f, 0.95f, 0.85f), 0.0f), new GradientColorKey(new Color(1f, 0.8f, 0.55f), 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(1.0f, 1.0f) }
        );
        main.startColor = new ParticleSystem.MinMaxGradient(startGrad);

        // --- Emission Module ---
        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(14.0f, 20.0f);

        // --- Shape Module ---
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(26f, 1.5f, 26f); // 26m x 26m box overhead
        shape.randomDirectionAmount = 0.2f;

        // --- Velocity over Lifetime Module (gentle wind breeze) ---
        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.4f, 0.6f);
        velocity.y = new ParticleSystem.MinMaxCurve(-0.2f, 0.1f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.5f);

        // --- Noise Module (Fluttering & Swaying) ---
        var noise = ps.noise;
        noise.enabled = true;
        noise.separateAxes = true;
        noise.strengthX = 0.45f;
        noise.strengthY = 0.15f;
        noise.strengthZ = 0.45f;
        noise.frequency = 0.2f;
        noise.scrollSpeed = 0.35f;
        noise.damping = true;
        noise.octaveCount = 1;

        // --- Rotation over Lifetime Module (Tumbling & Spinning) ---
        var rotLife = ps.rotationOverLifetime;
        rotLife.enabled = true;
        rotLife.separateAxes = true;
        rotLife.x = new ParticleSystem.MinMaxCurve(-60f * Mathf.Deg2Rad, 60f * Mathf.Deg2Rad);
        rotLife.y = new ParticleSystem.MinMaxCurve(-80f * Mathf.Deg2Rad, 80f * Mathf.Deg2Rad);
        rotLife.z = new ParticleSystem.MinMaxCurve(-100f * Mathf.Deg2Rad, 100f * Mathf.Deg2Rad);

        // --- Color over Lifetime Module (Smooth fade-in at top, fade-out near ground) ---
        var colLife = ps.colorOverLifetime;
        colLife.enabled = true;
        Gradient fadeGrad = new Gradient();
        fadeGrad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.0f, 0.0f),
                new GradientAlphaKey(1.0f, 0.12f),
                new GradientAlphaKey(1.0f, 0.85f),
                new GradientAlphaKey(0.0f, 1.0f)
            }
        );
        colLife.color = new ParticleSystem.MinMaxGradient(fadeGrad);

        // --- Size over Lifetime Module ---
        var sizeLife = ps.sizeOverLifetime;
        sizeLife.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0.8f);
        sizeCurve.AddKey(0.15f, 1.0f);
        sizeCurve.AddKey(0.85f, 1.0f);
        sizeCurve.AddKey(1f, 0.85f);
        sizeLife.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // --- Texture Sheet Animation Module (4 leaves on 2x2 grid) ---
        var texSheet = ps.textureSheetAnimation;
        texSheet.enabled = true;
        texSheet.numTilesX = 2;
        texSheet.numTilesY = 2;
        texSheet.animation = ParticleSystemAnimationType.WholeSheet;
        // Randomly pick one of the 4 leaf variants for each particle
        texSheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f, 3.99f);
        texSheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 3.99f);
        texSheet.cycleCount = 1;

        // --- Renderer Module ---
        psRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        psRenderer.material = mat;
        psRenderer.alignment = ParticleSystemRenderSpace.View;
        psRenderer.minParticleSize = 0.005f;
        psRenderer.maxParticleSize = 0.5f;
        psRenderer.sortMode = ParticleSystemSortMode.Distance;

        EditorUtility.SetDirty(psGo);
    }
}
