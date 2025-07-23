using Multisynq;
using UnityEngine;
using TMPro;
using System.Collections;

public class PlayerNameDisplay : SynqBehaviour
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
    
    // Multisync compatibility properties
    public bool IsMine => true; // Placeholder for Multisync ownership
    public object Owner => this; // Placeholder for Multisync owner
    public int ActorNumber => GetInstanceID(); // Use instance ID as actor number
    
    private void Start()
    {
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
        
        // Multisync doesn't need event subscription for player properties
        
        StartCoroutine(RefreshPlayerNamePeriodically());
    }

    private void SetPlayerName()
    {
        if (nameText != null && Owner != null)
        {
            string playerName = $"Player {ActorNumber}";

            if (IsMine)
            {
                var nftManager = FindObjectOfType<ChogTanksNFTManager>();
                if (nftManager != null && nftManager.currentNFTState != null)
                {
                    int nftLevel = nftManager.currentNFTState.level;
                    playerName += $" lvl {nftLevel}";
                }
            }

            nameText.text = playerName;
            if (IsMine)
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
    
    // Multisync handles property updates automatically
    
    // Multisync handles events automatically
    
    private IEnumerator RefreshPlayerNamePeriodically()
    {
        while (true)
        {
            yield return new WaitForSeconds(5f); 
            SetPlayerName();
        }
    }
    
    void OnDestroy()
    {
        StopAllCoroutines();
    }

    public void SetLocalPlayerColor(Color color)
    {
        localPlayerColor = color;
        if (IsMine)
        {
            nameText.color = color;
        }
    }

    public void SetOtherPlayerColor(Color color)
    {
        otherPlayerColor = color;
        if (!IsMine)
        {
            nameText.color = color;
        }
    }
}