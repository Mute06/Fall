using System;
using System.Collections.Generic;
using UnityEngine;

namespace FirstPersonSystem.ProceduralGeneration
{
    [ExecuteAlways]
    public class ProceduralMountainGenerator : MonoBehaviour
    {
        [Header("Configuration Profile")]
        [Tooltip("Configurable ScriptableObject profile containing all terrain, path, slope, and asset settings. If left null, a default in-memory profile will be used.")]
        [SerializeField] private MountainGenerationProfile profile;

        [Header("Seed & Generation Trigger")]
        [SerializeField] private int seed = 1337;
        [SerializeField] private bool randomizeSeedOnStart = false;
        [SerializeField] private bool generateOnStart = true;

        [Header("Target Terrain & Actors")]
        [Tooltip("Target Unity Terrain. If null, one will be found or created automatically as a child.")]
        [SerializeField] private Terrain targetTerrain;

        [Tooltip("Optional Player Transform to place at the starting flat area upon generation.")]
        [SerializeField] private Transform playerTransform;

        [Tooltip("Optional Coffin Transform (or CoffinPullable) to place next to the player at the starting flat area.")]
        [SerializeField] private Transform coffinTransform;

        [SerializeField] private Vector3 playerStartOffset = new Vector3(-1.5f, 1.15f, 6f);
        [SerializeField] private Vector3 coffinStartOffset = new Vector3(1.2f, 0.6f, 8.5f);
        [SerializeField] private bool repositionActorsOnGenerate = true;

        [Header("Debug & Scene Visualization")]
        [SerializeField] private bool drawGoldenPathGizmos = true;
        [SerializeField] private bool drawSafeZoneGizmos = true;

        // Runtime / Cached State
        private GoldenPathGenerator pathGenerator = new GoldenPathGenerator();
        private SpatialOccupancyGrid occupancyGrid = new SpatialOccupancyGrid(4f);
        private Transform generatedRoot;

        public MountainGenerationProfile Profile => profile;
        public int Seed
        {
            get => seed;
            set => seed = value;
        }
        public IReadOnlyList<GoldenPathNode> GoldenPathNodes => pathGenerator?.Nodes;
        public IReadOnlyList<SafePlateauZone> SafeZones => pathGenerator?.SafeZones;

        private void Start()
        {
            if (!Application.isPlaying) return;

            if (generateOnStart)
            {
                if (randomizeSeedOnStart)
                {
                    seed = UnityEngine.Random.Range(1, 999999);
                }
                GenerateMountain();
            }
        }

        /// <summary>
        /// Executes the full 6-stage procedural mountain and object placement algorithm.
        /// Safe to call both in Edit Mode and Play Mode.
        /// </summary>
        [ContextMenu("Generate Mountain")]
        public void GenerateMountain()
        {
            MountainGenerationProfile activeProfile = GetOrCreateActiveProfile();

            ClearSpawnedObjects();
            EnsureTerrainSetup(activeProfile, out TerrainData terrainData, out Vector3 terrainOrigin);

            // Stage 1: Golden Path (Random Walk with Momentum from (0,0) to (0,250) stepping 8-12m)
            pathGenerator.Generate(activeProfile, seed);

            // Stage 2 & 3: Stepped Longitudinal Elevation + 2D Heightmap Synthesis + Path Carving + Valley Cliffs
            SynthesizeHeightmap(activeProfile, terrainData, terrainOrigin, seed);

            // Stage 4: Splatmap Painting (Grass default ground + optional Rock/Path layers)
            ApplyTerrainTextures(activeProfile, terrainData);

            // Prepare container hierarchy for spawned prefabs
            CreateSpawnContainers(out Transform anchorsParent, out Transform slopePropsParent, out Transform decorParent);
            occupancyGrid.Clear();

            // Reserve spawn clearance at Player & Coffin start area
            Vector2 startSpawnXZ = activeProfile.pathStartXZ + new Vector2(0f, 7f);
            occupancyGrid.Register(startSpawnXZ, 4.5f);

            // Stage 5: Guaranteed-Solvable Anchor Spawning along Golden Path nodes
            int anchorCount = SpawnGoldenPathAnchors(activeProfile, anchorsParent, seed + 101);

            // Stage 6A: Slope-Stratified Poisson Disk Sampling (<20°, 20°-38°, >38°)
            int slopePropCount = SpawnSlopeStratifiedObjects(activeProfile, slopePropsParent, seed + 202);

            // Stage 6B: Decorative Small Objects Scatter (Small Rocks, Bushes, etc.)
            int decorCount = SpawnDecorativeObjects(activeProfile, decorParent, seed + 303);

            // Position Player & Coffin on the starting flat zone if requested
            if (repositionActorsOnGenerate)
            {
                PositionStartingActors(activeProfile);
            }

            Debug.Log($"[ProceduralMountainGenerator] Generated mountain (Seed: {seed}, Elevation Gain: {activeProfile.targetElevationGain}m, Golden Path Nodes: {pathGenerator.Nodes.Count}, Guaranteed Anchors: {anchorCount}, Slope Objects: {slopePropCount}, Decorations: {decorCount}).");
        }

