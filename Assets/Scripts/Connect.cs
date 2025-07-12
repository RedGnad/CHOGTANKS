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
            
            yield return new WaitForSeconds(1f);

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
                        dapp.OnPersonalSignButton();
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
    }
}