using System;
using System.Collections.Generic;
using UnityEngine;

namespace FirstPersonSystem.ProceduralGeneration
{
    [Serializable]
    public struct GoldenPathNode
    {
        public int index;
        public Vector2 positionXZ;
        public Vector2 forwardXZ;
        public Vector2 rightXZ;
        public float distanceAlongPath;
        public float normalizedProgress;
        public float elevation;
        public bool isInSafeZone;
    }

    [Serializable]
    public struct SafePlateauZone
    {
        public int tierIndex;
        public float startZ;
        public float endZ;
        public float baseElevation;
    }

    /// <summary>
    /// Generates the Golden Path using a Random Walk with Momentum from startXZ (0,0) to endXZ (0,250)
    /// stepping every 8-12 meters, and computes the stepped longitudinal elevation profile
    /// (flat start -> increasingly steeper slopes -> flat safe areas after each slope -> 85m summit).
    /// </summary>
    public class GoldenPathGenerator
    {
        private readonly List<GoldenPathNode> nodes = new List<GoldenPathNode>();
        private readonly List<SafePlateauZone> safeZones = new List<SafePlateauZone>();

        public IReadOnlyList<GoldenPathNode> Nodes => nodes;
        public IReadOnlyList<SafePlateauZone> SafeZones => safeZones;

