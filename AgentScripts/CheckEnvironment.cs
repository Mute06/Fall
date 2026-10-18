using System;
using UnityEngine;
using UnityEditor;

public static class CheckEnvironment
{
    public static string Run()
    {
        var shaders = new string[]
        {
            "Universal Render Pipeline/Particles/Unlit",
            "Universal Render Pipeline/Particles/Simple Lit",
            "Universal Render Pipeline/Particles/Lit",
            "Universal Render Pipeline/Lit",
            "Universal Render Pipeline/Simple Lit",
            "Universal Render Pipeline/Unlit",
            "Particles/Standard Unlit"
        };

        var sb = new System.Text.StringBuilder();
        foreach (var s in shaders)
        {
            var shader = Shader.Find(s);
            sb.AppendLine($"{s}: {(shader != null ? "Found" : "Not Found")}");
        }

        var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        sb.AppendLine($"Active Scene: {activeScene}");

        var fpcInScene = GameObject.FindAnyObjectByType<FirstPersonSystem.FirstPersonController>();
        sb.AppendLine($"FPC in active scene: {(fpcInScene != null ? fpcInScene.name : "None")}");

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FP_Template/FP_Assets/Prefabs/FirstPersonController.prefab");
        sb.AppendLine($"Prefab loaded: {(prefab != null ? prefab.name : "None")}");

        return sb.ToString();
    }
}
