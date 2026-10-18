using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using FirstPersonSystem.Narrative;

namespace FirstPersonSystem.Narrative.Editor.Tests
{
    [TestFixture]
    public class NarrativeSystemTests
    {
        private GameObject testHost;
        private NarrativeManager narrativeManager;
        private QuestManager questManager;
        private NarrativeManager prevNarrativeMgr;
        private QuestManager prevQuestMgr;

        [SetUp]
        public void SetUp()
        {
            prevNarrativeMgr = NarrativeManager.Instance;
            prevQuestMgr = QuestManager.Instance;

            testHost = new GameObject("NarrativeTestHost");
            narrativeManager = testHost.AddComponent<NarrativeManager>();
            questManager = testHost.AddComponent<QuestManager>();

            NarrativeManager.Instance = narrativeManager;
            QuestManager.Instance = questManager;
        }

        [TearDown]
        public void TearDown()
        {
            if (testHost != null)
            {
                Object.DestroyImmediate(testHost);
            }

            NarrativeManager.Instance = prevNarrativeMgr;
            QuestManager.Instance = prevQuestMgr;
        }

        [Test]
        public void NarrativeLine_AutoCalculatesReadingDuration_WhenClipIsNull()
        {
            var line = new NarrativeDialogueLine
            {
                speaker = "Elena's Letter",
                subtitleText = "If you are reading this, Thomas... then I am already gone, and the long silence has begun."
            };

            float duration = line.GetEffectiveDuration();
            Assert.GreaterOrEqual(duration, 3.2f);
            Assert.LessOrEqual(duration, 10f);
        }

        [Test]
        public void NarrativeLine_UsesManualDuration_WhenProvided()
        {
            var line = new NarrativeDialogueLine
            {
                speaker = "Elena's Letter",
                subtitleText = "Brief text",
                customDuration = 5.5f
            };

            float duration = line.GetEffectiveDuration();
            Assert.AreEqual(5.5f, duration, 0.001f);
        }

        [Test]
        public void NarrativeSequence_Clone_CreatesDeepCopy()
        {
            var original = new NarrativeSequence
            {
                sequenceId = "Original_Seq",
                title = "Original Title",
                triggerQuest = true,
                questTitle = "Original Quest",
                questObjective = "Original Objective",
                lines = new List<NarrativeDialogueLine>
                {
                    new NarrativeDialogueLine { speaker = "Elena", subtitleText = "Line 1" },
                    new NarrativeDialogueLine { speaker = "Elena", subtitleText = "Line 2" }
                }
            };

            var clone = original.Clone();
            Assert.AreEqual(original.sequenceId, clone.sequenceId);
            Assert.AreEqual(original.lines.Count, clone.lines.Count);
            Assert.AreNotSame(original.lines, clone.lines);
            Assert.AreNotSame(original.lines[0], clone.lines[0]);
            Assert.AreEqual("Line 1", clone.lines[0].subtitleText);
        }

        [Test]
        public void NarrativeManager_EnqueuesSequencesProperly()
        {
            var seq1 = new NarrativeSequence
            {
                sequenceId = "Seq1",
                lines = new List<NarrativeDialogueLine>
                {
                    new NarrativeDialogueLine { subtitleText = "Test line 1" }
                }
            };

            var seq2 = new NarrativeSequence
            {
                sequenceId = "Seq2",
                lines = new List<NarrativeDialogueLine>
                {
                    new NarrativeDialogueLine { subtitleText = "Test line 2" }
                }
            };

            narrativeManager.EnqueueSequence(seq1);
            narrativeManager.EnqueueSequence(seq2);

            // One is playing, one is in the queue
            Assert.IsTrue(narrativeManager.IsPlaying || narrativeManager.QueuedCount > 0);
        }

        [Test]
        public void QuestManager_ActivatesAndCompletesQuest()
        {
            Assert.AreEqual(QuestState.Inactive, questManager.CurrentState);

            bool activatedFired = false;
            questManager.OnQuestActivated += (title, obj) => activatedFired = true;

            questManager.ActivateQuest("Lay your wife to rest", "Carry coffin to summit marker.");
            Assert.AreEqual(QuestState.Active, questManager.CurrentState);
            Assert.AreEqual("Lay your wife to rest", questManager.ActiveTitle);
            Assert.IsTrue(activatedFired);

            bool completedFired = false;
            questManager.OnQuestCompleted += (title) => completedFired = true;

            questManager.CompleteQuest();
            Assert.AreEqual(QuestState.Completed, questManager.CurrentState);
            Assert.IsTrue(completedFired);
        }

        [Test]
        public void SummitRestingMarker_ConfiguresTriggerAreaAndCompletesQuest()
        {
            var markerObj = new GameObject("TestMarker");
            var marker = markerObj.AddComponent<SummitRestingMarker>();

            marker.ConfigureGraveTrigger(new Vector2(2.6f, 4.2f), 1.6f);
            var col = markerObj.GetComponent<BoxCollider>();
            Assert.IsNotNull(col);
            Assert.IsTrue(col.isTrigger);
            Assert.GreaterOrEqual(col.size.x, 2.6f);
            Assert.GreaterOrEqual(col.size.z, 4.2f);

            questManager.ActivateQuest("Lay your wife to rest", "Drag coffin into hole");
            marker.CompleteResting();

            Assert.IsTrue(marker.IsCompleted);
            Assert.AreEqual(QuestState.Completed, questManager.CurrentState);

            Object.DestroyImmediate(markerObj);
        }

