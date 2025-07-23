using UnityEngine;
using UnityEngine.UI;
using Multisynq;

public class SimpleSkinSelector : MonoBehaviour
{
    [SerializeField] private GameObject skinPanel;
    [SerializeField] private Button[] skinButtons;
    [SerializeField] private string[] spriteNames;
    
    private TankHealth2D localTank;
    
    private const string SELECTED_SKIN_KEY = "SelectedTankSkin";
    
    private void Start()
    {
        if (skinPanel) skinPanel.SetActive(false);
        
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
        PhotonTankSpawner.OnTankSpawned += OnTankSpawned;
    }
    
    private void OnDisable()
    {
        PhotonTankSpawner.OnTankSpawned -= OnTankSpawned;
    }
    
    public void ToggleSkinPanel()
    {
        if (skinPanel)
            skinPanel.SetActive(!skinPanel.activeSelf);
    }
    
    private void SelectSkin(int index)
    {
        if (index < 0 || index >= spriteNames.Length) return;
        
        PlayerPrefs.SetInt(SELECTED_SKIN_KEY, index);
        PlayerPrefs.Save();
        
        if (localTank == null)
        {
            GameObject[] tanks = GameObject.FindGameObjectsWithTag("Player");
            foreach (var tank in tanks)
            {
                TankHealth2D tankHealth = tank.GetComponent<TankHealth2D>();
                if (tankHealth && tankHealth.IsMine)
                {
                    localTank = tankHealth;
                    break;
                }
            }
        }
        
        if (localTank != null)
        {
            TankAppearanceHandler handler = localTank.GetComponent<TankAppearanceHandler>();
            if (handler != null)
            {
                handler.ChangeTankSprite(spriteNames[index]);
            }
        }
        
        if (skinPanel) skinPanel.SetActive(false);
    }
    
    private void OnTankSpawned(GameObject tank)
    {
        TankHealth2D tankHealth = tank.GetComponent<TankHealth2D>();
        if (tankHealth && tankHealth.IsMine)
        {
            localTank = tankHealth;
            
            TankAppearanceHandler handler = tank.GetComponent<TankAppearanceHandler>();
            if (handler != null)
            {
                int savedSkinIndex = PlayerPrefs.GetInt(SELECTED_SKIN_KEY, 0);
                if (savedSkinIndex >= 0 && savedSkinIndex < spriteNames.Length)
                {
                    Debug.Log($"[SKIN] Application du skin sauvegardé: {spriteNames[savedSkinIndex]}");
                    handler.ChangeTankSprite(spriteNames[savedSkinIndex]);
                }
            }
        }
    }
    
    public void HideSkinPanel()
    {
        if (skinPanel) skinPanel.SetActive(false);
    }
}