        /// <summary>
        /// Generates the Golden Path nodes and safe zones deterministically from the given profile and seed.
        /// </summary>
        public void Generate(MountainGenerationProfile profile, int seed)
        {
            nodes.Clear();
            safeZones.Clear();

            System.Random rng = new System.Random(seed);

            Vector2 startXZ = profile.pathStartXZ;
            Vector2 endXZ = profile.pathEndXZ;
            float totalZSpan = Mathf.Max(10f, endXZ.y - startXZ.y);
            float halfPlayableWidth = profile.playableWidth * 0.5f;
            float safeLateralLimit = Mathf.Max(8f, halfPlayableWidth - profile.pathOuterBlendRadius - 4f);

            float minStep = Mathf.Max(2f, Mathf.Min(profile.goldenPathStepRange.x, profile.goldenPathStepRange.y));
            float maxStep = Mathf.Max(minStep + 0.5f, Mathf.Max(profile.goldenPathStepRange.x, profile.goldenPathStepRange.y));

            // Build Safe Plateau Zones metadata
            BuildSafeZones(profile, startXZ.y, endXZ.y);

            // State for Random Walk with Momentum
            Vector2 currentPos = startXZ;
            float headingRad = 0f; // 0 rad = +Z direction
            float angularMomentum = 0f;
            float maxTurnRad = profile.maxStepTurnDegrees * Mathf.Deg2Rad;
            float maxHeadingRad = profile.maxHeadingAngleDegrees * Mathf.Deg2Rad;
            float momentum = Mathf.Clamp(profile.pathMomentum, 0.05f, 0.95f);

            float accumulatedDist = 0f;
            int stepIndex = 0;

            // Add initial node at (0, 0)
            AddNode(stepIndex++, currentPos, new Vector2(0f, 1f), accumulatedDist, startXZ.y, endXZ.y, profile);

            int safetyMaxSteps = 500;
            while (currentPos.y < endXZ.y && stepIndex < safetyMaxSteps)
            {
                float stepLength = Mathf.Lerp(minStep, maxStep, (float)rng.NextDouble());
                float remainingZ = endXZ.y - currentPos.y;
                float distToEnd = Vector2.Distance(currentPos, endXZ);

                // If within one step of the end target, snap cleanly to endXZ (0, 250)
                if (remainingZ <= maxStep * 0.95f || distToEnd <= stepLength * 1.05f)
                {
                    Vector2 finalDir = (endXZ - currentPos).normalized;
                    if (finalDir.sqrMagnitude < 0.001f) finalDir = new Vector2(0f, 1f);
                    accumulatedDist += Vector2.Distance(currentPos, endXZ);
                    currentPos = endXZ;
                    AddNode(stepIndex++, currentPos, finalDir, accumulatedDist, startXZ.y, endXZ.y, profile);
                    break;
                }

                // 1. Random angular impulse
                float randomTurn = ((float)rng.NextDouble() * 2f - 1f) * maxTurnRad;

                // 2. Momentum integration
                angularMomentum = momentum * angularMomentum + (1f - momentum) * randomTurn;

                // 3. Lateral boundary restoring force (keeps path inside playable valley)
                float normalizedX = currentPos.x / safeLateralLimit;
                if (Mathf.Abs(normalizedX) > 0.55f)
                {
                    float pushBack = -Mathf.Sign(normalizedX) * Mathf.Pow((Mathf.Abs(normalizedX) - 0.55f) / 0.45f, 1.5f) * maxTurnRad * 1.35f;
                    angularMomentum += pushBack;
                }

                // 4. Homing steering toward endXZ (0, 250) during final 25% of climb
                float progressZ = Mathf.Clamp01((currentPos.y - startXZ.y) / totalZSpan);
                if (progressZ > 0.72f)
                {
                    Vector2 toTarget = (endXZ - currentPos).normalized;
                    float targetAngle = Mathf.Atan2(toTarget.x, Mathf.Max(0.1f, toTarget.y));
                    float homingWeight = Mathf.SmoothStep(0f, 0.85f, (progressZ - 0.72f) / 0.28f);
                    headingRad = Mathf.Lerp(headingRad + angularMomentum, targetAngle, homingWeight);
                    angularMomentum *= (1f - homingWeight * 0.5f);
                }
                else
                {
                    headingRad += angularMomentum;
                }

                // Clamp heading so the walk always makes forward (+Z) progress uphill
                headingRad = Mathf.Clamp(headingRad, -maxHeadingRad, maxHeadingRad);

                Vector2 stepDir = new Vector2(Mathf.Sin(headingRad), Mathf.Cos(headingRad)).normalized;
                Vector2 nextPos = currentPos + stepDir * stepLength;

                // Hard safety clamp on X
                nextPos.x = Mathf.Clamp(nextPos.x, -safeLateralLimit, safeLateralLimit);
                if (nextPos.y > endXZ.y)
                {
                    nextPos.y = endXZ.y;
                }

                Vector2 actualDir = (nextPos - currentPos).normalized;
                if (actualDir.sqrMagnitude < 0.001f) actualDir = new Vector2(0f, 1f);

                accumulatedDist += Vector2.Distance(currentPos, nextPos);
                currentPos = nextPos;

                AddNode(stepIndex++, currentPos, actualDir, accumulatedDist, startXZ.y, endXZ.y, profile);
            }

            // Smooth forward/right tangents across nodes
            for (int i = 0; i < nodes.Count; i++)
            {
                Vector2 tangent;
                if (nodes.Count == 1)
                {
                    tangent = new Vector2(0f, 1f);
                }
                else if (i == 0)
                {
                    tangent = (nodes[1].positionXZ - nodes[0].positionXZ).normalized;
                }
                else if (i == nodes.Count - 1)
                {
                    tangent = (nodes[i].positionXZ - nodes[i - 1].positionXZ).normalized;
                }
                else
                {
                    tangent = (nodes[i + 1].positionXZ - nodes[i - 1].positionXZ).normalized;
                }

                if (tangent.sqrMagnitude < 0.001f) tangent = new Vector2(0f, 1f);

                GoldenPathNode n = nodes[i];
                n.forwardXZ = tangent;
                n.rightXZ = new Vector2(tangent.y, -tangent.x);
                nodes[i] = n;
            }
        }

