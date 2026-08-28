using UnityEngine;

[DisallowMultipleComponent]
public sealed class ShopAudioFeedback : MonoBehaviour
{
    [SerializeField] private AK.Wwise.Event purchaseEvent;

    public void PlayPurchase()
    {
        if (purchaseEvent == null || !purchaseEvent.IsValid())
        {
            Debug.LogWarning("[ShopAudioFeedback] Purchase Event is not assigned.", this);
            return;
        }

        purchaseEvent.Post(gameObject);
    }
}
