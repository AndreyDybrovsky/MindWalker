using UnityEngine;

/// <summary>
/// Враг уровня Autism: хранит свой ElementColor.
/// Пуля совпадающего цвета наносит урон (TakeDamage), несовпадающего — баффает урон врага.
/// Урон/смерть делегируются существующему <see cref="EnemyHealth"/>.
/// </summary>
[DisallowMultipleComponent]
public class ColorEnemy : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [Header("Цвет врага")]
    [SerializeField] private ElementColor enemyColor = ElementColor.Red;

    [Tooltip("Перекрасить модель врага в его цвет на старте (визуальная подсказка игроку).")]
    [SerializeField] private bool tintModel = true;

    [Header("Бафф при попадании неправильным цветом")]
    [Tooltip("Во сколько раз растёт урон врага при попадании несовпадающим цветом.")]
    [SerializeField] private float damageBuffMultiplier = 1.5f;

    [Tooltip("Максимум применений баффа (защита от бесконечного роста).")]
    [SerializeField] private int maxBuffStacks = 3;

    [Tooltip("Вспышка при баффе.")]
    [SerializeField] private float buffFlashDuration = 0.15f;

    public ElementColor EnemyColor => enemyColor;

    private EnemyHealth _health;
    private EnemyShooting _shooting;
    private int _buffStacks;
    private Renderer[] _renderers;
    private MaterialPropertyBlock _mpb;

    private void Awake()
    {
        _health = GetComponent<EnemyHealth>();
        if (_health == null)
            _health = GetComponentInChildren<EnemyHealth>(true);

        _shooting = GetComponent<EnemyShooting>();
        if (_shooting == null)
            _shooting = GetComponentInChildren<EnemyShooting>(true);

        _renderers = GetComponentsInChildren<Renderer>(true);
        _mpb = new MaterialPropertyBlock();
    }

    private void Start()
    {
        if (tintModel)
            ApplyTint(ColorManager.ToColor(enemyColor));
    }

    /// <summary>Задать цвет врага из кода (используется настройкой префабов).</summary>
    public void SetEnemyColor(ElementColor color)
    {
        enemyColor = color;
        if (tintModel && _renderers != null)
            ApplyTint(ColorManager.ToColor(enemyColor));
    }

    /// <summary>
    /// Обработать попадание пули заданного цвета.
    /// Совпадение → урон, иначе → бафф врага.
    /// </summary>
    public void HandleHit(ElementColor bulletColor, float damage)
    {
        if (bulletColor == enemyColor)
            TakeDamage(damage);
        else
            ApplyDamageBuff();
    }

    /// <summary>Нанести урон врагу (делегируется EnemyHealth).</summary>
    public void TakeDamage(float damage)
    {
        if (_health != null)
        {
            _health.TakeDamage(damage);
            GameStatsTracker.Instance?.RecordDamageDealt(damage);
        }
    }

    /// <summary>Бафф урона врага при попадании неправильным цветом.</summary>
    public void ApplyDamageBuff()
    {
        if (_buffStacks >= maxBuffStacks)
            return;

        _buffStacks++;

        if (_shooting != null)
            _shooting.ApplyDamageMultiplier(damageBuffMultiplier);

        if (buffFlashDuration > 0f && isActiveAndEnabled)
            StartCoroutine(BuffFlashRoutine());
    }

    private System.Collections.IEnumerator BuffFlashRoutine()
    {
        ApplyTint(Color.white);
        yield return new WaitForSeconds(buffFlashDuration);
        ApplyTint(ColorManager.ToColor(enemyColor));
    }

    private void ApplyTint(Color color)
    {
        if (_renderers == null)
            return;

        for (int i = 0; i < _renderers.Length; i++)
        {
            Renderer r = _renderers[i];
            if (r == null || r.sharedMaterial == null)
                continue;

            r.GetPropertyBlock(_mpb);
            if (r.sharedMaterial.HasProperty(BaseColorId))
                _mpb.SetColor(BaseColorId, color);
            if (r.sharedMaterial.HasProperty(ColorId))
                _mpb.SetColor(ColorId, color);
            r.SetPropertyBlock(_mpb);
        }
    }
}
