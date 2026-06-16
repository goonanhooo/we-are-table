using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

/// <summary>
/// 시작 화면. 배경 씬(SampleScene)을 additive로 보여주고, 스테이지 선택 버튼(1/2/3)을 표시한다.
/// 버튼 클릭 시 해당 스테이지 씬을 단일 로드로 시작한다.
/// 클릭은 EventSystem 없이 마우스 위치로 직접 감지. 커서는 항상 보이게 강제.
/// </summary>
public class StartMenu : MonoBehaviour
{
    [Tooltip("배경으로 보여줄 씬")]
    public string backgroundScene = "SampleScene";

    [Tooltip("시작 화면 카메라(이 카메라만 유지)")]
    public Camera flyoverCamera;

    [Header("Stage Buttons")]
    public RectTransform stage1Button;
    public RectTransform stage2Button;
    public RectTransform stage3Button;
    public string stage1Scene = "Stage1";
    public string stage2Scene = "Stage2";
    public string stage3Scene = "SampleScene";

    void Start()
    {
        StartCoroutine(LoadBackground());
    }

    IEnumerator LoadBackground()
    {
        if (!SceneManager.GetSceneByName(backgroundScene).isLoaded)
        {
            var op = SceneManager.LoadSceneAsync(backgroundScene, LoadSceneMode.Additive);
            while (op != null && !op.isDone) yield return null;
        }
        yield return null;

        foreach (var cam in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            if (cam != flyoverCamera) cam.enabled = false;
        foreach (var al in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
            if (flyoverCamera == null || al.gameObject != flyoverCamera.gameObject) al.enabled = false;
        foreach (var lc in FindObjectsByType<LegController>(FindObjectsSortMode.None)) lc.enabled = false;
        foreach (var sr in FindObjectsByType<SceneReset>(FindObjectsSortMode.None)) sr.enabled = false;
        foreach (var cc in FindObjectsByType<ClearChecker>(FindObjectsSortMode.None)) cc.enabled = false;
        foreach (var rb in FindObjectsByType<Rigidbody>(FindObjectsSortMode.None)) rb.isKinematic = true;

        Unlock();
    }

    void Unlock()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update()
    {
        Unlock();
        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

        Vector2 p = mouse.position.ReadValue();
        if (Hit(stage1Button, p)) Load(stage1Scene);
        else if (Hit(stage2Button, p)) Load(stage2Scene);
        else if (Hit(stage3Button, p)) Load(stage3Scene);
    }

    bool Hit(RectTransform r, Vector2 p)
    {
        return r != null && RectTransformUtility.RectangleContainsScreenPoint(r, p, null);
    }

    void Load(string scene)
    {
        SceneManager.LoadScene(scene, LoadSceneMode.Single);
    }
}
