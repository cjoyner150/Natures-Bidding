using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityUtils;

public class MenuNetworkErrorHandler : Singleton<MenuNetworkErrorHandler>
{
    private static readonly Queue<string> errorQueue = new();

    [SerializeField] RectTransform errorMessageLayoutGroup;
    [SerializeField] TextMeshProUGUI errorTextPrefab;
    [SerializeField] Image errorMessagePanel;
    [SerializeField] float autoHideSeconds = 8f;

    protected override void Awake()
    {
        if (HasInstance && Instance != this) { Destroy(gameObject); return; }

        base.Awake();
        errorMessagePanel.gameObject.SetActive(false);
    }

    private void Start() => ShowErrors();

    private void OnDestroy() => ClearVisuals();

    public static void ThrowMenuErrorToQueue(string msg)
    {
        if (string.IsNullOrEmpty(msg) || errorQueue.Contains(msg)) return;

        errorQueue.Enqueue(msg);
        if (HasInstance && Instance != null) 
            Instance.ShowErrors();
    }

    public void ShowErrors()
    {
        if (errorQueue.Count == 0) return;
        ClearVisuals();
        errorMessagePanel.gameObject.SetActive(true);

        int shownCount = 0;
        while (errorQueue.Count > 0 && shownCount < 5) { Create(errorQueue.Dequeue()); shownCount++; }
        if (errorQueue.Count > 0) { Create($"+{errorQueue.Count} more"); errorQueue.Clear(); }

        CancelInvoke(nameof(HideErrors));
        if (autoHideSeconds > 0) Invoke(nameof(HideErrors), autoHideSeconds);
    }

    public void HideErrors()   // TODO - hook to a close button
    {
        ClearVisuals();
        errorMessagePanel.gameObject.SetActive(false);
    }

    private void ClearVisuals() { if (errorMessageLayoutGroup) errorMessageLayoutGroup.DestroyChildren(); }
    private void Create(string message) => Instantiate(errorTextPrefab, errorMessageLayoutGroup).text = message;
}