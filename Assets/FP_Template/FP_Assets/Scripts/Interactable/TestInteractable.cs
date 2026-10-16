using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FirstPersonSystem
{
    public class TestInteractable : Interactable
    {
        public override void OnFocus()
        {
            Debug.Log("Looking at " + gameObject.name);
        }

        public override void OnInteractEnd(FirstPersonController player)
        {
            Debug.Log("Interaction ended with" + gameObject.name);
        }

        public override void OnInteracting(FirstPersonController player)
        {
            Debug.Log("Interacting with " + gameObject.name);
        }

        public override void OnInteractStart(FirstPersonController player)
        {
            Debug.Log("Interacted with " + gameObject.name);
        }

        public override void OnLoseFocus()
        {
            Debug.Log("Stopped looking at " + gameObject.name);

        }

        public override void OnStartFocus()
        {
            Debug.Log("Started Looking at " + gameObject.name);
        }
    }
}