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

    // ★2026-07-14 v8: 입력 방향 화살표 — 색 모드 중 '키를 누르는 동안만' 그 다리 색 납작 삼각형이
    //   다리 옆 공중에 나타나 지금 명령한 이동/회전 방향(앞뒤 스윙=앞/뒤, 좌우 스핀=왼/오른쪽,
    //   동시 입력=대각)을 가리킨다. 손을 떼면 사라짐.
    const float ArrowHalf = 0.06f;    // 삼각형 밑변 반폭(m)
    const float ArrowLen = 0.104f;    // 길이 = 반폭×1.732(정삼각형)
    const float ArrowDist = 0.30f;    // 다리 중심에서 화살표까지 거리(m)
    readonly Transform[] _legT = new Transform[4];
    readonly Vector3[] _legC = new Vector3[4];
    readonly Transform[] _yawT = new Transform[4];        // 다리 요 회전체(방향 기준)
    readonly ArtLegController[] _ctrl = new ArtLegController[4];
    readonly Transform[] _arrowT = new Transform[4];
    readonly MeshRenderer[] _arrowR = new MeshRenderer[4];
    TableInput _tin;

    public bool IsOn => _on;

    void Start()
    {
        // ★멀티 세션이면 자동으로 켜고 선도 굵게 — 누가 어느 다리인지 색으로 바로 보이게.
        var nm = Unity.Netcode.NetworkManager.Singleton;
        if (nm != null && nm.IsListening)
        {
            startOn = true;
            lineWidth = Mathf.Max(lineWidth, 0.025f);
        }

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
            _legT[i] = leg;
            _legC[i] = center;
            _yawT[i] = leg.parent != null && leg.parent.parent != null ? leg.parent.parent : leg;
            _ctrl[i] = leg.parent != null ? leg.parent.GetComponent<ArtLegController>() : null;

            // ★입력 방향 화살표(누르는 동안만 표시) — 납작 정삼각형, 다리 색
            var ago = new GameObject("Xray_MoveArrow_" + LegNames[i]);
            ago.transform.SetParent(transform, false);
            ago.AddComponent<MeshFilter>().sharedMesh = TriMesh(col);
            var amr = ago.AddComponent<MeshRenderer>();
            amr.sharedMaterial = _mat;
            amr.shadowCastingMode = ShadowCastingMode.Off;
            amr.receiveShadows = false;
            amr.lightProbeUsage = LightProbeUsage.Off;
            amr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            amr.enabled = false;
            _arrowT[i] = ago.transform;
            _arrowR[i] = amr;

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

    /// <summary>납작 정삼각형(+Z=꼭짓점, XZ 평면) — 전 정점 다리 색. 셰이더 Cull Off라 양면 표시.</summary>
    Mesh TriMesh(Color c)
    {
        var v = new Vector3[]
        {
            new Vector3(-ArrowHalf, 0f, 0f), new Vector3(ArrowHalf, 0f, 0f), new Vector3(0f, 0f, ArrowLen),
        };
        var tris = new int[] { 0, 1, 2 };
        var cols = new Color[v.Length];
        for (int i = 0; i < cols.Length; i++) cols[i] = c;
        var m = new Mesh { hideFlags = HideFlags.DontSave };
        m.vertices = v;
        m.triangles = tris;
        m.colors = cols;
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
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
        UpdateMoveArrows();
    }

    // 키를 누르는 동안만: 명령한 이동/회전 방향으로 다리 색 삼각형 표시(요 프레임 기준, 수평).
    // 앞뒤(스윙) = 요 +Z/-Z, 좌우(스핀) = 요 -X/+X(왼/오른쪽), 동시 입력 = 대각.
    void UpdateMoveArrows()
    {
        if (_tin == null) _tin = TableInput.Of(this);
        for (int i = 0; i < 4; i++)
        {
            if (_arrowR[i] == null) continue;
            bool show = false;
            if (_on && _ctrl[i] != null && _tin != null && _legT[i] != null && _yawT[i] != null)
            {
                LegInput inp = _tin.Get(_ctrl[i].controlSet);
                float fw = (inp.up ? 1f : 0f) - (inp.down ? 1f : 0f);
                float sd = (inp.right ? 1f : 0f) - (inp.left ? 1f : 0f);
                if (fw != 0f || sd != 0f)
                {
                    Vector3 dir = _yawT[i].forward * fw + _yawT[i].right * sd;
                    dir.y = 0f;
                    if (dir.sqrMagnitude > 1e-4f)
                    {
                        dir.Normalize();
                        Vector3 mid = _legT[i].TransformPoint(_legC[i]);
                        _arrowT[i].SetPositionAndRotation(mid + dir * ArrowDist, Quaternion.LookRotation(dir));
                        show = true;
                    }
                }
            }
            if (_arrowR[i].enabled != show) _arrowR[i].enabled = show;
        }
    }

    public void SetOn(bool value)
    {
        _on = value;
        foreach (var lr in _lines)
            if (lr != null) lr.enabled = value;
        if (!value)
            foreach (var mr in _arrowR)
                if (mr != null) mr.enabled = false;
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
