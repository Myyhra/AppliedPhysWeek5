using System;
using UnityEngine;

public class player : MonoBehaviour
{
    private BoxCollider playerCollider => GetComponent<BoxCollider>();
    [SerializeField] public float timeToApex = 0.35f;
    [SerializeField] public float timeToFall = .25f;
    [SerializeField] public float jumpHeight = 3;

    [SerializeField] private float maxSpeed = 8;
    [SerializeField] private float maxSpeedOnAir = 4;

    [SerializeField] private float timeToReachMax = .3f;

    [SerializeField] private float timeToStop = .1f;

    [SerializeField] private float turnSpeed = .5f;

    [SerializeField] private float turnSpeedScalar = 2;

    float acceleration => (isGrounded ? maxSpeed : maxSpeedOnAir) / timeToReachMax;
    float deceleration => (isGrounded ? maxSpeed : maxSpeedOnAir) / timeToStop;

    float turnRate => (maxSpeed * turnSpeedScalar) / turnSpeed;

    float riseG => 2f * jumpHeight / (timeToApex * timeToApex);
    float fallG => 2f * jumpHeight / (timeToFall * timeToFall);
    float jumpVelocity => 2f * jumpHeight / timeToApex;

    Vector3 velocity = Vector3.zero;

    private float groundPos;
    private bool isGrounded = false;

    private RaycastHit[] hitThisFrame = new RaycastHit[10];
    private RaycastHit[] hitLastFrame = new RaycastHit[10];

    Vector3 direction;

    float YGroundPoint = 0f;
    [SerializeField] private float groundSnapDistance = 0.15f;
    [SerializeField] private float groundSkin = 0.02f;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Application.targetFrameRate = 60;
        groundPos = transform.position.y;
    }

    // Update is called once per frame
    void Update()
    {
        HorizontalMovement();

        VerticalMovement();



        transform.position += velocity * Time.deltaTime;
    }

    private void VerticalMovement()
    {
        if (isGrounded && Input.GetButtonDown("Jump"))
        {
            isGrounded = false;
            velocity.y = jumpVelocity;
        }

        if (!isGrounded)
        {
            velocity.y -= (velocity.y < 0 ? fallG : riseG) * Time.deltaTime;
            // velocity.y -= 9.31f * Time.deltaTime;
        }

        if (isGrounded)
        {
            // velocity.y = direction.y + YGroundPoint; //FIX IT SOON (idk maybe like push player upward until the collider is at the end of the ray?)
            // velocity.y = 0f;                                   // or switch using rays for position to capsule collider if thats allowed
                                                               //hit.point.y
        }
    }


    private void HorizontalMovement()
    {
        float input = Input.GetAxisRaw("Horizontal");
        float targetSpeed = maxSpeed * input;
        float targetAccel = ChooseAccel(input);
        YGroundPoint = 0f;
        var downNormal = Vector3.zero;
        var rightNormal = Vector3.zero;
        var RayDownHitType = RayCastDownRay(out downNormal, out YGroundPoint);
        var RayRightHitType = RaycastRight(out rightNormal);
        Debug.Log($"HorizontalMovement {RayDownHitType}: {downNormal}");

        switch (RayDownHitType)
        {
            case HitType.WalkableSlope:
                {
                    isGrounded = true;
                    direction = Vector3.ProjectOnPlane(Vector3.right, downNormal) * targetSpeed;
                    // velocity.y = YGroundPoint;
                    velocity = Vector3.MoveTowards(velocity, direction, targetAccel * Time.deltaTime);
                    Debug.Log($"WalkableSlope {direction} : {velocity}");
                    break;
                }
            case HitType.Ground:
                {
                    isGrounded = true;
                    direction = Vector3.ProjectOnPlane(Vector3.right, downNormal) * targetSpeed;
                    // velocity.y = YGroundPoint;
                    velocity = Vector3.MoveTowards(velocity, direction, targetAccel * Time.deltaTime);
                    break;
                }
            default:
                {
                    isGrounded = false;
                    break;
                }

        }

        switch (RayRightHitType)
        {
            case HitType.Wall:
                {
                    break;
                }
        }

        // if (RayDownHitType == HitType.Air) isGrounded = false;
    }

    float ChooseAccel(float input)
    {
        bool isTurning = !Mathf.Approximately(Mathf.Sign(input), Mathf.Sign(velocity.x));
        //Debug.Log($"{input}, {isTurning}");
        if (Mathf.Abs(input) > 0)
        {
            if (!isTurning)
                return acceleration;

            return turnRate;
        }
        else
            return deceleration;
    }


    private HitType RayCastDownRay(out Vector3 normal, out float YhitPoint)
    {
        Ray ray = new Ray(transform.position, Vector3.down);
        String debugText = $"Normal RaycastDown collided with:";
        RaycastHit hit;
        var hitray = Physics.Raycast(ray, out hit, 1.5f);
        if (hitray)
        {
            normal = hit.normal;
            YhitPoint = hit.point.y;





            var dot = Vector3.Dot(hit.normal, Vector3.up);
            var angle = Mathf.Acos(dot) * Mathf.Rad2Deg;
            debugText += $"Ground: {hit.collider.name} Dot: {dot} Angle: {angle}\n";
            var lerpedAngle = Mathf.InverseLerp(0, 45, angle);
            var lerpedSpeed = Mathf.Lerp(3, 5, lerpedAngle);
            if (angle == 0)
            {
                return HitType.Ground;
            }
            else if (angle <= 45)
            {
                return HitType.WalkableSlope;
            }
            else
            {
                return HitType.Air;
            }
        }

        normal = Vector3.zero;
        YhitPoint = 0f;
        return HitType.Air;
    }


    private HitType RaycastRight(out Vector3 normal)
    {
        Ray ray = new Ray(transform.position, Vector3.right);
        var hits = Physics.RaycastNonAlloc(ray, hitThisFrame, 3f);
        if (hits > 0)
        {
            var text = "Collided with ";
            foreach (var hit in hitThisFrame)
            {
                if (hit.collider == null) continue;
                var dot = Vector3.Dot(hit.normal, Vector3.up);
                var angle = Mathf.Acos(dot) * Mathf.Rad2Deg;
                text += $"Wall: {hit.collider.name} Dot: {dot} Angle: {angle}\n";
                normal = hit.normal;
                var lerpedAngle = Mathf.InverseLerp(0, 45, angle);
                var lerpedSpeed = Mathf.Lerp(3, 5, lerpedAngle);
                if (angle <= 15 || angle >= 345)
                {
                    return HitType.Wall;
                }
                else
                {
                    return HitType.Air;
                }


            }

            Debug.Log($"{text}");
            hitLastFrame = hitThisFrame;

        }

        normal = Vector3.zero;
        return HitType.Air;
    }

    enum HitType
    {
        Ground = 0,
        Air = 1,
        Slope = 2,
        WalkableSlope = 3,
        Wall = 4
    }
}
