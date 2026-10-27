using UnityEngine;

public class AudioVolumeControls : MonoBehaviour
{
    private static void SetVolume(string parameter, float value)
    {
        var result = AkUnitySoundEngine.SetRTPCValue(parameter, Mathf.Clamp(value, 0, 100f));
        if (result != AKRESULT.AK_Success)
            Debug.LogWarning($"Could not set {parameter}: {result}");
    }
    public static void SetMasterVolume(float value) => SetVolume("Master_Volume", value);
    public static void SetMusicVolume(float value) => SetVolume("Music_Volume", value);
    public static void SetUIVolume(float value) => SetVolume("UI_Volume", value);
    public static void SetSFXVolume(float value) => SetVolume("SFX_Volume", value);
    public static void SetAmbienceVolume(float value) => SetVolume("Ambience_Volume", value);


}
