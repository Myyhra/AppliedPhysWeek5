using System;
using UnityEngine;
 
[Serializable]
public class JumpSettings
{
    public float timeToApex = 0.35f;
    public float timeToFall = 0.25f;
    public float jumpHeight = 3f;
 
    public float RiseGravity => 2f * jumpHeight / (timeToApex * timeToApex);
    public float FallGravity => 2f * jumpHeight / (timeToFall * timeToFall);
    public float JumpVelocity => 2f * jumpHeight / timeToApex;
}
 
[Serializable]
public class RunSettings
{
    public float maxSpeed = 8f;
    public float maxSpeedOnAir = 4f;        // cap for air steering only; never slows you down
    public float timeToReachMax = 0.3f;
    public float timeToStop = 0.1f;
    public float turnSpeed = 0.5f;
    public float turnSpeedScalar = 2f;
 
    public float Acceleration => maxSpeed / timeToReachMax;
    public float Deceleration => maxSpeed / timeToStop;
    public float TurnRate => maxSpeed * turnSpeedScalar / turnSpeed;
    public float AirAcceleration => maxSpeedOnAir / timeToReachMax;
}
 
[Serializable]
public class SlideSettings
{
    public float maxSpeed = 10f;
    public float timeToReachMax = 0.5f;
 
    public float Acceleration => maxSpeed / timeToReachMax;
}
 
[Serializable]
public class SurfaceSettings
{
    public LayerMask groundMask = ~0;
 
    [Header("Thresholds (degrees)")]
    public float flatAngle = 1f;             // at or below this, a surface counts as flat ground
    public float maxWalkableAngle = 45f;     // above this, a slope is unwalkable (slide)
    public float wallAngleTolerance = 10f;   // how far from vertical a surface still counts as a wall
 
    [Header("Contact")]
    public float groundSnapDistance = 0.3f;  // used while already on the ground
    public float groundSkin = 0.02f;
    public float wallSkin = 0.02f;
 
    // Compared against Vector3.Dot(normal, Vector3.up)
    public float FlatDot => Mathf.Cos(flatAngle * Mathf.Deg2Rad);
    public float WalkableDot => Mathf.Cos(maxWalkableAngle * Mathf.Deg2Rad);
    public float WallDot => Mathf.Sin(wallAngleTolerance * Mathf.Deg2Rad);
}
 