using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FirstPersonSystem
{
    public class FlashlightController : MonoBehaviour
    {
        [SerializeField] private bool canUseFlashlight = true;

        [SerializeField] private float animTime = 0.5f;
        [SerializeField] private GameObject FlashlightGO;
        [SerializeField] private Transform openPos;
        private Vector3 closedPos;
        private InputManager _input;
        private bool isOpen;
        private bool isOnAnim;
        private Flashlight_PRO flashlight;

        private void Start()
        {
            _input = InputManager.Instance;
            _input.OnFlashlight += OnFlashLightToggle;
            closedPos = FlashlightGO.transform.localPosition;
            flashlight = FlashlightGO.GetComponent<Flashlight_PRO>();

            FlashlightGO.SetActive(isOpen);
        }
        private void OnDisable()
        {
            _input.OnFlashlight -= OnFlashLightToggle;
        }

        /// <summary>
        /// Toggles flashlight on and of 
        /// Can be called from other scripts
        /// </summary>
        public void OnFlashLightToggle()
        {
            if (!canUseFlashlight) { return; }
            if (isOnAnim) { return; }

            isOpen = !isOpen;

            if (isOpen)
            {
                isOnAnim = true;
                FlashlightGO.SetActive(true);
                LeanTween.moveLocal(FlashlightGO, openPos.localPosition, animTime).setOnComplete(OnAnimComplete);
            }
            else
            {
                isOnAnim = true;
                flashlight.Switch();
                LeanTween.moveLocal(FlashlightGO, closedPos, animTime).setOnComplete(OnAnimComplete);
            }
        }

        private void OnAnimComplete()
        {
            isOnAnim = false;

            if (!isOpen)
            {
                FlashlightGO.SetActive(false);
            }
            else
            {
                flashlight.Switch();
            }
        }

    }
}