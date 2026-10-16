using System;
using System.Collections.Generic;
using UnityEngine;

namespace FirstPersonSystem.ProceduralGeneration
{
    [Serializable]
    public class SpawnablePrefabEntry
    {
        [Tooltip("Descriptive label (e.g., 'Oak Tree', 'Birch Tree', 'Crag Boulder', 'Leaf Pile').")]
        public string label = "Spawn Entry";

        [Tooltip("Prefab to instantiate. Can be any asset.")]
        public GameObject prefab;

        [Tooltip("Relative weight when choosing from this pool. Higher = more abundant.")]
        [Range(0.01f, 100f)]
        public float weight = 10f;

        [Tooltip("Uniform or non-uniform scale range (min, max).")]
        public Vector2 scaleRange = new Vector2(0.85f, 1.25f);

        [Tooltip("0 = strictly upright (World Up, good for trees), 1 = aligned to terrain surface normal (good for rocks/logs/leaf piles).")]
        [Range(0f, 1f)]
        public float alignToNormal = 0.08f;

        [Tooltip("Maximum random tilt angle in degrees added for organic variation.")]
        [Range(0f, 25f)]
        public float randomTiltDegrees = 3.5f;

        [Tooltip("Minimum clearance radius (meters) around this object where other objects won't overlap.")]
        [Min(0.1f)]
        public float footprintRadius = 1.5f;

        [Tooltip("Vertical offset (meters) along world Y to sink roots/rock bases into the slope so they never float.")]
        public float verticalEmbedOffset = -0.15f;

        [Tooltip("If true, automatically assigns this object and its children with colliders to the Anchorable layer for the WinchSystem.")]
        public bool isAnchorable = true;
    }

    [Serializable]
    public class DecorativeScatterRule
    {
        public string label = "Small Props (Rocks / Bushes)";
        public bool enabled = true;
        public SpawnablePrefabEntry prefabEntry = new SpawnablePrefabEntry
        {
            label = "Small Bush / Rock",
            weight = 10f,
            scaleRange = new Vector2(0.75f, 1.35f),
            alignToNormal = 0.4f,
            randomTiltDegrees = 8f,
            footprintRadius = 0.6f,
            verticalEmbedOffset = -0.05f,
            isAnchorable = false
        };

        [Tooltip("Allowed slope angle range (in degrees) for this decorative prop.")]
        public Vector2 allowedSlopeRange = new Vector2(0f, 42f);

        [Tooltip("Allowed normalized elevation progress (0 = start, 1 = summit).")]
        public Vector2 allowedProgressRange = new Vector2(0f, 1f);

        [Tooltip("Can this decoration spawn directly on the Golden Path trail?")]
        public bool allowOnGoldenPath = false;
    }

    [CreateAssetMenu(fileName = "MountainGenerationProfile", menuName = "Procedural Mountain/Mountain Generation Profile", order = 1)]
    public class MountainGenerationProfile : ScriptableObject
    {
        [Header("1. Map Dimensions & Elevation")]
        [Tooltip("Playable width (X axis, centered around X=0). E.g., 120m means playable X is [-60, +60].")]
        [Min(40f)]
        public float playableWidth = 120f;

        [Tooltip("Extra border terrain margin on left/right/back/front beyond playable boundaries.")]
        [Min(15f)]
        public float boundaryMargin = 35f;

        [Tooltip("Start and End Z coordinates of the Golden Path climb (default: 0 to 250m).")]
        public Vector2 pathStartXZ = new Vector2(0f, 0f);
        public Vector2 pathEndXZ = new Vector2(0f, 250f);

        [Tooltip("Total elevation gain from Z=0 to Z=250 (default: 85m).")]
        [Min(10f)]
        public float targetElevationGain = 85f;

        [Tooltip("Extra height allocated in TerrainData.size.y.")]
        [Min(20f)]
        public float terrainMaxHeightHeadroom = 50f;

        [Tooltip("Heightmap resolution (must be power of 2 plus 1, e.g., 257, 513, 1025).")]
        public int heightmapResolution = 513;

        [Tooltip("Alphamap (splatmap) resolution for terrain textures.")]
        public int alphamapResolution = 512;

        [Header("2. Stepped Climb Profile (Flat Start + Progressive Slopes + Safe Areas)")]
        [Tooltip("Length (meters) of the initial flat starting area before the first slope.")]
        [Min(8f)]
        public float startingFlatLength = 24f;

