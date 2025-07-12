using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.Numerics;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

[System.Serializable]
public class EvolutionData
{
    public bool authorized;
    public string walletAddress;
    public int score;
    public int currentLevel;
    public int requiredScore;
    public long nonce;
    public long timestamp;
    public string signature;
    public string error;
}

[System.Serializable]
public class NFTStateData
{
    public bool hasNFT;
    public int level;
    public string walletAddress;
    public int score;
    public int tokenId; 
}

[System.Serializable]
public class CanMintResponse
{
    public bool canMint;
    public string error;
}

public class ChogTanksNFTManager : MonoBehaviour
{
    [Header("Contract Settings")]
    private const string CONTRACT_ADDRESS = "0xefaa4c2a26b258378228e049fb6b2611caf7ee86"; 
    private const string MINT_NFT_SELECTOR = "0x14f710fe";     
    private const string EVOLVE_NFT_SELECTOR = "0x46abdccf";    
    private const string GET_LEVEL_SELECTOR = "0x86481d40";    
    private const string CAN_MINT_NFT_SELECTOR = "0x13d0a65a"; 
    private const string UPDATE_SCORE_SELECTOR = "0x24bbd84c";
    private const string PLAYER_NFT_SELECTOR = "0x82e40315"; 
    
    [Header("UI References")]
    public UnityEngine.UI.Button evolutionButton;
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI levelText;
    
    private string currentPlayerWallet = "";
    private bool isProcessingEvolution = false;
    private NFTStateData currentNFTState = new NFTStateData();

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void CheckEvolutionEligibilityJS(string walletAddress);
    
    [DllImport("__Internal")]
    private static extern void GetNFTStateJS(string walletAddress);
    
    [DllImport("__Internal")]
    private static extern void UpdateNFTLevelJS(string walletAddress, int newLevel);
    
    [DllImport("__Internal")]
    private static extern void CanMintNFTJS(string walletAddress, string callbackMethod);
#endif

    void Start()
    {
        RefreshWalletAddress();
        
        if (evolutionButton != null)
        {
            evolutionButton.onClick.AddListener(OnEvolutionButtonClicked);
        }
        
        UpdateStatusUI("Initializing...");
        
        if (!string.IsNullOrEmpty(currentPlayerWallet))
        {
            LoadNFTStateFromFirebase();
        }
    }

    void RefreshWalletAddress()
    {
        string walletAddress = string.Empty;
        
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
                    PlayerPrefs.SetString("walletAddress", appKitAddress);
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[NFT] Erreur AppKit: {ex.Message}");
        }
        
        if (string.IsNullOrEmpty(walletAddress))
        {
            string walletFromPrefs = PlayerPrefs.GetString("walletAddress", "");
            if (!string.IsNullOrEmpty(walletFromPrefs))
            {
                walletAddress = walletFromPrefs;
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
                Debug.LogWarning($"[NFT] Erreur PlayerSession: {ex.Message}");
            }
        }
        
        currentPlayerWallet = walletAddress;
        
