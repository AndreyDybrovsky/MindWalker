using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Фаза-передышка босса церкви (Bipolar). Когда HP босса впервые опускается до одного из порогов
/// (например 50% и 25%), босс исчезает, по центру появляется надпись «Не двигайся» + звук.
/// Даётся время остановиться (grace), затем, пока надпись висит, раз в секунду: игрок стоит — лечим,
/// двигается — наносим урон. Надпись подсвечивается вживую (зелёная — безопасно, красная — двигаешься).
/// Игрок при этом не может умереть (HP не падает ниже пола). По истечении totalDuration босс
/// возвращается на свой спавн с HP, равным этому порогу. Каждый порог срабатывает один раз.
///
/// Вешать на ОТДЕЛЬНЫЙ объект (не на босса): на время фазы GameObject босса выключается.
/// </summary>
[DisallowMultipleComponent]
public class BipolarBossStunPhase : MonoBehaviour
{
    [Header("Босс")]
    [SerializeField] private EnemyHealth bossHealth;
    [Tooltip("Точка возврата босса. Если не задано — позиция/поворот босса в начале боя.")]
    [SerializeField] private Transform bossSpawnPoint;
    [Tooltip("Доли максимального HP, на которых срабатывает фаза. Каждая — один раз.")]
    [SerializeField] private float[] triggerHealthFractions = { 0.5f, 0.25f };

    [Header("UI «Не двигайся»")]
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private string messageKey = "boss.dont_move";
    [SerializeField] private string messageFallback = "Не двигайся";
    [Tooltip("Цвет надписи во время паузы перед эффектом.")]
    [SerializeField] private Color idleColor = Color.white;
    [Tooltip("Игрок стоит — безопасно (лечение).")]
    [SerializeField] private Color safeColor = new Color(0.30f, 0.95f, 0.35f, 1f);
    [Tooltip("Игрок двигается — опасно (урон).")]
    [SerializeField] private Color dangerColor = new Color(0.95f, 0.20f, 0.20f, 1f);
    [SerializeField] private float colorLerpSpeed = 12f;
    [Tooltip("Стартовый масштаб надписи для «pop»-появления.")]
    [SerializeField] private float popScale = 1.3f;
    [SerializeField] private float popDuration = 0.25f;

    [Header("Звук")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("Стингер при появлении надписи (исчезновение босса).")]
    [SerializeField] private AudioClip stingerSound;
    [Tooltip("Whoosh при возвращении босса (опционально).")]
    [SerializeField] private AudioClip returnSound;
    [SerializeField, Range(0f, 1f)] private float stingerVolume = 1f;

    [Header("Тайминги")]
    [Tooltip("Время остановиться до начала лечения/урона.")]
    [SerializeField] private float graceDuration = 1.8f;
    [Tooltip("Полная длительность фазы — когда босс возвращается.")]
    [SerializeField] private float totalDuration = 5.8f;
    [SerializeField] private float tickInterval = 1f;
    [SerializeField] private float healPerTick = 10f;
    [SerializeField] private float damagePerTick = 5f;
    [Tooltip("Смещение игрока (м) за тик, выше которого тик считается «движением» (урон).")]
    [SerializeField] private float moveThreshold = 0.18f;
    [Tooltip("Скорость (м/с), выше которой надпись подсвечивается красным вживую.")]
    [SerializeField] private float liveMoveSpeed = 0.6f;

    [Header("Защита от смерти в фазе")]
    [SerializeField] private bool protectFromDeath = true;
    [Tooltip("Ниже этого HP игрок в фазе не опустится.")]
    [SerializeField] private float minHealthFloor = 1f;

    [Header("Экранная вспышка")]
    [Tooltip("CanvasGroup чёрного оверлея на весь экран (мигание при исчезновении/возврате).")]
    [SerializeField] private CanvasGroup blinkOverlay;
    [SerializeField, Range(0f, 1f)] private float blinkPeak = 0.7f;
    [SerializeField] private float blinkDuration = 0.28f;

    [Header("Прочее")]
    [Tooltip("Уничтожать вражеские пули в полёте в момент исчезновения босса.")]
    [SerializeField] private bool clearEnemyBulletsOnStart = true;

