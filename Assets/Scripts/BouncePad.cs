using UnityEngine;

// [RequireComponent(typeof(Rigidbody))]
public class BouncePad : MonoBehaviour
{
    [SerializeField] float BounceImpulse = 20f;
    void OnTriggerEnter(Collider c)
    {
        if (!c.CompareTag("CompanionCube")) return;

        c.attachedRigidbody.AddForce(Vector3.up * BounceImpulse, ForceMode.Impulse);
    }
}