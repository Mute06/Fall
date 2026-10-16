using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FirstPersonSystem
{
    public class CrosshairManager : MonoBehaviour
    {
        #region Singleton
        private static CrosshairManager _instance;
        public static CrosshairManager Instance { get { return _instance; } }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
            }
            else
            {
                _instance = this;
            }
        }
        #endregion


        [SerializeField] private Sprite normalCrosshair, InteractableCrosshair, InteractingCrosshair;
        private Image crosshairImage;

        public enum CrosshairStates
        {
            Normal, Interactable, Interacting
        }

        private CrosshairStates currentState;

        private void Start()
        {
            crosshairImage = GetComponentInChildren<Image>();
            ChangeState(CrosshairStates.Normal);
        }

        public void ChangeState(CrosshairStates newState)
        {
            currentState = newState;

            switch (currentState)
            {
                case CrosshairStates.Normal:
                    crosshairImage.sprite = normalCrosshair;
                    break;
                case CrosshairStates.Interactable:
                    crosshairImage.sprite = InteractableCrosshair;
                    break;
                case CrosshairStates.Interacting:
                    crosshairImage.sprite = InteractingCrosshair;
                    break;
            }
        }

        public void CloseCrosshair()
        {
            crosshairImage.enabled = false;
        }
        public void EnableCrosshair()
        {
            crosshairImage.enabled = true;
        }
    }
}