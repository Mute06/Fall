using UnityEngine;

namespace FirstPersonSystem
{
    public class AnchorPoint : MonoBehaviour
    {
        [SerializeField] private Transform ropeAttachPoint;
        public Transform RopeAttachPoint => ropeAttachPoint != null ? ropeAttachPoint : transform;

        public Vector3 Normal { get; private set; }

        public void Initialize(Vector3 position, Vector3 normal, Transform parent = null)
        {
            transform.position = position;
            transform.rotation = Quaternion.LookRotation(normal);
            Normal = normal;

            if (parent != null)
            {
                transform.SetParent(parent, true);
            }

            if (ropeAttachPoint == null)
            {
                GameObject attachObj = new GameObject("AttachPoint");
                attachObj.transform.SetParent(transform);
                attachObj.transform.localPosition = Vector3.forward * 0.05f;
                ropeAttachPoint = attachObj.transform;
            }
        }

        public void DestroyAnchor()
        {
            Destroy(gameObject);
        }
    }
}
