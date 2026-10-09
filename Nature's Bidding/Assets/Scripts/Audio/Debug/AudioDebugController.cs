using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum AudioDebugCategory
{
    Music,
    SoundEffect,
    Ambience,
    Dialogue,
    UI,
    Stop
}

[Serializable]
public sealed class AudioDebugStation
{
    public string stationLabel;
    [TextArea] public string description;
    public AudioDebugCategory category;
    public AK.Wwise.Event[] events = Array.Empty<AK.Wwise.Event>();
    public AK.Wwise.State[] states = Array.Empty<AK.Wwise.State>();
    public AK.Wwise.Switch[] switches = Array.Empty<AK.Wwise.Switch>();
    public bool startMusic;
    public bool silenceMusicAndAmbience;
    public bool stopAllBeforePlay;
}

[DisallowMultipleComponent]
[RequireComponent(typeof(AkGameObj))]
public sealed class AudioDebugController : MonoBehaviour
{
    [Header("Stations")]
    public AudioDebugStation[] stations = Array.Empty<AudioDebugStation>();

    [Header("Background audio")]
    public AK.Wwise.Event playMusicSystem;
    public AK.Wwise.Event stopMusicSystem;
    public AK.Wwise.Event stopForestAmbience;
    public AK.Wwise.Event stopLavaAmbience;
    public AK.Wwise.Event stopRockSlide;
    public AK.Wwise.State[] initialStates = Array.Empty<AK.Wwise.State>();
    public bool startMusicOnStart;
    public bool musicUnderSoundEffects;

    [Header("Panel")]
    public bool showPanel = true;

    private readonly List<string> lastResults = new List<string>();
    private readonly Dictionary<uint, string> selectedStates = new Dictionary<uint, string>();
    private readonly HashSet<uint> auditionPlayingIds = new HashSet<uint>();
    private Vector2 stationScroll;
    private Vector2 resultScroll;
    private bool musicSystemIsPlaying;
    private uint musicPlayingId;
    private string lastStationLabel = "No station selected";
    private string lastStationDescription;
    private bool initializationFinished;
    private GUIStyle wrappedLabel;

    public string LastStationLabel => lastStationLabel;
    public string LastResult => string.Join("\n", lastResults);
    public bool MusicIsPlaying => musicSystemIsPlaying;

    private IEnumerator Start()
    {
        float deadline = Time.realtimeSinceStartup + 15f;
        while (!AkUnitySoundEngine.IsInitialized() && Time.realtimeSinceStartup < deadline)
            yield return null;

        if (!RequireSoundEngine())
            yield break;

        ApplyStates(initialStates);
        initializationFinished = true;
        if (startMusicOnStart)
            StartMusic();
    }

    public void PlayStation(int stationIndex)
    {
        AudioDebugPlayerController walker = FindFirstObjectByType<AudioDebugPlayerController>();
        PlayStation(stationIndex, walker != null ? walker.gameObject : null);
    }

    public void PlayStation(int stationIndex, GameObject source)
    {
        lastResults.Clear();
        resultScroll = Vector2.zero;

        if (stations == null || stationIndex < 0 || stationIndex >= stations.Length || stations[stationIndex] == null)
        {
            RecordFailure("The station has no audio configuration.");
            return;
        }

        AudioDebugStation station = stations[stationIndex];
        lastStationLabel = station.stationLabel;
        lastStationDescription = station.description;

        if (!RequireSoundEngine())
            return;

        if (!initializationFinished)
        {
            ApplyStates(initialStates);
            initializationFinished = true;
        }

        if (source != null)
            transform.position = source.transform.position;

        if (station.category != AudioDebugCategory.Stop)
            StopPreviousAuditions();

        if (station.stopAllBeforePlay)
            StopAllAudioInternal();
        else if (station.silenceMusicAndAmbience &&
            (!musicUnderSoundEffects || station.category == AudioDebugCategory.Ambience))
            StopBackgroundAudio();

        // The builder lists Map and Players before Game_Phase so switch tracks
        // have a valid selection when the music phase changes.
        ApplyStates(station.states);
        ApplySwitches(station.switches);

        if (station.startMusic)
            StartMusic();

        if (station.events != null)
        {
            foreach (AK.Wwise.Event wwiseEvent in station.events)
            {
                if (IsSameEvent(wwiseEvent, playMusicSystem) && musicSystemIsPlaying)
                {
                    lastResults.Add("Music is already playing. Its current states remain selected.");
                    continue;
                }

                bool trackAudition = station.category == AudioDebugCategory.SoundEffect ||
                    station.category == AudioDebugCategory.Dialogue || station.category == AudioDebugCategory.UI;
                PostEvent(wwiseEvent, trackAudition);
            }
        }

        if (lastResults.Count == 0)
            lastResults.Add("The station has no assigned events, states, or switches.");
    }

