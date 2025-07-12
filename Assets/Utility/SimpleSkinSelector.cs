using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;

public class SimpleSkinSelector : MonoBehaviour
{
    [SerializeField] private GameObject skinPanel;
    [SerializeField] private Button[] skinButtons;
    [SerializeField] private string[] spriteNames;
    
    private PhotonView localTankView;
    
    // Clé de sauvegarde PlayerPrefs
    private const string SELECTED_SKIN_KEY = "SelectedTankSkin";
    
    private void Start()
    {
        // Cacher le panneau au démarrage
        if (skinPanel) skinPanel.SetActive(false);
        
        // Configurer les boutons
        for (int i = 0; i < skinButtons.Length && i < spriteNames.Length; i++)
        {
            int index = i;  // Pour capture dans lambda
            if (skinButtons[i] != null)
            {
                skinButtons[i].onClick.AddListener(() => SelectSkin(index));
            }
        }
    }
    
    private void OnEnable()
    {
        // S'abonner à l'événement d'instanciation de tank
        PhotonTankSpawner.OnTankSpawned += OnTankSpawned;
    }
    
    private void OnDisable()
    {
        // Se désabonner de l'événement
        PhotonTankSpawner.OnTankSpawned -= OnTankSpawned;
    }
    
    // Appelé quand on clique sur le bouton d'ouverture
    public void ToggleSkinPanel()
    {
        if (skinPanel)
            skinPanel.SetActive(!skinPanel.activeSelf);
    }
    
    private void SelectSkin(int index)
    {
        if (index < 0 || index >= spriteNames.Length) return;
        
        // Sauvegarder l'index du skin sélectionné
        PlayerPrefs.SetInt(SELECTED_SKIN_KEY, index);
        PlayerPrefs.Save();
        
        // Trouver notre tank
        if (localTankView == null)
        {
            GameObject[] tanks = GameObject.FindGameObjectsWithTag("Player");
            foreach (var tank in tanks)
            {
                PhotonView view = tank.GetComponent<PhotonView>();
                if (view && view.IsMine)
                {
                    localTankView = view;
                    break;
                }
            }
        }
        
        // Appliquer le changement à tous les joueurs
        if (localTankView != null)
        {
            TankAppearanceHandler handler = localTankView.GetComponent<TankAppearanceHandler>();
            if (handler != null)
            {
                // Debug.Log pour vérifier que la fonction est bien appelée
                Debug.Log($"[SKIN] Changement du skin vers {spriteNames[index]}");
                localTankView.RPC("ChangeTankSprite", RpcTarget.AllBuffered, spriteNames[index]);
            }
        }
        
        // Fermer le panneau après sélection
        if (skinPanel) skinPanel.SetActive(false);
    }
    
    // Appelée quand un nouveau tank est spawné
    private void OnTankSpawned(GameObject tank, PhotonView view)
    {
        if (view.IsMine)
        {
            localTankView = view;
            
            // Appliquer le skin sauvegardé au nouveau tank
            TankAppearanceHandler handler = tank.GetComponent<TankAppearanceHandler>();
            if (handler != null)
            {
                int savedSkinIndex = PlayerPrefs.GetInt(SELECTED_SKIN_KEY, 0);
                if (savedSkinIndex >= 0 && savedSkinIndex < spriteNames.Length)
                {
                    Debug.Log($"[SKIN] Application du skin sauvegardé: {spriteNames[savedSkinIndex]}");
                    view.RPC("ChangeTankSprite", RpcTarget.AllBuffered, spriteNames[savedSkinIndex]);
                }
            }
        }
    }
    
    // Méthode publique pour cacher le panel (à appeler depuis le lobby manager)
    public void HideSkinPanel()
    {
        if (skinPanel) skinPanel.SetActive(false);
    }
    
    // Méthode pour réinitialiser le selector quand on revient au lobby
    public void ResetSelector()
    {
        localTankView = null;
        if (skinPanel) skinPanel.SetActive(false);
    }
}