        private void AddNode(
            int index,
            Vector2 posXZ,
            Vector2 forwardXZ,
            float distAlongPath,
            float startZ,
            float endZ,
            MountainGenerationProfile profile)
        {
            float progress = Mathf.Clamp01((posXZ.y - startZ) / Mathf.Max(1f, endZ - startZ));
            float elev = EvaluateLongitudinalElevation(posXZ.y, profile, out bool isSafe);

            GoldenPathNode node = new GoldenPathNode
            {
                index = index,
                positionXZ = posXZ,
                forwardXZ = forwardXZ,
                rightXZ = new Vector2(forwardXZ.y, -forwardXZ.x),
                distanceAlongPath = distAlongPath,
                normalizedProgress = progress,
                elevation = elev,
                isInSafeZone = isSafe
            };
            nodes.Add(node);
        }

        private void BuildSafeZones(MountainGenerationProfile profile, float startZ, float endZ)
        {
            // Initial starting flat zone
            float flatEndZ = startZ + profile.startingFlatLength;
            safeZones.Add(new SafePlateauZone
            {
                tierIndex = 0,
                startZ = startZ,
                endZ = flatEndZ,
                baseElevation = EvaluateLongitudinalElevation(startZ + profile.startingFlatLength * 0.5f, profile, out _)
            });

            float climbSpanZ = Mathf.Max(10f, endZ - flatEndZ);
            int tiers = Mathf.Max(1, profile.climbTierCount);
            float tierLengthZ = climbSpanZ / tiers;
            float rampFraction = 1f - Mathf.Clamp(profile.safeAreaRatioPerTier, 0.1f, 0.5f);

            for (int k = 0; k < tiers; k++)
            {
                float tierStartZ = flatEndZ + k * tierLengthZ;
                float safeStartZ = tierStartZ + tierLengthZ * rampFraction;
                float safeEndZ = tierStartZ + tierLengthZ;
                float midSafeZ = (safeStartZ + safeEndZ) * 0.5f;

                safeZones.Add(new SafePlateauZone
                {
                    tierIndex = k + 1,
                    startZ = safeStartZ,
                    endZ = safeEndZ,
                    baseElevation = EvaluateLongitudinalElevation(midSafeZ, profile, out _)
                });
            }
        }

        /// <summary>
        /// Evaluates the stepped mountain elevation profile at coordinate Z:
        /// - Starts with a flat starting zone [startZ, startZ + startingFlatLength]
        /// - Divides the climb up to endZ (250m) into N tiers
        /// - Each tier has a smooth uphill ramp (with progressively steeper slope on higher tiers)
        ///   followed by a flat Safe Area plateau
        /// - Reaches exact targetElevationGain (85m) at endZ (250m).
        /// </summary>
        public static float EvaluateLongitudinalElevation(
            float worldZ,
            MountainGenerationProfile profile,
            out bool isSafeZone)
        {
            float startZ = profile.pathStartXZ.y;
            float endZ = profile.pathEndXZ.y;
            float targetGain = profile.targetElevationGain;
            float flatStartLen = Mathf.Clamp(profile.startingFlatLength, 5f, (endZ - startZ) * 0.35f);
            float climbStartZ = startZ + flatStartLen;

            // 1. Before or within initial starting flat area
            if (worldZ <= climbStartZ)
            {
                isSafeZone = true;
                float tFlat = Mathf.Clamp01((worldZ - startZ) / Mathf.Max(1f, flatStartLen));
                float gentleRise = tFlat * tFlat * (3f - 2f * tFlat) * (targetGain * 0.015f);
                return gentleRise;
            }

            float initialBaseHeight = targetGain * 0.015f;
            float remainingGain = targetGain - initialBaseHeight;

            // 2. Beyond summit Z (250m): flat summit safe plateau
            if (worldZ >= endZ)
            {
                isSafeZone = worldZ <= (endZ + profile.summitSafeLength);
                return targetGain;
            }

            // 3. Inside the progressive stepped climb [climbStartZ .. endZ]
            int tiers = Mathf.Max(1, profile.climbTierCount);
            float climbSpanZ = endZ - climbStartZ;
            float tierLengthZ = climbSpanZ / tiers;

            // Compute normalized elevation weights per tier so each tier gets progressively steeper
            float totalWeight = 0f;
            for (int i = 0; i < tiers; i++)
            {
                totalWeight += Mathf.Pow(i + 1, profile.slopeProgressionExponent);
            }

            float relativeZ = worldZ - climbStartZ;
            int currentTier = Mathf.Clamp(Mathf.FloorToInt(relativeZ / tierLengthZ), 0, tiers - 1);
            float localTierZ = relativeZ - currentTier * tierLengthZ;
            float localTierT = Mathf.Clamp01(localTierZ / tierLengthZ);

            // Sum elevation gained in all completed previous tiers
            float accumulatedTierWeight = 0f;
            for (int i = 0; i < currentTier; i++)
            {
                accumulatedTierWeight += Mathf.Pow(i + 1, profile.slopeProgressionExponent);
            }

            float currentTierWeight = Mathf.Pow(currentTier + 1, profile.slopeProgressionExponent);
            float tierElevationGain = (currentTierWeight / totalWeight) * remainingGain;
            float tierStartElevation = initialBaseHeight + (accumulatedTierWeight / totalWeight) * remainingGain;

            // Split current tier into [Ramp portion] -> [Flat Safe Plateau portion]
            float safeRatio = Mathf.Clamp(profile.safeAreaRatioPerTier, 0.1f, 0.5f);
            float rampRatio = 1f - safeRatio;
            float safeSlopeShare = Mathf.Clamp(profile.safeZoneSlopeFactor, 0f, 0.1f);
            float rampElevationShare = 1f - safeSlopeShare;

            float tierProgress;
            if (localTierT < rampRatio)
            {
                isSafeZone = false;
                float u = localTierT / rampRatio;
                // Smooth C1 Hermite curve with strong mid-ramp gradient and smooth entry/exit into safe areas
                float smoothRamp = u * u * (3f - 2f * u);
                tierProgress = smoothRamp * rampElevationShare;
            }
            else
            {
                isSafeZone = true;
                float v = (localTierT - rampRatio) / safeRatio;
                // Nearly flat safe zone plateau where coffin can rest safely
                tierProgress = rampElevationShare + v * safeSlopeShare;
            }

            return tierStartElevation + tierProgress * tierElevationGain;
        }

