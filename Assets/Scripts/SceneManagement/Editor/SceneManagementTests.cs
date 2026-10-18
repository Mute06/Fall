using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using FirstPersonSystem.SceneManagement;
using FirstPersonSystem.ProceduralGeneration;

namespace FirstPersonSystem.Tests
{
    public class SceneManagementTests
    {
        private GameObject testRoot;

        [SetUp]
        public void SetUp()
        {
            testRoot = new GameObject("TestRoot");
        }

        [TearDown]
        public void TearDown()
        {
            if (testRoot != null)
            {
                Object.DestroyImmediate(testRoot);
            }

            if (LoadingScreenManager.Instance != null)
            {
                Object.DestroyImmediate(LoadingScreenManager.Instance.gameObject);
            }
        }

        [Test]
        public void LoadingScreenManager_Show_CreatesBlackOverlayAndBlocksRaycasts()
        {
            LoadingScreenManager.Show();

            LoadingScreenManager instance = LoadingScreenManager.Instance;
            Assert.IsNotNull(instance, "LoadingScreenManager instance should exist after Show().");
            Assert.IsTrue(instance.IsShowing, "IsShowing should be true after Show().");

            Canvas canvas = instance.GetComponentInChildren<Canvas>(true);
            Assert.IsNotNull(canvas, "Canvas component should exist.");
            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode, "Canvas must be ScreenSpaceOverlay.");
            Assert.AreEqual(32767, canvas.sortingOrder, "Canvas sorting order should be topmost (32767).");

            CanvasGroup group = instance.GetComponentInChildren<CanvasGroup>(true);
            Assert.IsNotNull(group, "CanvasGroup component should exist.");
            Assert.AreEqual(1f, group.alpha, 0.01f, "CanvasGroup alpha should be 1.");
            Assert.IsTrue(group.blocksRaycasts, "CanvasGroup must block raycasts during loading.");

            Image img = instance.GetComponentInChildren<Image>(true);
            Assert.IsNotNull(img, "Image overlay must exist.");
            Assert.AreEqual(Color.black, img.color, "Overlay image must be solid black.");
        }

        [Test]
        public void LoadingScreenManager_HideInstant_DeactivatesOverlay()
        {
            LoadingScreenManager.Show();
            Assert.IsTrue(LoadingScreenManager.Instance.IsShowing);

            LoadingScreenManager.Hide(0f);
            Assert.IsFalse(LoadingScreenManager.Instance.IsShowing, "IsShowing should be false after immediate Hide(0).");
        }

        [Test]
        public void StartMenuController_InvokesLoadingScreenOnButtonClick()
        {
            GameObject btnGO = new GameObject("StartButtonTest", typeof(RectTransform), typeof(Button), typeof(StartMenuController));
            btnGO.transform.SetParent(testRoot.transform);

            Button btn = btnGO.GetComponent<Button>();
            StartMenuController controller = btnGO.GetComponent<StartMenuController>();

            Assert.IsNotNull(btn);
            Assert.IsNotNull(controller);

            // Clicking should disable button interactability and show loading screen
            controller.OnStartButtonClicked();

            Assert.IsFalse(btn.interactable, "Start button should be set non-interactable after click to prevent double clicks.");
            Assert.IsTrue(LoadingScreenManager.Instance.IsShowing, "Loading screen should be showing after Start button click.");
        }

        [Test]
        public void ProceduralMountainGenerator_HasRandomizeSeedProperty_AndEvents()
        {
            GameObject genGO = new GameObject("MountainGenTest", typeof(ProceduralMountainGenerator));
            genGO.transform.SetParent(testRoot.transform);

            ProceduralMountainGenerator generator = genGO.GetComponent<ProceduralMountainGenerator>();
            Assert.IsNotNull(generator);

            int initialSeed = generator.Seed;
            generator.Seed = 42;
            Assert.AreEqual(42, generator.Seed);

            bool startedFired = false;
            bool completedFired = false;

            ProceduralMountainGenerator.OnGenerationStarted += (gen) => startedFired = true;
            ProceduralMountainGenerator.OnGenerationCompleted += (gen) => completedFired = true;

            try
            {
                generator.GenerateWithRandomSeed();

                Assert.IsTrue(startedFired, "OnGenerationStarted should fire.");
                Assert.IsTrue(completedFired, "OnGenerationCompleted should fire.");
                Assert.IsTrue(generator.IsGenerationCompleted, "IsGenerationCompleted should be true.");
                Assert.IsFalse(generator.IsGenerating, "IsGenerating should be false after completion.");
            }
            finally
            {
                ProceduralMountainGenerator.OnGenerationStarted -= (gen) => startedFired = true;
                ProceduralMountainGenerator.OnGenerationCompleted -= (gen) => completedFired = true;
            }
        }
    }
}
