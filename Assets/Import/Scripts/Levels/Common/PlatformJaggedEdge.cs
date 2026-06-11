using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// Генерирует процедурные «оборванные края» вокруг верхнего периметра платформы.
/// Создаёт дочерний объект _JaggedEdge с мешем-юбкой; оригинал не изменяется.
///
/// Использование: ПКМ по компоненту в инспекторе → "Generate Jagged Edges".
/// Параметры меняются — жми Generate снова, старая юбка заменится.
/// </summary>
[RequireComponent(typeof(MeshFilter))]
public class PlatformJaggedEdge : MonoBehaviour
{
    [Header("Форма края")]
    [Tooltip("Точек на каждую сторону платформы. Больше = детальнее.")]
    [SerializeField] private int   samplesPerEdge = 30;
    [Tooltip("Частота Perlin-шума (больше = чаще изломы)")]
    [SerializeField] private float noiseFrequency = 2.0f;
    [Tooltip("Макс. выпирание края наружу")]
    [SerializeField] private float maxOutward     = 0.18f;
    [Tooltip("Макс. углубление края внутрь")]
    [SerializeField] private float maxInward      = 0.10f;
    [Tooltip("Высота свисающей бахромы")]
    [SerializeField] private float skirtDepth     = 0.30f;
    [Tooltip("Разброс высоты бахромы — даёт рваный нижний край")]
    [SerializeField] private float depthVariance  = 0.14f;
    [Tooltip("Seed шума. 0 = новый случайный при каждом Generate")]
    [SerializeField] private int   seed           = 1;

    [Header("Рендер")]
    [Tooltip("Рисовать с обеих сторон (оставь true, если материал не двусторонний)")]
    [SerializeField] private bool     doubleSided      = true;
    [Tooltip("null — берётся материал самой платформы")]
    [SerializeField] private Material overrideMaterial;

    // ─── Контекстное меню ──────────────────────────────────────────────────

