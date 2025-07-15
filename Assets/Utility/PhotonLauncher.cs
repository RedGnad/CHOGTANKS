using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using System.Linq; // Ajout pour manipuler les listes
using System.Collections.Generic; // Ajout pour List<T>

public class PhotonLauncher : MonoBehaviourPunCallbacks
{
    [Header("UI References")]
    [SerializeField] private GameObject gameOverUIPrefab;

    [Header("Gestion de déconnexion")]
    [SerializeField] private float autoReconnectDelay = 2f;
    [SerializeField] private string lobbySceneName = "LobbyScene";
    [SerializeField] private GameObject reconnectionNotificationPrefab;
    
    private bool isWaitingForReconnection = false;
    private bool wasDisconnected = false;

    private List<RoomInfo> cachedRoomList = new List<RoomInfo>();

    [PunRPC]
    public void RestartMatchSoftRPC()
    {
        foreach (var ui in GameObject.FindGameObjectsWithTag("GameOverUI"))
        {
            Destroy(ui);
        }

        var minimapCam = FindObjectOfType<MinimapCamera>();
        if (minimapCam != null)
        {
            minimapCam.ForceReset();
        }

        TankHealth2D myTank = null;
        foreach (var t in FindObjectsOfType<TankHealth2D>())
        {
            if (t.photonView.IsMine)
            {
                myTank = t;
                break;
            }
        }
        if (myTank != null)
        {
            PhotonNetwork.Destroy(myTank.gameObject);
        }

        var spawner = FindObjectOfType<PhotonTankSpawner>();
        if (spawner != null)
        {
            spawner.SpawnTank();
        }
    }

