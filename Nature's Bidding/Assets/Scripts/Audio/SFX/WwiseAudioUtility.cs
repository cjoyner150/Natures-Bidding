using UnityEngine;

public static class WwiseAudioUtility
{
    public static bool TryApplyScreenSpacePan(
        AK.Wwise.RTPC panRtpc,
        GameObject emitter,
        Vector3 soundWorldPosition,
        float edgePanValue,
        Camera cameraOverride = null)
    {
        if (panRtpc == null || !panRtpc.IsValid() || emitter == null)
        {
            return false;
        }

        Camera panCamera = cameraOverride != null ? cameraOverride : Camera.main;
        float panValue = 0f;

        if (panCamera != null)
        {
            Vector3 viewportPosition = panCamera.WorldToViewportPoint(soundWorldPosition);
            if (viewportPosition.z > 0f)
            {
                float normalizedScreenX = Mathf.Clamp(viewportPosition.x * 2f - 1f, -1f, 1f);
                panValue = normalizedScreenX * Mathf.Abs(edgePanValue);
            }
        }

        panRtpc.SetValue(emitter, panValue);
        return panCamera != null;
    }
}