    [ContextMenu("Generate Jagged Edges")]
    public void Generate()
    {
        RemoveOld();

        var mf = GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
        {
            Debug.LogWarning($"[PlatformJaggedEdge] {name}: нет MeshFilter/Mesh.", this);
            return;
        }

        Bounds  b    = mf.sharedMesh.bounds;
        float   top  = b.max.y;
        Vector3 c2d  = new Vector3(b.center.x, top, b.center.z);

        // Четыре угла верхней грани, обход по часовой стрелке сверху
        Vector3[] corners =
        {
            new Vector3(b.min.x, top, b.min.z),
            new Vector3(b.min.x, top, b.max.z),
            new Vector3(b.max.x, top, b.max.z),
            new Vector3(b.max.x, top, b.min.z),
        };

        float s = (seed == 0 ? Random.Range(1, 9999) : seed) * 31.41f;

        // Предпосчёт длин рёбер для равномерного UV вдоль периметра
        float totalLen = 0f;
        float[] eLens  = new float[4];
        for (int e = 0; e < 4; e++)
        {
            eLens[e] = Vector3.Distance(corners[e], corners[(e + 1) % 4]);
            totalLen += eLens[e];
        }

        var verts = new List<Vector3>();
        var uvs   = new List<Vector2>();
        var tris  = new List<int>();

        float uvOffset = 0f;

        for (int e = 0; e < 4; e++)
        {
            Vector3 a = corners[e];
            Vector3 z = corners[(e + 1) % 4];

            Vector3 edgeDir = (z - a).normalized;

            // Нормаль наружу в плоскости XZ
            Vector3 outN = new Vector3(-edgeDir.z, 0f, edgeDir.x);
            if (Vector3.Dot(outN, (a + z) * 0.5f - c2d) < 0f) outN = -outN;

            int   N      = samplesPerEdge;
            float eUVW   = eLens[e] / totalLen;
            int   startI = verts.Count;

            for (int i = 0; i <= N; i++)
            {
                float   t = (float)i / N;
                Vector3 p = Vector3.Lerp(a, z, t);

                // Затухание у углов — убирает щели на стыках рёбер
                float cf = Mathf.Clamp01(Mathf.Min(t, 1f - t) * 8f);

                // Два октава шума: крупный профиль + мелкие зазубрины
                float n1   = Mathf.PerlinNoise(p.x * noiseFrequency + s,          p.z * noiseFrequency + s + 5.7f);
                float n2   = Mathf.PerlinNoise(p.x * noiseFrequency * 2.4f + s + 3f, p.z * noiseFrequency * 2.4f + s + 8f) * 0.35f;
                float nTop = Mathf.Clamp01(n1 + n2 - 0.15f);

                float   dispTop = Mathf.Lerp(-maxInward, maxOutward, nTop) * cf;
                Vector3 topV    = p + outN * dispTop;

                // Нижний край: независимый шум + вариация глубины
                float nb      = Mathf.PerlinNoise(p.x * noiseFrequency * 0.85f + s + 17f, p.z * noiseFrequency * 0.85f + s + 2f);
                float nd      = Mathf.PerlinNoise(p.x * noiseFrequency * 3f    + s + 9f,  p.z * noiseFrequency * 3f    + s + 1f);
                float dispBot = Mathf.Lerp(-maxInward * 0.5f, maxOutward * 1.5f, nb) * cf;
                float depth   = skirtDepth + nd * depthVariance;
                Vector3 botV  = new Vector3(p.x + outN.x * dispBot, top - depth, p.z + outN.z * dispBot);

                verts.Add(topV);
                verts.Add(botV);

                float uu = (uvOffset + t * eUVW) * totalLen;
                uvs.Add(new Vector2(uu, 0f));
                uvs.Add(new Vector2(uu, depth));
            }

            // Треугольники с нормалями наружу (CCW со стороны наружной нормали)
            for (int i = 0; i < N; i++)
            {
                int i0 = startI + i * 2;
                int i1 = i0 + 1;   // bot[i]
                int i2 = i0 + 2;   // top[i+1]
                int i3 = i0 + 3;   // bot[i+1]

                tris.Add(i0); tris.Add(i1); tris.Add(i3);
                tris.Add(i0); tris.Add(i3); tris.Add(i2);
            }

            uvOffset += eUVW;
        }

        // Дублируем треугольники обратной стороной для двусторонности
        if (doubleSided)
        {
            int count = tris.Count;
            for (int i = 0; i < count; i += 3)
            {
                tris.Add(tris[i + 2]);
                tris.Add(tris[i + 1]);
                tris.Add(tris[i]);
            }
        }

        var mesh = new Mesh { name = name + "_jaggedEdge" };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        // Создаём дочерний объект
        var go = new GameObject("_JaggedEdge");
        go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;

        var mr        = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = overrideMaterial != null
            ? overrideMaterial
            : GetComponent<MeshRenderer>()?.sharedMaterial;

#if UNITY_EDITOR
        SaveMesh(mesh);
        EditorUtility.SetDirty(go);
        EditorUtility.SetDirty(this);
        EditorSceneManager.MarkSceneDirty(go.scene);
#endif
    }

    [ContextMenu("Remove Jagged Edges")]
    public void Remove() => RemoveOld();

    private void RemoveOld()
    {
        var old = transform.Find("_JaggedEdge");
        if (old != null) DestroyImmediate(old.gameObject);
    }

#if UNITY_EDITOR
    private void SaveMesh(Mesh mesh)
    {
        string scenePath = System.IO.Path.GetDirectoryName(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
        string dir  = scenePath + "/JaggedMeshes";
        System.IO.Directory.CreateDirectory(dir);
        string path = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{mesh.name}.asset");
        AssetDatabase.CreateAsset(mesh, path);
        AssetDatabase.SaveAssets();
        Debug.Log($"[PlatformJaggedEdge] → {path}", mesh);
    }
#endif
}
