using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace FirstPersonSystem
{
    public class FirstPersonController : MonoBehaviour
    {
        #region Variables
        public bool CanMove { get; private set; } = true;
        public bool IsSprinting => canSprint && _input.GetInput_Sprinting();
        private bool ShouldJump => IsGrounded && _input.GetInput_Jumped();
        private bool ShouldCrouch => !duringCrouchAnimation && IsGrounded && _input.GetInput_Crouching();

        [Header("Functional Options")]
        [SerializeField] private bool canSprint = true;
        [SerializeField] private bool canJump = true;
        [SerializeField] private bool canCrouch = true;
        [SerializeField] private bool dynamicFOV = true;
        [SerializeField] private bool willSlideOnSlopes = true;
        [SerializeField] private bool canZoom = true;
        [SerializeField] private bool useStamina = true;

        [Header("Movement Paremeters")]
        [SerializeField] private float walkSpeed = 3f;
        [SerializeField] private float sprintSpeed = 6f;
        [SerializeField] private float crouchSpeed = 1.5f;
        [SerializeField] private float slopeSpeed = 8f;

        [Header("Look Parameters")]
        [SerializeField, Range(1f, 10f)] private float lookSpeedX = 2f;
        [SerializeField, Range(1f, 10f)] private float lookSpeedY = 2f;
        [SerializeField, Range(1, 180f)] private float upperLookLimit = 80f;
        [SerializeField, Range(1, 180f)] private float lowerLookLimit = 80f;

        [Header("Jump Parameters")]
        [SerializeField] private float jumpHeight = 1f;
        [SerializeField] private float gravity = 30f;

        [Header("Crouch Parameters")]
        [SerializeField] private float crouchHeight = 0.5f;
        [SerializeField] private float standingHeight = 2f;
        [SerializeField] private float timeToCrouch = 0.25f;
        [SerializeField] private Vector3 crouchingCenter = new Vector3(0f, 0.5f, 0f);
        [SerializeField] private Vector3 standingCenter = Vector3.zero;

        [Header("Ground Check Parameters")]
        [SerializeField] private Transform groundCheckPos;
        [SerializeField] private float crouchedGroundCheckLocalY = 0f;
        [SerializeField] private float groundCheckRadius = 0.25f;
        [SerializeField] private LayerMask groundMask;

        [Header("Camera Parameters")]
        [SerializeField] private float sprintFOV = 75f;
        [SerializeField] private float FOVChangeTime = 0.25f;

        [Header("Zoom Parameters")]
        [SerializeField] private float timeToZoom = 0.3f;
        [SerializeField] private float zoomFOV = 30f;

        [Header("Stamina Parameters")]
        [SerializeField] private float maxStamina = 100f;
        [SerializeField] private float stamineUseMultipleir = 5f;
        [SerializeField] private float timeBeforeStaminaRegenStarts = 4f;
        [SerializeField] private float staminaValueIncrement = 2f;
        [SerializeField] private float stamineTimeIncrement = 0.2f;
        private float currentStamina;
        private Coroutine regeneratingStamina;
        public static Action<float> OnStaminaChange;

        //Refs
        private Camera playerCamera;
        private CharacterController characterController;
        private InputManager _input;
        private HeadbobEffect headbob;

        private Vector3 moveDirection;
        private float targetSpeed;
        public Vector2 CurrentInput { get; private set; }
        private float rotationX = 0f;
        private float defaultFOV;
        private bool duringCrouchAnimation = false;
        private bool changingFOV;
        private float defaultGroundCheckY;
        private Coroutine zoomRoutine;
        private Coroutine changeFOVRoutine;
        private bool canLook = true;
        private ExamineSystem examineSystem;


        //Slope sliding
        private Vector3 hitPointNormal;
        private bool IsSliding
        {
            get
            {
                Debug.DrawRay(transform.position, Vector3.down, Color.blue);
                if (characterController.isGrounded && Physics.Raycast(transform.position, Vector3.down, out RaycastHit slopeHit, 4f))
                {
                    hitPointNormal = slopeHit.normal;
                    return Vector3.Angle(hitPointNormal, Vector3.up) > characterController.slopeLimit;
                }
                else
                {
                    return false;
                }
            }
        }

        public bool IsGrounded { get; private set; }
        public bool IsCrouching { get; private set; }

        #endregion

        #region Singleton
        private static FirstPersonController _instance;
        public static FirstPersonController Instance { get { return _instance; } }

        #endregion

        #region Events
        public delegate void MovementEvent();
        public event MovementEvent OnJump;

        #endregion

        //States
        public enum MovementStates
        {
            Idle, Walk, Sprint, Crouch, LadderClimbing
        }
        private MovementStates currentState;

        private void Awake()
        {
            _instance = this;

            playerCamera = GetComponentInChildren<Camera>();
            characterController = GetComponent<CharacterController>();
            _input = GetComponent<InputManager>();
            defaultFOV = playerCamera.fieldOfView;
            currentStamina = maxStamina;
            defaultGroundCheckY = groundCheckPos.localPosition.y;
            headbob = GetComponentInChildren<HeadbobEffect>();
            examineSystem = GetComponent<ExamineSystem>();
            examineSystem.OnExamineEnd += SetCanZoomTrueLater;

            //Lock cursor
            SetCursorLock(true);
        }



        private void Update()
        {
            if (CanMove)
            {
                HandleMovementInput();
                HandleMouseLook();

                if (canJump)
                    HandleJump();
                if (canCrouch)
                    HandleCrouch();
                if (canZoom)
                    HandleZoom();
                if (useStamina)
                    HandleStamina();

                ApplyFinalMovements();

                CheckStates();
            }
        }


        private void HandleMovementInput()
        {
            targetSpeed = IsCrouching ? crouchSpeed : IsSprinting ? sprintSpeed : _input.GetInput_AnyMoveInput() ? walkSpeed : 0f;

            CurrentInput = new Vector2(targetSpeed * _input.GetInput_Vertical(), targetSpeed * _input.GetInput_Horizontal());

            float moveDirectionY = moveDirection.y;

            moveDirection = transform.TransformDirection(Vector3.forward) * CurrentInput.x + transform.TransformDirection(Vector3.right) * CurrentInput.y;
            moveDirection.y = moveDirectionY;
        }

        private void HandleMouseLook()
        {
            if (!canLook)
            {
                return;
            }

            //Rotate Camera up and down (around x axis)
            rotationX -= _input.GetInput_MouseY() * lookSpeedY;
            rotationX = Mathf.Clamp(rotationX, -upperLookLimit, lowerLookLimit);

            playerCamera.transform.localRotation = Quaternion.Euler(rotationX, 0f, 0f);

            //Rotate parent player left and right (around y axix)
            transform.rotation *= Quaternion.Euler(0f, _input.GetInput_MouseX() * lookSpeedX, 0f);
        }


        private void HandleJump()
        {
            if (ShouldJump)
            {
                moveDirection.y = jumpHeight;

                OnJump?.Invoke();
                Debug.Log("Jumped");
            }
        }

        private void HandleStamina()
        {
            if (IsSprinting && CurrentInput != Vector2.zero)
            {
                if (regeneratingStamina != null)
                {
                    StopCoroutine(regeneratingStamina);
                    regeneratingStamina = null;
                }

                currentStamina -= stamineUseMultipleir * Time.deltaTime;

                if (currentStamina < 0f)
                {
                    currentStamina = 0f;
                }

                OnStaminaChange?.Invoke(currentStamina / maxStamina);

                if (currentStamina <= 0)
                {
                    canSprint = false;
                }
            }

            if (!IsSprinting && currentStamina < maxStamina && regeneratingStamina == null)
            {
                regeneratingStamina = StartCoroutine(RegenerateStamina());
            }
        }

        private void HandleZoom()
        {
            if (IsSprinting) { return; } // Can't zoom while sprinting
            if (_input.GetInput_ZoomDown())
            {
                if (zoomRoutine != null)
                {
                    StopCoroutine(zoomRoutine);
                    zoomRoutine = null;
                }

                zoomRoutine = StartCoroutine(ToogleZoom(true));
            }
            if (_input.GetInput_ZoomUp())
            {
                if (zoomRoutine != null)
                {
                    StopCoroutine(zoomRoutine);
                    zoomRoutine = null;
                }

                zoomRoutine = StartCoroutine(ToogleZoom(false));
            }
        }

        private IEnumerator ToogleZoom(bool isEnter)
        {
            float targetFOV = isEnter ? zoomFOV : defaultFOV;
            float startingFOV = playerCamera.fieldOfView;
            float timeElapsed = 0f;

            while (timeElapsed < timeToZoom)
            {
                playerCamera.fieldOfView = Mathf.Lerp(startingFOV, targetFOV, timeElapsed / timeToZoom);
                timeElapsed += Time.deltaTime;
                yield return null;
            }

            playerCamera.fieldOfView = targetFOV;
            zoomRoutine = null;
        }

        private IEnumerator RegenerateStamina()
        {
            yield return new WaitForSeconds(timeBeforeStaminaRegenStarts);
            WaitForSeconds timeToWait = new WaitForSeconds(stamineTimeIncrement);

            while (currentStamina < maxStamina)
            {
                if (currentStamina > 0)
                    canSprint = true;
                currentStamina += staminaValueIncrement;

                if (currentStamina > maxStamina)
                    currentStamina = maxStamina;

                OnStaminaChange?.Invoke(currentStamina / maxStamina);

                yield return timeToWait;
            }

            regeneratingStamina = null;
        }

        private void ApplyFinalMovements()
        {
            if (!IsGrounded)
            {
                moveDirection.y += -gravity * Time.deltaTime;
            }
            if (willSlideOnSlopes && IsSliding)
            {
                Debug.Log("Sliding");
                moveDirection += new Vector3(hitPointNormal.x, -hitPointNormal.y, hitPointNormal.z) * slopeSpeed;
            }

            characterController.Move(moveDirection * Time.deltaTime);
        }


        private void HandleCrouch()
        {
            if (ShouldCrouch)
            {
                ToggleCrouch();
            }
        }
        public void ToggleCrouch()
        {
            StartCoroutine(CrouchStand());
        }

        private IEnumerator CrouchStand()
        {
            //Check ceiling
            if (IsCrouching && Physics.Raycast(playerCamera.transform.position, Vector3.up, 1f))
                yield break;

            duringCrouchAnimation = true;


            float timeElapsed = 0f;
            float targetHeight = IsCrouching ? standingHeight : crouchHeight;
            float currentHeight = characterController.height;
            Vector3 targetCenter = IsCrouching ? standingCenter : crouchingCenter;
            Vector3 currentCenter = characterController.center;

            while (timeElapsed < timeToCrouch)
            {
                characterController.height = Mathf.Lerp(currentHeight, targetHeight, timeElapsed / timeToCrouch);
                characterController.center = Vector3.Lerp(currentCenter, targetCenter, timeElapsed / timeToCrouch);
                timeElapsed += Time.deltaTime;
                yield return null;
            }

            characterController.height = targetHeight;
            characterController.center = targetCenter;

            IsCrouching = !IsCrouching;
            duringCrouchAnimation = false;

            if (IsCrouching)
            {
                Vector3 newGroundCheckPos = groundCheckPos.localPosition;
                newGroundCheckPos.y = crouchedGroundCheckLocalY;
                groundCheckPos.localPosition = newGroundCheckPos;
            }
            else
            {
                Vector3 newGroundCheckPos = groundCheckPos.localPosition;
                newGroundCheckPos.y = defaultGroundCheckY;
                groundCheckPos.localPosition = newGroundCheckPos;
            }

        }

        public float GetTargetSpeed()
        {
            return targetSpeed;
        }

        public void EnterLadderClimbing()
        {
            SetCanMove(false);
            ChangeState(MovementStates.LadderClimbing);
        }
        public void ExitLadderClimbing(float newRotationX)
        {
            rotationX = newRotationX;
            SetCanMove(true);
            ChangeState(MovementStates.Idle);
        }

        public Vector3 GetForward()
        {
            return transform.forward;
        }

        public void SetCanMove(bool value)
        {
            CanMove = value;
            headbob.SetCanUseHeadbob(value);
            if (value)
            {

            }
            else
            {
                CurrentInput = Vector2.zero;
            }
        }

        public void SetCanLook(bool value)
        {
            canLook = value;
        }

        public void SetCanZoom(bool value)
        {
            canZoom = value;
        }

        public void SetCanZoomTrueLater()
        {
            canZoom = false;
            Invoke(nameof(SetCanZoomTrue), 0.1f);
        }

        private void SetCanZoomTrue()
        {
            canZoom = true;
        }

        private void ChangeState(MovementStates newState)
        {
            if (currentState == newState) { return; }

            //Exit funcs
            switch (currentState)
            {
                case MovementStates.Idle:
                    break;
                case MovementStates.Walk:
                    break;
                case MovementStates.Sprint:
                    OnSprintExit();
                    break;
                case MovementStates.Crouch:
                    break;
                default:
                    break;
            }

            currentState = newState;

            //Enter funcs
            switch (currentState)
            {
                case MovementStates.Idle:
                    break;
                case MovementStates.Walk:
                    break;
                case MovementStates.Sprint:
                    OnSprintEnter();
                    break;
                case MovementStates.Crouch:
                    break;
                default:
                    break;
            }
        }

        private void CheckStates()
        {
            if (IsGrounded)
            {
                if (characterController.velocity == Vector3.zero) // Idle state
                {
                    ChangeState(MovementStates.Idle);
                }
                else if (_input.GetInput_Sprinting() && targetSpeed == sprintSpeed && zoomRoutine == null) //Sprint state
                {
                    ChangeState(MovementStates.Sprint);
                }
                else if (targetSpeed == walkSpeed)
                {
                    ChangeState(MovementStates.Walk);
                }
                else if (IsCrouching)
                {
                    ChangeState(MovementStates.Crouch);
                }
            }
        }

        private void FixedUpdate()
        {
            IsGrounded = Physics.CheckSphere(groundCheckPos.position, groundCheckRadius, groundMask);
        }
        private void OnDrawGizmosSelected()
        {
            if (IsGrounded)
            {
                Gizmos.color = Color.green;
            }
            else
            {
                Gizmos.color = Color.red;
            }
            Gizmos.DrawWireSphere(groundCheckPos.position, groundCheckRadius);
        }

        private IEnumerator ChangeFOV(float targetFOV, float duration)
        {
            changingFOV = true;

            float timeElapsed = 0f;
            float currentFOV = playerCamera.fieldOfView;

            while (timeElapsed < duration)
            {
                playerCamera.fieldOfView = Mathf.Lerp(currentFOV, targetFOV, timeElapsed / duration);

                timeElapsed += Time.deltaTime;
                yield return null;
            }

            playerCamera.fieldOfView = targetFOV;
            changingFOV = false;
            changeFOVRoutine = null;
        }


        public void SetCursorLock(bool lockValue)
        {
            if (lockValue)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        #region State Functions
        //Sprint
        private void OnSprintEnter()
        {
            if (dynamicFOV)
            {
                if (changeFOVRoutine == null)
                {
                    changeFOVRoutine = StartCoroutine(ChangeFOV(sprintFOV, FOVChangeTime));
                }
                else
                {
                    StopCoroutine(changeFOVRoutine);
                    changeFOVRoutine = StartCoroutine(ChangeFOV(sprintFOV, FOVChangeTime));
                }

            }
        }

        private void OnSprintExit()
        {
            if (dynamicFOV)
            {
                if (changeFOVRoutine == null)
                {
                    changeFOVRoutine = StartCoroutine(ChangeFOV(defaultFOV, FOVChangeTime));

                }
                else
                {
                    StopCoroutine(changeFOVRoutine);
                    changeFOVRoutine = StartCoroutine(ChangeFOV(defaultFOV, FOVChangeTime));
                }


            }
        }

        #endregion
    }
}