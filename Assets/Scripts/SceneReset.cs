using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// '1' 키를 누르면 테이블(상판 + 다리)을 초기 상태로 되돌린다. (씬 재로드 아님)
/// - 시작 시 상판(이 오브젝트)과 모든 다리 Rigidbody의 초기 위치/회전을 기록.
/// - '1' 입력 시: 위치/회전 복원, 속도 0, 각 다리의 내부 상태(yaw/힌지/잠금) 리셋.
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
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.digit1Key.wasPressedThisFrame)
            ResetAll();
    }

    void ResetAll()
    {
        for (int i = 0; i < bodies.Length; i++)
        {
            var b = bodies[i];
            if (b == null) continue;
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
    }
}
