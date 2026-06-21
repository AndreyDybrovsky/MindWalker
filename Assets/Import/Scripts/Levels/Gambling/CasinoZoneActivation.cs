using UnityEngine;

/// <summary>
/// Вешается на BunkerTrigger. Когда игрок проходит в казино —
/// отключает уличных врагов, уличные LureZone и уличный ambient.
/// </summary>
public class CasinoZoneActivation : MonoBehaviour
{
    [SerializeField] private GameObject streetEnemies;
    [SerializeField] private GameObject streetSlots;
    [SerializeField] private AudioSource streetAmbient;
    [Tooltip("AudioSource-ы казино, которые запустятся при входе (CasinoBackGround, CasinoSound и т.д.)")]
    [SerializeField] private AudioSource[] casinoSources;

    private bool _activated;

    private void OnTriggerEnter(Collider other)
    {
        if (_activated) return;

        if (!other.CompareTag("Player") && !other.transform.root.CompareTag("Player"))
            return;

        _activated = true;

        if (streetEnemies != null)
            streetEnemies.SetActive(false);

        if (streetSlots != null)
        {
            foreach (SlotMachineLureZone lure in streetSlots.GetComponentsInChildren<SlotMachineLureZone>(true))
                lure.enabled = false;
        }

        if (streetAmbient != null && streetAmbient.isPlaying)
            streetAmbient.Stop();

        foreach (AudioSource src in casinoSources)
        {
            if (src != null && !src.isPlaying)
                src.Play();
        }
    }
}
