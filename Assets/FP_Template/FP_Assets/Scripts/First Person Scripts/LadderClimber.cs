using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FirstPersonSystem
{
    public class LadderClimber : MonoBehaviour
    {
        [SerializeField] private float climbSpeed = 5f;
        [SerializeField] private float enterClimbingTime = 0.5f;
        [Tooltip("speed to rotate the camera up and down (around x axis) while climbing")]
        [SerializeField] private float lookSpeedY = 1f;
        [SerializeField] private float lookSpeedX = 1.5f;
        [SerializeField] private float upperLookLimit = 80f, lowerLookLimit = 80f;
        private bool isClimbing;
        private FirstPersonController controller;
        private CharacterController characterController;
        private InputManager _input;
        private Vector3 ladderUpDir;
        private float rotationX;
        private Transform playerCamera;
        private Transform currentExitPos;
        private InteractableLadder currentClimbingLadder;
        private Vector3 moveDirection;
        private void Start()
        {
            controller = GetComponent<FirstPersonController>();
            characterController = GetComponent<CharacterController>();
            _input = InputManager.Instance;
            playerCamera = Camera.main.transform;
        }
        private void OnTriggerEnter(Collider other)
        {

            if (other.CompareTag("Ladder"))
            {
                if (isClimbing)
                {
                    //Exit climbing
                    ExitLadder(currentExitPos);
                }
            }
        }


        private void Update()
        {
            if (!isClimbing) { return; }

            HandleLadderMovement();

            HandleMouseLook();
        }

        private void HandleLadderMovement()
        {
            moveDirection = ladderUpDir * _input.GetInput_Vertical();

            if (currentClimbingLadder.isUp)
            {
                if (transform.position.y < currentClimbingLadder.ladderClimbEnterPos.position.y)
                {
                    if (_input.GetInput_Vertical() < -0.1f)
                    {
                        moveDirection *= 0f;
                    }
                }
            }
            else
            {
                if (transform.position.y > currentClimbingLadder.ladderClimbEnterPos.position.y)
                {
                    if (_input.GetInput_Vertical() > 0.1f)
                    {
                        moveDirection *= 0f;
                    }
                }
            }


            characterController.Move(climbSpeed * Time.deltaTime * moveDirection);

        }

        private void HandleMouseLook()
        {

            //Rotate Camera up and down (around x axis)
            rotationX -= _input.GetInput_MouseY() * lookSpeedY;
            rotationX = Mathf.Clamp(rotationX, -upperLookLimit, lowerLookLimit);

            playerCamera.transform.localRotation = Quaternion.Euler(rotationX, 0f, 0f);


            //Rotate parent player left and right (around y axix)
            transform.rotation *= Quaternion.Euler(0f, _input.GetInput_MouseX() * lookSpeedX, 0f);
        }

        public void EnterLadder(Transform ladderEnterTransform, Transform ladderExitPos, Vector3 upDir, InteractableLadder ladder)
        {
            if (controller.IsCrouching)
            {
                controller.ToggleCrouch();
            }
            currentClimbingLadder = ladder;
            controller.EnterLadderClimbing();
            ladderUpDir = upDir;
            currentExitPos = ladderExitPos;
            LeanTween.rotate(gameObject, ladderEnterTransform.forward, enterClimbingTime);

            LeanTween.move(gameObject, ladderEnterTransform, enterClimbingTime).setOnComplete(() =>
            isClimbing = true);

        }

        public void ExitLadder(Transform ExitPos)
        {
            isClimbing = false;
            LeanTween.move(gameObject, ExitPos, enterClimbingTime).setOnComplete(() => controller.ExitLadderClimbing(rotationX));
        }

        public bool GetIsClimbing()
        {
            return isClimbing;
        }
        public bool IsClimbingThisLadder(InteractableLadder ladderToCheck)
        {
            return currentClimbingLadder == ladderToCheck;
        }
    }
}