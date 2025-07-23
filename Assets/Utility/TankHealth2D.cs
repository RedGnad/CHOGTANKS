using Multisynq;
using UnityEngine;
using System.Linq;
using System.Collections;

public class TankHealth2D : SynqBehaviour
{
    [Header("Paramètres de santé")]
    [SerializeField] private float maxHealth = 100f;

    [SynqVar] public float currentHealth = 100f;
    [SynqVar] public bool _isDead = false;
    [SynqVar] public int lastDamageDealer = -1;
    
    // Multisync compatibility properties
    public object Owner => this; // Placeholder for Multisync owner
    public int ActorNumber => GetInstanceID(); // Use instance ID as actor number
    public bool IsMine => true; // Placeholder for Multisync ownership
    
    public float CurrentHealth => currentHealth;
    public bool IsDead => _isDead;
    
    public void ResetHealth()
    {
        currentHealth = maxHealth;
        _isDead = false;
    }

    private void Start()
    {
        if (IsMine)
        {
            currentHealth = maxHealth;
            _isDead = false;
        }
    }

    private void EnableInputs()
    {
        var move = GetComponent<TankMovement2D>();
        if (move != null) move.enabled = true;
        var shoot = GetComponent<TankShoot2D>();
        if (shoot != null) shoot.enabled = true;
    }

    [SynqRPC]
    public void TakeDamageRPC(float amount, int damageDealer)
    {
        if (_isDead) return;
        
        lastDamageDealer = damageDealer;
        currentHealth = Mathf.Max(0f, currentHealth - amount);
        
        
        if (currentHealth <= 0 && !_isDead)
        {
            _isDead = true; // Marquer comme mort immédiatement
            
            SimpleTankRespawn respawnHandler = GetComponent<SimpleTankRespawn>();
            if (respawnHandler != null)
            {
                // Use Multisync RPC directly
                respawnHandler.Die(damageDealer);
            }
            else
            {
                Debug.LogError("[TankHealth2D] Impossible d'appeler Die: SimpleTankRespawn non trouvé!");
            }
        }
    }
}