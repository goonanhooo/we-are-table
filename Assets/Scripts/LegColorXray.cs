using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 4개의 다리(Leg_FR/FL/BR/BL) 박스의 12개 모서리를 각각 다른 색의 선명한 선으로 그린다.
/// 모서리마다 독립된 2점 LineRenderer를 써서 한 붓 그리기 경로에서 생기던 "꼬임"을 없앴다.
/// 선은 X-ray 셰이더(ZTest Always)로 그려져 몸통을 통과해 항상 화면에 보인다. 메뉴에서 on/off 토글.
/// </summary>
public class LegColorXray : MonoBehaviour
{
    [Tooltip("시작 시 켜진 상태로 둘지")]
    public bool startOn = false;

    [Tooltip("선 굵기(월드 단위)")]
    public float lineWidth = 0.01f;

    [Tooltip("FR, FL, BR, BL 순서의 다리 색 (검정/하양 제외)")]
    public Color[] legColors = new Color[]
    {
        new Color(1f,    0.20f, 0.20f), // Leg_FR 빨강
        new Color(0.20f, 0.85f, 0.30f), // Leg_FL 초록
        new Color(0.25f, 0.55f, 1f),    // Leg_BR 파랑
        new Color(1f,    0.80f, 0.15f), // Leg_BL 노랑
    };

    static readonly string[] LegNames = { "Leg_FR", "Leg_FL", "Leg_BR", "Leg_BL" };

    // 큐브 8코너 인덱스로 표현한 12개 모서리(각각 독립 선분).
    static readonly int[,] Edges =
    {
        {0,1},{1,2},{2,3},{3,0}, // 아랫면
        {4,5},{5,6},{6,7},{7,4}, // 윗면
        {0,4},{1,5},{2,6},{3,7}, // 수직
    };

    // 선분 단위 병렬 리스트
    readonly List<Transform> _legOf = new List<Transform>();
    readonly List<Vector3> _aLocal = new List<Vector3>();
    readonly List<Vector3> _bLocal = new List<Vector3>();
    readonly List<LineRenderer> _lines = new List<LineRenderer>();
    readonly Vector3[] _buf = new Vector3[2];
    Material _mat;
    bool _on;

    public bool IsOn => _on;

    void Start()
    {
        var shader = Shader.Find("Custom/XrayLine");
        _mat = new Material(shader != null ? shader : Shader.Find("Sprites/Default"));
        _mat.hideFlags = HideFlags.DontSave;

        for (int i = 0; i < LegNames.Length; i++)
        {
            var leg = FindLeg(LegNames[i]);
            if (leg == null) continue;

            // 다리 메시의 로컬 바운드(보통 단위 큐브)로 8코너 산출.
            Vector3 center = Vector3.zero;
            Vector3 ext = new Vector3(0.5f, 0.5f, 0.5f);
            var mf = leg.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                center = mf.sharedMesh.bounds.center;
                ext = mf.sharedMesh.bounds.extents;
            }
            var corners = Corners(center, ext);
            Color col = i < legColors.Length ? legColors[i] : Color.magenta;

            var parent = new GameObject("Xray_" + LegNames[i]);
            parent.transform.SetParent(transform, false);

            for (int e = 0; e < Edges.GetLength(0); e++)
            {
                var go = new GameObject("Edge" + e);
                go.transform.SetParent(parent.transform, false);

                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.sharedMaterial = _mat;
                lr.widthMultiplier = lineWidth;
                lr.numCapVertices = 0;
                lr.numCornerVertices = 0;
                lr.alignment = LineAlignment.View;
                lr.textureMode = LineTextureMode.Stretch;
                lr.shadowCastingMode = ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.lightProbeUsage = LightProbeUsage.Off;
                lr.reflectionProbeUsage = ReflectionProbeUsage.Off;
                lr.startColor = col;
                lr.endColor = col;
                lr.positionCount = 2;

                _legOf.Add(leg);
                _aLocal.Add(corners[Edges[e, 0]]);
                _bLocal.Add(corners[Edges[e, 1]]);
                _lines.Add(lr);
            }
        }

        SetOn(startOn);
    }

    void LateUpdate()
    {
        if (!_on) return;
        for (int i = 0; i < _lines.Count; i++)
        {
            var leg = _legOf[i];
            if (leg == null) continue;
            _buf[0] = leg.TransformPoint(_aLocal[i]);
            _buf[1] = leg.TransformPoint(_bLocal[i]);
            _lines[i].SetPositions(_buf);
        }
    }

    public void SetOn(bool value)
    {
        _on = value;
        foreach (var lr in _lines)
            if (lr != null) lr.enabled = value;
        if (value) LateUpdate(); // 켜는 즉시 위치 갱신
    }

    public void Toggle() => SetOn(!_on);

    static Vector3[] Corners(Vector3 c, Vector3 e)
    {
        return new[]
        {
            c + new Vector3(-e.x, -e.y, -e.z), // 0
            c + new Vector3( e.x, -e.y, -e.z), // 1
            c + new Vector3( e.x, -e.y,  e.z), // 2
            c + new Vector3(-e.x, -e.y,  e.z), // 3
            c + new Vector3(-e.x,  e.y, -e.z), // 4
            c + new Vector3( e.x,  e.y, -e.z), // 5
            c + new Vector3( e.x,  e.y,  e.z), // 6
            c + new Vector3(-e.x,  e.y,  e.z), // 7
        };
    }

    Transform FindLeg(string legName)
    {
        // 프리팹 인스턴스: 자식 계층에서 탐색
        var inChildren = FindInChildren(transform, legName);
        if (inChildren != null) return inChildren;
        // 씬 복사본: 다리가 별도 루트에 있을 수 있으니 전역 검색
        var go = GameObject.Find(legName);
        return go != null ? go.transform : null;
    }

    static Transform FindInChildren(Transform root, string n)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == n) return t;
        return null;
    }
}
