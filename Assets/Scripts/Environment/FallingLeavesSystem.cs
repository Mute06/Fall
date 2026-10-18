using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FirstPersonSystem
{
    [ExecuteAlways]
    [RequireComponent(typeof(ParticleSystem))]
    [DisallowMultipleComponent]
    public class FallingLeavesSystem : MonoBehaviour
    {
        [Header("Material & Visuals")]
        [SerializeField] private Material leafMaterial;

        [Header("Emission Settings")]
        [SerializeField, Range(1f, 50f)] private float emissionRate = 16f;
        [SerializeField] private Vector3 emitterArea = new Vector3(26f, 1.5f, 26f);
        [SerializeField] private float heightAbovePlayer = 7f;

        [Header("Falling Physics")]
        [SerializeField, Range(0.01f, 0.3f)] private float gravityModifier = 0.06f;
        [SerializeField] private Vector2 particleSpeedRange = new Vector2(0.3f, 0.9f);
        [SerializeField] private Vector2 particleLifetimeRange = new Vector2(7f, 11f);
        [SerializeField] private Vector2 particleSizeRange = new Vector2(0.20f, 0.38f);

        [Header("Wind & Flutter Noise")]
        [SerializeField] private Vector3 windDrift = new Vector3(0.3f, -0.1f, 0.2f);
        [SerializeField] private float flutterStrength = 0.4f;

        private ParticleSystem _particleSystem;
        private ParticleSystemRenderer _particleRenderer;

        private void OnEnable()
        {
            InitializeSystem();
        }

        private void Reset()
        {
            InitializeSystem();
        }

        public void InitializeSystem()
        {
            _particleSystem = GetComponent<ParticleSystem>();
            _particleRenderer = GetComponent<ParticleSystemRenderer>();

            if (leafMaterial == null)
            {
#if UNITY_EDITOR
                leafMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_FallingLeaves.mat");
#endif
                if (leafMaterial == null)
                {
                    leafMaterial = Resources.Load<Material>("M_FallingLeaves");
                }
            }

            ConfigureParticleSystem();
        }

        public void ConfigureParticleSystem()
        {
            if (_particleSystem == null) _particleSystem = GetComponent<ParticleSystem>();
            if (_particleRenderer == null) _particleRenderer = GetComponent<ParticleSystemRenderer>();

            // Ensure local position is set to overhead height
            transform.localPosition = new Vector3(0f, heightAbovePlayer, 0f);
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            // --- 1. Main Module ---
            var main = _particleSystem.main;
            // Particles MUST move in world space so player movement does not drag existing falling leaves
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.simulationSpeed = 0.85f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(particleLifetimeRange.x, particleLifetimeRange.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(particleSpeedRange.x, particleSpeedRange.y);
            main.startSize = new ParticleSystem.MinMaxCurve(particleSizeRange.x, particleSizeRange.y);
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
            main.gravityModifier = gravityModifier;
            main.maxParticles = 300;
            main.loop = true;
            main.playOnAwake = true;

            // Autumn color variation
            Gradient startGrad = new Gradient();
            startGrad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(new Color(1f, 0.98f, 0.92f), 0.0f), new GradientColorKey(new Color(1f, 0.85f, 0.65f), 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(1.0f, 1.0f) }
            );
            main.startColor = new ParticleSystem.MinMaxGradient(startGrad);

            // --- 2. Emission Module ---
            var emission = _particleSystem.emission;
            emission.enabled = true;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(emissionRate * 0.8f, emissionRate * 1.2f);

            // --- 3. Shape Module ---
            var shape = _particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = emitterArea;
            shape.randomDirectionAmount = 0.2f;

            // --- 4. Velocity over Lifetime Module (gentle wind breeze in World Space) ---
            var velocity = _particleSystem.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(windDrift.x - 0.3f, windDrift.x + 0.3f);
            velocity.y = new ParticleSystem.MinMaxCurve(windDrift.y - 0.15f, windDrift.y + 0.15f);
            velocity.z = new ParticleSystem.MinMaxCurve(windDrift.z - 0.3f, windDrift.z + 0.3f);

            // --- 5. Noise Module (Fluttering / Swaying like real leaves) ---
            var noise = _particleSystem.noise;
            noise.enabled = true;
            noise.separateAxes = true;
            noise.strengthX = flutterStrength;
            noise.strengthY = flutterStrength * 0.3f;
            noise.strengthZ = flutterStrength;
            noise.frequency = 0.22f;
            noise.scrollSpeed = 0.35f;
            noise.damping = true;
            noise.octaveCount = 1;

            // --- 6. Rotation over Lifetime Module (3D Tumbling & Wobble) ---
            var rotLife = _particleSystem.rotationOverLifetime;
            rotLife.enabled = true;
            rotLife.separateAxes = true;
            rotLife.x = new ParticleSystem.MinMaxCurve(-50f * Mathf.Deg2Rad, 50f * Mathf.Deg2Rad);
            rotLife.y = new ParticleSystem.MinMaxCurve(-70f * Mathf.Deg2Rad, 70f * Mathf.Deg2Rad);
            rotLife.z = new ParticleSystem.MinMaxCurve(-90f * Mathf.Deg2Rad, 90f * Mathf.Deg2Rad);

            // --- 7. Color over Lifetime Module (Smooth fade-in at canopy, fade-out near ground) ---
            var colLife = _particleSystem.colorOverLifetime;
            colLife.enabled = true;
            Gradient fadeGrad = new Gradient();
            fadeGrad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.0f, 0.0f),
                    new GradientAlphaKey(1.0f, 0.15f),
                    new GradientAlphaKey(1.0f, 0.85f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            colLife.color = new ParticleSystem.MinMaxGradient(fadeGrad);

            // --- 8. Size over Lifetime Module ---
            var sizeLife = _particleSystem.sizeOverLifetime;
            sizeLife.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 0.85f);
            sizeCurve.AddKey(0.2f, 1.0f);
            sizeCurve.AddKey(0.85f, 1.0f);
            sizeCurve.AddKey(1f, 0.9f);
            sizeLife.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            // --- 9. Texture Sheet Animation Module (4 leaf variations in 2x2 grid) ---
            var texSheet = _particleSystem.textureSheetAnimation;
            texSheet.enabled = true;
            texSheet.numTilesX = 2;
            texSheet.numTilesY = 2;
            texSheet.animation = ParticleSystemAnimationType.WholeSheet;
            texSheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f, 3.99f);
            texSheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 3.99f);
            texSheet.cycleCount = 1;

            // --- 10. Renderer Module ---
            if (_particleRenderer != null)
            {
                _particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
                if (leafMaterial != null)
                {
                    _particleRenderer.material = leafMaterial;
                }
                _particleRenderer.alignment = ParticleSystemRenderSpace.View;
                _particleRenderer.minParticleSize = 0.005f;
                _particleRenderer.maxParticleSize = 0.5f;
                _particleRenderer.sortMode = ParticleSystemSortMode.Distance;
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Rebuild Particle System")]
        public void RebuildParticleSystem()
        {
            ConfigureParticleSystem();
            EditorUtility.SetDirty(gameObject);
        }

        [MenuItem("Tools/Falling Leaves/Attach to Player in Active Scene", false, 10)]
        public static void AttachToActivePlayer()
        {
            var fpc = UnityEngine.Object.FindAnyObjectByType<FirstPersonController>();
            if (fpc == null)
            {
                Debug.LogWarning("[FallingLeavesSystem] No FirstPersonController found in active scene.");
                return;
            }

            Transform child = fpc.transform.Find("FallingLeaves_ParticleSystem");
            GameObject leafObj;
            if (child != null)
            {
                leafObj = child.gameObject;
            }
            else
            {
                leafObj = new GameObject("FallingLeaves_ParticleSystem");
                leafObj.transform.SetParent(fpc.transform, false);
                Undo.RegisterCreatedObjectUndo(leafObj, "Create FallingLeaves_ParticleSystem");
            }

            FallingLeavesSystem system = leafObj.GetComponent<FallingLeavesSystem>();
            if (system == null)
            {
                system = leafObj.AddComponent<FallingLeavesSystem>();
            }

            system.InitializeSystem();
            EditorUtility.SetDirty(leafObj);
            EditorUtility.SetDirty(fpc.gameObject);
            Debug.Log("[FallingLeavesSystem] Successfully attached and configured FallingLeaves_ParticleSystem under " + fpc.name);
        }
#endif
    }
}
