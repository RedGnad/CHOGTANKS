using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Reown.AppKit.Unity;

namespace Sample
{
    [RequireComponent(typeof(Button))]
    public class ConnectWalletButton : MonoBehaviour
    {
        [SerializeField] private Button connectButton;

        private void Awake()
        {
            if (connectButton == null)
                connectButton = GetComponent<Button>();

            connectButton.interactable = true;
            connectButton.onClick.AddListener(OnConnectClicked);
        }

        private async void OnConnectClicked()
        {
            
            try
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    return;
                }
                
                if (AppKit.IsInitialized)
                {
                    Debug.Log("[Connect] AppKit already initialized");
                }
#endif

                if (!AppKit.IsInitialized)
                {
                    Debug.Log("[Connect] AppKit non initialisé, tentative d'initialisation...");
                    await AppKitInit.TryInitializeAsync();
                    await System.Threading.Tasks.Task.Delay(500);
                    Debug.Log("[Connect] AppKit initialisé avec succès");
                }

                string initialAddress = "";
                try
                {
                    if (AppKit.IsInitialized && AppKit.IsAccountConnected && AppKit.Account != null)
                    {
                        initialAddress = AppKit.Account.Address ?? "";
                    }
                }
                catch (System.Exception accountEx)
                {
                    initialAddress = "";
                }
                
                
                try
                {
                    AppKit.OpenModal();
                    StartCoroutine(WaitForModalCloseAndSign(initialAddress));
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[Connect] Erreur ouverture modal : {e.Message}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Connect] ERREUR CRITIQUE : {e}");
            }
        }

        private IEnumerator WaitForModalCloseAndSign(string initialAddress)
        {
            
            float timeout = Time.time + 10f;
            while (!AppKit.IsModalOpen && Time.time < timeout)
            {
                yield return null;
            }
            
            if (!AppKit.IsModalOpen)
            {
                yield break;
            }
            
            timeout = Time.time + 300f;
            while (AppKit.IsModalOpen && Time.time < timeout)
            {
                yield return null;
            }

            if (AppKit.IsModalOpen)
            {
                yield break;
            }
            
            yield return new WaitForSeconds(0.2f);

            string finalAddress = "";
            try
            {
                if (AppKit.IsInitialized && AppKit.IsAccountConnected && AppKit.Account != null)
                {
                    finalAddress = AppKit.Account.Address ?? "";
                }
            }
            catch (System.Exception accountEx)
            {
                finalAddress = "";
            }
            
            
            if (string.IsNullOrEmpty(finalAddress))
            {
                yield break;
            }

            
            if (finalAddress != initialAddress)
            {
                
                try
                {
                    PlayerPrefs.SetString("walletAddress", finalAddress);
                    PlayerPrefs.Save();
                    
                    try
                    {
                        PlayerSession.SetWalletAddress(finalAddress);
                        Debug.Log("[Connect] Adresse enregistrée dans PlayerSession");
                    }
                    catch (System.Exception playerEx)
                    {
                        Debug.LogWarning($"[Connect] PlayerSession non disponible : {playerEx.Message}");
                    }

                    var dapp = FindObjectOfType<Dapp>();
                    if (dapp != null)
                    {
                        Debug.Log("[Connect] Préparation de la signature différée...");
                        StartCoroutine(TriggerPersonalSignAfterDelay(dapp));
                    }
                    else
                    {
                        Debug.LogError("[Connect] ERREUR: Aucun composant Dapp trouvé");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[Connect] ERREUR lors du traitement : {e}");
                }
            }
            else
            {
                Debug.Log("[Connect] Aucun changement d'adresse détecté");
            }
        }
        
        // Fonction pour déclencher la signature après un délai suffisant
        private IEnumerator TriggerPersonalSignAfterDelay(Dapp dapp)
        {
            // Délai réduit à 1 seconde (ou moins si tu veux)
            yield return new WaitForSeconds(1f);
            
            // Attendre quelques frames supplémentaires pour s'assurer que tout est prêt
            for (int i = 0; i < 5; i++)
                yield return null;
            
            try
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                // Sur WebGL, utiliser directement l'API AppKit.Evm
                Debug.Log("[Connect] Utilisation directe de AppKit.Evm pour WebGL");
                
                string message = "Hello from Unity! (Request #1)";
                Debug.Log($"[Connect] Tentative de signature du message: {message}");
                
                // Appel asynchrone en "fire and forget"
                AppKit.Evm.SignMessageAsync(message);
#else
                // Sur les autres plateformes, utiliser la méthode standard
                Debug.Log("[Connect] Simulation de l'appui sur le bouton de signature personnelle...");
                dapp.OnPersonalSignButton();
#endif
                Debug.Log("[Connect] Traitement de signature terminé");
                
                // After personal sign is completed, refresh NFT verification
                var nftVerification = FindObjectOfType<NFTVerification>();
                if (nftVerification != null)
                {
                    nftVerification.ForceNFTCheck();
                }
            }
            catch (System.Exception signEx)
            {
                Debug.LogWarning($"[Connect] Exception lors de la signature, mais continuons : {signEx.Message}");
                // Ne pas laisser cette exception crasher l'application
            }
        }
    }
}