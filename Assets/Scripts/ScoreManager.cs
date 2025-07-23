using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using System.Runtime.InteropServices;
using TMPro;
using Multisynq;

public class ScoreManager : SynqBehaviour
{
    private const float ROOM_LIFETIME = 180f; 
    private const float RESPAWN_TIME = 5f;
    
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern bool SubmitScoreJS(string score, string bonus, string walletAddress);
#endif
    
    // Temporarily removed [SynqVar] from Dictionary - may cause crashes
    private Dictionary<int, int> playerScores = new Dictionary<int, int>(); 
    private Dictionary<string, string> playerWallets = new Dictionary<string, string>();
    [SynqVar] private float matchStartTime;
    [SynqVar] private bool matchEnded = false;
    
    // Multisynq compatibility properties
    [SynqVar] private bool isInRoom = false;
    [SynqVar] private bool isMasterClient = false;
    [SynqVar] private int localPlayerActorNumber = 1;
    
    public static ScoreManager Instance { get; private set; }
    private static Dictionary<int, string> _playerNames = new Dictionary<int, string>();
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void Start()
    {
        if (isInRoom)
        {
            StartMatch();
        }
    }
    
    public void ResetManager()
    {
        playerScores.Clear();
        playerWallets.Clear();
        matchStartTime = Time.time; 
        matchEnded = false;
        
        StopAllCoroutines();
    }
    
    [SynqRPC]
    public void OnJoinedSession()
    {
        ResetManager();
        StartMatch();
        
        if (!string.IsNullOrEmpty(PlayerSession.WalletAddress))
        {
            string walletAddress = PlayerSession.WalletAddress;
            int actorNumber = localPlayerActorNumber;
            
            UpdatePlayerWalletRPC(actorNumber.ToString(), walletAddress);
            playerWallets[actorNumber.ToString()] = walletAddress;
        }
    }
    
    private void StartMatch()
    {
        if (isMasterClient)
        {
            matchStartTime = Time.time;
            matchEnded = false;
            
            playerScores.Clear();
            // Initialize scores for connected players (placeholder logic)
            for (int i = 1; i <= 2; i++)
            {
                playerScores[i] = 0;
            }
            
            StartCoroutine(MatchTimer());
            
            SyncMatchTimeRPC(ROOM_LIFETIME);
            SyncScoresRPC();
        }
        else
        {            
            matchStartTime = Time.time - 1;
            StartCoroutine(MatchTimer());
        }
    }
    
    private IEnumerator MatchTimer()
    {
        float timeLeft = ROOM_LIFETIME;
        bool waitingForSync = !isMasterClient;
        
        if (LobbyUI.Instance != null)
        {
            LobbyUI.Instance.UpdateRoomStatus("Ongoing Match");
            
            if (waitingForSync)
            {
                LobbyUI.Instance.UpdateTimer((int)timeLeft);
            }
        }
        
        float nextSyncTime = 0f;
        
        while (timeLeft > 0 && !matchEnded)
        {
            timeLeft = ROOM_LIFETIME - (Time.time - matchStartTime);
            
            if (LobbyUI.Instance != null)
            {
                LobbyUI.Instance.UpdateTimer(Mathf.Max(0, (int)timeLeft));
            }
            
            if (isMasterClient && Time.time > nextSyncTime)
            {
                SyncMatchTimeRPC(timeLeft);
                nextSyncTime = Time.time + 5f; 
            }
            
            yield return null;
            
            if (timeLeft <= 0 && isMasterClient)
            {
                EndMatch();
                break;
            }
        }
    }
    
    public void AddKill(int killerActorNumber, int victimActorNumber, int killerViewID, int victimViewID)
    {
        if (matchEnded) return;
        
        if (playerScores.ContainsKey(killerActorNumber))
        {
            playerScores[killerActorNumber]++;
        }
        else
        {
            playerScores[killerActorNumber] = 1;
        }
        
        if (isMasterClient)
        {
            UpdateScoreRPC(killerActorNumber, playerScores[killerActorNumber]);
            SyncScoresRPC();
        }
        
        if (LobbyUI.Instance != null)
        {
            LobbyUI.Instance.UpdatePlayerList();
        }
        
        PlayerDied(killerActorNumber, victimActorNumber, killerViewID, victimViewID);
    }
    
    private void HandleScoreUpdate(int actorNumber, int score)
    {
        playerScores[actorNumber] = score;
        
        if (LobbyUI.Instance != null)
        {
            LobbyUI.Instance.UpdatePlayerList();
        }
    }
    
