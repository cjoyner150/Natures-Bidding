using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// PointerNPC — A 3D character (host/auctioneer) that speaks in world space.
/// </summary>
public class PointerNPC : MonoBehaviour
{
    public static PointerNPC Instance { get; private set; }

    [Header("Animation")]
    public Animator animator;
    public string idleTrigger = "Idle";
    public string celebrationOneTrigger = "Celebrate1";
    public string celebrationTwoTrigger = "Celebrate2";

    [Header("Speech Bubble")]
    public Transform speechBubbleAnchor;
    public Vector3 speechBubbleLocalOffset = new Vector3(0f, 2.6f, 0f);
    public Camera speechBubbleCamera;
    public Canvas speechBubbleCanvas;
    public CanvasGroup speechBubbleCanvasGroup;
    public Image speechBubbleBackground;
    public TMP_Text speechBubbleText;

    [Header("Dialogue")]
    [TextArea(2, 4)] public string openingInstruction = "Use the arrow keys to raise or lower your bid, then press Enter to submit.";
    [TextArea(2, 4)] public string itemRevealLine = "This is the {0}.";
    [TextArea(2, 4)] public string itemDescriptionLine = "{0}";
    [TextArea(2, 4)] public string biddingFinishedLine = "All players are done bidding.";
    [TextArea(2, 4)] public string winnerLine = "Player {0} won the {1}.";
    [TextArea(2, 4)] public string noWinnerLine = "No one won the {0}.";
    [TextArea(2, 4)] public string transitionLine = "Bidding is over. Head to the shop.";

    [Header("Speech Timing")]
    public float characterDelay = 0.02f;
    public float linePauseSeconds = 0.9f;
    public float bubbleScaleSpeed = 10f;

