using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace FirstPersonSystem
{
    public class UIManager : MonoBehaviour
    {
        [SerializeField] private Image staminaSlider = default;

        private void OnEnable()
        {
            FirstPersonController.OnStaminaChange += UpdateStamina;
        }
        private void OnDisable()
        {
            FirstPersonController.OnStaminaChange -= UpdateStamina;
        }

        private void Start()
        {
            UpdateStamina(100f); // You may wanna get the max stamina instead of 100
        }

        private void UpdateStamina(float currentStaminaRatio)
        {
            staminaSlider.fillAmount = currentStaminaRatio;
        }
    }
}