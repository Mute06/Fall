using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using FirstPersonSystem;

public static class AttachLeavesToScene
{
    public static string Run()
    {
        var logs = new System.Text.StringBuilder();

        // 1. Scene instance
        FallingLeavesSystem.AttachToActivePlayer();
        logs.AppendLine("Ran FallingLeavesSystem.AttachToActivePlayer() on scene.");

        // Mark scene dirty so user can see it in Hierarchy immediately
        var activeScene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(activeScene);
        logs.AppendLine($"Active scene: {activeScene.name}");

        return logs.ToString();
    }
}
