public class DummyVisualEffectManager : PlayerVisualEffectManager
{
    protected override void Start()
    {
        audioFeedback ??= GetComponent<PlayerAudioFeedback>();
    }
}
