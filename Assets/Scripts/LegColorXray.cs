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
    const float FillAlpha = 0.35f;    // 채움 투명도(약간 투명) — 테두리는 진한 같은 색
    const float BarHalfW = 0.045f;    // 스핀 직사각형 반폭
    readonly GameObject[] _arrowGO = new GameObject[4];   // 앞뒤 스윙 = 삼각형(채움+테두리)
    readonly GameObject[] _spinGO = new GameObject[4];    // 좌우 스핀 = 직사각형(채움+테두리)
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

            // ★입력 방향 화살표(누르는 동안만) — 반투명 채움 + 같은 색 진한 테두리(와이어와 구분되는 룩)
            var triPts = new Vector3[] { new Vector3(-ArrowHalf, 0f, 0f), new Vector3(ArrowHalf, 0f, 0f), new Vector3(0f, 0f, ArrowLen) };
            _arrowGO[i] = MakeFilledShape("Xray_MoveArrow_" + LegNames[i], triPts, col);

            // ★좌우 스핀: 다리 옆면과 평행하게 도는 얇은 직사각형(채움+테두리)
            var rectPts = new Vector3[]
            {
                new Vector3(-ArrowDist, 0f, -BarHalfW), new Vector3(ArrowDist, 0f, -BarHalfW),
                new Vector3(ArrowDist, 0f, BarHalfW), new Vector3(-ArrowDist, 0f, BarHalfW),
            };
            _spinGO[i] = MakeFilledShape("Xray_SpinBar_" + LegNames[i], rectPts, col);

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

    /// <summary>납작 XZ 다각형(볼록) — 반투명 채움 메시 + 같은 색 진한 테두리 LineRenderer(로컬 좌표).
    /// 기존 X-ray 와이어(선만)와 구분되는 '색칠+윤곽' 룩. 위치/회전은 루트 트랜스폼으로 제어.</summary>
    GameObject MakeFilledShape(string name, Vector3[] pts, Color col)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);

        // 채움(팬 삼각분할, 정점색 = 반투명)
        var tris = new int[(pts.Length - 2) * 3];
        for (int t = 0; t < pts.Length - 2; t++) { tris[t * 3] = 0; tris[t * 3 + 1] = t + 1; tris[t * 3 + 2] = t + 2; }
        var cols = new Color[pts.Length];
        for (int k = 0; k < cols.Length; k++) cols[k] = new Color(col.r, col.g, col.b, FillAlpha);
        var m = new Mesh { hideFlags = HideFlags.DontSave };
        m.vertices = pts;
        m.triangles = tris;
        m.colors = cols;
        m.RecalculateNormals();
        m.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh = m;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = _mat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = LightProbeUsage.Off;
        mr.reflectionProbeUsage = ReflectionProbeUsage.Off;

        // 테두리(진한 같은 색, 로컬 좌표 루프 — 루트 트랜스폼 따라감)
        var ogo = new GameObject("outline");
        ogo.transform.SetParent(go.transform, false);
        var lr = ogo.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.sharedMaterial = _mat;
        lr.widthMultiplier = lineWidth * 1.6f;
        lr.numCapVertices = 0;
        lr.numCornerVertices = 0;
        lr.alignment = LineAlignment.View;
        lr.shadowCastingMode = ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.lightProbeUsage = LightProbeUsage.Off;
        lr.reflectionProbeUsage = ReflectionProbeUsage.Off;
        lr.startColor = col;
        lr.endColor = col;
        lr.positionCount = pts.Length;
        lr.SetPositions(pts);

        go.SetActive(false);
        return go;
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

    // 키를 누르는 동안만 표시(요 프레임 기준):
    //  - 앞뒤(스윙) = 다리 색 삼각형이 요 ±Z 방향을 가리킴
    //  - 좌우(스핀) = 좌/우 화살표 자리를 잇는 '얇은 직사각형 선'(가로 막대) — 다리 옆면과 평행하게 같이 돎
    void UpdateMoveArrows()
    {
        if (_tin == null) _tin = TableInput.Of(this);
        for (int i = 0; i < 4; i++)
        {
            if (_arrowGO[i] == null) continue;
            bool showArrow = false, showBar = false;
            if (_on && _ctrl[i] != null && _tin != null && _legT[i] != null && _yawT[i] != null)
            {
                LegInput inp = _tin.Get(_ctrl[i].controlSet);
                float fw = (inp.up ? 1f : 0f) - (inp.down ? 1f : 0f);
                bool spin = inp.left || inp.right;
                Vector3 mid = _legT[i].TransformPoint(_legC[i]);

                if (fw != 0f)
                {
                    Vector3 dir = _yawT[i].forward * fw;
                    dir.y = 0f;
                    if (dir.sqrMagnitude > 1e-4f)
                    {
                        dir.Normalize();
                        _arrowGO[i].transform.SetPositionAndRotation(mid + dir * ArrowDist, Quaternion.LookRotation(dir));
                        showArrow = true;
                    }
                }
                if (spin && _spinGO[i] != null)
                {
                    // 직사각형: 요 회전 그대로(다리 옆면과 평행) — 다리와 함께 돎
                    _spinGO[i].transform.SetPositionAndRotation(mid, _yawT[i].rotation);
                    showBar = true;
                }
            }
            if (_arrowGO[i].activeSelf != showArrow) _arrowGO[i].SetActive(showArrow);
            if (_spinGO[i] != null && _spinGO[i].activeSelf != showBar) _spinGO[i].SetActive(showBar);
        }
    }

    public void SetOn(bool value)
    {
        _on = value;
        foreach (var lr in _lines)
            if (lr != null) lr.enabled = value;
        if (!value)
        {
            foreach (var g in _arrowGO)
                if (g != null) g.SetActive(false);
            foreach (var g in _spinGO)
                if (g != null) g.SetActive(false);
        }
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
