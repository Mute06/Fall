using UnityEngine;

namespace FirstPersonSystem
{
    public class CheckTerrainTexture : MonoBehaviour
    {
        private Transform playerTransform;
        private Terrain terrainObject;

        private int posX;
        private int posZ;

        public float[] textureValues;

        private void Start()
        {
            playerTransform = transform;
            EnsureTerrain();
        }

        private Terrain EnsureTerrain(Terrain preferred = null)
        {
            if (preferred != null)
            {
                terrainObject = preferred;
                return terrainObject;
            }

            if (terrainObject == null)
            {
                terrainObject = Terrain.activeTerrain;
                if (terrainObject == null)
                {
                    terrainObject = FindFirstObjectByType<Terrain>();
                }
            }

            return terrainObject;
        }

        public void GetTerrainTexture()
        {
            Vector3 queryPos = playerTransform != null ? playerTransform.position : transform.position;
            GetTerrainTexture(queryPos, null);
        }

        public void GetTerrainTexture(Vector3 worldPosition, Terrain targetTerrain = null)
        {
            Terrain terrain = EnsureTerrain(targetTerrain);
            if (terrain == null || terrain.terrainData == null) return;

            TerrainData tData = terrain.terrainData;
            Vector3 tPos = terrain.transform.position;
            Vector3 tSize = tData.size;

            if (tSize.x <= 0f || tSize.z <= 0f) return;

            // Normalized coordinates (0 to 1) across the terrain surface
            float normX = Mathf.Clamp01((worldPosition.x - tPos.x) / tSize.x);
            float normZ = Mathf.Clamp01((worldPosition.z - tPos.z) / tSize.z);

            // Alphamap resolution coordinates
            int alphaW = tData.alphamapWidth;
            int alphaH = tData.alphamapHeight;

            posX = Mathf.Clamp((int)(normX * alphaW), 0, alphaW - 1);
            posZ = Mathf.Clamp((int)(normZ * alphaH), 0, alphaH - 1);

            float[,,] splatMap = tData.GetAlphamaps(posX, posZ, 1, 1);
            int numLayers = splatMap.GetLength(2);

            if (textureValues == null || textureValues.Length != numLayers)
            {
                textureValues = new float[numLayers];
            }

            for (int i = 0; i < numLayers; i++)
            {
                textureValues[i] = splatMap[0, 0, i];
            }
        }

        /// <summary>
        /// Samples the terrain alphamap at the given position and returns the layer index with the highest weight.
        /// </summary>
        public int GetDominantTextureIndex(Vector3 worldPosition, Terrain targetTerrain = null)
        {
            GetTerrainTexture(worldPosition, targetTerrain);

            if (textureValues == null || textureValues.Length == 0)
                return 0;

            int dominantIndex = 0;
            float maxWeight = -1f;

            for (int i = 0; i < textureValues.Length; i++)
            {
                if (textureValues[i] > maxWeight)
                {
                    maxWeight = textureValues[i];
                    dominantIndex = i;
                }
            }

            return dominantIndex;
        }
    }
}