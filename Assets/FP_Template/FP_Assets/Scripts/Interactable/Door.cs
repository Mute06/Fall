using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FirstPersonSystem
{
    public class Door : Interactable
    {
        [SerializeField] private float rotateTime = 0.25f;
        [SerializeField] private float openDegree = 90f;
        private bool isOpen = false;
        private bool canBeInteracted = true;

        Vector3 doorTransformDir;
        private float startYRot;
        private void Start()
        {
            doorTransformDir = transform.TransformDirection(Vector3.forward);
            startYRot = transform.eulerAngles.y;
        }

        public override void OnFocus()
        {

        }

        public override void OnInteractEnd(FirstPersonController player)
        {

        }

        public override void OnInteracting(FirstPersonController player)
        {

        }

        public override void OnInteractStart(FirstPersonController player)
        {
            if (canBeInteracted)
            {
                isOpen = !isOpen;

                doorTransformDir = transform.TransformDirection(Vector3.forward);
                Vector3 playerTransfromDirection = Vector3.Normalize(player.transform.position - transform.position);
                float dot = Vector3.Dot(doorTransformDir, playerTransfromDirection);

                Debug.Log("Dot Product: " + dot);

                if (isOpen)
                {
                    //If behind the door
                    if (dot < 0)
                    {
                        StartCoroutine(RotateDoor(rotateTime, startYRot + -openDegree));
                    }

                    //if in front of the door
                    else
                    {
                        StartCoroutine(RotateDoor(rotateTime, startYRot + openDegree));
                    }
                }

                else
                {
                    StartCoroutine(RotateDoor(rotateTime, startYRot));
                }

            }
        }

        public override void OnLoseFocus()
        {
        }

        public override void OnStartFocus()
        {

        }

        private IEnumerator RotateDoor(float rotateTime, float newYRotation)
        {
            canBeInteracted = false;
            float timeElapsed = 0f;
            Quaternion startRotation = transform.rotation;
            Quaternion targetRotation = Quaternion.Euler(startRotation.eulerAngles.x, newYRotation, startRotation.eulerAngles.z);

            while (timeElapsed < rotateTime)
            {
                transform.rotation = Quaternion.Lerp(startRotation, targetRotation, timeElapsed / rotateTime);

                timeElapsed += Time.deltaTime;
                yield return null;
            }

            transform.rotation = targetRotation;
            canBeInteracted = true;
        }
    }
}