    private int _nextThresholdIndex;
    private bool _running;
    private bool _effectActive;
    private bool _currentlyMoving;
    private Vector3 _lastFramePos;
    private PlayerHealth _playerHealth;
    private Transform _playerBody;
    private Vector3 _bossSpawnPos;
    private Quaternion _bossSpawnRot;
    private NavMeshAgent _bossAgent;

    private void Awake()
    {
        SortFractionsDescending();

        if (bossHealth != null)
        {
            _bossSpawnPos = bossHealth.transform.position;
            _bossSpawnRot = bossHealth.transform.rotation;
            _bossAgent = bossHealth.GetComponent<NavMeshAgent>();
        }
    }

    private void OnEnable()
    {
        if (bossHealth != null)
            bossHealth.OnHealthChanged.AddListener(OnBossHealthChanged);
    }

    private void OnDisable()
    {
        if (bossHealth != null)
            bossHealth.OnHealthChanged.RemoveListener(OnBossHealthChanged);
    }

    private void Start()
    {
        if (audioSource == null)
            TryGetComponent(out audioSource);

        if (messageText != null)
            messageText.gameObject.SetActive(false);

        if (blinkOverlay != null)
        {
            blinkOverlay.alpha = 0f;
            blinkOverlay.blocksRaycasts = false;
        }
    }

    private void Update()
    {
        if (!_effectActive || messageText == null)
            return;

        // Живая подсветка: красная — двигаешься, зелёная — стоишь.
        Vector3 now = PlayerFlatPos();
        float allowed = liveMoveSpeed * Mathf.Max(Time.deltaTime, 0.0001f);
        _currentlyMoving = Vector3.Distance(now, _lastFramePos) > allowed;
        _lastFramePos = now;

        Color target = _currentlyMoving ? dangerColor : safeColor;
        messageText.color = Color.Lerp(messageText.color, target, colorLerpSpeed * Time.deltaTime);
    }

    private void OnBossHealthChanged(float current)
    {
        if (_running || bossHealth == null)
            return;

        if (_nextThresholdIndex >= triggerHealthFractions.Length)
            return;

        float frac = triggerHealthFractions[_nextThresholdIndex];
        if (current > 0f && current <= bossHealth.MaxHealth * frac)
        {
            _nextThresholdIndex++;
            StartCoroutine(StunRoutine(frac));
        }
    }

    private IEnumerator StunRoutine(float frac)
    {
        _running = true;

        ResolvePlayerRefs();

        if (clearEnemyBulletsOnStart)
            ClearEnemyBullets();

        Transform bossT = bossHealth.transform;
        if (bossSpawnPoint == null)
        {
            _bossSpawnPos = bossT.position;
            _bossSpawnRot = bossT.rotation;
        }

        bossHealth.gameObject.SetActive(false);

        ShowMessage(true);
        PlayClip(stingerSound);
        StartCoroutine(ScreenBlink());
        StartCoroutine(PopText());

        if (graceDuration > 0f)
            yield return new WaitForSeconds(graceDuration);

        _effectActive = true;
        _lastFramePos = PlayerFlatPos();

        float remaining = Mathf.Max(0f, totalDuration - graceDuration);
        float interval = Mathf.Max(0.1f, tickInterval);
        Vector3 tickPrev = PlayerFlatPos();

        while (remaining > 0.001f)
        {
            float step = Mathf.Min(interval, remaining);
            yield return new WaitForSeconds(step);
            remaining -= step;

            Vector3 nowPos = PlayerFlatPos();
            float moved = Vector3.Distance(nowPos, tickPrev);
            tickPrev = nowPos;

            if (_playerHealth == null || _playerHealth.IsDead)
                continue;

            if (moved > moveThreshold)
                ApplyDamage(damagePerTick);
            else
                _playerHealth.Heal(healPerTick);
        }

        _effectActive = false;
        ShowMessage(false);

        PlayClip(returnSound);
        StartCoroutine(ScreenBlink());

        RestoreBoss(frac);

        _running = false;
    }

