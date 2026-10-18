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

        // 0. Force import the new pixel art texture
        AssetDatabase.ImportAsset("Assets/Textures/FallingLeaves/T_AutumnLeaves_PixelArt.png", ImportAssetOptions.ForceUpdate);
        Texture2D pixelTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/FallingLeaves/T_AutumnLeaves_PixelArt.png");
        logs.AppendLine($"Loaded pixelTex: {(pixelTex != null ? pixelTex.name + " (" + pixelTex.width + "x" + pixelTex.height + ")" : "null")}");

        Material mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_FallingLeaves.mat");
        if (mat != null && pixelTex != null)
        {
            mat.mainTexture = pixelTex;
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", pixelTex);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            logs.AppendLine("Assigned pixelTex to M_FallingLeaves.mat successfully.");
        }

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