        /// <summary>
        /// Finds the shortest distance from a 2D point (x, z) to the Golden Path polyline,
        /// along with the interpolated path elevation and closest point on the path.
        /// </summary>
        public float QueryClosestPointOnPath(
            Vector2 pointXZ,
            out Vector2 closestPointXZ,
            out float interpolatedElevation,
            out float normalizedProgress)
        {
            closestPointXZ = pointXZ;
            interpolatedElevation = 0f;
            normalizedProgress = 0f;

            if (nodes.Count == 0)
            {
                return float.MaxValue;
            }

            if (nodes.Count == 1)
            {
                closestPointXZ = nodes[0].positionXZ;
                interpolatedElevation = nodes[0].elevation;
                normalizedProgress = nodes[0].normalizedProgress;
                return Vector2.Distance(pointXZ, closestPointXZ);
            }

            float minDistSq = float.MaxValue;

            for (int i = 0; i < nodes.Count - 1; i++)
            {
                Vector2 a = nodes[i].positionXZ;
                Vector2 b = nodes[i + 1].positionXZ;
                Vector2 ab = b - a;
                float abLenSq = ab.sqrMagnitude;

                float t = 0f;
                if (abLenSq > 0.0001f)
                {
                    t = Mathf.Clamp01(Vector2.Dot(pointXZ - a, ab) / abLenSq);
                }

                Vector2 proj = a + ab * t;
                float distSq = (pointXZ - proj).sqrMagnitude;

                if (distSq < minDistSq)
                {
                    minDistSq = distSq;
                    closestPointXZ = proj;
                    interpolatedElevation = Mathf.Lerp(nodes[i].elevation, nodes[i + 1].elevation, t);
                    normalizedProgress = Mathf.Lerp(nodes[i].normalizedProgress, nodes[i + 1].normalizedProgress, t);
                }
            }

            return Mathf.Sqrt(minDistSq);
        }
    }
}
