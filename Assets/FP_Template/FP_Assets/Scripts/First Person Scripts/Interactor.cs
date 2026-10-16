using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FirstPersonSystem
{
    public class Interactor : MonoBehaviour
    {
        [SerializeField] private bool canInteract = true;

        [SerializeField] private Vector3 interactionRayPoint = default;
        [SerializeField] private float interactionDistance = default;
        [SerializeField] protected LayerMask interactionLayers = default;

        private Interactable currentInteractable;
        private FirstPersonController controller;
        private InputManager _input;
        private Camera _cam;
        private bool didLookedAtCurrentInteractable;
        private bool wasInteracting;
        private bool lostFocus;
        private CrosshairManager crosshair;

        private void Start()
        {
            _input = InputManager.Instance;
            _cam = GetComponentInChildren<Camera>();
            controller = GetComponent<FirstPersonController>();
            crosshair = CrosshairManager.Instance;
        }

        private void Update()
        {
            if (canInteract)
            {
                HandleInteractionCheck();
                HandleInteractionInput();
            }
        }

        private RaycastHit lastHit;
        public Camera Cam => _cam;
        public Vector3 InteractionRayPoint => interactionRayPoint;
        public float InteractionDistance => interactionDistance;
        public LayerMask InteractionLayers => interactionLayers;
        public Interactable CurrentInteractable => currentInteractable;
        public RaycastHit LastHit => lastHit;

        private void HandleInteractionCheck()
        {
            if (Physics.Raycast(_cam.ViewportPointToRay(interactionRayPoint), out RaycastHit hit, interactionDistance, interactionLayers))
            {
                lastHit = hit;
                if (hit.collider.TryGetComponent(out Interactable interactable) && (currentInteractable == null || interactable.GetInstanceID() != currentInteractable.GetInstanceID()))
                {
                    if (currentInteractable != null && !lostFocus)
                    {
                        currentInteractable.OnLoseFocus();
                    }

                    currentInteractable = interactable;

                    if (!didLookedAtCurrentInteractable)
                    {
                        currentInteractable.OnStartFocus();
                        didLookedAtCurrentInteractable = true;
                        lostFocus = false;
                        crosshair.ChangeState(CrosshairManager.CrosshairStates.Interactable);
                    }

                    if (currentInteractable)
                    {
                        currentInteractable.OnFocus();
                    }
                }
            }
            else if (currentInteractable)
            {
                if (!lostFocus)
                {
                    currentInteractable.OnLoseFocus();
                    lostFocus = true;
                }

                if (!wasInteracting)
                {
                    currentInteractable = null;
                    didLookedAtCurrentInteractable = false;
                    crosshair.ChangeState(CrosshairManager.CrosshairStates.Normal);
                }
                else if (_input.GetInput_Interacting())
                {
                    crosshair.ChangeState(CrosshairManager.CrosshairStates.Interacting);
                }
            }
        }

        private void HandleInteractionInput()
        {
            if (currentInteractable == null) { return; }

            if (currentInteractable != null)
            {
                if (Physics.Raycast(_cam.ViewportPointToRay(interactionRayPoint), out RaycastHit hit, interactionDistance, interactionLayers))
                {
                    lastHit = hit;
                    if (_input.GetInput_Interacted())
                    {
                        wasInteracting = true;
                        currentInteractable.OnInteractStart(controller);
                    }
                }

                if (_input.GetInput_Interacting())
                {
                    if (currentInteractable != null)
                    {
                        currentInteractable.OnInteracting(controller);
                        crosshair.ChangeState(CrosshairManager.CrosshairStates.Interacting);

                    }
                }
                else if (_input.GetInput_InteractingEnded())
                {
                    currentInteractable.OnInteractEnd(controller);
                    wasInteracting = false;
                    if (hit.collider != null)
                    {
                        crosshair.ChangeState(CrosshairManager.CrosshairStates.Interactable);
                    }
                    else
                    {
                        crosshair.ChangeState(CrosshairManager.CrosshairStates.Normal);
                    }
                }
            }
        }


        public void SetCanInteract(bool value)
        {
            canInteract = value;

            if (!canInteract)
            {
                crosshair.ChangeState(CrosshairManager.CrosshairStates.Normal);
                if (currentInteractable != null)
                {
                    currentInteractable.OnInteractEnd(controller);
                    currentInteractable.OnLoseFocus();
                    currentInteractable = null;
                    didLookedAtCurrentInteractable = false;
                    wasInteracting = false;
                }
            }
            if (!value && wasInteracting)
            {
                wasInteracting = false;
            }
        }
    }
}