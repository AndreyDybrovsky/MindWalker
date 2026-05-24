using System.Globalization;
using UnityEngine;
using TMPro;

/// <summary>
/// Отображение HP, таймера и счётчика врагов на часах.
/// Строки берутся из String Table <see cref="LocalizationManager.StringTableCollectionName"/> (ключи по умолчанию watch.*).
/// </summary>
public class WatchHandDisplay : MonoBehaviour
{
    [Header("Объекты и компоненты")]
    [SerializeField] private Transform watchHandObject;
    [SerializeField] private Camera playerCamera;

    [Header("3D Text")]
    [SerializeField] private TextMesh hpText3D;
    [SerializeField] private TextMesh timerText3D;
    [SerializeField] private TextMesh enemyCountText3D;

    [Header("2D UI Text")]
    [SerializeField] private TextMeshProUGUI hpTextUI;
    [SerializeField] private TextMeshProUGUI timerTextUI;
    [SerializeField] private TextMeshProUGUI enemyCountTextUI;

    [Header("Настройки движения")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Tab;
    [SerializeField] private float moveSpeed = 2f;

    [Header("Позиции")]
    [SerializeField] private Vector3 hiddenPosition = new Vector3(0.3f, -0.4f, 0.3f);
    [SerializeField] private Vector3 viewPosition = new Vector3(0f, 0f, 0.4f);

    [Header("Ссылки")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private GameTimer gameTimer;
    [SerializeField] private EnemyCounter enemyCounter;

    [Header("Локализация (LocalizationBase)")]
    [SerializeField] private bool useLocalization = true;
    [SerializeField] private string keyHpFormat = "watch.hp_format";
    [SerializeField] private string keyTimeFormat = "watch.time_format";
    [SerializeField] private string keyEnemiesFormat = "watch.enemies_format";

    private Vector3 targetPosition;
    private bool isVisible;
    private bool isMoving;
    private LocalizationManager subscribedLocalization;

    private void OnEnable()
    {
        SubscribeLocalizationChanged();
    }

    private void OnDisable()
    {
        UnsubscribeLocalizationChanged();
    }

    private void Start()
    {
        if (playerHealth == null)
            playerHealth = FindFirstObjectByType<PlayerHealth>(FindObjectsInactive.Include);

        if (gameTimer == null)
            gameTimer = FindFirstObjectByType<GameTimer>(FindObjectsInactive.Include);

        if (enemyCounter == null)
            enemyCounter = FindFirstObjectByType<EnemyCounter>(FindObjectsInactive.Include);

        if (playerCamera == null)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
                playerCamera = mainCam;
            else
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                    playerCamera = player.GetComponentInChildren<Camera>();
            }
        }

        if (watchHandObject == null)
            watchHandObject = transform;

        if (playerCamera != null && watchHandObject != null)
        {
            targetPosition = playerCamera.transform.TransformPoint(hiddenPosition);
            watchHandObject.position = targetPosition;
        }

        SubscribeLocalizationChanged();
        UpdateTextVisibility(false);
    }

    private void SubscribeLocalizationChanged()
    {
        if (!useLocalization)
            return;

        LocalizationManager loc = LocalizationManager.Instance;
        if (loc == null || subscribedLocalization == loc)
            return;

        UnsubscribeLocalizationChanged();
        subscribedLocalization = loc;
        subscribedLocalization.OnLanguageChanged += OnLanguageChanged;
    }

    private void UnsubscribeLocalizationChanged()
    {
        if (subscribedLocalization != null)
        {
            subscribedLocalization.OnLanguageChanged -= OnLanguageChanged;
            subscribedLocalization = null;
        }
    }

    private void OnLanguageChanged(GameLanguage _)
    {
        if (isVisible)
            UpdateDisplayTexts();
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            ToggleWatch();

        if (isMoving && watchHandObject != null && playerCamera != null)
        {
            Vector3 targetWorldPos = playerCamera.transform.TransformPoint(targetPosition);
            watchHandObject.position = Vector3.Lerp(watchHandObject.position, targetWorldPos, Time.deltaTime * moveSpeed);

            watchHandObject.LookAt(playerCamera.transform.position);
            watchHandObject.Rotate(0f, 180f, 0f);

            if (Vector3.Distance(watchHandObject.position, targetWorldPos) < 0.01f)
            {
                watchHandObject.position = targetWorldPos;
                isMoving = false;
            }
        }

        if (isVisible)
            UpdateDisplayTexts();
    }

