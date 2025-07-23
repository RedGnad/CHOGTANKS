using UnityEngine;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Multisynq;

/// <summary>
/// Multisynq network wrapper for CHOGTANKS
/// Provides networking functionality using Multisynq M4U
/// </summary>
public class NetworkWrapper : SynqBehaviour
{
    [Header("Multisynq Configuration")]
    [SerializeField] private string multisyncApiKey = "your_api_key";
    [SerializeField] private string multisyncAppId = "io.multisynq.chogtanks";
    
    [Header("Debug")]
    [SerializeField] private bool enableLogs = true;
    
    // Singleton instance
    public static NetworkWrapper Instance { get; private set; }
    
    // Multisynq network state
    [SynqVar] private bool isConnected = false;
    [SynqVar] private string currentRoomId = "";
    [SynqVar] private string localPlayerId = "";
    [SynqVar] private Dictionary<string, object> playerProperties = new Dictionary<string, object>();
    
    // Multisynq session events
    public static System.Action OnConnectedToMaster;
    public static System.Action<string> OnDisconnected;
    public static System.Action OnJoinedRoom;
    public static System.Action<short, string> OnJoinRoomFailed;
    public static System.Action<string> OnPlayerEnteredRoom;
    public static System.Action<string> OnPlayerLeftRoom;
    
    #region Unity Lifecycle
    
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
        InitializeMultisynq();
    }
    
    #endregion
    
    #region Multisynq Initialization
    
    private async void InitializeMultisynq()
    {
        try
        {
            Log("Initializing Multisynq networking...");
            
            // Safety check for API key
            if (string.IsNullOrEmpty(multisyncApiKey) || multisyncApiKey == "your_api_key")
            {
                Log("Warning: Multisynq API key not configured. Using simulation mode.");
            }
            
            #if UNITY_WEBGL && !UNITY_EDITOR
            // Call JavaScript Multisynq initialization
            if (!string.IsNullOrEmpty(multisyncApiKey) && multisyncApiKey != "your_api_key")
            {
                InitializeMultisyncJS(multisyncApiKey, multisyncAppId);
            }
            else
            {
                Log("Skipping Multisynq JS initialization - API key not configured");
                Invoke(nameof(SimulateMultisyncConnection), 1f);
            }
            #else
            // Simulate Multisynq connection for testing in editor
            Log("Multisynq initialization (simulated in editor)");
            Invoke(nameof(SimulateMultisyncConnection), 1f);
            #endif
        }
        catch (System.Exception e)
        {
            Log($"Multisynq initialization failed: {e.Message}");
            Log($"Stack trace: {e.StackTrace}");
            
            // Fallback to simulation mode
            try
            {
                Invoke(nameof(SimulateMultisyncConnection), 2f);
            }
            catch (System.Exception fallbackEx)
            {
                Log($"Fallback simulation also failed: {fallbackEx.Message}");
            }
        }
    }
    
    #endregion
    
    #region Public API
    
    /// <summary>
    /// Send RPC using Multisynq
    /// </summary>
    [SynqRPC]
    public void RPC(string methodName, object target, params object[] parameters)
    {
        Log($"Multisynq RPC called: {methodName} on {target?.GetType().Name}");
        SendMultisyncEvent(methodName, parameters);
    }
    
    /// <summary>
    /// Join or create Multisynq session
    /// </summary>
    [SynqRPC]
    public void JoinOrCreateRoom(string roomName, int maxPlayers = 2)
    {
        Log($"Joining/Creating Multisynq session: {roomName}");
        JoinMultisyncSession(roomName, maxPlayers);
    }
    
    /// <summary>
    /// Leave current Multisynq session
    /// </summary>
    [SynqRPC]
    public void LeaveRoom()
    {
        Log("Leaving Multisynq session");
        LeaveMultisyncSession();
    }
    
    /// <summary>
    /// Get local player ID
    /// </summary>
    public string GetLocalPlayerId()
    {
        return localPlayerId;
    }
    
    /// <summary>
    /// Check if connected
    /// </summary>
    public bool IsConnected()
    {
        return isConnected;
    }
    
    /// <summary>
    /// Get current room ID
    /// </summary>
    public string GetCurrentRoomId()
    {
        return currentRoomId;
    }
    
    #endregion
    
    private void SimulateMultisyncConnection()
    {
        isConnected = true;
        localPlayerId = "MultisynqPlayer_" + Random.Range(1000, 9999);
        OnConnectedToMaster?.Invoke();
        Log($"Simulated Multisynq connection established - Player ID: {localPlayerId}");
    }
    
    #region Multisync Implementation
    
    private void SendMultisyncEvent(string eventName, object[] parameters)
    {
        var eventData = new
        {
            eventName = eventName,
            parameters = parameters,
            senderId = localPlayerId,
            timestamp = Time.time
        };
        
        string jsonData = JsonUtility.ToJson(eventData);
        
        #if UNITY_WEBGL && !UNITY_EDITOR
        PublishMultisyncEventJS(eventName, jsonData);
        #else
        // Simulate event for testing
        Log($"Multisync event sent: {eventName}");
        #endif
    }
    
    private void JoinMultisyncSession(string sessionName, int maxPlayers)
    {
        currentRoomId = sessionName;
        
        #if UNITY_WEBGL && !UNITY_EDITOR
        JoinMultisyncSessionJS(sessionName, maxPlayers);
        #else
        // Simulate join for testing
        Invoke(nameof(SimulateMultisyncJoin), 0.5f);
        #endif
    }
    
    private void LeaveMultisyncSession()
    {
        #if UNITY_WEBGL && !UNITY_EDITOR
        LeaveMultisyncSessionJS();
        #endif
        
        currentRoomId = "";
    }
    
    private void SimulateMultisyncJoin()
    {
        OnJoinedRoom?.Invoke();
    }
    
    #endregion
    
    #region JavaScript Interop (WebGL)
    
    #if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void InitializeMultisyncJS(string apiKey, string appId);
    
    [DllImport("__Internal")]
    private static extern void JoinMultisyncSessionJS(string sessionName, int maxPlayers);
    
    [DllImport("__Internal")]
    private static extern void LeaveMultisyncSessionJS();
    
    [DllImport("__Internal")]
    private static extern void PublishMultisyncEventJS(string eventName, string jsonData);
    #endif
    
    // JavaScript callbacks (called from WebGL)
    public void OnMultisyncConnected()
    {
        isConnected = true;
        OnConnectedToMaster?.Invoke();
    }
    
    public void OnMultisyncDisconnected(string reason)
    {
        isConnected = false;
        OnDisconnected?.Invoke(reason);
    }
    
    public void OnMultisyncJoinedSession(string sessionId)
    {
        currentRoomId = sessionId;
        OnJoinedRoom?.Invoke();
    }
    
    public void OnMultisyncEventReceived(string eventData)
    {
        // Parse and handle received events
        Log($"Multisync event received: {eventData}");
    }
    
    #endregion
    
    #region Utility
    
    private void Log(string message)
    {
        if (enableLogs)
        {
            Debug.Log($"[NETWORK_WRAPPER] {message}");
        }
    }
    
    [ContextMenu("Test Connection")]
    public void TestConnection()
    {
        Log($"Testing connection - Connected: {isConnected}, Room: {currentRoomId}");
    }
    
    [ContextMenu("Test RPC")]
    public void TestRPC()
    {
        RPC("TestMethod", this, "test parameter");
    }
    
    #endregion
}
