using UnityEngine;

[DisallowMultipleComponent]
public sealed class AuctioneerAudioFeedback : MonoBehaviour
{
    [SerializeField] private AK.Wwise.Event speechEvent;

    public void PlayLine()
    {
        if (speechEvent == null || !speechEvent.IsValid())
        {
            Debug.LogWarning("[AuctioneerAudioFeedback] Speech Event is not assigned.", this);
            return;
        }

        speechEvent.Post(gameObject);
    }
}