        [Test]
        public void NarrativeTriggerManager_PopulatesAllSevenMilestonesWithClipsAndText()
        {
            var triggerHost = new GameObject("TriggerHost");
            var triggerMgr = triggerHost.AddComponent<NarrativeTriggerManager>();
            triggerMgr.InitializeDefaultNarrativesIfEmpty();
            triggerMgr.LoadEditorClipsIfMissing();

            Assert.IsNotNull(triggerMgr.Milestone1Sequence);
            Assert.IsNotNull(triggerMgr.Milestone2Sequence);
            Assert.IsNotNull(triggerMgr.Milestone3Sequence);
            Assert.IsNotNull(triggerMgr.Milestone4Sequence);
            Assert.IsNotNull(triggerMgr.Milestone5Sequence);
            Assert.IsNotNull(triggerMgr.Milestone6Sequence);
            Assert.IsNotNull(triggerMgr.Milestone7Sequence);

            Assert.IsTrue(triggerMgr.Milestone1Sequence.lines[0].subtitleText.Contains("My love, if you are reading this"));
            Assert.IsTrue(triggerMgr.Milestone2Sequence.lines[0].subtitleText.Contains("We didn't get a lifetime of years together"));
            Assert.IsTrue(triggerMgr.Milestone3Sequence.lines[0].subtitleText.Contains("I am so sorry I didn't tell you what was coming"));
            Assert.IsTrue(triggerMgr.Milestone4Sequence.lines[0].subtitleText.Contains("I couldn’t bear the thought of watching our final days turn into mourning"));
            Assert.IsTrue(triggerMgr.Milestone5Sequence.lines[0].subtitleText.Contains("I pray you can forgive me for hiding it"));
            Assert.IsTrue(triggerMgr.Milestone6Sequence.lines[0].subtitleText.Contains("Look how high your love has carried us"));
            Assert.IsTrue(triggerMgr.Milestone7Sequence.lines[0].subtitleText.Contains("Now is the time for me to rest"));

            Assert.IsNotNull(triggerMgr.Milestone1Sequence.lines[0].voiceClip);
            Assert.IsNotNull(triggerMgr.Milestone2Sequence.lines[0].voiceClip);
            Assert.IsNotNull(triggerMgr.Milestone3Sequence.lines[0].voiceClip);
            Assert.IsNotNull(triggerMgr.Milestone4Sequence.lines[0].voiceClip);
            Assert.IsNotNull(triggerMgr.Milestone5Sequence.lines[0].voiceClip);
            Assert.IsNotNull(triggerMgr.Milestone6Sequence.lines[0].voiceClip);
            Assert.IsNotNull(triggerMgr.Milestone7Sequence.lines[0].voiceClip);

            Object.DestroyImmediate(triggerHost);
        }

        [Test]
        public void SummitMusicController_InitializesAndPlaysOnLoop()
        {
            var musicHost = new GameObject("MusicHost");
            var musicController = musicHost.AddComponent<SummitMusicController>();
            musicController.LoadAudioClipIfMissing();
            musicController.EnsureAudioSource();

            musicController.PlaySummitMusic();

            Assert.IsTrue(musicController.HasTriggered);
            Assert.IsNotNull(musicController.MusicAudioSource);
            Assert.IsTrue(musicController.MusicAudioSource.loop);

            Object.DestroyImmediate(musicHost);
        }

        [Test]
        public void SummitMusicController_DucksVolumeWhenVoiceoverIsActive()
        {
            var musicHost = new GameObject("MusicHost");
            var musicController = musicHost.AddComponent<SummitMusicController>();
            musicController.LoadAudioClipIfMissing();
            musicController.EnsureAudioSource();
            musicController.SetVolumes(0.8f, 0.2f);
            musicController.PlaySummitMusic();

            // Simulate finished fade-in
            for (int i = 0; i < 20; i++)
            {
                musicController.EvaluateDucking(0.5f);
            }

            Assert.GreaterOrEqual(musicController.CurrentVolume, 0.7f);

            // Now enqueue a narrative sequence so NarrativeManager.IsPlaying = true
            narrativeManager.EnqueueSequence(new NarrativeSequence
            {
                sequenceId = "TestDuckSeq",
                lines = new List<NarrativeDialogueLine>
                {
                    new NarrativeDialogueLine { subtitleText = "Voiceover playing..." }
                }
            });

            // Evaluate ducking
            for (int i = 0; i < 20; i++)
            {
                musicController.EvaluateDucking(0.5f);
            }

            // Volume should have ducked to ~0.2f
            Assert.LessOrEqual(musicController.CurrentVolume, 0.25f);

            Object.DestroyImmediate(musicHost);
        }
    }
}
