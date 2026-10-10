using UnityEngine;
 
public enum SurfaceType
{
    None = 0,
    Ground,
    WalkableSlope,
    UnwalkableSlope,
    Wall,
    Ceiling
}
 
// Turns a surface normal into a SurfaceType using the dot product with Vector3.up.
public class SurfaceClassifier
{
    private readonly SurfaceSettings settings;
 
    public SurfaceClassifier(SurfaceSettings settings)
    {
        this.settings = settings;
    }
 
    public SurfaceType Classify(Vector3 normal)
    {
        float dot = Vector3.Dot(normal.normalized, Vector3.up);
 
        if (Mathf.Abs(dot) <= settings.WallDot) return SurfaceType.Wall;   // near-vertical
        if (dot < 0f) return SurfaceType.Ceiling;                          // faces downward
        if (dot >= settings.FlatDot) return SurfaceType.Ground;
        if (dot >= settings.WalkableDot) return SurfaceType.WalkableSlope;
        return SurfaceType.UnwalkableSlope;
    }
}
 
// Direction helpers for a 2D-style (XY plane) game.
public static class SurfaceMath
{
    // Along the surface, pointing toward +X.
    public static Vector3 Tangent(Vector3 normal)
        => Flatten(Vector3.ProjectOnPlane(Vector3.right, normal), Vector3.right);
 
    // Along the surface, pointing downhill.
    public static Vector3 Downhill(Vector3 normal)
        => Flatten(Vector3.ProjectOnPlane(Vector3.down, normal), Vector3.down);
 
    private static Vector3 Flatten(Vector3 v, Vector3 fallback)
    {
        v.z = 0f;
        return v.sqrMagnitude > 1e-6f ? v.normalized : fallback;
    }
}
 