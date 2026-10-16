using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FirstPersonSystem
{
    public class ObstaclePush : MonoBehaviour
    {
        [SerializeField] private float forceMagnitude = 2f;

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            Rigidbody rb = hit.collider.attachedRigidbody;

            if (rb != null)
            {
                Vector3 forceDir = hit.transform.position - transform.position;
                forceDir.y = 0f;
                forceDir.Normalize();

                rb.AddForceAtPosition(forceDir * forceMagnitude, transform.position, ForceMode.Impulse);
            }
        }
    }
}