        [ContextMenu("Randomize Seed & Generate")]
        public void RandomizeSeedAndGenerate()
        {
            seed = UnityEngine.Random.Range(1, 999999);
            GenerateMountain();
        }

        [ContextMenu("Clear Generated Objects")]
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
                targetTerrain = terrainGO.AddComponent<Terrain>();
                TerrainCollider tc = terrainGO.AddComponent<TerrainCollider>();
                targetTerrain.terrainData = new TerrainData();
                tc.terrainData = targetTerrain.terrainData;
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
            float totalLengthZ = climbDistanceZ + cfg.summitSafeLength + cfg.boundaryMargin * 2f;
            float maxTerrainHeightY = cfg.targetElevationGain + cfg.boundaryCliffHeight + cfg.terrainMaxHeightHeadroom;

            int hmRes = Mathf.ClosestPowerOfTwo(Mathf.Clamp(cfg.heightmapResolution - 1, 64, 2048)) + 1;
            terrainData.heightmapResolution = hmRes;
            terrainData.size = new Vector3(totalWidthX, maxTerrainHeightY, totalLengthZ);

            // Position terrain so world (0, 0, 0) corresponds to the start of the Golden Path
            // X is centered around 0: [-totalWidthX/2, +totalWidthX/2]
            // Z starts at (pathStartXZ.y - boundaryMargin)
            terrainOrigin = new Vector3(
                -totalWidthX * 0.5f,
                0f,
                cfg.pathStartXZ.y - cfg.boundaryMargin);

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

            // Note: Unity Terrain SetHeights expects heights[zIndex, xIndex] in normalized [0..1]
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
                    // Convert [0..1] FBM to signed [-0.45..+0.55] with ridges
                    float ridgeNoise = (fbm - 0.42f) * cfg.mountainRoughnessAmplitude * startFlatFade * safeZoneNoiseDamp;

                    // Subtle natural valley cupping across X so the mountain feels like a pass
                    float lateralRatio = Mathf.Clamp01(Mathf.Abs(worldX) / Mathf.Max(1f, halfPlayableWidth));
                    float valleyCupRise = lateralRatio * lateralRatio * 4.5f * startFlatFade;

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

                    // Slight concave cradle in the very center of the trail
                    float centerTrench = 0f;
                    if (distToPath < cfg.pathInnerCarveRadius)
                    {
                        float centerFactor = 1f - (distToPath / cfg.pathInnerCarveRadius);
                        centerTrench = centerFactor * centerFactor * cfg.pathTrenchDepth;
                    }

