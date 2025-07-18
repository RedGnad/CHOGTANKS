using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using TMPro;
using System.Collections;

public class PlayerNameDisplay : MonoBehaviourPunCallbacks
{
    [Header("UI References")]
    public TextMeshProUGUI nameText;
    public Canvas nameCanvas;
    
    [Header("Position Settings")]
    public float heightOffset = 1.5f;
    
    [Header("Color Settings")]
    public Color localPlayerColor = Color.green; 
    public Color otherPlayerColor = Color.white;
    
    private bool isSubscribedToPlayerProps = false;
    
    private void Start()
    {
        // Initialiser l'affichage du nom
        SetPlayerName();
        
        if (nameCanvas != null)
        {
            nameCanvas.renderMode = RenderMode.WorldSpace;
            nameCanvas.worldCamera = Camera.main;
            nameCanvas.sortingOrder = 10;
            
            RectTransform canvasRect = nameCanvas.GetComponent<RectTransform>();
            canvasRect.localScale = Vector3.one * 0.02f; 
            canvasRect.sizeDelta = new Vector2(200, 50);
            
            nameCanvas.transform.localPosition = Vector3.zero;
        }
        
        if (nameText != null)
        {
            nameText.alignment = TextAlignmentOptions.Center;
        }
        
        UpdateTextPosition();
        
        // S'abonner aux mises à jour des propriétés des joueurs
        if (!isSubscribedToPlayerProps)
        {
            PhotonNetwork.NetworkingClient.EventReceived += OnPhotonEvent;
            isSubscribedToPlayerProps = true;
        }
        
        // Rafraichir périodiquement les noms pour capter les mises à jour de niveau
        StartCoroutine(RefreshPlayerNamePeriodically());
    }

    private void SetPlayerName()
    {
        if (nameText != null && photonView.Owner != null)
        {
            string playerName = photonView.Owner.NickName;
            if (string.IsNullOrEmpty(playerName))
            {
                playerName = $"Player {photonView.Owner.ActorNumber}";
            }

            // Pour le joueur local, on récupère son niveau et on le synchronise via CustomProperties
            if (photonView.IsMine)
            {
                var nftManager = FindObjectOfType<ChogTanksNFTManager>();
                if (nftManager != null && nftManager.currentNFTState != null)
                {
                    int nftLevel = nftManager.currentNFTState.level;
                    
                    // Synchronisation du niveau via les propriétés customisées du joueur Photon
                    ExitGames.Client.Photon.Hashtable playerProps = new ExitGames.Client.Photon.Hashtable();
                    playerProps["level"] = nftLevel;
                    PhotonNetwork.LocalPlayer.SetCustomProperties(playerProps);
                }
            }

            // Pour tous les joueurs, on récupère le niveau depuis les CustomProperties
            int playerLevel = 0;
            if (photonView.Owner.CustomProperties.ContainsKey("level"))
            {
                playerLevel = (int)photonView.Owner.CustomProperties["level"];
            }
            
            // Affichage du niveau pour tous les joueurs s'il est disponible
            if (playerLevel > 0)
            {
                playerName += $" lvl {playerLevel}";
            }

            nameText.text = playerName;
            if (photonView.IsMine)
            {
                nameText.color = localPlayerColor;
            }
            else
            {
                nameText.color = otherPlayerColor;
            }
        }
    }

    private void UpdateTextPosition()
    {
        if (nameText != null)
        {
            nameText.transform.localPosition = new Vector3(0, heightOffset * 150, 0); 
        }
    }

    private void LateUpdate()
    {
        UpdateTextPosition();
        
        if (nameCanvas != null && Camera.main != null)
        {
            nameCanvas.transform.LookAt(Camera.main.transform);
            nameCanvas.transform.Rotate(0, 180, 0);
        }
    }
    
    // Mettre à jour l'affichage lorsque les propriétés du joueur changent
    public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        if (photonView.Owner != null && targetPlayer.ActorNumber == photonView.Owner.ActorNumber)
        {
            // Mettre à jour le nom si les propriétés du joueur associé à ce photonView ont changé
            if (changedProps.ContainsKey("level"))
            {
                SetPlayerName();
            }
        }
    }
    
    // Pour gérer les mises à jour d'événements réseau
    private void OnPhotonEvent(ExitGames.Client.Photon.EventData photonEvent)
    {
        // Rafraîchir l'affichage quand il y a des mises à jour de propriétés (code 226)
        // 226 est le code standard Photon pour PropertyChanged
        if (photonEvent.Code == 226)
        {
            SetPlayerName();
        }
    }
    
    private IEnumerator RefreshPlayerNamePeriodically()
    {
        while (true)
        {
            yield return new WaitForSeconds(5f); // Rafraîchir toutes les 5 secondes
            SetPlayerName();
        }
    }
    
    void OnDestroy()
    {
        // Se désabonner lors de la destruction de l'objet
        if (isSubscribedToPlayerProps)
        {
            PhotonNetwork.NetworkingClient.EventReceived -= OnPhotonEvent;
            isSubscribedToPlayerProps = false;
        }
        StopAllCoroutines();
    }

    public void SetLocalPlayerColor(Color color)
    {
        localPlayerColor = color;
        if (photonView.IsMine)
        {
            nameText.color = color;
        }
    }

    public void SetOtherPlayerColor(Color color)
    {
        otherPlayerColor = color;
        if (!photonView.IsMine)
        {
            nameText.color = color;
        }
    }
}