using UnityEngine;

/// <summary>
/// 씬의 모든 PressButton이 한 번이라도 동시에 눌리면 "CLEAR"를 표시한다.
/// - GUI가 아니라 3D 텍스트(TextMesh)를 카메라 앞에 띄워 화면 중앙에 보이게 한다.
/// - 나타날 때 크기가 0에서 서서히 커진다.
/// - 한 번 클리어되면 계속 유지(버튼을 떼도 사라지지 않음, latch).
/// - SampleScene 파일은 건드리지 않고 런타임에 생성.
/// </summary>
public class ClearChecker : MonoBehaviour
{
    [Tooltip("표시 문구")]
    public string clearMessage = "CLEAR";
    [Tooltip("커지는 데 걸리는 시간(초)")]
    public float growDuration = 1.2f;
    [Tooltip("카메라 앞 거리")]
    public float distance = 2.5f;
    [Tooltip("글자 크기(월드). 크게 보이려면 키우기")]
    public float characterSize = 0.5f;

    PressButton[] buttons;
    bool cleared;
    Transform textTf;
    float t;

    void Start()
    {
        buttons = FindObjectsByType<PressButton>(FindObjectsSortMode.None);
    }

    void Update()
    {
        if (!cleared)
        {
            if (buttons == null || buttons.Length == 0) return;
            foreach (var b in buttons)
                if (b == null || !b.IsPressed) return;   // 하나라도 안 눌리면 대기
            cleared = true;
            SpawnText();
            return;
        }

        // 서서히 커지는 연출
        if (textTf != null && t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, growDuration);
            float s = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            textTf.localScale = Vector3.one * s;
        }
    }

    void SpawnText()
    {
        var cam = Camera.main;
        var go = new GameObject("ClearText3D");
        var tm = go.AddComponent<TextMesh>();
        tm.text = clearMessage;
        tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        go.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;
        tm.fontSize = 100;
        tm.characterSize = characterSize;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = Color.yellow;

        textTf = go.transform;
        if (cam != null)
        {
            textTf.SetParent(cam.transform, false);     // 카메라에 붙여 항상 화면 중앙 앞
            textTf.localPosition = new Vector3(0f, 0f, distance);
            textTf.localRotation = Quaternion.identity;
        }
        else
        {
            textTf.position = new Vector3(0f, 3f, 0f);
        }
        textTf.localScale = Vector3.zero;  // 0에서 시작 → 서서히 커짐
    }
}
