using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FirstPersonSystem.ProceduralGeneration
{
    [ExecuteAlways]
    [SelectionBase]
    public class MountainBackdropManager : MonoBehaviour
    {
        [Serializable]
        public class BackdropLayer
        {
            [Tooltip("Display name for this layer.")]
            public string name = "Mountain Layer";

            [Tooltip("Backdrop material with transparent/cutout mountain texture.")]
            public Material material;

            [Tooltip("Radius (distance in meters) of this panoramic mountain ring from the center.")]
            [Min(50f)]
            public float radius = 250f;

            [Tooltip("Height (in meters) of this backdrop ring.")]
            [Min(10f)]
            public float height = 150f;

            [Tooltip("Base elevation (Y offset in meters) relative to the backdrop center.")]
            public float verticalOffset = -15f;

            [Tooltip("How many times the panoramic texture repeats horizontally around the 360 ring.")]
            [Range(1f, 16f)]
            public float uvTilingX = 4f;

            [Tooltip("Number of radial vertices forming the cylinder ring.")]
            [Range(16, 96)]
            public int segmentCount = 48;

            [Tooltip("Rotational offset in degrees to align peaks with desired vantage points.")]
            [Range(0f, 360f)]
            public float rotationDegrees = 0f;

            [Tooltip("Parallax tracking weight (0 = fully anchored in world, 0.5 = moves 50% with player XZ).")]
            [Range(0f, 0.95f)]
            public float parallaxFollowRatio = 0f;
        }

        [Header("Global Center & Placement")]
        [Tooltip("World Y coordinate (base elevation) for distant mountain backgrounds.")]
        [SerializeField] private float baseYCoordinate = 0f;

        [Tooltip("If true, worldCenter.y will use baseYCoordinate rather than the terrain's root Y.")]
        [SerializeField] private bool useExplicitBaseY = true;

        [Tooltip("World space center of the mountain backdrop rings. For a terrain spanning Z 0..250, (0, 0, 125) is the ideal midpoint.")]
        [SerializeField] private Vector3 worldCenter = new Vector3(0f, 0f, 125f);

        [Tooltip("If true, automatically computes world center from active Terrain if one exists.")]
        [SerializeField] private bool autoDetectTerrainCenter = true;

        public float BaseYCoordinate
        {
            get => baseYCoordinate;
            set
            {
                baseYCoordinate = value;
                useExplicitBaseY = true;
                worldCenter.y = value;
            }
        }

        public void SetBaseY(float y)
        {
            baseYCoordinate = y;
            useExplicitBaseY = true;
            worldCenter.y = y;
        }

        [Header("Parallax Player Tracking")]
        [Tooltip("If true, layers with parallaxFollowRatio > 0 will subtly drift with player movement to enhance distant depth.")]
        [SerializeField] private bool enableParallaxTracking = false;

        [Tooltip("Reference to the player transform. If null, automatically searches for Camera.main or tag 'Player'.")]
        [SerializeField] private Transform playerTransform;

        [Header("Backdrop Layers")]
        [SerializeField] private List<BackdropLayer> layers = new List<BackdropLayer>();

        [Header("Automation")]
        [SerializeField] private bool generateOnStart = true;
        [SerializeField] private bool showGizmosInScene = true;

        // Runtime cached layer transforms
        private readonly List<Transform> layerTransforms = new List<Transform>();
        private readonly List<Mesh> generatedMeshes = new List<Mesh>();

        private void Reset()
        {
            InitializeDefaultLayers();
        }

        private void Start()
        {
            if (layers == null || layers.Count == 0)
            {
                InitializeDefaultLayers();
            }

            if (autoDetectTerrainCenter)
            {
                DetectTerrainCenter();
            }

            if (generateOnStart || transform.childCount == 0)
            {
                GenerateBackdrops();
            }

            CachePlayerReference();
        }

        private void LateUpdate()
        {
            if (!enableParallaxTracking || !Application.isPlaying) return;

            if (playerTransform == null)
            {
                CachePlayerReference();
                if (playerTransform == null) return;
            }

            Vector3 playerPos = playerTransform.position;
            Vector3 deltaFromCenter = new Vector3(playerPos.x - worldCenter.x, 0f, playerPos.z - worldCenter.z);

            for (int i = 0; i < layers.Count && i < layerTransforms.Count; i++)
            {
                Transform layerT = layerTransforms[i];
                if (layerT == null) continue;

                float ratio = layers[i].parallaxFollowRatio;
                if (ratio > 0.001f)
                {
                    Vector3 targetPos = worldCenter + deltaFromCenter * ratio;
                    layerT.position = targetPos;
                }
            }
        }

        private void OnDestroy()
        {
            CleanupMeshes();
        }

        /// <summary>
        /// Populates the 3 default multi-layered 2.5D backdrop configurations.
        /// </summary>
        public void InitializeDefaultLayers()
        {
            layers.Clear();

            // Layer 1: Mid-Distance Forest & Rock Ridge
            layers.Add(new BackdropLayer
            {
                name = "Layer 1 - Mid Distance Ridge",
                radius = 260f,
                height = 140f,
                verticalOffset = -15f,
                uvTilingX = 5f,
                segmentCount = 48,
                rotationDegrees = 15f,
                parallaxFollowRatio = 0.0f,
                material = LoadMaterialByGuid("e111a222b333c444d555e666f7770001")
            });

            // Layer 2: Far Alpine Peaks
            layers.Add(new BackdropLayer
            {
                name = "Layer 2 - Far Alpine Range",
                radius = 440f,
                height = 240f,
                verticalOffset = -25f,
                uvTilingX = 4f,
                segmentCount = 48,
                rotationDegrees = 85f,
                parallaxFollowRatio = 0.15f,
                material = LoadMaterialByGuid("e111a222b333c444d555e666f7770002")
            });

            // Layer 3: Distant Atmospheric Silhouettes
            layers.Add(new BackdropLayer
            {
                name = "Layer 3 - Distant Silhouettes",
                radius = 720f,
                height = 420f,
                verticalOffset = -35f,
                uvTilingX = 3f,
                segmentCount = 48,
                rotationDegrees = 190f,
                parallaxFollowRatio = 0.35f,
                material = LoadMaterialByGuid("e111a222b333c444d555e666f7770003")
            });
        }

        /// <summary>
        /// Clears existing backdrop meshes and generates the full concentric panorama.
        /// </summary>
        [ContextMenu("Generate Backdrops")]
        public void GenerateBackdrops()
        {
            ClearBackdrops();

            if (autoDetectTerrainCenter)
            {
                DetectTerrainCenter();
            }

            layerTransforms.Clear();

            for (int i = 0; i < layers.Count; i++)
            {
                BackdropLayer layer = layers[i];
                GameObject layerGO = CreateCylinderLayerObject(layer, i);
                layerTransforms.Add(layerGO.transform);
            }

            Debug.Log($"[MountainBackdropManager] Successfully generated {layers.Count} mountain backdrop rings around center {worldCenter}.");
        }

        /// <summary>
        /// Removes all spawned backdrop layers.
        /// </summary>
        [ContextMenu("Clear Backdrops")]
        public void ClearBackdrops()
        {
            CleanupMeshes();
            layerTransforms.Clear();

            List<GameObject> toDestroy = new List<GameObject>();
            for (int i = 0; i < transform.childCount; i++)
            {
                toDestroy.Add(transform.GetChild(i).gameObject);
            }

            foreach (GameObject child in toDestroy)
            {
                if (Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }
        }

        private GameObject CreateCylinderLayerObject(BackdropLayer layer, int index)
        {
            GameObject layerGO = new GameObject($"Backdrop_{index + 1}_{layer.name}");
            layerGO.transform.SetParent(transform, false);
            layerGO.transform.position = worldCenter;
            layerGO.transform.rotation = Quaternion.Euler(0f, layer.rotationDegrees, 0f);

            MeshFilter filter = layerGO.AddComponent<MeshFilter>();
            MeshRenderer renderer = layerGO.AddComponent<MeshRenderer>();

            Mesh mesh = BuildInwardCylinderMesh(layer.radius, layer.height, layer.verticalOffset, layer.segmentCount, layer.uvTilingX);
            mesh.name = $"BackdropMesh_{layer.name}";
            generatedMeshes.Add(mesh);
            filter.sharedMesh = mesh;

            Material baseMat = layer.material != null ? layer.material : GetOrCreateFallbackMaterial(index);
            if (baseMat != null && baseMat.HasProperty("_FadeBottomY"))
            {
                Material instancedMat = new Material(baseMat);
                instancedMat.SetFloat("_FadeBottomY", worldCenter.y + layer.verticalOffset);
                renderer.sharedMaterial = instancedMat;
            }
            else
            {
                renderer.sharedMaterial = baseMat;
            }

            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            return layerGO;
        }

        /// <summary>
        /// Constructs a smooth inward-facing ribbon cylinder mesh with UVs repeating horizontally.
        /// </summary>
        private Mesh BuildInwardCylinderMesh(float radius, float height, float yOffset, int segments, float uvTiles)
        {
            Mesh mesh = new Mesh();

            int vertexCount = (segments + 1) * 2;
            int triangleCount = segments * 6;

            Vector3[] vertices = new Vector3[vertexCount];
            Vector3[] normals = new Vector3[vertexCount];
            Vector2[] uvs = new Vector2[vertexCount];
            Color[] colors = new Color[vertexCount];
            int[] triangles = new int[triangleCount];

            float angleStep = (Mathf.PI * 2f) / segments;

            for (int i = 0; i <= segments; i++)
            {
                float angle = i * angleStep;
                float sin = Mathf.Sin(angle);
                float cos = Mathf.Cos(angle);

                float u = ((float)i / segments) * uvTiles;

                int botIdx = i * 2;
                int topIdx = botIdx + 1;

                vertices[botIdx] = new Vector3(cos * radius, yOffset, sin * radius);
                vertices[topIdx] = new Vector3(cos * radius, yOffset + height, sin * radius);

                Vector3 inwardNormal = new Vector3(-cos, 0f, -sin).normalized;
                normals[botIdx] = inwardNormal;
                normals[topIdx] = inwardNormal;

                uvs[botIdx] = new Vector2(u, 0f);
                uvs[topIdx] = new Vector2(u, 1f);

                colors[botIdx] = new Color(1f, 1f, 1f, 0f);
                colors[topIdx] = new Color(1f, 1f, 1f, 1f);
            }

            int triOffset = 0;
            for (int i = 0; i < segments; i++)
            {
                int b0 = i * 2;
                int t0 = b0 + 1;
                int b1 = (i + 1) * 2;
                int t1 = b1 + 1;

                triangles[triOffset++] = b0;
                triangles[triOffset++] = b1;
                triangles[triOffset++] = t0;

                triangles[triOffset++] = t0;
                triangles[triOffset++] = b1;
                triangles[triOffset++] = t1;
            }

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.triangles = triangles;

            mesh.RecalculateBounds();
            return mesh;
        }

        private void DetectTerrainCenter()
        {
            Terrain terrain = FindFirstObjectByType<Terrain>();
            if (terrain != null && terrain.terrainData != null)
            {
                Vector3 origin = terrain.transform.position;
                Vector3 size = terrain.terrainData.size;
                float y = useExplicitBaseY ? baseYCoordinate : origin.y;
                worldCenter = new Vector3(origin.x + size.x * 0.5f, y, origin.z + size.z * 0.5f);
            }
            else if (useExplicitBaseY)
            {
                worldCenter.y = baseYCoordinate;
            }
        }

        private void CachePlayerReference()
        {
            if (playerTransform != null) return;

            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerTransform = playerObj.transform;
                return;
            }

            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                playerTransform = mainCam.transform;
            }
        }

        private void CleanupMeshes()
        {
            foreach (Mesh mesh in generatedMeshes)
            {
                if (mesh != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(mesh);
                    }
                    else
                    {
                        DestroyImmediate(mesh);
                    }
                }
            }
            generatedMeshes.Clear();
        }

        private Material LoadMaterialByGuid(string guid)
        {
#if UNITY_EDITOR
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(path))
            {
                return AssetDatabase.LoadAssetAtPath<Material>(path);
            }
#endif
            return null;
        }

        private Material GetOrCreateFallbackMaterial(int layerIndex)
        {
            Shader shader = Shader.Find("Custom/MountainBackdrop");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            Material mat = new Material(shader);
            mat.name = $"Backdrop_Fallback_{layerIndex}";
            return mat;
        }

        private void OnDrawGizmosSelected()
        {
            if (!showGizmosInScene) return;

            Color[] colors = new Color[] { Color.cyan, Color.blue, Color.magenta };

            for (int i = 0; i < layers.Count; i++)
            {
                BackdropLayer layer = layers[i];
                Gizmos.color = colors[i % colors.Length];
                Gizmos.DrawWireSphere(worldCenter + Vector3.up * (layer.verticalOffset + layer.height * 0.5f), layer.radius);
            }
        }
    }
}
