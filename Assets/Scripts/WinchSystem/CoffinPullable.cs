using System.Collections.Generic;
using UnityEngine;

namespace FirstPersonSystem
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class CoffinPullable : Interactable
    {
        [Header("Hook Points")]
        [Tooltip("Parent transform containing all the child hook points (e.g. HookPoints object).")]
        [SerializeField] private Transform hookPointsParent;

        [Header("Tow Attachment")]
        [SerializeField] private Transform defaultTowHookPoint;
        [SerializeField] private float alignmentTorque = 40f;
        [SerializeField] private float angularDamping = 15f;

        private Rigidbody rb;
        private bool isRopeConnected = false;
        private Transform activeHookPoint;

        public static CoffinPullable Instance { get; private set; }

        public Transform TowHookPoint => activeHookPoint != null ? activeHookPoint : (defaultTowHookPoint != null ? defaultTowHookPoint : transform);
        public Transform ActiveHookPoint => activeHookPoint;
        public Rigidbody Rb => rb;
        public bool IsRopeConnected => isRopeConnected;

        public override void Awake()
        {
            base.Awake();
            if (Instance == null) Instance = this;
            rb = GetComponent<Rigidbody>();

            // Lower center of mass to stabilize coffin against rolling and pitching
            rb.centerOfMass = new Vector3(0f, -0.25f, 0f);
            rb.maxAngularVelocity = 2.5f;

            // Auto-locate hookPointsParent if not assigned in Inspector
            if (hookPointsParent == null)
            {
                hookPointsParent = transform.Find("HookPoints");
                if (hookPointsParent == null) hookPointsParent = transform.Find("HooksPoint");
                if (hookPointsParent == null) hookPointsParent = transform.Find("Hooks");
            }

            if (defaultTowHookPoint == null)
            {
                // Fallback default hook
                GameObject hook = new GameObject("DefaultTowHook");
                hook.transform.SetParent(transform);
                hook.transform.localPosition = new Vector3(0f, 0.05f, 1.1f);
                defaultTowHookPoint = hook.transform;
            }

            activeHookPoint = defaultTowHookPoint;
        }

        /// <summary>
        /// Finds the closest hook point among all children of hookPointsParent to the given world position.
        /// </summary>
        public Transform GetClosestHookPoint(Vector3 targetPosition)
        {
            if (hookPointsParent != null && hookPointsParent.childCount > 0)
            {
                Transform closest = null;
                float minDistance = float.MaxValue;

                foreach (Transform child in hookPointsParent)
                {
                    float dist = Vector3.Distance(child.position, targetPosition);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        closest = child;
                    }
                }

                if (closest != null)
                {
                    return closest;
                }
            }

            return defaultTowHookPoint != null ? defaultTowHookPoint : transform;
        }

        public void SetActiveHookPoint(Transform hook)
        {
            activeHookPoint = hook != null ? hook : defaultTowHookPoint;
        }

        public void SetRopeConnected(bool connected)
        {
            isRopeConnected = connected;
            if (!connected)
            {
                activeHookPoint = defaultTowHookPoint;
            }
        }

        /// <summary>
        /// Applies the winch pulling force to the active hook point and smoothly aligns the coffin toward the pull.
        /// </summary>
        public void ApplyWinchForce(Vector3 pullDirection, float forceMagnitude)
        {
            if (rb == null || forceMagnitude <= 0.001f) return;

            Transform pullPoint = TowHookPoint;

            // Apply linear force at the specific hook point position
            Vector3 force = pullDirection.normalized * forceMagnitude;
            rb.AddForceAtPosition(force, pullPoint.position, ForceMode.Force);

            // Smooth yaw alignment only around Y axis - never pitch or roll
            Vector3 targetForward = Vector3.ProjectOnPlane(pullDirection, Vector3.up).normalized;
            if (targetForward.sqrMagnitude > 0.01f)
            {
                float angle = Vector3.SignedAngle(transform.forward, targetForward, Vector3.up);
                float yawTorque = Mathf.Clamp(angle * alignmentTorque, -120f, 120f) - rb.angularVelocity.y * angularDamping;
                rb.AddTorque(Vector3.up * yawTorque, ForceMode.Force);
            }
        }

        public override void OnInteractStart(FirstPersonController player)
        {
            if (WinchSystem.Instance != null && WinchSystem.Instance.CurrentState == WinchSystem.WinchState.CarryingRope)
            {
                Vector3 clickPosition = transform.position;
                if (player != null && player.TryGetComponent(out Interactor interactor) && interactor.LastHit.collider != null)
                {
                    clickPosition = interactor.LastHit.point;
                }

                // Find closest child hook point to where player clicked
                Transform closestHook = GetClosestHookPoint(clickPosition);
                SetActiveHookPoint(closestHook);

                WinchSystem.Instance.AttachRopeToCoffin(this, closestHook);
            }
        }

        public override void OnStartFocus()
        {
        }

        public override void OnFocus()
        {
        }

        public override void OnLoseFocus()
        {
        }

        public override void OnInteracting(FirstPersonController player)
        {
        }

        public override void OnInteractEnd(FirstPersonController player)
        {
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
