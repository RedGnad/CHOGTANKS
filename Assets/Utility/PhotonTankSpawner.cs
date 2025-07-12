using Photon.Pun;
using UnityEngine;
using System;

public class PhotonTankSpawner : MonoBehaviourPunCallbacks
{
    // Événement déclenché lorsqu'un tank est spawné (GameObject du tank, PhotonView)
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

    public void SpawnTank()
    {
        Vector2 spawnPos = fallbackSpawnPosition;
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            int playerIndex = PhotonNetwork.LocalPlayer.ActorNumber - 1;
            int spawnIdx = playerIndex % spawnPoints.Length;
            spawnPos = spawnPoints[spawnIdx].position;
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
        if (lobbyUI != null && PhotonNetwork.CurrentRoom != null)
        {
            lobbyUI.createdCodeText.text = "Room code : " + PhotonNetwork.CurrentRoom.Name;
        }
        
        // Déclencher l'événement OnTankSpawned
        OnTankSpawned?.Invoke(tank, view);
    }
}