    private Coroutine _speechCoroutine;
    private bool _speechInitialized;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        EnsureSpeechBubbleExists();
        HideSpeechBubbleImmediate();
    }

    void LateUpdate()
    {
        UpdateSpeechBubbleTransform();
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>Return NPC to idle state.</summary>
    public void SetIdle()
    {
        if (animator != null)
        {
            animator.SetTrigger(idleTrigger);
        }
    }

    public void CelebrateOne()
    {
        if (animator == null) return;
        animator.ResetTrigger(idleTrigger);
        animator.ResetTrigger(celebrationTwoTrigger);
        animator.SetTrigger(celebrationOneTrigger);
    }

    public void CelebrateTwo()
    {
        if (animator == null) return;
        animator.ResetTrigger(idleTrigger);
        animator.ResetTrigger(celebrationOneTrigger);
        animator.SetTrigger(celebrationTwoTrigger);
    }

    public void CelebrateRandom()
    {
        if (Random.value < 0.5f)
            CelebrateOne();
        else
            CelebrateTwo();
    }

    public void SayOpeningInstructions()
    {
        SpeakSequence(openingInstruction);
    }

    public void SayItemReveal(string itemName, string itemDescription)
    {
        string revealLine = string.Format(itemRevealLine, itemName);
        string description = string.Format(itemDescriptionLine, itemDescription);
        SpeakSequence(revealLine, description);
    }

    public void SayBiddingFinished()
    {
        SpeakSequence(biddingFinishedLine);
    }

    public void SayWinner(string playerName, string itemName)
    {
        SpeakSequence(string.Format(winnerLine, playerName, itemName));
    }

    public void SayNoWinner(string itemName)
    {
        SpeakSequence(string.Format(noWinnerLine, itemName));
    }

    public void SayTransition()
    {
        SpeakSequence(transitionLine);
    }

    public void SpeakSequence(params string[] lines)
    {
        EnsureSpeechBubbleExists();

        if (_speechCoroutine != null)
            StopCoroutine(_speechCoroutine);

        _speechCoroutine = StartCoroutine(PlaySpeechSequence(lines));
    }

    public void HideSpeechBubble()
    {
        if (_speechCoroutine != null)
            StopCoroutine(_speechCoroutine);

        HideSpeechBubbleImmediate();
    }

    // ── Internal coroutines ────────────────────────────────────────────────────

    void EnsureSpeechBubbleExists()
    {
        if (_speechInitialized) return;
        _speechInitialized = true;

        if (speechBubbleAnchor == null)
        {
            GameObject anchorObject = new GameObject("SpeechBubbleAnchor");
            anchorObject.transform.SetParent(transform, false);
            anchorObject.transform.localPosition = speechBubbleLocalOffset;
            speechBubbleAnchor = anchorObject.transform;
        }

        if (speechBubbleCamera == null)
            speechBubbleCamera = Camera.main;

        if (speechBubbleCanvas == null)
        {
            GameObject canvasObject = new GameObject("SpeechBubbleCanvas");
            canvasObject.transform.SetParent(speechBubbleAnchor, false);
            canvasObject.transform.localPosition = Vector3.zero;
            speechBubbleCanvas = canvasObject.AddComponent<Canvas>();
            speechBubbleCanvas.renderMode = RenderMode.WorldSpace;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();

            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(6f, 2f);
            canvasRect.localScale = Vector3.one * 0.01f;

            GameObject backgroundObject = new GameObject("Background");
            backgroundObject.transform.SetParent(canvasObject.transform, false);
            speechBubbleBackground = backgroundObject.AddComponent<Image>();
            speechBubbleBackground.color = new Color(0f, 0f, 0f, 0.8f);

            RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;

            GameObject textObject = new GameObject("SpeechText");
            textObject.transform.SetParent(canvasObject.transform, false);
            speechBubbleText = textObject.AddComponent<TextMeshProUGUI>();
            speechBubbleText.alignment = TextAlignmentOptions.Center;
            speechBubbleText.fontSize = 36;
            speechBubbleText.color = Color.white;
            speechBubbleText.enableWordWrapping = true;

            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.06f, 0.1f);
            textRect.anchorMax = new Vector2(0.94f, 0.9f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
        }

        if (speechBubbleCanvasGroup == null)
            speechBubbleCanvasGroup = speechBubbleCanvas.GetComponent<CanvasGroup>() ?? speechBubbleCanvas.gameObject.AddComponent<CanvasGroup>();

        if (speechBubbleText == null)
            speechBubbleText = speechBubbleCanvas.GetComponentInChildren<TextMeshProUGUI>(true);

        if (speechBubbleBackground == null)
            speechBubbleBackground = speechBubbleCanvas.GetComponentInChildren<Image>(true);

        HideSpeechBubbleImmediate();
    }

    void UpdateSpeechBubbleTransform()
    {
        if (speechBubbleCanvas == null) return;

        Transform anchor = speechBubbleAnchor != null ? speechBubbleAnchor : transform;
        speechBubbleCanvas.transform.position = anchor.position;

        Camera faceCamera = speechBubbleCamera != null ? speechBubbleCamera : Camera.main;
        if (faceCamera != null)
        {
            Vector3 forward = speechBubbleCanvas.transform.position - faceCamera.transform.position;
            if (forward.sqrMagnitude > 0.0001f)
                speechBubbleCanvas.transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        }
    }

    IEnumerator PlaySpeechSequence(string[] lines)
    {
        if (speechBubbleCanvasGroup != null)
        {
            speechBubbleCanvasGroup.alpha = 1f;
            speechBubbleCanvasGroup.interactable = false;
            speechBubbleCanvasGroup.blocksRaycasts = false;
        }

        if (speechBubbleCanvas != null)
            speechBubbleCanvas.gameObject.SetActive(true);

        for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            string line = lines[lineIndex] ?? string.Empty;
            if (speechBubbleText == null) yield break;

            speechBubbleText.text = string.Empty;
            for (int characterIndex = 0; characterIndex < line.Length; characterIndex++)
            {
                speechBubbleText.text += line[characterIndex];
                yield return new WaitForSeconds(characterDelay);
            }

            if (lineIndex < lines.Length - 1)
                yield return new WaitForSeconds(linePauseSeconds);
        }

        _speechCoroutine = null;
    }

    void HideSpeechBubbleImmediate()
    {
        if (speechBubbleText != null)
            speechBubbleText.text = string.Empty;

        if (speechBubbleCanvasGroup != null)
            speechBubbleCanvasGroup.alpha = 0f;

        if (speechBubbleCanvas != null)
            speechBubbleCanvas.gameObject.SetActive(false);
    }

}