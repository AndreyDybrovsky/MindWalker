using UnityEngine;

/// <summary>
/// Авто-момент ОКР: при входе игрока в зону запускает связанный <see cref="OCDMomentTrigger"/>
/// (без E). На триггере выключите Require Press E.
/// </summary>
[RequireComponent(typeof(Collider))]
public class OCDAutoMomentZone : MonoBehaviour
{
    [SerializeField] private OCDMomentTrigger moment;
    [SerializeField] private bool playOnce = true;
    [Tooltip("Доп. звук при касании (до затемнения). Пусто — только звуки момента.")]
    [SerializeField] private AudioClip touchSound;
    [SerializeField, Range(0f, 1f)] private float touchSoundVolume = 1f;
    [SerializeField] private AudioSource audioSource;

    private bool _played;

    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;

        if (audioSource == null)
            TryGetComponent(out audioSource);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_played && playOnce)
            return;

        if (!other.CompareTag("Player"))
            return;

        if (moment == null)
        {
            Debug.LogWarning($"OCDAutoMomentZone '{name}': не задан OCDMomentTrigger.", this);
            return;
        }

        if (touchSound != null)
        {
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f;
            }

            AudioMixerRoutingUtility.BindSourceToSfx(audioSource);
            audioSource.PlayOneShot(touchSound, touchSoundVolume);
        }

        if (!moment.CanBeginFromAutoZone)
            return;

        _played = true;
        moment.BeginMomentSequence();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.4f);
        Collider col = GetComponent<Collider>();
        if (col != null)
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);

        if (moment != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, moment.transform.position);
        }
    }
#endif
}
