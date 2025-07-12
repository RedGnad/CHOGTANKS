using UnityEngine;
using UnityEngine.UI;

public class SettingsPanelManager : MonoBehaviour
{
    [Header("Settings Panel")]
    public GameObject settingsPanel;
    public Button settingsButton;
    
    void Start()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
            
        if (settingsButton != null)
            settingsButton.onClick.AddListener(ToggleSettingsPanel);
    }
    
    public void ToggleSettingsPanel()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(!settingsPanel.activeSelf);
        }
    }
    
    public void HideSettingsPanel()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
    }
}