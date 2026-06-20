using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 테이블(상판 + 다리)을 초기 '자세'로 되돌린다. (씬 재로드 아님)
/// - 시작 시 상판(이 오브젝트)과 모든 다리 Rigidbody의 초기 위치/회전 + 상판 기준 상대 배치를 기록.
/// - '1' 키 → ResetPoseInPlace(): 처음 위치로 가지 않고 '현재 위치/방향'에서 기울기·다리만 똑바로 복원.
/// - ResetAll(): 처음(지면 스냅된) 위치로 텔레포트. 용암 사망 리스폰 등에서 호출.
/// 상판(Table) GameObject에 부착한다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class SceneReset : MonoBehaviour
{
    [Tooltip("리셋 직후 다리를 수직으로 강제 고정하는 시간(초). 안착 보정용.")]
    public float verticalHoldSeconds = 0.4f;

    Rigidbody[] bodies;
    Vector3[] initPos;
    Quaternion[] initRot;
    LegController[] legs;
    // '1' 제자리 자세복원용: 각 강체의 상판(bodies[0]) 기준 상대 배치 + 상판의 지면 위 높이.
    Vector3[] relPos;
    Quaternion[] relRot;
    float boardClearance;

    void Start()
    {
        var list = new List<Rigidbody>();
        var self = GetComponent<Rigidbody>();
        if (self != null) list.Add(self);

        legs = FindObjectsByType<LegController>(FindObjectsSortMode.None);
        foreach (var l in legs)
        {
            var r = l.GetComponent<Rigidbody>();
            if (r != null && !list.Contains(r)) list.Add(r);
        }

        bodies = list.ToArray();
        initPos = new Vector3[bodies.Length];
        initRot = new Quaternion[bodies.Length];
        for (int i = 0; i < bodies.Length; i++)
        {
            initPos[i] = bodies[i].transform.position;
            initRot[i] = bodies[i].transform.rotation;
        }

        SnapInitialToGround();

        // 상판 기준 상대 배치 기록(제자리 자세복원에서 이 배치를 그대로 재구성).
        relPos = new Vector3[bodies.Length];
        relRot = new Quaternion[bodies.Length];
        Quaternion invBoard = Quaternion.Inverse(initRot[0]);
        for (int i = 0; i < bodies.Length; i++)
        {
            relPos[i] = invBoard * (initPos[i] - initPos[0]);
            relRot[i] = invBoard * initRot[i];
        }
        boardClearance = initPos[0].y - GroundYAt(initPos[0]);
    }

    /// <summary>해당 XZ 지점의 지면 높이(테이블 자신 제외). 없으면 입력 y. (높은 곳에서 내리쏴 도랑 안에서도 정확.)</summary>
    float GroundYAt(Vector3 p)
    {
        var hits = Physics.RaycastAll(new Vector3(p.x, 500f, p.z), Vector3.down, 1000f, ~0, QueryTriggerInteraction.Ignore);
        float gy = float.NegativeInfinity; bool found = false;
        foreach (var h in hits)
        {
            if (h.collider.transform.root == transform.root) continue;
            if (h.point.y > gy) { gy = h.point.y; found = true; }
        }
        return found ? gy : p.y;
    }

    /// <summary>
    /// 시작 위치가 지면보다 한참 위(공중 스폰)면, 초기 위치 전체를 아래 지면으로 스냅한다.
    /// → '1' 리셋/리스폰 시 공중이 아니라 "그 위치 아래 땅"에 똑바로 선 상태로 생성.
    /// 이미 지면 위에서 시작하는 씬(Stage 등)은 낙차가 거의 없어 그대로 둔다.
    /// </summary>
    void SnapInitialToGround()
    {
        Vector3 boardPos = transform.position;
        var hits = Physics.RaycastAll(boardPos + Vector3.up * 2f, Vector3.down, 1000f, ~0, QueryTriggerInteraction.Ignore);
        float groundY = float.NegativeInfinity; bool found = false;
        foreach (var h in hits)
        {
            if (h.collider.transform.root == transform.root) continue; // 테이블 자신 제외
            if (h.point.y > groundY) { groundY = h.point.y; found = true; }
        }
        if (!found) return;

        float standY = groundY + 0.05f;     // 보드 높이 = 다리 바닥 높이 → 지면에 살짝 띄워 안착
        float drop = boardPos.y - standY;
        if (drop <= 2f) return;             // 이미 지면 근처에서 시작 → 스냅 안 함

        for (int i = 0; i < initPos.Length; i++) initPos[i].y -= drop;
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.digit1Key.wasPressedThisFrame)
            ResetPoseInPlace();
    }

    /// <summary>
    /// '1' 키: 처음 위치로 리스폰하지 않고 — 지금 있는 자리(XZ)·바라보는 방향(yaw)은 유지한 채
    /// 기울기/다리만 초기 상태로 똑바로 세운다.
    /// </summary>
    public void ResetPoseInPlace()
    {
        if (bodies == null || bodies.Length == 0 || bodies[0] == null) { ResetAll(); return; }
        var b0 = bodies[0].transform.position;
        ResetPoseInternal(new Vector2(b0.x, b0.z));
    }

    /// <summary>지정한 XZ 지점의 땅 위에 똑바로 선 자세로 복원. (용암 사망 → 가장 가까운 안전한 땅 리스폰용.)</summary>
    public void ResetPoseAtXZ(float x, float z)
    {
        if (bodies == null || bodies.Length == 0 || bodies[0] == null) { ResetAll(); return; }
        ResetPoseInternal(new Vector2(x, z));
    }

    void ResetPoseInternal(Vector2 xz)
    {
        var board = bodies[0];
        // 바라보는 '카메라 방향'으로 테이블 forward 를 맞춤 → 리셋 후 항상 WASD 다리(Leg_FL)가 화면 먼왼쪽.
        // (기울어진 상태의 eulerAngles.y 는 부정확해 다리-카메라 매핑이 틀어지므로 카메라 기준으로 정렬한다.)
        float yaw = board.transform.eulerAngles.y;         // 폴백(카메라 없을 때)
        var cam = Camera.main;
        if (cam != null)
        {
            Vector3 f = cam.transform.forward; f.y = 0f;
            if (f.sqrMagnitude > 1e-4f) yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
        }
        // 초기 자세에서 yaw 성분만 제거한 '기울기'를 그 위에 얹어 똑바로 세움.
        Quaternion tilt = Quaternion.Inverse(Quaternion.Euler(0f, initRot[0].eulerAngles.y, 0f)) * initRot[0];
        Quaternion newBoardRot = Quaternion.Euler(0f, yaw, 0f) * tilt;
        Vector3 newBoardPos = new Vector3(xz.x, GroundYAt(new Vector3(xz.x, 0f, xz.y)) + boardClearance, xz.y);

        for (int i = 0; i < bodies.Length; i++)
        {
            var b = bodies[i];
            if (b == null) continue;
            Vector3 p = newBoardPos + newBoardRot * relPos[i];
            Quaternion r = newBoardRot * relRot[i];
            b.position = p;
            b.rotation = r;
            b.transform.SetPositionAndRotation(p, r);
            b.linearVelocity = Vector3.zero;
            b.angularVelocity = Vector3.zero;
        }
        if (legs != null)
            foreach (var l in legs)
                if (l != null) { l.ResetState(); l.HoldVertical(verticalHoldSeconds); }

        var giraffe = FindAnyObjectByType<GiraffeMode>();
        if (giraffe != null) giraffe.ResetLegs();
    }

    /// <summary>테이블을 초기(지면 스냅된) 위치·자세로 되돌린다. 용암 사망 리스폰 등에서도 호출.</summary>
    public void ResetAll()
    {
        for (int i = 0; i < bodies.Length; i++)
        {
            var b = bodies[i];
            if (b == null) continue;
            // 동적 Rigidbody 는 물리 위치가 우선 → rb.position/rotation 으로 직접 텔레포트(트랜스폼도 동기).
            b.position = initPos[i];
            b.rotation = initRot[i];
            b.transform.SetPositionAndRotation(initPos[i], initRot[i]);
            b.linearVelocity = Vector3.zero;
            b.angularVelocity = Vector3.zero;
        }
        if (legs != null)
            foreach (var l in legs)
                if (l != null)
                {
                    l.ResetState();
                    l.HoldVertical(verticalHoldSeconds); // 리셋 직후 잠깐 수직 고정 → 안착 후 해제
                }

        // 지라프 모드로 늘어난 다리 길이도 초기화
        var giraffe = FindAnyObjectByType<GiraffeMode>();
        if (giraffe != null) giraffe.ResetLegs();
    }
}
