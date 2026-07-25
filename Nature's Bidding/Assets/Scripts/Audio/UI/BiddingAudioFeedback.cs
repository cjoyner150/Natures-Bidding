using UnityEngine;

[DisallowMultipleComponent]
public sealed class BiddingAudioFeedback : MonoBehaviour
{
    [Header("Wwise")]
    [SerializeField] private AK.Wwise.Event bidAdjustEvent;
    [SerializeField] private AK.Wwise.Event bidSubmitEvent;
    [SerializeField] private AK.Wwise.Event bidRejectEvent;
    [SerializeField] private AK.Wwise.Switch bidUpSwitch;
    [SerializeField] private AK.Wwise.Switch bidDownSwitch;

    public void PlayBidUp()
    {
        PlayAdjustment(bidUpSwitch);
    }

    public void PlayBidDown()
    {
        PlayAdjustment(bidDownSwitch);
    }

    public void PlayBidSubmit()
    {
    if (bidSubmitEvent == null || !bidSubmitEvent.IsValid())
    {
        Debug.LogWarning("[BiddingAudioFeedback] Bid submit Event is not assigned.", this);
        return;
    }

    bidSubmitEvent.Post(gameObject);
    }

    public void PlayBidReject()
    {
        if (bidRejectEvent == null || !bidRejectEvent.IsValid())
        {
            Debug.LogWarning("[BiddingAudioFeedback] Bid reject Event is not assigned.", this);
            return;
        }

        bidRejectEvent.Post(gameObject);
    }

    private void PlayAdjustment(AK.Wwise.Switch directionSwitch)
    {
        if (directionSwitch == null || !directionSwitch.IsValid())
        {
            Debug.LogWarning("[BiddingAudioFeedback] Bid direction Switch is not assigned.", this);
            return;
        }

        if (bidAdjustEvent == null || !bidAdjustEvent.IsValid())
        {
            Debug.LogWarning("[BiddingAudioFeedback] Bid adjust Event is not assigned.", this);
            return;
        }

        directionSwitch.SetValue(gameObject);
        bidAdjustEvent.Post(gameObject);
    }
}
