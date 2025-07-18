using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System;
using System.Linq;

[Serializable]
public class WhitelistedButtonRule
{
    [Tooltip("Le bouton à activer/désactiver en fonction de la whitelist")]
    public Button button;
    
    [Tooltip("Texte TMP à afficher quand le bouton est verrouillé (optionnel)")]
    public TextMeshProUGUI lockedText;
    
    [Tooltip("Message à afficher quand verrouillé")]
    public string lockedMessage = "Access denied";
    
    [Tooltip("Couleur du bouton quand débloqué")]
    public Color unlockedColor = Color.white;
    
    [Tooltip("Couleur du bouton quand verrouillé")]
    public Color lockedColor = Color.gray;
}

/// <summary>
/// Gère une whitelist d'adresses de wallet et contrôle l'accès à certains boutons en fonction de l'appartenance à cette liste.
/// </summary>
public class WalletWhitelistManager : MonoBehaviour
{
    [Header("Whitelist Configuration")]
    [Tooltip("Liste des adresses de wallet autorisées (sensible à la casse)")]
    [SerializeField] private List<string> whitelistedWallets = new List<string>();
    
    [Header("Button Configuration")]
    [Tooltip("Boutons à gérer selon la whitelist")]
    [SerializeField] private List<WhitelistedButtonRule> buttonRules = new List<WhitelistedButtonRule>();
    
    [Header("NFT Skin Buttons")]
    [Tooltip("Texte à afficher sur les boutons NFT quand pas de wallet connecté")]
    [SerializeField] private string notConnectedMessage = "Connect Wallet";
    
    [Tooltip("Liste des textes TMP des boutons de skins NFT")]
    [SerializeField] private List<TextMeshProUGUI> nftButtonTexts = new List<TextMeshProUGUI>();
    
    [Tooltip("Textes originaux des boutons NFT (remplis automatiquement)")]
    [SerializeField] private List<string> originalNftButtonTexts = new List<string>();
    
    private string currentWallet = "";
    private bool isWalletConnected = false;
    
    void Start()
    {
        // Sauvegarder les textes originaux des boutons NFT
        SaveOriginalButtonTexts();
        
        // Vérifier périodiquement le statut du wallet
        InvokeRepeating(nameof(CheckWalletStatus), 0.5f, 1.5f);
    }
    
    private void SaveOriginalButtonTexts()
    {
        originalNftButtonTexts.Clear();
        foreach (var textComponent in nftButtonTexts)
        {
            if (textComponent != null)
            {
                originalNftButtonTexts.Add(textComponent.text);
            }
            else
            {
                originalNftButtonTexts.Add("");
            }
        }
    }
    
    public void CheckWalletStatus()
    {
        bool wasConnected = isWalletConnected;
        string oldWallet = currentWallet;
        
        // Récupérer l'adresse du wallet connecté
        currentWallet = GetConnectedWallet();
        isWalletConnected = !string.IsNullOrEmpty(currentWallet);
        
        // Mettre à jour l'interface si l'état de connexion ou l'adresse a changé
        if (wasConnected != isWalletConnected || oldWallet != currentWallet)
        {
            UpdateButtonStates();
            UpdateNftButtonTexts();
        }
    }
    
    private string GetConnectedWallet()
    {
        string walletAddress = string.Empty;
        
        try
        {
            // Essayer d'obtenir l'adresse via AppKit
            if (Reown.AppKit.Unity.AppKit.IsInitialized && 
                Reown.AppKit.Unity.AppKit.IsAccountConnected && 
                Reown.AppKit.Unity.AppKit.Account != null)
            {
                walletAddress = Reown.AppKit.Unity.AppKit.Account.Address;
                if (!string.IsNullOrEmpty(walletAddress))
                {
                    return walletAddress;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[WalletWhitelist] Erreur AppKit: {ex.Message}");
        }
        
        // Fallback: vérifier PlayerPrefs
        walletAddress = PlayerPrefs.GetString("walletAddress", "");
        if (!string.IsNullOrEmpty(walletAddress))
        {
            return walletAddress;
        }
        
        // Fallback: vérifier PlayerSession
        try
        {
            if (PlayerSession.IsConnected && !string.IsNullOrEmpty(PlayerSession.WalletAddress))
            {
                return PlayerSession.WalletAddress;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[WalletWhitelist] Erreur PlayerSession: {ex.Message}");
        }
        
        return "";
    }
    
    private bool IsWalletWhitelisted(string walletAddress)
    {
        if (string.IsNullOrEmpty(walletAddress)) return false;
        
        // Vérifier si l'adresse est dans la whitelist (case sensitive)
        return whitelistedWallets.Contains(walletAddress);
    }
    
    public void UpdateButtonStates()
    {
        bool isWhitelisted = IsWalletWhitelisted(currentWallet);
        
        foreach (var rule in buttonRules)
        {
            if (rule.button == null) continue;
            
            // Si wallet connecté et whitelisté, activer le bouton
            bool unlocked = isWalletConnected && isWhitelisted;
            
            // Mettre à jour l'état du bouton
            rule.button.interactable = unlocked;
            
            // Mettre à jour la couleur du bouton
            Image buttonImage = rule.button.GetComponent<Image>();
            if (buttonImage != null)
            {
                buttonImage.color = unlocked ? rule.unlockedColor : rule.lockedColor;
            }
            
            // Mettre à jour le texte de verrouillage si présent
            if (rule.lockedText != null)
            {
                rule.lockedText.gameObject.SetActive(!unlocked);
                rule.lockedText.text = rule.lockedMessage;
            }
        }
    }
    
    public void UpdateNftButtonTexts()
    {
        for (int i = 0; i < nftButtonTexts.Count; i++)
        {
            TextMeshProUGUI textComponent = nftButtonTexts[i];
            if (textComponent == null) continue;
            
            if (isWalletConnected)
            {
                // Restaurer le texte original si le wallet est connecté
                textComponent.text = (i < originalNftButtonTexts.Count) ? originalNftButtonTexts[i] : "";
            }
            else
            {
                // Afficher "Connect Wallet" si pas de wallet connecté
                textComponent.text = notConnectedMessage;
            }
        }
    }
    
    /// <summary>
    /// Ajoute une adresse wallet à la whitelist à l'exécution
    /// </summary>
    /// <param name="walletAddress">Adresse à ajouter</param>
    public void AddWalletToWhitelist(string walletAddress)
    {
        if (string.IsNullOrEmpty(walletAddress)) return;
        
        if (!whitelistedWallets.Contains(walletAddress))
        {
            whitelistedWallets.Add(walletAddress);
            UpdateButtonStates();
        }
    }
    
    /// <summary>
    /// Supprime une adresse wallet de la whitelist à l'exécution
    /// </summary>
    /// <param name="walletAddress">Adresse à supprimer</param>
    public void RemoveWalletFromWhitelist(string walletAddress)
    {
        if (whitelistedWallets.Contains(walletAddress))
        {
            whitelistedWallets.Remove(walletAddress);
            UpdateButtonStates();
        }
    }
    
    void OnDestroy()
    {
        CancelInvoke(nameof(CheckWalletStatus));
    }
}