    public void StopAllAudio()
    {
        lastResults.Clear();
        lastStationLabel = "Stop all";
        lastStationDescription = "Stop the debug sounds before choosing another station.";
        if (!RequireSoundEngine())
            return;

        StopAllAudioInternal();
    }

    public void RestartMusic()
    {
        lastResults.Clear();
        lastStationLabel = "Restart music";
        lastStationDescription = "Restart the music with the selected states.";
        if (!RequireSoundEngine())
            return;

        StopMusic();
        StartMusic();
    }

    private void StopAllAudioInternal()
    {
        // Every station posts on this emitter. StopAll targets that emitter,
        // then the authored stop events finish the background loops.
        AkUnitySoundEngine.StopAll(gameObject);
        auditionPlayingIds.Clear();
        musicSystemIsPlaying = false;
        musicPlayingId = AkUnitySoundEngine.AK_INVALID_PLAYING_ID;
        PostEvent(stopMusicSystem);
        PostEvent(stopForestAmbience);
        PostEvent(stopLavaAmbience);
        PostEvent(stopRockSlide);
        lastResults.Add("Stopped the debug emitter and its background loops.");
    }

    private void StopBackgroundAudio()
    {
        StopMusic();
        PostEvent(stopForestAmbience);
        PostEvent(stopLavaAmbience);
    }

    private void StopPreviousAuditions()
    {
        foreach (uint playingId in auditionPlayingIds)
            AkUnitySoundEngine.StopPlayingID(playingId);

        auditionPlayingIds.Clear();
    }

    private void StopMusic()
    {
        if (musicPlayingId != AkUnitySoundEngine.AK_INVALID_PLAYING_ID)
            AkUnitySoundEngine.StopPlayingID(musicPlayingId);

        if (musicSystemIsPlaying)
            PostEvent(stopMusicSystem);

        musicSystemIsPlaying = false;
        musicPlayingId = AkUnitySoundEngine.AK_INVALID_PLAYING_ID;
    }

    private void StartMusic()
    {
        if (musicSystemIsPlaying)
        {
            lastResults.Add("Music is already playing. Its current states remain selected.");
            return;
        }

        PostEvent(playMusicSystem);
    }

    private void ApplyStates(AK.Wwise.State[] statesToApply)
    {
        if (statesToApply == null)
            return;

        foreach (AK.Wwise.State state in statesToApply)
        {
            if (state == null || !state.IsValid())
            {
                RecordFailure("A Wwise state is not assigned.");
                continue;
            }

            AKRESULT result = AkUnitySoundEngine.SetState(state.GroupId, state.Id);
            string stateName = state.GroupWwiseObjectReference.ObjectName + "/" + state.ObjectReference.ObjectName;
            if (result == AKRESULT.AK_Success)
            {
                selectedStates[state.GroupId] = stateName;
                lastResults.Add(stateName + " selected.");
            }
            else
            {
                RecordFailure(stateName + " failed: " + result + ".");
            }
        }
    }

    private void ApplySwitches(AK.Wwise.Switch[] switchesToApply)
    {
        if (switchesToApply == null)
            return;

        foreach (AK.Wwise.Switch wwiseSwitch in switchesToApply)
        {
            if (wwiseSwitch == null || !wwiseSwitch.IsValid())
            {
                RecordFailure("A Wwise switch is not assigned.");
                continue;
            }

            AKRESULT result = AkUnitySoundEngine.SetSwitch(wwiseSwitch.GroupId, wwiseSwitch.Id, gameObject);
            string switchName = wwiseSwitch.GroupWwiseObjectReference.ObjectName + "/" + wwiseSwitch.ObjectReference.ObjectName;
            if (result == AKRESULT.AK_Success)
                lastResults.Add(switchName + " selected.");
            else
                RecordFailure(switchName + " failed: " + result + ".");
        }
    }

