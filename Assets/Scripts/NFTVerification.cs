using UnityEngine;
using TMPro;
using System;
using System.Numerics;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

[Serializable]
public class NFTCondition
{
    public enum Standard { ERC721, ERC1155 }
    public enum UnlockMode { AnyToken, SpecificToken }

    [Tooltip("ERC-721 ou ERC-1155")]
    public Standard standard = Standard.ERC1155;

    [Tooltip("AnyToken = n'importe quel jeton | SpecificToken = IDs listés")]
    public UnlockMode unlockMode = UnlockMode.AnyToken;

    [Tooltip("Adresse du contrat NFT")]
    public string contractAddress;

    [Tooltip("Liste des tokenIds (pour SpecificToken), ou vide pour AnyToken")]
    public List<string> tokenIds = new List<string>();

    [Tooltip("Texte à afficher si la condition est remplie")]
    public string successMessage = "NFT détecté !";
}

public class NFTVerification : MonoBehaviour
{
    [Header("Configuration")]
    [Tooltip("URL du RPC (par défaut: Monad Testnet)")]
    public string rpcUrl = "https://testnet-rpc.monad.xyz";

    [Tooltip("Liste des conditions NFT à vérifier")]
    public List<NFTCondition> conditions = new List<NFTCondition>();

    [Header("UI Elements")]
    [Tooltip("Texte UI (TMP) qui s'affichera uniquement si les conditions NFT sont remplies")]
    public TextMeshProUGUI statusText;
    
    [Tooltip("Message à afficher quand le NFT est détecté (laisser vide pour utiliser le message de la condition)")]
    public string customSuccessMessage = "";
    
    [Tooltip("Message à afficher quand aucun NFT n'est détecté")]
    public string noNFTOwnedMessage = "";

    const string SEL_ERC1155_BALANCE = "0x00fdd58e";  
    const string SEL_ERC721_BALANCE  = "0x70a08231";  
    const string SEL_ERC721_OWNER    = "0x6352211e";
    const string SIG_ERC1155_LOG     = "0xc3d58168c5ab16844f149d4b3945f6c6af9a1c1e0db3a9e6b207d0e2de5e2c8b";

    private string currentWallet;

    private void Start()
    {
        if (statusText != null)
        {
            statusText.gameObject.SetActive(false);
        }
        
        currentWallet = PlayerPrefs.GetString("walletAddress", "");
        Debug.Log($"[NFT] Adresse lue depuis PlayerPrefs: {currentWallet}");
        
        if (!string.IsNullOrEmpty(currentWallet))
        {
            Debug.Log("[NFT] Wallet connecté: " + currentWallet);
            StartCoroutine(CheckAllNFTs());
        }
        else
        {
            Debug.LogWarning("[NFT] Aucun wallet connecté trouvé dans PlayerPrefs");
            bool hasKey = PlayerPrefs.HasKey("walletAddress");
            Debug.Log($"[NFT] La clé walletAddress existe: {hasKey}");
        }
    }

    public void OnWalletConnected(string walletAddress)
    {
        currentWallet = walletAddress;
        PlayerPrefs.SetString("WalletAddress", walletAddress);
        UpdateStatus("Wallet connecté: " + walletAddress);
        StartCoroutine(CheckAllNFTs());
    }

    public void StartVerification()
    {
        if (string.IsNullOrEmpty(currentWallet))
        {
            UpdateStatus("Connect a Wallet First");
            return;
        }
        StartCoroutine(CheckAllNFTs());
    }

