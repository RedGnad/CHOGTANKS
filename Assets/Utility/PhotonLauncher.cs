using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using System.Linq; // Ajout pour manipuler les listes
using System.Collections.Generic; // Ajout pour List<T>
using System.Collections;
using TMPro;

public class PhotonLauncher : MonoBehaviourPunCallbacks
{
    [Header("UI References")]
    [SerializeField] private GameObject gameOverUIPrefab;

    [Header("Gestion de déconnexion")]
    [SerializeField] private float autoReconnectDelay = 2f;
    [SerializeField] private string lobbySceneName = "LobbyScene";
    [SerializeField] private GameObject reconnectionNotificationPrefab;
    [SerializeField] private int maxReconnectAttempts = 3;
    [SerializeField] private float timeBetweenReconnectAttempts = 2f;
    
    private bool isWaitingForReconnection = false;
    private bool wasDisconnected = false;
    private int currentReconnectAttempt = 0;
    private string lastRoomName = "";
    private bool wasInRoom = false;

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
        // Solution simple : juste un log et retour automatique au lobby
        bool isWinner = PhotonNetwork.LocalPlayer.ActorNumber == winnerActorNumber;
        
        if (isWinner)
        {
            Debug.Log($"You Win, {winnerName}!");
        }
        else
        {
            Debug.Log($"{winnerName} Wins!");
        }
        
