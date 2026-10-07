using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class DeveloperConsole : MonoBehaviour
{
    [SerializeField] private bool doNotDestroyOnLoad;
    [SerializeField] private GameObject console;
    [SerializeField] private VerticalLayoutGroup logContainer;
    [SerializeField] private GameObject logEntryPrefab;
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private InputAction consoleToggleInput;

    public static Action OnConsoleOpened;
    public static Action OnConsoleClosed;

    private void Awake() => CommandRegistry.Initialize();

    private void Start()
    {
        if (doNotDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }

        console.SetActive(false);
        consoleToggleInput.Enable();

        consoleToggleInput.performed += OnConsoleToggled;
    }

    private void OnDestroy()
    {
        consoleToggleInput.performed -= OnConsoleToggled;

        consoleToggleInput.Disable();
    }

    private void OnConsoleToggled(InputAction.CallbackContext context)
    {
        if (console.activeSelf) 
            DisableConsole();
        else
            EnableConsole();
    }

    private void EnableConsole()
    {
        console.SetActive(true);
        inputField.ActivateInputField();

        OnConsoleOpened?.Invoke();
    }

    private void DisableConsole()
    {
        inputField.text = "";
        console.SetActive(false);

        OnConsoleClosed?.Invoke();
    }

    public void HandleInputSubmit()
    {
        string command = inputField.text;
        if (string.IsNullOrWhiteSpace(command)) return;

        GameLogger.Log(LogSeverity.Info, $"Command entered: {command}");

        bool succeeded = CommandRegistry.TryExecute(command, out string output);

        GameObject logEntry = Instantiate(logEntryPrefab, logContainer.transform);
        var text = logEntry.GetComponentInChildren<TextMeshProUGUI>();
        text.text = output;
        text.color = succeeded ? Color.white : new Color(1f, 0.45f, 0.45f);

        inputField.text = string.Empty;
        inputField.ActivateInputField();
    }

    public void HandleInputTextChange()
    {
        string prefix = inputField.text.Split(' ')[0];
        var matches = CommandRegistry.All.Where(c => c.Name.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase));
    }
}