        [Tooltip("Length (meters) of the flat safe area at the summit after z = 250m.")]
        [Min(10f)]
        public float summitSafeLength = 22f;

        [Tooltip("Number of progressive slope-then-safe-area climb tiers between start and summit.")]
        [Range(2, 10)]
        public int climbTierCount = 5;

        [Tooltip("Fraction of each climb tier dedicated to the flat safe plateau (e.g., 0.28 = 28% flat safe zone, 72% uphill slope).")]
        [Range(0.1f, 0.5f)]
        public float safeAreaRatioPerTier = 0.28f;

        [Tooltip("Controls how much steeper each consecutive tier gets (1.0 = equal elevation per tier, > 1.0 = gentle early slopes, progressively steeper later slopes).")]
        [Range(1.0f, 2.5f)]
        public float slopeProgressionExponent = 1.45f;

        [Tooltip("Gentle slope factor in flat/safe zones (0 = dead flat, 0.05 = slight 2-3 degree incline).")]
        [Range(0f, 0.1f)]
        public float safeZoneSlopeFactor = 0.03f;

        [Header("3. Golden Path (Random Walk with Momentum)")]
        [Tooltip("Step distance range (meters) between Golden Path anchor nodes (default: 8m to 12m).")]
        public Vector2 goldenPathStepRange = new Vector2(8f, 12f);

        [Tooltip("Momentum retention factor [0..1]. Higher keeps smooth winding curves before changing direction.")]
        [Range(0.1f, 0.95f)]
        public float pathMomentum = 0.65f;

        [Tooltip("Maximum steering angle delta per step (in degrees).")]
        [Range(5f, 65f)]
        public float maxStepTurnDegrees = 28f;

        [Tooltip("Maximum angle deviation from +Z uphill direction (in degrees) so the path always progresses uphill.")]
        [Range(15f, 75f)]
        public float maxHeadingAngleDegrees = 48f;

        [Tooltip("Inner radius (meters) of the carved Golden Path trail where roughness is flattened.")]
        [Min(1.5f)]
        public float pathInnerCarveRadius = 3.5f;

        [Tooltip("Outer blend radius (meters) where the carved trail blends into the rugged mountain slope.")]
        [Min(3f)]
        public float pathOuterBlendRadius = 10f;

        [Tooltip("How strongly the Golden Path flattens/carves into lateral mountain noise [0..1].")]
        [Range(0f, 1f)]
        public float pathCarveStrength = 0.85f;

        [Tooltip("Slight trench depth (meters) in the center of the path to help cradle the coffin.")]
        [Range(0f, 1.5f)]
        public float pathTrenchDepth = 0.25f;

        [Header("4. Mountain Noise & Natural Edges")]
        [Tooltip("Primary low-frequency mountain ridge noise scale.")]
        [Min(0.001f)]
        public float primaryNoiseScale = 0.018f;

        [Tooltip("Maximum vertical amplitude (meters) of mountain roughness off the path.")]
        [Min(0f)]
        public float mountainRoughnessAmplitude = 11f;

        [Tooltip("Number of FBM Perlin octaves for terrain surface detail.")]
        [Range(1, 6)]
        public int noiseOctaves = 4;

        [Tooltip("Keep edges normal and natural (false) or raise artificial boundary cliffs (true).")]
        public bool enableBoundaryCliffs = false;

        [Tooltip("Height of boundary cliffs if enabled.")]
        public float boundaryCliffHeight = 32f;

        [Header("5. Invisible Boundary Colliders")]
        [Tooltip("Automatically spawns 4 invisible BoxColliders at North, South, East, and West boundaries of the playable area.")]
        public bool spawnInvisibleBoundaries = true;

        [Tooltip("Height (meters) of the invisible boundary walls.")]
        [Min(10f)]
        public float boundaryWallHeight = 45f;

        [Tooltip("Thickness (meters) of the invisible boundary walls.")]
        [Min(0.5f)]
        public float boundaryWallThickness = 3f;

        [Header("6. Terrain Ground Textures (Grass Default)")]
        [Tooltip("Primary ground TerrainLayer (Grass). Used across walkable terrain.")]
        public TerrainLayer grassTerrainLayer;

        [Tooltip("Optional steep slope TerrainLayer (Rock/Cliff) blended when slope > rockSlopeThreshold.")]
        public TerrainLayer rockTerrainLayer;

        [Tooltip("Optional trail/safe-zone TerrainLayer (Dirt/Path) blended along the Golden Path.")]
        public TerrainLayer pathTerrainLayer;