    private void ToggleWatch()
    {
        isVisible = !isVisible;
        isMoving = true;

        if (playerCamera == null || watchHandObject == null)
            return;

        if (isVisible)
        {
            targetPosition = viewPosition;
            UpdateTextVisibility(true);
        }
        else
        {
            targetPosition = hiddenPosition;
            UpdateTextVisibility(false);
        }
    }

    private static string FormatLocalized(string tableKey, string fallbackFormat, params object[] args)
    {
        string fmt = fallbackFormat;
        if (LocalizationManager.Instance != null)
        {
            string t = LocalizationManager.Instance.T(tableKey);
            if (!string.IsNullOrEmpty(t) && t != tableKey)
                fmt = t;
        }

        if (args == null || args.Length == 0)
            return fmt;

        try
        {
            return string.Format(CultureInfo.CurrentCulture, fmt, args);
        }
        catch (System.FormatException)
        {
            try
            {
                return string.Format(CultureInfo.InvariantCulture, fallbackFormat, args);
            }
            catch
            {
                return fallbackFormat;
            }
        }
    }

    private void UpdateDisplayTexts()
    {
        string hpString = "";
        string timerString = "";
        string enemyCountString = "";

        if (playerHealth != null)
        {
            float currentHP = playerHealth.CurrentHealth;
            float maxHP = playerHealth.MaxHealth;
            if (useLocalization)
                hpString = FormatLocalized(keyHpFormat, "HP: {0:F0} / {1:F0}", currentHP, maxHP);
            else
                hpString = $"HP: {currentHP:F0} / {maxHP:F0}";
        }

        if (gameTimer != null)
        {
            float remainingTime = gameTimer.GetRemainingTime();
            int minutes = Mathf.FloorToInt(remainingTime / 60f);
            int seconds = Mathf.FloorToInt(remainingTime % 60f);
            if (useLocalization)
                timerString = FormatLocalized(keyTimeFormat, "Time: {0:00}:{1:00}", minutes, seconds);
            else
                timerString = $"Time: {minutes:00}:{seconds:00}";
        }

        if (enemyCounter != null)
        {
            int remaining = enemyCounter.RemainingEnemies;
            int total = enemyCounter.TotalEnemies;
            if (useLocalization)
                enemyCountString = FormatLocalized(keyEnemiesFormat, "Осталось: {0}/{1}", remaining, total);
            else
                enemyCountString = $"Осталось: {remaining}/{total}";
        }

        if (hpText3D != null && !string.IsNullOrEmpty(hpString))
            hpText3D.text = hpString;

        if (timerText3D != null && !string.IsNullOrEmpty(timerString))
            timerText3D.text = timerString;

        if (enemyCountText3D != null && !string.IsNullOrEmpty(enemyCountString))
            enemyCountText3D.text = enemyCountString;

        if (hpTextUI != null && !string.IsNullOrEmpty(hpString))
            hpTextUI.text = hpString;

        if (timerTextUI != null && !string.IsNullOrEmpty(timerString))
            timerTextUI.text = timerString;

        if (enemyCountTextUI != null && !string.IsNullOrEmpty(enemyCountString))
            enemyCountTextUI.text = enemyCountString;
    }

    private void UpdateTextVisibility(bool visible)
    {
        if (hpText3D != null)
            hpText3D.gameObject.SetActive(visible);

        if (timerText3D != null)
            timerText3D.gameObject.SetActive(visible);

        if (enemyCountText3D != null)
            enemyCountText3D.gameObject.SetActive(visible);

        if (hpTextUI != null)
            hpTextUI.gameObject.SetActive(visible);

        if (timerTextUI != null)
            timerTextUI.gameObject.SetActive(visible);

        if (enemyCountTextUI != null)
            enemyCountTextUI.gameObject.SetActive(visible);
    }
}
