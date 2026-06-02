using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Кнопки «Да» / «Нет» перед игроком. Ищет шаблоны на игроке (UI/CasinoButton) или строит запасной UI.
/// </summary>
public class CasinoChoicePresenter : MonoBehaviour
{
    private const string CasinoButtonPath = "CasinoButton";

    [SerializeField] private Transform anchorOverride;
    [SerializeField] private float distanceInFrontOfPlayer = 1.65f;
    [SerializeField] private float heightOffset = 1.1f;
    [SerializeField] private float buttonSpacing = 1.4f;

    private Transform _root;
    private Button _yesButton;
    private Button _noButton;
    private Action _onYes;
    private Action _onNo;
    private bool _visible;

    public bool IsVisible => _visible;

    public void Show(Action onYes, Action onNo)
    {
        _onYes = onYes;
        _onNo = onNo;
        EnsureUi();
        PositionBeforePlayer();
        WireButtons();
        _root.gameObject.SetActive(true);
        _visible = true;
        EnsureEventSystem();
        GameplayInputBlocker.UnlockCursorForMenu();
    }

    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
            return;

        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();
    }

    public void Hide()
    {
        _visible = false;
        _onYes = null;
        _onNo = null;

        if (_root != null)
            _root.gameObject.SetActive(false);

        GameplayInputBlocker.LockCursorForGameplay();
    }

    private void EnsureUi()
    {
        if (_root != null)
            return;

        if (TryBindExistingOnPlayer())
            return;

        BuildFallbackUi();
    }

    private bool TryBindExistingOnPlayer()
    {
        Transform casinoRoot = FindCasinoButtonRoot();
        if (casinoRoot == null)
            return false;

        _root = casinoRoot;
        _yesButton = FindButton(casinoRoot, "Yes", "Да", "yes");
        _noButton = FindButton(casinoRoot, "No", "Нет", "no");
        return _yesButton != null && _noButton != null;
    }

    private static Transform FindCasinoButtonRoot()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] == null)
                continue;

            Transform direct = players[i].transform.Find(CasinoButtonPath);
            if (direct != null)
                return direct;

            Transform[] all = players[i].GetComponentsInChildren<Transform>(true);
            for (int t = 0; t < all.Length; t++)
            {
                if (all[t] != null && all[t].name == CasinoButtonPath)
                    return all[t];
            }
        }

        return null;
    }

    private static Button FindButton(Transform root, params string[] names)
    {
        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null)
                continue;

            string n = button.gameObject.name;
            for (int j = 0; j < names.Length; j++)
            {
                if (string.Equals(n, names[j], StringComparison.OrdinalIgnoreCase))
                    return button;
            }
        }

        return buttons.Length > 0 ? buttons[0] : null;
    }

    private void BuildFallbackUi()
    {
        GameObject rootGo = new GameObject("CasinoChoice_Fallback");
        _root = rootGo.transform;

        Canvas canvas = rootGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 10060;
        rootGo.AddComponent<CanvasScaler>();
        rootGo.AddComponent<GraphicRaycaster>();

        RectTransform canvasRt = rootGo.GetComponent<RectTransform>();
        canvasRt.sizeDelta = new Vector2(4f, 1.2f);

        _yesButton = CreateFallbackButton(rootGo.transform, "Да", new Vector2(-buttonSpacing * 0.5f, 0f));
        _noButton = CreateFallbackButton(rootGo.transform, "Нет", new Vector2(buttonSpacing * 0.5f, 0f));
        _root.gameObject.SetActive(false);
    }

    private static Button CreateFallbackButton(Transform parent, string label, Vector2 anchoredPos)
    {
        GameObject go = new GameObject(label);
        go.transform.SetParent(parent, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(1.2f, 0.55f);
        rt.anchoredPosition = anchoredPos;

        Image image = go.AddComponent<Image>();
        image.color = new Color(0.12f, 0.12f, 0.14f, 0.92f);

        Button button = go.AddComponent<Button>();

        GameObject textGo = new GameObject("Label");
        textGo.transform.SetParent(go.transform, false);
        RectTransform textRt = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        TMPro.TextMeshProUGUI tmp = textGo.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 36;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.color = Color.white;

        return button;
    }

    private void WireButtons()
    {
        if (_yesButton == null || _noButton == null)
            return;

        _yesButton.onClick.RemoveAllListeners();
        _noButton.onClick.RemoveAllListeners();
        _yesButton.onClick.AddListener(OnYesClicked);
        _noButton.onClick.AddListener(OnNoClicked);
    }

    private void OnYesClicked()
    {
        Action callback = _onYes;
        Hide();
        callback?.Invoke();
    }

    private void OnNoClicked()
    {
        Action callback = _onNo;
        Hide();
        callback?.Invoke();
    }

    private void PositionBeforePlayer()
    {
        if (_root == null)
            return;

        Transform anchor = anchorOverride;
        if (anchor == null && PlayerTeleportUtility.TryGetPlayerBody(out Transform body))
        {
            Transform cam = body.GetComponentInChildren<Camera>(true)?.transform;
            Vector3 forward = cam != null ? cam.forward : body.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
                forward = body.forward;

            forward.Normalize();
            Vector3 pos = body.position + forward * distanceInFrontOfPlayer + Vector3.up * heightOffset;
            _root.SetPositionAndRotation(pos, Quaternion.LookRotation(-forward, Vector3.up));
            return;
        }

        if (anchor != null)
            _root.SetPositionAndRotation(anchor.position, anchor.rotation);
    }
}
