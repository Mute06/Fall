using NUnit.Framework;
using UnityEngine;

namespace FirstPersonSystem.Editor.Tests
{
    [TestFixture]
    public class FootstepSystemTests
    {
        private GameObject playerObj;
        private GameObject terrainObj;
        private Terrain terrain;
        private TerrainData terrainData;
        private Footsteps footsteps;
        private CheckTerrainTexture checker;

        [SetUp]
        public void SetUp()
        {
            // 1. Create a test Terrain with 3 layers (Grass, Rock, Dirt)
            terrainData = new TerrainData
            {
                heightmapResolution = 65,
                size = new Vector3(100f, 20f, 100f),
                alphamapResolution = 64
            };

            TerrainLayer layerGrass = new TerrainLayer { name = "Grass" };
            TerrainLayer layerRock = new TerrainLayer { name = "Rock" };
            TerrainLayer layerDirt = new TerrainLayer { name = "Dirt" };
            terrainData.terrainLayers = new[] { layerGrass, layerRock, layerDirt };

            // Paint alphamaps: left half Grass (layer 0 = 1.0), right half Dirt (layer 2 = 1.0)
            int res = terrainData.alphamapResolution;
            float[,,] maps = new float[res, res, 3];
            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    if (x < res / 2)
                    {
                        maps[y, x, 0] = 0.85f; // Grass dominant
                        maps[y, x, 2] = 0.15f; // Blended dirt
                    }
                    else
                    {
                        maps[y, x, 0] = 0.1f;  // Blended grass
                        maps[y, x, 2] = 0.9f;  // Dirt dominant
                    }
                }
            }
            terrainData.SetAlphamaps(0, 0, maps);

            terrainObj = Terrain.CreateTerrainGameObject(terrainData);
            terrainObj.name = "TestTerrain";
            terrainObj.tag = "Terrain";
            terrainObj.transform.position = Vector3.zero;
            terrain = terrainObj.GetComponent<Terrain>();

            // 2. Create Player with Footsteps and CheckTerrainTexture
            playerObj = new GameObject("TestPlayer");
            playerObj.AddComponent<CharacterController>();
            playerObj.AddComponent<FirstPersonController>();
            checker = playerObj.AddComponent<CheckTerrainTexture>();
            footsteps = playerObj.AddComponent<Footsteps>();

            // 3. Configure GroundTypes
            GroundType grassGround = ScriptableObject.CreateInstance<GroundType>();
            grassGround.indexOfTerrainTexture = 0;
            grassGround.walkAudioClips = new[] { AudioClip.Create("GrassStep", 441, 1, 44100, false) };

            GroundType dirtGround = ScriptableObject.CreateInstance<GroundType>();
            dirtGround.indexOfTerrainTexture = 2;
            dirtGround.walkAudioClips = new[] { AudioClip.Create("DirtStep", 441, 1, 44100, false) };

            footsteps.groundTypes = new[] { grassGround, dirtGround };
            footsteps.defaultGroundType = grassGround;
        }

        [TearDown]
        public void TearDown()
        {
            if (playerObj != null) Object.DestroyImmediate(playerObj);
            if (terrainObj != null) Object.DestroyImmediate(terrainObj);
        }

        [Test]
        public void CheckTerrainTexture_SamplesDominantLayer_OnGrassArea()
        {
            // Position at (20, 0, 50) -> x < 50% -> Grass dominant (layer 0)
            Vector3 testPos = new Vector3(20f, 0f, 50f);
            int dominant = checker.GetDominantTextureIndex(testPos, terrain);

            Assert.AreEqual(0, dominant, "Grass (index 0) should be dominant on the left side of the terrain.");
            Assert.AreEqual(3, checker.textureValues.Length, "textureValues should be dynamically sized to 3 layers.");
            Assert.Greater(checker.textureValues[0], checker.textureValues[2]);
        }

        [Test]
        public void CheckTerrainTexture_SamplesDominantLayer_OnDirtArea()
        {
            // Position at (80, 0, 50) -> x > 50% -> Dirt dominant (layer 2)
            Vector3 testPos = new Vector3(80f, 0f, 50f);
            int dominant = checker.GetDominantTextureIndex(testPos, terrain);

            Assert.AreEqual(2, dominant, "Dirt (index 2) should be dominant on the right side of the terrain.");
            Assert.Greater(checker.textureValues[2], checker.textureValues[0]);
        }

        [Test]
        public void CheckTerrainTexture_ClampsOutOfBoundsCoordinates_SafelyWithoutException()
        {
            // Position far outside terrain boundaries: negative and large positive
            Assert.DoesNotThrow(() =>
            {
                checker.GetTerrainTexture(new Vector3(-500f, 0f, -500f), terrain);
                checker.GetTerrainTexture(new Vector3(9999f, 0f, 9999f), terrain);
            });

            Assert.IsNotNull(checker.textureValues);
            Assert.AreEqual(3, checker.textureValues.Length);
        }
    }
}
