using NUnit.Framework;
using UnityEngine;

namespace FirstPersonSystem.Editor.Tests
{
    [TestFixture]
    public class CoffinDistanceWarningSystemTests
    {
        private GameObject playerObj;
        private GameObject coffinObj;
        private FirstPersonController controller;
        private CoffinPullable coffin;
        private CoffinDistanceWarningSystem warningSystem;

        [SetUp]
        public void SetUp()
        {
            // Create Coffin Object with required Collider and Rigidbody
            coffinObj = new GameObject("TestCoffin");
            coffinObj.transform.position = Vector3.zero;
            coffinObj.AddComponent<BoxCollider>();
            coffin = coffinObj.AddComponent<CoffinPullable>();

            // Create Player Object with required CharacterController
            playerObj = new GameObject("TestPlayer");
            playerObj.transform.position = new Vector3(0f, 0f, 10f); // 10m away
            playerObj.AddComponent<CharacterController>();
            controller = playerObj.AddComponent<FirstPersonController>();
            warningSystem = playerObj.AddComponent<CoffinDistanceWarningSystem>();

            warningSystem.SetReferences(controller, coffin);
            warningSystem.SetDistances(30f, 42f);
        }

        [TearDown]
        public void TearDown()
        {
            if (playerObj != null) Object.DestroyImmediate(playerObj);
            if (coffinObj != null) Object.DestroyImmediate(coffinObj);
        }

        [Test]
        public void DistanceState_WhenPlayerWithinSafeZone_WarningAndRestraintAreInactive()
        {
            // Player at 10m (warning is 30m, max is 42m)
            playerObj.transform.position = new Vector3(0f, 0f, 10f);
            warningSystem.EvaluateDistanceState(0.016f);

            Assert.IsFalse(warningSystem.IsWarningActive, "Warning should be inactive when player is near coffin.");
            Assert.IsFalse(warningSystem.IsRestrained, "Player should not be restrained within safe distance.");
            Assert.AreEqual(10f, warningSystem.CurrentDistance, 0.05f);
        }

        [Test]
        public void DistanceState_WhenPlayerExceedsWarningDistance_WarningBecomesActive()
        {
            // Player at 34m (between warning 30m and max 42m)
            playerObj.transform.position = new Vector3(0f, 0f, 34f);
            warningSystem.EvaluateDistanceState(0.016f);

            Assert.IsTrue(warningSystem.IsWarningActive, "Warning should be active when player is >= 30m away.");
            Assert.IsFalse(warningSystem.IsRestrained, "Player should not be restrained until exceeding 42m.");
            Assert.AreEqual(34f, warningSystem.CurrentDistance, 0.05f);
        }

        [Test]
        public void DistanceState_WhenPlayerExceedsMaxDistance_PlayerIsRestrained()
        {
            // Player at 46m (beyond maxDistance 42m)
            playerObj.transform.position = new Vector3(0f, 0f, 46f);
            warningSystem.EvaluateDistanceState(0.016f);

            Assert.IsTrue(warningSystem.IsWarningActive, "Warning should be active when exceeding max distance.");
            Assert.IsTrue(warningSystem.IsRestrained, "Player must be restrained when exceeding 42m.");
            Assert.AreEqual(46f, warningSystem.CurrentDistance, 0.05f);
        }

        [Test]
        public void DistanceState_WhenPlayerReturnsToCoffin_RecoversFromRestraintAndWarning()
        {
            // Step 1: exceed boundary
            playerObj.transform.position = new Vector3(0f, 0f, 45f);
            warningSystem.EvaluateDistanceState(0.016f);
            Assert.IsTrue(warningSystem.IsRestrained);

            // Step 2: walk back towards coffin to 15m
            playerObj.transform.position = new Vector3(0f, 0f, 15f);
            warningSystem.EvaluateDistanceState(0.016f);

            Assert.IsFalse(warningSystem.IsRestrained, "Player restraint should release when walking back.");
            Assert.IsFalse(warningSystem.IsWarningActive, "Warning should clear when player returns close to coffin.");
        }
    }
}
