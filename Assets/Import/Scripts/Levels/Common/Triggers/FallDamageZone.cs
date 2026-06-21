using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class FallDamageZone : MonoBehaviour
{
    [SerializeField] private Transform respawnPoint;
    [SerializeField] private float damage = 25f;
    [SerializeField] private AudioClip fallSound;
    [SerializeField] private float fadeOutDuration = 0.4f;
    [SerializeField] private float fadeInDuration = 0.5f;

    private AudioSource _audio;
    private bool _recovering;

    private void Awake()
    {
        _audio = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake = false;
        AudioMixerRoutingUtility.BindSourceToSfx(_audio);

        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_recovering) return;
        if (!other.CompareTag("Player")) return;

        StartCoroutine(RecoverSequence());
    }

    private IEnumerator RecoverSequence()
    {
        _recovering = true;

        if (fallSound != null)
            _audio.PlayOneShot(fallSound);

        var fade = ScreenFadeUtility.EnsureFadeCanvasGroup();
        ScreenFadeUtility.PrepareForFade(fade);
        yield return StartCoroutine(ScreenFadeRunner.FadeToBlack(fadeOutDuration, fade));

        if (respawnPoint != null)
            PlayerTeleportUtility.TeleportTo(respawnPoint, false);

        if (PlayerTeleportUtility.TryGetPlayerBody(out var body))
        {
            var health = body.GetComponentInParent<PlayerHealth>()
                      ?? body.GetComponent<PlayerHealth>();
            health?.TakeDamage(damage);
        }

        yield return StartCoroutine(ScreenFadeRunner.FadeFromBlack(fadeInDuration, fade));

        _recovering = false;
    }
}
