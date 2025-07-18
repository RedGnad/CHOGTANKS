using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Script simplifié pour gérer l'accès à des boutons spécifiques en fonction d'une whitelist d'adresses wallet
/// </summary>
public class SimpleWalletWhitelist : MonoBehaviour
{
    [Tooltip("Liste des adresses de wallet autorisées (sensible à la casse)")]
    [SerializeField] private List<string> whitelistedWallets = new List<string>();
    
    [Tooltip("Boutons qui ne seront accessibles qu'aux wallets whitelistés")]
    [SerializeField] private List<Button> restrictedButtons = new List<Button>();
    
    void Start()
    {
        // Désactiver les boutons au démarrage
        UpdateButtonsState(false);
        
        // Vérifier le wallet périodiquement
        InvokeRepeating(nameof(CheckWalletAccess), 0.5f, 2f);
    }
    
    /// <summary>
    /// Vérifie si le wallet actuel est dans la whitelist et met à jour l'état des boutons
    /// </summary>
    public void CheckWalletAccess()
    {
        string currentWallet = GetConnectedWallet();
        bool isWhitelisted = IsWalletWhitelisted(currentWallet);
        
        UpdateButtonsState(isWhitelisted);
    }
    
    /// <summary>
    /// Active ou désactive tous les boutons restreints
    /// </summary>
    private void UpdateButtonsState(bool enabled)
    {
        foreach (Button button in restrictedButtons)
        {
            if (button != null)
            {
                button.interactable = enabled;
            }
        }
    }
    
    /// <summary>
    /// Récupère l'adresse du wallet connecté
    /// </summary>
    private string GetConnectedWallet()
    {
        string wallet = string.Empty;
        
        // Essayer AppKit
        try 
        {
            if (Reown.AppKit.Unity.AppKit.IsInitialized && 
                Reown.AppKit.Unity.AppKit.IsAccountConnected && 
                Reown.AppKit.Unity.AppKit.Account != null)
            {
                wallet = Reown.AppKit.Unity.AppKit.Account.Address;
                if (!string.IsNullOrEmpty(wallet))
                {
                    return wallet;
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Whitelist] Erreur AppKit: {ex.Message}");
        }
        
        // Vérifier PlayerPrefs
        wallet = PlayerPrefs.GetString("walletAddress", "");
        if (!string.IsNullOrEmpty(wallet))
        {
            return wallet;
        }
        
        // Vérifier PlayerSession
        try
        {
            if (PlayerSession.IsConnected && !string.IsNullOrEmpty(PlayerSession.WalletAddress))
            {
                return PlayerSession.WalletAddress;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Whitelist] Erreur PlayerSession: {ex.Message}");
        }
        
        return "";
    }
    
    /// <summary>
    /// Vérifie si une adresse wallet est dans la whitelist
    /// </summary>
    private bool IsWalletWhitelisted(string walletAddress)
    {
        if (string.IsNullOrEmpty(walletAddress)) return false;
        
        return whitelistedWallets.Contains(walletAddress);
    }
    
    /// <summary>
    /// Ajoute une adresse à la whitelist
    /// </summary>
    public void AddWalletToWhitelist(string walletAddress)
    {
        if (!string.IsNullOrEmpty(walletAddress) && !whitelistedWallets.Contains(walletAddress))
        {
            whitelistedWallets.Add(walletAddress);
            CheckWalletAccess();
        }
    }
    
    /// <summary>
    /// Supprime une adresse de la whitelist
    /// </summary>
    public void RemoveWalletFromWhitelist(string walletAddress)
    {
        if (whitelistedWallets.Remove(walletAddress))
        {
            CheckWalletAccess();
        }
    }
    
    void OnDestroy()
    {
        CancelInvoke();
    }
}
