using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Конус обзора: меш по лучам, обрезается стенами (не полом).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ConeVisionVisualizer : MonoBehaviour
{
    [Header("Параметры конуса")]
    [SerializeField] private float viewAngle = 70f;
    [SerializeField] private float viewDistance = 14f;
    [SerializeField] private Transform raycastOrigin;
    [SerializeField] private LayerMask obstacleLayer = 1;

    [Header("Отрисовка")]
    [SerializeField] private MeshFilter meshFilter;
    [SerializeField] private MeshRenderer meshRenderer;
    [SerializeField] private Color coneColor = new Color(1f, 0.28f, 0.12f, 0.35f);
    [SerializeField] private int edgeSegments = 28;
    [SerializeField] private int radialRings = 3;
    [SerializeField] private float rayOriginHeight = 1.1f;
    [SerializeField] private bool ignoreFloorHits = true;
    [SerializeField] private bool rebuildEveryFrame = true;
    [SerializeField] private bool clipByObstacles = true;

    [Header("Зона вокруг охранника")]
    [SerializeField] private float proximityRadius;
    [SerializeField] private Color proximityRingColor = new Color(1f, 0.55f, 0.1f, 0.22f);
    [SerializeField] private int proximitySegments = 32;

    private Material _runtimeMaterial;
    private Mesh _mesh;

    private void Reset()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        raycastOrigin = transform;
    }

    private void Awake()
    {
        EnsureComponents();
    }

    private void OnEnable()
    {
        EnsureComponents();
        RebuildMesh();
    }

    private void LateUpdate()
    {
        if (!rebuildEveryFrame)
            return;

        RebuildMesh();
    }

    public void Configure(float angleDegrees, float distance)
    {
        Configure(angleDegrees, distance, raycastOrigin != null ? raycastOrigin : transform, obstacleLayer, clipByObstacles);
    }

    public void Configure(
        float angleDegrees,
        float distance,
        Transform origin,
        LayerMask obstacles,
        bool useObstacleClipping)
    {
        Configure(angleDegrees, distance, origin, obstacles, useObstacleClipping, proximityRadius);
    }

    public void Configure(
        float angleDegrees,
        float distance,
        Transform origin,
        LayerMask obstacles,
        bool useObstacleClipping,
        float nearbyRadius)
    {
        viewAngle = angleDegrees;
        viewDistance = distance;
        proximityRadius = nearbyRadius;
        raycastOrigin = origin != null ? origin : transform;
        obstacleLayer = obstacles;
        clipByObstacles = useObstacleClipping;

        EnsureComponents();
        RebuildMesh();
    }

    [ContextMenu("Пересобрать конус")]
    public void RebuildMeshNow()
    {
        EnsureComponents();
        RebuildMesh();
    }

    private void EnsureComponents()
    {
        if (meshFilter == null)
            meshFilter = GetComponent<MeshFilter>();
        if (meshRenderer == null)
            meshRenderer = GetComponent<MeshRenderer>();

        ApplyTransparentMaterial();
        if (meshRenderer != null)
        {
            meshRenderer.enabled = true;
            gameObject.SetActive(true);
        }
    }

    private void ApplyTransparentMaterial()
    {
        if (_runtimeMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Unlit/Transparent");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Unlit");

            _runtimeMaterial = new Material(shader);
            _runtimeMaterial.renderQueue = (int)RenderQueue.Transparent;
        }

        if (_runtimeMaterial.HasProperty("_BaseColor"))
            _runtimeMaterial.SetColor("_BaseColor", coneColor);
        if (_runtimeMaterial.HasProperty("_Color"))
            _runtimeMaterial.SetColor("_Color", coneColor);

        _runtimeMaterial.color = coneColor;

        if (_runtimeMaterial.HasProperty("_Surface"))
        {
            _runtimeMaterial.SetFloat("_Surface", 1f);
            _runtimeMaterial.SetFloat("_Blend", 0f);
            _runtimeMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            _runtimeMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            _runtimeMaterial.SetFloat("_ZWrite", 0f);
            _runtimeMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }

        if (meshRenderer == null)
            return;

        meshRenderer.sharedMaterial = _runtimeMaterial;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
    }

    private void RebuildMesh()
    {
        if (viewDistance <= 0.01f || viewAngle <= 0.01f)
            return;

        EnsureComponents();

        BuildConeMesh();
        if (proximityRadius > 0.05f)
            AppendProximityDisc();
    }

    private void BuildConeMesh()
    {
        int edgeSeg = Mathf.Max(3, edgeSegments);
        int rings = Mathf.Max(1, radialRings);
        int vertsPerRing = edgeSeg + 1;
        int vertexCount = 1 + vertsPerRing * rings;
        Vector3[] vertices = new Vector3[vertexCount];
        int[] triangles = new int[edgeSeg * rings * 6];

        vertices[0] = Vector3.zero;
        float halfAngle = viewAngle * 0.5f * Mathf.Deg2Rad;
        Transform origin = raycastOrigin != null ? raycastOrigin : transform;

        for (int ring = 0; ring < rings; ring++)
        {
            int ringStart = 1 + ring * vertsPerRing;
            float radiusT = (ring + 1) / (float)rings;

            for (int i = 0; i <= edgeSeg; i++)
            {
                float t = i / (float)edgeSeg;
                float angle = Mathf.Lerp(-halfAngle, halfAngle, t);
                float castDistance = viewDistance * radiusT;
                vertices[ringStart + i] = CastConePoint(origin, angle, castDistance);
            }
        }

        int tri = 0;
        for (int ring = 0; ring < rings; ring++)
        {
            int ringStart = 1 + ring * vertsPerRing;

            if (ring == 0)
            {
                for (int i = 0; i < edgeSeg; i++)
                {
                    triangles[tri++] = 0;
                    triangles[tri++] = ringStart + i;
                    triangles[tri++] = ringStart + i + 1;
                }
            }
            else
            {
                int prevRingStart = 1 + (ring - 1) * vertsPerRing;
                for (int i = 0; i < edgeSeg; i++)
                {
                    triangles[tri++] = prevRingStart + i;
                    triangles[tri++] = ringStart + i;
                    triangles[tri++] = ringStart + i + 1;

                    triangles[tri++] = prevRingStart + i;
                    triangles[tri++] = ringStart + i + 1;
                    triangles[tri++] = prevRingStart + i + 1;
                }
            }
        }

        if (tri < triangles.Length)
        {
            int[] trimmed = new int[tri];
            System.Array.Copy(triangles, trimmed, tri);
            triangles = trimmed;
        }

        ApplyMesh(vertices, triangles);
    }

    private void AppendProximityDisc()
    {
        int seg = Mathf.Max(8, proximitySegments);
        Transform origin = raycastOrigin != null ? raycastOrigin : transform;
        float fullCircle = Mathf.PI * 2f;

        Vector3[] ringVerts = new Vector3[seg + 2];
        ringVerts[0] = Vector3.zero;
        for (int i = 0; i <= seg; i++)
        {
            float angle = (i / (float)seg) * fullCircle;
            ringVerts[1 + i] = CastConePoint(origin, angle, proximityRadius);
        }

        int[] ringTris = new int[seg * 3];
        for (int i = 0; i < seg; i++)
        {
            int triBase = i * 3;
            ringTris[triBase] = 0;
            ringTris[triBase + 1] = 1 + i;
            ringTris[triBase + 2] = 1 + i + 1;
        }

        if (_mesh == null || _mesh.vertexCount == 0)
        {
            ApplyMesh(ringVerts, ringTris);
            return;
        }

        int baseVertex = _mesh.vertexCount;
        Vector3[] combined = new Vector3[baseVertex + ringVerts.Length];
        System.Array.Copy(_mesh.vertices, combined, baseVertex);
        System.Array.Copy(ringVerts, 0, combined, baseVertex, ringVerts.Length);

        int[] oldTris = _mesh.triangles;
        int[] combinedTris = new int[oldTris.Length + ringTris.Length];
        System.Array.Copy(oldTris, combinedTris, oldTris.Length);
        for (int i = 0; i < ringTris.Length; i++)
            combinedTris[oldTris.Length + i] = ringTris[i] + baseVertex;

        ApplyMesh(combined, combinedTris);
    }

    private void ApplyMesh(Vector3[] vertices, int[] triangles)
    {
        if (_mesh == null)
        {
            _mesh = new Mesh { name = "VisionConeClipped" };
            _mesh.MarkDynamic();
        }

        _mesh.Clear();
        _mesh.vertices = vertices;
        _mesh.triangles = triangles;
        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();
        meshFilter.sharedMesh = _mesh;
    }

    private Vector3 CastConePoint(Transform origin, float signedAngleRadians, float castDistance)
    {
        Vector3 worldDir = GetConeWorldDirection(origin, signedAngleRadians);
        Vector3 worldOrigin = origin.position + Vector3.up * rayOriginHeight;
        Vector3 worldEnd = worldOrigin + worldDir * castDistance;

        if (clipByObstacles && castDistance > 0.05f && obstacleLayer.value != 0)
        {
            RaycastHit[] hits = Physics.RaycastAll(
                worldOrigin,
                worldDir,
                castDistance,
                obstacleLayer,
                QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            for (int i = 0; i < hits.Length; i++)
            {
                if (ignoreFloorHits && hits[i].normal.y > 0.65f)
                    continue;

                worldEnd = hits[i].point;
                break;
            }
        }

        Vector3 local = transform.InverseTransformPoint(worldEnd);
        local.y = 0f;
        return local;
    }

    public static Vector3 GetFlatForward(Transform origin)
    {
        if (origin == null)
            return Vector3.forward;

        Vector3 forward = origin.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = origin.rotation * Vector3.forward;
            forward.y = 0f;
        }

        return forward.sqrMagnitude < 0.0001f ? Vector3.forward : forward.normalized;
    }

    public static Vector3 GetConeWorldDirection(Transform origin, float signedAngleRadians)
    {
        return Quaternion.AngleAxis(signedAngleRadians * Mathf.Rad2Deg, Vector3.up) * GetFlatForward(origin);
    }

    private void OnDestroy()
    {
        if (_runtimeMaterial != null)
            Destroy(_runtimeMaterial);

        if (_mesh != null)
            Destroy(_mesh);
    }

    private void OnDrawGizmosSelected()
    {
        if (viewDistance <= 0.01f)
            return;

        Transform origin = raycastOrigin != null ? raycastOrigin : transform;
        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.5f);
        Gizmos.DrawWireSphere(origin.position + Vector3.up * rayOriginHeight, 0.15f);
    }
}