                    float carvedHeight = Mathf.Lerp(rawMountainHeight, pathSpineElevation - centerTrench, carveMask);

                    // 4. Steep Bounded Valley / Cliff Walls around map edges (Left, Right, Back, Summit)
                    float cliffRise = ComputeBoundaryCliffHeight(
                        worldX,
                        worldZ,
                        halfPlayableWidth,
                        halfTotalWidth,
                        startZ,
                        summitEndZ,
                        cfg.boundaryMargin,
                        cfg.boundaryCliffHeight,
                        cfg.boundaryCliffSharpness,
                        fbm);

                    float finalWorldHeight = Mathf.Max(0f, carvedHeight + cliffRise);
                    heights[zIdx, xIdx] = Mathf.Clamp01(finalWorldHeight / maxH);
                }
            }

            terrainData.SetHeights(0, 0, heights);
        }

        private float ComputeBoundaryCliffHeight(
            float worldX,
            float worldZ,
            float halfPlayableWidth,
            float halfTotalWidth,
            float startZ,
            float summitEndZ,
            float margin,
            float cliffHeight,
            float sharpness,
            float fbmValue)
        {
            // Left / Right valley walls
            float absX = Mathf.Abs(worldX);
            float lateralEdgeT = 0f;
            if (absX > halfPlayableWidth)
            {
                lateralEdgeT = Mathf.Clamp01((absX - halfPlayableWidth) / Mathf.Max(1f, halfTotalWidth - halfPlayableWidth));
            }

            // Back wall behind Z = startZ
            float backEdgeT = 0f;
            if (worldZ < startZ)
            {
                backEdgeT = Mathf.Clamp01((startZ - worldZ) / Mathf.Max(1f, margin));
            }

            // Summit wall beyond Z = summitEndZ
            float frontEdgeT = 0f;
            if (worldZ > summitEndZ)
            {
                frontEdgeT = Mathf.Clamp01((worldZ - summitEndZ) / Mathf.Max(1f, margin));
            }

            float combinedEdgeT = Mathf.Max(lateralEdgeT, Mathf.Max(backEdgeT, frontEdgeT));
            if (combinedEdgeT <= 0.0001f) return 0f;

            float curved = Mathf.Pow(combinedEdgeT, sharpness);
            float cragDetail = (0.75f + 0.5f * fbmValue);
            return curved * cliffHeight * cragDetail;
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
        /// </summary>
        private void ApplyTerrainTextures(MountainGenerationProfile cfg, TerrainData terrainData)
        {
            List<TerrainLayer> layers = new List<TerrainLayer>();

            TerrainLayer grassLayer = cfg.grassTerrainLayer;
            if (grassLayer == null)
            {
                // Keep existing first layer if already assigned on TerrainData, or create a fallback Grass layer
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

            int rockLayerIndex = -1;
            if (cfg.rockTerrainLayer != null)
            {
                rockLayerIndex = layers.Count;
                layers.Add(cfg.rockTerrainLayer);
            }

            int pathLayerIndex = -1;
            if (cfg.pathTerrainLayer != null)
            {
                pathLayerIndex = layers.Count;
                layers.Add(cfg.pathTerrainLayer);
            }

            terrainData.terrainLayers = layers.ToArray();

            int alphaRes = Mathf.ClosestPowerOfTwo(Mathf.Clamp(cfg.alphamapResolution, 64, 1024));
            terrainData.alphamapResolution = alphaRes;

            int numLayers = layers.Count;
            float[,,] alphamaps = new float[alphaRes, alphaRes, numLayers];
            Vector3 terrainOrigin = targetTerrain.transform.position;
            Vector3 terrainSize = terrainData.size;

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

                    float slopeDeg = terrainData.GetSteepness(normX, normZ);

                    float rockWeight = 0f;
                    if (rockLayerIndex != -1)
                    {
                        rockWeight = Mathf.SmoothStep(
                            0f,
                            1f,
                            Mathf.InverseLerp(cfg.rockSlopeStartDegrees, cfg.rockSlopeFullDegrees, slopeDeg));
                    }

                    float pathWeight = 0f;
                    if (pathLayerIndex != -1)
                    {
                        float distToPath = pathGenerator.QueryClosestPointOnPath(new Vector2(worldX, worldZ), out _, out _, out _);
                        pathWeight = (1f - Mathf.SmoothStep(cfg.pathInnerCarveRadius * 0.6f, cfg.pathInnerCarveRadius * 1.6f, distToPath)) * (1f - rockWeight);
                    }

                    float grassWeight = Mathf.Max(0f, 1f - rockWeight - pathWeight);
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
            out Transform slopePropsParent,
            out Transform decorParent)
        {
            GameObject rootGO = new GameObject("Generated_Mountain_Objects");
            rootGO.transform.SetParent(transform, false);
            generatedRoot = rootGO.transform;

            GameObject anchorsGO = new GameObject("1_GoldenPath_GuaranteedAnchors");
            anchorsGO.transform.SetParent(generatedRoot, false);
            anchorsParent = anchorsGO.transform;

            GameObject slopeGO = new GameObject("2_PoissonSlope_Objects");
            slopeGO.transform.SetParent(generatedRoot, false);
            slopePropsParent = slopeGO.transform;

            GameObject decorGO = new GameObject("3_Decorative_SmallObjects");
            decorGO.transform.SetParent(generatedRoot, false);
            decorParent = decorGO.transform;
        }

        /// <summary>
        /// Stage 5: Guaranteed-Solvable Anchor Spawning.
        /// At each node along the Golden Path (every 8-12m from (0,0) to (0,250)),
        /// spawns at least one sturdy Oak or Boulder so the mountain is 100% completable.
        /// </summary>
        private int SpawnGoldenPathAnchors(
            MountainGenerationProfile cfg,
            Transform parent,
            int stageSeed)
        {
            if (!HasValidEntry(cfg.goldenPathAnchors))
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

                // Alternate primary side or pick randomly so anchors flank both sides of the climb
                int primarySide = (rng.NextDouble() < 0.5) ? 1 : -1;
                int countForThisNode = cfg.anchorsPerPathNode;
                if (rng.NextDouble() < cfg.bonusSecondAnchorChance)
                {
                    countForThisNode++;
                }

                for (int a = 0; a < countForThisNode; a++)
                {
                    SpawnablePrefabEntry chosenEntry = SelectWeightedEntry(cfg.goldenPathAnchors, rng);
                    if (chosenEntry == null || chosenEntry.prefab == null) continue;

                    int side = (a % 2 == 0) ? primarySide : -primarySide;
                    float lateralDist = Mathf.Lerp(minLateral, maxLateral, (float)rng.NextDouble());
                    float forwardJitter = ((float)rng.NextDouble() * 2f - 1f) * 1.75f;

                    Vector2 spawnXZ = node.positionXZ + node.rightXZ * (side * lateralDist) + node.forwardXZ * forwardJitter;

                    if (!occupancyGrid.IsFree(spawnXZ, chosenEntry.footprintRadius * 0.8f))
                        continue;

                    if (TrySpawnEntryAtXZ(chosenEntry, spawnXZ, parent, rng, cfg.anchorableLayerName, forceAnchorable: true))
                    {
                        occupancyGrid.Register(spawnXZ, chosenEntry.footprintRadius);
                        spawnedCount++;
                    }
                }
            }

            return spawnedCount;
        }

        /// <summary>
        /// Stage 6A: Runs Poisson Disk Sampling across the remaining terrain and classifies each sample by slope:
        /// - Slope < 20°: Leaf piles (low friction triggers) and deadfall logs (+ optional lowland trees)
        /// - Slope 20° - 38°: Birch and Oak trees (high abundance)
        /// - Slope > 38°: Jagged Boulders and loose scree zones (high rollback hazard)
        /// </summary>
        private int SpawnSlopeStratifiedObjects(
            MountainGenerationProfile cfg,
            Transform parent,
            int stageSeed)
        {
            System.Random rng = new System.Random(stageSeed);

            float halfWidth = cfg.playableWidth * 0.5f + cfg.boundaryMargin * 0.35f;
            float zMin = cfg.pathStartXZ.y - cfg.boundaryMargin * 0.25f;
            float zMax = cfg.pathEndXZ.y + cfg.summitSafeLength + cfg.boundaryMargin * 0.25f;

            Rect sampleBounds = new Rect(-halfWidth, zMin, halfWidth * 2f, zMax - zMin);
            List<Vector2> candidates = PoissonDiskSampler.GeneratePoints(sampleBounds, cfg.primaryPoissonRadius, rng);

            int spawnedCount = 0;

            for (int i = 0; i < candidates.Count; i++)
            {
                Vector2 ptXZ = candidates[i];

                // Keep the very center of the Golden Path clear of random obstacles so the coffin isn't blocked
                float distToPath = pathGenerator.QueryClosestPointOnPath(ptXZ, out _, out _, out _);
                if (distToPath < cfg.goldenPathCenterClearance)
                    continue;

                if (!SampleTerrainDataAtWorldXZ(ptXZ, out _, out _, out float slopeDegrees))
                    continue;

                List<SpawnablePrefabEntry> pool;
                float spawnChance;

                if (slopeDegrees < cfg.lowSlopeMaxDegrees)
                {
                    // Slope < 20°: Leaf piles & deadfall logs
                    pool = cfg.lowSlopePool;
                    spawnChance = cfg.lowSlopeSpawnChance;
                }
                else if (slopeDegrees <= cfg.midSlopeMaxDegrees)
                {
                    // Slope 20° - 38°: Birch & Oak trees (abundant)
                    pool = cfg.midSlopePool;
                    spawnChance = cfg.midSlopeSpawnChance;
                }
                else
                {
                    // Slope > 38°: Jagged Boulders & loose scree zones
                    pool = cfg.highSlopePool;
                    spawnChance = cfg.highSlopeSpawnChance;
                }

                if ((float)rng.NextDouble() > spawnChance)
                    continue;

                SpawnablePrefabEntry entry = SelectWeightedEntry(pool, rng);
                if (entry == null || entry.prefab == null)
                    continue;

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
        /// Stage 6B: Secondary Poisson Disk Sampling pass for decorative small objects (small rocks, bushes, shrubs).
        /// </summary>
        private int SpawnDecorativeObjects(
            MountainGenerationProfile cfg,
            Transform parent,
            int stageSeed)
        {
            if (cfg.decorativeRules == null || cfg.decorativeRules.Count == 0)
                return 0;

            System.Random rng = new System.Random(stageSeed);

            float halfWidth = cfg.playableWidth * 0.5f + cfg.boundaryMargin * 0.25f;
            float zMin = cfg.pathStartXZ.y - 5f;
            float zMax = cfg.pathEndXZ.y + cfg.summitSafeLength + 5f;

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

                // Weighted selection among eligible decorative rules
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

        private bool TrySpawnEntryAtXZ(
            SpawnablePrefabEntry entry,
            Vector2 worldXZ,
            Transform parent,
            System.Random rng,
            string anchorableLayerName,
            bool forceAnchorable)
        {
            if (entry == null || entry.prefab == null)
                return false;

            if (!SampleTerrainDataAtWorldXZ(worldXZ, out float worldY, out Vector3 normal, out _))
                return false;

            Vector3 spawnPos = new Vector3(worldXZ.x, worldY + entry.verticalEmbedOffset, worldXZ.y);

            // Compute rotation blending between World Up and Terrain Surface Normal
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

            return true;
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
        }
    }
}
