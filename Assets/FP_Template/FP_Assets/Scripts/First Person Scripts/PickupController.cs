using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FirstPersonSystem
{
    public class PickupController : MonoBehaviour
    {
        [Header("Pickup Settings")]
        [SerializeField] private bool canPickup = true;
        [SerializeField] private Transform holdArea;
        private GameObject heldObj;
        private Rigidbody heldRB;
        private Camera cam;

        [Header("Physics Parameters")]
        [SerializeField] private float pickupRange = 5f;
        [SerializeField] private float pickupForce = 150f;
        [SerializeField] private float throwForce = 200f;
        [SerializeField] private float heldDrag = 10f;
        [SerializeField] private float heldAngularDrag = 10f;
        [SerializeField] private float distanceThershold = 0.1f;
        [SerializeField] private float scrollScale = 0.5f;
        [SerializeField] private float minDistanceToCam = 0.75f, maxDistanceToCam = 3f;
        [SerializeField] private LayerMask pickupLayer;
        private InputManager _input;
        private float defaultDrag;
        private float defaultAngularDrag;
        private Vector3 defaultHoldPos;
        private FirstPersonController controller;

        private void Start()
        {
            _input = InputManager.Instance;
            cam = Camera.main;
            defaultHoldPos = holdArea.localPosition;
            controller = GetComponent<FirstPersonController>();
        }

        private void OnEnable()
        {
            _input = InputManager.Instance;
            _input.OnInteracted += OnInteracted;
        }

        private void OnDisable()
        {
            _input.OnInteracted -= OnInteracted;
        }

        private void OnInteracted()
        {
            if (!canPickup) { return; }

            if (heldObj == null)
            {
                Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                if (Physics.Raycast(ray, out RaycastHit hit, 5f, pickupLayer))
                {

                    //Pickup object
                    PickupObject(hit.transform.gameObject);
                }
            }

            else
            {
                //Drop Object
                DropObject();

            }
        }


        private void Update()
        {
            if (heldObj != null)
            {
                //Move object
                MoveObject();
            }
        }

        private void MoveObject()
        {
            if (Vector3.Distance(heldObj.transform.position, holdArea.position) > distanceThershold)
            {
                Vector3 moveDir = holdArea.position - heldObj.transform.position;
                heldRB.AddForce(moveDir * pickupForce);

            }

            //Move forward and backwards
            float scroll = _input.GetInput_MouseScrollDelta();
            if (Mathf.Abs(scroll) >= 0.1f)
            {
                Vector3 targetPos = holdArea.position + scroll * scrollScale * Time.deltaTime * cam.transform.forward;

                float distanceFromCam = Vector3.Distance(targetPos, cam.transform.position);

                if (minDistanceToCam <= distanceFromCam && distanceFromCam <= maxDistanceToCam)
                {
                    Debug.Log("Moved with scroll");
                    holdArea.position = targetPos;
                }
            }

            //Reset Rotation
            if (_input.GetInput_MiddleMouseButtonPressed())
            {
                Quaternion targetRotation = Quaternion.LookRotation(cam.transform.position - heldObj.transform.position);
                StartCoroutine(QuaternionLerp(heldObj.transform, targetRotation, 0.25f));
            }

            if (_input.GetInput_ZoomDown())
            {
                ThrowObject();
            }

        }

        private void PickupObject(GameObject pickObj)
        {
            if (pickObj.TryGetComponent(out heldRB))
            {
                if (heldRB != null)
                {
                    heldRB.useGravity = false;
                    defaultDrag = heldRB.linearDamping;
                    heldRB.linearDamping = heldDrag;
                    defaultAngularDrag = heldRB.angularDamping;
                    heldRB.angularDamping = heldAngularDrag;
                    heldRB.interpolation = RigidbodyInterpolation.Interpolate;

                    heldObj = pickObj;
                    controller.SetCanZoom(false);
                }
            }
        }

        private void DropObject()
        {
            if (heldRB != null)
            {
                heldRB.useGravity = true;
                heldRB.linearDamping = defaultDrag;
                heldRB.angularDamping = defaultAngularDrag;

                holdArea.localPosition = defaultHoldPos;
                heldObj = null;
                controller.SetCanZoomTrueLater();
            }

        }

        private void ThrowObject()
        {
            if (heldRB != null)
            {
                heldRB.AddForce(cam.transform.forward * throwForce, ForceMode.Impulse);
                DropObject();
            }
        }

        IEnumerator QuaternionLerp(Transform transformToRotate, Quaternion targetRotation, float duration)
        {
            float timeElapsed = 0f;
            Quaternion startRotation = transformToRotate.rotation;

            while (timeElapsed <= duration)
            {
                transformToRotate.rotation = Quaternion.Lerp(startRotation, targetRotation, timeElapsed / duration);
                timeElapsed += Time.deltaTime;
                yield return null;
            }

            transformToRotate.rotation = targetRotation;
        }
    }
}