    IEnumerator CheckAllNFTs()
    {
        UpdateStatus("Verifying NFTs...", true);
        
        if (string.IsNullOrEmpty(currentWallet))
        {
            string error = "No Connected Wallet";
            UpdateStatus(error, true); 
            yield break;
        }

        foreach (var condition in conditions)
        {
            bool ownsNFT = false;
            
            if (condition.standard == NFTCondition.Standard.ERC1155)
            {
                if (condition.unlockMode == NFTCondition.UnlockMode.AnyToken)
                {
                    yield return StartCoroutine(CheckAnyTokenERC1155(
                        condition.contractAddress, 
                        currentWallet,
                        result => ownsNFT = result
                    ));
                }
                else
                {
                    foreach (var tokenId in condition.tokenIds)
                    {
                        yield return StartCoroutine(CheckBalance1155(
                            condition.contractAddress, 
                            currentWallet, 
                            tokenId,
                            result => ownsNFT |= result
                        ));
                        if (ownsNFT) break;
                    }
                }
            }
            else // ERC-721
            {
                if (condition.unlockMode == NFTCondition.UnlockMode.AnyToken)
                {
                    yield return StartCoroutine(CheckBalance721(
                        condition.contractAddress, 
                        currentWallet,
                        result => ownsNFT = result
                    ));
                }
                else
                {
                    foreach (var tokenId in condition.tokenIds)
                    {
                        yield return StartCoroutine(CheckOwnerOf721(
                            condition.contractAddress, 
                            currentWallet, 
                            tokenId,
                            result => ownsNFT |= result
                        ));
                        if (ownsNFT) break;
                    }
                }
            }

            if (ownsNFT)
            {
                yield return null;
                
                if (statusText != null)
                {
                    string finalMessage = string.IsNullOrEmpty(customSuccessMessage) ? condition.successMessage : customSuccessMessage;
                    statusText.text = finalMessage;
                    statusText.gameObject.SetActive(true);
                }
                
                yield break;
            }
        }

        if (!string.IsNullOrEmpty(noNFTOwnedMessage))
        {
            UpdateStatus(noNFTOwnedMessage, true);
        }
        else
        {
            if (statusText != null)
            {
                statusText.gameObject.SetActive(false);
            }
        }
    }

    IEnumerator CheckBalance1155(string contract, string wallet, string tokenId, Action<bool> cb)
    {
        string ownerHex = wallet.StartsWith("0x") ? wallet.Substring(2).PadLeft(64, '0') : wallet.PadLeft(64, '0');
        string idHex = BigInteger.Parse(tokenId).ToString("X").PadLeft(64, '0');
        string data = SEL_ERC1155_BALANCE + ownerHex + idHex;
        
        yield return CallRpc(contract, data, cb, res =>
        {
            var bal = BigInteger.Parse(res.Substring(2), System.Globalization.NumberStyles.HexNumber);
            return bal > 0;
        });
    }

    IEnumerator CheckBalance721(string contract, string wallet, Action<bool> cb)
    {
        string ownerHex = wallet.StartsWith("0x") ? wallet.Substring(2).PadLeft(64, '0') : wallet.PadLeft(64, '0');
        string data = SEL_ERC721_BALANCE + ownerHex + new string('0', 64);
        
        yield return CallRpc(contract, data, cb, res =>
        {
            var bal = BigInteger.Parse(res.Substring(2), System.Globalization.NumberStyles.HexNumber);
            return bal > 0;
        });
    }

    IEnumerator CheckOwnerOf721(string contract, string wallet, string tokenId, Action<bool> cb)
    {
        string idHex = BigInteger.Parse(tokenId).ToString("X").PadLeft(64, '0');
        string data = SEL_ERC721_OWNER + idHex;
        
        yield return CallRpc(contract, data, cb, res =>
        {
            string owner = "0x" + res.Substring(res.Length - 40);
            return string.Equals(owner, wallet, StringComparison.OrdinalIgnoreCase);
        });
    }