    public void PlayerDied(int killerActorNumber, int victimActorNumber, int killerViewID, int victimViewID)
    {
        if (matchEnded) return;
        
        string killerName = GetPlayerName(killerActorNumber);
        string victimName = GetPlayerName(victimActorNumber);
        
        if (LobbyUI.Instance != null && LobbyUI.Instance.killFeedText != null)
        {
            LobbyUI.Instance.killFeedText.text = $"{killerName} a tué {victimName} !";
            LobbyUI.Instance.StartCoroutine(HideKillFeedAfterDelay(3f));
        }
        
        // Find and destroy victim tank (Multisynq equivalent)
        GameObject victimTank = FindTankByViewID(victimViewID);
        if (victimTank != null)
        {
            Destroy(victimTank);
        }
        
        StartCoroutine(RespawnPlayer(victimActorNumber));
    }
    
    private string GetPlayerName(int actorNumber)
    {
        // Return player name based on actor number (Multisynq equivalent)
        if (playerScores.ContainsKey(actorNumber))
        {
            return $"Player {actorNumber}";
        }
        return $"Player {actorNumber}";
    }
    
    private IEnumerator HideKillFeedAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (LobbyUI.Instance != null && LobbyUI.Instance.killFeedText != null)
            LobbyUI.Instance.killFeedText.text = "";
    }
    
    private IEnumerator RespawnPlayer(int actorNumber)
    {
        yield return new WaitForSeconds(RESPAWN_TIME);
        
        if (localPlayerActorNumber == actorNumber)
        {
            yield break;
        }
        
        foreach (var ui in GameObject.FindGameObjectsWithTag("GameOverUI"))
        {
            Destroy(ui);
        }
        
        var spawner = FindObjectOfType<PhotonTankSpawner>();
        if (spawner != null)
        {
            spawner.SpawnTank(); // Use SpawnTank method instead of RespawnTank
        }
    }
    
    private GameObject FindTankByViewID(int viewID)
    {
        // Implement logic to find tank by view ID
        return null;
    }
    
    private void EndMatch()
    {
        if (matchEnded) return;
        matchEnded = true;
        
        if (LobbyUI.Instance != null)
        {
            LobbyUI.Instance.UpdateRoomStatus("Match ended!");
        }
        
        int highestScore = -1;
        int winnerActorNumber = -1;
        string winnerName = "Unknown Player";
        
        foreach (var scoreEntry in playerScores)
        {
            int actorNum = scoreEntry.Key;
            int score = scoreEntry.Value;
            if (score > highestScore)
            {
                highestScore = score;
                winnerActorNumber = actorNum;
                winnerName = $"Player {actorNum}";
            }
        }
        
        if (winnerActorNumber == -1)
        {
            foreach (var pair in playerScores)
            {
                if (pair.Value == highestScore)
                {
                    winnerActorNumber = pair.Key;
                    winnerName = _playerNames.ContainsKey(winnerActorNumber) ? 
                        _playerNames[winnerActorNumber] : $"Player {winnerActorNumber}";
                    break;
                }
            }
        }
        
        if (winnerActorNumber != -1)
        {
            playerScores[winnerActorNumber]++;
            highestScore++;
            
            if (LobbyUI.Instance != null)
            {
                LobbyUI.Instance.UpdatePlayerList();
            }
        }
        
        if (isMasterClient)
        {
            MatchEndRPC(winnerActorNumber, winnerName, highestScore);
        }
        else
        {
            return;
        }
        
        ShowWinnerAndSubmitScores(winnerActorNumber, winnerName, highestScore);
    }
    
    public void ShowWinnerAndSubmitScores(int winnerActorNumber, string winnerName, int highestScore)
    {
        if (LobbyUI.Instance != null)
        {
            LobbyUI.Instance.UpdateRoomStatus($"Victory: {winnerName} with {highestScore} points!");
        }
        
        GameObject[] gameOverUIs = GameObject.FindGameObjectsWithTag("GameOverUI");
        
        if (gameOverUIs.Length == 0)
        {
            PhotonLauncher launcher = FindObjectOfType<PhotonLauncher>();
            if (launcher != null)
            {
                launcher.ShowWinnerToAllRPC(winnerName, winnerActorNumber);
            }
        }
        
        int localPlayerScore = 0;
        if (playerScores.ContainsKey(localPlayerActorNumber))
        {
            localPlayerScore = playerScores[localPlayerActorNumber];
        }
        
        int bonus = 0;
        
        if (Application.platform == RuntimePlatform.WebGLPlayer)
        {
            SubmitScoreToFirebase(localPlayerScore, bonus);
        }
        
        ChogTanksNFTManager nftManager = FindObjectOfType<ChogTanksNFTManager>();
        if (nftManager != null)
        {
            nftManager.ForceRefreshAfterMatch(localPlayerScore);
        }
    }
    
    private void SubmitScoreToFirebase(int score, int bonus)
    {
        string walletAddress = "";
        
        try
        {
            if (Reown.AppKit.Unity.AppKit.IsInitialized && 
                Reown.AppKit.Unity.AppKit.IsAccountConnected && 
                Reown.AppKit.Unity.AppKit.Account != null)
            {
                string appKitAddress = Reown.AppKit.Unity.AppKit.Account.Address;
                if (!string.IsNullOrEmpty(appKitAddress))
                {
                    walletAddress = appKitAddress;
                }
            }
        }
        catch (System.Exception ex)
        {
        }
        
        if (string.IsNullOrEmpty(walletAddress))
        {
            string prefsAddress = PlayerPrefs.GetString("walletAddress", "");
            if (!string.IsNullOrEmpty(prefsAddress))
            {
                walletAddress = prefsAddress;
            }
        }
        
        if (string.IsNullOrEmpty(walletAddress))
        {
            try
            {
                if (PlayerSession.IsConnected && !string.IsNullOrEmpty(PlayerSession.WalletAddress))
                {
                    walletAddress = PlayerSession.WalletAddress;
                }
            }
            catch (System.Exception ex)
            {
            }
        }
        
        if (string.IsNullOrEmpty(walletAddress))
        {
            walletAddress = "anonymous";
        }
        
#if UNITY_WEBGL && !UNITY_EDITOR
        SubmitScoreJS(score.ToString(), bonus.ToString(), walletAddress);
#else
#endif
    }
    
    [SynqRPC]
    private void SyncScoresRPC()
    {
        if (!isMasterClient) return;
        
        // Multisynq automatically syncs [SynqVar] variables, so we just need to update UI
        if (LobbyUI.Instance != null)
        {
            LobbyUI.Instance.UpdatePlayerList();
        }
    }
    
    [SynqRPC]
    private void SyncMatchTimeRPC(float timeLeft)
    {
        if (!isMasterClient) return;
        
        // Update match start time based on remaining time
        matchStartTime = Time.time - (ROOM_LIFETIME - timeLeft);
        
        if (LobbyUI.Instance != null)
        {
            LobbyUI.Instance.UpdateTimer(Mathf.Max(0, (int)timeLeft));
        }
    }
    
    // Replaced OnEvent with individual SynqRPC methods
    
    [SynqRPC]
    public void UpdateScoreRPC(int actorNumber, int score)
    {
        HandleScoreUpdate(actorNumber, score);
    }
    
    [SynqRPC]
    public void MatchEndRPC(int winnerActorNumber, string winnerName, int highestScore)
    {
        ShowWinnerAndSubmitScores(winnerActorNumber, winnerName, highestScore);
    }
    
    [SynqRPC]
    public void UpdatePlayerWalletRPC(string actorIdStr, string walletAddress)
    {
        playerWallets[actorIdStr] = walletAddress;
    }
    
    [SynqRPC]
    public void SyncPlayerScoresRPC(int[] actorNumbers, int[] scores)
    {
        playerScores.Clear();
        for (int i = 0; i < actorNumbers.Length; i++)
        {
            playerScores[actorNumbers[i]] = scores[i];
        }
        
        if (LobbyUI.Instance != null)
        {
            LobbyUI.Instance.UpdatePlayerList();
        }
    }
    
    [SynqRPC]
    public void SyncTimerRPC(float timeRemaining)
    {
        matchStartTime = Time.time - (ROOM_LIFETIME - timeRemaining);
        
        if (LobbyUI.Instance != null)
        {
            LobbyUI.Instance.UpdateTimer(Mathf.Max(0, (int)timeRemaining));
        }
    }
    
    [SynqRPC]
    public void OnMasterClientSwitched(int newMasterClientActorNumber)
    {
        if (newMasterClientActorNumber == localPlayerActorNumber)
        {
            isMasterClient = true;
            if (!matchEnded)
            {
                SyncMatchTimeRPC(ROOM_LIFETIME - (Time.time - matchStartTime));
            }
        }
        else
        {
            isMasterClient = false;
        }
    }
    
    [SynqRPC]
    public void OnPlayerEnteredSession(int newPlayerActorNumber)
    {
        if (!playerScores.ContainsKey(newPlayerActorNumber))
        {
            playerScores[newPlayerActorNumber] = 0;
        }
        
        if (isMasterClient)
        {
            SyncScoresRPC();
            
            float timeLeft = ROOM_LIFETIME - (Time.time - matchStartTime);
            SyncMatchTimeRPC(timeLeft);
        }
    }
    
    [SynqRPC]
    public void OnLeftSession()
    {
        ResetManager();
    }
    
    [SynqRPC]
    public void OnDisconnected(string reason)
    {
        ResetManager();
    }
    
    public Dictionary<int, int> GetPlayerScores()
    {
        return playerScores;
    }
    
    public bool IsMatchEnded()
    {
        return matchEnded || (Time.time - matchStartTime) >= ROOM_LIFETIME;
    }
}
