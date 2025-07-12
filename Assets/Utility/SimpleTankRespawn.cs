using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class SimpleTankRespawn : MonoBehaviourPun, IMatchmakingCallbacks
{
    [Header("Respawn")]
    [SerializeField] private float respawnTime = 5f;
    [SerializeField] public GameObject gameOverUIPrefab; // Rendu public pour être accessible depuis TankComponentAdder
    
    private bool isDead = false;
    private GameObject gameOverUI;
    
    private List<Renderer> renderers;
    private List<Collider2D> colliders;
    private TankMovement2D movement;
    private TankShoot2D shooting;
    private TankHealth2D healthScript;
    
    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true).ToList();
        colliders = GetComponentsInChildren<Collider2D>(true).ToList();
        movement = GetComponent<TankMovement2D>();
        shooting = GetComponent<TankShoot2D>();
        healthScript = GetComponent<TankHealth2D>();
    }
    
    private void Start()
    {
        InitializeComponents();
        
        // S'inscrire aux événements de callback Photon
        PhotonNetwork.AddCallbackTarget(this);
        
        isDead = false;
        
    }
    
    private void InitializeComponents()
    {
        renderers = GetComponentsInChildren<Renderer>(true).ToList();
        colliders = GetComponentsInChildren<Collider2D>(true).ToList();
        healthScript = GetComponent<TankHealth2D>();
        
        if (renderers.Count == 0)
        {
            Debug.LogWarning($"[TANK] Aucun renderer trouvé pour {photonView.Owner?.NickName}");
        }
        
        if (colliders.Count == 0)
        {
            Debug.LogWarning($"[TANK] Aucun collider trouvé pour {photonView.Owner?.NickName}");
        }
    }
    
    private void OnDestroy()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
    }
    
    public void ResetTankState()
    {
        StopAllCoroutines();
        
        InitializeComponents();
        
        SetTankActive(true);
        
        isDead = false;
        
        if (gameOverUI != null)
        {
            Destroy(gameOverUI);
            gameOverUI = null;
        }
        
        if (healthScript != null)
        {
            healthScript.ResetHealth();
        }
        else
        {
            healthScript = GetComponent<TankHealth2D>();
            if (healthScript != null)
            {
                healthScript.ResetHealth();
            }
            else
            {
                Debug.LogWarning($"[TANK] Impossible de trouver TankHealth2D pour {photonView.Owner?.NickName}");
            }
        }
        
        
        if (TankComponentAdder.Instance != null && PhotonNetwork.IsConnected)
        {
            Debug.Log($"[TANK] Vérification des composants via TankComponentAdder pour {photonView.Owner?.NickName}");
        }
    }
    
    public void OnJoinedRoom()
    {
        
        StartCoroutine(DelayedReset());
    }

    private IEnumerator DelayedReset()
    {
        yield return new WaitForSeconds(0.2f);
        ResetTankState();
        
        if (!isDead)
        {
            SetTankActive(true);
        }
    }

    public void OnLeftRoom()
    {
        ResetTankState();
    }
    
    public void OnFriendListUpdate(List<FriendInfo> friendList) { /* Non utilisé */ }
    public void OnCreatedRoom() { /* Non utilisé */ }
    public void OnCreateRoomFailed(short returnCode, string message) { /* Non utilisé */ }
    public void OnJoinRoomFailed(short returnCode, string message) { /* Non utilisé */ }
    public void OnJoinRandomFailed(short returnCode, string message) { /* Non utilisé */ }
    
    [PunRPC]
    public void Die(int killerActorNumber)
    {
        if (isDead)
        {
            return;
        }
        isDead = true;
        
        
        if (renderers == null || renderers.Count == 0 || colliders == null || colliders.Count == 0)
        {
            InitializeComponents();
        }
        
        SetTankActive(false);
        
        if (renderers.Count > 0)
        {
            Debug.Log($"[TANK] Renderers désactivés pour {photonView.Owner?.NickName}, renderer[0].enabled = {renderers[0].enabled}");
        }
        else
        {
            Debug.LogWarning($"[TANK] Pas de renderers à désactiver pour {photonView.Owner?.NickName}");
        }
        
        if (PhotonNetwork.IsMasterClient && killerActorNumber > 0 && killerActorNumber != photonView.Owner.ActorNumber)
        {
            var scoreManager = ScoreManager.Instance;
            if (scoreManager != null)
            {
                scoreManager.AddKill(killerActorNumber);
            }
            else
            {
                Debug.LogError("[TANK] ScoreManager.Instance est null, impossible d'attribuer le kill");
            }
        }

        if (photonView.IsMine)
        {
            ShowGameOverUI();
            StartCoroutine(RespawnCoroutine());
        }
    }
    
    private void SetTankActive(bool active)
    {
        if (renderers == null || colliders == null)
        {
            InitializeComponents();
        }
        
        var validRenderers = renderers?.Where(r => r != null).ToList() ?? new List<Renderer>();
        var validColliders = colliders?.Where(c => c != null).ToList() ?? new List<Collider2D>();
        
        foreach (var rend in validRenderers)
        {
            try { rend.enabled = active; }
            catch (System.Exception ex) { Debug.LogError($"[TANK] Erreur lors de l'activation du renderer: {ex.Message}"); }
        }
        
        foreach (var col in validColliders)
        {
            try { col.enabled = active; }
            catch (System.Exception ex) { Debug.LogError($"[TANK] Erreur lors de l'activation du collider: {ex.Message}"); }
        }
        
        
        if (movement) movement.enabled = active;
        if (shooting) shooting.enabled = active;
        
        var health = GetComponent<TankHealth2D>();
        if (health) health.enabled = active;
    }
    
    private IEnumerator RespawnCoroutine()
    {
        yield return new WaitForSeconds(respawnTime);
        
        Vector3 respawnPosition = transform.position;
        var spawner = FindObjectOfType<PhotonTankSpawner>();
        if (spawner != null && spawner.spawnPoints != null && spawner.spawnPoints.Length > 0)
        {
            int spawnIdx = photonView.Owner.ActorNumber % spawner.spawnPoints.Length;
            respawnPosition = spawner.spawnPoints[spawnIdx].position;
        }
        
        photonView.RPC("RespawnRPC", RpcTarget.All, respawnPosition.x, respawnPosition.y, respawnPosition.z);
        
        if (gameOverUI != null)
        {
            Destroy(gameOverUI);
            gameOverUI = null;
        }
        
    }
    
    [PunRPC]
    public void RespawnRPC(float x, float y, float z, PhotonMessageInfo info)
    {
        
        SetTankActive(true);
        isDead = false;
        
        transform.position = new Vector3(x, y, z);
        
        var health = GetComponent<TankHealth2D>();
        if (health != null)
        {
            health.ResetHealth();
        }
        
    }
    
    private void ShowGameOverUI()
    {
        if (gameOverUIPrefab == null) return;
        
        if (gameOverUI != null)
        {
            Destroy(gameOverUI);
        }
        
        Camera mainCam = Camera.main;
        if (mainCam == null) return;
        
        gameOverUI = Instantiate(gameOverUIPrefab, mainCam.transform);
        
        RectTransform rt = gameOverUI.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.localPosition = new Vector3(0f, 0f, 1f);
            rt.localRotation = Quaternion.identity;
            float baseScale = 1f;
            float dist = Vector3.Distance(mainCam.transform.position, rt.position);
            float scaleFactor = baseScale * (dist / mainCam.orthographicSize) * 0.1f;
            rt.localScale = new Vector3(scaleFactor, scaleFactor, scaleFactor);
        }
        
        var controller = gameOverUI.GetComponent<GameOverUIController>();
        if (controller != null)
        {
            controller.ShowGameOver();
        }
    }
}
