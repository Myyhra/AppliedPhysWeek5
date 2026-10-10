using UnityEngine;
 
[RequireComponent(typeof(BoxCollider))]
public class CLplayer : MonoBehaviour
{
    [SerializeField] private JumpSettings jump = new JumpSettings();
    [SerializeField] private RunSettings run = new RunSettings();
    [SerializeField] private SlideSettings slide = new SlideSettings();
    [SerializeField] private SurfaceSettings surface = new SurfaceSettings();
 
    private enum MoveMode { Airborne, Grounded, Sliding }
 
    private const float MinTangentX = 0.25f;   // avoids dividing by ~0 on very steep walkable slopes
 
    private SurfaceProbe probe;
    private float feetOffsetY;
 
    private MoveMode mode = MoveMode.Airborne;
    private Vector3 velocity;     // true world velocity (rebuilt every tick on ground / slide)
    private float surfaceSpeed;   // speed along the surface while Grounded (signed) or Sliding (downhill)
 
    // ----------------------------------------------------------------- setup
 
    void Awake()
    {
        var size = new BodyDimensions(GetComponent<BoxCollider>());
        probe = new SurfaceProbe(transform, size, surface, new SurfaceClassifier(surface));
        feetOffsetY = size.FeetOffsetY;
    }
 
    void Start()
    {
        Application.targetFrameRate = 60;
    }
 
    // ----------------------------------------------------------------- frame
 
    void Update()
    {
        float dt = Time.deltaTime;
 
        SurfaceHit ground = SenseGround();
        ChangeMode(ModeFor(ground), ground);
 
        if (mode == MoveMode.Grounded && JumpPressed())
            StartJump(ground);
 
        switch (mode)
        {
            case MoveMode.Grounded: TickGrounded(ground, dt); break;
            case MoveMode.Sliding:  TickSliding(ground, dt);  break;
            default:                TickAirborne(dt);         break;
        }
    }
 
    // ----------------------------------------------------------------- modes
 
    private MoveMode ModeFor(SurfaceHit ground)
    {
        switch (ground.Type)
        {
            case SurfaceType.Ground:
            case SurfaceType.WalkableSlope:
                return MoveMode.Grounded;
 
            case SurfaceType.UnwalkableSlope:
                return MoveMode.Sliding;
 
            default:
                return MoveMode.Airborne;
        }
    }
 
    // Converts momentum between modes so nothing halts on takeoff / landing.
    private void ChangeMode(MoveMode next, SurfaceHit ground)
    {
        if (next == mode) return;
 
        MoveMode previous = mode;
        mode = next;
 
        switch (next)
        {
            case MoveMode.Grounded:
                // keep the horizontal (X) speed we arrived with
                float tangentX = Mathf.Max(SurfaceMath.Tangent(ground.Normal).x, MinTangentX);
                surfaceSpeed = velocity.x / tangentX;
                break;
 
            case MoveMode.Sliding:
                // keep whatever speed already points downhill
                Vector3 downhill = SurfaceMath.Downhill(ground.Normal);
                surfaceSpeed = Mathf.Max(0f, Vector3.Dot(velocity, downhill));
                break;
 
            case MoveMode.Airborne:
                // walking off a ledge shouldn't launch you upward; sliding off keeps its fall
                if (previous == MoveMode.Grounded)
                    velocity.y = Mathf.Min(velocity.y, 0f);
                break;
        }
    }
 
    private void StartJump(SurfaceHit ground)
    {
        ChangeMode(MoveMode.Airborne, ground);
        velocity.y = jump.JumpVelocity;
    }
 
    // ----------------------------------------------------------------- ticks
 
    private void TickGrounded(SurfaceHit ground, float dt)
    {
        float input = ReadMoveInput();
        surfaceSpeed = Mathf.MoveTowards(surfaceSpeed, input * run.maxSpeed, GroundRate(input) * dt);
 
        Vector3 tangent = SurfaceMath.Tangent(ground.Normal);
        velocity = tangent * surfaceSpeed;
        Debug.DrawRay(transform.position, tangent * 2f, Color.green);
 
        Move(velocity * dt);
    }
 
