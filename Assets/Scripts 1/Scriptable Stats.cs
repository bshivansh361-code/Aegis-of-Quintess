using UnityEngine;

namespace TarodevController
{
    [CreateAssetMenu(fileName = "PlayerStats", menuName = "Naboo/Player Stats")]
    public class ScriptableStats : ScriptableObject
    {
        [Header("LAYERS")]
        [Tooltip("Set this to the layer your player is on")]
        public LayerMask PlayerLayer;

        [Header("INPUT")]
        [Tooltip("Makes all Input snap to an integer. Prevents floating point movement input")]
        public bool SnapInput = true;

        [Tooltip("Minimum input required before you mount a ladder or auto-jump. Avoids drifty controllers")]
        public float VerticalDeadZoneThreshold = 0.3f;

        [Tooltip("Minimum input required before you move. Avoids drifty controllers")]
        public float HorizontalDeadZoneThreshold = 0.1f;

        [Header("MOVEMENT")]
        [Tooltip("Maximum movement speed")]
        public float MaxSpeed = 14;

        [Tooltip("How fast to reach max speed")]
        public float Acceleration = 120;

        [Tooltip("How fast to stop after letting go")]
        public float GroundDeceleration = 60;

        [Tooltip("How fast to stop while in the air")]
        public float AirDeceleration = 30;

        [Tooltip("A constant downward force applied while grounded. Helps on slopes")]
        public float GroundingForce = -1.5f;

        [Tooltip("The detection distance for grounding and roof detection")]
        public float GrounderDistance = 0.05f;

        [Header("JUMP")]
        [Tooltip("The immediate velocity applied when jumping")]
        public float JumpPower = 36;

        [Tooltip("The maximum vertical movement speed")]
        public float MaxFallSpeed = 40;

        [Tooltip("The player's capacity to gain fall speed")]
        public float FallAcceleration = 110;

        [Tooltip("The gravity multiplier added when jump is released early. (Makes short hops possible)")]
        public float JumpEndEarlyGravityModifier = 3;

        [Tooltip("The time before coyote jump becomes unusable. (Forgiveness if they jump late off a ledge)")]
        public float CoyoteTime = .15f;

        [Tooltip("The amount of time we buffer a jump. (Forgiveness if they press jump right before landing)")]
        public float JumpBuffer = .2f;
        [Header("DASH")]
        [Tooltip("How fast Naboo dashes")]
        public float DashVelocity = 30f;
        [Tooltip("How long the dash lasts (seconds)")]
        public float DashDuration = 0.15f;
        [Tooltip("Time between dashes")]
        public float DashCooldown = 0.8f;
        public int MaxJumps = 2;
        public float WallStickDuration = 0.5f;
        public float WallLockoutDuration = 0.5f;
      
    }
}