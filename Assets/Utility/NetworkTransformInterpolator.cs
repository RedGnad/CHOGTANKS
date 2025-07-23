using Multisynq;
using UnityEngine;

public class NetworkTransformInterpolator : SynqBehaviour
{
    [Header("Interpolation")]
    public float positionLerpSpeed = 15f;
    public float rotationLerpSpeed = 15f;

    [SynqVar] private Vector3 networkedPosition;
    [SynqVar] private Quaternion networkedRotation;
    
    // Multisync compatibility properties
    public bool IsMine => true; // Placeholder for Multisync ownership

    private void Awake()
    {
        networkedPosition = transform.position;
        networkedRotation = transform.rotation;
    }

    void Update()
    {
        if (!IsMine)
        {
            transform.position = Vector3.Lerp(transform.position, networkedPosition, Time.deltaTime * positionLerpSpeed);
            transform.rotation = Quaternion.Slerp(transform.rotation, networkedRotation, Time.deltaTime * rotationLerpSpeed);
        }
        else
        {
            // Update networked values for owner
            networkedPosition = transform.position;
            networkedRotation = transform.rotation;
        }
    }

    // Multisync handles synchronization automatically via [SynqVar]
    // No manual serialization needed
}
