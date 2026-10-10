using UnityEngine;

public readonly struct SurfaceHit
{
    public readonly SurfaceType Type;
    public readonly Vector3 Normal;
    public readonly Vector3 Point;
    public readonly float Distance;

    public SurfaceHit(SurfaceType type, Vector3 normal, Vector3 point, float distance)
    {
        Type = type;
        Normal = normal;
        Point = point;
        Distance = distance;
    }

    public bool IsHit => Type != SurfaceType.None;
    public static SurfaceHit None => new SurfaceHit(SurfaceType.None, Vector3.up, Vector3.zero, 0f);
}

// World-space measurements of the box, cached once so we never depend on collider.bounds.
public readonly struct BodyDimensions
{
    public readonly float HalfWidth;
    public readonly float HalfHeight;
    public readonly Vector3 CenterOffset;   // collider center relative to transform.position

    public BodyDimensions(BoxCollider box)
    {
        Vector3 scale = box.transform.lossyScale;
        HalfWidth = box.size.x * scale.x * 0.5f;
        HalfHeight = box.size.y * scale.y * 0.5f;
        CenterOffset = Vector3.Scale(box.center, scale);
    }

    public float FeetOffsetY => HalfHeight - CenterOffset.y;   // transform.y minus the bottom of the box
}

// Everything that touches Physics lives here. It knows nothing about player movement.
public class SurfaceProbe
{
    private const float LaneReach = 0.9f;                      // how close to the box edge the outer rays sit
    private static readonly float[] Lanes = { -1f, 0f, 1f };

    private readonly Transform body;
    private readonly BodyDimensions size;
    private readonly SurfaceSettings settings;
    private readonly SurfaceClassifier classifier;

    public SurfaceProbe(Transform body, BodyDimensions size, SurfaceSettings settings, SurfaceClassifier classifier)
    {
        this.body = body;
        this.size = size;
        this.settings = settings;
        this.classifier = classifier;
    }

    private Vector3 Center => body.position + size.CenterOffset;

    // Highest supporting surface under the box (left / center / right rays).
    public SurfaceHit Ground(float extraDistance)
    {
        SurfaceHit best = SurfaceHit.None;
        float length = size.HalfHeight + extraDistance;

        foreach (float lane in Lanes)
        {
            Vector3 origin = Center + Vector3.right * (lane * size.HalfWidth * LaneReach);
            SurfaceHit hit = Cast(origin, Vector3.down, length);

            if (!IsSupport(hit.Type)) continue;
            if (!best.IsHit || hit.Point.y > best.Point.y) best = hit;
        }
        return best;
    }

    // How far the box can move sideways (up to 'travel') before touching a blocking surface.
    public float SideClearance(float directionX, float travel)
    {
        Vector3 direction = new Vector3(Mathf.Sign(directionX), 0f, 0f);
        float reach = size.HalfWidth + settings.wallSkin;
        float clearance = travel;

        foreach (float lane in Lanes)
        {
            Vector3 origin = Center + Vector3.up * (lane * size.HalfHeight * LaneReach);
            SurfaceHit hit = Cast(origin, direction, reach + travel);

            if (!BlocksSideways(hit.Type)) continue;
            clearance = Mathf.Min(clearance, Mathf.Max(0f, hit.Distance - reach));
        }
        return clearance;
    }

    // How far the box can move upward (up to 'travel') before bumping a ceiling.
    public float UpClearance(float travel)
    {
        float reach = size.HalfHeight + settings.groundSkin;
        float clearance = travel;

        foreach (float lane in Lanes)
        {
            Vector3 origin = Center + Vector3.right * (lane * size.HalfWidth * LaneReach);
            SurfaceHit hit = Cast(origin, Vector3.up, reach + travel);

            if (hit.Type != SurfaceType.Ceiling && hit.Type != SurfaceType.Wall) continue;
            clearance = Mathf.Min(clearance, Mathf.Max(0f, hit.Distance - reach));
        }
        return clearance;
    }

    private SurfaceHit Cast(Vector3 origin, Vector3 direction, float length)
    {
        if (!Physics.Raycast(origin, direction, out RaycastHit hit, length,
                             settings.groundMask, QueryTriggerInteraction.Ignore))
            return SurfaceHit.None;

        return new SurfaceHit(classifier.Classify(hit.normal), hit.normal, hit.point, hit.distance);
    }

    private static bool IsSupport(SurfaceType type)
        => type == SurfaceType.Ground
        || type == SurfaceType.WalkableSlope
        || type == SurfaceType.UnwalkableSlope;

    private static bool BlocksSideways(SurfaceType type)
        => type == SurfaceType.Wall
        || type == SurfaceType.Ceiling
        || type == SurfaceType.UnwalkableSlope;
}