    [PunRPC]
    public void ShowWinnerToAllRPC(string winnerName, int winnerActorNumber)
    {
        
        bool isWinner = PhotonNetwork.LocalPlayer.ActorNumber == winnerActorNumber;
        
        GameObject prefabToUse = gameOverUIPrefab;
        if (prefabToUse == null)
        {
            var tankHealth = FindObjectOfType<TankHealth2D>();
            if (tankHealth != null)
            {
                var field = typeof(TankHealth2D).GetField("gameOverUIPrefab", 
                    System.Reflection.BindingFlags.NonPublic | 
                    System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    prefabToUse = field.GetValue(tankHealth) as GameObject;
                }
            }
        }
        
        Camera mainCam = Camera.main;
        if (mainCam != null && prefabToUse != null)
        {
            GameObject uiInstance = Instantiate(prefabToUse, mainCam.transform);
            RectTransform rt = uiInstance.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.localPosition = new Vector3(0f, 0f, 1f);
                rt.localRotation = Quaternion.identity;
                float baseScale = 1f;
                float dist = Vector3.Distance(mainCam.transform.position, rt.position);
                float scaleFactor = baseScale * (dist / mainCam.orthographicSize) * 0.1f;
                rt.localScale = new Vector3(scaleFactor, scaleFactor, scaleFactor);
            }
            
            var controller = uiInstance.GetComponent<GameOverUIController>();
            if (controller != null)
            {
                if (isWinner)
                {
                    controller.ShowWin(winnerName);
                }
                else
                {
                    controller.ShowWinner(winnerName);
                }
            }
            
            StartCoroutine(AutoDestroyAndRestart(uiInstance));
        }
    }

    private System.Collections.IEnumerator AutoDestroyAndRestart(GameObject uiInstance)
    {
        yield return new WaitForSeconds(3f);
        if (uiInstance != null)
        {
            Destroy(uiInstance);
        }
        CallRestartMatchSoft();
    }

    public static void CallRestartMatchSoft()
    {
        var launcher = FindObjectOfType<PhotonLauncher>();
        if (launcher != null)
        {
            if (launcher.photonView != null)
            {
                launcher.photonView.RPC("RestartMatchSoftRPC", RpcTarget.All);
            }
            else
            {
                Debug.LogError("[PhotonLauncher] PhotonView manquant sur PhotonLauncher !");
            }
        }
        else
        {
            Debug.LogError("[PhotonLauncher] Impossible de trouver PhotonLauncher pour le reset soft!");
        }
    }

    public bool isConnectedAndReady = false;

    [Header("Room Settings")]
    public string roomName = "";
    public byte maxPlayers = 8;

    public LobbyUI lobbyUI;

    private static readonly string chars = "ABCDEFGHIJKLMNPQRSTUVWXYZ123456789";
    private System.Random rng = new System.Random();

    public string GenerateRoomCode()
    {
        char[] code = new char[4];
        for (int i = 0; i < 4; i++)
        {
            code[i] = chars[rng.Next(chars.Length)];
        }
        return new string(code);
    }

    public void CreatePrivateRoom()
    {
        roomName = GenerateRoomCode();
        RoomOptions options = new RoomOptions { MaxPlayers = maxPlayers, IsVisible = true, IsOpen = true };
        PhotonNetwork.CreateRoom(roomName, options, TypedLobby.Default);
    }

    public void JoinRoomByCode(string code)
    {
        roomName = code.ToUpper();
        PhotonNetwork.JoinRoom(roomName);
    }

    public void SetPlayerName(string playerName)
    {
        if (string.IsNullOrEmpty(playerName))
        {
            PhotonNetwork.NickName = "Player_" + Random.Range(1000, 9999);
        }
        else
        {
            PhotonNetwork.NickName = playerName;
        }
    }

    private void Start()
    {
        if (GetComponent<PhotonView>() == null)
        {
            Debug.LogError("[PhotonLauncher] PhotonView manquant sur l'objet PhotonLauncher ! Merci d'ajouter un PhotonView dans l'inspecteur AVANT de lancer la scène.");
        }
        
        if (!PhotonNetwork.IsConnected)
        {
            
            PhotonNetwork.NetworkingClient.LoadBalancingPeer.DisconnectTimeout = 300000; // 5 minutes
            PhotonNetwork.NetworkingClient.LoadBalancingPeer.TimePingInterval = 5000; // 5 secondes
            PhotonNetwork.KeepAliveInBackground = 60; // 60 secondes
            
            PhotonNetwork.ConnectUsingSettings();
        }
        
        StartCoroutine(ConnectionHeartbeat());
    }
    
    private System.Collections.IEnumerator ConnectionHeartbeat()
    {
        WaitForSeconds wait = new WaitForSeconds(20f); // Envoie un heartbeat toutes les 20 secondes
        
        while (true)
        {
            yield return wait;
            
            if (PhotonNetwork.IsConnected)
            {
                
                if (PhotonNetwork.InRoom)
                {
                    photonView.RPC("HeartbeatPing", RpcTarget.MasterClient);
                }
            }
        }
    }
    
    [PunRPC]
    private void HeartbeatPing()
    {
        // ...
    }

    public override void OnConnectedToMaster()
    {
        isConnectedAndReady = true;
        wasDisconnected = false; 
        
        if (lobbyUI == null) lobbyUI = FindObjectOfType<LobbyUI>();
        if (lobbyUI != null)
        {
            lobbyUI.OnPhotonReady();
        }
        else
        {
            Debug.LogError("[PHOTON LAUNCHER] lobbyUI est null dans OnConnectedToMaster !");
        }
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        
        wasDisconnected = true;
        isConnectedAndReady = false;
        
        ShowReconnectionNotification();
        
        StartCoroutine(ReturnToLobby());
    }
    
    private void ShowReconnectionNotification()
    {
        if (reconnectionNotificationPrefab != null)
        {
            GameObject notif = Instantiate(reconnectionNotificationPrefab);
            Destroy(notif, 3f);
        }
        else
        {
            Debug.LogWarning("[PhotonLauncher] reconnectionNotificationPrefab non assigné");
        }
    }
    
    private System.Collections.IEnumerator ReturnToLobby()
    {
        yield return new WaitForSeconds(autoReconnectDelay);
        
        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
        }
        
        UnityEngine.SceneManagement.SceneManager.LoadScene(lobbySceneName);
    }

    public override void OnJoinedRoom()
    {
        if (lobbyUI == null) lobbyUI = FindObjectOfType<LobbyUI>();
        if (lobbyUI != null)
        {
            lobbyUI.OnJoinedRoomUI(PhotonNetwork.CurrentRoom.Name);
        }
        
        
        var spawner = FindObjectOfType<PhotonTankSpawner>();
        if (spawner != null)
        {
            spawner.SpawnTank();
        }
        else
        {
            Debug.LogError("[PhotonLauncher] PhotonTankSpawner non trouvé dans la scène !");
        }
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        if (lobbyUI == null) lobbyUI = FindObjectOfType<LobbyUI>();
        if (lobbyUI != null)
        {
            lobbyUI.OnJoinRoomFailedUI();
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (lobbyUI == null) lobbyUI = FindObjectOfType<LobbyUI>();
        if (lobbyUI != null)
        {
            lobbyUI.HideWaitingForPlayerTextIfRoomFull();
            lobbyUI.UpdatePlayerList();
        }
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (lobbyUI == null) lobbyUI = FindObjectOfType<LobbyUI>();
        if (lobbyUI != null)
        {
            lobbyUI.ShowWaitingForPlayerTextIfNotFull();
            lobbyUI.UpdatePlayerList();
        }
    }

    public void JoinRandomPublicRoom()
    {
        string publicRoomName = "PublicRoom";
        roomName = publicRoomName; // Synchronise le champ roomName avec la room publique
        RoomOptions options = new RoomOptions
        {
            MaxPlayers = maxPlayers,
            IsVisible = true,
            IsOpen = true
        };
        PhotonNetwork.JoinOrCreateRoom(publicRoomName, options, TypedLobby.Default);
    }

    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        cachedRoomList = roomList;
    }

    public void JoinOrCreatePublicRoom()
    {
        // Cherche la première room publique non pleine
        int roomIndex = 1;
        foreach (var room in cachedRoomList)
        {
            if (room.Name.StartsWith("PublicRoom") && room.PlayerCount < room.MaxPlayers && room.IsOpen && room.IsVisible)
            {
                roomName = room.Name;
                PhotonNetwork.JoinRoom(room.Name);
                return;
            }
            // Pour générer le prochain index
            if (room.Name.StartsWith("PublicRoom"))
            {
                int idx;
                if (int.TryParse(room.Name.Replace("PublicRoom", ""), out idx))
                {
                    roomIndex = Mathf.Max(roomIndex, idx + 1);
                }
            }
        }
        // Si aucune room publique dispo, crée la suivante
        string newRoomName = $"PublicRoom{roomIndex}";
        roomName = newRoomName;
        RoomOptions options = new RoomOptions
        {
            MaxPlayers = maxPlayers,
            IsVisible = true,
            IsOpen = true
        };
        PhotonNetwork.CreateRoom(newRoomName, options, TypedLobby.Default);
    }
}