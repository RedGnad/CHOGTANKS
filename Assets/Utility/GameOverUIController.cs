using TMPro;
using UnityEngine;
using System.Collections;
using Photon.Pun;

public class GameOverUIController : MonoBehaviourPunCallbacks
{
    [SerializeField] private TextMeshProUGUI gameOverText;
    [SerializeField] private TextMeshProUGUI winText;
    [SerializeField] private TextMeshProUGUI winnerText;
    [SerializeField] private TextMeshProUGUI countdownText; // Nouveau texte pour le compte à rebours
    
    // Alternative si le glisser-déposer ne fonctionne pas
    public void SetCountdownText(TextMeshProUGUI text)
    {
        countdownText = text;
    }

    private void Awake()
    {
        if (gameOverText != null) gameOverText.gameObject.SetActive(false);
        if (winText      != null) winText.gameObject.SetActive(false);
        if (winnerText   != null) winnerText.gameObject.SetActive(false);
        if (countdownText != null) countdownText.gameObject.SetActive(false);
    }

    public void ShowGameOver()
    {
        if (gameOverText != null) gameOverText.gameObject.SetActive(true);
        if (winText      != null) winText.gameObject.SetActive(false);
        if (winnerText   != null) winnerText.gameObject.SetActive(false); 
    }

    public void ShowWin(string winnerName = "")
    {
        if (gameOverText != null) gameOverText.gameObject.SetActive(false);
        if (winText      != null) 
        {
            winText.gameObject.SetActive(true);
            if (!string.IsNullOrEmpty(winnerName))
            {
                winText.text = $"You Win, {winnerName}!";
            }
        }
        if (winnerText   != null) winnerText.gameObject.SetActive(false); // NOUVEAU
    }

    public void ShowWinner(string winnerName)
    {
        if (gameOverText != null) gameOverText.gameObject.SetActive(false);
        if (winText      != null) winText.gameObject.SetActive(false);
        if (winnerText   != null) 
        {
            winnerText.gameObject.SetActive(true);
            winnerText.text = $"{winnerName} Wins!";
        }
        
        // Toujours lancer la coroutine pour le compte à rebours, même si winnerText est null
        StartCoroutine(CountdownAndReturnToLobby(6));
    }
    
    private IEnumerator CountdownAndReturnToLobby(int seconds)
    {
        // Activer le texte du compte à rebours s'il existe
        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(true);
        }
        
        // Faire le compte à rebours
        for (int i = seconds; i > 0; i--)
        {
            if (countdownText != null)
            {
                countdownText.text = $"Match ended: returning to lobby in {i}...";
            }
            
            yield return new WaitForSeconds(1.0f);
        }
        
        // Dernière seconde
        if (countdownText != null)
        {
            countdownText.text = "Returning to lobby...";
        }
        
        // Revenir au lobby en quittant la room et en chargeant la scène du lobby
        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            Debug.Log("Automatic return to lobby");
            // On stocke le nom de la scène de lobby à charger après avoir quitté la room
            string lobbySceneName = "LobbyScene";
            
            // On s'abonne à l'événement de sortie de room pour charger la scène ensuite
            PhotonNetwork.AddCallbackTarget(new LobbySceneLoader(lobbySceneName));
            
            // On quitte la room (ce qui déclenchera OnLeftRoom)
            PhotonNetwork.LeaveRoom();
        }
    }
    
    // Classe auxiliaire pour gérer le chargement de la scène après avoir quitté la room
    private class LobbySceneLoader : MonoBehaviourPunCallbacks
    {
        private string _lobbySceneName;
        
        public LobbySceneLoader(string lobbySceneName)
        {
            _lobbySceneName = lobbySceneName;
        }
        
        public override void OnLeftRoom()
        {
            // On charge la scène du lobby quand la room est quittée
            UnityEngine.SceneManagement.SceneManager.LoadScene(_lobbySceneName);
            
            // On se désabonne après utilisation
            PhotonNetwork.RemoveCallbackTarget(this);
        }
    }
    
    public void HideAll()
    {
        if (gameOverText != null) gameOverText.gameObject.SetActive(false);
        if (winText      != null) winText.gameObject.SetActive(false);
        if (winnerText   != null) winnerText.gameObject.SetActive(false);
        if (countdownText != null) countdownText.gameObject.SetActive(false);
    }
}