        // Retour automatique au lobby après 6 secondes
        StartCoroutine(AutoReturnToLobby());
    }

    private System.Collections.IEnumerator AutoReturnToLobby()
    {
        // Attendre 6 secondes
        yield return new WaitForSeconds(6.0f);
        
        // Quitter la room si on y est encore
        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            Debug.Log("Auto return to lobby - leaving room");
            PhotonNetwork.LeaveRoom();
        }
        
        // Réinitialiser l'UI du lobby
        LobbyUI lobbyUI = FindObjectOfType<LobbyUI>();
        if (lobbyUI != null)
        {
            lobbyUI.OnBackToLobby();
        }
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
        
        // Sauvegarder l'état de connexion actuel avant la déconnexion
        wasInRoom = PhotonNetwork.InRoom;
        if (wasInRoom && PhotonNetwork.CurrentRoom != null)
        {
            lastRoomName = PhotonNetwork.CurrentRoom.Name;
        }
        
        ShowReconnectionNotification();
        
        // Au lieu de retourner au lobby directement, tenter de se reconnecter
        currentReconnectAttempt = 0;
        StopAllCoroutines(); // Arrêter les anciennes tentatives
        StartCoroutine(AttemptReconnect());
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
    
    private IEnumerator AttemptReconnect()
    {
        Debug.Log("[PHOTON] Tentative de reconnexion automatique...");
        
        while (currentReconnectAttempt < maxReconnectAttempts)
        {
            currentReconnectAttempt++;
            Debug.Log($"[PHOTON] Tentative de reconnexion {currentReconnectAttempt}/{maxReconnectAttempts}");
            
            // Afficher notification de tentative de reconnexion
            if (reconnectionNotificationPrefab != null)
            {
                GameObject notif = Instantiate(reconnectionNotificationPrefab);
                TextMeshProUGUI textComponent = notif.GetComponentInChildren<TextMeshProUGUI>();
                if (textComponent != null)
                {
                    textComponent.text = $"Tentative de reconnexion {currentReconnectAttempt}/{maxReconnectAttempts}...";
                }
                Destroy(notif, timeBetweenReconnectAttempts);
            }
            
            // Se reconnecter si déconnecté
            if (!PhotonNetwork.IsConnected)
            {
                PhotonNetwork.ConnectUsingSettings();
            }
            
            // Attendre que la connexion s'établisse
            float timeWaited = 0f;
            while (!PhotonNetwork.IsConnected && timeWaited < timeBetweenReconnectAttempts)
            {
                timeWaited += 0.2f;
                yield return new WaitForSeconds(0.2f);
            }
            
            // Si nous sommes reconnectés avec succès
            if (PhotonNetwork.IsConnected)
            {
                Debug.Log("[PHOTON] Reconnecté au serveur Photon avec succès!");
                
                // Si nous étions dans une room, essayer de la rejoindre
                if (wasInRoom && !string.IsNullOrEmpty(lastRoomName))
                {
                    yield return new WaitForSeconds(1f); // Attendre que la connexion soit stable
                    Debug.Log($"[PHOTON] Tentative de rejoindre la room précédente: {lastRoomName}");
                    
                    PhotonNetwork.JoinRoom(lastRoomName);
                    
                    // Attendre de voir si nous avons rejoint la room
                    yield return new WaitForSeconds(2f);
                    
                    if (PhotonNetwork.InRoom)
                    {
                        Debug.Log("[PHOTON] Room rejointe avec succès!");
                        // Réinitialiser les variables
                        wasDisconnected = false;
                        isWaitingForReconnection = false;
                        currentReconnectAttempt = 0;
                        
                        // Afficher notification de succès
                        if (reconnectionNotificationPrefab != null)
                        {
                            GameObject notif = Instantiate(reconnectionNotificationPrefab);
                            TextMeshProUGUI textComponent = notif.GetComponentInChildren<TextMeshProUGUI>();
                            if (textComponent != null)
                            {
                                textComponent.text = "Reconnecté avec succès!";
                            }
                            Destroy(notif, 3f);
                        }
                        
                        yield break; // Sortir de la coroutine
                    }
                }
                else
                {
                    // Nous n'étions pas dans une room, donc c'est déjà un succès
                    Debug.Log("[PHOTON] Reconnecté au lobby avec succès!");
                    wasDisconnected = false;
                    isWaitingForReconnection = false;
                    currentReconnectAttempt = 0;
                    
                    // Afficher notification de succès
                    if (reconnectionNotificationPrefab != null)
                    {
                        GameObject notif = Instantiate(reconnectionNotificationPrefab);
                        TextMeshProUGUI textComponent = notif.GetComponentInChildren<TextMeshProUGUI>();
                        if (textComponent != null)
                        {
                            textComponent.text = "Reconnecté au lobby avec succès!";
                        }
                        Destroy(notif, 3f);
                    }
                    
                    yield break;
                }
            }
            
            // Attendre avant la prochaine tentative
            yield return new WaitForSeconds(timeBetweenReconnectAttempts);
        }
        
        // Échec après plusieurs tentatives, retourner au lobby
        Debug.LogWarning("[PHOTON] Toutes les tentatives de reconnexion ont échoué, retour au lobby");
        StartCoroutine(ReturnToLobby());
    }
    
    private System.Collections.IEnumerator ReturnToLobby()
    {
        yield return new WaitForSeconds(autoReconnectDelay);
        
        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
        }
        
        // Afficher notification de retour au lobby
        if (reconnectionNotificationPrefab != null)
        {
            GameObject notif = Instantiate(reconnectionNotificationPrefab);
            TextMeshProUGUI textComponent = notif.GetComponentInChildren<TextMeshProUGUI>();
            if (textComponent != null)
            {
                textComponent.text = "Retour au lobby...";
            }
            Destroy(notif, 3f);
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
        Debug.Log("[LOBBYUI] Tentative de rejoindre la room publique fixe: " + publicRoomName);
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

    // MÉTHODE OBSOLÈTE - Utiliser JoinRandomPublicRoom à la place
    // Cette méthode est conservée pour compatibilité mais redirige vers JoinRandomPublicRoom
    public void JoinOrCreatePublicRoom()
    {
        Debug.Log("[LOBBYUI] JoinOrCreatePublicRoom appelé, redirection vers JoinRandomPublicRoom");
        JoinRandomPublicRoom();
    }
}