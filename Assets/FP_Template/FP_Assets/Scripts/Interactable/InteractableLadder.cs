using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FirstPersonSystem
{
    public class InteractableLadder : Interactable
    {
        private LadderClimber ladderClimber;
        public bool isUp;
        public Transform ladderClimbEnterPos;
        public Transform ladderClimbExitPos;
        public Transform cancelClimbingPos;

        private void Start()
        {
            ladderClimber = FindObjectOfType<LadderClimber>();
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
            if (!ladderClimber.GetIsClimbing())
            {
                ladderClimber.EnterLadder(ladderClimbEnterPos, ladderClimbExitPos, transform.up, this);
            }
            else if (ladderClimber.IsClimbingThisLadder(this))
            {
                ladderClimber.ExitLadder(cancelClimbingPos);
            }
        }

        public override void OnLoseFocus()
        {

        }

        public override void OnStartFocus()
        {

        }
    }
}