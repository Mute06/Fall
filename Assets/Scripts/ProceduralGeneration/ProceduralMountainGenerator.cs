using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FirstPersonSystem.Narrative;
using FirstPersonSystem.SceneManagement;

namespace FirstPersonSystem.ProceduralGeneration
{
    [ExecuteAlways]
    public class ProceduralMountainGenerator : MonoBehaviour
    {
        public static event Action<ProceduralMountainGenerator> OnGenerationStarted;
        public static event Action<ProceduralMountainGenerator> OnGenerationCompleted;

        [Header("Configuration Profile")]
        [Tooltip("Configurable ScriptableObject profile containing all terrain, path, slope, and asset settings. If left null, a default in-memory profile will be used.")]
        [SerializeField] private MountainGenerationProfile profile;

        [Header("Seed & Generation Trigger")]
        [SerializeField] private int seed = 1337;
        [SerializeField] private bool randomizeSeedOnStart = true;
        [SerializeField] private bool generateOnStart = true;

        [Header("Target Terrain & Actors")]
        [Tooltip("Target Unity Terrain. If null, one will be found or created automatically as a child.")]
        [SerializeField] private Terrain targetTerrain;

        [Tooltip("Optional Player Transform to place at the starting flat area upon generation.")]
        [SerializeField] private Transform playerTransform;

        [Tooltip("Optional Coffin Transform (or CoffinPullable) to place next to the player at the starting flat area.")]
        [SerializeField] private Transform coffinTransform;

        [Tooltip("Optional Cabin Transform (e.g. 'Cabın' GameObject) to place near the player on the flat terrain.")]
        [SerializeField] private Transform cabinTransform;

        [SerializeField] private Vector3 playerStartOffset = new Vector3(-1.5f, 1.15f, 2f);
        [SerializeField] private Vector3 coffinStartOffset = new Vector3(1.5f, 0.6f, 3.5f);
        [SerializeField] private Vector3 cabinStartOffset = new Vector3(-8.5f, 0f, 1f);
        [SerializeField] private Vector3 cabinRotationEuler = new Vector3(0f, -270f, 0f);
        [SerializeField] private float cabinFootprintRadius = 7.5f;
        [SerializeField] private bool repositionActorsOnGenerate = true;

        [Header("Distant Mountain Backdrops")]
        [Tooltip("If enabled, overrides the profile's mountain backdrops Y coordinate with customBackdropBaseY.")]
        [SerializeField] private bool overrideBackdropBaseY = false;

        [Tooltip("Custom World Y coordinate for distant mountain backgrounds when generating.")]
        [SerializeField] private float customBackdropBaseY = 0f;

        [Header("Debug & Scene Visualization")]
        [SerializeField] private bool drawGoldenPathGizmos = true;
        [SerializeField] private bool drawSafeZoneGizmos = true;
        [SerializeField] private bool drawBoundaryGizmos = true;

        // Runtime / Cached State
        private GoldenPathGenerator pathGenerator = new GoldenPathGenerator();
        private SpatialOccupancyGrid occupancyGrid = new SpatialOccupancyGrid(4f);
        private Transform generatedRoot;
        private bool isGenerating = false;
        private bool isGenerationCompleted = false;

        public MountainGenerationProfile Profile => profile;
        public int Seed
        {
            get => seed;
            set => seed = value;
        }
        public IReadOnlyList<GoldenPathNode> GoldenPathNodes => pathGenerator?.Nodes;
        public IReadOnlyList<SafePlateauZone> SafeZones => pathGenerator?.SafeZones;
        public bool IsGenerating => isGenerating;
        public bool IsGenerationCompleted => isGenerationCompleted;

        private void Awake()
        {
            if (!Application.isPlaying) return;

            if (generateOnStart && !isGenerationCompleted)
            {
                LoadingScreenManager.Show();
            }
        }

        private IEnumerator Start()
        {
            if (!Application.isPlaying) yield break;

            if (generateOnStart && !isGenerationCompleted)
            {
                LoadingScreenManager.Show();

                if (randomizeSeedOnStart)
                {
                    seed = UnityEngine.Random.Range(1, 999999);
                }
                GenerateMountain();

                yield return null;
                yield return new WaitForEndOfFrame();

                LoadingScreenManager.Hide();
            }
        }

        /// <summary>
        /// Executes the procedural mountain, golden path, forest, and hazard generation.
        /// Safe to call both in Edit Mode and Play Mode.
        /// </summary>
        [ContextMenu("Generate Mountain")]
        public void GenerateMountain()
        {
            isGenerating = true;
            OnGenerationStarted?.Invoke(this);

            try
            {
                MountainGenerationProfile activeProfile = GetOrCreateActiveProfile();

                ClearSpawnedObjects();
                EnsureTerrainSetup(activeProfile, out TerrainData terrainData, out Vector3 terrainOrigin);

                // Stage 1: Golden Path (Random Walk with Momentum from (0,0) to (0,250) stepping 8-12m)
                pathGenerator.Generate(activeProfile, seed);

                // Stage 2 & 3: Stepped Longitudinal Elevation + 2D Heightmap Synthesis + Path Carving (with natural edges)
                SynthesizeHeightmap(activeProfile, terrainData, terrainOrigin, seed);

                // Stage 4: Splatmap Painting (Grass default ground + optional Rock/Path layers)
                ApplyTerrainTextures(activeProfile, terrainData, terrainOrigin);

                // Prepare container hierarchy for spawned objects
                CreateSpawnContainers(
                    out Transform anchorsParent,
                    out Transform denseForestParent,
                    out Transform hazardsParent,
                    out Transform decorParent,
                    out Transform boundariesParent,
                    out Transform backdropsParent);

                occupancyGrid.Clear();

                // Reserve spawn clearance at Player & Coffin start area
                Vector2 playerAreaXZ = activeProfile.pathStartXZ + new Vector2(0f, 2.5f);
                occupancyGrid.Register(playerAreaXZ, 5.5f);

                // Reserve cabin footprint so trees/rocks do not spawn inside the cabin
                Vector2 cabinSpawnXZ = activeProfile.pathStartXZ + new Vector2(cabinStartOffset.x, cabinStartOffset.z);
                occupancyGrid.Register(cabinSpawnXZ, cabinFootprintRadius);

                // Reserve grave hole area at the summit so trees/rocks do not spawn inside or around the hole
                if (activeProfile.enableSummitGraveHole)
                {
                    Vector2 graveSpawnXZ = activeProfile.pathEndXZ + activeProfile.graveHoleOffset;
                    occupancyGrid.Register(graveSpawnXZ, 6.0f);
                }

                // Stage 5: Guaranteed-Solvable Anchor Spawning along Golden Path nodes
                // Uses a random tree and random rock from the shared treePrefabs & rockPrefabs pools
                int anchorCount = SpawnGoldenPathAnchors(activeProfile, anchorsParent, seed + 101);

                // Stage 6A: Dense Forest Spawning
                // Fills the mountain with a dense forest of trees using the multiple treePrefabs
                int forestTreeCount = 0;
                if (activeProfile.enableDenseForest)
                {
                    forestTreeCount = SpawnDenseForest(activeProfile, denseForestParent, seed + 202);
                }

                // Stage 6B: Slope Hazards & Rocks
                // Slope < 20°: Leaf piles & deadfall logs
                // Slope > 38°: Jagged rocks from rockPrefabs and loose scree zones
                int hazardCount = SpawnSlopeHazardsAndRocks(activeProfile, hazardsParent, seed + 303);

                // Stage 6C: Decorative Small Objects Scatter (Small Rocks, Bushes, etc.)
                int decorCount = SpawnDecorativeObjects(activeProfile, decorParent, seed + 404);

                // Stage 7: Invisible Boundary Colliders (North, South, East, West)
                if (activeProfile.spawnInvisibleBoundaries)
                {
                    SpawnInvisibleBoundaryColliders(activeProfile, boundariesParent);
                }

                // Stage 8: Distant Mountain Vista Backdrops (2.5D concentric backdrop rings)
                if (activeProfile.enableMountainBackdrops)
                {
                    SpawnMountainBackdrops(activeProfile, backdropsParent, terrainOrigin, terrainData.size);
                }

                // Setup Summit Resting Marker & Grave Hole Trigger Area
                if (activeProfile.enableSummitGraveHole)
                {
                    SetupSummitGraveMarker(activeProfile);
                }

                // Position Player & Coffin on the starting flat zone if requested
                if (repositionActorsOnGenerate)
                {
                    PositionStartingActors(activeProfile);
                }

                Debug.Log($"[ProceduralMountainGenerator] Mountain generated (Seed: {seed}, Elev: {activeProfile.targetElevationGain}m, Path Nodes: {pathGenerator.Nodes.Count}, Path Anchors: {anchorCount}, Forest Trees: {forestTreeCount}, Slope Hazards/Rocks: {hazardCount}, Props: {decorCount}).");
            }
            finally
            {
                isGenerating = false;
                isGenerationCompleted = true;
                OnGenerationCompleted?.Invoke(this);
            }
        }

