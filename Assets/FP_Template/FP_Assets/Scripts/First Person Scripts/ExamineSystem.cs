using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FirstPersonSystem
{
    public class ExamineSystem : MonoBehaviour
    {
        [SerializeField] private Transform examinePoint;
        [SerializeField] private float scrolScale = 5f;
        [SerializeField] private float maxDistance = 4f;
        [SerializeField] private float minDistanceToCam = 0.5f;

        public delegate void ExamineDelegate();
        public event ExamineDelegate OnExamineStart;
        public event ExamineDelegate OnExamineEnd;

        const int examineLayer = 9;
        private bool isExamining;
        private Vector3 objectsDefaultPos;
        private int defaultLayer;
        private Quaternion defaultRot;
        private GameObject currentExamineGO;
        private FirstPersonController controller;
        private InputManager _input;
        private Interactor interactor;
        private Camera cam;
        private Blur blur;
        private Action OnExamineEndAction;

        #region Singleton
        private static ExamineSystem _instance;
        public static ExamineSystem Instance { get { return _instance; } }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }
            _instance = this;
        }
        #endregion
        private void Start()
        {
            controller = FirstPersonController.Instance;
            _input = InputManager.Instance;
            _input.OnRightMouseDown += StopExamining;
            interactor = GetComponent<Interactor>();
            cam = GameObject.FindGameObjectWithTag("ExamineCamera").GetComponent<Camera>();
            CamSetActive(false);
            blur = Blur.Instance;
        }
        private void OnDisable()
        {
            _input.OnRightMouseDown -= StopExamining;
        }

        public void ExamineObject(GameObject objectToExamine, Action OnExamineEnd = null)
        {
            if (isExamining) { return; }

            isExamining = true;
            OnExamineStart?.Invoke();
            CamSetActive(true);

            //Lock the player and blur the background
            controller.SetCanMove(false);
            interactor.SetCanInteract(false);
            blur.SetBlur(true);
            CrosshairManager.Instance.CloseCrosshair();
            controller.SetCursorLock(false);

            currentExamineGO = objectToExamine;
            objectsDefaultPos = objectToExamine.transform.position;
            objectToExamine.transform.position = examinePoint.position;
            defaultRot = objectToExamine.transform.rotation;
            objectToExamine.transform.LookAt(cam.transform.position, Vector3.up);
            objectToExamine.transform.SetParent(examinePoint);
            defaultLayer = objectToExamine.layer;
            SetLayerRecursively(objectToExamine, examineLayer);

            if (OnExamineEnd != null)
                OnExamineEndAction = OnExamineEnd;
        }


        public void StopExamining()
        {
            if (!isExamining) { return; }

            CamSetActive(false);
            isExamining = false;
            OnExamineEnd?.Invoke();
            CrosshairManager.Instance.EnableCrosshair();
            controller.SetCursorLock(true);

            blur.SetBlur(false);
            controller.SetCanMove(true);
            interactor.SetCanInteract(true);

            if (currentExamineGO != null)
            {
                currentExamineGO.transform.SetParent(null);
                currentExamineGO.transform.position = objectsDefaultPos;
                currentExamineGO.transform.rotation = defaultRot;
                SetLayerRecursively(currentExamineGO, defaultLayer);

                if (OnExamineEndAction != null)
                {
                    OnExamineEndAction.Invoke();
                }
            }
        }

        private void Update()
        {
            if (isExamining && currentExamineGO != null)
            {
                OnExamining();
            }
        }

        private void OnExamining()
        {
            Vector3 forward = cam.transform.forward;
            Vector3 targetPos = currentExamineGO.transform.position + _input.GetInput_MouseScrollDelta() * scrolScale * Time.deltaTime * forward;

            float distance = Vector3.Distance(targetPos, examinePoint.position);
            float distanceToCam = Vector3.Distance(targetPos, cam.transform.position);

            if (distance < maxDistance && distanceToCam > minDistanceToCam)
            {
                currentExamineGO.transform.position = targetPos;

            }

        }

        void SetLayerRecursively(GameObject obj, int newLayer)
        {
            if (null == obj)
            {
                return;
            }

            obj.layer = newLayer;

            foreach (Transform child in obj.transform)
            {
                if (null == child)
                {
                    continue;
                }
                SetLayerRecursively(child.gameObject, newLayer);
            }
        }

        private void CamSetActive(bool value)
        {
            cam.enabled = value;
            cam.gameObject.SetActive(value);
        }
    }
}