    IEnumerator CheckAnyTokenERC1155(string contract, string wallet, Action<bool> cb)
    {
        BigInteger latest = 0;
        yield return StartCoroutine(CallRpcRaw(new JObject{
            ["jsonrpc"]="2.0", ["method"]="eth_blockNumber", ["params"]=new JArray(), ["id"]=1
        }, json => {
            latest = BigInteger.Parse(
                JObject.Parse(json)["result"].Value<string>().Substring(2),
                System.Globalization.NumberStyles.HexNumber
            );
        }));

        string topicTo = "0x" + wallet.Substring(2).PadLeft(64, '0');
        BigInteger chunk = 100, start = BigInteger.Max(0, latest - 1000); 
        bool found = false;
        
        while (start <= latest && !found)
        {
            BigInteger end = BigInteger.Min(start + chunk - 1, latest);
            var filter = new JObject{
                ["address"] = contract,
                ["fromBlock"] = "0x" + start.ToString("X"),
                ["toBlock"] = "0x" + end.ToString("X"),
                ["topics"] = new JArray(SIG_ERC1155_LOG, null, null, topicTo)
            };
            
            yield return StartCoroutine(CallRpcRaw(new JObject{
                ["jsonrpc"]="2.0", ["method"]="eth_getLogs",
                ["params"]=new JArray(filter), ["id"]=1
            }, json => {
                var logs = JObject.Parse(json)["result"] as JArray;
                if (logs != null && logs.Count > 0) found = true;
            }));
            
            start += chunk;
        }
        
        cb(found);
    }

    IEnumerator CallRpc(string contract, string data, Action<bool> cb, Func<string, bool> parse)
    {
        var payload = new JObject(
            new JProperty("jsonrpc", "2.0"),
            new JProperty("method", "eth_call"),
            new JProperty("params", new JArray(
                new JObject(
                    new JProperty("to", contract), 
                    new JProperty("data", data)
                ),
                "latest"
            )),
            new JProperty("id", 1)
        );
        
        yield return CallRpcRaw(payload, json => {
            if (string.IsNullOrEmpty(json))
            {
                cb(false);
                return;
            }
            
            try
            {
                string res = JObject.Parse(json)["result"].Value<string>();
                cb(parse(res));
            }
            catch (Exception ex)
            {
                string errorMsg = "No NFT found";
                UpdateStatus(errorMsg, true); 
            }
        });
    }

    IEnumerator CallRpcRaw(JObject payload, Action<string> onResult)
    {
        using var uwr = new UnityEngine.Networking.UnityWebRequest(rpcUrl, "POST")
        {
            uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(
                System.Text.Encoding.UTF8.GetBytes(payload.ToString())
            ),
            downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer()
        };
        
        uwr.SetRequestHeader("Content-Type", "application/json");
        yield return uwr.SendWebRequest();
        
        if (uwr.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
        {
            onResult(null);
        }
        else
        {
            onResult(uwr.downloadHandler.text);
        }
    }

    private void UpdateStatus(string message, bool hideAfterDelay = false)
    {
        if (statusText != null)
        {
            CancelInvoke(nameof(HideStatus));
            
            statusText.text = message;
            statusText.gameObject.SetActive(true);
            
            if (hideAfterDelay)
            {
                Invoke(nameof(HideStatus), 3f);
            }
        }
    }
    
    private IEnumerator HideStatusAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (statusText != null)
        {
            statusText.gameObject.SetActive(false);
        }
    }
    
    private void HideStatus()
    {
        if (statusText != null)
        {
            statusText.gameObject.SetActive(false);
        }
    }

    public void DisconnectWallet()
    {
        currentWallet = "";
        PlayerPrefs.DeleteKey("WalletAddress");
        PlayerPrefs.Save();
        UpdateStatus("Déconnecté");
    }
    
    public void ForceNFTCheck()
    {
        
        if (!string.IsNullOrEmpty(currentWallet))
        {
            StartCoroutine(CheckAllNFTs());
            return;
        }
        
        string savedAddress = PlayerPrefs.GetString("walletAddress", "");
        
        if (!string.IsNullOrEmpty(savedAddress))
        {
            currentWallet = savedAddress;
            StartCoroutine(CheckAllNFTs());
        }
        else
        {
            bool hasKey = PlayerPrefs.HasKey("walletAddress");
            
            if (statusText != null)
            {
                statusText.text = "chog chest mega chad holder";
                statusText.gameObject.SetActive(true);
            }
        }
    }
}
