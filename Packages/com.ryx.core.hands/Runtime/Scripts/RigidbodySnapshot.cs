using UnityEngine;

namespace Ryx.Core.Hands
{
    public readonly struct RigidbodySnapshot
    {
        public readonly float Mass;
        public readonly float LinearDamping;
        public readonly float AngularDamping;
        public readonly bool UseGravity;
        public readonly bool IsKinematic;
        public readonly RigidbodyInterpolation Interpolation;
        public readonly CollisionDetectionMode CollisionDetection;
        public readonly RigidbodyConstraints Constraints;

        private RigidbodySnapshot(Rigidbody rb)
        {
            Mass = rb.mass;
            LinearDamping = rb.linearDamping;
            AngularDamping = rb.angularDamping;
            UseGravity = rb.useGravity;
            IsKinematic = rb.isKinematic;
            Interpolation = rb.interpolation;
            CollisionDetection = rb.collisionDetectionMode;
            Constraints = rb.constraints;
        }

        public static RigidbodySnapshot? Capture(Rigidbody rb)
        {
            return rb ? new RigidbodySnapshot(rb) : null;
        }

        public void ApplyTo(Rigidbody rb)
        {
            rb.mass = Mass;
            rb.linearDamping = LinearDamping;
            rb.angularDamping = AngularDamping;
            rb.useGravity = UseGravity;
            rb.isKinematic = IsKinematic;
            rb.interpolation = Interpolation;
            rb.collisionDetectionMode = CollisionDetection;
            rb.constraints = Constraints;
        }
    }
}