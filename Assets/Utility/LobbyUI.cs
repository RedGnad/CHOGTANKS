using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using Photon.Realtime;
using System.Text;
using System.Collections.Generic;

public class LobbyUI : MonoBehaviourPun, IMatchmakingCallbacks
{
    public static LobbyUI Instance { get; private set; }
    
    [Header("UI Elements")]
    public TMP_InputField playerNameInput;
    public TMP_Text waitingForPlayerText;
    public Button createRoomButton;
    public Button joinRoomButton;
    public Button goButton; // NOUVEAU BOUTON
    public TMP_InputField joinCodeInput;
    public TMP_Text createdCodeText;
    public GameObject joinPanel;
    public GameObject waitingPanel;
    public TMP_Text playerListText;
    public Button backButton;
    
    [Header("Match UI")]
    public TMP_Text timerText;
    public TMP_Text roomStatusText;

    private PhotonLauncher launcher;

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
    }
    
    void Start()
    {
        launcher = FindObjectOfType<PhotonLauncher>();

        if (backButton != null)
            backButton.onClick.AddListener(OnBackToLobby);
        
        createRoomButton.onClick.AddListener(OnCreateRoom);
        joinRoomButton.onClick.AddListener(OnJoinRoom);
        
        // NOUVEAU : Connecter le bouton GO
        if (goButton != null)
            goButton.onClick.AddListener(OnGoButtonClicked);

        if (playerNameInput != null)
        {
            playerNameInput.onEndEdit.AddListener(OnPlayerNameEndEdit);
        }

        joinPanel.SetActive(true);
        waitingPanel.SetActive(false);
        createRoomButton.interactable = false;
        joinRoomButton.interactable = false;
        
        // NOUVEAU : Désactiver le bouton GO par défaut
        if (goButton != null)
            goButton.interactable = false;
            
        createdCodeText.text = "";
        
        string defaultName = "Player_" + Random.Range(1000, 9999);
        if (playerNameInput != null)
        {
            playerNameInput.text = defaultName;
        }
        OnPlayerNameEndEdit(defaultName);

        if (playerListText != null)
        {
            playerListText.text = "";
        }
        
        if (timerText != null)
        {
            timerText.text = "";
        }
        
        if (roomStatusText != null)
        {
            roomStatusText.text = "";
        }
    }
    
    private void OnEnable()
    {
        // S'enregistrer aux callbacks Photon
        PhotonNetwork.AddCallbackTarget(this);
    }
    
    private void OnDisable()
    {
        // Se désenregistrer des callbacks Photon
        PhotonNetwork.RemoveCallbackTarget(this);
    }

    private void OnPlayerNameEndEdit(string newName)
    {
        if (launcher == null) return;
        
        string playerName = newName.Trim();
        
        if (string.IsNullOrEmpty(playerName) || playerName.Length < 2)
        {
            if(createdCodeText != null) createdCodeText.text = "Name must be at least 2 characters.";
            playerName = "Player_" + Random.Range(1000, 9999);
            if (playerNameInput != null) playerNameInput.text = playerName;
        }
        else if (playerName.Length > 20)
        {
            if(createdCodeText != null) createdCodeText.text = "Name cannot exceed 20 characters.";
            playerName = playerName.Substring(0, 20);
            if (playerNameInput != null) playerNameInput.text = playerName;
        }
        
        launcher.SetPlayerName(playerName);
        
        if (launcher.isConnectedAndReady)
        {
            OnPhotonReady();
        }
    }

    void OnCreateRoom()
    {
        launcher.CreatePrivateRoom();
        joinPanel.SetActive(false);
        waitingPanel.SetActive(true);
    }

    void OnJoinRoom()
    {
        string code = joinCodeInput.text.Trim().ToUpper();
        if (code.Length == 4)
        {
            launcher.JoinRoomByCode(code);
            joinPanel.SetActive(false);
            waitingPanel.SetActive(true);
        }
        else
        {
            createdCodeText.text = "Invalid code (must be 4 characters)";
        }
    }

    // NOUVELLE MÉTHODE : Gérer le clic sur le bouton GO
    void OnGoButtonClicked()
    {
        launcher.JoinRandomPublicRoom();
        joinPanel.SetActive(false);
        waitingPanel.SetActive(true);
        
        // Changer le texte pour indiquer qu'on cherche une room
        if (createdCodeText != null)
            createdCodeText.text = "Searching for players...";
    }

    public void OnPhotonReady()
    {
        
        if (!string.IsNullOrEmpty(PhotonNetwork.NickName))
        {
            createRoomButton.interactable = true;
            joinRoomButton.interactable = true;
            
            // NOUVEAU : Activer le bouton GO
            if (goButton != null)
                goButton.interactable = true;
        }
    }

    public void OnJoinRoomFailedUI()
    {
        createdCodeText.text = "No room found with this code.";
        joinPanel.SetActive(true);
        waitingPanel.SetActive(false);
    }

    public void OnJoinedRoomUI(string code)
    {
        createdCodeText.text = "Room code: " + code;
        joinPanel.SetActive(false);
        waitingPanel.SetActive(true);
        UpdatePlayerList();
        HideWaitingForPlayerTextIfRoomFull();
    }

    // NOUVELLE MÉTHODE : Gérer quand on rejoint une room publique
    public void OnJoinedRandomRoomUI()
    {
        // Si c'est une room publique (pas de code), afficher un message différent
        if (createdCodeText != null)
            createdCodeText.text = "Joined public match!";
            
        joinPanel.SetActive(false);
        waitingPanel.SetActive(true);
        UpdatePlayerList();
        HideWaitingForPlayerTextIfRoomFull();
    }

    public void UpdatePlayerList()
    {
        if (playerListText == null || PhotonNetwork.CurrentRoom == null)
        {
            if(playerListText != null) playerListText.text = "";
            return;
        }
        
        StringBuilder sb = new StringBuilder();
        Dictionary<int, int> playerScores = ScoreManager.Instance ? ScoreManager.Instance.GetPlayerScores() : new Dictionary<int, int>();
        
        foreach (Player p in PhotonNetwork.PlayerList)
        {
            string playerName = string.IsNullOrEmpty(p.NickName) ? $"Player {p.ActorNumber}" : p.NickName;
            int score = 0;
            
            if (playerScores.ContainsKey(p.ActorNumber))
            {
                score = playerScores[p.ActorNumber];
            }
            
            sb.AppendLine($"{playerName} - {score} pts");
        }
        
        playerListText.text = sb.ToString();
    }

    public void OnDisconnectedUI()
    {
        
        joinPanel.SetActive(true);
        waitingPanel.SetActive(false);
        
        createRoomButton.interactable = false;
        joinRoomButton.interactable = false;
        
        if (createdCodeText != null)
        {
            createdCodeText.text = "Connection lost... Reconnecting...";
        }
        
        if (playerListText != null)
        {
            playerListText.text = "";
        }
    }

    public void OnBackToLobby()
    {
        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
        }
        joinPanel.SetActive(true);
        waitingPanel.SetActive(false);
        createdCodeText.text = "";
        playerListText.text = "";
        
        foreach (var ui in GameObject.FindGameObjectsWithTag("GameOverUI"))
        {
            Destroy(ui);
        }
    }

    // --- Waiting Text Management ---

    public void HideWaitingForPlayerTextIfRoomFull()
    {
        if (PhotonNetwork.CurrentRoom != null && waitingForPlayerText != null)
        {
            waitingForPlayerText.gameObject.SetActive(PhotonNetwork.CurrentRoom.PlayerCount < 2);
        }
    }

    public void ShowWaitingForPlayerTextIfNotFull()
    {
        if (PhotonNetwork.CurrentRoom != null && waitingForPlayerText != null)
        {
            waitingForPlayerText.gameObject.SetActive(PhotonNetwork.CurrentRoom.PlayerCount < 2);
        }
    }
    
    public void UpdateTimer(int remainingSeconds)
    {
        if (timerText != null)
        {
            int minutes = remainingSeconds / 60;
            int seconds = remainingSeconds % 60;
            timerText.text = $"Time: {minutes:00}:{seconds:00}";
        }
    }
    
    public void UpdateRoomStatus(string status)
    {
        if (roomStatusText != null)
        {  
            roomStatusText.text = status;
        }
    }
    
    // Appelé quand on quitte une room et revient au lobby
    public void OnLeftRoom()
    {
        // Réinitialiser le panel de skins sans forcer sa fermeture
        SimpleSkinSelector skinSelector = FindObjectOfType<SimpleSkinSelector>();
        if (skinSelector != null)
        {
            // Juste cacher le panel s'il est ouvert, sans réinitialiser tout
            skinSelector.HideSkinPanel();
        }
        
        // Fermer le settings panel si ouvert
        SettingsPanelManager settingsManager = FindObjectOfType<SettingsPanelManager>();
        if (settingsManager != null)
        {
            settingsManager.HideSettingsPanel();
        }
        
        // Autres réinitialisations du lobby si nécessaire
        joinPanel.SetActive(true);
        waitingPanel.SetActive(false);
    }
    
    // Autres méthodes requises par IMatchmakingCallbacks (vides si pas utilisées)
    public void OnFriendListUpdate(System.Collections.Generic.List<FriendInfo> friendList) { }
    public void OnCreatedRoom() { }
    public void OnCreateRoomFailed(short returnCode, string message) { }
    public void OnJoinedRoom() { }
    public void OnJoinRoomFailed(short returnCode, string message) { }
    public void OnJoinRandomFailed(short returnCode, string message) { }
}