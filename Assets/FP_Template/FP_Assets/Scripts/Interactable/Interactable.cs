using UnityEngine;

namespace FirstPersonSystem
{
    public abstract class Interactable : MonoBehaviour
    {
        public virtual void Awake()
        {
            if (gameObject.layer == 0)
            {
                int layer = LayerMask.NameToLayer("Interactable");
                gameObject.layer = layer != -1 ? layer : 8;
            }
        }
        public abstract void OnInteractStart(FirstPersonController player);
        public abstract void OnFocus();
        public abstract void OnLoseFocus();
        public abstract void OnStartFocus();
        public abstract void OnInteracting(FirstPersonController player);
        public abstract void OnInteractEnd(FirstPersonController player);
    }
}