        [ContextMenu("Randomize Seed & Generate")]
        public void RandomizeSeedAndGenerate()
        {
            seed = UnityEngine.Random.Range(1, 999999);
            GenerateMountain();
        }

        public void GenerateWithRandomSeed()
        {
            RandomizeSeedAndGenerate();
        }

        [ContextMenu("Clear Spawned Objects")]
        public void ClearSpawnedObjects()
        {
            Transform existing = transform.Find("Generated_Mountain_Objects");
            if (existing != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(existing.gameObject);
                }
                else
                {
                    DestroyImmediate(existing.gameObject);
                }
            }
            generatedRoot = null;
        }

        private MountainGenerationProfile GetOrCreateActiveProfile()
        {
            if (profile != null) return profile;

            MountainGenerationProfile fallback = ScriptableObject.CreateInstance<MountainGenerationProfile>();
            fallback.name = "RuntimeDefaultMountainProfile";
            return fallback;
        }

        private void EnsureTerrainSetup(
            MountainGenerationProfile cfg,
            out TerrainData terrainData,
            out Vector3 terrainOrigin)
        {
            if (targetTerrain == null)
            {
                targetTerrain = GetComponent<Terrain>();
            }
            if (targetTerrain == null)
            {
                targetTerrain = GetComponentInChildren<Terrain>();
            }
            if (targetTerrain == null)
            {
                GameObject terrainGO = new GameObject("ProceduralMountainTerrain");
                terrainGO.transform.SetParent(transform, false);
                terrainGO.tag = "Terrain";
                targetTerrain = terrainGO.AddComponent<Terrain>();
                TerrainCollider tc = terrainGO.AddComponent<TerrainCollider>();
                targetTerrain.terrainData = new TerrainData();
                tc.terrainData = targetTerrain.terrainData;
            }

            if (!targetTerrain.gameObject.CompareTag("Terrain"))
            {
                targetTerrain.gameObject.tag = "Terrain";
            }

            if (targetTerrain.terrainData == null)
            {
                targetTerrain.terrainData = new TerrainData();
            }

            terrainData = targetTerrain.terrainData;
            TerrainCollider collider = targetTerrain.GetComponent<TerrainCollider>();
            if (collider == null)
            {
                collider = targetTerrain.gameObject.AddComponent<TerrainCollider>();
            }
            collider.terrainData = terrainData;

            // Ensure default terrain material is assigned if missing (URP / Built-in safe fallback)
            if (targetTerrain.materialTemplate == null)
            {
                Shader urpTerrainShader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
                if (urpTerrainShader == null) urpTerrainShader = Shader.Find("Nature/Terrain/Standard");
                if (urpTerrainShader != null)
                {
                    targetTerrain.materialTemplate = new Material(urpTerrainShader);
                }
            }

            // Compute total terrain world dimensions
            float totalWidthX = cfg.playableWidth + cfg.boundaryMargin * 2f;
            float climbDistanceZ = Mathf.Max(50f, cfg.pathEndXZ.y - cfg.pathStartXZ.y);
            float totalLengthZ = cfg.forestBehindPlayerDistance + climbDistanceZ + cfg.summitSafeLength + cfg.boundaryMargin;
            float maxTerrainHeightY = cfg.targetElevationGain + (cfg.enableBoundaryCliffs ? cfg.boundaryCliffHeight : 0f) + cfg.terrainMaxHeightHeadroom;

            int hmRes = Mathf.ClosestPowerOfTwo(Mathf.Clamp(cfg.heightmapResolution - 1, 64, 2048)) + 1;
            terrainData.heightmapResolution = hmRes;
            terrainData.size = new Vector3(totalWidthX, maxTerrainHeightY, totalLengthZ);

            // Position terrain so that the player starts ~forestBehindPlayerDistance meters ahead of the terrain's starting point
            // X is centered around 0: [-totalWidthX/2, +totalWidthX/2]
            // Z starts at (pathStartXZ.y - forestBehindPlayerDistance)
            float terrainStartZ = cfg.pathStartXZ.y - cfg.forestBehindPlayerDistance;
            terrainOrigin = new Vector3(
                -totalWidthX * 0.5f,
                0f,
                terrainStartZ);

            targetTerrain.transform.position = terrainOrigin;
        }

        private void SynthesizeHeightmap(
            MountainGenerationProfile cfg,
            TerrainData terrainData,
            Vector3 terrainOrigin,
            int currentSeed)
        {
            int res = terrainData.heightmapResolution;
            Vector3 terrainSize = terrainData.size;
            float maxH = terrainSize.y;

            float[,] heights = new float[res, res];

            System.Random noiseRng = new System.Random(currentSeed + 17);
            Vector2[] octaveOffsets = new Vector2[cfg.noiseOctaves];
            for (int i = 0; i < cfg.noiseOctaves; i++)
            {
                octaveOffsets[i] = new Vector2(
                    (float)noiseRng.NextDouble() * 10000f - 5000f,
                    (float)noiseRng.NextDouble() * 10000f - 5000f);
            }

            float halfPlayableWidth = cfg.playableWidth * 0.5f;
            float halfTotalWidth = terrainSize.x * 0.5f;
            float startZ = cfg.pathStartXZ.y;
            float endZ = cfg.pathEndXZ.y;
            float summitEndZ = endZ + cfg.summitSafeLength;

            for (int zIdx = 0; zIdx < res; zIdx++)
            {
                float normZ = (float)zIdx / (res - 1);
                float worldZ = terrainOrigin.z + normZ * terrainSize.z;

                // From terrain start up to player start position: strictly 0.0 elevation across entire width
                if (worldZ <= startZ)
                {
                    for (int xIdx = 0; xIdx < res; xIdx++)
                    {
                        heights[zIdx, xIdx] = 0f;
                    }
                    continue;
                }

                // 1. Evaluate stepped longitudinal elevation profile (Flat start -> progressive slopes & safe plateaus -> 85m summit)
                float baseElevation = GoldenPathGenerator.EvaluateLongitudinalElevation(worldZ, cfg, out bool isSafeZone);

                // Reduce terrain roughness in the initial flat spawn area and safe zones
                float startFlatFade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((worldZ - (startZ + cfg.startingFlatLength * 0.65f)) / 18f));
                float safeZoneNoiseDamp = isSafeZone ? 0.35f : 1.0f;

                for (int xIdx = 0; xIdx < res; xIdx++)
                {
                    float normX = (float)xIdx / (res - 1);
                    float worldX = terrainOrigin.x + normX * terrainSize.x;
                    Vector2 worldXZ = new Vector2(worldX, worldZ);

                    // 2. Multi-octave FBM Mountain Noise
                    float fbm = SampleFBM(worldXZ, cfg.primaryNoiseScale, octaveOffsets);
                    float ridgeNoise = (fbm - 0.42f) * cfg.mountainRoughnessAmplitude * startFlatFade * safeZoneNoiseDamp;

                    // Subtle natural valley cupping across X so the mountain feels natural towards the edges
                    float lateralRatio = Mathf.Clamp01(Mathf.Abs(worldX) / Mathf.Max(1f, halfPlayableWidth));
                    float valleyCupRise = lateralRatio * lateralRatio * 3.5f * startFlatFade;

                    float rawMountainHeight = baseElevation + ridgeNoise + valleyCupRise;

                    // 3. Carve / Flatten along the Golden Path so the trail is navigable
                    float distToPath = pathGenerator.QueryClosestPointOnPath(
                        worldXZ,
                        out _,
                        out float pathSpineElevation,
                        out _);

                    float carveMask = 0f;
                    if (distToPath < cfg.pathOuterBlendRadius)
                    {
                        float t = Mathf.Clamp01((distToPath - cfg.pathInnerCarveRadius) /
                                                Mathf.Max(0.5f, cfg.pathOuterBlendRadius - cfg.pathInnerCarveRadius));
                        carveMask = (1f - t * t * (3f - 2f * t)) * cfg.pathCarveStrength;
                    }

                    // Concave cradle in center of the trail
                    float centerTrench = 0f;
                    if (distToPath < cfg.pathInnerCarveRadius)
                    {
                        float centerFactor = 1f - (distToPath / cfg.pathInnerCarveRadius);
                        centerTrench = centerFactor * centerFactor * cfg.pathTrenchDepth;
                    }

                    float carvedHeight = Mathf.Lerp(rawMountainHeight, pathSpineElevation - centerTrench, carveMask);

                    // 4. Edges: Keep natural unless boundary cliffs are explicitly enabled
                    float cliffRise = 0f;
                    if (cfg.enableBoundaryCliffs)
                    {
                        cliffRise = ComputeBoundaryCliffHeight(
                            worldX, worldZ, halfPlayableWidth, halfTotalWidth,
                            startZ, summitEndZ, cfg.boundaryMargin, cfg.boundaryCliffHeight, 2.0f, fbm);
                    }

                    float finalWorldHeight = Mathf.Max(0f, carvedHeight + cliffRise);

                    // 5. Carve Summit Grave Hole at end of Golden Path in final flat safe area
                    if (cfg.enableSummitGraveHole)
                    {
                        Vector2 graveCenter = cfg.pathEndXZ + cfg.graveHoleOffset;
                        float holeDepression = ComputeGraveHoleDepression(worldX, worldZ, graveCenter, cfg.graveHoleSize, cfg.graveHoleDepth, cfg.graveHoleBlendMargin);
                        finalWorldHeight = Mathf.Max(0f, finalWorldHeight - holeDepression);
                    }

                    heights[zIdx, xIdx] = Mathf.Clamp01(finalWorldHeight / maxH);
                }
            }

