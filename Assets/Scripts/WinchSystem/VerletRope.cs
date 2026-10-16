using System.Collections.Generic;
using UnityEngine;

namespace FirstPersonSystem
{
    [RequireComponent(typeof(LineRenderer))]
    public class VerletRope : MonoBehaviour
    {
        public struct RopeNode
        {
            public Vector3 currentPos;
            public Vector3 oldPos;
            public Vector3 acceleration;

            public RopeNode(Vector3 pos)
            {
                currentPos = pos;
                oldPos = pos;
                acceleration = Vector3.zero;
            }
        }

        [Header("Chain / Rope Configuration")]
        [Tooltip("Lower node count gives a stiffer, heavier chain feel.")]
        [SerializeField] private int nodeCount = 14;
        [Tooltip("Higher iterations ensure no rubbery stretching.")]
        [SerializeField] private int constraintIterations = 16;
        [SerializeField] private float gravityScale = -4.5f;
        [SerializeField] private float damping = 0.88f;
        [SerializeField] private float chainStiffness = 0.15f;
        [SerializeField] private float nodeRadius = 0.05f;
        [SerializeField] private LayerMask collisionLayers;

        [Header("Tension Settings")]
        [SerializeField] private float tensionStiffness = 3000f;

        private Transform startPoint;
        private Transform endPoint;
        private float targetLength;
        private RopeNode[] nodes;
        private LineRenderer lineRenderer;
        private bool isInitialized = false;

        public float TargetLength => targetLength;
        public float CurrentSpanDistance => (startPoint != null && endPoint != null) ? Vector3.Distance(startPoint.position, endPoint.position) : 0f;
        public bool IsTaut => CurrentSpanDistance >= targetLength * 0.98f;

        private void Awake()
        {
            lineRenderer = GetComponent<LineRenderer>();
            lineRenderer.useWorldSpace = true;
            if (collisionLayers.value == 0)
            {
                collisionLayers = LayerMask.GetMask("Default");
            }
        }

        public void Initialize(Transform start, Transform end, float initialLength, Material ropeMaterial = null, float ropeWidth = 0.05f)
        {
            startPoint = start;
            endPoint = end;
            targetLength = Mathf.Max(initialLength, 0.5f);

            if (ropeMaterial != null)
            {
                lineRenderer.material = ropeMaterial;
            }
            lineRenderer.startWidth = ropeWidth;
            lineRenderer.endWidth = ropeWidth;

            nodes = new RopeNode[nodeCount];
            ResetNodesLinear();

            lineRenderer.positionCount = nodeCount;
            isInitialized = true;
        }

        public void ResetNodesLinear()
        {
            if (startPoint == null || endPoint == null) return;

            Vector3 startPos = startPoint.position;
            Vector3 endPos = endPoint.position;

            for (int i = 0; i < nodeCount; i++)
            {
                float t = (float)i / (nodeCount - 1);
                Vector3 pos = Vector3.Lerp(startPos, endPos, t);
                nodes[i] = new RopeNode(pos);
            }
        }

        public void SetEndPoint(Transform newEnd, bool snapNodes = true)
        {
            endPoint = newEnd;
            if (snapNodes && isInitialized)
            {
                ResetNodesLinear();
            }
        }

        public void SetRopeLength(float length)
        {
            targetLength = Mathf.Max(length, 0.5f);
        }

        public float CalculateTensionForce()
        {
            if (startPoint == null || endPoint == null || !isInitialized) return 0f;

            float distance = Vector3.Distance(startPoint.position, endPoint.position);
            if (distance > targetLength)
            {
                float stretch = distance - targetLength;
                return stretch * tensionStiffness;
            }
            return 0f;
        }

        public Vector3 GetDirectionToStart()
        {
            if (startPoint == null || endPoint == null) return Vector3.forward;
            return (startPoint.position - endPoint.position).normalized;
        }

