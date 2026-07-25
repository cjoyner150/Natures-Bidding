using TMPro;
using UnityEngine;

public class HostLevelSelectPanel : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI currentLevelText;

    private void OnEnable()
    {
        UpdateLevelSelectText();
    }

    public void UpdateLevelSelectText()
    {
        var level = PersistentGameStateManager.Instance.GetCurrentLevelSelectionType();
        currentLevelText.text = "Current: " + level.ToString();
    }

    public void SelectCombatLevel(int levelTypeIdx) {
        PersistentGameStateManager.Instance.SetLevelSelectionType((PersistentGameStateManager.CombatLevelSelectType)levelTypeIdx);
        UpdateLevelSelectText();
    }
}
