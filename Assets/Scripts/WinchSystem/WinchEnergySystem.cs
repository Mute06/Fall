using System;
using UnityEngine;

namespace FirstPersonSystem
{
    public class WinchEnergySystem : MonoBehaviour
    {
        [Header("Energy Capacity")]
        [SerializeField] private float maxEnergy = 150f;
        [SerializeField] private float currentEnergy = 150f;

        [Header("Consumption Tuning")]
        [Tooltip("Base motor electrical drain per second when winch is running.")]
        [SerializeField] private float baseMotorDrain = 2f;

        [Tooltip("Multiplier scaling tension force (N) to work consumed.")]
        [SerializeField] private float forceWorkMultiplier = 0.0015f;

        [Header("Recharge Tuning")]
        [SerializeField] private bool canRecharge = true;
        [SerializeField] private float rechargeDelay = 1.2f;
        [SerializeField] private float rechargeRate = 12f;

        private float timeSinceLastDrain = 0f;

        public float MaxEnergy => maxEnergy;
        public float CurrentEnergy => currentEnergy;
        public float EnergyRatio => maxEnergy > 0f ? Mathf.Clamp01(currentEnergy / maxEnergy) : 0f;
        public bool HasEnergy => currentEnergy > 0.5f;

        public event Action<float> OnEnergyChanged;
        public event Action OnEnergyDepleted;

        private void Start()
        {
            currentEnergy = maxEnergy;
            OnEnergyChanged?.Invoke(EnergyRatio);
        }

        private void Update()
        {
            if (canRecharge && currentEnergy < maxEnergy)
            {
                timeSinceLastDrain += Time.deltaTime;
                if (timeSinceLastDrain >= rechargeDelay)
                {
                    currentEnergy = Mathf.Min(maxEnergy, currentEnergy + rechargeRate * Time.deltaTime);
                    OnEnergyChanged?.Invoke(EnergyRatio);
                }
            }
        }

        /// <summary>
        /// Consumes energy based on motor idle + physical work (Tension * Speed).
        /// Returns true if energy was successfully consumed and remains available.
        /// </summary>
        public bool ConsumeEnergy(float tensionForce, float reelSpeed, float deltaTime)
        {
            if (currentEnergy <= 0f)
            {
                currentEnergy = 0f;
                OnEnergyDepleted?.Invoke();
                return false;
            }

            timeSinceLastDrain = 0f;

            // Physical Work: Power = Force * Velocity
            float dynamicWork = Mathf.Max(0f, tensionForce) * forceWorkMultiplier * Mathf.Max(0.5f, reelSpeed);
            float totalDrainRate = baseMotorDrain + dynamicWork;
            float energyUsed = totalDrainRate * deltaTime;

            currentEnergy = Mathf.Max(0f, currentEnergy - energyUsed);
            OnEnergyChanged?.Invoke(EnergyRatio);

            if (currentEnergy <= 0f)
            {
                OnEnergyDepleted?.Invoke();
                return false;
            }

            return true;
        }

        public void AddEnergy(float amount)
        {
            currentEnergy = Mathf.Clamp(currentEnergy + amount, 0f, maxEnergy);
            OnEnergyChanged?.Invoke(EnergyRatio);
        }
    }
}