        [Tooltip("Slope angle (degrees) where rock texture starts blending in.")]
        [Range(20f, 50f)]
        public float rockSlopeStartDegrees = 28f;

        [Tooltip("Slope angle (degrees) where rock texture reaches 100% weight.")]
        [Range(25f, 65f)]
        public float rockSlopeFullDegrees = 40f;

        [Header("7. Core Asset Pools: Trees & Rocks")]
        [Tooltip("Layer name assigned to anchorable objects (trees/boulders) so WinchSystem can hook them.")]
        public string anchorableLayerName = "Anchorable";

        [Tooltip("Pool of multiple tree prefabs (Oak, Birch, Pine, etc.). Golden Path anchors and the Dense Forest randomly pick from this list.")]
        public List<SpawnablePrefabEntry> treePrefabs = new List<SpawnablePrefabEntry>
        {
            new SpawnablePrefabEntry
            {
                label = "Oak Tree",
                weight = 50f,
                scaleRange = new Vector2(0.85f, 1.3f),
                alignToNormal = 0.05f,
                randomTiltDegrees = 3.5f,
                footprintRadius = 1.6f,
                verticalEmbedOffset = -0.15f,
                isAnchorable = true
            },
            new SpawnablePrefabEntry
            {
                label = "Birch Tree",
                weight = 50f,
                scaleRange = new Vector2(0.85f, 1.3f),
                alignToNormal = 0.05f,
                randomTiltDegrees = 3.5f,
                footprintRadius = 1.5f,
                verticalEmbedOffset = -0.15f,
                isAnchorable = true
            }
        };

        [Tooltip("Pool of multiple rock and boulder prefabs. Used for Golden Path anchors and slope boulders.")]
        public List<SpawnablePrefabEntry> rockPrefabs = new List<SpawnablePrefabEntry>
        {
            new SpawnablePrefabEntry
            {
                label = "Boulder",
                weight = 40f,
                scaleRange = new Vector2(0.85f, 1.35f),
                alignToNormal = 0.65f,
                randomTiltDegrees = 12f,
                footprintRadius = 2.0f,
                verticalEmbedOffset = -0.25f,
                isAnchorable = true
            },
            new SpawnablePrefabEntry
            {
                label = "Jagged Rock",
                weight = 30f,
                scaleRange = new Vector2(0.85f, 1.4f),
                alignToNormal = 0.75f,
                randomTiltDegrees = 15f,
                footprintRadius = 1.8f,
                verticalEmbedOffset = -0.2f,
                isAnchorable = true
            }
        };

        [Header("8. Golden Path Guaranteed Anchors")]
        [Tooltip("Probability [0..1] that an anchor along the Golden Path picks from treePrefabs rather than rockPrefabs (e.g. 0.75 = 75% Tree, 25% Rock).")]
        [Range(0f, 1f)]
        public float goldenPathTreeRatio = 0.75f;

        [Tooltip("Number of guaranteed anchors spawned per Golden Path node (8-12m apart).")]
        [Range(1, 3)]
        public int anchorsPerPathNode = 1;

        [Tooltip("Chance [0..1] to spawn a second bonus anchor on the opposite side of the path node.")]
        [Range(0f, 1f)]
        public float bonusSecondAnchorChance = 0.35f;

        [Tooltip("Lateral distance range (meters) from Golden Path center to place anchors (keeps center clear for coffin).")]
        public Vector2 anchorLateralOffsetRange = new Vector2(2.8f, 6.2f);

        [Header("9. Dense Forest of Trees")]
        [Tooltip("Enable spawning a dense forest covering the mountain terrain.")]
        public bool enableDenseForest = true;

        [Tooltip("Poisson Disk sampling radius (meters) between trees in the dense forest. Lower = denser forest.")]
        [Range(2.0f, 8.0f)]
        public float denseForestPoissonRadius = 3.6f;

        [Tooltip("Overall spawn probability [0..1] for candidate tree locations.")]
        [Range(0.2f, 1.0f)]
        public float denseForestSpawnChance = 0.88f;

        [Tooltip("Maximum terrain slope angle (degrees) where trees can grow. Cliffs steeper than this will remain rocky without trees.")]
        [Range(20f, 60f)]
        public float maxTreeSlopeDegrees = 38f;

        [Tooltip("Minimum clearance distance (meters) from the center of the Golden Path so dense trees do not obstruct the trail.")]
        [Range(1.5f, 6.0f)]
        public float goldenPathTreeClearance = 2.6f;