    private uint PostEvent(AK.Wwise.Event wwiseEvent, bool trackAudition = false)
    {
        if (wwiseEvent == null || !wwiseEvent.IsValid())
        {
            RecordFailure("A Wwise event is not assigned.");
            return AkUnitySoundEngine.AK_INVALID_PLAYING_ID;
        }

        uint playingId = wwiseEvent.Post(gameObject);
        string eventName = wwiseEvent.ObjectReference.ObjectName;
        if (playingId == AkUnitySoundEngine.AK_INVALID_PLAYING_ID)
        {
            RecordFailure(eventName + " failed to post. Check its SoundBank.");
            return playingId;
        }

        lastResults.Add(eventName + " posted. Playing ID " + playingId + ".");

        if (IsSameEvent(wwiseEvent, playMusicSystem))
        {
            musicSystemIsPlaying = true;
            musicPlayingId = playingId;
        }
        else if (IsSameEvent(wwiseEvent, stopMusicSystem))
        {
            musicSystemIsPlaying = false;
            musicPlayingId = AkUnitySoundEngine.AK_INVALID_PLAYING_ID;
        }
        else if (trackAudition && !eventName.StartsWith("Stop_", StringComparison.Ordinal))
        {
            auditionPlayingIds.Add(playingId);
        }

        return playingId;
    }

    private static bool IsSameEvent(AK.Wwise.Event first, AK.Wwise.Event second)
    {
        return first != null && first.IsValid() && second != null && second.IsValid() && first.Id == second.Id;
    }

    private bool RequireSoundEngine()
    {
        if (AkUnitySoundEngine.IsInitialized())
            return true;

        RecordFailure("Wwise is not initialized. Check the scene's Wwise initializer.");
        return false;
    }

    private void RecordFailure(string message)
    {
        lastResults.Add(message);
        Debug.LogWarning("[AudioDebugController] " + message, this);
    }

    private void OnDisable()
    {
        if (AkUnitySoundEngine.IsInitialized())
            AkUnitySoundEngine.StopAll(gameObject);

        auditionPlayingIds.Clear();
        musicSystemIsPlaying = false;
        musicPlayingId = AkUnitySoundEngine.AK_INVALID_PLAYING_ID;
    }

    private void OnGUI()
    {
        if (GUI.Button(new Rect(12f, 12f, 148f, 26f), showPanel ? "Hide audio panel" : "Show audio panel"))
            showPanel = !showPanel;

        if (!showPanel)
            return;

        float panelWidth = Mathf.Min(390f, Screen.width - 24f);
        float panelHeight = Mathf.Min(660f, Screen.height - 58f);
        if (wrappedLabel == null)
            wrappedLabel = new GUIStyle(GUI.skin.label) { wordWrap = true };

        GUILayout.BeginArea(new Rect(12f, 44f, panelWidth, panelHeight), GUI.skin.box);
        GUILayout.Label("Audio Debug");
        GUILayout.Label("WASD or arrows to move. Space to jump. R to return.", wrappedLabel);
        GUILayout.Label("Walk into a labeled box. Leave and return to play it again.", wrappedLabel);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Stop all"))
            StopAllAudio();
        if (GUILayout.Button("Restart music"))
            RestartMusic();
        GUILayout.EndHorizontal();
        musicUnderSoundEffects = GUILayout.Toggle(musicUnderSoundEffects, "Keep music and ambience during SFX");
        GUILayout.Label("Selected: " + lastStationLabel, wrappedLabel);
        if (!string.IsNullOrEmpty(lastStationDescription))
            GUILayout.Label(lastStationDescription, wrappedLabel);

        if (selectedStates.Count > 0)
            GUILayout.Label(string.Join(" | ", selectedStates.Values), wrappedLabel);

        GUILayout.Label(AkUnitySoundEngine.IsInitialized() ? "Wwise is ready." : "Waiting for Wwise.");
        resultScroll = GUILayout.BeginScrollView(resultScroll, GUILayout.Height(100f));
        foreach (string result in lastResults)
            GUILayout.Label(result, wrappedLabel);
        GUILayout.EndScrollView();

        stationScroll = GUILayout.BeginScrollView(stationScroll);
        if (stations != null)
        {
            for (int stationIndex = 0; stationIndex < stations.Length; stationIndex++)
            {
                AudioDebugStation station = stations[stationIndex];
                if (station == null)
                    continue;

                if (GUILayout.Button(new GUIContent(station.stationLabel, station.description)))
                    PlayStation(stationIndex);
            }
        }
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }
}
