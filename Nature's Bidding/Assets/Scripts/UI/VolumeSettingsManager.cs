using System.IO;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UI;

public class VolumeSettingsManager : MonoBehaviour
{
    [SerializeField] Slider masterVolumeSlider;
    [SerializeField] Slider musicVolumeSlider;
    [SerializeField] Slider sfxVolumeSlider;


    private static string savePath => Path.Combine(Application.persistentDataPath, "VolumeConfig.json");

    private void Start()
    {
        UpdateVolumeSliders();
        UpdateVolumeSettings(0);

        masterVolumeSlider.onValueChanged.AddListener(UpdateVolumeSettings);
        musicVolumeSlider.onValueChanged.AddListener(UpdateVolumeSettings);
        sfxVolumeSlider.onValueChanged.AddListener(UpdateVolumeSettings);
    }

    private void OnDestroy()
    {
        masterVolumeSlider.onValueChanged.RemoveListener(UpdateVolumeSettings);
        musicVolumeSlider.onValueChanged.RemoveListener(UpdateVolumeSettings);
        sfxVolumeSlider.onValueChanged.RemoveListener(UpdateVolumeSettings);
    }

    public static void Save(VolumeSettingsSave data)
    {
        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(savePath, json);
    }

    public static VolumeSettingsSave Load()
    {
        if (!File.Exists(savePath)) return null;
        string json = File.ReadAllText(savePath);
        return JsonUtility.FromJson<VolumeSettingsSave>(json);
    }

    private void UpdateVolumeSliders()
    {
        var data = Load();

        if (data != null)
        {
            masterVolumeSlider.value = data.MasterVolume;
            musicVolumeSlider.value = data.MusicVolume;
            sfxVolumeSlider.value = data.SfxVolume;
        }
        else
        {
            masterVolumeSlider.value = 1;
            musicVolumeSlider.value = 1;
            sfxVolumeSlider.value = 1;
        }
    }

    private void UpdateVolumeSettings(float _)
    {
        var data = new VolumeSettingsSave(
            masterVolumeSlider.value, 
            musicVolumeSlider.value, 
            sfxVolumeSlider.value
        );

        Save(data);

        // Sliders use 0-1; Wwise volume parameters use 0-100.
        AudioVolumeControls.SetMasterVolume(data.MasterVolume * 100f);
        AudioVolumeControls.SetMusicVolume(data.MusicVolume * 100f);
        AudioVolumeControls.SetSFXVolume(data.SfxVolume * 100f);
    }
}

[System.Serializable]
public class VolumeSettingsSave
{
    public float MasterVolume = 1.0f;
    public float MusicVolume = 1.0f;
    public float SfxVolume = 1.0f;

    public VolumeSettingsSave(float masterVolume, float musicVolume, float sfxVolume)
    {
        MasterVolume = masterVolume;
        MusicVolume = musicVolume;
        SfxVolume = sfxVolume;
    }
}
