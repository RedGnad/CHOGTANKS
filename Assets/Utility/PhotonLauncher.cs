using Multisynq;
using UnityEngine;
using System.Linq; 
using System.Collections;

public class PhotonLauncher : SynqBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject gameOverUIPrefab;

    [Header("Gestion de déconnexion")]
    [SerializeField] private float autoReconnectDelay = 2f;
    [SerializeField] private string lobbySceneName = "LobbyScene";
    [SerializeField] private GameObject reconnectionNotificationPrefab;
    
    [Header("Room Settings")]
    [SerializeField] private byte maxPlayers = 2;
    
    private bool isWaitingForReconnection = false;
    private bool wasDisconnected = false;
    public bool isConnectedAndReady = false;
    public string roomName = "";
    private LobbyUI lobbyUI;
    
    // Multisync compatibility properties
    public int ActorNumber => GetInstanceID(); // Use instance ID as actor number

    [SynqRPC]
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
            if (t.IsMine)
            {
                myTank = t;
                break;
            }
        }
        if (myTank != null)
        {
            Destroy(myTank.gameObject);
        }

        var spawner = FindObjectOfType<PhotonTankSpawner>();
        if (spawner != null)
        {
            spawner.SpawnTank();
        }
    }

    [SynqRPC]
    public void ShowWinnerToAllRPC(string winnerName, int winnerActorNumber)
    {
        bool isWinner = ActorNumber == winnerActorNumber;
        
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
                
                StartCoroutine(ReturnToLobbyAfterDelay(6));
            }
            
            StartCoroutine(AutoDestroyAndRestart(uiInstance));
        }
    }

    private IEnumerator ReturnToLobbyAfterDelay(int seconds)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetGameOver();
        }
        
        Debug.Log($"[MULTISYNC] Retour lobby dans {seconds} secondes...");
        yield return new WaitForSeconds(seconds);
        
        LobbyUI lobbyUI = FindObjectOfType<LobbyUI>();
        if (lobbyUI != null)
        {
            lobbyUI.OnBackToLobby();
        }
        else
        {
            Debug.LogWarning("[MULTISYNC] LobbyUI non trouvé pour le retour au lobby");
        }
    }

    private IEnumerator AutoDestroyAndRestart(GameObject uiInstance)
    {
        yield return new WaitForSeconds(8);
        
        if (uiInstance != null)
        {
            Destroy(uiInstance);
        }
        
        RestartMatchSoftRPC();
    }

    public void CallRestartMatchSoft()
    {
        Debug.Log("[MULTISYNC] Tentative de redémarrage du match...");
        RestartMatchSoftRPC();
    }

    public string GenerateRoomCode()
    {
        char[] code = new char[4];
        for (int i = 0; i < 4; i++)
        {
            code[i] = (char)Random.Range(65, 91); // A-Z
        }
        return new string(code);
    }

    public void CreatePrivateRoom()
    {
        roomName = GenerateRoomCode();
        Debug.Log($"[MULTISYNC] Création de room privée: {roomName}");
        // TODO: Implement Multisync room creation
    }

    public void JoinRoomByCode(string roomCode)
    {
        Debug.Log($"[MULTISYNC] Tentative de rejoindre room: {roomCode}");
        // TODO: Implement Multisync room joining
    }

    public void SetPlayerName(string playerName)
    {
        if (string.IsNullOrEmpty(playerName))
        {
            playerName = "Newbie_" + Random.Range(100, 999);
        }
        
        Debug.Log($"[MULTISYNC] Nom du joueur défini: {playerName}");
        // TODO: Set Multisync player name
    }

    private void Start()
    {
        lobbyUI = FindObjectOfType<LobbyUI>();
        
        Debug.Log("[MULTISYNC] Initialisation de la connexion Multisync...");
        StartCoroutine(SimulateConnection());
    }

    private IEnumerator SimulateConnection()
    {
        yield return new WaitForSeconds(1f);
        isConnectedAndReady = true;
        OnConnectedToMaster();
    }

    [SynqRPC]
    private void HeartbeatPing()
    {
        Debug.Log("[MULTISYNC] Heartbeat ping reçu");
    }

    public void OnConnectedToMaster()
    {
        isConnectedAndReady = true;
        wasDisconnected = false; 
        
        if (lobbyUI == null) lobbyUI = FindObjectOfType<LobbyUI>();
        if (lobbyUI != null)
        {
            lobbyUI.OnMultisynqReady();
        }
        else
        {
            Debug.LogError("[MULTISYNC LAUNCHER] lobbyUI est null dans OnConnectedToMaster !");
        }
    }

    public void OnDisconnected(string cause)
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
            Debug.LogWarning("[MULTISYNC] reconnectionNotificationPrefab non assigné");
        }
    }
    
    private IEnumerator ReturnToLobby()
    {
        yield return new WaitForSeconds(autoReconnectDelay);
        
        LobbyUI lobbyUI = FindObjectOfType<LobbyUI>();
        if (lobbyUI != null)
        {
            lobbyUI.OnBackToLobby();
        }
        else
        {
            Debug.LogWarning("[MULTISYNC] LobbyUI non trouvé pour le retour au lobby après déconnexion");
        }
    }

    public void OnJoinedRoom()
    {
        if (lobbyUI == null) lobbyUI = FindObjectOfType<LobbyUI>();
        if (lobbyUI != null)
        {
            lobbyUI.OnJoinedRoomUI(roomName);
        }
        
        if (GameManager.Instance != null)
        {
            GameManager.Instance.isGameOver = false;
        }
        
        if (ScoreManager.Instance != null) 
        {
            ScoreManager.Instance.ResetManager();
        }
        
        var spawner = FindObjectOfType<PhotonTankSpawner>();
        if (spawner != null)
        {
            spawner.SpawnTank();
        }
        else
        {
            Debug.LogError("[MULTISYNC] PhotonTankSpawner non trouvé dans la scène !");
        }
    }

    public void OnJoinRoomFailed(string message)
    {
        if (lobbyUI == null) lobbyUI = FindObjectOfType<LobbyUI>();
        if (lobbyUI != null)
        {
            lobbyUI.OnJoinRoomFailedUI();
        }
    }

    public void OnPlayerEnteredRoom(string playerName)
    {
        if (lobbyUI == null) lobbyUI = FindObjectOfType<LobbyUI>();
        if (lobbyUI != null)
        {
            lobbyUI.HideWaitingForPlayerTextIfRoomFull();
            lobbyUI.UpdatePlayerList();
        }
    }

    public void OnPlayerLeftRoom(string playerName)
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
        roomName = publicRoomName; 
        Debug.Log($"[MULTISYNC] Rejoindre/créer room publique: {publicRoomName}");
        
        StartCoroutine(SimulateJoinRoom());
    }

    private IEnumerator SimulateJoinRoom()
    {
        yield return new WaitForSeconds(0.5f);
        OnJoinedRoom();
    }

    public void JoinOrCreatePublicRoom()
    {
        JoinRandomPublicRoom();
    }
}
