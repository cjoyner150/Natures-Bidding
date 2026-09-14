using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DeveloperConsole : MonoBehaviour
{
    [SerializeField] private VerticalLayoutGroup logContainer;
    [SerializeField] private GameObject logEntryPrefab;
    [SerializeField] private TMP_InputField inputField;
    

    private void Awake() => CommandRegistry.Initialize();

    public void HandleInputSubmit()
    {
        string command = inputField.text;
        if (string.IsNullOrWhiteSpace(command)) return;

        GameLogger.Log(LogSeverity.Info, $"Command entered: {command}");

        bool ok = CommandRegistry.TryExecute(command, out string output);

        GameObject logEntry = Instantiate(logEntryPrefab, logContainer.transform);
        var text = logEntry.GetComponentInChildren<TextMeshProUGUI>();
        text.text = output;
        text.color = ok ? Color.white : new Color(1f, 0.45f, 0.45f);

        inputField.text = string.Empty;
        inputField.ActivateInputField();
    }

    public void HandleInputTextChange()
    {
        string prefix = inputField.text.Split(' ')[0];
        var matches = CommandRegistry.All.Where(c => c.Name.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase));
    }

    private bool IsValidCommand(string command)
    {
        // Implement your command validation logic here
        return false;
    }
}


