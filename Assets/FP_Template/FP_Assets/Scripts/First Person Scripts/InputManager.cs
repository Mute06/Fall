using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FirstPersonSystem
{
    [DefaultExecutionOrder(-1)]
    public class InputManager : MonoBehaviour
    {
        #region Singleton
        private static InputManager _instance;
        public static InputManager Instance { get { return _instance; } }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
            }
            else
            {
                _instance = this;
            }

        }
        #endregion

        [SerializeField] private KeyCode jumpKey = KeyCode.Space;
        [SerializeField] private KeyCode sprintKey = KeyCode.LeftShift;
        [SerializeField] private KeyCode crouchKey = KeyCode.LeftControl;
        [SerializeField] private KeyCode zoomKey = KeyCode.Mouse1;
        [SerializeField] private KeyCode interactKey = KeyCode.Mouse0;
        [SerializeField] private KeyCode flashlightKey = KeyCode.F;
        [SerializeField] private KeyCode cancelKey = KeyCode.Q;
        private float _moveInputHorizontal, _moveInputVertical, _mouseX, _mouseY;
        private bool _jumpedThisFrame, _sprinting, _crouching, _zoomDown, _zoomUp, _interacted, _interacting, _interactEnd, _flashlight, _cancelDown;

        public delegate void InputEvent();
        public event InputEvent OnFlashlight;
        public event InputEvent OnRightMouseDown;
        public event InputEvent OnInteracted;
        public event InputEvent OnCancel;

        private void Update()
        {
            _moveInputHorizontal = Input.GetAxis("Horizontal");
            _moveInputVertical = Input.GetAxis("Vertical");
            _mouseX = Input.GetAxis("Mouse X");
            _mouseY = Input.GetAxis("Mouse Y");
            _jumpedThisFrame = Input.GetKeyDown(jumpKey);
            _sprinting = Input.GetKey(sprintKey);
            _crouching = Input.GetKeyDown(crouchKey);
            _zoomDown = Input.GetKeyDown(zoomKey);
            if (_zoomDown) { OnRightMouseDown?.Invoke(); }
            _zoomUp = Input.GetKeyUp(zoomKey);
            _interacted = Input.GetKeyDown(interactKey);
            if (_interacted) { OnInteracted?.Invoke(); }
            _interacting = Input.GetKey(interactKey);
            _interactEnd = Input.GetKeyUp(interactKey);
            _flashlight = Input.GetKeyDown(flashlightKey);
            if (_flashlight) { OnFlashlight?.Invoke(); }
            _cancelDown = Input.GetKeyDown(cancelKey);
            if (_cancelDown) { OnCancel?.Invoke(); }
        }

        public float GetInput_Horizontal()
        {
            return _moveInputHorizontal;
        }

        public float GetInput_Vertical()
        {
            return _moveInputVertical;
        }

        public float GetInput_MouseX()
        {
            return _mouseX;
        }

        public float GetInput_MouseY()
        {
            return _mouseY;
        }

        public Vector2 GetInput_Move()
        {
            return new Vector2(_moveInputHorizontal, _moveInputVertical);
        }
        public Vector2 GetInput_Look()
        {
            return new Vector2(_mouseX, _mouseY);
        }

        public bool GetInput_Jumped()
        {
            return _jumpedThisFrame;
        }

        public bool GetInput_Sprinting()
        {
            return _sprinting;
        }

        public bool GetInput_Crouching()
        {
            return _crouching;
        }

        public bool GetInput_ZoomDown()
        {
            return _zoomDown;
        }

        public bool GetInput_ZoomUp()
        {
            return _zoomUp;
        }

        public bool GetInput_Interacted()
        {
            return _interacted;
        }

        public bool GetInput_Interacting()
        {
            return _interacting;
        }

        public bool GetInput_InteractingEnded()
        {
            return _interactEnd;
        }

        public Vector2 GetInput_MousePos()
        {
            return Input.mousePosition;
        }

        public bool GetInput_Flashlight()
        {
            return _flashlight;
        }

        public float GetInput_MouseScrollDelta()
        {
            return Input.mouseScrollDelta.y;
        }

        public bool GetInput_MiddleMouseButtonPressed()
        {
            return Input.GetMouseButtonDown(2);
        }

        public bool GetInput_AnyMoveInput()
        {
            return Input.GetAxisRaw("Horizontal") != 0 || Input.GetAxisRaw("Vertical") != 0;
        }

        public bool GetInput_CancelDown()
        {
            return _cancelDown;
        }
    }
}