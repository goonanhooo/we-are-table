using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 지라프 터치버튼 4개가 '동시에' 밟혀(Pressed) 있는 순간 → 문(둥근 뚜껑)이 경첩처럼 열리는 애니메이션 +
/// 건물 구석에서 전체를 비추며 문 열리는 모습을 보여주는 컷씬. 그 뒤 테이블이 구멍으로 떨어지면 빙산 씬으로 전환.
/// 버튼은 모멘터리(떼면 빨강)라, 네 발로 네 버튼을 동시에 밟고 있어야만 발동한다.
/// </summary>
public class GiraffeGate : MonoBehaviour
{
    public GiraffeTouchButton[] buttons;

    [Header("문(뚜껑)")]
    public Transform lid;
    [Tooltip("경첩 회전 각도(도).")]
    public float doorOpenAngle = 105f;
    public float doorSeconds = 1.3f;

    [Header("컷씬")]
    [Tooltip("문 열림 보여준 뒤 멈춰서 보여주는 시간(초).")]
    public float cutsceneHold = 1.0f;
    [Tooltip("구석 카메라 위치(Warehouse 로컬). 높은 구석에서 전체+문을 봄.")]
    public Vector3 cornerLocal = new Vector3(-22f, 24f, 30f);

    [Header("낙하 → 다음 씬")]
    public string nextScene = "IceMountain";
    public float fallTriggerY = 5f;
    public float holeRadius = 2.5f;

    Transform board;
    Vector2 holeXZ;
    bool opened, transitioned;

    void Start()
    {
        if (lid != null) holeXZ = new Vector2(lid.position.x, lid.position.z);
        var b = GameObject.Find("Table");
        if (b != null) board = b.transform;
    }

    void Update()
    {
        if (!opened && buttons != null && buttons.Length > 0)
        {
            bool all = true;
            foreach (var b in buttons) if (b == null || !b.Pressed) { all = false; break; }   // ★동시에 전부 Pressed
            if (all) { opened = true; StartCoroutine(OpenCutscene()); }
        }

        if (opened && !transitioned && board != null && board.position.y < fallTriggerY
            && new Vector2(board.position.x - holeXZ.x, board.position.z - holeXZ.y).sqrMagnitude < holeRadius * holeRadius)
        {
            TryFall();
        }
    }

    public bool Opened => opened;

    /// <summary>구멍 트리거(GateFallTrigger)가 테이블 진입 시 호출 — 문 열린 뒤에만 전환.</summary>
    public void TryFall()
    {
        if (!opened || transitioned) return;
        transitioned = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene(nextScene, LoadSceneMode.Single);
    }

    IEnumerator OpenCutscene()
    {
        var cam = Camera.main;
        var w = GameObject.Find("Warehouse");
        CameraFollow follow = cam != null ? cam.GetComponent<CameraFollow>() : null;

        // 1) 건물 구석(높은 곳)으로 컷 — 전체 + 문 보여줌
        if (cam != null && w != null)
        {
            if (follow != null) follow.enabled = false;
            Vector3 camPos = w.transform.TransformPoint(cornerLocal);
            Vector3 lookAt = lid != null ? lid.position : w.transform.position;
            cam.transform.SetPositionAndRotation(camPos, Quaternion.LookRotation((lookAt - camPos).normalized, Vector3.up));
        }
        yield return new WaitForSeconds(0.5f);

        // 2) 문(뚜껑) 경첩 회전으로 열림
        if (lid != null)
        {
            Vector3 axis = w != null ? w.transform.right : Vector3.right;
            Vector3 fwd = w != null ? w.transform.forward : Vector3.forward;
            float lidR = lid.lossyScale.x * 0.5f;
            Vector3 pivot = lid.position + fwd * lidR;       // +forward 모서리에 경첩 → 반대편이 구멍 속으로 떨어지며 열림
            float done = 0f;
            while (done < doorOpenAngle)
            {
                float step = Mathf.Min(doorOpenAngle - done, doorOpenAngle * Time.deltaTime / Mathf.Max(0.05f, doorSeconds));
                lid.RotateAround(pivot, axis, -step);        // ★아래(구멍 속)로 열림
                done += step;
                yield return null;
            }
        }

        // 3) 잠깐 멈춰서 보여준 뒤 카메라 복귀
        yield return new WaitForSeconds(cutsceneHold);
        if (follow != null) follow.enabled = true;
    }
}
