using Photon.Pun;
using UnityEngine;
using System;

public class PhotonTankSpawner : MonoBehaviourPunCallbacks
{
    public static event Action<GameObject, PhotonView> OnTankSpawned;
    
    [Header("Spawns multiples")]
    public Transform[] spawnPoints; 

    public string tankPrefabName = "TankPrefab"; 
    public Vector2 fallbackSpawnPosition = new Vector2(0, 0); 

    private void Start()
    {
        if (PhotonNetwork.InRoom)
        {
            SpawnTank();
        }
    }

    // Dictionnaire pour stocker le dernier point de spawn utilisé par chaque joueur
    private static System.Collections.Generic.Dictionary<int, int> lastSpawnPointByPlayer = 
        new System.Collections.Generic.Dictionary<int, int>();
    
    public void SpawnTank()
    {
        Vector2 spawnPos = fallbackSpawnPosition;
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            int actorNumber = PhotonNetwork.LocalPlayer.ActorNumber;
            int spawnIdx = 0;
            
            // Si plusieurs points de spawn sont disponibles, on utilise une logique aléatoire
            if (spawnPoints.Length > 1)
            {
                // Générer un index aléatoire
                spawnIdx = UnityEngine.Random.Range(0, spawnPoints.Length);
                
                // Vérifier si le joueur a déjà utilisé un point de spawn précédemment
                if (lastSpawnPointByPlayer.ContainsKey(actorNumber))
                {
                    int previousIdx = lastSpawnPointByPlayer[actorNumber];
                    
                    // Éviter de réutiliser le même point de spawn consécutivement
                    while (spawnIdx == previousIdx && spawnPoints.Length > 1)
                    {
                        spawnIdx = UnityEngine.Random.Range(0, spawnPoints.Length);
                    }
                }
                
                // Sauvegarder le point de spawn utilisé pour ce joueur
                lastSpawnPointByPlayer[actorNumber] = spawnIdx;
            }
            
            spawnPos = spawnPoints[spawnIdx].position;
            
            // Ajouter une légère variation dans la position pour éviter les collisions
            float offsetX = UnityEngine.Random.Range(-0.5f, 0.5f);
            float offsetY = UnityEngine.Random.Range(-0.5f, 0.5f);
            spawnPos += new Vector2(offsetX, offsetY);
        }

        GameObject tank = PhotonNetwork.Instantiate(tankPrefabName, spawnPos, Quaternion.identity);
        var view = tank.GetComponent<PhotonView>();
        
        var nameDisplay = tank.GetComponent<PlayerNameDisplay>();
        if (nameDisplay != null)
        {
            Debug.Log("[SPAWN DEBUG] PlayerNameDisplay trouvé et configuré pour " + PhotonNetwork.LocalPlayer.NickName);
        }
        else
        {
            Debug.LogWarning("[SPAWN DEBUG] PlayerNameDisplay non trouvé sur le prefab TankPrefab");
        }

        var lobbyUI = FindObjectOfType<LobbyUI>();
        OnTankSpawned?.Invoke(tank, view);
    }
}