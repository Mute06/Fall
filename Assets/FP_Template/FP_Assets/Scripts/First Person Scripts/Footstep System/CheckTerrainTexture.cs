using System.Collections;
using System.Collections.Generic;
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
            terrainObject = Terrain.activeTerrain;
            playerTransform = transform;
        }

        public void GetTerrainTexture()
        {
            UpdatePosition();
            CheckTexture();
        }

        void UpdatePosition()
        {
            Vector3 terrainPos = playerTransform.position - terrainObject.transform.position;
            Vector3 mapPosition = new Vector3(terrainPos.x / terrainObject.terrainData.size.x, 0f, terrainPos.z / terrainObject.terrainData.size.z);
            float xCoord = mapPosition.x * terrainObject.terrainData.alphamapWidth;
            float zCoord = mapPosition.z * terrainObject.terrainData.alphamapHeight;

            posX = (int)xCoord;
            posZ = (int)zCoord;
        }

        void CheckTexture()
        {
            float[,,] splatMap = terrainObject.terrainData.GetAlphamaps(posX, posZ, 1, 1);

            for (int i = 0; i < textureValues.Length; i++)
            {
                textureValues[i] = splatMap[0, 0, i];
            }
        }
    }
}