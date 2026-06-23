using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 조작법 안내 이미지(키보드 + 다리별 색)를 화면에 띄운다. Hallway 컷씬이 끝나는 순간 `Show()` 호출.
/// 페이드인 → 일정 시간(또는 이동키 입력 시) 유지 → 페이드아웃. 씬에 저장된 편집 가능한 오버레이 캔버스.
/// </summary>
public class ControlsGuide : MonoBehaviour
{
    public CanvasGroup group;
    public float fadeIn = 0.5f;
    public float hold = 8f;          // 최대 표시 시간
    public float minShow = 2f;       // 이 시간 전엔 키 입력으로 안 닫힘
    public float fadeOut = 0.8f;

    bool running;

    public void Show()
    {
        if (running) return;
        running = true;
        if (group == null) group = GetComponent<CanvasGroup>();
        gameObject.SetActive(true);
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        for (float t = 0; t < fadeIn; t += Time.deltaTime) { group.alpha = Mathf.Clamp01(t / fadeIn); yield return null; }
        group.alpha = 1f;

        float h = 0f;
        while (h < hold) { if (h >= minShow && AnyMoveKey()) break; h += Time.deltaTime; yield return null; }

        for (float t = 0; t < fadeOut; t += Time.deltaTime) { group.alpha = 1f - Mathf.Clamp01(t / fadeOut); yield return null; }
        group.alpha = 0f;
        gameObject.SetActive(false);
    }

    static bool AnyMoveKey()
    {
        var k = Keyboard.current;
        if (k == null) return false;
        return k.wKey.isPressed || k.aKey.isPressed || k.sKey.isPressed || k.dKey.isPressed
            || k.tKey.isPressed || k.fKey.isPressed || k.gKey.isPressed || k.hKey.isPressed
            || k.iKey.isPressed || k.jKey.isPressed || k.kKey.isPressed || k.lKey.isPressed
            || k.upArrowKey.isPressed || k.downArrowKey.isPressed || k.leftArrowKey.isPressed || k.rightArrowKey.isPressed;
    }
}
