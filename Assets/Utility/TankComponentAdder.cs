using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Multisynq;

[DefaultExecutionOrder(-1000)] // Priorité MAXIMALE pour s'exécuter avant tous les autres scripts
public class TankComponentAdder : MonoBehaviour
{
    [SerializeField] private GameObject gameOverUIPrefab;
    
    private List<GameObject> processedTanks = new List<GameObject>();
    
    public static TankComponentAdder Instance { get; private set; }
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        TreatExistingTanks();
        
        StartCoroutine(CheckForNewTanks());
    }
    
    private void TreatExistingTanks()
    {
        TankHealth2D[] tanks = FindObjectsOfType<TankHealth2D>();
        foreach (TankHealth2D tank in tanks)
        {
            AddComponentToTank(tank.gameObject);
        }
    }
    
    private void AddComponentToTank(GameObject tankObject)
    {
        TankHealth2D health = tankObject.GetComponent<TankHealth2D>();
        if (health == null) return; // Pas un tank, on ignore
        
        SimpleTankRespawn respawn = tankObject.GetComponent<SimpleTankRespawn>();
        if (respawn == null)
        {
            try
            {
                respawn = tankObject.AddComponent<SimpleTankRespawn>();
                
                if (gameOverUIPrefab != null)
                {
                    respawn.gameOverUIPrefab = gameOverUIPrefab;
                }
                
                if (!processedTanks.Contains(tankObject))
                {
                    processedTanks.Add(tankObject);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[TankComponentAdder] Erreur lors de l'ajout de SimpleTankRespawn: {ex.Message}");
            }
        }
        else
        {
            if (respawn.gameOverUIPrefab == null && gameOverUIPrefab != null)
            {
                respawn.gameOverUIPrefab = gameOverUIPrefab;
            }
        }
    }
    
    private IEnumerator CheckForNewTanks()
    {
        while (true)
        {
            TankHealth2D[] tanks = FindObjectsOfType<TankHealth2D>();
            foreach (TankHealth2D tank in tanks)
            {
                AddComponentToTank(tank.gameObject);
            }
            
            yield return new WaitForSeconds(0.2f); // Vérification plus fréquente (5 fois par seconde)
        }
    }
    
    public void OnJoinedSession()
    {
        processedTanks.Clear();
        TreatExistingTanks();
    }
    
    public void OnLeftSession()
    {
        processedTanks.Clear();
    }
    
    public void ResetAndTreatAllTanks()
    {
        processedTanks.Clear();
        TreatExistingTanks();
    }
}