            terrainData.SetHeights(0, 0, heights);
        }

        private float ComputeGraveHoleDepression(
            float worldX,
            float worldZ,
            Vector2 graveCenter,
            Vector2 holeSize,
            float depth,
            float blendMargin)
        {
            float halfW = Mathf.Max(0.2f, holeSize.x * 0.5f);
            float halfL = Mathf.Max(0.2f, holeSize.y * 0.5f);
            float innerHalfW = Mathf.Max(0.05f, halfW - blendMargin);
            float innerHalfL = Mathf.Max(0.05f, halfL - blendMargin);

            float dx = Mathf.Max(0f, Mathf.Abs(worldX - graveCenter.x) - innerHalfW);
            float dz = Mathf.Max(0f, Mathf.Abs(worldZ - graveCenter.y) - innerHalfL);
            float dist = Mathf.Sqrt(dx * dx + dz * dz);

            if (dist <= 0.0001f)
            {
                return depth;
            }

            if (dist < blendMargin)
            {
                float t = Mathf.Clamp01(dist / blendMargin);
                float smooth = 1f - t * t * (3f - 2f * t);
                return depth * smooth;
            }

            return 0f;
        }

        private float ComputeBoundaryCliffHeight(
            float worldX, float worldZ, float halfPlayableWidth, float halfTotalWidth,
            float startZ, float summitEndZ, float margin, float cliffHeight, float sharpness, float fbmValue)
        {
            float absX = Mathf.Abs(worldX);
            float lateralEdgeT = (absX > halfPlayableWidth) ? Mathf.Clamp01((absX - halfPlayableWidth) / Mathf.Max(1f, halfTotalWidth - halfPlayableWidth)) : 0f;
            float backEdgeT = (worldZ < startZ) ? Mathf.Clamp01((startZ - worldZ) / Mathf.Max(1f, margin)) : 0f;
            float frontEdgeT = (worldZ > summitEndZ) ? Mathf.Clamp01((worldZ - summitEndZ) / Mathf.Max(1f, margin)) : 0f;

            float combinedEdgeT = Mathf.Max(lateralEdgeT, Mathf.Max(backEdgeT, frontEdgeT));
            if (combinedEdgeT <= 0.0001f) return 0f;

            float curved = Mathf.Pow(combinedEdgeT, sharpness);
            return curved * cliffHeight * (0.75f + 0.5f * fbmValue);
        }

        private float SampleFBM(Vector2 worldXZ, float baseScale, Vector2[] octaveOffsets)
        {
            float amplitude = 1f;
            float frequency = 1f;
            float total = 0f;
            float maxAmp = 0f;

            for (int i = 0; i < octaveOffsets.Length; i++)
            {
                float sampleX = worldXZ.x * baseScale * frequency + octaveOffsets[i].x;
                float sampleZ = worldXZ.y * baseScale * frequency + octaveOffsets[i].y;

                float perlin = Mathf.PerlinNoise(sampleX, sampleZ);
                total += perlin * amplitude;
                maxAmp += amplitude;

                amplitude *= 0.5f;
                frequency *= 2.05f;
            }

            return maxAmp > 0f ? (total / maxAmp) : 0.5f;
        }

        /// <summary>
        /// Configures TerrainLayers (Grass as normal ground, plus optional Rock/Path layers)
        /// and paints the terrain alphamap so CheckTerrainTexture works out of the box.
        /// <summary>
        /// Configures TerrainLayers (Grass as normal ground, Rock on cliffs, Path on Golden Trail)
        /// and paints the terrain alphamap so the Golden Path that the player is supposed to take
        /// is clearly and visibly painted with pathTerrainLayer.
        /// </summary>
        private void ApplyTerrainTextures(
            MountainGenerationProfile cfg,
            TerrainData terrainData,
            Vector3 terrainOrigin)
        {
            List<TerrainLayer> layers = new List<TerrainLayer>();

            // Layer 0: Grass (base walkable ground)
            TerrainLayer grassLayer = cfg.grassTerrainLayer;
            if (grassLayer == null)
            {
                if (terrainData.terrainLayers != null && terrainData.terrainLayers.Length > 0 && terrainData.terrainLayers[0] != null)
                {
                    grassLayer = terrainData.terrainLayers[0];
                }
                else
                {
                    grassLayer = CreateFallbackGrassLayer();
                }
            }
            layers.Add(grassLayer);

            // Layer 1: Rock (steep cliffs off path)
            int rockLayerIndex = -1;
            if (cfg.rockTerrainLayer != null)
            {
                rockLayerIndex = layers.Count;
                layers.Add(cfg.rockTerrainLayer);
            }

            // Layer 2: Path (the trail the player takes)
            int pathLayerIndex = -1;
            TerrainLayer pathLayer = cfg.pathTerrainLayer;
            if (pathLayer == null)
            {
                if (terrainData.terrainLayers != null && terrainData.terrainLayers.Length > 2 && terrainData.terrainLayers[2] != null)
                {
                    pathLayer = terrainData.terrainLayers[2];
                }
                else
                {
                    pathLayer = CreateFallbackPathLayer();
                }
            }
            if (pathLayer != null)
            {
                pathLayerIndex = layers.Count;
                layers.Add(pathLayer);
            }

            terrainData.terrainLayers = layers.ToArray();

            int alphaRes = Mathf.ClosestPowerOfTwo(Mathf.Clamp(cfg.alphamapResolution, 64, 1024));
            terrainData.alphamapResolution = alphaRes;

            int numLayers = layers.Count;
            float[,,] alphamaps = new float[alphaRes, alphaRes, numLayers];
            Vector3 terrainSize = terrainData.size;

            float innerR = Mathf.Max(0.5f, cfg.pathTextureRadius);
            float blendDist = Mathf.Max(0.2f, cfg.pathTextureBlend);
            float pathOpacity = Mathf.Clamp01(cfg.pathTextureOpacity);

            for (int z = 0; z < alphaRes; z++)
            {
                float normZ = (float)z / (alphaRes - 1);
                float worldZ = terrainOrigin.z + normZ * terrainSize.z;

                for (int x = 0; x < alphaRes; x++)
                {
                    float normX = (float)x / (alphaRes - 1);
                    float worldX = terrainOrigin.x + normX * terrainSize.x;

                    if (numLayers == 1)
                    {
                        alphamaps[z, x, 0] = 1f;
                        continue;
                    }

                    // 1. Distance to the Golden Path trail (with clean start & summit extensions)
                    float distToPath = QueryDistanceToGoldenTrail(new Vector2(worldX, worldZ), cfg);

                    float pathWeight = 0f;
                    if (pathLayerIndex != -1)
                    {
                        if (distToPath <= innerR)
                        {
                            pathWeight = pathOpacity;
                        }
                        else if (distToPath < innerR + blendDist)
                        {
                            float u = (distToPath - innerR) / blendDist;
                            float smooth = 1f - (u * u * (3f - 2f * u));
                            pathWeight = smooth * pathOpacity;
                        }

                        // Also paint earthy dirt texture inside and around the summit grave hole
                        if (cfg.enableSummitGraveHole)
                        {
                            Vector2 graveCenter = cfg.pathEndXZ + cfg.graveHoleOffset;
                            float distGrave = Vector2.Distance(new Vector2(worldX, worldZ), graveCenter);
                            float graveRadius = Mathf.Max(cfg.graveHoleSize.x, cfg.graveHoleSize.y) * 0.75f;
                            if (distGrave < graveRadius)
                            {
                                float t = distGrave / graveRadius;
                                float graveBlend = 1f - t * t;
                                pathWeight = Mathf.Max(pathWeight, graveBlend * pathOpacity);
                            }
                        }
                    }

                    // 2. Off-path terrain: blend Rock on steep slopes, Grass on walkable ground
                    float nonPathWeight = 1f - pathWeight;

                    float slopeDeg = terrainData.GetSteepness(normX, normZ);
                    float rockFactor = 0f;
                    if (rockLayerIndex != -1)
                    {
                        rockFactor = Mathf.SmoothStep(
                            0f, 1f,
                            Mathf.InverseLerp(cfg.rockSlopeStartDegrees, cfg.rockSlopeFullDegrees, slopeDeg));
                    }

                    float rockWeight = rockFactor * nonPathWeight;
                    float grassWeight = Mathf.Max(0f, nonPathWeight - rockWeight);

                    // Normalize weights so they sum to 1.0
                    float sum = grassWeight + rockWeight + pathWeight;
                    if (sum > 0.0001f)
                    {
                        alphamaps[z, x, 0] = grassWeight / sum;
                        if (rockLayerIndex != -1) alphamaps[z, x, rockLayerIndex] = rockWeight / sum;
                        if (pathLayerIndex != -1) alphamaps[z, x, pathLayerIndex] = pathWeight / sum;
                    }
                    else
                    {
                        alphamaps[z, x, 0] = 1f;
                    }
                }
            }

            terrainData.SetAlphamaps(0, 0, alphamaps);
        }

        private float QueryDistanceToGoldenTrail(Vector2 worldXZ, MountainGenerationProfile cfg)
        {
            IReadOnlyList<GoldenPathNode> nodes = pathGenerator.Nodes;
            if (nodes == null || nodes.Count == 0) return float.MaxValue;

            float distToPolyline = pathGenerator.QueryClosestPointOnPath(worldXZ, out _, out _, out _);

            // Backward extension to spawn zone (Z < startXZ.y)
            if (worldXZ.y < cfg.pathStartXZ.y)
            {
                Vector2 startPos = nodes[0].positionXZ;
                Vector2 startFwd = nodes[0].forwardXZ.sqrMagnitude > 0.001f ? nodes[0].forwardXZ : new Vector2(0f, 1f);
                Vector2 diff = worldXZ - startPos;
                float proj = Vector2.Dot(diff, startFwd);
                // Connect player and cabin starting area without paving the entire back-forest
                if (proj < 0f && proj >= -8f)
                {
                    Vector2 closestOnRay = startPos + startFwd * proj;
                    float rayDist = Vector2.Distance(worldXZ, closestOnRay);
                    return Mathf.Min(distToPolyline, rayDist);
                }
            }
            // Forward extension to summit safe zone (Z > endXZ.y)
            else if (worldXZ.y > cfg.pathEndXZ.y)
            {
                GoldenPathNode lastNode = nodes[nodes.Count - 1];
                Vector2 endPos = lastNode.positionXZ;
                Vector2 endFwd = lastNode.forwardXZ.sqrMagnitude > 0.001f ? lastNode.forwardXZ : new Vector2(0f, 1f);
                Vector2 diff = worldXZ - endPos;
                float proj = Vector2.Dot(diff, endFwd);
                if (proj > 0f && proj <= (cfg.summitSafeLength + cfg.boundaryMargin * 0.5f))
                {
                    Vector2 closestOnRay = endPos + endFwd * proj;
                    float rayDist = Vector2.Distance(worldXZ, closestOnRay);
                    return Mathf.Min(distToPolyline, rayDist);
                }
            }

            return distToPolyline;
        }

        private TerrainLayer CreateFallbackPathLayer()
        {
            TerrainLayer layer = new TerrainLayer();
            layer.name = "ProceduralPathLayer";
            Texture2D pathTex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            Color pathColor = new Color(0.52f, 0.38f, 0.22f, 1f); // Distinct earthy trail brown
            Color[] pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = pathColor;
            pathTex.SetPixels(pixels);
            pathTex.Apply();
            layer.diffuseTexture = pathTex;
            layer.tileSize = new Vector2(3f, 3f);
            return layer;
        }

        private TerrainLayer CreateFallbackGrassLayer()
        {
            TerrainLayer layer = new TerrainLayer();
            layer.name = "ProceduralGrassLayer";
            Texture2D grassTex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            Color grassColor = new Color(0.27f, 0.43f, 0.19f, 1f);
            Color[] pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = grassColor;
            grassTex.SetPixels(pixels);
            grassTex.Apply();
            layer.diffuseTexture = grassTex;
            layer.tileSize = new Vector2(10f, 10f);
            return layer;
        }

        private void CreateSpawnContainers(
            out Transform anchorsParent,
            out Transform denseForestParent,
            out Transform hazardsParent,
            out Transform decorParent,
            out Transform boundariesParent,
            out Transform backdropsParent)
        {
            GameObject rootGO = new GameObject("Generated_Mountain_Objects");
            rootGO.transform.SetParent(transform, false);
            generatedRoot = rootGO.transform;

            GameObject anchorsGO = new GameObject("1_GoldenPath_GuaranteedAnchors");
            anchorsGO.transform.SetParent(generatedRoot, false);
            anchorsParent = anchorsGO.transform;

            GameObject forestGO = new GameObject("2_DenseForest_Trees");
            forestGO.transform.SetParent(generatedRoot, false);
            denseForestParent = forestGO.transform;

            GameObject hazardsGO = new GameObject("3_SlopeHazards_And_Rocks");
            hazardsGO.transform.SetParent(generatedRoot, false);
            hazardsParent = hazardsGO.transform;

            GameObject decorGO = new GameObject("4_Decorative_SmallObjects");
            decorGO.transform.SetParent(generatedRoot, false);
            decorParent = decorGO.transform;

            GameObject boundsGO = new GameObject("BoundaryColliders");
            boundsGO.transform.SetParent(generatedRoot, false);
            boundariesParent = boundsGO.transform;

            GameObject backdropsGO = new GameObject("5_Distant_Mountain_Backdrops");
            backdropsGO.transform.SetParent(generatedRoot, false);
            backdropsParent = backdropsGO.transform;
        }

        private void SpawnMountainBackdrops(
            MountainGenerationProfile cfg,
            Transform parent,
            Vector3 terrainOrigin,
            Vector3 terrainSize)
        {
            MountainBackdropManager backdropMgr = parent.gameObject.GetComponent<MountainBackdropManager>();
            if (backdropMgr == null)
            {
                backdropMgr = parent.gameObject.AddComponent<MountainBackdropManager>();
            }

            backdropMgr.InitializeDefaultLayers();
            float targetY = overrideBackdropBaseY ? customBackdropBaseY : cfg.mountainBackdropsBaseY;
            backdropMgr.SetBaseY(targetY);
            backdropMgr.GenerateBackdrops();
        }

        /// <summary>
        /// Stage 5: Guaranteed-Solvable Anchor Spawning.
        /// At each node along the Golden Path (every 8-12m from (0,0) to (0,250)),
        /// picks a random tree or rock from the shared treePrefabs & rockPrefabs pools.
        /// </summary>
        private int SpawnGoldenPathAnchors(
            MountainGenerationProfile cfg,
            Transform parent,
            int stageSeed)
        {
            bool hasTrees = HasValidEntry(cfg.treePrefabs);
            bool hasRocks = HasValidEntry(cfg.rockPrefabs);

            if (!hasTrees && !hasRocks)
            {
                return 0;
            }

            System.Random rng = new System.Random(stageSeed);
            IReadOnlyList<GoldenPathNode> nodes = pathGenerator.Nodes;
            int spawnedCount = 0;

            float minLateral = Mathf.Min(cfg.anchorLateralOffsetRange.x, cfg.anchorLateralOffsetRange.y);
            float maxLateral = Mathf.Max(cfg.anchorLateralOffsetRange.x, cfg.anchorLateralOffsetRange.y);

            for (int i = 0; i < nodes.Count; i++)
            {
                GoldenPathNode node = nodes[i];

                int primarySide = (rng.NextDouble() < 0.5) ? 1 : -1;
                int countForThisNode = cfg.anchorsPerPathNode;
                if (rng.NextDouble() < cfg.bonusSecondAnchorChance)
                {
                    countForThisNode++;
                }

                for (int a = 0; a < countForThisNode; a++)
                {
                    // Pick from treePrefabs or rockPrefabs based on goldenPathTreeRatio
                    bool pickTree = (rng.NextDouble() < cfg.goldenPathTreeRatio && hasTrees) || !hasRocks;
                    List<SpawnablePrefabEntry> chosenPool = pickTree ? cfg.treePrefabs : cfg.rockPrefabs;
                    SpawnablePrefabEntry chosenEntry = SelectWeightedEntry(chosenPool, rng);

                    if (chosenEntry == null || chosenEntry.prefab == null) continue;

                    int side = (a % 2 == 0) ? primarySide : -primarySide;
                    float lateralDist = Mathf.Lerp(minLateral, maxLateral, (float)rng.NextDouble());
                    float forwardJitter = ((float)rng.NextDouble() * 2f - 1f) * 1.75f;

                    Vector2 spawnXZ = node.positionXZ + node.rightXZ * (side * lateralDist) + node.forwardXZ * forwardJitter;

                    if (!occupancyGrid.IsFree(spawnXZ, chosenEntry.footprintRadius * 0.8f))
                        continue;

                    if (TrySpawnEntryAtXZ(chosenEntry, spawnXZ, parent, rng, cfg.anchorableLayerName, forceAnchorable: true, isTree: pickTree))
                    {
                        occupancyGrid.Register(spawnXZ, chosenEntry.footprintRadius);
                        spawnedCount++;
                    }
                }
            }

            return spawnedCount;
        }

        /// <summary>
        /// Stage 6A: Dense Forest of Trees.
        /// Fills the mountain terrain with a dense forest using the multiple treePrefabs.
        /// All trees are anchorable by the winch and possess colliders.
        /// </summary>
        private int SpawnDenseForest(
            MountainGenerationProfile cfg,
            Transform parent,
            int stageSeed)
        {
            if (!HasValidEntry(cfg.treePrefabs))
                return 0;

            System.Random rng = new System.Random(stageSeed);

            float halfWidth = cfg.playableWidth * 0.5f;
            float terrainStartZ = cfg.pathStartXZ.y - cfg.forestBehindPlayerDistance;
            float zMin = terrainStartZ + 2.5f;
            float zMax = cfg.pathEndXZ.y + cfg.summitSafeLength;

            Rect sampleBounds = new Rect(-halfWidth, zMin, halfWidth * 2f, zMax - zMin);
            List<Vector2> candidates = PoissonDiskSampler.GeneratePoints(sampleBounds, cfg.denseForestPoissonRadius, rng);

            int spawnedCount = 0;

            for (int i = 0; i < candidates.Count; i++)
            {
                Vector2 ptXZ = candidates[i];

                // Keep golden path center clear so coffin and player aren't blocked
                float distToPath = pathGenerator.QueryClosestPointOnPath(ptXZ, out _, out _, out _);
                if (distToPath < cfg.goldenPathTreeClearance)
                    continue;

                if (!SampleTerrainDataAtWorldXZ(ptXZ, out _, out _, out float slopeDegrees))
                    continue;

                // Trees do not grow on extreme rocky cliffs
                if (slopeDegrees > cfg.maxTreeSlopeDegrees)
                    continue;

                if ((float)rng.NextDouble() > cfg.denseForestSpawnChance)
                    continue;

                // Randomly select a tree prefab from the configured multiple tree prefabs
                SpawnablePrefabEntry treeEntry = SelectWeightedEntry(cfg.treePrefabs, rng);
                if (treeEntry == null || treeEntry.prefab == null)
                    continue;

                if (!occupancyGrid.IsFree(ptXZ, treeEntry.footprintRadius))
                    continue;

                if (TrySpawnEntryAtXZ(treeEntry, ptXZ, parent, rng, cfg.anchorableLayerName, forceAnchorable: true, isTree: true))
                {
                    occupancyGrid.Register(ptXZ, treeEntry.footprintRadius);
                    spawnedCount++;
                }
            }

            return spawnedCount;
        }

        /// <summary>
        /// Stage 6B: Spawns slope hazards & rocks:
        /// - Low slopes (< 20°): Leaf piles (low friction) & deadfall logs
        /// - High slopes (> 38°): Jagged rocks from rockPrefabs and loose scree zones
        /// </summary>
        private int SpawnSlopeHazardsAndRocks(
            MountainGenerationProfile cfg,
            Transform parent,
            int stageSeed)
        {
            System.Random rng = new System.Random(stageSeed);

            float halfWidth = cfg.playableWidth * 0.5f + cfg.boundaryMargin * 0.2f;
            float zMin = cfg.pathStartXZ.y - 2f;
            float zMax = cfg.pathEndXZ.y + cfg.summitSafeLength + 2f;

            Rect sampleBounds = new Rect(-halfWidth, zMin, halfWidth * 2f, zMax - zMin);
            List<Vector2> candidates = PoissonDiskSampler.GeneratePoints(sampleBounds, 4.5f, rng);

            int spawnedCount = 0;

            for (int i = 0; i < candidates.Count; i++)
            {
                Vector2 ptXZ = candidates[i];

                float distToPath = pathGenerator.QueryClosestPointOnPath(ptXZ, out _, out _, out _);
                if (distToPath < cfg.goldenPathTreeClearance * 0.8f)
                    continue;

                if (!SampleTerrainDataAtWorldXZ(ptXZ, out _, out _, out float slopeDegrees))
                    continue;

                SpawnablePrefabEntry entryToSpawn = null;
                bool isAnchor = false;

                if (slopeDegrees < cfg.lowSlopeMaxDegrees)
                {
                    if (HasValidEntry(cfg.lowSlopeHazards) && (float)rng.NextDouble() < cfg.lowSlopeHazardChance)
                    {
                        entryToSpawn = SelectWeightedEntry(cfg.lowSlopeHazards, rng);
                        isAnchor = entryToSpawn != null && entryToSpawn.isAnchorable;
                    }
                }
                else if (slopeDegrees >= cfg.highSlopeMinDegrees)
                {
                    if ((float)rng.NextDouble() < cfg.highSlopeHazardChance)
                    {
                        bool pickRock = (rng.NextDouble() < 0.65 && HasValidEntry(cfg.rockPrefabs)) || !HasValidEntry(cfg.highSlopeHazards);
                        if (pickRock && HasValidEntry(cfg.rockPrefabs))
                        {
                            entryToSpawn = SelectWeightedEntry(cfg.rockPrefabs, rng);
                            isAnchor = true;
                        }
                        else if (HasValidEntry(cfg.highSlopeHazards))
                        {
                            entryToSpawn = SelectWeightedEntry(cfg.highSlopeHazards, rng);
                            isAnchor = false;
                        }
                    }
                }

                if (entryToSpawn == null || entryToSpawn.prefab == null)
                    continue;

                if (!occupancyGrid.IsFree(ptXZ, entryToSpawn.footprintRadius))
                    continue;

                if (TrySpawnEntryAtXZ(entryToSpawn, ptXZ, parent, rng, cfg.anchorableLayerName, forceAnchorable: isAnchor))
                {
                    occupancyGrid.Register(ptXZ, entryToSpawn.footprintRadius);
                    spawnedCount++;
                }
            }

            return spawnedCount;
        }

        /// <summary>
        /// Stage 6C: Secondary Poisson Disk Sampling pass for decorative small objects (small rocks, bushes, shrubs).
        /// </summary>
        private int SpawnDecorativeObjects(
            MountainGenerationProfile cfg,
            Transform parent,
            int stageSeed)
        {
            if (cfg.decorativeRules == null || cfg.decorativeRules.Count == 0)
                return 0;

            System.Random rng = new System.Random(stageSeed);

            float halfWidth = cfg.playableWidth * 0.5f;
            float terrainStartZ = cfg.pathStartXZ.y - cfg.forestBehindPlayerDistance;
            float zMin = terrainStartZ + 2.5f;
            float zMax = cfg.pathEndXZ.y + cfg.summitSafeLength;

            Rect sampleBounds = new Rect(-halfWidth, zMin, halfWidth * 2f, zMax - zMin);
            List<Vector2> candidates = PoissonDiskSampler.GeneratePoints(sampleBounds, cfg.decorativePoissonRadius, rng);

            float totalClimbZ = Mathf.Max(1f, cfg.pathEndXZ.y - cfg.pathStartXZ.y);
            List<DecorativeScatterRule> eligibleRules = new List<DecorativeScatterRule>();
            int spawnedCount = 0;

            for (int i = 0; i < candidates.Count; i++)
            {
                if ((float)rng.NextDouble() > cfg.decorativeSpawnChance)
                    continue;

                Vector2 ptXZ = candidates[i];
                if (!SampleTerrainDataAtWorldXZ(ptXZ, out _, out _, out float slopeDegrees))
                    continue;

                float progressZ = Mathf.Clamp01((ptXZ.y - cfg.pathStartXZ.y) / totalClimbZ);
                float distToPath = pathGenerator.QueryClosestPointOnPath(ptXZ, out _, out _, out _);
                bool isOnPath = distToPath < cfg.pathInnerCarveRadius;

                eligibleRules.Clear();
                float totalWeight = 0f;

                for (int r = 0; r < cfg.decorativeRules.Count; r++)
                {
                    DecorativeScatterRule rule = cfg.decorativeRules[r];
                    if (rule == null || !rule.enabled || rule.prefabEntry == null || rule.prefabEntry.prefab == null)
                        continue;

                    if (isOnPath && !rule.allowOnGoldenPath)
                        continue;

                    if (slopeDegrees < rule.allowedSlopeRange.x || slopeDegrees > rule.allowedSlopeRange.y)
                        continue;

                    if (progressZ < rule.allowedProgressRange.x || progressZ > rule.allowedProgressRange.y)
                        continue;

                    eligibleRules.Add(rule);
                    totalWeight += Mathf.Max(0.01f, rule.prefabEntry.weight);
                }

                if (eligibleRules.Count == 0)
                    continue;

                float pick = (float)rng.NextDouble() * totalWeight;
                DecorativeScatterRule chosenRule = eligibleRules[eligibleRules.Count - 1];
                float accum = 0f;
                for (int r = 0; r < eligibleRules.Count; r++)
                {
                    accum += Mathf.Max(0.01f, eligibleRules[r].prefabEntry.weight);
                    if (pick <= accum)
                    {
                        chosenRule = eligibleRules[r];
                        break;
                    }
                }

                SpawnablePrefabEntry entry = chosenRule.prefabEntry;
                if (!occupancyGrid.IsFree(ptXZ, entry.footprintRadius))
                    continue;

                if (TrySpawnEntryAtXZ(entry, ptXZ, parent, rng, cfg.anchorableLayerName, forceAnchorable: false))
                {
                    occupancyGrid.Register(ptXZ, entry.footprintRadius);
                    spawnedCount++;
                }
            }

            return spawnedCount;
        }

        /// <summary>
        /// Stage 7: Spawns 4 invisible BoxColliders (West, East, South, North)
        /// enclosing the playable area so the player and coffin cannot leave.
        /// </summary>
        private void SpawnInvisibleBoundaryColliders(
            MountainGenerationProfile cfg,
            Transform parent)
        {
            float halfWidth = cfg.playableWidth * 0.5f;
            float terrainStartZ = cfg.pathStartXZ.y - cfg.forestBehindPlayerDistance;
            float summitEndZ = cfg.pathEndXZ.y + cfg.summitSafeLength;
            float lengthZ = summitEndZ - terrainStartZ;
            float midZ = (terrainStartZ + summitEndZ) * 0.5f;

            float wallH = cfg.boundaryWallHeight;
            float wallT = cfg.boundaryWallThickness;
            float halfElev = cfg.targetElevationGain * 0.5f;

            // 1. West Wall (Left: X = -halfWidth)
            CreateBoundaryWall(
                "Boundary_West_Left",
                new Vector3(-halfWidth - wallT * 0.5f, halfElev, midZ),
                new Vector3(wallT, wallH * 2.5f, lengthZ + wallT * 2f),
                parent);

            // 2. East Wall (Right: X = +halfWidth)
            CreateBoundaryWall(
                "Boundary_East_Right",
                new Vector3(halfWidth + wallT * 0.5f, halfElev, midZ),
                new Vector3(wallT, wallH * 2.5f, lengthZ + wallT * 2f),
                parent);

            // 3. South Wall (Start / Back: Z = terrainStartZ)
            CreateBoundaryWall(
                "Boundary_South_Start",
                new Vector3(0f, halfElev, terrainStartZ - wallT * 0.5f),
                new Vector3(cfg.playableWidth + wallT * 2f, wallH * 2.5f, wallT),
                parent);

            // 4. North Wall (Summit / End: Z = summitEndZ)
            CreateBoundaryWall(
                "Boundary_North_Summit",
                new Vector3(0f, cfg.targetElevationGain, summitEndZ + wallT * 0.5f),
                new Vector3(cfg.playableWidth + wallT * 2f, wallH * 2.5f, wallT),
                parent);
        }

        private void CreateBoundaryWall(string wallName, Vector3 center, Vector3 size, Transform parent)
        {
            GameObject wallGO = new GameObject(wallName);
            wallGO.transform.SetParent(parent, false);
            wallGO.transform.position = center;

            BoxCollider col = wallGO.AddComponent<BoxCollider>();
            col.size = size;
            col.isTrigger = false;
        }

        private bool TrySpawnEntryAtXZ(
            SpawnablePrefabEntry entry,
            Vector2 worldXZ,
            Transform parent,
            System.Random rng,
            string anchorableLayerName,
            bool forceAnchorable,
            bool isTree = false)
        {
            if (entry == null || entry.prefab == null)
                return false;

            if (!SampleTerrainDataAtWorldXZ(worldXZ, out float worldY, out Vector3 normal, out _))
                return false;

            Vector3 spawnPos = new Vector3(worldXZ.x, worldY + entry.verticalEmbedOffset, worldXZ.y);

            Vector3 upAxis = Vector3.Slerp(Vector3.up, normal, Mathf.Clamp01(entry.alignToNormal)).normalized;
            float randomYaw = (float)rng.NextDouble() * 360f;
            Quaternion baseRotation = Quaternion.FromToRotation(Vector3.up, upAxis) * Quaternion.Euler(0f, randomYaw, 0f);

            if (entry.randomTiltDegrees > 0.01f)
            {
                float tiltX = ((float)rng.NextDouble() * 2f - 1f) * entry.randomTiltDegrees;
                float tiltZ = ((float)rng.NextDouble() * 2f - 1f) * entry.randomTiltDegrees;
                baseRotation = baseRotation * Quaternion.Euler(tiltX, 0f, tiltZ);
            }

            float minScale = Mathf.Min(entry.scaleRange.x, entry.scaleRange.y);
            float maxScale = Mathf.Max(entry.scaleRange.x, entry.scaleRange.y);
            float uniformScale = Mathf.Lerp(minScale, maxScale, (float)rng.NextDouble());

            GameObject instance = Instantiate(entry.prefab, spawnPos, baseRotation, parent);
            instance.name = string.IsNullOrEmpty(entry.label) ? entry.prefab.name : entry.label;
            instance.transform.localScale = Vector3.Scale(instance.transform.localScale, Vector3.one * uniformScale);

            if (forceAnchorable || entry.isAnchorable)
            {
                int anchorLayer = LayerMask.NameToLayer(anchorableLayerName);
                if (anchorLayer != -1)
                {
                    SetLayerRecursively(instance.transform, anchorLayer);
                }
            }

            // Random material assignment
            MountainGenerationProfile activeProfile = GetOrCreateActiveProfile();
            Material chosenMaterial = SelectMaterialForObject(entry, isTree, worldXZ, worldY, activeProfile, rng);
            if (chosenMaterial != null)
            {
                ApplyMaterialToInstance(instance, chosenMaterial, entry.targetMaterialSlot);
            }

            return true;
        }

        private Material SelectMaterialForObject(
            SpawnablePrefabEntry entry,
            bool isTree,
            Vector2 worldXZ,
            float worldY,
            MountainGenerationProfile cfg,
            System.Random rng)
        {
            if (entry != null && entry.materialOverrides != null && entry.materialOverrides.Count > 0)
            {
                return PickMaterialFromList(entry.materialOverrides, null, rng);
            }

            if (isTree && cfg != null && cfg.randomizeTreeMaterials && cfg.treeMaterials != null && cfg.treeMaterials.Count > 0)
            {
                switch (cfg.treeMaterialDistribution)
                {
                    case TreeMaterialDistribution.ElevationGradient:
                        return PickMaterialByElevation(cfg.treeMaterials, worldY, cfg, rng);

                    case TreeMaterialDistribution.NoiseClustered:
                        return PickMaterialByNoise(cfg.treeMaterials, worldXZ, cfg, rng);

                    case TreeMaterialDistribution.Random:
                    default:
                        return PickMaterialFromList(cfg.treeMaterials, cfg.treeMaterialWeights, rng);
                }
            }

            return null;
        }

        private Material PickMaterialFromList(List<Material> list, List<float> weights, System.Random rng)
        {
            if (list == null || list.Count == 0) return null;

            if (weights != null && weights.Count == list.Count)
            {
                float totalWeight = 0f;
                for (int i = 0; i < weights.Count; i++)
                {
                    if (list[i] != null && weights[i] > 0f) totalWeight += weights[i];
                }

                if (totalWeight > 0.0001f)
                {
                    float pick = (float)rng.NextDouble() * totalWeight;
                    float accum = 0f;
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (list[i] == null || weights[i] <= 0f) continue;
                        accum += weights[i];
                        if (pick <= accum) return list[i];
                    }
                }
            }

            int validCount = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null) validCount++;
            }
            if (validCount == 0) return null;

            int pickIdx = rng.Next(validCount);
            int current = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null)
                {
                    if (current == pickIdx) return list[i];
                    current++;
                }
            }

            return list[0];
        }

        private Material PickMaterialByElevation(List<Material> list, float worldY, MountainGenerationProfile cfg, System.Random rng)
        {
            if (list == null || list.Count == 0) return null;

            float minElev = 0f;
            float maxElev = cfg.targetElevationGain;
            float normElev = Mathf.Clamp01((worldY - minElev) / Mathf.Max(1f, maxElev - minElev));

            // Organic jitter along contour lines
            float jitter = ((float)rng.NextDouble() * 2f - 1f) * 0.08f;
            float t = Mathf.Clamp01(normElev + jitter);

            int idx = Mathf.Clamp(Mathf.FloorToInt(t * list.Count), 0, list.Count - 1);
            return list[idx];
        }

        private Material PickMaterialByNoise(List<Material> list, Vector2 worldXZ, MountainGenerationProfile cfg, System.Random rng)
        {
            if (list == null || list.Count == 0) return null;

            float scale = Mathf.Max(0.001f, cfg.treeMaterialClusterScale);
            float noiseVal = Mathf.PerlinNoise(worldXZ.x * scale + (seed * 0.17f), worldXZ.y * scale + (seed * 0.23f));

            float jitter = ((float)rng.NextDouble() * 2f - 1f) * 0.06f;
            float t = Mathf.Clamp01(noiseVal + jitter);

            int idx = Mathf.Clamp(Mathf.FloorToInt(t * list.Count), 0, list.Count - 1);
            return list[idx];
        }

        private void ApplyMaterialToInstance(GameObject instance, Material material, int targetSlot)
        {
            if (instance == null || material == null) return;

            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer rend = renderers[i];
                if (rend == null || !rend.enabled) continue;

                Material[] sharedMats = rend.sharedMaterials;
                if (sharedMats == null || sharedMats.Length == 0) continue;

                if (targetSlot < 0 || targetSlot >= sharedMats.Length)
                {
                    for (int m = 0; m < sharedMats.Length; m++)
                    {
                        sharedMats[m] = material;
                    }
                }
                else
                {
                    sharedMats[targetSlot] = material;
                }

                rend.sharedMaterials = sharedMats;
            }
        }

        private void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            foreach (Transform child in root)
            {
                SetLayerRecursively(child, layer);
            }
        }

        private bool SampleTerrainDataAtWorldXZ(
            Vector2 worldXZ,
            out float worldY,
            out Vector3 surfaceNormal,
            out float slopeDegrees)
        {
            worldY = 0f;
            surfaceNormal = Vector3.up;
            slopeDegrees = 0f;

            if (targetTerrain == null || targetTerrain.terrainData == null)
                return false;

            TerrainData td = targetTerrain.terrainData;
            Vector3 origin = targetTerrain.transform.position;
            Vector3 size = td.size;

            float normX = (worldXZ.x - origin.x) / size.x;
            float normZ = (worldXZ.y - origin.z) / size.z;

            if (normX < 0f || normX > 1f || normZ < 0f || normZ > 1f)
                return false;

            worldY = origin.y + td.GetInterpolatedHeight(normX, normZ);
            surfaceNormal = td.GetInterpolatedNormal(normX, normZ);
            slopeDegrees = td.GetSteepness(normX, normZ);
            return true;
        }

        private bool HasValidEntry(List<SpawnablePrefabEntry> pool)
        {
            if (pool == null) return false;
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i] != null && pool[i].prefab != null && pool[i].weight > 0f)
                    return true;
            }
            return false;
        }

        private SpawnablePrefabEntry SelectWeightedEntry(List<SpawnablePrefabEntry> pool, System.Random rng)
        {
            if (pool == null || pool.Count == 0) return null;

            float totalWeight = 0f;
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i] != null && pool[i].prefab != null && pool[i].weight > 0f)
                {
                    totalWeight += pool[i].weight;
                }
            }

            if (totalWeight <= 0.0001f) return null;

            float pick = (float)rng.NextDouble() * totalWeight;
            float accum = 0f;

            for (int i = 0; i < pool.Count; i++)
            {
                SpawnablePrefabEntry entry = pool[i];
                if (entry == null || entry.prefab == null || entry.weight <= 0f)
                    continue;

                accum += entry.weight;
                if (pick <= accum)
                {
                    return entry;
                }
            }

            return null;
        }

        private void PositionStartingActors(MountainGenerationProfile cfg)
        {
            if (playerTransform == null)
            {
                FirstPersonController fp = FindFirstObjectByType<FirstPersonController>();
                if (fp != null) playerTransform = fp.transform;
            }

            if (coffinTransform == null)
            {
                CoffinPullable coffin = FindFirstObjectByType<CoffinPullable>();
                if (coffin != null) coffinTransform = coffin.transform;
            }

            if (cabinTransform == null)
            {
                // Auto-resolve Cabin GameObject in scene by checking standard names (supports Turkish dotless 'ı' or standard 'i')
                GameObject cabinObj = GameObject.Find("Cabın");
                if (cabinObj == null) cabinObj = GameObject.Find("Cabin");
                if (cabinObj != null) cabinTransform = cabinObj.transform;
            }

            Vector2 startXZ = cfg.pathStartXZ;

            if (playerTransform != null)
            {
                Vector2 playerXZ = startXZ + new Vector2(playerStartOffset.x, playerStartOffset.z);
                if (SampleTerrainDataAtWorldXZ(playerXZ, out float groundY, out _, out _))
                {
                    CharacterController cc = playerTransform.GetComponent<CharacterController>();
                    if (cc != null) cc.enabled = false;
                    playerTransform.position = new Vector3(playerXZ.x, groundY + playerStartOffset.y, playerXZ.y);
                    playerTransform.rotation = Quaternion.identity;
                    if (cc != null) cc.enabled = true;
                }
            }

            if (coffinTransform != null)
            {
                Vector2 coffinXZ = startXZ + new Vector2(coffinStartOffset.x, coffinStartOffset.z);
                if (SampleTerrainDataAtWorldXZ(coffinXZ, out float groundY, out Vector3 normal, out _))
                {
                    Rigidbody rb = coffinTransform.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                    }
                    coffinTransform.position = new Vector3(coffinXZ.x, groundY + coffinStartOffset.y, coffinXZ.y);
                    coffinTransform.rotation = Quaternion.FromToRotation(Vector3.up, normal);
                }
            }

            if (cabinTransform != null)
            {
                cabinTransform.rotation = Quaternion.Euler(cabinRotationEuler);
                Vector2 cabinXZ = startXZ + new Vector2(cabinStartOffset.x, cabinStartOffset.z);

                if (SampleTerrainDataAtWorldXZ(cabinXZ, out float groundY, out _, out _))
                {
                    // Temporarily set position at groundY to sample its bounding box with current rotation
                    cabinTransform.position = new Vector3(cabinXZ.x, groundY, cabinXZ.y);

                    float bottomOffset = 0f;
                    Renderer rend = cabinTransform.GetComponentInChildren<Renderer>();
                    if (rend != null)
                    {
                        bottomOffset = cabinTransform.position.y - rend.bounds.min.y;
                    }
                    else
                    {
                        Collider col = cabinTransform.GetComponentInChildren<Collider>();
                        if (col != null)
                        {
                            bottomOffset = cabinTransform.position.y - col.bounds.min.y;
                        }
                    }

                    cabinTransform.position = new Vector3(cabinXZ.x, groundY + bottomOffset + cabinStartOffset.y, cabinXZ.y);

                    Rigidbody rb = cabinTransform.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                    }
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (pathGenerator == null || pathGenerator.Nodes == null || pathGenerator.Nodes.Count == 0)
                return;

            if (drawGoldenPathGizmos)
            {
                IReadOnlyList<GoldenPathNode> nodes = pathGenerator.Nodes;
                for (int i = 0; i < nodes.Count; i++)
                {
                    GoldenPathNode n = nodes[i];
                    float worldY = n.elevation;
                    if (SampleTerrainDataAtWorldXZ(n.positionXZ, out float sampledY, out _, out _))
                    {
                        worldY = sampledY;
                    }

                    Vector3 pt = new Vector3(n.positionXZ.x, worldY + 0.5f, n.positionXZ.y);
                    Gizmos.color = n.isInSafeZone ? Color.cyan : new Color(1f, 0.82f, 0.1f, 0.95f);
                    Gizmos.DrawSphere(pt, 0.65f);

                    if (i < nodes.Count - 1)
                    {
                        GoldenPathNode next = nodes[i + 1];
                        float nextY = next.elevation;
                        if (SampleTerrainDataAtWorldXZ(next.positionXZ, out float sampledNextY, out _, out _))
                        {
                            nextY = sampledNextY;
                        }
                        Vector3 nextPt = new Vector3(next.positionXZ.x, nextY + 0.5f, next.positionXZ.y);
                        Gizmos.DrawLine(pt, nextPt);
                    }
                }
            }

            if (drawSafeZoneGizmos && pathGenerator.SafeZones != null)
            {
                MountainGenerationProfile cfg = profile;
                float width = cfg != null ? cfg.playableWidth : 100f;
                Gizmos.color = new Color(0.1f, 0.9f, 0.4f, 0.22f);

                foreach (SafePlateauZone zone in pathGenerator.SafeZones)
                {
                    float lenZ = zone.endZ - zone.startZ;
                    Vector3 center = new Vector3(0f, zone.baseElevation + 0.5f, (zone.startZ + zone.endZ) * 0.5f);
                    Gizmos.DrawCube(center, new Vector3(width * 0.7f, 0.5f, lenZ));
                }
            }

            if (drawBoundaryGizmos)
            {
                MountainGenerationProfile cfg = profile;
                if (cfg != null && cfg.spawnInvisibleBoundaries)
                {
                    Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.35f);
                    float halfWidth = cfg.playableWidth * 0.5f;
                    float terrainStartZ = cfg.pathStartXZ.y - cfg.forestBehindPlayerDistance;
                    float summitEndZ = cfg.pathEndXZ.y + cfg.summitSafeLength;
                    float lengthZ = summitEndZ - terrainStartZ;
                    float midZ = (terrainStartZ + summitEndZ) * 0.5f;
                    float halfElev = cfg.targetElevationGain * 0.5f;
                    float wallH = cfg.boundaryWallHeight;

                    // Wire boundary box visualization
                    Gizmos.DrawWireCube(
                        new Vector3(0f, halfElev, midZ),
                        new Vector3(cfg.playableWidth, wallH, lengthZ));
                }
            }

            if (profile != null && profile.enableSummitGraveHole)
            {
                Gizmos.color = new Color(0.9f, 0.55f, 0.15f, 0.85f);
                Vector2 graveCenter = profile.pathEndXZ + profile.graveHoleOffset;
                float graveY = targetTerrain != null ? targetTerrain.SampleHeight(new Vector3(graveCenter.x, 0f, graveCenter.y)) + targetTerrain.transform.position.y : profile.targetElevationGain;
                Vector3 center = new Vector3(graveCenter.x, graveY + profile.graveHoleDepth * 0.5f, graveCenter.y);
                Gizmos.DrawWireCube(center, new Vector3(profile.graveHoleSize.x, profile.graveHoleDepth + 1.2f, profile.graveHoleSize.y));
            }
        }

        private void SetupSummitGraveMarker(MountainGenerationProfile cfg)
        {
            Vector2 graveCenterXZ = cfg.pathEndXZ + cfg.graveHoleOffset;
            float sampledY = 0f;
            if (targetTerrain != null)
            {
                sampledY = targetTerrain.SampleHeight(new Vector3(graveCenterXZ.x, 0f, graveCenterXZ.y)) + targetTerrain.transform.position.y;
            }
            else
            {
                sampledY = cfg.targetElevationGain;
            }

            Vector3 markerPos = new Vector3(graveCenterXZ.x, sampledY + 0.1f, graveCenterXZ.y);

            SummitRestingMarker marker = FindAnyObjectByType<SummitRestingMarker>();
            if (marker == null)
            {
                GameObject markerObj = new GameObject("SummitRestingMarker");
                marker = markerObj.AddComponent<SummitRestingMarker>();
            }

            marker.transform.position = markerPos;
            marker.ConfigureGraveTrigger(cfg.graveHoleSize, cfg.graveHoleDepth);
            Debug.Log($"[ProceduralMountainGenerator] Summit grave hole marker positioned accurately at {markerPos} with trigger bounds.");
        }
    }
}
