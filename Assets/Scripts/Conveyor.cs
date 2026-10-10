using UnityEngine;

// [RequireComponent(typeof(Rigidbody))]
public class Conveyor : MonoBehaviour
{
    [SerializeField] float beltSpeed = 5f;
    void OnCollisionStay(Collision c)
    {
        Rigidbody rb = c.rigidbody;
        if(rb == null) return;

        Vector3 n = c.GetContact(0).normal;
        Vector3 beltDir = Vector3.ProjectOnPlane(transform.right, n).normalized;

        float current = Vector3.Dot(rb.linearVelocity,beltDir);
        float missing = beltSpeed - current;

        rb.AddForce(beltDir * missing, ForceMode.VelocityChange);
    }
}