        if (!string.IsNullOrEmpty(currentPlayerWallet))
        {
        }
        else
        {
            Debug.LogError("[NFT] Aucun wallet connecté détecté");
        }
    }

    private void LoadNFTStateFromFirebase()
    {
        if (string.IsNullOrEmpty(currentPlayerWallet))
        {
            Debug.LogError("[NFT] No wallet address to load NFT state");
            return;
        }

        UpdateStatusUI("Loading NFT state...");
        
#if UNITY_WEBGL && !UNITY_EDITOR
        GetNFTStateJS(currentPlayerWallet);
#else
        var mockNFTState = new NFTStateData
        {
            hasNFT = false,
            level = 0,
            walletAddress = currentPlayerWallet,
            score = 150
        };
        OnNFTStateLoaded(JsonUtility.ToJson(mockNFTState));
#endif
    }

    public void OnNFTStateLoaded(string nftStateJson)
    {
        try
        {
            currentNFTState = JsonUtility.FromJson<NFTStateData>(nftStateJson);
            
            if (currentNFTState.hasNFT && currentNFTState.level > 0)
            {
                UpdateStatusUI($"You have a Level {currentNFTState.level} NFT");
                UpdateLevelUI(currentNFTState.level);
            }
            else
            {
                UpdateStatusUI("Ready to mint your first NFT!");
                UpdateLevelUI(0);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[NFT] Error parsing NFT state: {ex.Message}");
            UpdateStatusUI("Error loading NFT state");
            
            currentNFTState = new NFTStateData
            {
                hasNFT = false,
                level = 0,
                walletAddress = currentPlayerWallet,
                score = 0
            };
        }
    }

    public void OnEvolutionButtonClicked()
    {
        if (isProcessingEvolution)
        {
            return;
        }
        
        string walletAddress = "";
        try
        {
            if (Reown.AppKit.Unity.AppKit.IsInitialized && 
                Reown.AppKit.Unity.AppKit.IsAccountConnected && 
                Reown.AppKit.Unity.AppKit.Account != null)
            {
                walletAddress = Reown.AppKit.Unity.AppKit.Account.Address;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[NFT] AppKit error: {ex.Message}");
        }
        
        if (string.IsNullOrEmpty(walletAddress))
        {
            Debug.LogWarning("[NFT] No wallet detected!");
            UpdateStatusUI("Please connect your wallet first");
            return;
        }
        
        currentPlayerWallet = walletAddress;
        
        if (!currentNFTState.hasNFT || currentNFTState.level == 0)
        {
            isProcessingEvolution = true;
            UpdateStatusUI("Checking mint eligibility...");
            
#if UNITY_WEBGL && !UNITY_EDITOR
            CanMintNFTJS(currentPlayerWallet, "OnCanMintChecked");
#else
            OnCanMintChecked(JsonUtility.ToJson(new CanMintResponse { canMint = true }));
#endif
        }
        else
        {
            RequestEvolution();
        }
    }

    public void RequestEvolution()
    {
        isProcessingEvolution = true;
        UpdateStatusUI("Checking evolution eligibility...");
        
#if UNITY_WEBGL && !UNITY_EDITOR
        CheckEvolutionEligibilityJS(currentPlayerWallet);
#else
        var mockData = new EvolutionData
        {
            authorized = true,
            walletAddress = currentPlayerWallet,
            score = 250,
            currentLevel = currentNFTState.level,
            requiredScore = 100,
            nonce = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            signature = "0xmocksignature123"
        };
        OnEvolutionCheckComplete(JsonUtility.ToJson(mockData));
#endif
    }

    public void OnEvolutionCheckComplete(string evolutionDataJson)
    {
        try
        {
            var evolutionData = JsonUtility.FromJson<EvolutionData>(evolutionDataJson);
            
            if (evolutionData.authorized)
            {
                int targetLevel = CalculateTargetLevel(evolutionData.score, evolutionData.currentLevel);
                
                if (targetLevel > currentNFTState.level)
                {
                    UpdateStatusUI($"Evolution authorized to Level {targetLevel}! Score: {evolutionData.score}");
                    SendEvolveTransaction(targetLevel);
                }
                else
                {
                    UpdateStatusUI($"Already at maximum level for your score ({evolutionData.score} points)");
                    isProcessingEvolution = false;
                }
            }
            else
            {
                string errorMsg = !string.IsNullOrEmpty(evolutionData.error) ? 
                    evolutionData.error : 
                    $"Insufficient Score: {evolutionData.score}";
                    
                UpdateStatusUI($"Git Gud. {errorMsg}"); 
                isProcessingEvolution = false;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[NFT] Error parsing evolution data: {ex.Message}");
            UpdateStatusUI("Error checking evolution eligibility");
            isProcessingEvolution = false;
        }
    }

    private int CalculateTargetLevel(int score, int currentLevel = 1)
    {
        
        if (score >= 2 && currentLevel == 1)
        {
            return 2;
        }
        
        int maxLevel = 2;
        int threshold = 100; 
        
        while (score >= threshold)
        {
            maxLevel++;
            threshold += 100;
        }
        
        return Mathf.Max(currentLevel, maxLevel);
    }

    private async void SendMintTransaction()
    {
        try
        {
            UpdateStatusUI("Sending mint transaction...");
            
            if (Reown.AppKit.Unity.AppKit.IsInitialized && 
                Reown.AppKit.Unity.AppKit.IsAccountConnected)
            {
                string functionSelector = MINT_NFT_SELECTOR;
                string data = functionSelector;
                
                try 
                {
                    BigInteger mintPrice = BigInteger.Parse("1000000000000000"); // 0.001 ETH
                    
                    var result = await Reown.AppKit.Unity.AppKit.Evm.SendTransactionAsync(
                        CONTRACT_ADDRESS,  // to address
                        mintPrice,         // value (0.001 ETH sent)
                        data               // transaction data
                    );
                    
                    if (!string.IsNullOrEmpty(result))
                    {
                        OnMintTransactionSuccess(result);
                    }
                    else
                    {
                        OnTransactionError("Empty transaction result");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[NFT] Mint transaction failed: {ex.Message}");
                    OnTransactionError(ex.Message);
                }
            }
            else
            {
                Debug.LogError("[NFT] AppKit not initialized or wallet not connected");
                UpdateStatusUI("Connect your wallet first");
                isProcessingEvolution = false;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[NFT] Error sending mint transaction: {ex.Message}");
            UpdateStatusUI("Error sending mint transaction");
            isProcessingEvolution = false;
        }
    }

    private async void SendEvolveTransaction(int targetLevel)
    {
        try
        {
            UpdateStatusUI("Sending evolution transaction...");
            
            if (Reown.AppKit.Unity.AppKit.IsInitialized && 
                Reown.AppKit.Unity.AppKit.IsAccountConnected)
            {
                string functionSelector = EVOLVE_NFT_SELECTOR;
                string paddedLevel = targetLevel.ToString("X").PadLeft(64, '0');
                string data = functionSelector + paddedLevel;
                
                try 
                {
                    var result = await Reown.AppKit.Unity.AppKit.Evm.SendTransactionAsync(
                        CONTRACT_ADDRESS,  // to address
                        BigInteger.Zero,   // value (no ETH sent)
                        data               // transaction data
                    );
                    
                    if (!string.IsNullOrEmpty(result))
                    {
                        OnEvolveTransactionSuccess(result, targetLevel);
                    }
                    else
                    {
                        OnTransactionError("Empty transaction result");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[NFT] Evolution transaction failed: {ex.Message}");
                    OnTransactionError(ex.Message);
                }
            }
            else
            {
                Debug.LogError("[NFT] AppKit not initialized or wallet not connected");
                UpdateStatusUI("Connect your wallet first");
                isProcessingEvolution = false;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[NFT] Error sending evolution transaction: {ex.Message}");
            UpdateStatusUI("Error sending transaction");
            isProcessingEvolution = false;
        }
    }

    private void OnMintTransactionSuccess(string transactionHash)
    {
        try
        {
            string displayHash = string.IsNullOrEmpty(transactionHash) ? 
                "unknown" : 
                (transactionHash.Length > 10 ? transactionHash.Substring(0, 10) + "..." : transactionHash);
            
            UpdateNFTLevelInFirebase(1);
            
            currentNFTState.hasNFT = true;
            currentNFTState.level = 1;
            
            UpdateStatusUI($"NFT minted successfully! TX: {displayHash}");
            UpdateLevelUI(1);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[NFT] Error in OnMintTransactionSuccess: {ex.Message}");
            UpdateStatusUI("Error processing mint result");
        }
        finally
        {
            isProcessingEvolution = false;
        }
    }

    private void OnEvolveTransactionSuccess(string transactionHash, int newLevel)
    {
        try
        {
            string displayHash = string.IsNullOrEmpty(transactionHash) ? 
                "unknown" : 
                (transactionHash.Length > 10 ? transactionHash.Substring(0, 10) + "..." : transactionHash);
            
            UpdateNFTLevelInFirebase(newLevel);
            
            currentNFTState.level = newLevel;
            
            UpdateStatusUI($"NFT evolved to Level {newLevel}! TX: {displayHash}");
            UpdateLevelUI(newLevel);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[NFT] Error in OnEvolveTransactionSuccess: {ex.Message}");
            UpdateStatusUI("Error processing evolution result");
        }
        finally
        {
            isProcessingEvolution = false;
        }
    }

    public async void OnMintSuccess(string transactionHash)
    {
        try
        {
            UpdateStatusUI("NFT créé avec succès! Récupération du tokenId...");
            
            await Task.Delay(3000); // 3 secondes
            
            int actualTokenId = await GetPlayerNFTTokenId(currentPlayerWallet);
            
            if (actualTokenId > 0)
            {
                currentNFTState.tokenId = actualTokenId;
                currentNFTState.hasNFT = true;
                currentNFTState.level = 1;  // Le niveau initial est 1
                
                string updateData = JsonUtility.ToJson(currentNFTState);
                UpdateNFTDataInFirebase(updateData);
                
                UpdateLevelUI(1);
                UpdateStatusUI($"NFT #{actualTokenId} créé avec succès!");
                
                ReadNFTLevelFromBlockchain();
            }
            else
            {
                Debug.LogWarning("[NFT] Failed to retrieve tokenId after mint");
                UpdateStatusUI("NFT créé, mais impossible de récupérer le tokenId");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[NFT] Error handling mint success: {ex.Message}");
            UpdateStatusUI("Erreur lors de la récupération des informations du NFT");
        }
    }

    private void UpdateNFTLevelInFirebase(int newLevel)
    {
        if (string.IsNullOrEmpty(currentPlayerWallet))
        {
            Debug.LogError("[NFT] Cannot update NFT level: currentPlayerWallet is empty!");
            return;
        }
        
#if UNITY_WEBGL && !UNITY_EDITOR
        UpdateNFTLevelJS(currentPlayerWallet, newLevel);
#else
        // Simulation en mode éditeur
        OnNFTLevelUpdated($"{newLevel}"); // Simulate successful update
#endif
    }

    public void OnNFTLevelUpdated(string levelStr)
    {
        try
        {
            if (string.IsNullOrEmpty(levelStr) || !int.TryParse(levelStr, out int level))
            {
                Debug.LogError($"[NFT] Invalid level value received: {levelStr}");
                level = 0; // Default to 0 if invalid
            }
            
            currentNFTState.level = level;
            currentNFTState.hasNFT = level > 0;
            
            UpdateLevelUI(level);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[NFT] Error in OnNFTLevelUpdated: {ex.Message}");
        }
    }
    
    public void OnNFTStateReceived(string levelStr) => OnNFTLevelUpdated(levelStr);
    
    public void OnEvolutionEligibilityChecked(string evolutionDataJson) => OnEvolutionCheckComplete(evolutionDataJson);
    
    public void OnCanMintChecked(string jsonResponse)
    {
        try
        {
            CanMintResponse response = JsonUtility.FromJson<CanMintResponse>(jsonResponse);
            
            if (response == null)
            {
                Debug.LogError("[NFT] Failed to parse CanMintResponse JSON");
                UpdateStatusUI("Error checking mint eligibility");
                isProcessingEvolution = false;
                return;
            }
            
            if (response.canMint)
            {
                SendMintTransaction();
            }
            else
            {
                string errorMsg = !string.IsNullOrEmpty(response.error) ? 
                    response.error : 
                    "This wallet already has an NFT";
                    
                UpdateStatusUI($"Cannot mint: {errorMsg}");
                isProcessingEvolution = false;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[NFT] Error in OnCanMintChecked: {ex.Message}");
            UpdateStatusUI("Error checking mint eligibility");
            isProcessingEvolution = false;
        }
    }

    public async Task<int> GetPlayerNFTTokenId(string walletAddress)
    {
        try
        {
            if (string.IsNullOrEmpty(walletAddress))
            {
                Debug.LogWarning("[NFT] Cannot get tokenId: Wallet address is empty");
                return 0;
            }
            
            if (Reown.AppKit.Unity.AppKit.IsInitialized && 
                Reown.AppKit.Unity.AppKit.IsAccountConnected)
            {
                try
                {
                    string abi = "function playerNFT(address) view returns (uint256)";
                    
                    var tokenId = await Reown.AppKit.Unity.AppKit.Evm.ReadContractAsync<int>(
                        CONTRACT_ADDRESS,
                        abi,
                        "playerNFT",
                        new object[] { walletAddress }
                    );
                    
                    return tokenId;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[NFT] Error calling playerNFT: {ex.Message}");
                }
            }
            
            return 0; 
        }
        catch (Exception ex)
        {
            return 0;
        }
    }

    public async void ReadNFTLevelFromBlockchain()
    {
        try
        {
            UpdateStatusUI("Vérification du NFT sur la blockchain...");
            
            if (string.IsNullOrEmpty(currentPlayerWallet))
            {
                UpdateStatusUI("Wallet non connecté");
                return;
            }
            
            if (Reown.AppKit.Unity.AppKit.IsInitialized && 
                Reown.AppKit.Unity.AppKit.IsAccountConnected)
            {
                int tokenId = await GetPlayerNFTTokenId(currentPlayerWallet);
                
                if (tokenId <= 0)
                {
                    UpdateStatusUI("Aucun NFT détecté");
                    UpdateLevelUI(0);
                    return;
                }
                
                currentNFTState.tokenId = tokenId;
                
                
                try 
                {
                    string abi = "function getLevel(uint256) view returns (uint256)";
                    
                    var level = await Reown.AppKit.Unity.AppKit.Evm.ReadContractAsync<int>(
                        CONTRACT_ADDRESS,
                        abi,
                        "getLevel",
                        new object[] { tokenId }
                    );
                    
                    
                    currentNFTState.level = level;
                    currentNFTState.hasNFT = level > 0;
                    
                    UpdateLevelUI(level);
                    
                    if (level > 0) {
                        UpdateStatusUI($"NFT #{tokenId}, niveau {level} confirmé");
                    } else {
                        UpdateStatusUI("Aucun NFT trouvé on-chain");
                    }
                    
                    UpdateNFTLevelInFirebase(level);
                }
                catch (Exception ex)
                {
                    UpdateStatusUI("Erreur lors de la lecture du niveau");
                }
            }
            else
            {
                UpdateStatusUI("Wallet non connecté");
            }
        }
        catch (Exception ex)
        {
            UpdateStatusUI("Erreur lors de la lecture du niveau");
        }
    }

    private void UpdateNFTDataInFirebase(string data)
    {
        if (string.IsNullOrEmpty(currentPlayerWallet))
        {
            return;
        }
        
        
#if UNITY_WEBGL && !UNITY_EDITOR
#else
#endif
    }

    public void OnNFTLevelUpdateError(string error)
    {
        Debug.LogError($"[NFT] Error updating NFT level in Firebase: {error}");
    }

    private void OnTransactionError(string error)
    {
        UpdateStatusUI($"Transaction error: {error}");
        isProcessingEvolution = false;
    }

    void UpdateLevelUI(int level)
    {
        if (levelText != null)
        {
            levelText.text = level > 0 ? $"NFT Level: {level}" : "No NFT";
        }
    }

    void UpdateStatusUI(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    // Fonctions de test disponibles dans l'inspecteur
    [ContextMenu("Test Evolution")]
    public void TestEvolution()
    {
        if (!string.IsNullOrEmpty(currentPlayerWallet))
        {
            OnEvolutionButtonClicked();
        }
        else
        {
            Debug.LogWarning("[NFT] No wallet connected for test");
        }
    }

    [ContextMenu("Reload NFT State")]
    public void ReloadNFTState()
    {
        if (!string.IsNullOrEmpty(currentPlayerWallet))
        {
            LoadNFTStateFromFirebase();
        }
        else
        {
            Debug.LogWarning("[NFT] No wallet connected");
        }
    }
}
