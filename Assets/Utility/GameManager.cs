using UnityEngine;
using Photon.Pun;

public class GameManager : MonoBehaviourPunCallbacks
{
    public static GameManager Instance { get; private set; }
    
    [HideInInspector]
    public bool isGameOver = false;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
        
        isGameOver = false;
    }
    
    public void SetGameOver()
    {
        isGameOver = true;
    }
    
    public override void OnLeftRoom()
    {
        isGameOver = false;
    }
    
    public bool ShouldEndMatch()
    {
        // Logique simple pour déterminer la fin du match
        // Retourne false par défaut pour éviter de finir le match trop tôt
        // Cette méthode peut être améliorée avec une vraie logique de fin de match
        return isGameOver;
    }
}
