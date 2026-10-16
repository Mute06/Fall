using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FirstPersonSystem
{
    [RequireComponent(typeof(Rigidbody))]
    public class Door_Draggable : Interactable
    {
        [SerializeField] private float pushForce;
        private Rigidbody rb;
        private InputManager _input;
        private float defaultAngularDrag;
        private Vector3 doorTransformDir;
        public bool IsOpen => transform.eulerAngles.y <= 330f && transform.localEulerAngles.y >= 270f;
        private void Start()
        {
            rb = GetComponent<Rigidbody>();
            _input = InputManager.Instance;
            defaultAngularDrag = rb.angularDamping;
            doorTransformDir = transform.TransformDirection(Vector3.forward);
        }

        public override void OnFocus()
        {

        }

        public override void OnInteractEnd(FirstPersonController player)
        {
            rb.angularDamping = defaultAngularDrag;
            StartCoroutine(AutoClose());
        }

        public override void OnInteracting(FirstPersonController player)
        {

            Vector3 playerTransfromDirection = Vector3.Normalize(player.transform.position - transform.position);
            float dot = Vector3.Dot(doorTransformDir, playerTransfromDirection);

            Debug.Log("Dot Product: " + dot);

            //Player is on behind  of the door
            if (dot < 0)
            {
                rb.AddRelativeTorque(0f, _input.GetInput_MouseX() * pushForce * Time.deltaTime, 0f, ForceMode.Impulse);
            }

            else // Player is in front of the door
            {
                rb.AddRelativeTorque(0f, _input.GetInput_MouseX() * -pushForce * Time.deltaTime, 0f, ForceMode.Impulse);
            }



        }

        public override void OnInteractStart(FirstPersonController player)
        {
            rb.angularDamping = 0f;
        }

        public override void OnLoseFocus()
        {
        }

        public override void OnStartFocus()
        {

        }

        private IEnumerator AutoClose()
        {
            while (IsOpen)
            {
                yield return new WaitForSeconds(3f);

                if (Vector3.Distance(transform.position, FirstPersonController.Instance.transform.position) >= 3f)
                {
                    rb.AddRelativeTorque(0f, 1000, 0f, ForceMode.Impulse);
                    Debug.Log("auto closed");
                    yield break;
                }
            }
        }
    }
}