    private void TickSliding(SurfaceHit ground, float dt)
    {
        surfaceSpeed = Mathf.MoveTowards(surfaceSpeed, slide.maxSpeed, slide.Acceleration * dt);
        velocity = SurfaceMath.Downhill(ground.Normal) * surfaceSpeed;
 
        Move(velocity * dt);
    }
 
    private void TickAirborne(float dt)
    {
        velocity.x = SteerInAir(velocity.x, ReadMoveInput(), dt);
        velocity.y -= (velocity.y < 0f ? jump.FallGravity : jump.RiseGravity) * dt;
 
        Move(velocity * dt);
    }
 
    // ----------------------------------------------------------------- speed rules
 
    private float GroundRate(float input)
    {
        if (Mathf.Abs(input) < 0.01f)
            return run.Deceleration;
 
        bool turning = Mathf.Abs(surfaceSpeed) > 0.01f && Mathf.Sign(input) != Mathf.Sign(surfaceSpeed);
        return turning ? run.TurnRate : run.Acceleration;
    }
 
    // Steering only: never pulls you below a speed you already have.
    private float SteerInAir(float speed, float input, float dt)
    {
        if (Mathf.Abs(input) < 0.01f) return speed;   // no input = keep momentum
 
        bool sameDirection = Mathf.Sign(input) == Mathf.Sign(speed);
        if (sameDirection && Mathf.Abs(speed) >= run.maxSpeedOnAir) return speed;
 
        return Mathf.MoveTowards(speed, input * run.maxSpeedOnAir, run.AirAcceleration * dt);
    }
 
    // ----------------------------------------------------------------- movement + collision
 
    private void Move(Vector3 delta)
    {
        delta = BlockAtWalls(delta);
        delta = BlockAtCeiling(delta);
 
        transform.position += delta;
        SnapToGround();
    }
 
    private Vector3 BlockAtWalls(Vector3 delta)
    {
        float travel = Mathf.Abs(delta.x);
        if (travel < Mathf.Epsilon) return delta;
 
        float clearance = probe.SideClearance(delta.x, travel);
        if (clearance >= travel) return delta;
 
        delta.x = Mathf.Sign(delta.x) * clearance;
        velocity.x = 0f;
        surfaceSpeed = 0f;
        return delta;
    }
 
    private Vector3 BlockAtCeiling(Vector3 delta)
    {
        if (delta.y <= 0f) return delta;
 
        float clearance = probe.UpClearance(delta.y);
        if (clearance >= delta.y) return delta;
 
        delta.y = clearance;
        velocity.y = 0f;
        return delta;
    }
 
    private void SnapToGround()
    {
        if (IsRisingInAir) return;
 
        SurfaceHit ground = probe.Ground(CurrentSnapDistance);
        if (!ground.IsHit) return;
 
        Vector3 p = transform.position;
        p.y = ground.Point.y + feetOffsetY + surface.groundSkin;
        transform.position = p;
    }
 
    // Skipped while rising so the ground check can't cancel a jump.
    private SurfaceHit SenseGround()
        => IsRisingInAir ? SurfaceHit.None : probe.Ground(CurrentSnapDistance);
 
    private bool IsRisingInAir => mode == MoveMode.Airborne && velocity.y > 0f;
 
    // Big snap distance only when already on a surface; in the air just a small tolerance.
    private float CurrentSnapDistance
        => mode == MoveMode.Airborne ? surface.groundSkin * 2f : surface.groundSnapDistance;
 
    // ----------------------------------------------------------------- input (swap here for the new Input System)
 
    private float ReadMoveInput() => Input.GetAxisRaw("Horizontal");
    private bool JumpPressed() => Input.GetButtonDown("Jump");
}
 