    private void ApplyDamage(float amount)
    {
        if (_playerHealth == null)
            return;

        float dmg = amount;
        if (protectFromDeath)
        {
            float maxAllowed = Mathf.Max(0f, _playerHealth.CurrentHealth - minHealthFloor);
            dmg = Mathf.Min(dmg, maxAllowed);
        }

        if (dmg > 0f)
            _playerHealth.TakeDamage(dmg);
    }

    private void RestoreBoss(float frac)
    {
        if (bossHealth == null)
            return;

        Vector3 pos = bossSpawnPoint != null ? bossSpawnPoint.position : _bossSpawnPos;
        Quaternion rot = bossSpawnPoint != null ? bossSpawnPoint.rotation : _bossSpawnRot;

        GameObject go = bossHealth.gameObject;
        go.transform.SetPositionAndRotation(pos, rot);
        go.SetActive(true);

        bossHealth.SetHealth(bossHealth.MaxHealth * frac, bossHealth.MaxHealth);

        if (_bossAgent == null)
            _bossAgent = go.GetComponent<NavMeshAgent>();
        if (_bossAgent != null && _bossAgent.enabled && _bossAgent.isOnNavMesh)
            _bossAgent.Warp(pos);
    }

    private IEnumerator PopText()
    {
        if (messageText == null || popDuration <= 0.001f)
            yield break;

        RectTransform rt = messageText.rectTransform;
        float e = 0f;
        while (e < popDuration)
        {
            e += Time.deltaTime;
            float t = Mathf.Clamp01(e / popDuration);
            rt.localScale = Vector3.one * Mathf.Lerp(popScale, 1f, t);
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    private IEnumerator ScreenBlink()
    {
        if (blinkOverlay == null || blinkDuration <= 0.001f)
            yield break;

        blinkOverlay.gameObject.SetActive(true);
        float e = 0f;
        while (e < blinkDuration)
        {
            e += Time.deltaTime;
            float t = Mathf.Clamp01(e / blinkDuration);
            blinkOverlay.alpha = Mathf.Lerp(blinkPeak, 0f, t);
            yield return null;
        }
        blinkOverlay.alpha = 0f;
    }

    private void ResolvePlayerRefs()
    {
        if (_playerHealth == null)
            _playerHealth = FindFirstObjectByType<PlayerHealth>();

        if (_playerBody == null && PlayerTeleportUtility.TryGetPlayerBody(out Transform body))
            _playerBody = body;

        if (_playerBody == null && _playerHealth != null)
            _playerBody = _playerHealth.transform;
    }

    private Vector3 PlayerFlatPos()
    {
        if (_playerBody == null)
            ResolvePlayerRefs();

        Vector3 p = _playerBody != null ? _playerBody.position : Vector3.zero;
        p.y = 0f;
        return p;
    }

    private void ShowMessage(bool show)
    {
        if (messageText == null)
            return;

        if (show)
        {
            messageText.text = ResolveMessage();
            messageText.color = idleColor;
        }

        messageText.gameObject.SetActive(show);
    }

    private string ResolveMessage()
    {
        if (!string.IsNullOrEmpty(messageKey) && LocalizationManager.Instance != null)
        {
            string s = LocalizationManager.Instance.T(messageKey);
            if (!string.IsNullOrEmpty(s) && s != messageKey)
                return s;
        }

        return messageFallback;
    }

    private void PlayClip(AudioClip clip)
    {
        if (clip == null || audioSource == null)
            return;

        audioSource.PlayOneShot(clip, stingerVolume);
    }

    private void ClearEnemyBullets()
    {
        EnemyBullet[] bullets = FindObjectsByType<EnemyBullet>(FindObjectsSortMode.None);
        for (int i = 0; i < bullets.Length; i++)
        {
            if (bullets[i] != null)
                Destroy(bullets[i].gameObject);
        }
    }

    private void SortFractionsDescending()
    {
        if (triggerHealthFractions == null || triggerHealthFractions.Length == 0)
        {
            triggerHealthFractions = new[] { 0.5f };
            return;
        }

        System.Array.Sort(triggerHealthFractions);
        System.Array.Reverse(triggerHealthFractions);
    }
}
