using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class testplayer : MonoBehaviour
{
    [Header("Jump")]
    [SerializeField] public float timeToApex = 0.35f;
    [SerializeField] public float timeToFall = .25f;
    [SerializeField] public float jumpHeight = 3;

    [Header("Run")]
    [SerializeField] private float maxSpeed = 8;
    [SerializeField] private float maxSpeedOnAir = 4;   // cap for air steering only; never slows you down
    [SerializeField] private float timeToReachMax = .3f;
    [SerializeField] private float timeToStop = .1f;
    [SerializeField] private float turnSpeed = .5f;
    [SerializeField] private float turnSpeedScalar = 2;

    [Header("Ground")]
    [SerializeField] private float groundSnapDistance = 0.3f;  // only used while already grounded
    [SerializeField] private float groundSkin = 0.02f;
    [SerializeField] private float maxSlopeAngle = 45f;
    [SerializeField] private LayerMask groundMask = ~0;

    // Ground-only rates
    float acceleration => maxSpeed / timeToReachMax;
    float deceleration => maxSpeed / timeToStop;
    float turnRate => (maxSpeed * turnSpeedScalar) / turnSpeed;
    float airAcceleration => maxSpeedOnAir / timeToReachMax;

    float riseG => 2f * jumpHeight / (timeToApex * timeToApex);
    float fallG => 2f * jumpHeight / (timeToFall * timeToFall);
    float jumpVelocity => 2f * jumpHeight / timeToApex;

    // Cached collider measurements (world units)
    private float halfHeight, halfWidth, centerOffsetX, centerOffsetY, feetOffsetY;

    // On the ground: speed along the surface. In the air: speed along X.
    private float horizontalSpeed;
    private float verticalVelocity;
    private bool isGrounded;
    private Vector3 groundNormal = Vector3.up;
    private float lastTangentX = 1f;

    void Awake()
    {
        var col = GetComponent<BoxCollider>();
        Vector3 s = transform.lossyScale;
        halfHeight = col.size.y * s.y * 0.5f;
        halfWidth = col.size.x * s.x * 0.5f;
        centerOffsetX = col.center.x * s.x;
        centerOffsetY = col.center.y * s.y;
        feetOffsetY = halfHeight - centerOffsetY;
    }

    void Start()
    {
        Application.targetFrameRate = 60;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        bool wasGrounded = isGrounded;

        // 1. Ground check. Skipped while rising so the jump isn't cancelled.
        //    Big snap distance only if we were already grounded; in the air just a small tolerance.
        isGrounded = false;
        groundNormal = Vector3.up;
        float probe = wasGrounded ? groundSnapDistance : groundSkin * 2f;
        if (verticalVelocity <= 0f && GroundCheck(probe, out _, out Vector3 n))
        {
            isGrounded = true;
            groundNormal = n;
        }

        Vector3 tangent = Vector3.right;
        if (isGrounded)
            tangent = Vector3.ProjectOnPlane(Vector3.right, groundNormal).normalized;

        // 2. Jump
        if (isGrounded && Input.GetButtonDown("Jump"))
        {
            isGrounded = false;
            verticalVelocity = jumpVelocity;
        }

        // 3. Keep momentum across takeoff / landing (surface speed <-> X speed)
        if (wasGrounded && !isGrounded)
            horizontalSpeed *= lastTangentX;
        else if (!wasGrounded && isGrounded)
            horizontalSpeed /= tangent.x;
        if (isGrounded)
            lastTangentX = tangent.x;

        // 4. Horizontal speed
        float input = Input.GetAxisRaw("Horizontal");
        if (isGrounded)
            horizontalSpeed = Mathf.MoveTowards(horizontalSpeed, input * maxSpeed, ChooseAccel(input) * dt);
        else
            AirControl(input, dt);

        // 5. Vertical speed
        if (isGrounded)
            verticalVelocity = 0f;
        else
            verticalVelocity -= (verticalVelocity < 0f ? fallG : riseG) * dt;

        // 6. Move
        Vector3 move;
        if (isGrounded)
        {
            move = tangent * (horizontalSpeed * dt);          // along the surface
            Debug.DrawRay(transform.position, tangent * 2f, Color.green);
        }
        else
        {
            move = new Vector3(horizontalSpeed, verticalVelocity, 0f) * dt;
        }
        transform.position += move;

        // 7. Snap to the surface after moving
        if (verticalVelocity <= 0f)
        {
            float snapProbe = isGrounded ? groundSnapDistance : groundSkin * 2f;
            if (GroundCheck(snapProbe, out float groundY, out _))
            {
                Vector3 p = transform.position;
                p.y = groundY + feetOffsetY + groundSkin;
                transform.position = p;
            }
        }
    }

    // Steering only: never pulls you below a speed you already have.
    void AirControl(float input, float dt)
    {
        if (Mathf.Abs(input) < 0.01f) return;   // no input = keep momentum

        bool sameDir = Mathf.Sign(input) == Mathf.Sign(horizontalSpeed);
        if (sameDir && Mathf.Abs(horizontalSpeed) >= maxSpeedOnAir) return;   // already at/over cap

        horizontalSpeed = Mathf.MoveTowards(horizontalSpeed, input * maxSpeedOnAir, airAcceleration * dt);
    }

    // Three rays (left / center / right of the box). Highest valid contact wins,
    // so the box rides on its highest corner and never clips into slopes.
    bool GroundCheck(float extra, out float groundY, out Vector3 normal)
    {
        groundY = float.MinValue;
        normal = Vector3.up;
        bool found = false;

        Vector3 center = transform.position + new Vector3(centerOffsetX, centerOffsetY, 0f);
        float length = halfHeight + extra;

        for (int i = -1; i <= 1; i++)
        {
            Vector3 origin = center + Vector3.right * (i * halfWidth * 0.9f);
            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, length, groundMask, QueryTriggerInteraction.Ignore))
                continue;
            if (Vector3.Angle(hit.normal, Vector3.up) > maxSlopeAngle)
                continue;

            if (hit.point.y > groundY)
            {
                groundY = hit.point.y;
                normal = hit.normal;
                found = true;
            }
        }
        return found;
    }

    float ChooseAccel(float input)
    {
        if (Mathf.Abs(input) < 0.01f)
            return deceleration;

        bool isTurning = Mathf.Abs(horizontalSpeed) > 0.01f &&
                         Mathf.Sign(input) != Mathf.Sign(horizontalSpeed);

        return isTurning ? turnRate : acceleration;
    }
}