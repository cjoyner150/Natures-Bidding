using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Playables;

/// <summary>
/// PointerNPC — A 3D character (host/auctioneer) that speaks in world space.
/// </summary>
[RequireComponent(typeof(AuctioneerAudioFeedback))]
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
    //public Image speechBubbleBackground;
    public TMP_Text speechBubbleText;

    [Header("Dialogue")]
    [TextArea(2, 4)] public string greeting = "Welcome travelers.";
    [TextArea(2, 4)] public string  curseMention = "On the table you will find a terrible curse.";
    [TextArea(2, 4)] public string goldMention = "Each of you will sacrifice a secret amount of gold.";
    [TextArea(2, 4)] public string punishmentMention = "The punishment for the least sufficient sacrifice is severe.";
    [TextArea(2, 4)] public string itemRevealLine = "This is the {0}.";
    [TextArea(2, 4)] public string biddingFinishedLine = "Your sacrifices have been counted.";
    [TextArea(2, 4)] public string winnerLine = "Player {0} won the {1}.";
    [TextArea(2, 4)] public string noWinnerLine = "No one was cursed with the {0}.";
    [TextArea(2, 4)] public string transitionLine = "The goddess is satisfied. You may proceed.";

    [Header("Speech Timing")]
    public float characterDelay = 0.02f;
    public float linePauseSeconds = 0.9f;
    public float holdAfterLastLineSeconds = 3f;
    public float bubbleScaleSpeed = 10f;

    private Coroutine _speechCoroutine;
    private readonly Queue<string[]> _speechQueue = new Queue<string[]>();
    private bool _speechInitialized;
    private AuctioneerAudioFeedback _audioFeedback;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _audioFeedback = GetComponent<AuctioneerAudioFeedback>();
        EnsureSpeechBubbleExists();
        HideSpeechBubbleImmediate();
    }

    void LateUpdate()
    {
        //UpdateSpeechBubbleTransform();
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

    #region Cutscene Dialogue Hooks

    // These are referenced by signals on the timeline
    public void SayGreeting() => SpeakSequence(greeting);
    public void SayCurseMention() => SpeakSequence(curseMention);
    public void SayGoldMention() => SpeakSequence(goldMention);
    public void SayPunishmentMention() => SpeakSequence(punishmentMention);

    #endregion

    public void SayItemReveal(string itemName)
    {
        string revealLine = string.Format(itemRevealLine, itemName);
        SpeakSequence(revealLine);
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

        if (lines == null || lines.Length == 0)
            return;

        _speechQueue.Enqueue(lines);
        if (_speechCoroutine == null)
            _speechCoroutine = StartCoroutine(ProcessSpeechQueue());
    }

    public void HideSpeechBubble()
    {
        if (_speechCoroutine != null)
        {
            StopCoroutine(_speechCoroutine);
            _speechCoroutine = null;
        }

        _speechQueue.Clear();
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
            //speechBubbleBackground = backgroundObject.AddComponent<Image>();
            //speechBubbleBackground.color = new Color(0f, 0f, 0f, 0.8f);

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

        //if (speechBubbleBackground == null)
        //    speechBubbleBackground = speechBubbleCanvas.GetComponentInChildren<Image>(true);

        HideSpeechBubbleImmediate();
    }

    void UpdateSpeechBubbleTransform()
    {
        if (speechBubbleCanvas == null) return;

        //Transform anchor = speechBubbleAnchor != null ? speechBubbleAnchor : transform;
        //speechBubbleCanvas.transform.position = anchor.position;

        Camera faceCamera = speechBubbleCamera != null ? speechBubbleCamera : Camera.main;
        if (faceCamera != null)
        {
            Vector3 forward = speechBubbleCanvas.transform.position - faceCamera.transform.position;
            if (forward.sqrMagnitude > 0.0001f)
                speechBubbleCanvas.transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        }
    }

    IEnumerator ProcessSpeechQueue()
    {
        while (_speechQueue.Count > 0)
        {
            string[] lines = _speechQueue.Dequeue();
            yield return PlaySpeechSequence(lines);

            // Hold the finished text on screen. If another sequence arrives during the
            // hold, move on to it immediately instead of blanking the bubble first.
            float held = 0f;
            while (held < holdAfterLastLineSeconds && _speechQueue.Count == 0)
            {
                held += Time.deltaTime;
                yield return null;
            }

            if (_speechQueue.Count > 0)
                yield return new WaitForSeconds(linePauseSeconds);
        }

        _speechCoroutine = null;
        HideSpeechBubbleImmediate();
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
            yield return null;

            if (!string.IsNullOrWhiteSpace(line))
                PlayAuctioneerAudio();

            for (int characterIndex = 0; characterIndex < line.Length; characterIndex++)
            {
                speechBubbleText.text += line[characterIndex];
                yield return new WaitForSeconds(characterDelay);
            }

            if (lineIndex < lines.Length - 1)
            {
                GameLogger.Log(LogSeverity.Debug, $"Finished playing lines.");
                yield return new WaitForSeconds(linePauseSeconds);
                GameLogger.Log(LogSeverity.Debug, $"Finished waiting after playing lines.");
            }

        }
    }

    void PlayAuctioneerAudio()
    {
        if (_audioFeedback == null)
            _audioFeedback = GetComponent<AuctioneerAudioFeedback>();

        _audioFeedback?.PlayLine();
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
