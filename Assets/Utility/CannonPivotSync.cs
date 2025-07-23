using Multisynq;
using UnityEngine;

public class CannonPivotSync : SynqBehaviour
{
    [SerializeField] private Transform cannonPivot; 
    [SynqVar] private float networkedZ = 0f;
    
    // Multisync compatibility properties
    public bool IsMine => true; // Placeholder for Multisync ownership

    void Update()
    {
        if (!IsMine)
        {
            Vector3 rot = cannonPivot.localEulerAngles;
            rot.z = Mathf.LerpAngle(rot.z, networkedZ, Time.deltaTime * 10f);
            cannonPivot.localEulerAngles = rot;
        }
        else
        {
            // Update networked value for owner
            networkedZ = cannonPivot.localEulerAngles.z;
        }
    }

    // Multisync handles synchronization automatically via [SynqVar]
    // No manual serialization needed
}
