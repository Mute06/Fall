using System;
using UnityEngine;
using UnityEditor;
using FirstPersonSystem;

public static class VerifyFallingLeaves
{
    public static string Check()
    {
        var fpc = UnityEngine.Object.FindAnyObjectByType<FirstPersonController>();
        if (fpc == null) return "Error: FirstPersonController not found in scene.";

        var child = fpc.transform.Find("FallingLeaves_ParticleSystem");
        if (child == null) return "Error: FallingLeaves_ParticleSystem child not found under FirstPersonController.";

        var ps = child.GetComponent<ParticleSystem>();
        if (ps == null) return "Error: ParticleSystem component missing.";

        var renderer = child.GetComponent<ParticleSystemRenderer>();
        if (renderer == null) return "Error: ParticleSystemRenderer component missing.";

        var main = ps.main;
        var emission = ps.emission;
        var shape = ps.shape;
        var noise = ps.noise;
        var rot = ps.rotationOverLifetime;
        var texSheet = ps.textureSheetAnimation;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Parent: {child.parent.name}");
        sb.AppendLine($"Local Position: {child.localPosition}");
        sb.AppendLine($"Simulation Space: {main.simulationSpace} (Is World: {main.simulationSpace == ParticleSystemSimulationSpace.World})");
        sb.AppendLine($"Gravity Modifier: {main.gravityModifier.constant}");
        sb.AppendLine($"Emission Enabled: {emission.enabled}, Rate: {emission.rateOverTime.constantMin}-{emission.rateOverTime.constantMax}");
        sb.AppendLine($"Shape: {shape.shapeType}, Scale: {shape.scale}");
        sb.AppendLine($"Noise Enabled: {noise.enabled}, Strength: ({noise.strengthX.constant}, {noise.strengthY.constant}, {noise.strengthZ.constant})");
        sb.AppendLine($"Rotation Over Lifetime: {rot.enabled}, SeparateAxes: {rot.separateAxes}");
        sb.AppendLine($"Texture Sheet: {texSheet.enabled}, Tiles: {texSheet.numTilesX}x{texSheet.numTilesY}");
        sb.AppendLine($"Material: {(renderer.sharedMaterial != null ? renderer.sharedMaterial.name : "null")}");
        if (renderer.sharedMaterial != null)
        {
            sb.AppendLine($"Shader: {renderer.sharedMaterial.shader.name}");
            sb.AppendLine($"Texture: {(renderer.sharedMaterial.mainTexture != null ? renderer.sharedMaterial.mainTexture.name : "null")}");
        }

        return sb.ToString();
    }
}
