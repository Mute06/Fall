using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FirstPersonSystem
{
    public class HeadbobEffect : MonoBehaviour
    {
        [Header("Headbob Parameters")]
        [SerializeField] private bool canUseHeadbob = true;
        [SerializeField] private bool focusCam = true;
        [SerializeField] private float toggleSpeed = 0.3f;
        [SerializeField] private float focusDistance = 10f;
        [SerializeField] private Vector2 walkBobSpeed = new Vector2(8f, 14f);
        [SerializeField] private Vector2 walkBobAmount = new Vector2(0.25f, 0.5f);
        [SerializeField] private Vector2 sprintBobSpeed = new Vector2(10, 18);
        [SerializeField] private Vector2 sprintBobAmount = new Vector2(0.5f, 1f);
        [SerializeField] private Vector2 crouchBobSpeed = new Vector2(5f, 8f);
        [SerializeField] private Vector2 crouchBobAmount = new Vector2(0.1f, 0.25f);

        private Vector3 defaultCamPos;
        private Vector2 headbobTimer;

        //Refs
        private CharacterController characterController;
        private FirstPersonController firstPersonController;
        private Transform parent;

        private void Awake()
        {
            characterController = GetComponentInParent<CharacterController>();
            firstPersonController = GetComponentInParent<FirstPersonController>();

            defaultCamPos = transform.localPosition;
            parent = transform.parent;
        }

        private void Update()
        {
            if (canUseHeadbob)
            {
                HandleHeadbob();
                if (focusCam)
                {
                    HandleCamFocus();
                }
            }
        }

        private Vector3 FootStepMotion(Vector2 amplitude)
        {
            headbobTimer.y += Time.deltaTime * (firstPersonController.IsCrouching ? crouchBobSpeed.y : firstPersonController.IsSprinting ? sprintBobSpeed.y : walkBobSpeed.y);
            headbobTimer.x += Time.deltaTime * (firstPersonController.IsCrouching ? crouchBobSpeed.x : firstPersonController.IsSprinting ? sprintBobSpeed.x : walkBobSpeed.x);
            Vector3 pos = Vector3.zero;
            pos.y = Mathf.Sin(headbobTimer.y) * amplitude.y;
            pos.x = Mathf.Sin(headbobTimer.x) * amplitude.x;
            return pos;
        }

        private void HandleHeadbob()
        {
            if (!firstPersonController.IsGrounded) { return; }
            float speed = characterController.velocity.magnitude;
            //If moving
            if (speed >= toggleSpeed)
            {

                Vector2 amplitude = new Vector2(firstPersonController.IsCrouching ? crouchBobAmount.x : firstPersonController.IsSprinting ? sprintBobAmount.x : walkBobAmount.x,
                    firstPersonController.IsCrouching ? crouchBobAmount.y : firstPersonController.IsSprinting ? sprintBobAmount.y : walkBobAmount.y);
                Vector3 motion = FootStepMotion(amplitude);
                Vector3 newCamPos = Vector3.zero;

                newCamPos.y = transform.localPosition.y + motion.y * Time.deltaTime;
                newCamPos.x = transform.localPosition.x + motion.x * Time.deltaTime;

                transform.localPosition = newCamPos;
            }
            else
            {
                ResetCamPostion();
            }
        }

        private void ResetCamPostion()
        {
            if (transform.localPosition == defaultCamPos) return;
            StartCoroutine(ReturnToDefaultPos(0.2f));
            headbobTimer = Vector2.zero;
        }

        private void HandleCamFocus()
        {
            transform.LookAt(FocusTarget());
        }

        private Vector3 FocusTarget()
        {
            Vector3 pos = transform.position;
            pos += parent.forward * focusDistance;
            return pos;
        }

        IEnumerator ReturnToDefaultPos(float time, System.Action onCompleteAction = null)
        {
            float timeElapsed = 0f;
            Vector3 startPos = transform.localPosition;

            while (timeElapsed < time)
            {
                transform.localPosition = Vector3.Lerp(startPos, defaultCamPos, timeElapsed / time);
                timeElapsed += Time.deltaTime;
                yield return null;
            }

            transform.localPosition = defaultCamPos;
            onCompleteAction?.Invoke();
        }

        public void SetCanUseHeadbob(bool value)
        {
            canUseHeadbob = value;
        }
    }
}