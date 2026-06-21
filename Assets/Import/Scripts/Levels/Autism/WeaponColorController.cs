using UnityEngine;

/// <summary>
/// Управление цветом оружия игрока (уровень Autism).
/// Q / E или колёсико мыши переключают текущий цвет; материал спрайта оружия перекрашивается.
/// Работает только после разблокировки крашения (подбор ColorGunObject).
/// </summary>
public class WeaponColorController : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [Header("Спрайт/рендер оружия для перекраски")]
    [Tooltip("SpriteRenderer оружия (если 2D-спрайт).")]
    [SerializeField] private SpriteRenderer weaponSprite;
    [Tooltip("Renderer оружия (если 3D-модель/меш). Используется, если SpriteRenderer не задан.")]
    [SerializeField] private Renderer weaponRenderer;

    [Header("Управление")]
    [SerializeField] private KeyCode prevColorKey = KeyCode.Q;
    [SerializeField] private KeyCode nextColorKey = KeyCode.E;
    [SerializeField] private bool useMouseWheel = true;

    [Header("Подсветка")]
    [Tooltip("Усиливать эмиссию материала в цвет (для светящегося оружия).")]
    [SerializeField] private bool applyEmission = true;

    private Material _weaponMaterialInstance;

    private void Awake()
    {
        if (weaponSprite == null)
            weaponSprite = GetComponentInChildren<SpriteRenderer>(true);

        if (weaponSprite == null && weaponRenderer == null)
            weaponRenderer = GetComponentInChildren<Renderer>(true);

        if (weaponRenderer != null)
            _weaponMaterialInstance = weaponRenderer.material;
    }

    private void OnEnable()
    {
        ColorManager.OnColorChanged += OnColorChanged;
    }

    private void OnDisable()
    {
        ColorManager.OnColorChanged -= OnColorChanged;
    }

    private void Start()
    {
        // Привести оружие к текущему цвету (если крашение уже открыто).
        if (ColorManager.ColoringUnlocked)
            Recolor(ColorManager.CurrentColor);
    }

    private void Update()
    {
        if (!ColorManager.ColoringUnlocked || GameplayInputBlocker.IsBlocked)
            return;

        if (Input.GetKeyDown(prevColorKey))
        {
            ColorManager.Cycle(-1);
            return;
        }

        if (Input.GetKeyDown(nextColorKey))
        {
            ColorManager.Cycle(+1);
            return;
        }

        if (useMouseWheel)
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll > 0.01f)
                ColorManager.Cycle(+1);
            else if (scroll < -0.01f)
                ColorManager.Cycle(-1);
        }
    }

    private void OnColorChanged(ElementColor color) => Recolor(color);

    private void Recolor(ElementColor color)
    {
        Color c = ColorManager.ToColor(color);

        if (weaponSprite != null)
            weaponSprite.color = c;

        if (weaponRenderer != null)
        {
            if (_weaponMaterialInstance == null)
                _weaponMaterialInstance = weaponRenderer.material;

            Material mat = _weaponMaterialInstance;
            if (mat.HasProperty(BaseColorId))
                mat.SetColor(BaseColorId, c);
            if (mat.HasProperty(ColorId))
                mat.SetColor(ColorId, c);
            if (applyEmission && mat.HasProperty(EmissionColorId))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor(EmissionColorId, c * 1.5f);
            }
        }
    }
}
