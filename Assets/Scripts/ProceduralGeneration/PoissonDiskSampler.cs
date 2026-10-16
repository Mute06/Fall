using System;
using System.Collections.Generic;
using UnityEngine;

namespace FirstPersonSystem.ProceduralGeneration
{
    /// <summary>
    /// 2D Spatial Hash Grid for fast radius-based footprint overlap checks
    /// between Golden Path anchors, slope-stratified objects, and decorative small props.
    /// </summary>
    public class SpatialOccupancyGrid
    {
        private struct OccupiedCircle
        {
            public Vector2 positionXZ;
            public float radius;
        }

        private readonly float cellSize;
        private readonly Dictionary<Vector2Int, List<OccupiedCircle>> cells = new Dictionary<Vector2Int, List<OccupiedCircle>>();
        private float maxRegisteredRadius = 0f;

        public SpatialOccupancyGrid(float cellSize = 4f)
        {
            this.cellSize = Mathf.Max(0.5f, cellSize);
        }

        public void Clear()
        {
            cells.Clear();
            maxRegisteredRadius = 0f;
        }

        private Vector2Int WorldToCell(Vector2 posXZ)
        {
            return new Vector2Int(
                Mathf.FloorToInt(posXZ.x / cellSize),
                Mathf.FloorToInt(posXZ.y / cellSize));
        }

        public void Register(Vector2 posXZ, float footprintRadius)
        {
            footprintRadius = Mathf.Max(0.05f, footprintRadius);
            if (footprintRadius > maxRegisteredRadius)
            {
                maxRegisteredRadius = footprintRadius;
            }

            Vector2Int cell = WorldToCell(posXZ);
            if (!cells.TryGetValue(cell, out List<OccupiedCircle> list))
            {
                list = new List<OccupiedCircle>(4);
                cells[cell] = list;
            }

            list.Add(new OccupiedCircle
            {
                positionXZ = posXZ,
                radius = footprintRadius
            });
        }

        /// <summary>
        /// Returns true if the candidate point with candidateRadius does NOT overlap any already-registered object.
        /// </summary>
        public bool IsFree(Vector2 candidateXZ, float candidateRadius)
        {
            float searchRange = candidateRadius + maxRegisteredRadius;
            int cellSpan = Mathf.CeilToInt(searchRange / cellSize);
            Vector2Int centerCell = WorldToCell(candidateXZ);

            for (int dx = -cellSpan; dx <= cellSpan; dx++)
            {
                for (int dz = -cellSpan; dz <= cellSpan; dz++)
                {
                    Vector2Int key = new Vector2Int(centerCell.x + dx, centerCell.y + dz);
                    if (!cells.TryGetValue(key, out List<OccupiedCircle> list)) continue;

                    for (int i = 0; i < list.Count; i++)
                    {
                        OccupiedCircle item = list[i];
                        float minDist = candidateRadius + item.radius;
                        if ((candidateXZ - item.positionXZ).sqrMagnitude < minDist * minDist)
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Bridson's Fast 2D Poisson Disk Sampling algorithm for organic blue-noise distribution across the terrain.
    /// </summary>
    public static class PoissonDiskSampler
    {
        public static List<Vector2> GeneratePoints(
            Rect boundsXZ,
            float minDistance,
            System.Random rng,
            int rejectionLimit = 25)
        {
            minDistance = Mathf.Max(0.75f, minDistance);
            float cellSize = minDistance / Mathf.Sqrt(2f);

            int gridWidth = Mathf.Max(1, Mathf.CeilToInt(boundsXZ.width / cellSize));
            int gridHeight = Mathf.Max(1, Mathf.CeilToInt(boundsXZ.height / cellSize));

            int[,] grid = new int[gridWidth, gridHeight];
            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    grid[x, y] = -1;
                }
            }

            List<Vector2> points = new List<Vector2>();
            List<Vector2> activeList = new List<Vector2>();

            // Seed initial point in the center of bounds
            Vector2 firstPoint = new Vector2(
                boundsXZ.xMin + boundsXZ.width * (0.35f + 0.3f * (float)rng.NextDouble()),
                boundsXZ.yMin + boundsXZ.height * (0.35f + 0.3f * (float)rng.NextDouble()));

            points.Add(firstPoint);
            activeList.Add(firstPoint);

            int startGX = Mathf.Clamp(Mathf.FloorToInt((firstPoint.x - boundsXZ.xMin) / cellSize), 0, gridWidth - 1);
            int startGY = Mathf.Clamp(Mathf.FloorToInt((firstPoint.y - boundsXZ.yMin) / cellSize), 0, gridHeight - 1);
            grid[startGX, startGY] = 0;

            float minDistSq = minDistance * minDistance;

            while (activeList.Count > 0)
            {
                int activeIndex = rng.Next(activeList.Count);
                Vector2 center = activeList[activeIndex];
                bool acceptedAny = false;

                for (int k = 0; k < rejectionLimit; k++)
                {
                    float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                    float radius = minDistance * (1f + (float)rng.NextDouble());
                    Vector2 candidate = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

                    if (!boundsXZ.Contains(candidate))
                        continue;

                    int gx = Mathf.FloorToInt((candidate.x - boundsXZ.xMin) / cellSize);
                    int gy = Mathf.FloorToInt((candidate.y - boundsXZ.yMin) / cellSize);

                    if (gx < 0 || gx >= gridWidth || gy < 0 || gy >= gridHeight)
                        continue;

                    bool tooClose = false;
                    int x0 = Mathf.Max(0, gx - 2);
                    int x1 = Mathf.Min(gridWidth - 1, gx + 2);
                    int y0 = Mathf.Max(0, gy - 2);
                    int y1 = Mathf.Min(gridHeight - 1, gy + 2);

                    for (int nx = x0; nx <= x1 && !tooClose; nx++)
                    {
                        for (int ny = y0; ny <= y1; ny++)
                        {
                            int ptIndex = grid[nx, ny];
                            if (ptIndex != -1)
                            {
                                if ((candidate - points[ptIndex]).sqrMagnitude < minDistSq)
                                {
                                    tooClose = true;
                                    break;
                                }
                            }
                        }
                    }

                    if (!tooClose)
                    {
                        int newIndex = points.Count;
                        points.Add(candidate);
                        activeList.Add(candidate);
                        grid[gx, gy] = newIndex;
                        acceptedAny = true;
                        break;
                    }
                }

                if (!acceptedAny)
                {
                    // Swap-remove from activeList
                    int lastIdx = activeList.Count - 1;
                    activeList[activeIndex] = activeList[lastIdx];
                    activeList.RemoveAt(lastIdx);
                }
            }

            return points;
        }
    }
}
