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
        [SerializeField, Range(0.1f, 1f)] private float stepVolume = 0.75f;
        [SerializeField, Range(0f, 1f)] private float minPitch = 0.92f;
        [SerializeField, Range(1f, 2f)] private float maxPitch = 1.08f;
        [SerializeField] private AudioSource footstepAudioSource = default;
        [SerializeField] private LayerMask groundLayerMask = ~0;

        private float footstepTimer = 0f;
        private float GetCurrentOffset => _controller != null && _controller.IsCrouching
            ? baseStepSpeed * crouchStepMultipleir
            : (_controller != null && _controller.IsSprinting ? baseStepSpeed * sprintStepMultipleir : baseStepSpeed);

        private FirstPersonController _controller;
        private CheckTerrainTexture terrainTextureChecker;
        private AudioClip previousClip;

        const string terrainTag = "Terrain";

        private void Start()
        {
            _controller = GetComponent<FirstPersonController>();
            terrainTextureChecker = GetComponent<CheckTerrainTexture>();

            if (footstepAudioSource == null)
            {
                footstepAudioSource = GetComponent<AudioSource>();
                if (footstepAudioSource == null)
                {
                    footstepAudioSource = gameObject.AddComponent<AudioSource>();
                    footstepAudioSource.playOnAwake = false;
                    footstepAudioSource.spatialBlend = 0f;
                }
            }
        }

        private void Update()
        {
            if (useFootsteps)
                HandleFootsteps();
        }

        private void HandleFootsteps()
        {
            if (_controller == null) _controller = GetComponent<FirstPersonController>();
            if (_controller == null || !_controller.IsGrounded) return;
            if (_controller.CurrentInput == Vector2.zero) return;

            footstepTimer -= Time.deltaTime;

            if (footstepTimer <= 0f)
            {
                // Raycast downwards from slightly above the player base to detect what ground was stepped on
                Vector3 rayOrigin = transform.position + Vector3.up * 0.25f;
                if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 2.0f, groundLayerMask, QueryTriggerInteraction.Ignore))
                {
                    Terrain hitTerrain = hit.collider.GetComponent<Terrain>();
                    if (hitTerrain == null && hit.collider is TerrainCollider tc)
                    {
                        hitTerrain = tc.GetComponent<Terrain>();
                    }

                    bool isTerrain = (hitTerrain != null) || hit.collider.CompareTag(terrainTag);

                    if (isTerrain)
                    {
                        HandleTerrain(hit.point, hitTerrain);
                    }
                    else
                    {
                        HandleCollider(hit.collider);
                    }
                }

                footstepTimer = GetCurrentOffset;
            }
        }

        private void HandleTerrain(Vector3 hitPoint, Terrain hitTerrain)
        {
            if (terrainTextureChecker == null)
            {
                terrainTextureChecker = GetComponent<CheckTerrainTexture>();
            }

            GroundType selectedGround = null;

            if (terrainTextureChecker != null && groundTypes != null && groundTypes.Length > 0)
            {
                terrainTextureChecker.GetTerrainTexture(hitPoint, hitTerrain);
                float[] weights = terrainTextureChecker.textureValues;

                if (weights != null && weights.Length > 0)
                {
                    // Find the single ground type with the highest texture weight at this exact step location
                    float maxWeight = -1f;

                    foreach (var gt in groundTypes)
                    {
                        if (gt == null) continue;
                        int texIdx = gt.indexOfTerrainTexture;
                        if (texIdx >= 0 && texIdx < weights.Length)
                        {
                            float w = weights[texIdx];
                            if (w > maxWeight)
                            {
                                maxWeight = w;
                                selectedGround = gt;
                            }
                        }
                    }

                    // Fallback to dominant texture layer index if weights were zero/unpainted
                    if (maxWeight <= 0f)
                    {
                        int dominantIdx = terrainTextureChecker.GetDominantTextureIndex(hitPoint, hitTerrain);
                        foreach (var gt in groundTypes)
                        {
                            if (gt != null && gt.indexOfTerrainTexture == dominantIdx)
                            {
                                selectedGround = gt;
                                break;
                            }
                        }
                    }
                }
            }

            // Fallback for terrain: use the first ground type in groundTypes (e.g. Grass) or defaultGroundType
            if (selectedGround == null)
            {
                selectedGround = (groundTypes != null && groundTypes.Length > 0 && groundTypes[0] != null)
                    ? groundTypes[0]
                    : defaultGroundType;
            }

            if (selectedGround != null && selectedGround.walkAudioClips != null && selectedGround.walkAudioClips.Length > 0)
            {
                AudioClip clip = GetClipFromArray(selectedGround.walkAudioClips);
                if (clip != null)
                {
                    PlayFootstepSound(clip);
                }
            }
        }

        private void HandleCollider(Collider col)
        {
            GroundType matchedGround = null;

            if (groundTypes != null)
            {
                for (int i = 0; i < groundTypes.Length; i++)
                {
                    if (groundTypes[i] != null && !string.IsNullOrEmpty(groundTypes[i].groundTag))
                    {
                        if (col.CompareTag(groundTypes[i].groundTag))
                        {
                            matchedGround = groundTypes[i];
                            break;
                        }
                    }
                }
            }

            if (matchedGround == null)
            {
                matchedGround = defaultGroundType;
            }

            if (matchedGround != null && matchedGround.walkAudioClips != null && matchedGround.walkAudioClips.Length > 0)
            {
                AudioClip clip = GetClipFromArray(matchedGround.walkAudioClips);
                if (clip != null)
                {
                    PlayFootstepSound(clip);
                }
            }
        }

        private AudioClip GetClipFromArray(AudioClip[] clipArray)
        {
            if (clipArray == null || clipArray.Length == 0) return null;
            if (clipArray.Length == 1) return clipArray[0];

            int attempts = 3;
            int index = Random.Range(0, clipArray.Length);
            AudioClip selectedClip = clipArray[index];

            while (selectedClip == previousClip && attempts > 0)
            {
                index = Random.Range(0, clipArray.Length);
                selectedClip = clipArray[index];
                attempts--;
            }

            previousClip = selectedClip;
            return selectedClip;
        }

        private void PlayFootstepSound(AudioClip clip)
        {
            if (clip == null || footstepAudioSource == null) return;

            float volume = stepVolume;
            if (_controller != null)
            {
                if (_controller.IsCrouching) volume *= 0.5f;
                else if (_controller.IsSprinting) volume = Mathf.Min(1.0f, volume * 1.25f);
            }

            footstepAudioSource.pitch = Random.Range(minPitch, maxPitch);
            footstepAudioSource.PlayOneShot(clip, volume);
        }

        public void PlaySoundWithRandomPitch(AudioClip clip)
        {
            if (clip == null) return;
            PlayFootstepSound(clip);
        }

        public void PlaySoundWithRandomPitch(AudioClip clip, float volume)
        {
            if (clip == null) return;
            if (footstepAudioSource == null) return;
            footstepAudioSource.pitch = Random.Range(minPitch, maxPitch);
            footstepAudioSource.PlayOneShot(clip, volume);
        }

        public void PlaySoundNormal(AudioClip clip)
        {
            if (clip == null || footstepAudioSource == null) return;
            footstepAudioSource.pitch = 1f;
            footstepAudioSource.PlayOneShot(clip, stepVolume);
        }
    }
}