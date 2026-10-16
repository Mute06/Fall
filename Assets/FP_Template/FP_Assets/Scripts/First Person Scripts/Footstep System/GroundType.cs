using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FirstPersonSystem
{
    [CreateAssetMenu(fileName = "groundType_", menuName = "ScriptableObjects/Ground Type")]
    public class GroundType : ScriptableObject
    {
        public AudioClip[] walkAudioClips;
        [TagSelector] public string groundTag = "";
        public int indexOfTerrainTexture;
    }
}