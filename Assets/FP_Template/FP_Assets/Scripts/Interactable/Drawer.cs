using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FirstPersonSystem
{
    public class Drawer : Interactable
    {
        [SerializeField] private float moveSpeed = 2f;
        private InputManager _input;
        private Rigidbody _rb;
        private void Start()
        {
            _input = InputManager.Instance;
            _rb = GetComponent<Rigidbody>();
        }
        public override void OnFocus()
        {

        }

        public override void OnInteractEnd(FirstPersonController player)
        {

        }

        public override void OnInteracting(FirstPersonController player)
        {
            Vector3 direction = -transform.TransformDirection(Vector3.right);

            _rb.AddRelativeForce(-_input.GetInput_MouseY() * moveSpeed * Time.deltaTime * direction, ForceMode.Impulse);
        }

        public override void OnInteractStart(FirstPersonController player)
        {

        }

        public override void OnLoseFocus()
        {

        }

        public override void OnStartFocus()
        {

        }
    }
}