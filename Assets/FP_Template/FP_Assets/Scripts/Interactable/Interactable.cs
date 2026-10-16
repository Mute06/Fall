using UnityEngine;

namespace FirstPersonSystem
{
    public abstract class Interactable : MonoBehaviour
    {
        public virtual void Awake()
        {
            gameObject.layer = 8; // Interactable layer
        }
        public abstract void OnInteractStart(FirstPersonController player);
        public abstract void OnFocus();
        public abstract void OnLoseFocus();
        public abstract void OnStartFocus();
        public abstract void OnInteracting(FirstPersonController player);
        public abstract void OnInteractEnd(FirstPersonController player);
    }
}