        [Header("10. Slope Hazards & Props")]
        [Tooltip("Slope angle threshold (degrees) below which low-slope hazards (leaf piles, deadfall logs) spawn.")]
        [Range(10f, 30f)]
        public float lowSlopeMaxDegrees = 20f;

        [Tooltip("Slope angle threshold (degrees) above which high-slope rocks & scree zones spawn.")]
        [Range(25f, 50f)]
        public float highSlopeMinDegrees = 38f;

        [Tooltip("Probability of spawning low slope hazards (leaf piles / deadfall logs) on flat/gentle areas.")]
        [Range(0f, 1f)]
        public float lowSlopeHazardChance = 0.45f;

        [Tooltip("Prefabs for low-slope hazards (Leaf Piles, Deadfall Logs).")]
        public List<SpawnablePrefabEntry> lowSlopeHazards = new List<SpawnablePrefabEntry>
        {
            new SpawnablePrefabEntry
            {
                label = "Leaf Pile (Low Friction Trigger)",
                weight = 50f,
                scaleRange = new Vector2(0.9f, 1.3f),
                alignToNormal = 0.95f,
                randomTiltDegrees = 2f,
                footprintRadius = 1.6f,
                verticalEmbedOffset = -0.02f,
                isAnchorable = false
            },
            new SpawnablePrefabEntry
            {
                label = "Deadfall Log",
                weight = 35f,
                scaleRange = new Vector2(0.85f, 1.25f),
                alignToNormal = 0.85f,
                randomTiltDegrees = 5f,
                footprintRadius = 1.8f,
                verticalEmbedOffset = -0.1f,
                isAnchorable = true
            }
        };

        [Tooltip("Probability of spawning jagged rocks and loose scree zones on steep slopes (> highSlopeMinDegrees).")]
        [Range(0f, 1f)]
        public float highSlopeHazardChance = 0.65f;

        [Tooltip("Prefabs for steep slope hazards (e.g. Loose Scree Zones). Note: steep slopes also spawn rocks from rockPrefabs.")]
        public List<SpawnablePrefabEntry> highSlopeHazards = new List<SpawnablePrefabEntry>
        {
            new SpawnablePrefabEntry
            {
                label = "Loose Scree Zone (Rollback Hazard)",
                weight = 40f,
                scaleRange = new Vector2(0.9f, 1.25f),
                alignToNormal = 0.95f,
                randomTiltDegrees = 3f,
                footprintRadius = 2.5f,
                verticalEmbedOffset = -0.05f,
                isAnchorable = false
            }
        };

        [Header("11. Decorative Small Objects (Small Rocks, Bushes, Ground Detail)")]
        [Tooltip("Poisson Disk sampling radius (meters) for small decorative objects.")]
        [Range(1.0f, 8f)]
        public float decorativePoissonRadius = 2.8f;

        [Tooltip("Overall probability [0..1] that a decorative sample point spawns a prop.")]
        [Range(0f, 1f)]
        public float decorativeSpawnChance = 0.65f;

        [Tooltip("Configurable list of decorative small objects (small rocks, bushes, ferns, pebbles).")]
        public List<DecorativeScatterRule> decorativeRules = new List<DecorativeScatterRule>
        {
            new DecorativeScatterRule
            {
                label = "Small Bush / Shrub",
                enabled = true,
                allowedSlopeRange = new Vector2(0f, 35f),
                allowedProgressRange = new Vector2(0f, 1f),
                allowOnGoldenPath = false,
                prefabEntry = new SpawnablePrefabEntry
                {
                    label = "Small Bush",
                    weight = 60f,
                    scaleRange = new Vector2(0.7f, 1.35f),
                    alignToNormal = 0.15f,
                    randomTiltDegrees = 6f,
                    footprintRadius = 0.65f,
                    verticalEmbedOffset = -0.05f,
                    isAnchorable = false
                }
            },
            new DecorativeScatterRule
            {
                label = "Small Decorative Rock",
                enabled = true,
                allowedSlopeRange = new Vector2(0f, 50f),
                allowedProgressRange = new Vector2(0f, 1f),
                allowOnGoldenPath = false,
                prefabEntry = new SpawnablePrefabEntry
                {
                    label = "Small Rock",
                    weight = 45f,
                    scaleRange = new Vector2(0.6f, 1.4f),
                    alignToNormal = 0.85f,
                    randomTiltDegrees = 15f,
                    footprintRadius = 0.55f,
                    verticalEmbedOffset = -0.08f,
                    isAnchorable = false
                }
            }
        };
    }
}
