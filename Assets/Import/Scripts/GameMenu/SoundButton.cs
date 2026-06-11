using UnityEngine;
using UnityEngine.EventSystems;

public class UiButtonSound : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    [SerializeField] private AudioClip hoverSound;
    [SerializeField] private AudioClip clickSound;
    [Range(0f, 1f)] [SerializeField] private float volume = 0.6f;

    private static AudioSource _shared;

    private static AudioSource GetShared()
    {
        if (_shared != null)
            return _shared;

        var go = new GameObject("[UiAudioPlayer]");
        Object.DontDestroyOnLoad(go);
        _shared = go.AddComponent<AudioSource>();
        _shared.playOnAwake = false;
        AudioMixerRoutingUtility.BindSourceToSfx(_shared);
        return _shared;
    }

    public void OnPointerEnter(PointerEventData _)
    {
        if (hoverSound != null) GetShared().PlayOneShot(hoverSound, volume);
    }

    public void OnPointerClick(PointerEventData _)
    {
        if (clickSound != null) GetShared().PlayOneShot(clickSound, volume);
    }
}
