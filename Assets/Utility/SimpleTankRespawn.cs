using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Multisynq;
using UnityEngine;

public class SimpleTankRespawn : SynqBehaviour
{
    [Header("Respawn")]
    [SerializeField] private float respawnTime = 5f;
    [SerializeField] public GameObject gameOverUIPrefab; 
    
    [SynqVar] public bool isDead = false;
    private LobbyUI lobbyUI;
    
    // Multisync compatibility properties
    public object Owner => this; // Placeholder for Multisync owner
    public int ActorNumber => GetInstanceID(); // Use instance ID as actor number
    public bool IsMine => true; // Placeholder for Multisync ownership
    
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
        isDead = false;
    }
    
    private void InitializeComponents()
    {
        renderers = GetComponentsInChildren<Renderer>(true).ToList();
        colliders = GetComponentsInChildren<Collider2D>(true).ToList();
        healthScript = GetComponent<TankHealth2D>();
        
        if (renderers.Count == 0)
        {
            Debug.LogWarning($"[TANK] Aucun renderer trouvé pour {Owner?.ToString()}");
        }
        
        if (colliders.Count == 0)
        {
            Debug.LogWarning($"[TANK] Aucun collider trouvé pour {Owner?.ToString()}");
        }
    }
    
    public void ResetTankState()
    {
        StopAllCoroutines();
        
        InitializeComponents();
        
        bool shouldActivate = true;
        if (ScoreManager.Instance != null && ScoreManager.Instance.IsMatchEnded())
        {
            shouldActivate = false;
        }
        
        if (GameManager.Instance != null && GameManager.Instance.isGameOver)
        {
            shouldActivate = false;
        }
        
        if (shouldActivate)
        {
            SetTankActive(true);
        }
        
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
                Debug.LogWarning($"[TANK] Impossible de trouver TankHealth2D pour {Owner?.ToString()}");
            }
        }
    }
    
    public void OnJoinedRoom()
    {
        if (ScoreManager.Instance != null && ScoreManager.Instance.IsMatchEnded())
        {
            return;
        }
        
        if (GameManager.Instance != null && GameManager.Instance.isGameOver)
        {
            return;
        }
        
        StartCoroutine(DelayedReset());
    }

    private IEnumerator DelayedReset()
    {
        yield return new WaitForSeconds(0.2f);
        
        // Double vérification après le délai
        if (ScoreManager.Instance != null && ScoreManager.Instance.IsMatchEnded())
        {
            yield break;
        }
        
        if (GameManager.Instance != null && GameManager.Instance.isGameOver)
        {
            yield break;
        }
        
        ResetTankState();
        
        if (!isDead)
        {
            SetTankActive(true);
        }
    }

    private void OnDestroy()
    {
    }
    
    public void OnLeftRoom()
    {
        ResetTankState();
    }
    
    [SynqRPC]
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
        
        
        if (killerActorNumber > 0 && killerActorNumber != ActorNumber)
        {
            
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.AddKill(killerActorNumber, ActorNumber, killerActorNumber, ActorNumber);
            }
            else
            {
                Debug.LogError("[TANK] ScoreManager.Instance est null, impossible d'attribuer le kill");
            }
            
            
            KillNotificationManager killManager = KillNotificationManager.Instance;
            if (killManager != null)
            {
                killManager.ShowKillNotification(killerActorNumber, ActorNumber);
            }
            else
            {
                Debug.LogWarning("[TANK] KillNotificationManager.Instance est null, impossible d'afficher la notification de kill");
            }
        }

        if (IsMine)
        {
            ShowGameOverUI();
            
            if (GameManager.Instance == null || !GameManager.Instance.isGameOver)
            {
                StartCoroutine(RespawnCoroutine());
            }
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
        
        if (GameManager.Instance != null && GameManager.Instance.isGameOver)
        {
            yield break;
        }
        
        if (ScoreManager.Instance != null && ScoreManager.Instance.IsMatchEnded())
        {
            yield break;
        }
        
        Vector3 respawnPosition = transform.position;
        var spawner = FindObjectOfType<PhotonTankSpawner>();
        
        
        if (spawner != null && spawner.spawnPoints != null && spawner.spawnPoints.Length > 0)
        {
            
            int actorNumber = ActorNumber;
            int spawnIdx = 0;
            
            if (spawner.spawnPoints.Length > 1)
            {
                int originalIdx = Random.Range(0, spawner.spawnPoints.Length);
                spawnIdx = originalIdx;
                
                
            if (PhotonTankSpawner.lastSpawnPointByPlayer.ContainsKey(actorNumber))
            {
                int previousIdx = PhotonTankSpawner.lastSpawnPointByPlayer[actorNumber];
                
                int attempts = 0;
                while (spawnIdx == previousIdx && spawner.spawnPoints.Length > 1 && attempts < 10)
                {
                    spawnIdx = Random.Range(0, spawner.spawnPoints.Length);
                    attempts++;
                }
            }
            else
            {
                Debug.Log($"[RESPAWN] Aucun historique pour l'acteur {actorNumber}");
            }
            
            PhotonTankSpawner.lastSpawnPointByPlayer[actorNumber] = spawnIdx;
            }
            else
            {
                Debug.Log($"[RESPAWN] Owner: {Owner?.ToString() ?? "null"}");
            }
            
            respawnPosition = spawner.spawnPoints[spawnIdx].position;
            
            float offsetX = Random.Range(-0.5f, 0.5f);
            float offsetY = Random.Range(-0.5f, 0.5f);
            respawnPosition += new Vector3(offsetX, offsetY, 0);
            
        }
        else
        {
            Debug.LogWarning($"[RESPAWN] Spawner non trouvé ou pas de points de spawn!");
        }
        
        RespawnRPC(respawnPosition.x, respawnPosition.y, respawnPosition.z);
        
        if (gameOverUI != null)
        {
            Destroy(gameOverUI);
            gameOverUI = null;
        }
    }
    
    [SynqRPC]
    public void RespawnRPC(float x, float y, float z)
    {
        if (GameManager.Instance != null && GameManager.Instance.isGameOver)
        {
            return;
        }
        
        if (ScoreManager.Instance != null && ScoreManager.Instance.IsMatchEnded())
        {
            return;
        }
        
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
    
    [SynqRPC]
    public void ShowWinnerUI(string winnerName)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetGameOver();
        }
        
        if (gameOverUIPrefab == null) return;
        
        if (gameOverUI != null)
        {
            Destroy(gameOverUI);
        }
        
        Camera mainCam = Camera.main;
        if (mainCam == null) return;
        
        gameOverUI = Instantiate(gameOverUIPrefab, mainCam.transform);
        if (gameOverUI != null)
        {
            gameOverUI.tag = "GameOverUI";
        }
        
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
            controller.ShowWinner(winnerName);
        }
    }
}