        public Vector3 GetDirectionToEnd()
        {
            if (startPoint == null || endPoint == null) return Vector3.back;
            return (endPoint.position - startPoint.position).normalized;
        }

        private void FixedUpdate()
        {
            if (!isInitialized || startPoint == null || endPoint == null) return;

            SimulateVerlet();

            for (int i = 0; i < constraintIterations; i++)
            {
                ApplyDistanceConstraints();
                ApplyStiffnessConstraints();
                ApplyCollisionConstraints();
            }
        }

        private void SimulateVerlet()
        {
            Vector3 gravity = new Vector3(0f, gravityScale, 0f);
            float dt = Time.fixedDeltaTime;

            // Pin ends
            nodes[0].currentPos = startPoint.position;
            nodes[nodeCount - 1].currentPos = endPoint.position;

            for (int i = 1; i < nodeCount - 1; i++)
            {
                Vector3 velocity = (nodes[i].currentPos - nodes[i].oldPos) * damping;
                nodes[i].oldPos = nodes[i].currentPos;
                nodes[i].currentPos += velocity + gravity * (dt * dt);
            }
        }

        private void ApplyDistanceConstraints()
        {
            // Pin endpoints
            nodes[0].currentPos = startPoint.position;
            nodes[nodeCount - 1].currentPos = endPoint.position;

            float segmentLength = targetLength / (nodeCount - 1);

            for (int i = 0; i < nodeCount - 1; i++)
            {
                Vector3 delta = nodes[i + 1].currentPos - nodes[i].currentPos;
                float currentDist = delta.magnitude;
                if (currentDist < 0.0001f) continue;

                float error = currentDist - segmentLength;
                Vector3 correction = delta.normalized * error;

                if (i == 0)
                {
                    nodes[i + 1].currentPos -= correction;
                }
                else if (i + 1 == nodeCount - 1)
                {
                    nodes[i].currentPos += correction;
                }
                else
                {
                    nodes[i].currentPos += correction * 0.5f;
                    nodes[i + 1].currentPos -= correction * 0.5f;
                }
            }
        }

        private void ApplyStiffnessConstraints()
        {
            if (chainStiffness <= 0f) return;

            // Straightens chain segments toward adjacent neighbors to feel rigid like steel
            for (int i = 1; i < nodeCount - 1; i++)
            {
                Vector3 midPoint = (nodes[i - 1].currentPos + nodes[i + 1].currentPos) * 0.5f;
                nodes[i].currentPos = Vector3.Lerp(nodes[i].currentPos, midPoint, chainStiffness);
            }
        }

        private void ApplyCollisionConstraints()
        {
            for (int i = 1; i < nodeCount - 1; i++)
            {
                Collider[] colliders = Physics.OverlapSphere(nodes[i].currentPos, nodeRadius, collisionLayers, QueryTriggerInteraction.Ignore);
                foreach (var col in colliders)
                {
                    if (Physics.ComputePenetration(
                        col, col.transform.position, col.transform.rotation,
                        GetNodeSphereCollider(), nodes[i].currentPos, Quaternion.identity,
                        out Vector3 direction, out float distance))
                    {
                        nodes[i].currentPos += direction * (distance + 0.005f);
                    }
                }
            }
        }

        private static SphereCollider dummyCollider;
        private SphereCollider GetNodeSphereCollider()
        {
            if (dummyCollider == null)
            {
                GameObject dummy = new GameObject("VerletRopeDummyCollider") { hideFlags = HideFlags.HideAndDontSave };
                dummyCollider = dummy.AddComponent<SphereCollider>();
                dummyCollider.radius = nodeRadius;
            }
            return dummyCollider;
        }

        private void Update()
        {
            if (!isInitialized || nodes == null) return;

            for (int i = 0; i < nodeCount; i++)
            {
                lineRenderer.SetPosition(i, nodes[i].currentPos);
            }
        }
    }
}
