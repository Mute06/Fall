using NUnit.Framework;
using UnityEngine;

namespace FirstPersonSystem.Editor.Tests
{
    [TestFixture]
    public class WinchAudioSystemTests
    {
        private GameObject playerObj;
        private GameObject coffinObj;
        private WinchSystem winchSystem;
        private CoffinPullable coffin;
        private AudioClip testClip;

        [SetUp]
        public void SetUp()
        {
            // Coffin setup
            coffinObj = new GameObject("TestCoffin");
            coffinObj.AddComponent<BoxCollider>();
            coffin = coffinObj.AddComponent<CoffinPullable>();

            // Player setup
            playerObj = new GameObject("TestPlayer");
            playerObj.AddComponent<CharacterController>();
            playerObj.AddComponent<FirstPersonController>();
            winchSystem = playerObj.AddComponent<WinchSystem>();
            winchSystem.SetupPullAudio();

            // Create a simple dummy test clip
            testClip = AudioClip.Create("TestPullSound", 44100, 1, 44100, false);
            winchSystem.SetPullAudioClip(testClip);
        }

        [TearDown]
        public void TearDown()
        {
            if (playerObj != null) Object.DestroyImmediate(playerObj);
            if (coffinObj != null) Object.DestroyImmediate(coffinObj);
            if (testClip != null) Object.DestroyImmediate(testClip);
        }

        [Test]
        public void WinchAudio_WhenNotWinching_AudioIsNotPlaying()
        {
            winchSystem.SetTestWinchState(WinchSystem.WinchState.Idle, null, false, 0f);
            winchSystem.UpdatePullAudio(0.1f);

            Assert.IsNotNull(winchSystem.PullAudioSource, "Pull audio source should be initialized.");
            Assert.IsFalse(winchSystem.IsPullAudioPlaying, "Audio should not be playing when idle.");
        }

        [Test]
        public void WinchAudio_WhenHoldingLMBAndPullingCoffin_AudioPlaysAndLoops()
        {
            winchSystem.SetTestWinchState(WinchSystem.WinchState.WinchRigged, coffin, true, 500f);
            winchSystem.UpdatePullAudio(0.1f);

            Assert.IsTrue(winchSystem.IsPullAudioPlaying, "Audio system should be active while pulling coffin.");
            Assert.IsTrue(winchSystem.PullAudioSource.loop, "Audio source should loop while pulling coffin.");
            Assert.Greater(winchSystem.PullAudioSource.volume, 0f, "Audio volume should fade in above 0.");
        }

        [Test]
        public void WinchAudio_PitchAdjustsAccordingToForceApplied()
        {
            winchSystem.SetPitchRange(0.95f, 1.20f);

            // 1. Simulate minimal force pull
            winchSystem.SetTestWinchState(WinchSystem.WinchState.WinchRigged, coffin, true, 0f);
            for (int i = 0; i < 20; i++)
            {
                winchSystem.UpdatePullAudio(0.1f);
            }
            float pitchAtMinForce = winchSystem.PullAudioSource.pitch;

            // 2. Simulate maximum force pull (e.g. 3500N)
            winchSystem.SetTestWinchState(WinchSystem.WinchState.WinchRigged, coffin, true, 3500f);
            for (int i = 0; i < 20; i++)
            {
                winchSystem.UpdatePullAudio(0.1f);
            }
            float pitchAtMaxForce = winchSystem.PullAudioSource.pitch;

            Assert.Greater(pitchAtMaxForce, pitchAtMinForce, "Audio pitch should increase under higher pulling force.");
            Assert.AreEqual(0.95f, pitchAtMinForce, 0.05f, "Pitch at low force should be near minPitch.");
            Assert.AreEqual(1.20f, pitchAtMaxForce, 0.05f, "Pitch at high force should be near maxPitch.");
        }

        [Test]
        public void WinchAudio_WhenPlayerReleasesLMB_AudioFadesAndStops()
        {
            // Start playing
            winchSystem.SetTestWinchState(WinchSystem.WinchState.WinchRigged, coffin, true, 1000f);
            for (int i = 0; i < 10; i++)
            {
                winchSystem.UpdatePullAudio(0.1f);
            }
            Assert.IsTrue(winchSystem.IsPullAudioPlaying);

            // Release LMB
            winchSystem.SetTestWinchState(WinchSystem.WinchState.WinchRigged, coffin, false, 0f);
            for (int i = 0; i < 30; i++)
            {
                winchSystem.UpdatePullAudio(0.1f);
            }

            Assert.IsFalse(winchSystem.IsPullAudioPlaying, "Audio should stop playing after fading out when LMB is released.");
            Assert.AreEqual(0f, winchSystem.PullAudioSource.volume, 0.005f);
        }

        [Test]
        public void WinchAudio_WhenRopeCanceled_AudioStopsImmediately()
        {
            winchSystem.SetTestWinchState(WinchSystem.WinchState.WinchRigged, coffin, true, 1000f);
            winchSystem.UpdatePullAudio(0.1f);
            Assert.IsTrue(winchSystem.IsPullAudioPlaying);

            winchSystem.CancelRope();

            Assert.IsFalse(winchSystem.IsPullAudioPlaying, "Audio should immediately stop on rope cancel.");
            Assert.AreEqual(0f, winchSystem.PullAudioSource.volume);
        }
    }
}
