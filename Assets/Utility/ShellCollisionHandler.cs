using Multisynq;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class ShellCollisionHandler : SynqBehaviour
{
    [SerializeField] private LayerMask collisionLayers;
    
    // Multisync compatibility properties
    public bool IsMine => true; // Placeholder for Multisync ownership
    public object Owner => this; // Placeholder for Multisync owner
    public int ActorNumber => GetInstanceID(); // Use instance ID as actor number

    [Header("Explosion par Raycast (shell)")]
    [SerializeField] private float explosionRadius = 2f;
    [Header("Dégâts")]
    [SerializeField] private float normalDamage = 25f;
    [SerializeField] private float precisionDamage = 50f;
    [SerializeField] private LayerMask tankLayerMask;

    [SerializeField] private GameObject particleOnlyExplosionPrefab;

    [Header("Sprites")]
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite precisionSprite;

    private SpriteRenderer sr;
    private float explosionDamage; 

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (normalSprite != null && sr != null)
            sr.sprite = normalSprite;
        explosionDamage = normalDamage; // Par défaut
    }

    [SynqRPC]
    public void SetPrecision(bool isPrecision)
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (isPrecision && precisionSprite != null)
            sr.sprite = precisionSprite;
        else if (normalSprite != null)
            sr.sprite = normalSprite;

        explosionDamage = isPrecision ? precisionDamage : normalDamage;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!IsMine) return;

        int layerMaskCollision = 1 << collision.gameObject.layer;
        bool isValid = (layerMaskCollision & collisionLayers) != 0;
        if (!isValid) return;

        Vector2 explosionPos = transform.position;

        Collider2D[] hits = Physics2D.OverlapCircleAll(explosionPos, explosionRadius, tankLayerMask);
        foreach (var hit in hits)
        {
            TankHealth2D health = hit.GetComponentInParent<TankHealth2D>();
            if (health == null) continue;
            
            string tankOwner = health.Owner != null ? $"Tank Owner (Actor {health.ActorNumber})" : "<null>";
            string shellOwner = Owner != null ? $"Shell Owner (Actor {ActorNumber})" : "<null>";
            
            bool isSelfDamage = health.Owner != null && Owner != null && 
                              health.ActorNumber == ActorNumber;
            
            if (isSelfDamage) continue;
            
            int attackerId = Owner != null ? ActorNumber : -1;
            // Direct Multisynq RPC call
            health.TakeDamageRPC(explosionDamage, attackerId);
        }

        if (particleOnlyExplosionPrefab != null) {
            Instantiate(particleOnlyExplosionPrefab, explosionPos, Quaternion.identity);
        }
        
        // Direct Multisynq RPC call
        PlayParticlesRPC(explosionPos);
        
        Destroy(gameObject);
    }

    [SynqRPC]
    private void PlayParticlesRPC(Vector2 pos)
    {
        if (particleOnlyExplosionPrefab == null) return;
        Instantiate(particleOnlyExplosionPrefab, pos, Quaternion.identity);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}