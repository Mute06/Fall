using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FirstPersonSystem
{
    public class Examinable : Interactable
    {
        [SerializeField] private float rotateSpeed = 5f;
        private bool isExamining;

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
            ExamineSystem.Instance.ExamineObject(gameObject, OnExamineEnd);
            isExamining = true;
        }

        public override void OnLoseFocus()
        {

        }

        public override void OnStartFocus()
        {

        }

        private void OnExamineEnd()
        {
            isExamining = false;
        }

        private void OnMouseDrag()
        {
            if (isExamining)
            {
                Vector2 delta = new Vector2(-InputManager.Instance.GetInput_MouseY() * rotateSpeed * Time.deltaTime,
                                            -InputManager.Instance.GetInput_MouseX() * rotateSpeed * Time.deltaTime);
                transform.Rotate(delta);
            }

        }

    }
}