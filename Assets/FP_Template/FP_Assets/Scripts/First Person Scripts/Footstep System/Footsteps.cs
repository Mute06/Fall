using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FirstPersonSystem
{
    public class Footsteps : MonoBehaviour
    {
        [Header("Footstep Parameters")]
        [SerializeField] private bool useFootsteps = true;

        public GroundType defaultGroundType;
        public GroundType[] groundTypes;
        [SerializeField] private float baseStepSpeed = 0.5f;
        [SerializeField] private float crouchStepMultipleir = 1.5f;
        [SerializeField] private float sprintStepMultipleir = 0.6f;
        [SerializeField, Range(0f, 1f)] private float minPitch = 0.9f;
        [SerializeField, Range(1f, 2f)] private float maxPitch = 1.1f;
        [SerializeField] private AudioSource footstepAudioSource = default;
        private float footstepTimer = 0f;
        private float GetCurrentOffset => _controller.IsCrouching ? baseStepSpeed * crouchStepMultipleir : _controller.IsSprinting ? baseStepSpeed * sprintStepMultipleir : baseStepSpeed;
        private FirstPersonController _controller;
        private CheckTerrainTexture terrainTextureChecker;
        private AudioClip previousClip;

        const string terrainTag = "Terrain";

        private void Start()
        {
            _controller = GetComponent<FirstPersonController>();
            terrainTextureChecker = GetComponent<CheckTerrainTexture>();
        }

        private void Update()
        {
            if (useFootsteps)
                HandleFootsteps();
        }

        private void HandleFootsteps()
        {
            if (!_controller.IsGrounded) return;
            if (_controller.CurrentInput == Vector2.zero) return;

            footstepTimer -= Time.deltaTime;

            if (footstepTimer <= 0f)
            {
                if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 3f))
                {

                    bool didFound = false;
                    if (hit.collider.tag == terrainTag)
                    {
                        HandleTerrain();
                        didFound = true;
                    }
                    else
                    {
                        for (int i = 0; i < groundTypes.Length; i++)
                        {
                            if (groundTypes[i].groundTag == hit.collider.tag)
                            {
                                PlaySoundWithRandomPitch(GetClipFromArray(groundTypes[i].walkAudioClips));
                                didFound = true;
                                break;
                            }
                        }
                    }

                    if (!didFound)
                    {
                        PlaySoundWithRandomPitch(GetClipFromArray(defaultGroundType.walkAudioClips));
                    }

                }

                footstepTimer = GetCurrentOffset;
            }
        }

        private AudioClip GetClipFromArray(AudioClip[] clipArray)
        {
            int attemps = 3;
            AudioClip selectedClip = clipArray[Random.Range(0, clipArray.Length - 1)];

            while (selectedClip == previousClip && attemps > 0)
            {
                selectedClip = clipArray[Random.Range(0, clipArray.Length - 1)];
                attemps--;
            }

            previousClip = selectedClip;
            return selectedClip;
        }

        private void PlaySoundWithRandomPitch(AudioClip clip)
        {
            footstepAudioSource.pitch = Random.Range(minPitch, maxPitch);
            footstepAudioSource.PlayOneShot(clip);
        }
        private void PlaySoundWithRandomPitch(AudioClip clip, float volume)
        {
            footstepAudioSource.pitch = Random.Range(minPitch, maxPitch);
            footstepAudioSource.PlayOneShot(clip, volume);
        }
        private void PlaySoundNormal(AudioClip clip)
        {
            footstepAudioSource.pitch = 1f;
            footstepAudioSource.PlayOneShot(clip);
        }

        private void HandleTerrain()
        {
            terrainTextureChecker.GetTerrainTexture();


            foreach (var item in groundTypes)
            {
                if (terrainTextureChecker.textureValues[item.indexOfTerrainTexture] > 0)
                {
                    PlaySoundWithRandomPitch(GetClipFromArray(item.walkAudioClips), terrainTextureChecker.textureValues[item.indexOfTerrainTexture]);
                }
            }
        }
    }
}